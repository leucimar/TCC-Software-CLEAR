using Clear.Domain.Arquivos;

namespace Clear.Domain.Analise;

/// <summary>
/// Uma regra independente identifica indícios por metadados, sem modificar arquivos.
/// </summary>
public interface IRegraClassificacao
{
    EvidenciaAnalise? Avaliar(ArquivoInventariado arquivo);
}
