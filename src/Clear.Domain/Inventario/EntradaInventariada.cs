using Clear.Domain.Arquivos;

namespace Clear.Domain.Inventario;

/// <summary>Entrada de inventário somente leitura. A raiz selecionada não é uma entrada.</summary>
public enum TipoEntradaInventario
{
    Arquivo,
    Diretorio
}

/// <summary>
/// Representa arquivo ou diretório sem consultar o disco.
/// Apenas arquivos possuem tamanho lógico neste contrato.
/// </summary>
public sealed class EntradaInventariada
{
    private EntradaInventariada(string caminhoCompleto, ArquivoInventariado? arquivo)
    {
        CaminhoCompleto = caminhoCompleto;
        Arquivo = arquivo;
        Tipo = arquivo is null ? TipoEntradaInventario.Diretorio : TipoEntradaInventario.Arquivo;
    }

    public static EntradaInventariada DeArquivo(ArquivoInventariado arquivo)
    {
        ArgumentNullException.ThrowIfNull(arquivo);
        return new EntradaInventariada(arquivo.CaminhoCompleto, arquivo);
    }

    public static EntradaInventariada DeDiretorio(string caminhoCompleto)
    {
        if (string.IsNullOrWhiteSpace(caminhoCompleto))
            throw new ArgumentException("O caminho do diretório é obrigatório.", nameof(caminhoCompleto));

        return new EntradaInventariada(caminhoCompleto, null);
    }

    public TipoEntradaInventario Tipo { get; }
    public string CaminhoCompleto { get; }
    public ArquivoInventariado? Arquivo { get; }
    public long? TamanhoBytes => Arquivo?.TamanhoBytes;
}
