using Clear.Domain.Analise;
using Clear.Domain.Arquivos;

namespace Clear.Application.Inventario;

/// <summary>Quantidade de arquivos identificados em uma categoria; há sobreposição entre categorias.</summary>
public sealed record ContagemCategoria(CategoriaAnalise Categoria, long QuantidadeArquivos);

/// <summary>
/// Resumo e amostra de metadados classificados; não implica indicação de descarte.
/// </summary>
public sealed class ResultadoInventario
{
    public ResultadoInventario(
        long quantidadeArquivos,
        long totalBytes,
        IEnumerable<ArquivoInventariado> amostraArquivos,
        IEnumerable<ResultadoAnaliseArquivo> classificacoesAmostra,
        IEnumerable<ContagemCategoria> contagensCategoria,
        long arquivosComIndicios,
        IEnumerable<OcorrenciaInventario> ocorrencias,
        long totalOcorrencias)
    {
        QuantidadeArquivos = quantidadeArquivos;
        TotalBytes = totalBytes;
        AmostraArquivos = Array.AsReadOnly(amostraArquivos.ToArray());
        ClassificacoesAmostra = Array.AsReadOnly(classificacoesAmostra.ToArray());
        ContagensCategoria = Array.AsReadOnly(contagensCategoria.ToArray());
        ArquivosComIndicios = arquivosComIndicios;
        Ocorrencias = Array.AsReadOnly(ocorrencias.ToArray());
        TotalOcorrencias = totalOcorrencias;
    }

    public long QuantidadeArquivos { get; }
    public long TotalBytes { get; }
    public IReadOnlyList<ArquivoInventariado> AmostraArquivos { get; }
    public IReadOnlyList<ResultadoAnaliseArquivo> ClassificacoesAmostra { get; }
    public IReadOnlyList<ContagemCategoria> ContagensCategoria { get; }
    public long ArquivosComIndicios { get; }
    public IReadOnlyList<OcorrenciaInventario> Ocorrencias { get; }
    public long TotalOcorrencias { get; }
    public bool AmostraLimitada => QuantidadeArquivos > AmostraArquivos.Count;
    public bool Incompleto => TotalOcorrencias > 0;
}

/// <summary>
/// Inventaria e classifica todos os metadados recebidos, guardando só uma amostra
/// da listagem e contagens completas das categorias. Operação somente leitura.
/// </summary>
public sealed class InventariarDiretorio
{
    private readonly IFonteInventario _fonte;
    private readonly int _limiteAmostra;
    private readonly ClassificadorArquivos _classificador;

    public InventariarDiretorio(
        IFonteInventario fonte,
        int limiteAmostra = 1000,
        ClassificadorArquivos? classificador = null)
    {
        _fonte = fonte ?? throw new ArgumentNullException(nameof(fonte));
        if (limiteAmostra < 0 || limiteAmostra > 10000)
            throw new ArgumentOutOfRangeException(nameof(limiteAmostra), "Limite permitido: 0 a 10000.");

        _limiteAmostra = limiteAmostra;
        _classificador = classificador ?? new ClassificadorArquivos();
    }

    public async Task<ResultadoInventario> ExecutarAsync(
        string diretorioRaiz,
        IProgress<ProgressoInventario>? progresso = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(diretorioRaiz))
            throw new ArgumentException("Selecione um diretório raiz.", nameof(diretorioRaiz));

        cancellationToken.ThrowIfCancellationRequested();

        long quantidade = 0;
        long bytes = 0;
        long arquivosComIndicios = 0;

        var amostra = new List<ArquivoInventariado>(Math.Min(_limiteAmostra, 1000));
        var amostraClassificada = new List<ResultadoAnaliseArquivo>(Math.Min(_limiteAmostra, 1000));
        var contagens = new Dictionary<CategoriaAnalise, long>();

        await foreach (var arquivo in _fonte.EnumerarAsync(diretorioRaiz, cancellationToken)
                           .WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            cancellationToken.ThrowIfCancellationRequested();
            ArgumentNullException.ThrowIfNull(arquivo);

            quantidade = checked(quantidade + 1);
            bytes = checked(bytes + arquivo.TamanhoBytes);

            var resultadoArquivo = _classificador.Classificar(arquivo);
            if (resultadoArquivo.Categorias.Count > 0)
                arquivosComIndicios = checked(arquivosComIndicios + 1);

            foreach (var categoria in resultadoArquivo.Categorias)
            {
                contagens.TryGetValue(categoria, out var anterior);
                contagens[categoria] = checked(anterior + 1);
            }

            if (amostra.Count < _limiteAmostra)
            {
                amostra.Add(arquivo);
                amostraClassificada.Add(resultadoArquivo);
            }

            if (quantidade % 100 == 0)
                progresso?.Report(new ProgressoInventario(quantidade, bytes));
        }

        var fonteComOcorrencias = _fonte as IFonteInventarioComOcorrencias;
        var ocorrencias = fonteComOcorrencias?.Ocorrencias ?? Array.Empty<OcorrenciaInventario>();
        var totalOcorrencias = fonteComOcorrencias?.TotalOcorrencias ?? 0;

        progresso?.Report(new ProgressoInventario(quantidade, bytes));

        return new ResultadoInventario(
            quantidade,
            bytes,
            amostra,
            amostraClassificada,
            contagens.OrderBy(x => x.Key).Select(x => new ContagemCategoria(x.Key, x.Value)),
            arquivosComIndicios,
            ocorrencias,
            totalOcorrencias);
    }
}
