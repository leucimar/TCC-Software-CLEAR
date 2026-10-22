using System.Globalization;
using System.Text;
using System.Text.Json;
using Clear.Application.Duplicatas;
using Clear.Application.Inventario;
using Clear.Domain.Analise;
using Clear.Domain.Compatibilidade;
using Clear.Domain.Inventario;

namespace Clear.Infrastructure.Relatorios;

public enum FiltroLinhasRelatorio
{
    TodasAsEntradas,
    ApenasIndiciosGerais,
    ApenasIncompatibilidades,
    ApenasTamanhoPersonalizado,
    ApenasAchados
}

public sealed record RelatoriosGerados(
    string InventarioCsv,
    string ResumoJson,
    string? DuplicatasCsv,
    long LinhasExportadas);

/// <summary>
/// Exportação UTF-8 CSV + resumo JSON com gravação em fluxo e staging temporário.
/// Nunca altera arquivos do acervo. Somente publica nomes finais ao concluir.
/// </summary>
public sealed class ExportadorRelatoriosM5 : IObservadorInventario, IAsyncDisposable
{
    private const string CabecalhoInventario =
        "Tipo;Nome;Caminho;TamanhoBytes;UltimaModificacaoUtc;UltimoAcessoUtc;CategoriasGerais;SituacaoPreAnalise;" +
        "EvidenciasGerais;EstadosDasRegras;JustificativasCompatibilidade;AcimaLimitePessoal";
    private const string CabecalhoDuplicatas =
        "Grupo;Caminho;TamanhoBytes;SHA256;BytesPotencialmenteRedundantesGrupo";

    private readonly string _raiz;
    private readonly string _prefixo;
    private readonly StreamWriter _escritor;
    private readonly FiltroLinhasRelatorio _filtro;
    private readonly DateTimeOffset _inicioUtc;
    private bool _concluido;
    private bool _descartado;
    private long _linhasExportadas;

    private ExportadorRelatoriosM5(string raiz, string destino,
        FiltroLinhasRelatorio filtro, string prefixo, StreamWriter escritor)
    {
        _raiz = raiz;
        _filtro = filtro;
        _prefixo = prefixo;
        _escritor = escritor;
        _inicioUtc = DateTimeOffset.UtcNow;
    }

    public long LinhasExportadas => _linhasExportadas;

    public static ExportadorRelatoriosM5 Iniciar(
        string diretorioRaiz, string diretorioRelatorios, FiltroLinhasRelatorio filtro)
    {
        if (!Enum.IsDefined(filtro))
            throw new ArgumentOutOfRangeException(nameof(filtro));
        if (string.IsNullOrWhiteSpace(diretorioRaiz) ||
            string.IsNullOrWhiteSpace(diretorioRelatorios))
            throw new ArgumentException("A pasta analisada e a pasta dos relatórios são obrigatórias.");

        var raiz = Path.GetFullPath(diretorioRaiz);
        var destino = Path.GetFullPath(diretorioRelatorios);
        if (!Directory.Exists(raiz) || !Directory.Exists(destino))
            throw new DirectoryNotFoundException("A pasta de origem ou de saída não existe.");

        // Não introduzir relatórios dentro do conjunto inventariado, nem por
        // diretório-alvo que contenha junction/reparse point.
        var relativo = Path.GetRelativePath(raiz, destino);
        if (!Path.IsPathRooted(relativo) &&
            !string.Equals(relativo, "..", StringComparison.Ordinal) &&
            !relativo.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw new ArgumentException("Salve os relatórios fora da pasta analisada.");

        for (DirectoryInfo? diretorio = new(destino);
             diretorio is not null; diretorio = diretorio.Parent)
        {
            if ((diretorio.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Pasta de saída contém link/junction; escolha uma pasta física.");
        }

        var prefixo = Path.Combine(destino,
            "CLEAR_M5_" + DateTimeOffset.UtcNow.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture) +
            "_" + Guid.NewGuid().ToString("N"));
        StreamWriter? escritor = null;
        try
        {
            escritor = new StreamWriter(new FileStream(prefixo + "_inventario.csv.part",
                FileMode.CreateNew, FileAccess.Write, FileShare.None,
                bufferSize: 65536, useAsync: true), new UTF8Encoding(true), 65536);
            escritor.WriteLine(CabecalhoInventario);
            return new ExportadorRelatoriosM5(raiz, destino, filtro, prefixo, escritor);
        }
        catch
        {
            escritor?.Dispose();
            ExcluirSeExistir(prefixo + "_inventario.csv.part");
            throw;
        }
    }

    public async ValueTask RegistrarAsync(
        EntradaInventariada entrada, ResultadoAnaliseArquivo? classificacaoGeral,
        AvaliacaoEntrada avaliacaoCompatibilidade,
        CancellationToken cancellationToken = default)
    {
        if (_descartado || _concluido)
            throw new InvalidOperationException("O relatório já foi finalizado.");
        ArgumentNullException.ThrowIfNull(entrada);
        ArgumentNullException.ThrowIfNull(avaliacaoCompatibilidade);
        cancellationToken.ThrowIfCancellationRequested();

        var incompatibilidades = avaliacaoCompatibilidade.Verificacoes
            .Where(x => x.Estado == EstadoVerificacao.IncompatibilidadeDocumentada).ToArray();
        var temIndicios = classificacaoGeral?.Categorias.Count > 0;
        var temIncompatibilidade = incompatibilidades.Length > 0;
        var temAlertaCompatibilidade = avaliacaoCompatibilidade.Verificacoes.Any(x =>
            x.Estado == EstadoVerificacao.Alerta);
        var excede = avaliacaoCompatibilidade.AcimaDoLimitePersonalizado;

        var incluir = _filtro switch
        {
            FiltroLinhasRelatorio.TodasAsEntradas => true,
            FiltroLinhasRelatorio.ApenasIndiciosGerais => temIndicios,
            FiltroLinhasRelatorio.ApenasIncompatibilidades => temIncompatibilidade,
            FiltroLinhasRelatorio.ApenasTamanhoPersonalizado => excede,
            FiltroLinhasRelatorio.ApenasAchados => temIndicios || temIncompatibilidade || excede,
            _ => throw new InvalidOperationException("Filtro desconhecido.")
        };
        if (!incluir)
            return;

        var estados = avaliacaoCompatibilidade.Verificacoes
            .Where(x => x.Estado != EstadoVerificacao.Desabilitada)
            .Select(x => x.CodigoRegra + ":" + x.Estado);
        var motivos = avaliacaoCompatibilidade.Verificacoes
            .Where(x => x.Estado is EstadoVerificacao.IncompatibilidadeDocumentada
                     or EstadoVerificacao.Alerta or EstadoVerificacao.NaoAvaliado)
            .Select(x => x.CodigoRegra + ": " + x.Justificativa);

        var valores = new[]
        {
            entrada.Tipo == TipoEntradaInventario.Arquivo ? "Arquivo" : "Pasta",
            Path.GetFileName(entrada.CaminhoCompleto),
            entrada.CaminhoCompleto,
            entrada.TamanhoBytes?.ToString(CultureInfo.InvariantCulture) ?? "",
            entrada.Arquivo?.UltimaModificacaoUtc?.ToString("O", CultureInfo.InvariantCulture) ?? "",
            entrada.Arquivo?.UltimoAcessoUtc?.ToString("O", CultureInfo.InvariantCulture) ?? "",
            string.Join(" | ", classificacaoGeral?.Categorias.Select(x => x.ToString())
                ?? Enumerable.Empty<string>()),
            temIndicios || temIncompatibilidade || temAlertaCompatibilidade || excede ?
                "Com indicios/alertas" : "Sem alertas detectados (nao significa aprovado)",
            string.Join(" | ", classificacaoGeral?.Evidencias.Select(x => x.Justificativa)
                ?? Enumerable.Empty<string>()),
            string.Join(" | ", estados),
            string.Join(" | ", motivos),
            excede ? "Sim" : "Nao"
        };
        await _escritor.WriteLineAsync(LinhaCsv(valores).AsMemory(), cancellationToken)
            .ConfigureAwait(false);
        _linhasExportadas = checked(_linhasExportadas + 1);
    }

    public async Task<RelatoriosGerados> ConcluirAsync(
        ResultadoInventario inventario, ResultadoDuplicatas? duplicatas,
        IReadOnlyList<string> filtrosGeraisAtivos,
        int? diasTemporais, DateTimeOffset? referenciaTemporalUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(inventario);
        ArgumentNullException.ThrowIfNull(filtrosGeraisAtivos);
        if (_concluido || _descartado)
            throw new InvalidOperationException("Relatórios já concluídos ou descartados.");

        var csv = _prefixo + "_inventario.csv";
        var json = _prefixo + "_resumo.json";
        var csvDuplicatas = duplicatas is null ? null : _prefixo + "_duplicatas.csv";
        var finais = new List<string>();
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            await _escritor.FlushAsync(cancellationToken).ConfigureAwait(false);
            await _escritor.DisposeAsync().ConfigureAwait(false);

            if (csvDuplicatas is not null)
            {
                await using var arquivo = new FileStream(csvDuplicatas + ".part",
                    FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536, useAsync: true);
                await using var escritor = new StreamWriter(arquivo, new UTF8Encoding(true), 65536);
                await escritor.WriteLineAsync(CabecalhoDuplicatas.AsMemory(), cancellationToken)
                    .ConfigureAwait(false);
                var numero = 0;
                foreach (var grupo in duplicatas!.Grupos)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    numero++;
                    foreach (var item in grupo.Arquivos)
                    {
                        var linha = LinhaCsv(new[]
                        {
                            numero.ToString(CultureInfo.InvariantCulture),
                            item.CaminhoCompleto,
                            grupo.TamanhoBytes.ToString(CultureInfo.InvariantCulture),
                            grupo.Sha256Hex,
                            grupo.BytesPotencialmenteRedundantes.ToString(CultureInfo.InvariantCulture)
                        });
                        await escritor.WriteLineAsync(linha.AsMemory(), cancellationToken)
                            .ConfigureAwait(false);
                    }
                }
                await escritor.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            var resumo = new
            {
                Formato = "CLEAR-M5-1",
                InicioUtc = _inicioUtc,
                FimUtc = DateTimeOffset.UtcNow,
                PastaAnalisada = _raiz,
                FiltroLinhas = _filtro.ToString(),
                FiltrosGeraisAtivos = filtrosGeraisAtivos.ToArray(),
                DiasTemporais = diasTemporais,
                CriterioTemporal = "UltimoAcessoUtc (metadado não garantido pelo sistema operacional)",
                ReferenciaTemporalUtc = referenciaTemporalUtc,
                LinhasExportadas = _linhasExportadas,
                Aviso = "Resultados de pre-analise. Nao autoriza descarte nem garante migracao.",
                Inventario = new
                {
                    inventario.QuantidadeArquivos, inventario.QuantidadeDiretorios,
                    inventario.TotalBytes, inventario.ArquivosComIndicios,
                    inventario.ItensComIncompatibilidade,
                    inventario.ArquivosAcimaLimitePersonalizado,
                    inventario.TotalOcorrencias, inventario.Incompleto,
                    Categorias = inventario.ContagensCategoria.Select(x => new
                    {
                        Nome = x.Categoria.ToString(), x.QuantidadeArquivos
                    }).ToArray(),
                    EstadosDasRegras = inventario.ContagensVerificacoes.Select(x => new
                    {
                        x.CodigoRegra, Estado = x.Estado.ToString(), x.Quantidade
                    }).ToArray(),
                    Perfil = inventario.ConfiguracaoCompatibilidade.Perfil?.Ambiente.ToString(),
                    Subtipo = inventario.ConfiguracaoCompatibilidade.Perfil?.Subtipo?.ToString(),
                    RegrasHabilitadas = inventario.ConfiguracaoCompatibilidade.CodigosHabilitados,
                    LimitePessoalBytes = inventario.FiltroPersonalizado?.LimiteBytes,
                    Ocorrencias = inventario.Ocorrencias
                },
                Duplicatas = duplicatas is null ? new
                {
                    Executada = false, Grupos = 0, ArquivosEmGrupos = 0L,
                    Candidatos = 0L, Hashes = 0L, BytesPotencialmenteRedundantes = 0L,
                    Incompleta = false, TotalOcorrencias = 0L
                } : new
                {
                    Executada = true, Grupos = duplicatas.Grupos.Count,
                    ArquivosEmGrupos = duplicatas.ArquivosEmGrupos,
                    Candidatos = duplicatas.CandidatosPorTamanho,
                    Hashes = duplicatas.ArquivosComHash,
                    BytesPotencialmenteRedundantes = duplicatas.BytesPotencialmenteRedundantes,
                    Incompleta = duplicatas.Incompleto,
                    TotalOcorrencias = duplicatas.TotalOcorrencias
                }
            };

            await using (var arquivo = new FileStream(json + ".part",
                FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536, useAsync: true))
            {
                await JsonSerializer.SerializeAsync(arquivo, resumo,
                    new JsonSerializerOptions { WriteIndented = true }, cancellationToken)
                    .ConfigureAwait(false);
                await arquivo.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            cancellationToken.ThrowIfCancellationRequested();
            File.Move(_prefixo + "_inventario.csv.part", csv);
            finais.Add(csv);
            if (csvDuplicatas is not null)
            {
                File.Move(csvDuplicatas + ".part", csvDuplicatas);
                finais.Add(csvDuplicatas);
            }
            File.Move(json + ".part", json);
            finais.Add(json);

            _concluido = true;
            return new RelatoriosGerados(csv, json, csvDuplicatas, _linhasExportadas);
        }
        catch
        {
            foreach (var final in finais)
                ExcluirSeExistir(final);
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_descartado) return;
        _descartado = true;
        await _escritor.DisposeAsync().ConfigureAwait(false);
        ExcluirSeExistir(_prefixo + "_inventario.csv.part");
        ExcluirSeExistir(_prefixo + "_resumo.json.part");
        ExcluirSeExistir(_prefixo + "_duplicatas.csv.part");
    }

    private static void ExcluirSeExistir(string arquivo)
    {
        if (File.Exists(arquivo))
            File.Delete(arquivo);
    }

    /// <summary>
    /// CSV separado por ponto e vírgula, com escape de aspas e neutralização
    /// de fórmulas para abertura em planilhas. Campos não são conteúdo bruto.
    /// </summary>
    internal static string LinhaCsv(IEnumerable<string?> valores) =>
        string.Join(";", valores.Select(Celula));

    private static string Celula(string? texto)
    {
        var valor = texto ?? "";
        if (valor.Length > 0 && "=+-@\t\r\n".Contains(valor[0]))
            valor = "'" + valor;

        return "\"" + valor.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
    }
}
