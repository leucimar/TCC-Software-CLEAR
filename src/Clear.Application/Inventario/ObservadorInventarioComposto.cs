using Clear.Domain.Analise;
using Clear.Domain.Compatibilidade;
using Clear.Domain.Inventario;

namespace Clear.Application.Inventario;

/// <summary>Encadeia destinos opcionais sem repetir o inventário.</summary>
public sealed class ObservadorInventarioComposto : IObservadorInventario
{
    private readonly IReadOnlyList<IObservadorInventario> _observadores;

    public ObservadorInventarioComposto(params IObservadorInventario[] observadores)
    {
        ArgumentNullException.ThrowIfNull(observadores);
        if (observadores.Any(x => x is null))
            throw new ArgumentException("Observadores nulos são inválidos.", nameof(observadores));
        _observadores = Array.AsReadOnly((IObservadorInventario[])observadores.Clone());
    }

    public async ValueTask RegistrarAsync(EntradaInventariada entrada,
        ResultadoAnaliseArquivo? classificacaoGeral,
        AvaliacaoEntrada avaliacaoCompatibilidade,
        CancellationToken cancellationToken = default)
    {
        foreach (var observador in _observadores)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await observador.RegistrarAsync(
                entrada, classificacaoGeral, avaliacaoCompatibilidade, cancellationToken)
                .ConfigureAwait(false);
        }
    }
}
