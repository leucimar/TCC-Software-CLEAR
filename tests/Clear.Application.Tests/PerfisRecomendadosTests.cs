using Clear.Application.Compatibilidade;
using Clear.Domain.Compatibilidade;

namespace Clear.Application.Tests;

[TestClass]
public sealed class PerfisRecomendadosTests
{
    [TestMethod]
    public void SemDestinoNaoSugereRegrasEspecificas()
    {
        Assert.HasCount(0, PerfisRecomendados.Para(null));
    }

    [TestMethod]
    public void AzureSelecionaApenasSuasSeteVerificacoes()
    {
        var ids = PerfisRecomendados.Para(AmbienteDestino.AzureFilesClassicoSmb);
        Assert.HasCount(7, ids);
        CollectionAssert.Contains(ids.ToArray(), "AZF-NOME-001");
        CollectionAssert.Contains(ids.ToArray(), "AZF-TAM-001");
        Assert.IsFalse(ids.Any(id => id.StartsWith("SPO-", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void SharePointSelecionaApenasSuasNoveVerificacoes()
    {
        var ids = PerfisRecomendados.Para(AmbienteDestino.SharePointOneDriveEmpresarial);
        Assert.HasCount(9, ids);
        CollectionAssert.Contains(ids.ToArray(), "SPO-NOME-004");
        CollectionAssert.Contains(ids.ToArray(), "SPO-TAM-001");
        Assert.IsFalse(ids.Any(id => id.StartsWith("AZF-", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void SugestaoNaoModificaCatalogoNemMantemEstadoEntreChamadas()
    {
        var primeira = PerfisRecomendados.Para(AmbienteDestino.AzureFilesClassicoSmb);
        var segunda = PerfisRecomendados.Para(AmbienteDestino.AzureFilesClassicoSmb);
        Assert.AreEqual(primeira[0], segunda[0]);
        Assert.HasCount(16, CatalogoVerificacoes.Definicoes);
    }
}
