using Clear.Domain.Arquivos;

namespace Clear.Application.Inventario;

/// <summary>Contrato de leitura de inventário; implementação em disco será no M2.</summary>
public interface IFonteInventario
{
    IAsyncEnumerable<ArquivoInventariado> EnumerarAsync(
        string diretorioRaiz,
        CancellationToken cancellationToken = default);
}
