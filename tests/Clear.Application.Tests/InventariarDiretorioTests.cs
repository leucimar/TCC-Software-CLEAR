using System.Runtime.CompilerServices;
using Clear.Application.Inventario;
using Clear.Domain.Arquivos;

namespace Clear.Application.Tests;

[TestClass]
public sealed class InventariarDiretorioTests
{
    [TestMethod]
    public async Task TotalizaTodosOsArquivosMesmoComAmostraLimitada()
    {
        var fonte = new FonteSimulada(
            new ArquivoInventariado(@"C:\Teste\a.txt", 10),
            new ArquivoInventariado(@"C:\Teste\b.txt", 20),
            new ArquivoInventariado(@"C:\Teste\c.txt", 0));

        var resultado = await new InventariarDiretorio(fonte, 2).ExecutarAsync(@"C:\Teste");

        Assert.AreEqual(3L, resultado.QuantidadeArquivos);
        Assert.AreEqual(30L, resultado.TotalBytes);
        Assert.HasCount(2, resultado.AmostraArquivos);
        Assert.IsTrue(resultado.AmostraLimitada);
        Assert.IsFalse(resultado.Incompleto);
    }

    [TestMethod]
    public async Task AdmiteInventarioSemArquivos()
    {
        var resultado = await new InventariarDiretorio(new FonteSimulada())
            .ExecutarAsync(@"C:\Teste");
        Assert.AreEqual(0L, resultado.QuantidadeArquivos);
        Assert.AreEqual(0L, resultado.TotalBytes);
        Assert.IsFalse(resultado.AmostraLimitada);
    }

    [TestMethod]
    public async Task RegistraOcorrenciasQueIndicamInventarioParcial()
    {
        var fonte = new FonteComOcorrencias();
        var resultado = await new InventariarDiretorio(fonte)
            .ExecutarAsync(@"C:\Teste");

        Assert.IsTrue(resultado.Incompleto);
        Assert.AreEqual(2L, resultado.TotalOcorrencias);
        Assert.HasCount(1, resultado.Ocorrencias);
    }

    [TestMethod]
    public void RejeitaLimiteForaDaFaixa()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => new InventariarDiretorio(new FonteSimulada(), -1));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => new InventariarDiretorio(new FonteSimulada(), 10001));
    }

    [TestMethod]
    public async Task RespeitaCancelamento()
    {
        var servico = new InventariarDiretorio(new FonteSimulada(
            new ArquivoInventariado(@"C:\Teste\a.txt", 2)));

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            () => servico.ExecutarAsync(@"C:\Teste", cancellationToken: new CancellationToken(true)));
    }

    [TestMethod]
    public async Task DetectaEstouroDeBytes()
    {
        var servico = new InventariarDiretorio(new FonteSimulada(
            new ArquivoInventariado(@"C:\Teste\a.bin", long.MaxValue),
            new ArquivoInventariado(@"C:\Teste\b.bin", 1)));

        await Assert.ThrowsExactlyAsync<OverflowException>(
            () => servico.ExecutarAsync(@"C:\Teste"));
    }

    private sealed class FonteSimulada : IFonteInventario
    {
        private readonly IReadOnlyList<ArquivoInventariado> _arquivos;
        public FonteSimulada(params ArquivoInventariado[] arquivos) => _arquivos = arquivos;

        public async IAsyncEnumerable<ArquivoInventariado> EnumerarAsync(
            string diretorioRaiz,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            foreach (var arquivo in _arquivos)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Yield();
                yield return arquivo;
            }
        }
    }

    private sealed class FonteComOcorrencias : IFonteInventarioComOcorrencias
    {
        public IReadOnlyList<OcorrenciaInventario> Ocorrencias =>
            new[] { new OcorrenciaInventario(@"C:\Teste\inacessivel", "Erro", "Sem permissão") };

        public long TotalOcorrencias => 2;

        public async IAsyncEnumerable<ArquivoInventariado> EnumerarAsync(
            string diretorioRaiz,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.Yield();
            yield break;
        }
    }
}
