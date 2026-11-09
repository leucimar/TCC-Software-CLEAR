using System.Security;
using Clear.Application.Inventario;
using Clear.Domain.Arquivos;
using Clear.Domain.Duplicatas;

namespace Clear.Application.Duplicatas;

/// <summary>Progresso da fase de conteúdo; não é porcentagem de conclusão.</summary>
public sealed record ProgressoDuplicatas(long CandidatosComHash, long ArquivosCandidatos,
    long? Processados = null, long Falhas = 0)
{
    public long CandidatosProcessados => Processados ?? CandidatosComHash + Falhas;
}

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
    /// <summary>Limite de leitores SHA-256 simultâneos. Padrão 1 preserva o M4.</summary>
    public int MaximoHashesSimultaneos { get; }
    /// <summary>
    /// Limite defensivo de arquivos indexados em RAM antes da fase de SHA-256.
    /// Se excedido, falha claramente sem produzir resultado incompleto como completo.
    /// </summary>
    public int MaximoArquivosIndexados { get; }

    public DetectarDuplicatasExatas(IFonteInventario fonte, ILeitorConteudoDuplicatas leitor,
        int maximoHashesSimultaneos = 1, int maximoArquivosIndexados = 250_000)
    {
        _fonte = fonte ?? throw new ArgumentNullException(nameof(fonte));
        _leitor = leitor ?? throw new ArgumentNullException(nameof(leitor));
        if (maximoHashesSimultaneos is < 1 or > 8)
            throw new ArgumentOutOfRangeException(nameof(maximoHashesSimultaneos),
                "Limite permitido: de 1 a 8 leitores SHA-256.");
        MaximoHashesSimultaneos = maximoHashesSimultaneos;
        if (maximoArquivosIndexados is < 1 or > 1_000_000)
            throw new ArgumentOutOfRangeException(nameof(maximoArquivosIndexados),
                "O limite deve ficar entre 1 e 1.000.000 arquivos.");
        MaximoArquivosIndexados = maximoArquivosIndexados;
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
            if (arquivosInventariados > MaximoArquivosIndexados)
                throw new InvalidOperationException(
                    $"M4 interrompido: limite de {MaximoArquivosIndexados:N0} arquivos " +
                    "indexados em memória foi ultrapassado. Não há resultado " +
                    "conclusivo de deduplicação. Reduza o acervo ou aumente o limite " +
                    "após avaliação dos recursos do computador.");
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

        // Uma fila global: há somente até MaximoHashesSimultaneos workers,
        // sem uma Task por arquivo ou barreiras entre tamanhos/lotes.
        var fila = candidatos.SelectMany(f => f.Value
            .OrderBy(x => x.CaminhoCompleto, StringComparer.OrdinalIgnoreCase)).ToArray();
        var resultados = new (string? Hash, Exception? Erro)[fila.Length];
        long concluidos = 0;
        var travaProgresso = new object();
        var relogioProgresso = System.Diagnostics.Stopwatch.StartNew();
        await Parallel.ForEachAsync(Enumerable.Range(0, fila.Length),
            new ParallelOptions { MaxDegreeOfParallelism = MaximoHashesSimultaneos,
                CancellationToken = cancellationToken }, async (indice, ct) =>
        {
            try
            {
                resultados[indice] = (await _leitor.CalcularSha256Async(fila[indice], ct)
                    .ConfigureAwait(false), null);
            }
            catch (Exception ex) when (Recuperavel(ex))
            {
                resultados[indice] = (null, ex);
            }
            lock (travaProgresso)
            {
                concluidos++;
                if (resultados[indice].Erro is null) arquivosComHash++;
                // Limitar notificações destinadas à UI sem perder o evento final.
                if (concluidos == 1 || concluidos == totalCandidatos ||
                    relogioProgresso.ElapsedMilliseconds >= 100)
                {
                    progresso?.Report(new ProgressoDuplicatas(arquivosComHash,
                        totalCandidatos, concluidos, concluidos - arquivosComHash));
                    relogioProgresso.Restart();
                }
            }
        }).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        if (totalCandidatos == 0)
            progresso?.Report(new ProgressoDuplicatas(0, 0, 0, 0));

        // Consolidação sequencial determinística, independente da ordem de término.
        var proximo = 0;
        foreach (var faixa in candidatos)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var porHash = new Dictionary<string, List<ArquivoInventariado>>(StringComparer.Ordinal);
            foreach (var arquivo in faixa.Value.OrderBy(x => x.CaminhoCompleto,
                         StringComparer.OrdinalIgnoreCase))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var resultado = resultados[proximo++];
                if (resultado.Erro is not null)
                    Registrar(arquivo.CaminhoCompleto, "HashNaoCalculado", resultado.Erro);
                else
                {
                    if (!porHash.TryGetValue(resultado.Hash!, out var lista))
                        porHash.Add(resultado.Hash!, lista = new List<ArquivoInventariado>());
                    lista.Add(arquivo);
                }
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
