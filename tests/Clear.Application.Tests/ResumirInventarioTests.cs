using System.Runtime.CompilerServices;
using Clear.Application.Inventario;
using Clear.Domain.Arquivos;

namespace Clear.Application.Tests;

[TestClass]
public sealed class ResumirInventarioTests
{
    [TestMethod]
    public async Task SomaBytesEContaArquivosInclusosOsVazios()
    {
        var fonte = new FonteFalsa(
            new ArquivoInventariado(@"C:\Dados\a.txt", 100),
            new ArquivoInventariado(@"C:\Dados\b.txt", 200),
            new ArquivoInventariado(@"C:\Dados\vazio.txt", 0));

        var resumo = await new ResumirInventario(fonte).ExecutarAsync(@"C:\Dados");

        Assert.AreEqual(3L, resumo.QuantidadeArquivos);
        Assert.AreEqual(300L, resumo.TotalBytes);
        Assert.AreEqual(@"C:\Dados", fonte.DiretorioRecebido);
    }

    [TestMethod]
    public async Task InventarioVazioProduzResumoZero()
    {
        var resumo = await new ResumirInventario(new FonteFalsa()).ExecutarAsync(@"C:\Dados");
        Assert.AreEqual(0L, resumo.QuantidadeArquivos);
        Assert.AreEqual(0L, resumo.TotalBytes);
    }

    [TestMethod]
    public async Task RejeitaDiretorioRaizEmBranco()
    {
        var servico = new ResumirInventario(new FonteFalsa());
        await Assert.ThrowsExactlyAsync<ArgumentException>(() => servico.ExecutarAsync("  "));
    }

    [TestMethod]
    public void RejeitaFonteNula()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new ResumirInventario(null!));
    }

    [TestMethod]
    public async Task PermiteCancelamentoAntesDaLeitura()
    {
        var servico = new ResumirInventario(
            new FonteFalsa(new ArquivoInventariado(@"C:\Dados\a.txt", 10)));
        await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            () => servico.ExecutarAsync(@"C:\Dados", new CancellationToken(true)));
    }

    [TestMethod]
    public async Task ImpedeEstouroNoTotalDeBytes()
    {
        var servico = new ResumirInventario(
            new FonteFalsa(
                new ArquivoInventariado(@"C:\Dados\a.bin", long.MaxValue),
                new ArquivoInventariado(@"C:\Dados\b.bin", 1)));
        await Assert.ThrowsExactlyAsync<OverflowException>(
            () => servico.ExecutarAsync(@"C:\Dados"));
    }

    private sealed class FonteFalsa : IFonteInventario
    {
        private readonly IReadOnlyList<ArquivoInventariado> _arquivos;

        public FonteFalsa(params ArquivoInventariado[] arquivos)
        {
            _arquivos = arquivos;
        }

        public string? DiretorioRecebido { get; private set; }

        public async IAsyncEnumerable<ArquivoInventariado> EnumerarAsync(
            string diretorioRaiz,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            DiretorioRecebido = diretorioRaiz;
            foreach (var arquivo in _arquivos)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Yield();
                yield return arquivo;
            }
        }
    }
}
