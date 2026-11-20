using Clear.Domain.Arquivos;
using Clear.Domain.Compatibilidade;
using Clear.Domain.Inventario;

namespace Clear.Domain.Tests;

[TestClass]
public sealed class RegrasCompatibilidadeM3BTests
{
    private static readonly string[] Azure = { "AZF-NOME-001", "AZF-TAM-001" };
    private static readonly string[] Spo =
    {
        "SPO-NOME-001", "SPO-NOME-002", "SPO-NOME-003",
        "SPO-NOME-004", "SPO-TAM-001"
    };

    private static AvaliadorCompatibilidade AzureCom(params string[] codigos) =>
        new(new ConfiguracaoCompatibilidade(PerfilDestino.AzureFilesSmb(), codigos));

    private static AvaliadorCompatibilidade SpoCom(params string[] codigos) =>
        new(new ConfiguracaoCompatibilidade(
            PerfilDestino.SharePointEmpresarial(SubtipoSharePoint.SharePointOnline), codigos));

    private static EntradaInventariada Arquivo(string nome, long bytes = 3) =>
        EntradaInventariada.DeArquivo(new ArquivoInventariado(@"C:\Teste\" + nome, bytes));

    private static EntradaInventariada Pasta(string nome) =>
        EntradaInventariada.DeDiretorio(@"C:\Teste\" + nome);

    private static EstadoVerificacao Estado(AvaliadorCompatibilidade avaliador,
        EntradaInventariada entrada, string codigo) =>
        avaliador.Avaliar(entrada).Single(x => x.CodigoRegra == codigo).Estado;

    [TestMethod]
    [DataRow("a?b.txt")]
    [DataRow("a*b.txt")]
    [DataRow("a:b.txt")]
    [DataRow("a|b.txt")]
    [DataRow("a<b.txt")]
    [DataRow("a>b.txt")]
    [DataRow("a\"b.txt")]
    public void PontuacaoProibidaEmArquivosEhDetectada(string nome)
    {
        Assert.AreEqual(EstadoVerificacao.IncompatibilidadeDocumentada,
            Estado(AzureCom(Azure), Arquivo(nome), "AZF-NOME-001"));
        Assert.AreEqual(EstadoVerificacao.IncompatibilidadeDocumentada,
            Estado(SpoCom(Spo), Arquivo(nome), "SPO-NOME-001"));
    }

    [TestMethod]
    public void SeparadoresDoCaminhoNaoSaoConsideradosPontuacaoDoNome()
    {
        Assert.AreEqual(EstadoVerificacao.SemIncompatibilidadeDetectada,
            Estado(AzureCom(Azure), Arquivo("relatorio.docx"), "AZF-NOME-001"));
    }

    [TestMethod]
    public void PastaTambemEhVerificadaPorNome()
    {
        Assert.AreEqual(EstadoVerificacao.IncompatibilidadeDocumentada,
            Estado(SpoCom(Spo), Pasta("projeto?"), "SPO-NOME-001"));
    }

    [TestMethod]
    public void EspacoInicialEFinalSaoVerificacoesSeparadas()
    {
        var validador = SpoCom(Spo);
        Assert.AreEqual(EstadoVerificacao.IncompatibilidadeDocumentada,
            Estado(validador, Arquivo(" ata.txt"), "SPO-NOME-002"));
        Assert.AreEqual(EstadoVerificacao.SemIncompatibilidadeDetectada,
            Estado(validador, Arquivo(" ata.txt"), "SPO-NOME-003"));
        Assert.AreEqual(EstadoVerificacao.IncompatibilidadeDocumentada,
            Estado(validador, Arquivo("ata.txt "), "SPO-NOME-003"));
        Assert.AreEqual(EstadoVerificacao.SemIncompatibilidadeDetectada,
            Estado(validador, Arquivo("ata doc.txt"), "SPO-NOME-002"));
        Assert.AreEqual(EstadoVerificacao.SemIncompatibilidadeDetectada,
            Estado(validador, Arquivo("ata doc.txt"), "SPO-NOME-003"));
    }

    [TestMethod]
    public void EspacoUnicodeDiferenteDeAsciiNaoAcionaRegra()
    {
        Assert.AreEqual(EstadoVerificacao.SemIncompatibilidadeDetectada,
            Estado(SpoCom(Spo), Arquivo("\u00a0relatorio.txt"), "SPO-NOME-002"));
    }

    [TestMethod]
    [DataRow("_vti_")]
    [DataRow("reuniao_VTI_2026")]
    [DataRow("a_vTi_b.txt")]
    public void SequenciaVti_SeDetectaEmQualquerPosicaoComCaixaInsensivel(string nome)
    {
        Assert.AreEqual(EstadoVerificacao.IncompatibilidadeDocumentada,
            Estado(SpoCom(Spo), Pasta(nome), "SPO-NOME-004"));
    }

    [TestMethod]
    public void SequenciasParecidasNaoAcionamRegraVti()
    {
        Assert.AreEqual(EstadoVerificacao.SemIncompatibilidadeDetectada,
            Estado(SpoCom(Spo), Arquivo("relatorio_vti.txt"), "SPO-NOME-004"));
        Assert.AreEqual(EstadoVerificacao.SemIncompatibilidadeDetectada,
            Estado(SpoCom(Spo), Arquivo("AVTIb.txt"), "SPO-NOME-004"));
    }

    [TestMethod]
    public void UnicodeNaoTemBloqueioGenerico()
    {
        var avaliador = SpoCom(Spo);
        foreach (var nome in new[] { "ação.txt", "é.txt", "emoji😀.txt",
            "hash#percent%.txt", "nome\uE000.txt" })
        {
            var resultado = avaliador.Avaliar(Arquivo(nome));
            Assert.IsFalse(resultado.Any(x => x.Estado == EstadoVerificacao.IncompatibilidadeDocumentada),
                "Não bloquear por lista Unicode antiga: " + nome);
        }
    }

    [TestMethod]
    public void AzureRespeitaTamanhoNaFronteiraDeQuatroTiB()
    {
        var avaliador = AzureCom(Azure);
        var limite = AvaliadorCompatibilidade.LimiteAzureFilesBytes;
        Assert.AreEqual(EstadoVerificacao.SemIncompatibilidadeDetectada,
            Estado(avaliador, Arquivo("a.txt", limite), "AZF-TAM-001"));
        Assert.AreEqual(EstadoVerificacao.IncompatibilidadeDocumentada,
            Estado(avaliador, Arquivo("a.txt", limite + 1), "AZF-TAM-001"));
    }

    [TestMethod]
    public void SharePointRespeitaLimiteBinarioDe250GiB()
    {
        var avaliador = SpoCom(Spo);
        var limite = AvaliadorCompatibilidade.LimiteSharePointBytes;
        Assert.AreEqual(268435456000L, limite);
        Assert.AreEqual(EstadoVerificacao.SemIncompatibilidadeDetectada,
            Estado(avaliador, Arquivo("a.txt", limite), "SPO-TAM-001"));
        Assert.AreEqual(EstadoVerificacao.IncompatibilidadeDocumentada,
            Estado(avaliador, Arquivo("a.txt", limite + 1), "SPO-TAM-001"));
        Assert.AreEqual(EstadoVerificacao.SemIncompatibilidadeDetectada,
            Estado(avaliador, Arquivo("a.txt", 250000000000L), "SPO-TAM-001"));
    }

    [TestMethod]
    public void TamanhoNaoEhAplicavelParaDiretorios()
    {
        Assert.AreEqual(EstadoVerificacao.NaoAplicavel,
            Estado(AzureCom(Azure), Pasta("Equipe"), "AZF-TAM-001"));
        Assert.AreEqual(EstadoVerificacao.NaoAplicavel,
            Estado(SpoCom(Spo), Pasta("Equipe"), "SPO-TAM-001"));
    }

    [TestMethod]
    public void RegraDesabilitadaNaoDetectaMesmoComNomeInvalido()
    {
        var resultado = AzureCom("AZF-TAM-001").Avaliar(Arquivo("nome?.txt"));
        Assert.AreEqual(EstadoVerificacao.Desabilitada,
            resultado.Single(x => x.CodigoRegra == "AZF-NOME-001").Estado);
        Assert.AreEqual(EstadoVerificacao.SemIncompatibilidadeDetectada,
            resultado.Single(x => x.CodigoRegra == "AZF-TAM-001").Estado);
    }

    [TestMethod]
    public void NenhumaRegraLigadaNaoSignificaCompatibilidadeGarantida()
    {
        var resultados = SpoCom().Avaliar(Arquivo("nome?.txt"));
        Assert.HasCount(8, resultados);
        Assert.IsTrue(resultados.All(x => x.Estado == EstadoVerificacao.Desabilitada));
        var resumo = new ResumoVerificacoes(resultados);
        Assert.IsFalse(resumo.AlgumaVerificacaoExecutada);
        Assert.IsFalse(resumo.ApenasSemIncompatibilidadeDetectada);
    }

    [TestMethod]
    public void SemPerfilNaoAvaliaNenhumDestino()
    {
        var avaliador = new AvaliadorCompatibilidade(
            new ConfiguracaoCompatibilidade(null, Array.Empty<string>()));
        Assert.HasCount(0, avaliador.DefinicoesDoPerfil);
        Assert.HasCount(0, avaliador.Avaliar(Arquivo("a?.txt")));
    }

    [TestMethod]
    public void CadaPerfilRecebeSomenteSuasRegras()
    {
        Assert.HasCount(5, AzureCom(Azure).Avaliar(Arquivo("a.txt")));
        Assert.HasCount(8, SpoCom(Spo).Avaliar(Arquivo("a.txt")));
        Assert.IsFalse(AzureCom(Azure).Avaliar(Arquivo("a.txt"))
            .Any(x => x.CodigoRegra.StartsWith("SPO-", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void JustificativaContemIdentificadorDocumentalEValorObservado()
    {
        var resultado = SpoCom("SPO-TAM-001").Avaliar(Arquivo("enorme.bin",
            AvaliadorCompatibilidade.LimiteSharePointBytes + 1));
        var achado = resultado.Single(x => x.CodigoRegra == "SPO-TAM-001");
        StringAssert.Contains(achado.Justificativa, "F15");
        StringAssert.Contains(achado.Justificativa, "268435456000");
        StringAssert.Contains(achado.Justificativa, "não certifica migração");
    }
}
