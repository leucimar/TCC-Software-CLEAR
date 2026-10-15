namespace Clear.Domain.Compatibilidade;

/// <summary>
/// Retrato de uma única escolha de destino e de checkboxes independentes.
/// Não executa regras, não presume compatibilidade e não conserva estado de UI.
/// </summary>
public sealed class ConfiguracaoCompatibilidade
{
    public ConfiguracaoCompatibilidade(PerfilDestino? perfil, IEnumerable<string> codigosHabilitados)
    {
        ArgumentNullException.ThrowIfNull(codigosHabilitados);
        var codigos = codigosHabilitados.ToArray();

        if (perfil is null && codigos.Length > 0)
            throw new ArgumentException("Não habilite regras sem selecionar um destino.", nameof(codigosHabilitados));

        var encontrados = new HashSet<string>(StringComparer.Ordinal);

        foreach (var codigo in codigos)
        {
            if (string.IsNullOrWhiteSpace(codigo))
                throw new ArgumentException("Identificador de verificação vazio.", nameof(codigosHabilitados));

            if (!encontrados.Add(codigo))
                throw new ArgumentException("A mesma verificação foi habilitada duas vezes.", nameof(codigosHabilitados));

            var definicao = CatalogoVerificacoes.Encontrar(codigo);
            if (definicao is null)
                throw new ArgumentException($"Verificação desconhecida: {codigo}.", nameof(codigosHabilitados));

            if (definicao.Ambiente != perfil!.Ambiente)
                throw new ArgumentException("Verificações de outros destinos não podem ser habilitadas.", nameof(codigosHabilitados));
        }

        Perfil = perfil;
        CodigosHabilitados = Array.AsReadOnly(codigos);
    }

    public PerfilDestino? Perfil { get; }
    public IReadOnlyList<string> CodigosHabilitados { get; }
    public bool HaVerificacoesHabilitadas => CodigosHabilitados.Count > 0;
}
