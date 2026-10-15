using Clear.Domain.Arquivos;
using Clear.Domain.Duplicatas;

namespace Clear.Domain.Tests;

[TestClass]
public sealed class GrupoDuplicatasExatasTests
{
    private static ArquivoInventariado Arquivo(string nome, long tamanho) =>
        new(@"C:\Amostra\" + nome, tamanho);

    [TestMethod]
    public void GrupoVerificadoMantemMembrosETamanhoEEstimativaTeorica()
    {
        var grupo = new GrupoDuplicatasExatas(12, new string('a', 64),
            new[] { Arquivo("um.txt", 12), Arquivo("dois.txt", 12), Arquivo("tres.dat", 12) });

        Assert.HasCount(3, grupo.Arquivos);
        Assert.AreEqual(24L, grupo.BytesPotencialmenteRedundantes);
        Assert.AreEqual(new string('A', 64), grupo.Sha256Hex);
    }

    [TestMethod]
    public void ArquivosVaziosPodemFormarGrupoSemEconomiaTeorica()
    {
        var grupo = new GrupoDuplicatasExatas(0, new string('0', 64),
            new[] { Arquivo("um.txt", 0), Arquivo("dois.txt", 0) });
        Assert.AreEqual(0L, grupo.BytesPotencialmenteRedundantes);
    }

    [TestMethod]
    public void GrupoExigePeloMenosDoisMembros()
    {
        Assert.ThrowsExactly<ArgumentException>(() =>
            new GrupoDuplicatasExatas(2, new string('0', 64),
                new[] { Arquivo("um.txt", 2) }));
    }

    [TestMethod]
    public void GrupoNaoMisturaTamanhos()
    {
        Assert.ThrowsExactly<ArgumentException>(() =>
            new GrupoDuplicatasExatas(2, new string('0', 64),
                new[] { Arquivo("um.txt", 2), Arquivo("outro.txt", 3) }));
    }

    [TestMethod]
    public void GrupoNaoRepeteMesmoCaminho()
    {
        Assert.ThrowsExactly<ArgumentException>(() =>
            new GrupoDuplicatasExatas(2, new string('0', 64),
                new[] { Arquivo("um.txt", 2), Arquivo("UM.txt", 2) }));
    }

    [TestMethod]
    public void GrupoExigeFormatoSha256()
    {
        Assert.ThrowsExactly<ArgumentException>(() =>
            new GrupoDuplicatasExatas(2, "hash-curto",
                new[] { Arquivo("um.txt", 2), Arquivo("outro.txt", 2) }));
    }
}
