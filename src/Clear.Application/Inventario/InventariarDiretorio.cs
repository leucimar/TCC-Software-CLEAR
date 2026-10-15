using Clear.Domain.Analise;
using Clear.Domain.Arquivos;
using Clear.Domain.Inventario;

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
        long totalOcorrencias,
        long quantidadeDiretorios = 0,
        IEnumerable<EntradaInventariada>? amostraDiretorios = null)
    {
        QuantidadeArquivos = quantidadeArquivos;
        TotalBytes = totalBytes;
        AmostraArquivos = Array.AsReadOnly(amostraArquivos.ToArray());
        ClassificacoesAmostra = Array.AsReadOnly(classificacoesAmostra.ToArray());
        ContagensCategoria = Array.AsReadOnly(contagensCategoria.ToArray());
        ArquivosComIndicios = arquivosComIndicios;
        Ocorrencias = Array.AsReadOnly(ocorrencias.ToArray());
        TotalOcorrencias = totalOcorrencias;
        if (quantidadeDiretorios < 0)
            throw new ArgumentOutOfRangeException(nameof(quantidadeDiretorios));

        var subpastas = (amostraDiretorios ?? Array.Empty<EntradaInventariada>()).ToArray();
        if (subpastas.Any(item => item is null || item.Tipo != TipoEntradaInventario.Diretorio))
            throw new ArgumentException("A amostra de diretórios contém uma entrada inválida.", nameof(amostraDiretorios));
        if (subpastas.Length > quantidadeDiretorios)
            throw new ArgumentException("A amostra excede a quantidade de diretórios.", nameof(amostraDiretorios));

        QuantidadeDiretorios = quantidadeDiretorios;
        AmostraDiretorios = Array.AsReadOnly(subpastas);
    }

    public long QuantidadeArquivos { get; }
    public long TotalBytes { get; }
    public IReadOnlyList<ArquivoInventariado> AmostraArquivos { get; }
    public IReadOnlyList<ResultadoAnaliseArquivo> ClassificacoesAmostra { get; }
    public IReadOnlyList<ContagemCategoria> ContagensCategoria { get; }
    public long ArquivosComIndicios { get; }
    public IReadOnlyList<OcorrenciaInventario> Ocorrencias { get; }
    public long TotalOcorrencias { get; }
    /// <summary>Subpastas descobertas; a raiz escolhida não está incluída.</summary>
    public long QuantidadeDiretorios { get; }
    public IReadOnlyList<EntradaInventariada> AmostraDiretorios { get; }
    public bool AmostraDiretoriosLimitada => QuantidadeDiretorios > AmostraDiretorios.Count;
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
        var amostraDiretorios = new List<EntradaInventariada>(Math.Min(_limiteAmostra, 1000));
        long quantidadeDiretorios = 0;
        var contagens = new Dictionary<CategoriaAnalise, long>();

        // Uma única enumeração quando a fonte também fornece subpastas.
        // Fontes antigas, que fornecem apenas arquivos, continuam suportadas.
        if (_fonte is IFonteInventarioComEntradas fonteComEntradas)
        {
            await foreach (var entrada in fonteComEntradas.EnumerarEntradasAsync(diretorioRaiz, cancellationToken)
                               .WithCancellation(cancellationToken).ConfigureAwait(false))
            {
                cancellationToken.ThrowIfCancellationRequested();
                ArgumentNullException.ThrowIfNull(entrada);

                if (entrada.Tipo == TipoEntradaInventario.Diretorio)
                {
                    quantidadeDiretorios = checked(quantidadeDiretorios + 1);
                    if (amostraDiretorios.Count < _limiteAmostra)
                        amostraDiretorios.Add(entrada);
                }
                else
                {
                    RegistrarArquivo(entrada.Arquivo ??
                        throw new InvalidOperationException("Entrada de arquivo sem metadados."));
                }
            }
        }
        else
        {
            await foreach (var arquivo in _fonte.EnumerarAsync(diretorioRaiz, cancellationToken)
                               .WithCancellation(cancellationToken).ConfigureAwait(false))
            {
                cancellationToken.ThrowIfCancellationRequested();
                RegistrarArquivo(arquivo);
            }
        }

        void RegistrarArquivo(ArquivoInventariado arquivo)
        {
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
            totalOcorrencias,
            quantidadeDiretorios,
            amostraDiretorios);
    }
}
