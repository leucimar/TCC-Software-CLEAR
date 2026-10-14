using Clear.Domain.Arquivos;

namespace Clear.Application.Inventario;

/// <summary>Resumo completo e amostra limitada de arquivos para exibição.</summary>
public sealed class ResultadoInventario
{
    public ResultadoInventario(
        long quantidadeArquivos,
        long totalBytes,
        IEnumerable<ArquivoInventariado> amostraArquivos,
        IEnumerable<OcorrenciaInventario> ocorrencias,
        long totalOcorrencias)
    {
        QuantidadeArquivos = quantidadeArquivos;
        TotalBytes = totalBytes;
        AmostraArquivos = Array.AsReadOnly(amostraArquivos.ToArray());
        Ocorrencias = Array.AsReadOnly(ocorrencias.ToArray());
        TotalOcorrencias = totalOcorrencias;
    }

    public long QuantidadeArquivos { get; }
    public long TotalBytes { get; }
    public IReadOnlyList<ArquivoInventariado> AmostraArquivos { get; }
    public IReadOnlyList<OcorrenciaInventario> Ocorrencias { get; }
    public long TotalOcorrencias { get; }
    public bool AmostraLimitada => QuantidadeArquivos > AmostraArquivos.Count;
    public bool Incompleto => TotalOcorrencias > 0;
}

/// <summary>
/// Consulta uma fonte sem modificar arquivos; evita acumular o inventário inteiro em RAM.
/// Uma fonte pode comunicar problemas de leitura como ocorrências.
/// </summary>
public sealed class InventariarDiretorio
{
    private readonly IFonteInventario _fonte;
    private readonly int _limiteAmostra;

    public InventariarDiretorio(IFonteInventario fonte, int limiteAmostra = 1000)
    {
        _fonte = fonte ?? throw new ArgumentNullException(nameof(fonte));
        if (limiteAmostra < 0 || limiteAmostra > 10000)
            throw new ArgumentOutOfRangeException(nameof(limiteAmostra), "Limite permitido: 0 a 10000.");
        _limiteAmostra = limiteAmostra;
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
        var amostra = new List<ArquivoInventariado>(Math.Min(_limiteAmostra, 1000));

        await foreach (var arquivo in _fonte.EnumerarAsync(diretorioRaiz, cancellationToken)
                           .WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            cancellationToken.ThrowIfCancellationRequested();
            ArgumentNullException.ThrowIfNull(arquivo);
            quantidade = checked(quantidade + 1);
            bytes = checked(bytes + arquivo.TamanhoBytes);

            if (amostra.Count < _limiteAmostra)
                amostra.Add(arquivo);

            if (quantidade % 100 == 0)
                progresso?.Report(new ProgressoInventario(quantidade, bytes));
        }

        var comOcorrencias = _fonte as IFonteInventarioComOcorrencias;
        var itens = comOcorrencias?.Ocorrencias ?? Array.Empty<OcorrenciaInventario>();
        var total = comOcorrencias?.TotalOcorrencias ?? 0;
        progresso?.Report(new ProgressoInventario(quantidade, bytes));

        return new ResultadoInventario(quantidade, bytes, amostra, itens, total);
    }
}
