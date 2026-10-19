using Clear.Domain.Arquivos;

namespace Clear.Application.Duplicatas;

/// <summary>
/// Contrato de acesso somente leitura ao conteúdo, separado da classificação.
/// A implementação deve verificar estabilidade de tamanho/data e rejeitar links.
/// </summary>
public interface ILeitorConteudoDuplicatas
{
    Task<string> CalcularSha256Async(ArquivoInventariado arquivo,
        CancellationToken cancellationToken = default);

    Task<bool> CompararBytesAsync(ArquivoInventariado primeiro,
        ArquivoInventariado segundo,
        CancellationToken cancellationToken = default);
}
