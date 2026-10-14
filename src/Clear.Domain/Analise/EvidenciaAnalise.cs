namespace Clear.Domain.Analise;

/// <summary>Categorias previstas; nenhuma autoriza movimentação ou exclusão.</summary>
public enum CategoriaAnalise
{
    DuplicataExata,
    CopiaBackupOuTemporario,
    SinalTemporal,
    ArquivoVazio,
    CaminhoPotencialmenteIncompativel
}

/// <summary>Indício acompanhado de justificativa compreensível.</summary>
public sealed class EvidenciaAnalise
{
    public EvidenciaAnalise(CategoriaAnalise categoria, string justificativa)
    {
        if (!Enum.IsDefined(categoria))
            throw new ArgumentOutOfRangeException(nameof(categoria));
        if (string.IsNullOrWhiteSpace(justificativa))
            throw new ArgumentException("A justificativa é obrigatória.", nameof(justificativa));

        Categoria = categoria;
        Justificativa = justificativa;
    }

    public CategoriaAnalise Categoria { get; }
    public string Justificativa { get; }
}
