namespace Clear.Application.Inventario;

/// <summary>Totais de arquivos e bytes recebidos de uma fonte.</summary>
public sealed record ResumoInventario(long QuantidadeArquivos, long TotalBytes);

/// <summary>Agrega um inventário sem acessar ou modificar arquivos físicos.</summary>
public sealed class ResumirInventario
{
    private readonly IFonteInventario _fonte;

    public ResumirInventario(IFonteInventario fonte)
    {
        _fonte = fonte ?? throw new ArgumentNullException(nameof(fonte));
    }

    public async Task<ResumoInventario> ExecutarAsync(
        string diretorioRaiz,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(diretorioRaiz))
            throw new ArgumentException("O diretório raiz é obrigatório.", nameof(diretorioRaiz));

        cancellationToken.ThrowIfCancellationRequested();

        long quantidade = 0;
        long totalBytes = 0;
        await foreach (var arquivo in _fonte.EnumerarAsync(diretorioRaiz, cancellationToken)
                           .WithCancellation(cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            ArgumentNullException.ThrowIfNull(arquivo);
            quantidade = checked(quantidade + 1);
            totalBytes = checked(totalBytes + arquivo.TamanhoBytes);
        }

        return new ResumoInventario(quantidade, totalBytes);
    }
}
