using Clear.Application.Inventario;
using Clear.Domain.Analise;
using Clear.Infrastructure.Inventario;

namespace Clear.Infrastructure.Tests;

[TestClass]
public sealed class InventarioTemporalIntegracaoTests
{
    [TestMethod]
    public async Task ArquivoTemporarioAntigoRecebeDoisIndiciosSemAlterarConteudoOuTimestamp()
    {
        var raiz = Path.Combine(Path.GetTempPath(), "CLEAR_M3_TEMPO_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(raiz);
        try
        {
            var antigo = Path.Combine(raiz, "antigo.tmp");
            var recente = Path.Combine(raiz, "recente.txt");

            await File.WriteAllBytesAsync(antigo, new byte[] { 1, 2, 3 });
            await File.WriteAllBytesAsync(recente, new byte[] { 4, 5 });
            var referencia = DateTimeOffset.UtcNow;
            File.SetLastWriteTimeUtc(antigo, referencia.AddDays(-500).UtcDateTime);

            var conteudoAntes = await File.ReadAllBytesAsync(antigo);
            var modificacaoAntes = File.GetLastWriteTimeUtc(antigo);
            var classificador = ClassificadorArquivos.CriarComSinalTemporal(365, referencia);

            var resultado = await new InventariarDiretorio(
                new FonteInventarioSistemaArquivos(), classificador: classificador).ExecutarAsync(raiz);

            Assert.AreEqual(2L, resultado.QuantidadeArquivos);
            Assert.AreEqual(5L, resultado.TotalBytes);
            Assert.AreEqual(1L, resultado.ArquivosComIndicios);
            Assert.AreEqual(1L, resultado.ContagensCategoria.Single(
                x => x.Categoria == CategoriaAnalise.SinalTemporal).QuantidadeArquivos);
            Assert.AreEqual(1L, resultado.ContagensCategoria.Single(
                x => x.Categoria == CategoriaAnalise.CopiaBackupOuTemporario).QuantidadeArquivos);
            CollectionAssert.AreEqual(conteudoAntes, await File.ReadAllBytesAsync(antigo));
            Assert.AreEqual(modificacaoAntes, File.GetLastWriteTimeUtc(antigo));
        }
        finally
        {
            if (Directory.Exists(raiz))
                Directory.Delete(raiz, recursive: true);
        }
    }
}
