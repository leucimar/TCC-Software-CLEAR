using Clear.Domain.Analise;
using Clear.Domain.Arquivos;

namespace Clear.Domain.Tests;

[TestClass]
public sealed class FiltrosSelecionadosTests
{
    private static readonly DateTimeOffset Agora = new(2026, 10, 15, 20, 0, 0, TimeSpan.Zero);
    private static readonly ArquivoInventariado AntigoVazioBackup =
        new(@"C:\Teste\vazio.bak", 0, Agora.AddDays(-500), Agora.AddDays(-500));

    [TestMethod]
    public void NenhumaRegraGeralHabilitadaNaoClassificaArquivo()
    {
        var resultado = ClassificadorArquivos.CriarComFiltrosSelecionados(
            false, false, null, Agora).Classificar(AntigoVazioBackup);

        Assert.HasCount(0, resultado.Categorias);
    }

    [TestMethod]
    public void AtivarSomenteArquivoVazioNaoAtivaBackupOuTemporal()
    {
        var resultado = ClassificadorArquivos.CriarComFiltrosSelecionados(
            true, false, null, Agora).Classificar(AntigoVazioBackup);

        Assert.HasCount(1, resultado.Categorias);
        Assert.AreEqual(CategoriaAnalise.ArquivoVazio, resultado.Categorias[0]);
    }

    [TestMethod]
    public void AtivarSomenteBackupNaoAtivaVazioOuTemporal()
    {
        var resultado = ClassificadorArquivos.CriarComFiltrosSelecionados(
            false, true, null, Agora).Classificar(AntigoVazioBackup);

        Assert.HasCount(1, resultado.Categorias);
        Assert.AreEqual(CategoriaAnalise.CopiaBackupOuTemporario, resultado.Categorias[0]);
    }

    [TestMethod]
    public void TresFiltrosAtivosProduzemTresCategorias()
    {
        var resultado = ClassificadorArquivos.CriarComFiltrosSelecionados(
            true, true, 365, Agora).Classificar(AntigoVazioBackup);

        Assert.HasCount(3, resultado.Categorias);
    }

    [TestMethod]
    public void TemporalInvalidoEhRejeitadoSomenteQuandoAtivado()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            ClassificadorArquivos.CriarComFiltrosSelecionados(true, true, 0, Agora));

        Assert.HasCount(2, ClassificadorArquivos.CriarComFiltrosSelecionados(
            true, true, null, Agora).Classificar(AntigoVazioBackup).Categorias);
    }
}
