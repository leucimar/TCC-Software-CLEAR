using Clear.Application.Inventario;
using Clear.Domain.Inventario;
using Clear.Infrastructure.Inventario;

namespace Clear.Infrastructure.Tests;

[TestClass]
public sealed class FonteInventarioDiretoriosTests
{
    [TestMethod]
    public async Task ContaPastasInclusiveVaziasSemContarRaiz()
    {
        var raiz = CriarRaiz();
        try
        {
            Directory.CreateDirectory(Path.Combine(raiz, "A", "B"));
            Directory.CreateDirectory(Path.Combine(raiz, "Vazia"));
            await File.WriteAllBytesAsync(Path.Combine(raiz, "A", "B", "a.txt"), new byte[] { 1, 2, 3 });

            var resultado = await new InventariarDiretorio(
                new FonteInventarioSistemaArquivos()).ExecutarAsync(raiz);

            Assert.AreEqual(3L, resultado.QuantidadeDiretorios);
            Assert.AreEqual(1L, resultado.QuantidadeArquivos);
            Assert.AreEqual(3L, resultado.TotalBytes);
            Assert.AreEqual(0L, resultado.TotalOcorrencias);
            Assert.HasCount(3, resultado.AmostraDiretorios);
            Assert.IsFalse(resultado.AmostraDiretorios.Any(x => x.CaminhoCompleto == raiz));
        }
        finally { Apagar(raiz); }
    }

    [TestMethod]
    public async Task PastaRaizVaziaRetornaZeroArquivosEZeroSubpastas()
    {
        var raiz = CriarRaiz();
        try
        {
            var resultado = await new InventariarDiretorio(
                new FonteInventarioSistemaArquivos()).ExecutarAsync(raiz);

            Assert.AreEqual(0L, resultado.QuantidadeDiretorios);
            Assert.AreEqual(0L, resultado.QuantidadeArquivos);
            Assert.AreEqual(0L, resultado.TotalBytes);
        }
        finally { Apagar(raiz); }
    }

    [TestMethod]
    public async Task InterfaceLegadaContinuaRetornandoSomenteArquivos()
    {
        var raiz = CriarRaiz();
        try
        {
            Directory.CreateDirectory(Path.Combine(raiz, "Sub"));
            await File.WriteAllBytesAsync(Path.Combine(raiz, "Sub", "arquivo.txt"), new byte[] { 1 });
            var fonte = new FonteInventarioSistemaArquivos();
            var arquivos = new List<string>();

            await foreach (var arquivo in fonte.EnumerarAsync(raiz))
                arquivos.Add(arquivo.CaminhoCompleto);

            Assert.HasCount(1, arquivos);
            Assert.IsTrue(arquivos[0].EndsWith("arquivo.txt", StringComparison.Ordinal));
        }
        finally { Apagar(raiz); }
    }

    [TestMethod]
    public async Task AmostraDasSubpastasNaoAfetaTotais()
    {
        var raiz = CriarRaiz();
        try
        {
            for (var i = 0; i < 5; i++)
                Directory.CreateDirectory(Path.Combine(raiz, "Sub" + i));

            var resultado = await new InventariarDiretorio(
                new FonteInventarioSistemaArquivos(), limiteAmostra: 2).ExecutarAsync(raiz);

            Assert.AreEqual(5L, resultado.QuantidadeDiretorios);
            Assert.HasCount(2, resultado.AmostraDiretorios);
            Assert.IsTrue(resultado.AmostraDiretoriosLimitada);
        }
        finally { Apagar(raiz); }
    }

    [TestMethod]
    public async Task EnumeracaoDeEntradasInformaTipoDeCadaItem()
    {
        var raiz = CriarRaiz();
        try
        {
            Directory.CreateDirectory(Path.Combine(raiz, "Pasta"));
            await File.WriteAllBytesAsync(Path.Combine(raiz, "arquivo.bin"), new byte[] { 1 });
            var fonte = new FonteInventarioSistemaArquivos();
            var entradas = new List<EntradaInventariada>();
            await foreach (var entrada in fonte.EnumerarEntradasAsync(raiz))
                entradas.Add(entrada);

            Assert.HasCount(2, entradas);
            Assert.AreEqual(1, entradas.Count(e => e.Tipo == TipoEntradaInventario.Arquivo));
            Assert.AreEqual(1, entradas.Count(e => e.Tipo == TipoEntradaInventario.Diretorio));
        }
        finally { Apagar(raiz); }
    }

    private static string CriarRaiz()
    {
        var pasta = Path.Combine(Path.GetTempPath(), "CLEAR_M3_3A_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(pasta);
        return pasta;
    }

    private static void Apagar(string raiz)
    {
        if (Directory.Exists(raiz))
            Directory.Delete(raiz, recursive: true);
    }
}
