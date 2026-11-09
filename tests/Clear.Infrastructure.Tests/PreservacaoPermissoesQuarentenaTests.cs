using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;
using Clear.Infrastructure.Quarentena;

namespace Clear.Infrastructure.Tests;

// Testes locais de descritores NTFS; não simulam login AD/SMB.
// Escritos nesta alteração, ainda NÃO EXECUTADOS.
[TestClass]
[SupportedOSPlatform("windows")]
public sealed class PreservacaoPermissoesQuarentenaTests
{
    private string _raiz = null!;
    private string _origem = null!;
    private string _arquivo = null!;
    private string _quarentena = null!;
    private static readonly AccessControlSections Secoes = AccessControlSections.Access |
        AccessControlSections.Owner | AccessControlSections.Group;

    [TestInitialize]
    public void Preparar()
    {
        if (!OperatingSystem.IsWindows()) Assert.Inconclusive("Requer NTFS/Windows.");
        _raiz = Path.Combine(Path.GetTempPath(), "CLEAR_ACL_" + Guid.NewGuid().ToString("N"));
        _origem = Path.Combine(_raiz, "Base_dados_oficial");
        _quarentena = Path.Combine(_raiz, "Base_dados_quarentena");
        _arquivo = Path.Combine(_origem, "FIN", "documento.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(_arquivo)!);
        // SID sintético: verifica preservação de identidades sem criar contas.
        // Não é evidência de autenticação nem de acesso efetivo de um usuário AD.
        var seguranca = new DirectoryInfo(Path.GetDirectoryName(_arquivo)!).GetAccessControl(Secoes);
        seguranca.SetAccessRuleProtection(true, false);
        using var identidade = WindowsIdentity.GetCurrent();
        seguranca.AddAccessRule(new FileSystemAccessRule(identidade.User!, FileSystemRights.FullControl,
            InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
        seguranca.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier("S-1-5-21-100-200-300-4101"), FileSystemRights.ReadAndExecute,
            InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
        new DirectoryInfo(Path.GetDirectoryName(_arquivo)!).SetAccessControl(seguranca);
        File.WriteAllText(_arquivo, "Arquivo descartável de teste de permissões.");
    }

    [TestCleanup]
    public void Limpar()
    {
        if (_raiz is not null && Directory.Exists(_raiz)) Directory.Delete(_raiz, true);
    }

    private PlanoQuarentenaM7 Plano() => new(Guid.NewGuid().ToString("N"), "teste-acl", 1,
        _origem, _arquivo, Path.Combine(_quarentena, "Base_dados_oficial", "FIN", "documento.txt"),
        new FileInfo(_arquivo).Length, File.GetLastWriteTimeUtc(_arquivo), "nao-utilizado-pelo-helper", _quarentena);

    [TestMethod]
    public void PastasRecriadasPossuemAclProtegidaESidsDaOrigem()
    {
        var plano = Plano();
        var antes = new DirectoryInfo(Path.GetDirectoryName(_arquivo)!).GetAccessControl(Secoes).GetSecurityDescriptorSddlForm(Secoes);
        var preservacao = PreservacaoPermissoesQuarentena.Preparar(plano);
        var destino = new DirectoryInfo(Path.GetDirectoryName(plano.CaminhoDestino)!).GetAccessControl(Secoes);
        Assert.IsTrue(destino.AreAccessRulesProtected);
        StringAssert.Contains(destino.GetSecurityDescriptorSddlForm(Secoes), "S-1-5-21-100-200-300-4101");
        Assert.AreEqual(antes, new DirectoryInfo(Path.GetDirectoryName(_arquivo)!).GetAccessControl(Secoes).GetSecurityDescriptorSddlForm(Secoes));
        preservacao.ValidarAntesDaMovimentacao();
        File.Move(_arquivo, plano.CaminhoDestino);
        preservacao.ValidarDepoisDaMovimentacao();
    }

    [TestMethod]
    public void PastaExistenteComHerancaAbertaNaoEhReconfiguradaNemUsada()
    {
        var plano = Plano();
        var existente = Path.Combine(_quarentena, "Base_dados_oficial");
        Directory.CreateDirectory(existente);
        var antes = new DirectoryInfo(existente).GetAccessControl(Secoes).GetSecurityDescriptorSddlForm(Secoes);
        Assert.ThrowsExactly<IOException>(() => PreservacaoPermissoesQuarentena.Preparar(plano));
        Assert.IsTrue(File.Exists(_arquivo));
        Assert.IsFalse(File.Exists(plano.CaminhoDestino));
        Assert.AreEqual(antes, new DirectoryInfo(existente).GetAccessControl(Secoes).GetSecurityDescriptorSddlForm(Secoes));
    }

    [TestMethod]
    public void AlteracaoDeAclDurantePreparacaoBloqueiaMovimento()
    {
        var plano = Plano();
        var preservacao = PreservacaoPermissoesQuarentena.Preparar(plano);
        var seguranca = new FileInfo(_arquivo).GetAccessControl(Secoes);
        seguranca.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier("S-1-5-21-100-200-300-4102"),
            FileSystemRights.ReadData, AccessControlType.Allow));
        new FileInfo(_arquivo).SetAccessControl(seguranca);
        Assert.ThrowsExactly<IOException>(preservacao.ValidarAntesDaMovimentacao);
        Assert.IsTrue(File.Exists(_arquivo));
        Assert.IsFalse(File.Exists(plano.CaminhoDestino));
    }

    [TestMethod]
    public void RestauracaoConfereArquivoSemAlterarAclDasPastasOriginais()
    {
        var plano = Plano();
        var preservacao = PreservacaoPermissoesQuarentena.Preparar(plano);
        File.Move(_arquivo, plano.CaminhoDestino);
        preservacao.ValidarDepoisDaMovimentacao();
        var pasta = new DirectoryInfo(Path.GetDirectoryName(_arquivo)!);
        var antes = pasta.GetAccessControl(Secoes).GetSecurityDescriptorSddlForm(Secoes);
        var restauracao = PreservacaoPermissoesQuarentena.PrepararRestauracao(plano.CaminhoDestino, _arquivo);
        restauracao.ValidarAntesDaMovimentacao();
        File.Move(plano.CaminhoDestino, _arquivo);
        restauracao.ValidarDepoisDaMovimentacao();
        Assert.AreEqual(antes, pasta.GetAccessControl(Secoes).GetSecurityDescriptorSddlForm(Secoes));
    }
}
