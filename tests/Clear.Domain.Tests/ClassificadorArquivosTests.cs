using Clear.Domain.Analise;
using Clear.Domain.Arquivos;

namespace Clear.Domain.Tests;

[TestClass]
public sealed class ClassificadorArquivosTests
{
    private readonly ClassificadorArquivos _classificador = new();

    [TestMethod]
    public void ArquivoComZeroBytesRecebeIndicioDeVazio()
    {
        var resultado = _classificador.Classificar(new ArquivoInventariado(@"C:\Teste\vazio.txt", 0));

        Assert.HasCount(1, resultado.Categorias);
        Assert.AreEqual(CategoriaAnalise.ArquivoVazio, resultado.Categorias[0]);
        StringAssert.Contains(resultado.Evidencias[0].Justificativa, "zero bytes");
    }

    [TestMethod]
    public void ArquivoRegularNaoRecebeIndicios()
    {
        var resultado = _classificador.Classificar(new ArquivoInventariado(@"C:\Teste\relatorio.docx", 12));

        Assert.HasCount(0, resultado.Categorias);
        Assert.HasCount(0, resultado.Evidencias);
    }

    [DataTestMethod]
    [DataRow("rascunho.tmp")]
    [DataRow("COPIA.BAK")]
    [DataRow("semanal.backup")]
    [DataRow("semanal.BACKUP")]
    [DataRow("~$planilha.xlsx")]
    public void PadroesExplicitosGeramIndicioSemConcluirDescarte(string nome)
    {
        var resultado = _classificador.Classificar(new ArquivoInventariado(@"C:\Teste\" + nome, 12));

        Assert.HasCount(1, resultado.Categorias);
        Assert.AreEqual(CategoriaAnalise.CopiaBackupOuTemporario, resultado.Categorias[0]);
        StringAssert.Contains(resultado.Evidencias[0].Justificativa, "indício");
    }

    [DataTestMethod]
    [DataRow("relatorio_tmp.txt")]
    [DataRow("backup_planejado.docx")]
    [DataRow("relatorio.tmpx")]
    [DataRow("arquivo.txt")]
    public void NomesSemPadraoExplicitoNaoSaoMarcados(string nome)
    {
        var resultado = _classificador.Classificar(new ArquivoInventariado(@"C:\Teste\" + nome, 19));

        Assert.HasCount(0, resultado.Categorias);
    }

    [TestMethod]
    public void NaoClassificaPeloNomeDeUmaPastaAncestral()
    {
        var resultado = _classificador.Classificar(
            new ArquivoInventariado(@"C:\pasta.backup\relatorio.docx", 20));

        Assert.HasCount(0, resultado.Categorias);
    }

    [TestMethod]
    public void ArquivoVazioETemporarioRecebeMultiplasCategorias()
    {
        var resultado = _classificador.Classificar(
            new ArquivoInventariado(@"C:\Teste\rascunho.tmp", 0));

        Assert.HasCount(2, resultado.Categorias);
        Assert.HasCount(2, resultado.Evidencias);
        CollectionAssert.Contains(resultado.Categorias.ToArray(), CategoriaAnalise.ArquivoVazio);
        CollectionAssert.Contains(resultado.Categorias.ToArray(), CategoriaAnalise.CopiaBackupOuTemporario);
    }

    [TestMethod]
    public void RegrasInjetadasPodemProduzirMaisDeUmaEvidenciaNaMesmaCategoria()
    {
        var classificador = new ClassificadorArquivos(new IRegraClassificacao[]
        {
            new RegraPersonalizada("Critério A."),
            new RegraPersonalizada("Critério B.")
        });

        var resultado = classificador.Classificar(new ArquivoInventariado(@"C:\Teste\x.txt", 1));

        Assert.HasCount(2, resultado.Evidencias);
        Assert.HasCount(1, resultado.Categorias);
    }

    [TestMethod]
    public void RejeitaArquivoNulo()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => _classificador.Classificar(null!));
    }

    private sealed class RegraPersonalizada(string motivo) : IRegraClassificacao
    {
        public EvidenciaAnalise Avaliar(ArquivoInventariado arquivo) =>
            new(CategoriaAnalise.SinalTemporal, motivo);
    }
}
