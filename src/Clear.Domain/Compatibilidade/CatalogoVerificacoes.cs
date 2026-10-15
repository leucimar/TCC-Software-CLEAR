namespace Clear.Domain.Compatibilidade;

/// <summary>
/// Metadados das regras propostas para a Parte 3B.
/// Estar catalogada NÃO significa que a verificação já esteja implementada.
/// </summary>
public sealed class DefinicaoVerificacao
{
    public DefinicaoVerificacao(
        string codigo, string rotulo, AmbienteDestino ambiente, string fonteDocumental)
    {
        if (string.IsNullOrWhiteSpace(codigo))
            throw new ArgumentException("Informe o identificador estável.", nameof(codigo));
        if (string.IsNullOrWhiteSpace(rotulo))
            throw new ArgumentException("Informe o nome da regra.", nameof(rotulo));
        if (!Enum.IsDefined(ambiente))
            throw new ArgumentOutOfRangeException(nameof(ambiente));
        if (string.IsNullOrWhiteSpace(fonteDocumental))
            throw new ArgumentException("Informe a fonte documental.", nameof(fonteDocumental));

        Codigo = codigo;
        Rotulo = rotulo;
        Ambiente = ambiente;
        FonteDocumental = fonteDocumental;
    }

    public string Codigo { get; }
    public string Rotulo { get; }
    public AmbienteDestino Ambiente { get; }
    public string FonteDocumental { get; }
}

/// <summary>
/// Catálogo inicial fundamentado no relatório Work de 14/10/2026, ainda
/// SEM algoritmo ou resultado executável de compatibilidade no M3 Parte 3A.
/// </summary>
public static class CatalogoVerificacoes
{
    public const string Versao = "M3-3A-1";

    public static IReadOnlyList<DefinicaoVerificacao> Definicoes { get; } =
        Array.AsReadOnly(new[]
        {
            new DefinicaoVerificacao("AZF-NOME-001", "Pontuação proibida", AmbienteDestino.AzureFilesClassicoSmb, "F01"),
            new DefinicaoVerificacao("AZF-TAM-001", "Tamanho máximo do serviço", AmbienteDestino.AzureFilesClassicoSmb, "F02"),
            new DefinicaoVerificacao("SPO-NOME-001", "Pontuação proibida", AmbienteDestino.SharePointOneDriveEmpresarial, "F14"),
            new DefinicaoVerificacao("SPO-NOME-002", "Espaço inicial", AmbienteDestino.SharePointOneDriveEmpresarial, "F14"),
            new DefinicaoVerificacao("SPO-NOME-003", "Espaço final", AmbienteDestino.SharePointOneDriveEmpresarial, "F14"),
            new DefinicaoVerificacao("SPO-NOME-004", "Sequência _vti_", AmbienteDestino.SharePointOneDriveEmpresarial, "F14/F19"),
            new DefinicaoVerificacao("SPO-TAM-001", "Tamanho máximo do serviço", AmbienteDestino.SharePointOneDriveEmpresarial, "F15")
        });

    public static DefinicaoVerificacao? Encontrar(string codigo) =>
        Definicoes.FirstOrDefault(r => string.Equals(r.Codigo, codigo, StringComparison.Ordinal));
}
