using Clear.Domain.Analise;
using Clear.Domain.Compatibilidade;
using Clear.Domain.Inventario;

namespace Clear.Application.Inventario;

/// <summary>
/// Observa entradas já classificadas durante a enumeração original.
/// Implementações podem gravar relatórios em fluxo, sem alterar o acervo.
/// </summary>
public interface IObservadorInventario
{
    ValueTask RegistrarAsync(
        EntradaInventariada entrada,
        ResultadoAnaliseArquivo? classificacaoGeral,
        AvaliacaoEntrada avaliacaoCompatibilidade,
        CancellationToken cancellationToken = default);
}
