using System.Runtime.CompilerServices;
using Clear.Application.Inventario;
using Clear.Domain.Arquivos;
using Clear.Domain.Inventario;

namespace Clear.Application.Tests;

[TestClass]
public sealed class InventarioDiretoriosTests
{
    [TestMethod]
    public async Task FonteMistaContaSubpastasEArquivosSeparadamenteSemDuasVarreduras()
    {
        var fonte = new FonteMista(
            EntradaInventariada.DeDiretorio(@"C:\Teste\Backup.bak"),
            EntradaInventariada.DeArquivo(new ArquivoInventariado(@"C:\Teste\arquivo.tmp", 0)),
            EntradaInventariada.DeDiretorio(@"C:\Teste\Vazia"),
            EntradaInventariada.DeArquivo(new ArquivoInventariado(@"C:\Teste\outro.txt", 12)));

        var resultado = await new InventariarDiretorio(fonte).ExecutarAsync(@"C:\Teste");

        Assert.AreEqual(2L, resultado.QuantidadeArquivos);
        Assert.AreEqual(2L, resultado.QuantidadeDiretorios);
        Assert.AreEqual(12L, resultado.TotalBytes);
        Assert.AreEqual(1L, resultado.ArquivosComIndicios);
        Assert.AreEqual(1, fonte.ChamadasEntradas);
        Assert.AreEqual(0, fonte.ChamadasArquivos);
        Assert.HasCount(2, resultado.AmostraDiretorios);
    }

    [TestMethod]
    public async Task AmostraDeSubpastasEhLimitadaSemPerderTotal()
    {
        var fonte = new FonteMista(
            EntradaInventariada.DeDiretorio(@"C:\Teste\A"),
            EntradaInventariada.DeDiretorio(@"C:\Teste\B"),
            EntradaInventariada.DeDiretorio(@"C:\Teste\C"));
        var resultado = await new InventariarDiretorio(fonte, limiteAmostra: 1)
            .ExecutarAsync(@"C:\Teste");

        Assert.AreEqual(3L, resultado.QuantidadeDiretorios);
        Assert.HasCount(1, resultado.AmostraDiretorios);
        Assert.IsTrue(resultado.AmostraDiretoriosLimitada);
        Assert.AreEqual(0L, resultado.QuantidadeArquivos);
    }

    [TestMethod]
    public async Task FonteAntigaPermaneceSuportada()
    {
        var fonte = new FonteAntiga();
        var resultado = await new InventariarDiretorio(fonte).ExecutarAsync(@"C:\Teste");
        Assert.AreEqual(1L, resultado.QuantidadeArquivos);
        Assert.AreEqual(0L, resultado.QuantidadeDiretorios);
        Assert.HasCount(0, resultado.AmostraDiretorios);
    }

    [TestMethod]
    public async Task CancelamentoAntesDaEnumeracaoMistaEhRespeitado()
    {
        var fonte = new FonteMista(EntradaInventariada.DeDiretorio(@"C:\Teste\A"));
        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() =>
            new InventariarDiretorio(fonte).ExecutarAsync(@"C:\Teste",
                cancellationToken: new CancellationToken(true)));
        Assert.AreEqual(0, fonte.ChamadasEntradas);
    }

    private sealed class FonteMista(params EntradaInventariada[] entradas) : IFonteInventarioComEntradas
    {
        public int ChamadasArquivos { get; private set; }
        public int ChamadasEntradas { get; private set; }

        public async IAsyncEnumerable<EntradaInventariada> EnumerarEntradasAsync(
            string diretorioRaiz,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            ChamadasEntradas++;
            foreach (var entrada in entradas)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Yield();
                yield return entrada;
            }
        }

        public async IAsyncEnumerable<ArquivoInventariado> EnumerarAsync(
            string diretorioRaiz,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            ChamadasArquivos++;
            await Task.Yield();
            yield break;
        }
    }

    private sealed class FonteAntiga : IFonteInventario
    {
        public async IAsyncEnumerable<ArquivoInventariado> EnumerarAsync(
            string diretorioRaiz,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.Yield();
            yield return new ArquivoInventariado(@"C:\Teste\normal.txt", 10);
        }
    }
}
