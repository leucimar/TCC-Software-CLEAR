using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Clear.Infrastructure.Execucao;

/// <summary>
/// Marcador durável e exclusivamente informativo de análise interrompida.
/// NÃO contém classificações, hashes de arquivos, estado do enumerador ou
/// resultados parciais reutilizáveis. Reinício sempre reprocessa desde o início.
/// </summary>
public sealed record EstadoCheckpointM9(
    int Versao, string Id, string Raiz, string AssinaturaConfiguracao,
    string Estado, long ArquivosLidos, long BytesInformados,
    DateTimeOffset InicioUtc, DateTimeOffset AtualizadoUtc)
{
    public bool PodeReiniciar => Estado is "EmAndamento" or "Cancelada"
        or "Falha" or "Interrompida";
}

/// <summary>
/// Checkpoint atômico com trava interprocesso; nenhum arquivo da raiz analisada
/// é escrito. O checkpoint habilita apenas reinício integral mediante confirmação.
/// </summary>
public sealed class CheckpointAnaliseM9 : IDisposable
{
    public const int VersaoAtual = 1;
    private readonly string _arquivo;
    private readonly FileStream _trava;
    private readonly TimeProvider _relogio;
    private DateTimeOffset _ultimaPersistencia;
    private bool _finalizado;
    private EstadoCheckpointM9 _estado;

    private CheckpointAnaliseM9(string arquivo, FileStream trava,
        EstadoCheckpointM9 estado, TimeProvider relogio)
    {
        _arquivo = arquivo;
        _trava = trava;
        _estado = estado;
        _relogio = relogio;
        _ultimaPersistencia = estado.AtualizadoUtc;
    }

    public static string PastaPadrao => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "CLEAR", "Checkpoints");

    public static string Assinatura(string parametros)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(parametros);
        return Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes("CLEAR-M9-CHECKPOINT-v1\n" + parametros)));
    }

    public static string Caminho(string raiz, string? pastaCheckpoints = null)
    {
        var normalizado = NormalizarRaiz(raiz);
        var chave = OperatingSystem.IsWindows() ? normalizado.ToUpperInvariant() : normalizado;
        var arquivo = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(chave)));
        return Path.Combine(pastaCheckpoints ?? PastaPadrao, arquivo + ".json");
    }

    public static EstadoCheckpointM9? Consultar(
        string raiz, string? pastaCheckpoints = null)
    {
        var arquivo = Caminho(raiz, pastaCheckpoints);
        if (!File.Exists(arquivo)) return null;
        if ((File.GetAttributes(arquivo) & FileAttributes.ReparsePoint) != 0)
            throw new IOException("Checkpoint não pode ser link simbólico.");
        try
        {
            var estado = JsonSerializer.Deserialize<EstadoCheckpointM9>(File.ReadAllText(arquivo))
                ?? throw new InvalidDataException("Checkpoint está vazio.");
            if (estado.Versao != VersaoAtual
                || !string.Equals(estado.Raiz, NormalizarRaiz(raiz),
                    OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase
                                                : StringComparison.Ordinal)
                || !Guid.TryParseExact(estado.Id, "N", out _)
                || estado.ArquivosLidos < 0 || estado.BytesInformados < 0
                || string.IsNullOrWhiteSpace(estado.AssinaturaConfiguracao)
                || estado.AssinaturaConfiguracao.Length != 64
                || !estado.AssinaturaConfiguracao.All(Uri.IsHexDigit)
                || estado.Estado is not ("EmAndamento" or "Cancelada" or "Falha"
                    or "Interrompida"))
                throw new InvalidDataException(
                    "Checkpoint inválido ou de versão incompatível. Execute nova análise.");
            return estado;
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException(
                "Checkpoint ilegível; não pode ser utilizado para reiniciar.", ex);
        }
    }

    /// <summary>Chamar somente após opção explícita de criar marcador.</summary>
    public static CheckpointAnaliseM9 Iniciar(string raiz, string parametros,
        string? pastaCheckpoints = null, TimeProvider? relogio = null)
    {
        var normalizado = NormalizarRaiz(raiz);
        var pasta = Path.GetFullPath(pastaCheckpoints ?? PastaPadrao);
        if (!Directory.Exists(normalizado))
            throw new DirectoryNotFoundException("Diretório analisado não existe.");
        VerificarAncestrais(normalizado);
        if (EstaDentro(pasta, normalizado) || EstaDentro(normalizado, pasta))
            throw new IOException(
                "O checkpoint deve ficar fora do acervo e não pode conter a raiz.");
        VerificarAncestrais(pasta);
        Directory.CreateDirectory(pasta);

        var arquivo = Caminho(normalizado, pasta);
        if ((File.Exists(arquivo) &&
             (File.GetAttributes(arquivo) & FileAttributes.ReparsePoint) != 0)
            || (File.Exists(arquivo + ".lock") &&
             (File.GetAttributes(arquivo + ".lock") & FileAttributes.ReparsePoint) != 0))
            throw new IOException("Checkpoint ou trava não pode ser link simbólico.");
        var trava = new FileStream(arquivo + ".lock", FileMode.OpenOrCreate,
            FileAccess.ReadWrite, FileShare.None);
        try
        {
            var clock = relogio ?? TimeProvider.System;
            var agora = clock.GetUtcNow();
            var estado = new EstadoCheckpointM9(VersaoAtual,
                Guid.NewGuid().ToString("N"), normalizado, Assinatura(parametros),
                "EmAndamento", 0, 0, agora, agora);
            var sessao = new CheckpointAnaliseM9(arquivo, trava, estado, clock);
            sessao.Persistir();
            return sessao;
        }
        catch
        {
            trava.Dispose();
            throw;
        }
    }

    public EstadoCheckpointM9 EstadoAtual => _estado;

    /// <summary>
    /// Guarda avanço observacional a cada 1000 arquivos ou 5 segundos.
    /// Em testes ou parada, forcar=true garante gravação imediata.
    /// </summary>
    public void Atualizar(long arquivos, long bytes, bool forcar = false)
    {
        if (_finalizado) throw new InvalidOperationException("Checkpoint encerrado.");
        if (arquivos < _estado.ArquivosLidos || bytes < _estado.BytesInformados)
            throw new ArgumentOutOfRangeException(nameof(arquivos),
                "Progresso não pode retroceder.");
        var agora = _relogio.GetUtcNow();
        _estado = _estado with { ArquivosLidos = arquivos, BytesInformados = bytes,
            AtualizadoUtc = agora };
        if (forcar || arquivos - _ultimaQuantidadePersistida >= 1000
            || agora - _ultimaPersistencia >= TimeSpan.FromSeconds(5))
            Persistir();
    }

    private long _ultimaQuantidadePersistida;

    public void MarcarCancelada() => Encerrar("Cancelada");
    public void MarcarFalha() => Encerrar("Falha");

    /// <summary>Remove marcador após conclusão integral da análise.</summary>
    public void MarcarConcluida()
    {
        if (_finalizado) throw new InvalidOperationException("Checkpoint encerrado.");
        File.Delete(_arquivo);
        _finalizado = true;
    }

    private void Encerrar(string estado)
    {
        if (_finalizado) return;
        _estado = _estado with { Estado = estado, AtualizadoUtc = _relogio.GetUtcNow() };
        Persistir();
        _finalizado = true;
    }

    private void Persistir()
    {
        var temporario = _arquivo + "." + _estado.Id + ".tmp";
        try
        {
            File.WriteAllText(temporario, JsonSerializer.Serialize(_estado));
            // Mesmo diretório: publicação atômica do arquivo novo.
            File.Move(temporario, _arquivo, overwrite: true);
            _ultimaPersistencia = _estado.AtualizadoUtc;
            _ultimaQuantidadePersistida = _estado.ArquivosLidos;
        }
        finally
        {
            if (File.Exists(temporario)) File.Delete(temporario);
        }
    }

    public void Dispose()
    {
        try
        {
            if (!_finalizado) Encerrar("Interrompida");
        }
        finally
        {
            _trava.Dispose();
        }
    }

    private static string NormalizarRaiz(string raiz)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(raiz);
        if (!Path.IsPathFullyQualified(raiz))
            throw new ArgumentException("O caminho deve ser absoluto.", nameof(raiz));
        return Path.TrimEndingDirectorySeparator(Path.GetFullPath(raiz));
    }

    private static bool EstaDentro(string caminho, string raiz)
    {
        var relativo = Path.GetRelativePath(raiz, caminho);
        return !Path.IsPathRooted(relativo)
            && !string.Equals(relativo, "..", StringComparison.Ordinal)
            && !relativo.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal);
    }

    private static void VerificarAncestrais(string caminho)
    {
        for (DirectoryInfo? pasta = new(caminho); pasta is not null; pasta = pasta.Parent)
        {
            if (pasta.Exists && (pasta.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Caminho não pode conter junction ou link simbólico.");
        }
    }
}
