using System.Security;
using Clear.Application.Inventario;
using Clear.Domain.Arquivos;
using Clear.Domain.Duplicatas;

namespace Clear.Application.Duplicatas;

/// <summary>Progresso da fase de conteúdo; não é porcentagem de conclusão.</summary>
public sealed record ProgressoDuplicatas(long CandidatosComHash, long ArquivosCandidatos);

public sealed class ResultadoDuplicatas
{
    public ResultadoDuplicatas(long arquivosInventariados, long candidatosPorTamanho,
        long arquivosComHash, IEnumerable<GrupoDuplicatasExatas> grupos,
        IEnumerable<OcorrenciaInventario> ocorrencias, long totalOcorrencias)
    {
        ArquivosInventariados = arquivosInventariados;
        CandidatosPorTamanho = candidatosPorTamanho;
        ArquivosComHash = arquivosComHash;
        Grupos = Array.AsReadOnly(grupos.ToArray());
        ArquivosEmGrupos = Grupos.Sum(g => (long)g.Arquivos.Count);
        BytesPotencialmenteRedundantes = Grupos.Aggregate(0L,
            (total, grupo) => checked(total + grupo.BytesPotencialmenteRedundantes));
        Ocorrencias = Array.AsReadOnly(ocorrencias.ToArray());
        TotalOcorrencias = totalOcorrencias;
    }

    public long ArquivosInventariados { get; }
    public long CandidatosPorTamanho { get; }
    public long ArquivosComHash { get; }
    public IReadOnlyList<GrupoDuplicatasExatas> Grupos { get; }
    public long ArquivosEmGrupos { get; }
    /// <summary>Estimativa teórica de bytes de cópias adicionais; não é economia realizada.</summary>
    public long BytesPotencialmenteRedundantes { get; }
    public IReadOnlyList<OcorrenciaInventario> Ocorrencias { get; }
    public long TotalOcorrencias { get; }
    public bool Incompleto => TotalOcorrencias > 0;
}

/// <summary>
/// Duas fases: 1) inventário só de metadados agrupado por tamanho;
/// 2) SHA-256 dos candidatos e verificação byte a byte antes de formar grupos.
/// Não move, elimina ou modifica arquivos.
/// </summary>
public sealed class DetectarDuplicatasExatas
{
    private const int MaximoOcorrencias = 100;
    private readonly IFonteInventario _fonte;
    private readonly ILeitorConteudoDuplicatas _leitor;

    public DetectarDuplicatasExatas(IFonteInventario fonte, ILeitorConteudoDuplicatas leitor)
    {
        _fonte = fonte ?? throw new ArgumentNullException(nameof(fonte));
        _leitor = leitor ?? throw new ArgumentNullException(nameof(leitor));
    }

    public async Task<ResultadoDuplicatas> ExecutarAsync(string diretorioRaiz,
        IProgress<ProgressoDuplicatas>? progresso = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(diretorioRaiz))
            throw new ArgumentException("Selecione o diretório raiz.", nameof(diretorioRaiz));
        cancellationToken.ThrowIfCancellationRequested();

        var porTamanho = new Dictionary<long, List<ArquivoInventariado>>();
        long arquivosInventariados = 0;

        await foreach (var arquivo in _fonte.EnumerarAsync(diretorioRaiz, cancellationToken)
                           .WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            cancellationToken.ThrowIfCancellationRequested();
            ArgumentNullException.ThrowIfNull(arquivo);
            arquivosInventariados = checked(arquivosInventariados + 1);
            if (!porTamanho.TryGetValue(arquivo.TamanhoBytes, out var lista))
            {
                lista = new List<ArquivoInventariado>();
                porTamanho.Add(arquivo.TamanhoBytes, lista);
            }
            lista.Add(arquivo);
        }

        var fonteComOcorrencias = _fonte as IFonteInventarioComOcorrencias;
        long totalOcorrencias = fonteComOcorrencias?.TotalOcorrencias ?? 0;
        var ocorrencias = new List<OcorrenciaInventario>(
            (fonteComOcorrencias?.Ocorrencias ?? Array.Empty<OcorrenciaInventario>())
            .Take(MaximoOcorrencias));

        void Registrar(string caminho, string fase, Exception ex)
        {
            totalOcorrencias = checked(totalOcorrencias + 1);
            if (ocorrencias.Count < MaximoOcorrencias)
                ocorrencias.Add(new OcorrenciaInventario(caminho, fase, ex.Message));
        }

        var candidatos = porTamanho.Where(x => x.Value.Count >= 2)
            .OrderBy(x => x.Key).ToArray();
        long totalCandidatos = candidatos.Sum(x => (long)x.Value.Count);
        long arquivosComHash = 0;
        var grupos = new List<GrupoDuplicatasExatas>();

        foreach (var faixa in candidatos)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var porHash = new Dictionary<string, List<ArquivoInventariado>>(StringComparer.Ordinal);

            foreach (var arquivo in faixa.Value.OrderBy(x => x.CaminhoCompleto, StringComparer.OrdinalIgnoreCase))
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    var sha = await _leitor.CalcularSha256Async(arquivo, cancellationToken)
                        .ConfigureAwait(false);
                    arquivosComHash = checked(arquivosComHash + 1);
                    if (!porHash.TryGetValue(sha, out var lista))
                    {
                        lista = new List<ArquivoInventariado>();
                        porHash.Add(sha, lista);
                    }
                    lista.Add(arquivo);
                }
                catch (Exception ex) when (Recuperavel(ex))
                {
                    Registrar(arquivo.CaminhoCompleto, "HashNaoCalculado", ex);
                }
                progresso?.Report(new ProgressoDuplicatas(arquivosComHash, totalCandidatos));
            }

            foreach (var hash in porHash.Where(x => x.Value.Count >= 2).OrderBy(x => x.Key))
            {
                // Hash igual é candidato forte, não comprovação definitiva:
                // conferir conteúdo comparando bytes, sem jamais alterar arquivos.
                var particoes = new List<List<ArquivoInventariado>>();
                foreach (var arquivo in hash.Value)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var inserido = false;
                    var teveErro = false;
                    foreach (var particao in particoes)
                    {
                        try
                        {
                            if (await _leitor.CompararBytesAsync(particao[0], arquivo, cancellationToken)
                                    .ConfigureAwait(false))
                            {
                                particao.Add(arquivo);
                                inserido = true;
                                break;
                            }
                        }
                        catch (Exception ex) when (Recuperavel(ex))
                        {
                            Registrar(arquivo.CaminhoCompleto, "ComparacaoInconclusiva", ex);
                            teveErro = true;
                            break;
                        }
                    }
                    if (!inserido && !teveErro)
                        particoes.Add(new List<ArquivoInventariado> { arquivo });
                }

                foreach (var particao in particoes.Where(x => x.Count >= 2))
                    grupos.Add(new GrupoDuplicatasExatas(faixa.Key, hash.Key, particao));
            }
        }

        return new ResultadoDuplicatas(arquivosInventariados, totalCandidatos,
            arquivosComHash, grupos, ocorrencias, totalOcorrencias);
    }

    private static bool Recuperavel(Exception ex) =>
        ex is IOException or UnauthorizedAccessException or SecurityException or InvalidDataException;
}
