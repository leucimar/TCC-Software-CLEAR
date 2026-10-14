using Clear.Application.Inventario;
using Clear.Domain.Analise;
using Clear.Infrastructure.Inventario;

namespace Clear.Infrastructure.Tests;

[TestClass]
public sealed class InventarioComClassificacaoTests
{
    [TestMethod]
    public async Task InventarioRealClassificaMultiplosIndiciosSemAlterarConteudo()
    {
        var raiz = Path.Combine(Path.GetTempPath(), "CLEAR_M3_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(raiz);

        try
        {
            var vazio = Path.Combine(raiz, "vazio.bak");
            var documento = Path.Combine(raiz, "relatorio.docx");
            await File.WriteAllBytesAsync(vazio, Array.Empty<byte>());
            await File.WriteAllBytesAsync(documento, new byte[] { 1, 2, 3 });
            var antes = await File.ReadAllBytesAsync(documento);

            var resultado = await new InventariarDiretorio(
                new FonteInventarioSistemaArquivos()).ExecutarAsync(raiz);

            Assert.AreEqual(2L, resultado.QuantidadeArquivos);
            Assert.AreEqual(3L, resultado.TotalBytes);
            Assert.AreEqual(1L, resultado.ArquivosComIndicios);
            Assert.AreEqual(1L, resultado.ContagensCategoria.Single(
                x => x.Categoria == CategoriaAnalise.ArquivoVazio).QuantidadeArquivos);
            Assert.AreEqual(1L, resultado.ContagensCategoria.Single(
                x => x.Categoria == CategoriaAnalise.CopiaBackupOuTemporario).QuantidadeArquivos);
            CollectionAssert.AreEqual(antes, await File.ReadAllBytesAsync(documento));
            Assert.IsTrue(File.Exists(vazio));
        }
        finally
        {
            Directory.Delete(raiz, recursive: true);
        }
    }
}
