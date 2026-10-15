using Clear.Domain.Compatibilidade;

namespace Clear.Application.Compatibilidade;

/// <summary>
/// Sugestões explícitas para o recorte aprovado do TCC (M3-3B-1).
/// Nenhuma regra geral ou leitura de conteúdo é ativada por este perfil.
/// </summary>
public static class PerfisRecomendados
{
    public static IReadOnlyList<string> Para(AmbienteDestino? ambiente)
    {
        if (ambiente is null)
            return Array.Empty<string>();

        return Array.AsReadOnly(CatalogoVerificacoes.Definicoes
            .Where(item => item.Ambiente == ambiente.Value)
            .Select(item => item.Codigo).ToArray());
    }
}
