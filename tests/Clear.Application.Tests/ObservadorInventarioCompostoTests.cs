using Clear.Application.Inventario;
using Clear.Domain.Analise;
using Clear.Domain.Arquivos;
using Clear.Domain.Compatibilidade;
using Clear.Domain.Inventario;

namespace Clear.Application.Tests;

[TestClass]
public sealed class ObservadorInventarioCompostoTests
{
    [TestMethod]
    public async Task EncaminhaCadaEntradaParaAmbosSemDuplicacao()
    {
        var a = new Contador();
        var b = new Contador();
        var arquivo = new ArquivoInventariado(@"C:\Teste\arquivo.txt", 4);
        var entrada = EntradaInventariada.DeArquivo(arquivo);
        await new ObservadorInventarioComposto(a, b).RegistrarAsync(
            entrada, null, new AvaliacaoEntrada(entrada, [], false));
        Assert.AreEqual(1, a.Chamadas);
        Assert.AreEqual(1, b.Chamadas);
    }

    [TestMethod]
    public async Task ExcecaoDoPrimeiroImpedeNotificacaoDosSeguintes()
    {
        var a = new Contador { Falhar = true };
        var b = new Contador();
        var arquivo = new ArquivoInventariado(@"C:\Teste\arquivo.txt", 4);
        var entrada = EntradaInventariada.DeArquivo(arquivo);
        await Assert.ThrowsExactlyAsync<IOException>(async () =>
            await new ObservadorInventarioComposto(a, b).RegistrarAsync(
                entrada, null, new AvaliacaoEntrada(entrada, [], false)));
        Assert.AreEqual(1, a.Chamadas);
        Assert.AreEqual(0, b.Chamadas);
    }

    private sealed class Contador : IObservadorInventario
    {
        public int Chamadas { get; private set; }
        public bool Falhar { get; init; }

        public ValueTask RegistrarAsync(EntradaInventariada entrada,
            ResultadoAnaliseArquivo? classificacaoGeral,
            AvaliacaoEntrada avaliacaoCompatibilidade,
            CancellationToken cancellationToken = default)
        {
            Chamadas++;
            if (Falhar)
                throw new IOException("Falha controlada.");
            return ValueTask.CompletedTask;
        }
    }
}
