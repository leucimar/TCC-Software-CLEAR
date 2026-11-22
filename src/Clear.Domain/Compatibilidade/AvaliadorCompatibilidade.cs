using System.IO;
using Clear.Domain.Inventario;

namespace Clear.Domain.Compatibilidade;

/// <summary>
/// Avaliação local e determinística das regras do catálogo de destinos.
/// Não consulta serviço remoto, não abre arquivos, não afirma sucesso de migração.
/// </summary>
public sealed class AvaliadorCompatibilidade
{
    public const long LimiteAzureFilesBytes = 4L * 1024 * 1024 * 1024 * 1024;
    public const long LimiteSharePointBytes = 250_000_000_000L;
    private static readonly char[] PontuacaoProibida =
        { '"', '\\', '/', ':', '|', '<', '>', '*', '?' };
    private static readonly HashSet<string> TiposComScriptRestrito = new(StringComparer.OrdinalIgnoreCase)
        { ".aspx", ".asmx", ".ascx", ".master", ".xap", ".swf", ".jar", ".xsf", ".htc" };

    private readonly ConfiguracaoCompatibilidade _configuracao;
    private readonly IReadOnlyList<DefinicaoVerificacao> _definicoes;
    private readonly HashSet<string> _habilitadas;

    private readonly string? _raizOrigem;
    private readonly string? _prefixoDestino;

    public AvaliadorCompatibilidade(ConfiguracaoCompatibilidade configuracao, string? raizOrigem = null)
    {
        _configuracao = configuracao ?? throw new ArgumentNullException(nameof(configuracao));
        _definicoes = Array.AsReadOnly(CatalogoVerificacoes.Definicoes
            .Where(x => x.Ambiente == _configuracao.Perfil?.Ambiente).ToArray());
        _habilitadas = new HashSet<string>(_configuracao.CodigosHabilitados, StringComparer.Ordinal);
        _raizOrigem = raizOrigem is null ? null : Path.GetFullPath(raizOrigem);
        _prefixoDestino = _configuracao.PrefixoProjetado;
    }

    public IReadOnlyList<DefinicaoVerificacao> DefinicoesDoPerfil => _definicoes;

    public IReadOnlyList<ResultadoVerificacao> Avaliar(EntradaInventariada entrada)
    {
        ArgumentNullException.ThrowIfNull(entrada);
        var resultados = new List<ResultadoVerificacao>(_definicoes.Count);

        var nome = Path.GetFileName(entrada.CaminhoCompleto
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)) ?? string.Empty;
        foreach (var definicao in _definicoes)
        {
            if (!_habilitadas.Contains(definicao.Codigo))
            {
                resultados.Add(new ResultadoVerificacao(definicao.Codigo, entrada,
                    EstadoVerificacao.Desabilitada, "Verificação desativada pelo operador."));
                continue;
            }

            var resultado = definicao.Codigo switch
            {
                "AZF-NOME-001" =>
                    VerificarCondicao(definicao, entrada, nome.IndexOfAny(PontuacaoProibida) >= 0 ||
                        TemControleOuUtf16Invalido(nome),
                        "O componente contém pontuação proibida, controle ASCII ou uma sequência UTF-16 inválida.",
                        "Nenhum caractere proibido ou inválido foi encontrado no componente."),

                "SPO-NOME-001" =>
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

                "AZF-NOME-002" =>
                    VerificarCondicao(definicao, entrada, NomeReservado(nome, azure: true),
                        "Nome reservado pelo Azure Files.", "Nome reservado não encontrado."),
                "AZF-NOME-003" => AvisarCondicao(definicao, entrada,
                    nome.EndsWith('.') || nome.Contains('\uE000'),
                    nome.EndsWith('.')
                        ? "O FileREST pode remover o ponto final em compartilhamentos SMB. Confira a configuração da ferramenta de transferência."
                        : "A documentação do FileREST indica U+E000 como caso de atenção em nomes de caminho."),

                "SPO-NOME-005" =>
                    VerificarCondicao(definicao, entrada, NomeReservado(nome, azure: false) ||
                        (entrada.Tipo == TipoEntradaInventario.Diretorio && nome.Length > 0 &&
                         nome[0] is '\u309B' or '\u3099' or '\u1027'),
                        "Nome ou prefixo reservado pelo SharePoint.", "Nome ou prefixo reservado não encontrado."),

                "SPO-NOME-006" => VerificarForms(definicao, entrada, nome),
                "AZF-CAM-001" => VerificarCaminho(definicao, entrada, 2048),
                "SPO-CAM-001" => VerificarCaminho(definicao, entrada, 400),
                "AZF-CAM-002" => VerificarComprimento(definicao, entrada, nome.Length, 255, "Nome do componente"),
                "AZF-CAM-003" => VerificarProfundidadeAzure(definicao, entrada),

                "AZF-TAM-001" =>
                    VerificarTamanho(definicao, entrada, LimiteAzureFilesBytes,
                        "4 TiB (4 × 2^40 bytes)"),

                "SPO-TAM-001" =>
                    VerificarTamanho(definicao, entrada, LimiteSharePointBytes,
                        "250 GB (250.000.000.000 bytes, leitura decimal do limite publicado)"),
                "SPO-TIPO-001" => VerificarTipoSharePoint(definicao, entrada, nome),

                _ => throw new InvalidOperationException(
                    $"Regra catalogada sem implementação: {definicao.Codigo}")
            };
            resultados.Add(resultado);
        }

        return Array.AsReadOnly(resultados.ToArray());
    }

    private static bool TemControleOuUtf16Invalido(string nome)
    {
        var texto = nome.AsSpan();
        while (!texto.IsEmpty)
        {
            if (System.Text.Rune.DecodeFromUtf16(texto, out var rune, out var consumidos) !=
                System.Buffers.OperationStatus.Done || rune.Value < 0x20)
                return true;
            texto = texto[consumidos..];
        }
        return false;
    }

    private static bool NomeReservado(string nome, bool azure)
    {
        // Nomes de dispositivo são comparados pelo nome-base. Nomes como .lock e
        // desktop.ini são comparados inteiros; a exceção ~$ é um prefixo.
        if (!azure && (nome.Equals(".lock", StringComparison.OrdinalIgnoreCase) ||
            nome.Equals("desktop.ini", StringComparison.OrdinalIgnoreCase) ||
            nome.StartsWith("~$", StringComparison.Ordinal)))
            return true;
        if (azure && nome is "." or "..")
            return true;

        var ponto = nome.IndexOf('.');
        var baseNome = ponto >= 0 ? nome[..ponto] : nome;
        var normalizado = baseNome.ToUpperInvariant();
        if (normalizado is "CON" or "PRN" or "AUX" or "NUL")
            return true;
        if (azure && normalizado == "CLOCK$")
            return true;
        return normalizado.Length == 4 && (normalizado.StartsWith("COM", StringComparison.Ordinal) ||
            normalizado.StartsWith("LPT", StringComparison.Ordinal)) &&
            normalizado[3] >= (azure ? '1' : '0') && normalizado[3] <= '9';
    }

    private string? CaminhoRelativo(EntradaInventariada entrada)
    {
        if (_raizOrigem is null || !Path.IsPathFullyQualified(entrada.CaminhoCompleto))
            return null;
        try
        {
            var relativo = Path.GetRelativePath(_raizOrigem, entrada.CaminhoCompleto).Replace('\\', '/');
            return relativo is "." or ".." || relativo.StartsWith("../", StringComparison.Ordinal) ||
                Path.IsPathRooted(relativo) ? null : relativo;
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or NotSupportedException)
        {
            return null;
        }
    }

    private ResultadoVerificacao VerificarForms(DefinicaoVerificacao regra,
        EntradaInventariada entrada, string nome)
    {
        if (!nome.Equals("forms", StringComparison.OrdinalIgnoreCase))
            return VerificarCondicao(regra, entrada, false,
                "Nome Forms reservado na raiz da biblioteca.", "O componente não se chama Forms.");
        var relativo = CaminhoRelativo(entrada);
        if (relativo is null || _configuracao.SubpastaDestino is null)
            return new ResultadoVerificacao(regra.Codigo, entrada, EstadoVerificacao.NaoAvaliado,
                "Informe se o acervo irá para a raiz ou uma subpasta da biblioteca para avaliar Forms.");
        return VerificarCondicao(regra, entrada,
            _configuracao.SubpastaDestino.Length == 0 && !relativo.Contains('/'),
            "Nome Forms reservado na raiz da biblioteca.", "Forms está abaixo da raiz da biblioteca.");
    }

    private ResultadoVerificacao VerificarCaminho(DefinicaoVerificacao regra,
        EntradaInventariada entrada, int limite)
    {
        var relativo = CaminhoRelativo(entrada);
        if (relativo is null || _prefixoDestino is null)
            return new ResultadoVerificacao(regra.Codigo, entrada, EstadoVerificacao.NaoAvaliado,
                "O caminho relativo da origem ou a localização do destino não foi informado.");
        // Conta segmentos e separadores internos. O nome local já está decodificado.
        var comprimento = relativo.Length + (_prefixoDestino.Length > 0 ? _prefixoDestino.Length + 1 : 0);
        return VerificarComprimento(regra, entrada, comprimento, limite, "Caminho projetado");
    }

    private ResultadoVerificacao VerificarProfundidadeAzure(DefinicaoVerificacao regra, EntradaInventariada entrada)
    {
        var relativo = CaminhoRelativo(entrada);
        if (relativo is null || _prefixoDestino is null)
            return new ResultadoVerificacao(regra.Codigo, entrada, EstadoVerificacao.NaoAvaliado,
                "O caminho relativo da origem ou a localização do destino não foi informado.");

        const int limite = 250;
        int profundidade = relativo.AsSpan().Count('/') + (entrada.Tipo == TipoEntradaInventario.Diretorio ? 1 : 0);
        if (_prefixoDestino.Length > 0)
            profundidade += _prefixoDestino.AsSpan().Count('/') + 1;
        var detalhe = $"O caminho projetado tem {profundidade} subpastas. Limite documentado: {limite}.";
        return VerificarCondicao(regra, entrada, profundidade > limite, detalhe, detalhe);
    }

    private static ResultadoVerificacao VerificarTipoSharePoint(DefinicaoVerificacao regra,
        EntradaInventariada entrada, string nome)
    {
        if (entrada.Tipo == TipoEntradaInventario.Diretorio)
            return new ResultadoVerificacao(regra.Codigo, entrada, EstadoVerificacao.NaoAplicavel,
                "O comportamento por tipo aplica-se a arquivos.");

        var extensao = Path.GetExtension(nome);
        if (TiposComScriptRestrito.Contains(extensao))
            return AvisarCondicao(regra, entrada, true,
                "A execução de script deste tipo depende da configuração do site SharePoint.");
        if (extensao.Equals(".tmp", StringComparison.OrdinalIgnoreCase) ||
            nome.Equals(".ds_store", StringComparison.OrdinalIgnoreCase) ||
            nome.Equals("desktop.ini", StringComparison.OrdinalIgnoreCase))
            return AvisarCondicao(regra, entrada, true,
                "Se a transferência usar a sincronização OneDrive, este tipo pode ser ignorado pelo cliente.");

        return AvisarCondicao(regra, entrada, false, string.Empty);
    }

    private static ResultadoVerificacao AvisarCondicao(DefinicaoVerificacao regra, EntradaInventariada entrada,
        bool condicao, string observacao) => new(regra.Codigo, entrada,
        condicao ? EstadoVerificacao.Alerta : EstadoVerificacao.SemIncompatibilidadeDetectada,
        (condicao ? observacao : "Nenhuma condição local desta regra foi identificada.") +
        $" Fonte documental: {regra.FonteDocumental}; catálogo {CatalogoVerificacoes.Versao}.");

    private static ResultadoVerificacao VerificarComprimento(DefinicaoVerificacao regra,
        EntradaInventariada entrada, int comprimento, int limite, string parte)
    {
        var medida = $"{parte}: {comprimento} caracteres (unidades UTF-16). Limite documentado: {limite}.";
        return VerificarCondicao(regra, entrada, comprimento > limite, medida, medida);
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
