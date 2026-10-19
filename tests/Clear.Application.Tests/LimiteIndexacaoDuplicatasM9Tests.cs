using System.Runtime.CompilerServices;
using Clear.Application.Duplicatas;
using Clear.Application.Inventario;
using Clear.Domain.Arquivos;

namespace Clear.Application.Tests;

[TestClass]
public sealed class LimiteIndexacaoDuplicatasM9Tests
{
    [TestMethod]
    public async Task AoExcederLimiteInterrompeAntesDeLerConteudo()
    {
        var fonte = new Fonte(8);
        var leitor = new Leitor();
        var detectar = new DetectarDuplicatasExatas(fonte, leitor,
            maximoHashesSimultaneos: 2, maximoArquivosIndexados: 4);
        var erro = await Assert.ThrowsExactlyAsync<InvalidOperationException>(async () =>
            await detectar.ExecutarAsync("C:\\Teste"));
        StringAssert.Contains(erro.Message, "limite");
        Assert.AreEqual(0, leitor.Leituras);
    }

    [TestMethod]
    public async Task NoLimiteExatoContinuaValido()
    {
        var fonte = new Fonte(4);
        var leitor = new Leitor();
        var resultado = await new DetectarDuplicatasExatas(fonte, leitor,
            maximoArquivosIndexados: 4).ExecutarAsync("C:\\Teste");
        Assert.AreEqual(4L, resultado.ArquivosInventariados);
        Assert.AreEqual(4L, resultado.ArquivosComHash);
        Assert.HasCount(1, resultado.Grupos);
        Assert.IsFalse(resultado.Incompleto);
    }

    [TestMethod]
    public void LimitesInvalidosSaoRejeitados()
    {
        var fonte = new Fonte(0);
        var leitor = new Leitor();
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            new DetectarDuplicatasExatas(fonte, leitor, 1, 0));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            new DetectarDuplicatasExatas(fonte, leitor, 1, 1_000_001));
        Assert.AreEqual(250_000,
            new DetectarDuplicatasExatas(fonte, leitor).MaximoArquivosIndexados);
    }

    private sealed class Fonte(int quantidade) : IFonteInventario
    {
        public async IAsyncEnumerable<ArquivoInventariado> EnumerarAsync(
            string diretorioRaiz,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            for (int i = 0; i < quantidade; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Yield();
                yield return new ArquivoInventariado(
                    "C:\\Teste\\arquivo_" + i + ".bin", 4);
            }
        }
    }

    private sealed class Leitor : ILeitorConteudoDuplicatas
    {
        public int Leituras { get; private set; }

        public Task<string> CalcularSha256Async(
            ArquivoInventariado arquivo, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Leituras++;
            return Task.FromResult(new string('A', 64));
        }

        public Task<bool> CompararBytesAsync(
            ArquivoInventariado primeiro, ArquivoInventariado segundo,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(true);
        }
    }
}
