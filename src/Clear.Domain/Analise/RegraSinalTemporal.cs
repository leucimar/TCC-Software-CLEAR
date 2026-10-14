using Clear.Domain.Arquivos;

namespace Clear.Domain.Analise;

/// <summary>
/// Aponta arquivos cuja última modificação precede um limiar escolhido pelo operador.
/// Datas de último acesso não são utilizadas. Não conclui desuso ou descarte.
/// </summary>
public sealed class RegraSinalTemporal : IRegraClassificacao
{
    private readonly DateTimeOffset _referenciaUtc;
    private readonly DateTimeOffset _limiteUtc;

    public RegraSinalTemporal(int diasSemModificacao, DateTimeOffset referencia)
    {
        if (diasSemModificacao is < 1 or > 3650)
            throw new ArgumentOutOfRangeException(nameof(diasSemModificacao),
                "Informe entre 1 e 3650 dias.");

        DiasSemModificacao = diasSemModificacao;
        _referenciaUtc = referencia.ToUniversalTime();
        _limiteUtc = _referenciaUtc.AddDays(-diasSemModificacao);
    }

    public int DiasSemModificacao { get; }
    public DateTimeOffset ReferenciaUtc => _referenciaUtc;
    public DateTimeOffset LimiteUtc => _limiteUtc;

    public EvidenciaAnalise? Avaliar(ArquivoInventariado arquivo)
    {
        ArgumentNullException.ThrowIfNull(arquivo);

        var ultimaModificacao = arquivo.UltimaModificacaoUtc;
        if (!ultimaModificacao.HasValue || ultimaModificacao.Value >= _limiteUtc)
            return null;

        return new EvidenciaAnalise(
            CategoriaAnalise.SinalTemporal,
            $"Última modificação em {ultimaModificacao.Value:yyyy-MM-dd HH:mm:ss} UTC, " +
            $"anterior ao limite de {DiasSemModificacao} dias " +
            $"(referência {_referenciaUtc:yyyy-MM-dd HH:mm:ss} UTC). " +
            "É somente um indício temporal: não comprova desuso nem autoriza descarte.");
    }
}
