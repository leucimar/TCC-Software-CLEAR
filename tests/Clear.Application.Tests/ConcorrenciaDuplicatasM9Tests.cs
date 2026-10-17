using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using Clear.Application.Duplicatas;
using Clear.Application.Inventario;
using Clear.Domain.Arquivos;

namespace Clear.Application.Tests;

[TestClass]
public sealed class ConcorrenciaDuplicatasM9Tests
{
    [TestMethod]
    public async Task LimiteConfiguradoNuncaEhUltrapassado()
    {
        var arquivos = CriarArquivos(32);
        var leitor = new LeitorInstrumentado(20);
        var resultado = await new DetectarDuplicatasExatas(
            new Fonte(arquivos), leitor, maximoHashesSimultaneos: 3)
            .ExecutarAsync("C:\\Teste");
        Assert.IsGreaterThanOrEqualTo(2, leitor.MaximoObservado);
        Assert.IsLessThanOrEqualTo(3, leitor.MaximoObservado);
        Assert.AreEqual(32L, resultado.ArquivosComHash);
        Assert.HasCount(1, resultado.Grupos);
        Assert.AreEqual(32L, resultado.ArquivosEmGrupos);
    }

    [TestMethod]
    public async Task MesmoResultadoDeDuplicatasParaConcorrenciaUmEOito()
    {
        var arquivos = CriarArquivos(25);
        var sequencial = await new DetectarDuplicatasExatas(new Fonte(arquivos),
            new LeitorInstrumentado(1), 1).ExecutarAsync("C:\\Teste");
        var concorrente = await new DetectarDuplicatasExatas(new Fonte(arquivos),
            new LeitorInstrumentado(1), 8).ExecutarAsync("C:\\Teste");
        Assert.AreEqual(sequencial.ArquivosInventariados, concorrente.ArquivosInventariados);
        Assert.AreEqual(sequencial.CandidatosPorTamanho, concorrente.CandidatosPorTamanho);
        Assert.AreEqual(sequencial.ArquivosComHash, concorrente.ArquivosComHash);
        Assert.AreEqual(sequencial.TotalOcorrencias, concorrente.TotalOcorrencias);
        Assert.AreEqual(sequencial.ArquivosEmGrupos, concorrente.ArquivosEmGrupos);
        Assert.AreEqual(sequencial.BytesPotencialmenteRedundantes,
            concorrente.BytesPotencialmenteRedundantes);
        CollectionAssert.AreEqual(
            sequencial.Grupos.SelectMany(g => g.Arquivos.Select(a => a.CaminhoCompleto)).ToArray(),
            concorrente.Grupos.SelectMany(g => g.Arquivos.Select(a => a.CaminhoCompleto)).ToArray());
    }

    [TestMethod]
    public async Task FalhaParcialEmLoteConcorrenteNaoFormaDuplicatasIncorretas()
    {
        var arquivos = CriarArquivos(9);
        var falha = arquivos[3].CaminhoCompleto;
        var leitor = new LeitorInstrumentado(3) { FalharPara = falha };
        var resultado = await new DetectarDuplicatasExatas(
            new Fonte(arquivos), leitor, 4).ExecutarAsync("C:\\Teste");
        Assert.AreEqual(1L, resultado.TotalOcorrencias);
        Assert.AreEqual(8L, resultado.ArquivosComHash);
        Assert.IsTrue(resultado.Incompleto);
        Assert.AreEqual("HashNaoCalculado", resultado.Ocorrencias.Single().Tipo);
        Assert.IsFalse(resultado.Grupos.SelectMany(g => g.Arquivos)
            .Any(a => a.CaminhoCompleto == falha));
    }

    [TestMethod]
    public async Task CancelamentoConcorrentePropagaSemRetornarResultadoParcial()
    {
        var arquivos = CriarArquivos(40);
        using var cancelar = new CancellationTokenSource();
        cancelar.CancelAfter(TimeSpan.FromMilliseconds(50));
        var leitor = new LeitorInstrumentado(100);
        await Assert.ThrowsExactlyAsync<OperationCanceledException>(async () =>
            await new DetectarDuplicatasExatas(new Fonte(arquivos), leitor, 4)
                .ExecutarAsync("C:\\Teste", cancellationToken: cancelar.Token));
    }

    [TestMethod]
    public void LimitesForaDaFaixaSaoRejeitadosEPadraoEhSequencial()
    {
        var fonte = new Fonte([]);
        var leitor = new LeitorInstrumentado(1);
        Assert.AreEqual(1, new DetectarDuplicatasExatas(fonte, leitor).MaximoHashesSimultaneos);
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => new DetectarDuplicatasExatas(fonte, leitor, 0));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => new DetectarDuplicatasExatas(fonte, leitor, 9));
    }

    private static ArquivoInventariado[] CriarArquivos(int n) =>
        Enumerable.Range(0, n).Select(i =>
            new ArquivoInventariado(@"C:\Teste\arquivo_" + i.ToString("D5") + ".dat", 8))
            .ToArray();

    private sealed class Fonte(ArquivoInventariado[] arquivos) : IFonteInventario
    {
        public async IAsyncEnumerable<ArquivoInventariado> EnumerarAsync(
            string diretorioRaiz,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            foreach (var arquivo in arquivos)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Yield();
                yield return arquivo;
            }
        }
    }

    private sealed class LeitorInstrumentado(int atrasoMs) : ILeitorConteudoDuplicatas
    {
        private int _ativas;
        private int _maximo;
        public int MaximoObservado => Volatile.Read(ref _maximo);
        public string? FalharPara { get; init; }

        public async Task<string> CalcularSha256Async(
            ArquivoInventariado arquivo, CancellationToken cancellationToken = default)
        {
            int ativas = Interlocked.Increment(ref _ativas);
            try
            {
                int valor;
                do
                {
                    valor = Volatile.Read(ref _maximo);
                    if (valor >= ativas) break;
                } while (Interlocked.CompareExchange(ref _maximo, ativas, valor) != valor);
                await Task.Delay(atrasoMs, cancellationToken);
                if (arquivo.CaminhoCompleto == FalharPara)
                    throw new IOException("Falha sintética de leitura.");
                // Os dados fakes têm conteúdo igual e tamanho idêntico.
                return Convert.ToHexString(SHA256.HashData(new byte[8]));
            }
            finally
            {
                Interlocked.Decrement(ref _ativas);
            }
        }

        public Task<bool> CompararBytesAsync(ArquivoInventariado primeiro,
            ArquivoInventariado segundo, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(true);
        }
    }
}
