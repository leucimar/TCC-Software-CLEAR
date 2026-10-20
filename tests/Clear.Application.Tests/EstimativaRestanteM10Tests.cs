using Clear.Application.Progresso;

namespace Clear.Application.Tests;

[TestClass]
public sealed class EstimativaRestanteM10Tests
{
    [TestMethod]
    public void MetadeDosArquivosEBytesPreveTempoSimilarAoDecorrido()
    {
        var eta = EstimativaRestante.Calcular(50, 100, 500, 1000,
            TimeSpan.FromSeconds(12));
        Assert.IsNotNull(eta);
        Assert.AreEqual(12, eta.Value.TotalSeconds, 0.01);
    }

    [TestMethod]
    public void PesosCombinadosEvitamPrevisaoSomentePeloNumero()
    {
        var eta = EstimativaRestante.Calcular(3, 100, 400, 1000,
            TimeSpan.FromSeconds(15));
        Assert.IsNotNull(eta);
        // 3% dos arquivos, mas 40% dos bytes: ETA não confunde
        // 3% de arquivos com somente 3% do trabalho realizado.
        Assert.IsLessThan(eta.Value.TotalMinutes, 3.0);
    }

    [TestMethod]
    public void SemAvancoOuAmostraCurtaRetornaCalculando()
    {
        Assert.IsNull(EstimativaRestante.Calcular(0, 100, 0, 1000,
            TimeSpan.FromSeconds(20)));
        Assert.IsNull(EstimativaRestante.Calcular(1, 100, 50, 1000,
            TimeSpan.FromMilliseconds(800)));
        Assert.AreEqual("ETA: calculando...", EstimativaRestante.Exibir(null));
    }

    [TestMethod]
    public void QuandoTodasAsUnidadesTerminamRetornaZero()
    {
        Assert.AreEqual(TimeSpan.Zero, EstimativaRestante.Calcular(
            100, 100, 1000, 1000, TimeSpan.FromMinutes(1)));
        Assert.AreEqual("ETA: concluído",
            EstimativaRestante.Exibir(TimeSpan.Zero));
    }

    [TestMethod]
    public void InformacaoInconsistenteNuncaFabricaEta()
    {
        Assert.IsNull(EstimativaRestante.Calcular(150, 100, 10, 10,
            TimeSpan.FromSeconds(12)));
        Assert.IsNull(EstimativaRestante.Calcular(5, 0, 10, 10,
            TimeSpan.FromSeconds(12)));
        Assert.IsNull(EstimativaRestante.Calcular(2, 100, 15, 10,
            TimeSpan.FromSeconds(12)));
    }
}
