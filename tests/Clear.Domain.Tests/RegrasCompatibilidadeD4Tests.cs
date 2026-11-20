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
}
