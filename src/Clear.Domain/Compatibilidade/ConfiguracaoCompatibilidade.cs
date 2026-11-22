namespace Clear.Domain.Compatibilidade;

/// <summary>
/// Retrato de uma única escolha de destino e de checkboxes independentes.
/// Não executa regras, não presume compatibilidade e não conserva estado de UI.
/// </summary>
public sealed class ConfiguracaoCompatibilidade
{
    public ConfiguracaoCompatibilidade(PerfilDestino? perfil, IEnumerable<string> codigosHabilitados,
        string? caminhoBibliotecaDecodificado = null, string? subpastaDestino = null)
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
        CaminhoBibliotecaDecodificado = NormalizarPrefixo(caminhoBibliotecaDecodificado);
        SubpastaDestino = NormalizarPrefixo(subpastaDestino);
        CodigosHabilitados = Array.AsReadOnly(codigos);
    }

    // Null representa uma localização desconhecida. O prefixo vazio representa a raiz.
    public string? CaminhoBibliotecaDecodificado { get; }
    public string? SubpastaDestino { get; }
    public string? PrefixoProjetado => Perfil?.Ambiente switch
    {
        AmbienteDestino.AzureFilesClassicoSmb => SubpastaDestino,
        AmbienteDestino.SharePointOneDriveEmpresarial
            when !string.IsNullOrEmpty(CaminhoBibliotecaDecodificado) && SubpastaDestino is not null
            => SubpastaDestino.Length == 0 ? CaminhoBibliotecaDecodificado
                : CaminhoBibliotecaDecodificado + "/" + SubpastaDestino,
        _ => null
    };

    private static string? NormalizarPrefixo(string? caminho)
    {
        if (caminho is null)
            return null;
        if (caminho.StartsWith("//", StringComparison.Ordinal) ||
            caminho.StartsWith(@"\\", StringComparison.Ordinal) ||
            caminho.IndexOfAny(new[] { ':', '?', '*', '"', '<', '>', '|' }) >= 0 ||
            caminho.Any(char.IsControl))
            throw new ArgumentException("Informe somente pastas do destino, sem URL completa, UNC ou letra de unidade.");

        var normalizado = caminho.Replace('\\', '/').Trim('/');
        if (normalizado.Length > 0 && normalizado.Split('/').Any(
                parte => parte.Length == 0 || parte is "." or ".."))
            throw new ArgumentException("Informe uma hierarquia de pastas sem segmentos vazios, '.' ou '..'.");
        return normalizado;
    }

    public PerfilDestino? Perfil { get; }
    public IReadOnlyList<string> CodigosHabilitados { get; }
    public bool HaVerificacoesHabilitadas => CodigosHabilitados.Count > 0;
}
