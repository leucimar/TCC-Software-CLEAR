using Clear.Domain.Arquivos;

namespace Clear.Domain.Analise;

/// <summary>
/// Reconhece apenas padrões de nome explicitamente definidos.
/// Os padrões são indícios, e não provas de que o arquivo é descartável.
/// </summary>
public sealed class RegraNomeTemporarioBackup : IRegraClassificacao
{
    public EvidenciaAnalise? Avaliar(ArquivoInventariado arquivo)
    {
        ArgumentNullException.ThrowIfNull(arquivo);

        // Evita classificar pastas com esses padrões: apenas o nome do arquivo.
        var nome = Path.GetFileName(arquivo.CaminhoCompleto);

        if (nome.StartsWith("~$", StringComparison.Ordinal))
        {
            return Criar("O nome começa com '~$', padrão usual de arquivo temporário de aplicativos Office.");
        }

        foreach (var extensao in new[] { ".tmp", ".bak", ".backup" })
        {
            if (nome.EndsWith(extensao, StringComparison.OrdinalIgnoreCase))
                return Criar($"O nome termina em '{extensao}', padrão potencial de arquivo temporário ou backup.");
        }

        return null;
    }

    private static EvidenciaAnalise Criar(string motivo) =>
        new(CategoriaAnalise.CopiaBackupOuTemporario,
            motivo + " É apenas um indício; revise o arquivo antes de qualquer decisão.");
}
