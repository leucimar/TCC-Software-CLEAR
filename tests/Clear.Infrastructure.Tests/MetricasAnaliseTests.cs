using Clear.Application.Inventario;
using Clear.Application.Metricas;
using Clear.Domain.Analise;
using Clear.Infrastructure.Metricas;

namespace Clear.Infrastructure.Tests;

[TestClass]
public sealed class MetricasAnaliseTests
{
    private static MetricasAnalise Criar(string origem,long arquivos=4) => MetricasAnalise.Criar(
        origem,DateTimeOffset.UtcNow,new ResultadoInventario(arquivos,100,[],[],
            arquivos>0?[new(CategoriaAnalise.ArquivoVazio,1)]:[],arquivos>0?1:0,[],0),null,
        TimeSpan.FromSeconds(1),TimeSpan.Zero,TimeSpan.FromSeconds(2),4,"Vazios=True",
        new[]{CategoriaAnalise.ArquivoVazio});

    [TestMethod]
    public void PercentuaisDiferenciamDesativadoEAcervoVazio()
    {
        var m=Criar(Path.GetTempPath());
        Assert.AreEqual(25d,m.Categorias.Single(c=>c.Categoria=="ArquivoVazio").Percentual);
        Assert.IsNull(m.Categorias.Single(c=>c.Categoria=="SinalTemporal").Percentual);
        Assert.IsFalse(m.Categorias.Single(c=>c.Categoria=="DuplicataExata").Avaliada);
        Assert.AreEqual(0,m.Leitores);
        Assert.IsNull(Criar(Path.GetTempPath(),0).Categorias.Single(c=>c.Categoria=="ArquivoVazio").Percentual);
    }

    [TestMethod]
    public async Task BancoReabertoPreservaExecucoesETabelaCsvNaoSobrescreve()
    {
        var raiz=Path.Combine(Path.GetTempPath(),"CLEAR_Metricas_"+Guid.NewGuid().ToString("N"));
        var origem=Path.Combine(raiz,"acervo");Directory.CreateDirectory(origem);
        try {
            var banco=Path.Combine(raiz,"metricas.sqlite3");var csv=Path.Combine(raiz,"tabela.csv");
            var a=Criar(origem);var b=Criar(origem);
            await RepositorioMetricasAnalise.SalvarAsync(banco,a);
            await RepositorioMetricasAnalise.SalvarAsync(banco,b);
            var rows=await RepositorioMetricasAnalise.ConsultarAsync(banco);
            Assert.HasCount(2,rows);Assert.AreEqual(a.TotalSegundos,rows[0].TotalSegundos);
            Assert.AreEqual(25d,rows[0].Categorias.Single(c=>c.Categoria=="ArquivoVazio").Percentual);
            await RepositorioMetricasAnalise.ExportarCsvAsync(csv,rows);
            var bytes=await File.ReadAllBytesAsync(csv);
            Assert.IsTrue(bytes.Take(3).SequenceEqual(new byte[]{239,187,191}));
            Assert.HasCount(13,File.ReadAllLines(csv));
            await Assert.ThrowsAsync<IOException>(()=>RepositorioMetricasAnalise.ExportarCsvAsync(csv,rows));
            CollectionAssert.AreEqual(bytes,await File.ReadAllBytesAsync(csv));
            Assert.HasCount(0,Directory.GetFiles(raiz,"*.part"));
        } finally {Directory.Delete(raiz,true);}
    }

    [TestMethod]
    public async Task SaidasDentroDoAcervoSaoRecusadas()
    {
        var raiz=Path.Combine(Path.GetTempPath(),"CLEAR_Metricas_"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(raiz);
        try {
            var m=Criar(raiz);
            await Assert.ThrowsAsync<IOException>(()=>RepositorioMetricasAnalise.SalvarAsync(Path.Combine(raiz,"db.sqlite3"),m));
            await Assert.ThrowsAsync<IOException>(()=>RepositorioMetricasAnalise.ExportarCsvAsync(Path.Combine(raiz,"a.csv"),new[]{m}));
            Assert.HasCount(0,Directory.GetFiles(raiz));
        } finally {Directory.Delete(raiz,true);}
    }
}
