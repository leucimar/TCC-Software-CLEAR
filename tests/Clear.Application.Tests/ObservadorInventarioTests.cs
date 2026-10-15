using System.Runtime.CompilerServices;
using Clear.Application.Inventario;
using Clear.Domain.Analise;
using Clear.Domain.Arquivos;
using Clear.Domain.Compatibilidade;
using Clear.Domain.Inventario;

namespace Clear.Application.Tests;

[TestClass]
public sealed class ObservadorInventarioTests
{
    [TestMethod]
    public async Task ObservadorRecebeTodasAsEntradasSemRepetirVarredura()
    {
        var fonte = new FonteMista(Enumerable.Range(0, 1105)
            .Select(i => EntradaInventariada.DeArquivo(
                new ArquivoInventariado(@"C:\Sintetico\" + i + ".txt", i)))
            .Append(EntradaInventariada.DeDiretorio(@"C:\Sintetico\Pasta"))
            .ToArray());
        var observador = new ObservadorFalso();
        var resultado = await new InventariarDiretorio(fonte,
            limiteAmostra: 5, observador: observador).ExecutarAsync(@"C:\Sintetico");

        Assert.AreEqual(1, fonte.Chamadas);
        Assert.AreEqual(1106, observador.Chamadas);
        Assert.AreEqual(1105L, resultado.QuantidadeArquivos);
        Assert.AreEqual(1L, resultado.QuantidadeDiretorios);
        Assert.HasCount(5, resultado.AmostraAvaliacoes);
        Assert.AreEqual(1, observador.PastasRecebidas);
    }

    [TestMethod]
    public async Task CancelamentoDoObservadorCancelaInventario()
    {
        var fonte = new FonteMista(EntradaInventariada.DeArquivo(
            new ArquivoInventariado(@"C:\Sintetico\arquivo.txt", 3)));
        var observador = new ObservadorFalso { Cancelar = true };

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() =>
            new InventariarDiretorio(fonte, observador: observador)
                .ExecutarAsync(@"C:\Sintetico"));
        Assert.AreEqual(1, observador.Chamadas);
    }

    private sealed class ObservadorFalso : IObservadorInventario
    {
        public int Chamadas { get; private set; }
        public int PastasRecebidas { get; private set; }
        public bool Cancelar { get; init; }

        public ValueTask RegistrarAsync(EntradaInventariada entrada,
            ResultadoAnaliseArquivo? classificacaoGeral,
            AvaliacaoEntrada avaliacaoCompatibilidade,
            CancellationToken cancellationToken = default)
        {
            Chamadas++;
            if (entrada.Tipo == TipoEntradaInventario.Diretorio)
            {
                PastasRecebidas++;
                Assert.IsNull(classificacaoGeral);
            }
            else
                Assert.IsNotNull(classificacaoGeral);
            if (Cancelar)
                throw new OperationCanceledException();
            return ValueTask.CompletedTask;
        }
    }

    private sealed class FonteMista(params EntradaInventariada[] entradas) : IFonteInventarioComEntradas
    {
        public int Chamadas { get; private set; }

        public async IAsyncEnumerable<EntradaInventariada> EnumerarEntradasAsync(
            string diretorioRaiz,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            Chamadas++;
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
            await Task.Yield();
            yield break;
        }
    }
}
