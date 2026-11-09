using Clear.Domain.Arquivos;

namespace Clear.Domain.Analise;

/// <summary>
/// Aponta arquivos cuja data selecionada no inventário é antiga, sem trocar campos ausentes.
/// O Windows pode não atualizar este metadado (ou atualizá-lo tardiamente).
/// Não conclui desuso, não autoriza movimentação e não recorre à modificação.
/// </summary>
public sealed class RegraSinalTemporal : IRegraClassificacao
{
    private readonly DateTimeOffset _referenciaUtc;
    private readonly DateTimeOffset _limiteUtc;

    public RegraSinalTemporal(int diasSemAcesso, DateTimeOffset referencia,
        CriterioTemporal criterio = CriterioTemporal.UltimoAcesso)
    {
        if (diasSemAcesso is < 1 or > 3650)
            throw new ArgumentOutOfRangeException(nameof(diasSemAcesso),
                "Informe entre 1 e 3650 dias.");

        if (!Enum.IsDefined(criterio)) throw new ArgumentOutOfRangeException(nameof(criterio));
        Criterio = criterio;
        DiasSemAcesso = diasSemAcesso;
        _referenciaUtc = referencia.ToUniversalTime();
        _limiteUtc = _referenciaUtc.AddDays(-diasSemAcesso);
    }

    public CriterioTemporal Criterio { get; }
    public int DiasSemAcesso { get; }
    public DateTimeOffset ReferenciaUtc => _referenciaUtc;
    public DateTimeOffset LimiteUtc => _limiteUtc;

    public EvidenciaAnalise? Avaliar(ArquivoInventariado arquivo)
    {
        ArgumentNullException.ThrowIfNull(arquivo);

        var ultimoAcesso = Criterio == CriterioTemporal.UltimoAcesso
            ? arquivo.UltimoAcessoUtc : arquivo.UltimaModificacaoUtc;
        // Data ausente ou sentinela do Windows: não criar falso indício.
        if (!ultimoAcesso.HasValue || ultimoAcesso.Value.Year <= 1601 ||
            ultimoAcesso.Value >= _limiteUtc)
            return null;

        return new EvidenciaAnalise(
            CategoriaAnalise.SinalTemporal,
            $"{(Criterio == CriterioTemporal.UltimoAcesso ? "Último acesso" : "Última modificação")} registrado(a) em {ultimoAcesso.Value:yyyy-MM-dd HH:mm:ss} UTC, " +
            $"anterior ao limite de {DiasSemAcesso} dias " +
            $"(referência {_referenciaUtc:yyyy-MM-dd HH:mm:ss} UTC). " +
            (Criterio == CriterioTemporal.UltimoAcesso
                ? "O Windows pode atrasar ou desativar a atualização da data de acesso. "
                : "A data de modificação não demonstra frequência de uso do arquivo. ") +
            "É somente um indício temporal: não comprova desuso nem autoriza descarte.");
    }
}
