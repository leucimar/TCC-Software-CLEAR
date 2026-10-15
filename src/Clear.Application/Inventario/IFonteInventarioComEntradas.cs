using Clear.Domain.Inventario;

namespace Clear.Application.Inventario;

/// <summary>
/// Extensão opcional da fonte original. Produz arquivos e subpastas numa só
/// travessia, mantendo o contrato antigo para fontes de teste do M1–M3.
/// </summary>
public interface IFonteInventarioComEntradas : IFonteInventario
{
    IAsyncEnumerable<EntradaInventariada> EnumerarEntradasAsync(
        string diretorioRaiz,
        CancellationToken cancellationToken = default);
}
