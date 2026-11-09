using Clear.Domain.Analise;
using Clear.Domain.Arquivos;
namespace Clear.Domain.Tests;
[TestClass]
public sealed class CriterioTemporalTests
{
    private static readonly DateTimeOffset Agora=new(2026,11,6,12,0,0,TimeSpan.Zero);
    [TestMethod]
    public void CriteriosExclusivosProduzemResultadosDistintos()
    {
        var antigo=Agora.AddDays(-700);var recente=Agora.AddDays(-1);
        var a=new ArquivoInventariado(@"C:\dado.txt",50,antigo,recente);
        Assert.IsNull(new RegraSinalTemporal(365,Agora,CriterioTemporal.UltimoAcesso).Avaliar(a));
        var evidencia=new RegraSinalTemporal(365,Agora,CriterioTemporal.UltimaModificacao).Avaliar(a);
        Assert.IsNotNull(evidencia);StringAssert.Contains(evidencia.Justificativa,"Última modificação");
        var b=new ArquivoInventariado(@"C:\outro.txt",50,recente,antigo);
        Assert.IsNotNull(new RegraSinalTemporal(365,Agora,CriterioTemporal.UltimoAcesso).Avaliar(b));
        Assert.IsNull(new RegraSinalTemporal(365,Agora,CriterioTemporal.UltimaModificacao).Avaliar(b));
    }
    [TestMethod]
    public void CriterioAusenteNaoRecorreAoOutroCampo()
    {
        var acesso=new ArquivoInventariado(@"C:\acesso.txt",1,null,Agora.AddDays(-900));
        var modificado=new ArquivoInventariado(@"C:\modificado.txt",1,Agora.AddDays(-900),null);
        Assert.IsNull(new RegraSinalTemporal(365,Agora,CriterioTemporal.UltimaModificacao).Avaliar(acesso));
        Assert.IsNull(new RegraSinalTemporal(365,Agora,CriterioTemporal.UltimoAcesso).Avaliar(modificado));
    }
    [TestMethod]
    public void ModificacaoRespeitaFronteiraSentinelaEFuturo()
    {
        var regra=new RegraSinalTemporal(365,Agora,CriterioTemporal.UltimaModificacao);
        foreach(var data in new[]{Agora.AddDays(-365),Agora.AddDays(1),DateTimeOffset.FromFileTime(0)})
            Assert.IsNull(regra.Avaliar(new ArquivoInventariado(@"C:\dado.txt",1,data)));
        Assert.IsNotNull(regra.Avaliar(new ArquivoInventariado(@"C:\dado.txt",1,Agora.AddDays(-365).AddTicks(-1))));
    }
    [TestMethod]
    public void CriterioInvalidoRejeitadoEPadraoAntigoPreservado()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(()=>new RegraSinalTemporal(365,Agora,(CriterioTemporal)55));
        Assert.AreEqual(CriterioTemporal.UltimoAcesso,new RegraSinalTemporal(365,Agora).Criterio);
    }
}
