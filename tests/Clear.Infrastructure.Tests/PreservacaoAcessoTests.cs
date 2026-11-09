using System.Security.Cryptography;
using System.Text.Json;
using System.Security.AccessControl;
using System.Security.Principal;
using Clear.Application.Duplicatas;
using Clear.Domain.Arquivos;
using Clear.Infrastructure.Arquivos;
using Clear.Infrastructure.Duplicatas;
using Clear.Infrastructure.Inventario;

namespace Clear.Infrastructure.Tests;

[TestClass]
public sealed class PreservacaoAcessoTests
{
    private string _raiz = null!;
    [TestInitialize] public void Preparar() => Directory.CreateDirectory(_raiz = Path.Combine(Path.GetTempPath(), "CLEAR_Preservacao_" + Guid.NewGuid().ToString("N")));
    [TestCleanup] public void Limpar() => Directory.Delete(_raiz, true);
    private string Arquivo(string nome="dado.txt")
    { var caminho=Path.Combine(_raiz,nome); File.WriteAllText(caminho,"Conteúdo controlado, igual e seguro.");return caminho; }
    private static FileStream Ler(string caminho,FileShare share,int buffer,bool proteger)
    { if(proteger)throw new FalhaPreservacaoAcessoException("Sem WRITE_ATTRIBUTES (simulado).");return new FileStream(caminho,FileMode.Open,FileAccess.Read,share,buffer,true); }

    [TestMethod]
    public void EstritoNaoAbreConteudoQuandoProtecaoFalha()
    {
        var tentativas=0;
        using var sessao=new SessaoPreservacaoAcesso(PoliticaSemPreservacao.NaoLerConteudo,(p,s,b,proteger)=>
        { tentativas++;Assert.IsTrue(proteger);return Ler(p,s,b,proteger); });
        Assert.ThrowsExactly<FalhaPreservacaoAcessoException>(()=>SessaoPreservacaoAcesso.Abrir(Arquivo()));
        Assert.AreEqual(1,tentativas);Assert.AreEqual(1L,sessao.Resumo.LeiturasIgnoradas);
        Assert.AreEqual(0L,sessao.Resumo.LeiturasSemGarantia);
    }
    [TestMethod]
    public async Task MistoAutorizaFallbackERegistraSemDeclararProtecao()
    {
        using var sessao=new SessaoPreservacaoAcesso(PoliticaSemPreservacao.LerComAviso,Ler);
        await using(var fluxo=SessaoPreservacaoAcesso.Abrir(Arquivo()))
            Assert.AreEqual(32,(await SHA256.HashDataAsync(fluxo)).Length);
        var resumo=sessao.Resumo;
        Assert.AreEqual(0L,resumo.AberturasProtegidas);Assert.AreEqual(1L,resumo.LeiturasSemGarantia);
        Assert.AreEqual("LeituraSemGarantia",resumo.Ocorrencias.Single().Estado);
        var auditoria=Path.Combine(_raiz,"auditoria.json");await sessao.SalvarAsync(auditoria);
        using var json=JsonDocument.Parse(await File.ReadAllTextAsync(auditoria));
        Assert.AreEqual(1,json.RootElement.GetProperty("LeiturasSemGarantia").GetInt32());
        StringAssert.Contains(await File.ReadAllTextAsync(Path.ChangeExtension(auditoria,"csv")),"LeituraSemGarantia");
    }
    [TestMethod]
    public void FalhaDeSegurancaOuIntegridadeNaoAcionaFallback()
    {
        var chamadas=0;
        using var sessao=new SessaoPreservacaoAcesso(PoliticaSemPreservacao.LerComAviso,(_,_,_,_)=>
        { chamadas++;throw new IOException("Reparse point/inconsistência."); });
        Assert.ThrowsExactly<IOException>(()=>SessaoPreservacaoAcesso.Abrir(Arquivo()));
        Assert.AreEqual(1,chamadas);Assert.AreEqual(0L,sessao.Resumo.LeiturasSemGarantia);
    }
    [TestMethod]
    public async Task PoliticaFluiParaOitoLeitoresConcorrentes()
    {
        var arquivo=Arquivo();
        using var sessao=new SessaoPreservacaoAcesso(PoliticaSemPreservacao.LerComAviso,Ler);
        await Task.WhenAll(Enumerable.Range(0,8).Select(_=>Task.Run(async()=>
        {await using var f=SessaoPreservacaoAcesso.Abrir(arquivo);await SHA256.HashDataAsync(f);} )));
        Assert.AreEqual(8L,sessao.Resumo.LeiturasSemGarantia);Assert.HasCount(8,sessao.Resumo.Ocorrencias);
    }
    [TestMethod]
    public void EscoposAninhadosRestauramPoliticaAnterior()
    {
        var arquivo=Arquivo();
        using var externo=new SessaoPreservacaoAcesso(PoliticaSemPreservacao.NaoLerConteudo,Ler);
        using(var interno=new SessaoPreservacaoAcesso(PoliticaSemPreservacao.LerComAviso,Ler))
        {using var fluxo=SessaoPreservacaoAcesso.Abrir(arquivo);Assert.AreEqual(1L,interno.Resumo.LeiturasSemGarantia);}
        Assert.ThrowsExactly<FalhaPreservacaoAcessoException>(()=>SessaoPreservacaoAcesso.Abrir(arquivo));
        Assert.AreEqual(1L,externo.Resumo.LeiturasIgnoradas);
    }
    [TestMethod]
    public async Task ConteudoIgnoradoGeraCoberturaIncompletaSemDuplicatasFalsas()
    {
        Arquivo("a.txt");Arquivo("b.txt");
        using var sessao=new SessaoPreservacaoAcesso(PoliticaSemPreservacao.NaoLerConteudo,Ler);
        var resultado=await new DetectarDuplicatasExatas(new FonteInventarioSistemaArquivos(),
            new LeitorConteudoDuplicatasSistemaArquivos(),8).ExecutarAsync(_raiz);
        Assert.IsTrue(resultado.Incompleto);Assert.AreEqual(0L,resultado.ArquivosComHash);
        Assert.HasCount(0,resultado.Grupos);Assert.AreEqual(2L,sessao.Resumo.LeiturasIgnoradas);
    }
    [TestMethod]
    public async Task MistoMantemDeteccaoExataMasReportaFaltaDeGarantia()
    {
        Arquivo("a.txt");Arquivo("b.txt");
        using var sessao=new SessaoPreservacaoAcesso(PoliticaSemPreservacao.LerComAviso,Ler);
        var resultado=await new DetectarDuplicatasExatas(new FonteInventarioSistemaArquivos(),
            new LeitorConteudoDuplicatasSistemaArquivos(),8).ExecutarAsync(_raiz);
        Assert.IsFalse(resultado.Incompleto);Assert.HasCount(1,resultado.Grupos);
        Assert.AreEqual(4L,sessao.Resumo.LeiturasSemGarantia); // two hashes + both byte-comparison handles
    }
    [TestMethod]
    public async Task WindowsNativoPreservaDatasNosHashesEComparacao()
    {
        if(!OperatingSystem.IsWindows()) {Assert.Inconclusive("Requer API Windows.");return;}
        var arquivos=new[]{Arquivo("a.txt"),Arquivo("b.txt")};
        var acesso=new DateTime(2023,2,3,4,5,6,DateTimeKind.Utc);
        var modificacao=acesso.AddDays(1);
        foreach(var p in arquivos){File.SetLastAccessTimeUtc(p,acesso);File.SetLastWriteTimeUtc(p,modificacao);}
        using var sessao=new SessaoPreservacaoAcesso(PoliticaSemPreservacao.NaoLerConteudo);
        foreach(var leitores in new[]{1,8})
        {
            var resultado=await new DetectarDuplicatasExatas(new FonteInventarioSistemaArquivos(),
                new LeitorConteudoDuplicatasSistemaArquivos(),leitores).ExecutarAsync(_raiz);
            Assert.IsFalse(resultado.Incompleto);Assert.HasCount(1,resultado.Grupos);
        }
        foreach(var p in arquivos)
        {Assert.AreEqual(acesso,File.GetLastAccessTimeUtc(p));Assert.AreEqual(modificacao,File.GetLastWriteTimeUtc(p));}
        Assert.AreEqual(8L,sessao.Resumo.AberturasProtegidas);Assert.AreEqual(0L,sessao.Resumo.LeiturasSemGarantia);
    }
    [TestMethod]
    public async Task DiarioPersisteInicioAntesDaLeituraEResumoNaoSobrescreve()
    {
        var diario=Path.Combine(_raiz,"eventos.jsonl");
        using var sessao=new SessaoPreservacaoAcesso(PoliticaSemPreservacao.NaoLerConteudo,diario);
        using(var leitor=new StreamReader(new FileStream(diario,FileMode.Open,FileAccess.Read,FileShare.ReadWrite)))
            StringAssert.Contains(await leitor.ReadToEndAsync(),"Inicio");
        var resumo=Path.Combine(_raiz,"resumo.json");await sessao.SalvarAsync(resumo);
        await Assert.ThrowsAsync<IOException>(()=>sessao.SalvarAsync(resumo));
    }
    [TestMethod]
    public async Task WindowsSemPermissaoDeAtributosRespeitaAsDuasPoliticas()
    {
        if(!OperatingSystem.IsWindows()){Assert.Inconclusive("Requer ACL Windows.");return;}
        var arquivo=Arquivo();var info=new FileInfo(arquivo);var original=info.GetAccessControl();
        var alterado=info.GetAccessControl();
        using var identidade=WindowsIdentity.GetCurrent();
        alterado.AddAccessRule(new FileSystemAccessRule(identidade.User!,FileSystemRights.WriteAttributes,AccessControlType.Deny));
        try
        {
            info.SetAccessControl(alterado);
            using(var estrito=new SessaoPreservacaoAcesso(PoliticaSemPreservacao.NaoLerConteudo))
            {
                Assert.ThrowsExactly<FalhaPreservacaoAcessoException>(()=>SessaoPreservacaoAcesso.Abrir(arquivo));
                Assert.AreEqual(1L,estrito.Resumo.LeiturasIgnoradas);
            }
            using(var misto=new SessaoPreservacaoAcesso(PoliticaSemPreservacao.LerComAviso))
            {
                await using var fluxo=SessaoPreservacaoAcesso.Abrir(arquivo);
                Assert.AreEqual(32,(await SHA256.HashDataAsync(fluxo)).Length);
                Assert.AreEqual(1L,misto.Resumo.LeiturasSemGarantia);
            }
        }
        finally{info.SetAccessControl(original);}
    }
}
