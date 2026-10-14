using Clear.Application.Inventario;
using Clear.Infrastructure.Inventario;

namespace Clear.Infrastructure.Tests;

[TestClass]
public sealed class FonteInventarioSistemaArquivosTests
{
    [TestMethod]
    public async Task InventariaArquivosEmSubpastasEContabilizaBytes()
    {
        var raiz = CriarRaiz();
        try
        {
            Directory.CreateDirectory(Path.Combine(raiz, "Subpasta"));
            await File.WriteAllTextAsync(Path.Combine(raiz, "a.txt"), "ABC");
            await File.WriteAllTextAsync(Path.Combine(raiz, "Subpasta", "b.txt"), "12345");
            await File.WriteAllTextAsync(Path.Combine(raiz, "vazio.txt"), "");

            var resultado = await new InventariarDiretorio(
                new FonteInventarioSistemaArquivos()).ExecutarAsync(raiz);

            Assert.AreEqual(3L, resultado.QuantidadeArquivos);
            Assert.AreEqual(8L, resultado.TotalBytes);
            Assert.HasCount(3, resultado.AmostraArquivos);
            Assert.AreEqual(0L, resultado.TotalOcorrencias);
        }
        finally { ApagarRaiz(raiz); }
    }

    [TestMethod]
    public async Task PastaVaziaRetornaZero()
    {
        var raiz = CriarRaiz();
        try
        {
            var resultado = await new InventariarDiretorio(
                new FonteInventarioSistemaArquivos()).ExecutarAsync(raiz);
            Assert.AreEqual(0L, resultado.QuantidadeArquivos);
            Assert.AreEqual(0L, resultado.TotalBytes);
        }
        finally { ApagarRaiz(raiz); }
    }

    [TestMethod]
    public async Task PastaInexistenteGeraErroSemCriarDiretorio()
    {
        var raiz = Path.Combine(Path.GetTempPath(), "CLEAR_M2_inexistente_" + Guid.NewGuid().ToString("N"));

        await Assert.ThrowsExactlyAsync<DirectoryNotFoundException>(
            () => new InventariarDiretorio(new FonteInventarioSistemaArquivos())
                .ExecutarAsync(raiz));

        Assert.IsFalse(Directory.Exists(raiz));
    }

    [TestMethod]
    public async Task CaminhoRelativoNaoEhPermitido()
    {
        var servico = new InventariarDiretorio(new FonteInventarioSistemaArquivos());
        await Assert.ThrowsExactlyAsync<ArgumentException>(
            () => servico.ExecutarAsync("PastaRelativaTeste"));
    }

    [TestMethod]
    public async Task CancelamentoNaoModificaArquivo()
    {
        var raiz = CriarRaiz();
        try
        {
            var caminho = Path.Combine(raiz, "teste.txt");
            await File.WriteAllTextAsync(caminho, "conteudo");
            var antes = await File.ReadAllTextAsync(caminho);

            await Assert.ThrowsExactlyAsync<OperationCanceledException>(
                () => new InventariarDiretorio(new FonteInventarioSistemaArquivos())
                    .ExecutarAsync(raiz, cancellationToken: new CancellationToken(true)));

            Assert.AreEqual(antes, await File.ReadAllTextAsync(caminho));
        }
        finally { ApagarRaiz(raiz); }
    }

    [TestMethod]
    public async Task RestringeAmostraMasMantemContagemCompleta()
    {
        var raiz = CriarRaiz();
        try
        {
            for (var i = 0; i < 5; i++)
                await File.WriteAllTextAsync(Path.Combine(raiz, $"arquivo{i}.txt"), "A");

            var resultado = await new InventariarDiretorio(
                new FonteInventarioSistemaArquivos(), limiteAmostra: 2).ExecutarAsync(raiz);

            Assert.AreEqual(5L, resultado.QuantidadeArquivos);
            Assert.AreEqual(5L, resultado.TotalBytes);
            Assert.HasCount(2, resultado.AmostraArquivos);
            Assert.IsTrue(resultado.AmostraLimitada);
        }
        finally { ApagarRaiz(raiz); }
    }

    private static string CriarRaiz()
    {
        var raiz = Path.Combine(Path.GetTempPath(), "CLEAR_M2_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(raiz);
        return raiz;
    }

    private static void ApagarRaiz(string raiz)
    {
        if (Directory.Exists(raiz))
            Directory.Delete(raiz, recursive: true);
    }
}
