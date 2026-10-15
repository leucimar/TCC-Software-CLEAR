using Clear.Domain.Analise;
using Clear.Domain.Arquivos;

namespace Clear.Domain.Tests;

[TestClass]
public sealed class RegraSinalTemporalTests
{
    private static readonly DateTimeOffset Referencia = new(2026, 10, 14, 20, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public void ArquivoAnteriorAoLimiteRecebeJustificativaRastreavel()
    {
        var regra = new RegraSinalTemporal(365, Referencia);
        var arquivo = Arquivo(Referencia.AddDays(-366));

        var evidencia = regra.Avaliar(arquivo);

        Assert.IsNotNull(evidencia);
        Assert.AreEqual(CategoriaAnalise.SinalTemporal, evidencia.Categoria);
        StringAssert.Contains(evidencia.Justificativa, "365 dias");
        StringAssert.Contains(evidencia.Justificativa, "não comprova desuso");
        StringAssert.Contains(evidencia.Justificativa, "2026");
    }

    [TestMethod]
    public void DataExatamenteNoLimiteNaoRecebeSinal()
    {
        var regra = new RegraSinalTemporal(365, Referencia);
        Assert.IsNull(regra.Avaliar(Arquivo(Referencia.AddDays(-365))));
    }

    [TestMethod]
    public void UmInstanteAnteriorAoLimiteRecebeSinal()
    {
        var regra = new RegraSinalTemporal(365, Referencia);
        Assert.IsNotNull(regra.Avaliar(Arquivo(Referencia.AddDays(-365).AddTicks(-1))));
    }

    [TestMethod]
    public void ArquivoRecenteEDataFuturaNaoRecebemSinal()
    {
        var regra = new RegraSinalTemporal(365, Referencia);
        Assert.IsNull(regra.Avaliar(Arquivo(Referencia.AddDays(-10))));
        Assert.IsNull(regra.Avaliar(Arquivo(Referencia.AddDays(1))));
    }

    [TestMethod]
    public void SemDataDeModificacaoNaoConcluiAntiguidade()
    {
        var arquivo = new ArquivoInventariado(@"C:\Teste\semData.txt", 2);
        Assert.IsNull(new RegraSinalTemporal(365, Referencia).Avaliar(arquivo));
    }

    [TestMethod]
    public void IgnoraDataDeAcessoAntigaQuandoModificacaoEhRecente()
    {
        var arquivo = new ArquivoInventariado(@"C:\Teste\arquivo.txt", 2,
            ultimaModificacaoUtc: Referencia.AddDays(-2),
            ultimoAcessoUtc: Referencia.AddDays(-1000));

        Assert.IsNull(new RegraSinalTemporal(365, Referencia).Avaliar(arquivo));
    }

    [TestMethod]
    public void DatasComFusosDistintosSaoComparadasEmUtc()
    {
        var referenciaComFuso = new DateTimeOffset(2026, 10, 14, 17, 0, 0, TimeSpan.FromHours(-3));
        var regra = new RegraSinalTemporal(365, referenciaComFuso);
        var anterior = new DateTimeOffset(2025, 10, 14, 16, 59, 59, TimeSpan.FromHours(-3));
        // O cálculo da fronteira deve usar o mesmo instante, não o horário local.
        Assert.AreEqual(Referencia, regra.ReferenciaUtc);
        Assert.IsNotNull(regra.Avaliar(Arquivo(anterior)));
    }

    [TestMethod]
    public void ValidaLimiaresForaDaFaixa()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new RegraSinalTemporal(0, Referencia));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new RegraSinalTemporal(3651, Referencia));
    }

    [TestMethod]
    public void AceitaLimiaresNosExtremosPermitidos()
    {
        Assert.AreEqual(1, new RegraSinalTemporal(1, Referencia).DiasSemModificacao);
        Assert.AreEqual(3650, new RegraSinalTemporal(3650, Referencia).DiasSemModificacao);
    }

    [TestMethod]
    public void ArquivoAntigoVazioETemporarioTemTresCategorias()
    {
        var classificador = ClassificadorArquivos.CriarComSinalTemporal(365, Referencia);
        var resultado = classificador.Classificar(
            new ArquivoInventariado(@"C:\Teste\vazio.bak", 0, Referencia.AddDays(-500)));

        Assert.HasCount(3, resultado.Categorias);
        Assert.HasCount(3, resultado.Evidencias);
        CollectionAssert.Contains(resultado.Categorias.ToArray(), CategoriaAnalise.ArquivoVazio);
        CollectionAssert.Contains(resultado.Categorias.ToArray(), CategoriaAnalise.CopiaBackupOuTemporario);
        CollectionAssert.Contains(resultado.Categorias.ToArray(), CategoriaAnalise.SinalTemporal);
    }

    [TestMethod]
    public void ClassificadorPadraoNaoAtivaRegraTemporalImplicitamente()
    {
        var resultado = new ClassificadorArquivos().Classificar(
            new ArquivoInventariado(@"C:\Teste\antigo.txt", 5, Referencia.AddDays(-900)));

        Assert.HasCount(0, resultado.Categorias);
    }

    private static ArquivoInventariado Arquivo(DateTimeOffset data) =>
        new(@"C:\Teste\relatorio.txt", 123, data);
}
