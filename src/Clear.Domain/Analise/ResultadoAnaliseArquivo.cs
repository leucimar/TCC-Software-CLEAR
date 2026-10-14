using Clear.Domain.Arquivos;

namespace Clear.Domain.Analise;

/// <summary>Evidências imutáveis por arquivo, permitindo múltiplas categorias.</summary>
public sealed class ResultadoAnaliseArquivo
{
    public ResultadoAnaliseArquivo(
        ArquivoInventariado arquivo,
        IEnumerable<EvidenciaAnalise> evidencias)
    {
        ArgumentNullException.ThrowIfNull(arquivo);
        ArgumentNullException.ThrowIfNull(evidencias);

        var itens = evidencias.ToArray();
        if (itens.Any(item => item is null))
            throw new ArgumentException("Não são permitidas evidências nulas.", nameof(evidencias));

        Arquivo = arquivo;
        Evidencias = Array.AsReadOnly(itens);
        Categorias = Array.AsReadOnly(itens.Select(item => item.Categoria).Distinct().ToArray());
    }

    public ArquivoInventariado Arquivo { get; }
    public IReadOnlyList<EvidenciaAnalise> Evidencias { get; }
    public IReadOnlyList<CategoriaAnalise> Categorias { get; }
}
