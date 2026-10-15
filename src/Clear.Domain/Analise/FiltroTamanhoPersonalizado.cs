namespace Clear.Domain.Analise;

/// <summary>
/// Preferência operacional, independente de compatibilidade do destino.
/// Não substitui o limite oficial e não recomenda descarte.
/// </summary>
public sealed class FiltroTamanhoPersonalizado
{
    public FiltroTamanhoPersonalizado(long limiteBytes)
    {
        if (limiteBytes <= 0)
            throw new ArgumentOutOfRangeException(nameof(limiteBytes), "Informe um valor positivo em bytes.");

        LimiteBytes = limiteBytes;
    }

    public long LimiteBytes { get; }
    public bool Excede(long tamanhoBytes)
    {
        if (tamanhoBytes < 0)
            throw new ArgumentOutOfRangeException(nameof(tamanhoBytes));

        return tamanhoBytes > LimiteBytes;
    }
}
