using Clear.Domain.Analise;
using Clear.Domain.Arquivos;

namespace Clear.Domain.Tests;

[TestClass]
public sealed class ModeloDominioTests
{
    [TestMethod]
    public void PermiteArquivosVaziosESemDataDeAcesso()
    {
        var arquivo = new ArquivoInventariado(@"C:\Dados\vazio.txt", 0);
        Assert.AreEqual(0L, arquivo.TamanhoBytes);
        Assert.IsNull(arquivo.UltimoAcessoUtc);
    }

    [TestMethod]
    public void RejeitaCaminhosEmBranco()
    {
        Assert.ThrowsExactly<ArgumentException>(() => new ArquivoInventariado(" ", 1));
    }

    [TestMethod]
    public void RejeitaTamanhosNegativos()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => new ArquivoInventariado(@"C:\Dados\arquivo.txt", -1));
    }

    [TestMethod]
    public void ConverteDataRecebidaParaUtc()
    {
        var data = new DateTimeOffset(2026, 10, 1, 12, 0, 0, TimeSpan.FromHours(-3));
        var arquivo = new ArquivoInventariado(@"C:\Dados\arquivo.txt", 10, data);
        Assert.AreEqual(TimeSpan.Zero, arquivo.UltimaModificacaoUtc!.Value.Offset);
        Assert.AreEqual(data.UtcDateTime, arquivo.UltimaModificacaoUtc.Value.UtcDateTime);
    }

    [TestMethod]
    public void UmArquivoPodeTerVariasCategorias()
    {
        var resultado = new ResultadoAnaliseArquivo(
            new ArquivoInventariado(@"C:\Dados\copia.tmp", 0),
            new[]
            {
                new EvidenciaAnalise(CategoriaAnalise.ArquivoVazio, "Tamanho zero."),
                new EvidenciaAnalise(CategoriaAnalise.CopiaBackupOuTemporario, "Nome suspeito.")
            });

        Assert.AreEqual(2, resultado.Categorias.Count);
        CollectionAssert.Contains(resultado.Categorias.ToArray(), CategoriaAnalise.ArquivoVazio);
        CollectionAssert.Contains(resultado.Categorias.ToArray(), CategoriaAnalise.CopiaBackupOuTemporario);
    }

    [TestMethod]
    public void EvidenciasSaoCopiadasEValoresDeCategoriaNaoSeRepetem()
    {
        var evidencias = new List<EvidenciaAnalise>
        {
            new(CategoriaAnalise.SinalTemporal, "Regra 1."),
            new(CategoriaAnalise.SinalTemporal, "Regra 2.")
        };
        var resultado = new ResultadoAnaliseArquivo(
            new ArquivoInventariado(@"C:\Dados\arquivo.txt", 20), evidencias);
        evidencias.Clear();

        Assert.AreEqual(2, resultado.Evidencias.Count);
        Assert.AreEqual(1, resultado.Categorias.Count);
    }

    [TestMethod]
    public void RejeitaJustificativaVaziaECategoriaInvalida()
    {
        Assert.ThrowsExactly<ArgumentException>(
            () => new EvidenciaAnalise(CategoriaAnalise.ArquivoVazio, " "));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => new EvidenciaAnalise((CategoriaAnalise)999, "Desconhecida."));
    }
}
