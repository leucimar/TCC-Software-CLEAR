using Clear.Domain.Arquivos;
using Clear.Domain.Compatibilidade;
using Clear.Domain.Inventario;

namespace Clear.Domain.Tests;

[TestClass]
public sealed class RegrasCompatibilidadeD4Tests
{
    private static readonly string Raiz = Path.GetFullPath("origem-virtual");
    private static readonly PerfilDestino Spo = PerfilDestino.SharePointEmpresarial(SubtipoSharePoint.SharePointOnline);
    private static readonly PerfilDestino Azure = PerfilDestino.AzureFilesSmb();

    private static ResultadoVerificacao Avaliar(ConfiguracaoCompatibilidade configuracao,
        string codigo, string caminho, bool diretorio = false)
    {
        var completo = Path.Combine(Raiz, caminho);
        var entrada = diretorio ? EntradaInventariada.DeDiretorio(completo)
            : EntradaInventariada.DeArquivo(new ArquivoInventariado(completo, 10));
        return new AvaliadorCompatibilidade(configuracao, Raiz).Avaliar(entrada)
            .Single(item => item.CodigoRegra == codigo);
    }

    [TestMethod]
    [DataRow("PrN.txt", true)]
    [DataRow("COM0", true)]
    [DataRow("LPT9", true)]
    [DataRow(".LOCK", true)]
    [DataRow("~$relatorio.xlsx", true)]
    [DataRow("documento.lock", false)]
    [DataRow("COM10", false)]
    public void NomeReservadoSharePointRespeitaBasePrefixoESufixo(string nome, bool esperado)
    {
        const string regra = "SPO-NOME-005";
        var resultado = Avaliar(new ConfiguracaoCompatibilidade(Spo, [regra]), regra, nome);
        Assert.AreEqual(esperado, resultado.Estado == EstadoVerificacao.IncompatibilidadeDocumentada);
    }

    [TestMethod]
    [DataRow("CLOCK$", true)]
    [DataRow("NuL.txt", true)]
    [DataRow("COM0", false)]
    [DataRow(".lock", false)]
    public void AzureUsaListaPropriaDeNomes(string nome, bool esperado)
    {
        const string regra = "AZF-NOME-002";
        var resultado = Avaliar(new ConfiguracaoCompatibilidade(Azure, [regra]), regra, nome);
        Assert.AreEqual(esperado, resultado.Estado == EstadoVerificacao.IncompatibilidadeDocumentada);
    }

    [TestMethod]
    public void FormsDependeDaPosicaoRealNaBiblioteca()
    {
        const string regra = "SPO-NOME-006";
        var raizBiblioteca = new ConfiguracaoCompatibilidade(Spo, [regra], "sites/TCC/Docs", "");
        var subpasta = new ConfiguracaoCompatibilidade(Spo, [regra], "sites/TCC/Docs", "Acervo");
        Assert.AreEqual(EstadoVerificacao.IncompatibilidadeDocumentada,
            Avaliar(raizBiblioteca, regra, "FoRms", diretorio: true).Estado);
        Assert.AreEqual(EstadoVerificacao.SemIncompatibilidadeDetectada,
            Avaliar(raizBiblioteca, regra, Path.Combine("Projeto", "Forms"), diretorio: true).Estado);
        Assert.AreEqual(EstadoVerificacao.SemIncompatibilidadeDetectada,
            Avaliar(subpasta, regra, "Forms", diretorio: true).Estado);
        Assert.AreEqual(EstadoVerificacao.NaoAvaliado,
            Avaliar(new ConfiguracaoCompatibilidade(Spo, [regra]), regra, "Forms").Estado);
    }

    [TestMethod]
    public void PrefixoDoDestinoMudaResultadoSemAlterarNomeLocal()
    {
        const string regra = "SPO-CAM-001";
        const string biblioteca = "sites/TCC/Documentos";
        var pasta = new string('a', 200);
        var prefixo = biblioteca + "/";
        var final = new string('é', 400 - prefixo.Length - pasta.Length - 1 - "%20.txt".Length) + "%20.txt";
        var relativo = Path.Combine(pasta, final);
        var limite = Avaliar(new ConfiguracaoCompatibilidade(Spo, [regra], biblioteca, ""), regra, relativo);
        var acima = Avaliar(new ConfiguracaoCompatibilidade(Spo, [regra], biblioteca, "A"), regra, relativo);
        Assert.AreEqual(EstadoVerificacao.SemIncompatibilidadeDetectada, limite.Estado);
        StringAssert.Contains(limite.Justificativa, "400 caracteres");
        Assert.AreEqual(EstadoVerificacao.IncompatibilidadeDocumentada, acima.Estado);
        Assert.AreEqual(EstadoVerificacao.NaoAvaliado,
            Avaliar(new ConfiguracaoCompatibilidade(Spo, [regra]), regra, relativo).Estado);
    }

    [TestMethod]
    public void NaoProjetaArquivoForaDaRaizDoInventario()
    {
        const string regra = "AZF-CAM-001";
        Assert.AreEqual(EstadoVerificacao.NaoAvaliado,
            Avaliar(new ConfiguracaoCompatibilidade(Azure, [regra], subpastaDestino: ""),
                regra, Path.Combine("..", "documento.txt")).Estado);
    }

    [TestMethod]
    public void ProfundidadeAzureContaPastasDoDestinoMasNaoONomeDoArquivo()
    {
        const string codigo = "AZF-CAM-003";
        var pastas = string.Join(Path.DirectorySeparatorChar, Enumerable.Repeat("p", 250));
        var raiz = new ConfiguracaoCompatibilidade(Azure, [codigo], subpastaDestino: "");
        var subpasta = new ConfiguracaoCompatibilidade(Azure, [codigo], subpastaDestino: "Lote");
        var arquivo = Path.Combine(pastas, "dado.txt");
        Assert.AreEqual(EstadoVerificacao.SemIncompatibilidadeDetectada, Avaliar(raiz, codigo, arquivo).Estado);
        Assert.AreEqual(EstadoVerificacao.SemIncompatibilidadeDetectada, Avaliar(raiz, codigo, pastas, diretorio: true).Estado);
        Assert.AreEqual(EstadoVerificacao.IncompatibilidadeDocumentada, Avaliar(subpasta, codigo, arquivo).Estado);
        Assert.AreEqual(EstadoVerificacao.IncompatibilidadeDocumentada, Avaliar(subpasta, codigo, pastas, diretorio: true).Estado);
        Assert.AreEqual(EstadoVerificacao.NaoAvaliado,
            Avaliar(new ConfiguracaoCompatibilidade(Azure, [codigo]), codigo, "dado.txt").Estado);
    }

    [TestMethod]
    public void AzureDetectaControlesESubstitutosIsoladosMasAceitaParesUnicode()
    {
        const string codigo = "AZF-NOME-001";
        var config = new ConfiguracaoCompatibilidade(Azure, [codigo]);
        foreach (var nome in new[] { "a\u0001.txt", "a\uD800.txt", "a\uDC00.txt" })
            Assert.AreEqual(EstadoVerificacao.IncompatibilidadeDocumentada, Avaliar(config, codigo, nome).Estado);
        foreach (var nome in new[] { "ação.txt", "a😀.txt", "a\uFFFE.txt", "a\uFFFF.txt" })
            Assert.AreEqual(EstadoVerificacao.SemIncompatibilidadeDetectada, Avaliar(config, codigo, nome).Estado);
    }

    [TestMethod]
    [DataRow("dado.txt.")]
    [DataRow("dado\uE000.txt")]
    public void ParticularidadesFileRestSaoAlertasCondicionais(string nome)
    {
        const string codigo = "AZF-NOME-003";
        Assert.AreEqual(EstadoVerificacao.Alerta, Avaliar(new ConfiguracaoCompatibilidade(Azure, [codigo]), codigo, nome).Estado);
    }

    [TestMethod]
    [DataRow("゛pasta", true)]
    [DataRow("\u3099pasta", true)]
    [DataRow("ဧpasta", true)]
    [DataRow("౧pasta", false)]
    public void PrefixoUnicodeSharePointAplicaAsFormasDocumentadasSomenteAPastas(string nome, bool vedado)
    {
        const string codigo = "SPO-NOME-005";
        var config = new ConfiguracaoCompatibilidade(Spo, [codigo]);
        Assert.AreEqual(vedado ? EstadoVerificacao.IncompatibilidadeDocumentada : EstadoVerificacao.SemIncompatibilidadeDetectada,
            Avaliar(config, codigo, nome, diretorio: true).Estado);
        Assert.AreEqual(EstadoVerificacao.SemIncompatibilidadeDetectada, Avaliar(config, codigo, nome + ".txt").Estado);
    }

    [TestMethod]
    [DataRow("arquivo.ASPX", true)]
    [DataRow("arquivo.TMP", true)]
    [DataRow(".Ds_Store", true)]
    [DataRow("desktop.ini", true)]
    [DataRow("arquivo.pdf", false)]
    [DataRow("arquivo.pst", false)]
    public void TiposSharePointSinalizamCondicoesDoClienteOuSite(string nome, bool alerta)
    {
        const string codigo = "SPO-TIPO-001";
        var config = new ConfiguracaoCompatibilidade(Spo, [codigo]);
        Assert.AreEqual(alerta ? EstadoVerificacao.Alerta : EstadoVerificacao.SemIncompatibilidadeDetectada,
            Avaliar(config, codigo, nome).Estado);
        Assert.AreEqual(EstadoVerificacao.NaoAplicavel, Avaliar(config, codigo, nome, diretorio: true).Estado);
    }
}
