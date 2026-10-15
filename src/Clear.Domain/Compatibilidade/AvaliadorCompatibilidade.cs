using System.IO;
using Clear.Domain.Inventario;

namespace Clear.Domain.Compatibilidade;

/// <summary>
/// Avaliação local e determinística das sete regras aprovadas para o M3-3B.
/// Não consulta serviço remoto, não abre arquivos, não afirma sucesso de migração.
/// </summary>
public sealed class AvaliadorCompatibilidade
{
    public const long LimiteAzureFilesBytes = 4L * 1024 * 1024 * 1024 * 1024;
    public const long LimiteSharePointBytes = 250L * 1024 * 1024 * 1024;
    private static readonly char[] PontuacaoProibida =
        { '"', '\\', '/', ':', '|', '<', '>', '*', '?' };

    private readonly ConfiguracaoCompatibilidade _configuracao;
    private readonly IReadOnlyList<DefinicaoVerificacao> _definicoes;
    private readonly HashSet<string> _habilitadas;

    public AvaliadorCompatibilidade(ConfiguracaoCompatibilidade configuracao)
    {
        _configuracao = configuracao ?? throw new ArgumentNullException(nameof(configuracao));
        _definicoes = Array.AsReadOnly(CatalogoVerificacoes.Definicoes
            .Where(x => x.Ambiente == _configuracao.Perfil?.Ambiente).ToArray());
        _habilitadas = new HashSet<string>(_configuracao.CodigosHabilitados, StringComparer.Ordinal);
    }

    public IReadOnlyList<DefinicaoVerificacao> DefinicoesDoPerfil => _definicoes;

    public IReadOnlyList<ResultadoVerificacao> Avaliar(EntradaInventariada entrada)
    {
        ArgumentNullException.ThrowIfNull(entrada);
        var resultados = new List<ResultadoVerificacao>(_definicoes.Count);

        foreach (var definicao in _definicoes)
        {
            if (!_habilitadas.Contains(definicao.Codigo))
            {
                resultados.Add(new ResultadoVerificacao(definicao.Codigo, entrada,
                    EstadoVerificacao.Desabilitada, "Verificação desativada pelo operador."));
                continue;
            }

            var nome = Path.GetFileName(entrada.CaminhoCompleto
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            var resultado = definicao.Codigo switch
            {
                "AZF-NOME-001" or "SPO-NOME-001" =>
                    VerificarCondicao(definicao, entrada, nome.IndexOfAny(PontuacaoProibida) >= 0,
                        "O componente contém pontuação não aceita pelo perfil.",
                        "Nenhuma das nove pontuações proibidas foi encontrada no componente."),

                "SPO-NOME-002" =>
                    VerificarCondicao(definicao, entrada, nome.StartsWith(' '),
                        "O componente começa por espaço ASCII (U+0020), não aceito.",
                        "O componente não começa por espaço ASCII."),

                "SPO-NOME-003" =>
                    VerificarCondicao(definicao, entrada, nome.EndsWith(' '),
                        "O componente termina por espaço ASCII (U+0020), não aceito.",
                        "O componente não termina por espaço ASCII."),

                "SPO-NOME-004" =>
                    VerificarCondicao(definicao, entrada,
                        nome.Contains("_vti_", StringComparison.OrdinalIgnoreCase),
                        "O componente contém a sequência reservada _vti_ (comparação sem distinção de caixa).",
                        "A sequência reservada _vti_ não foi encontrada."),

                "AZF-TAM-001" =>
                    VerificarTamanho(definicao, entrada, LimiteAzureFilesBytes,
                        "4 TiB (4 × 2^40 bytes)"),

                "SPO-TAM-001" =>
                    VerificarTamanho(definicao, entrada, LimiteSharePointBytes,
                        "250 GiB (250 × 2^30 bytes)"),

                _ => throw new InvalidOperationException(
                    $"Regra catalogada sem implementação: {definicao.Codigo}")
            };
            resultados.Add(resultado);
        }

        return Array.AsReadOnly(resultados.ToArray());
    }

    private static ResultadoVerificacao VerificarCondicao(
        DefinicaoVerificacao regra,
        EntradaInventariada entrada,
        bool incompatibilidade,
        string justificativaPositiva,
        string justificativaNegativa)
    {
        var estado = incompatibilidade
            ? EstadoVerificacao.IncompatibilidadeDocumentada
            : EstadoVerificacao.SemIncompatibilidadeDetectada;

        return new ResultadoVerificacao(regra.Codigo, entrada, estado,
            (incompatibilidade ? justificativaPositiva : justificativaNegativa) +
            $" Fonte documental: {regra.FonteDocumental}; catálogo {CatalogoVerificacoes.Versao}.");
    }

    private static ResultadoVerificacao VerificarTamanho(
        DefinicaoVerificacao regra,
        EntradaInventariada entrada,
        long limite,
        string descricao)
    {
        if (entrada.Tipo == TipoEntradaInventario.Diretorio)
            return new ResultadoVerificacao(regra.Codigo, entrada,
                EstadoVerificacao.NaoAplicavel,
                "Tamanho máximo individual aplica-se a arquivos, não diretórios.");

        if (entrada.TamanhoBytes is not long tamanho)
            return new ResultadoVerificacao(regra.Codigo, entrada,
                EstadoVerificacao.NaoAvaliado,
                "Não há tamanho lógico confiável para avaliar este arquivo.");

        var excede = tamanho > limite;
        return new ResultadoVerificacao(regra.Codigo, entrada,
            excede ? EstadoVerificacao.IncompatibilidadeDocumentada
                  : EstadoVerificacao.SemIncompatibilidadeDetectada,
            $"Tamanho lógico: {tamanho} bytes. Limite oficial no cenário: {descricao}, " +
            $"{limite} bytes. {(excede ? "Ultrapassa o limite." : "Não ultrapassa o limite.")} " +
            $"Fonte: {regra.FonteDocumental}. Isto não certifica migração.");
    }
}

/// <summary>Resultado de um item da amostra, mantendo classificação geral independente.</summary>
public sealed class AvaliacaoEntrada
{
    public AvaliacaoEntrada(
        EntradaInventariada entrada,
        IEnumerable<ResultadoVerificacao> verificacoes,
        bool acimaDoLimitePersonalizado)
    {
        Entrada = entrada ?? throw new ArgumentNullException(nameof(entrada));
        ArgumentNullException.ThrowIfNull(verificacoes);
        Verificacoes = Array.AsReadOnly(verificacoes.ToArray());
        AcimaDoLimitePersonalizado = acimaDoLimitePersonalizado;
    }

    public EntradaInventariada Entrada { get; }
    public IReadOnlyList<ResultadoVerificacao> Verificacoes { get; }
    public bool AcimaDoLimitePersonalizado { get; }
}
