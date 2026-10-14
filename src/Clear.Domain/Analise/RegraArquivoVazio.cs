using Clear.Domain.Arquivos;

namespace Clear.Domain.Analise;

/// <summary>Identifica tamanho zero, sem inferir que o arquivo seja dispensável.</summary>
public sealed class RegraArquivoVazio : IRegraClassificacao
{
    public EvidenciaAnalise? Avaliar(ArquivoInventariado arquivo)
    {
        ArgumentNullException.ThrowIfNull(arquivo);

        return arquivo.TamanhoBytes == 0
            ? new EvidenciaAnalise(
                CategoriaAnalise.ArquivoVazio,
                "O tamanho informado nos metadados é de zero bytes; isso não significa que o arquivo possa ser excluído.")
            : null;
    }
}
