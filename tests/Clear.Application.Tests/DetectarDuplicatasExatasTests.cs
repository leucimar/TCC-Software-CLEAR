using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using Clear.Application.Duplicatas;
using Clear.Application.Inventario;
using Clear.Domain.Arquivos;

namespace Clear.Application.Tests;

[TestClass]
public sealed class DetectarDuplicatasExatasTests
{
    private static ArquivoInventariado Arquivo(string nome, int tamanho) =>
        new(@"C:\Teste\" + nome, tamanho);

    [TestMethod]
    public async Task MesmoConteudoComNomesDiferentesGeraGrupoConfirmado()
    {
        var a = Arquivo("a.txt", 3);
        var b = Arquivo("b.dat", 3);
        var c = Arquivo("outro.txt", 3);
        var unico = Arquivo("unico.txt", 5);
        var leitor = new LeitorFalso(new Dictionary<string, byte[]>
        {
            [a.CaminhoCompleto] = new byte[] { 1, 2, 3 },
            [b.CaminhoCompleto] = new byte[] { 1, 2, 3 },
            [c.CaminhoCompleto] = new byte[] { 1, 2, 4 },
            [unico.CaminhoCompleto] = new byte[] { 1, 2, 3, 4, 5 }
        });

        var resultado = await new DetectarDuplicatasExatas(
            new FonteFalsa(a, b, c, unico), leitor).ExecutarAsync(@"C:\Teste");

        Assert.AreEqual(4L, resultado.ArquivosInventariados);
        Assert.AreEqual(3L, resultado.CandidatosPorTamanho);
        Assert.AreEqual(3L, resultado.ArquivosComHash);
        Assert.AreEqual(3, leitor.ChamadasHash);
        Assert.HasCount(1, resultado.Grupos);
        Assert.AreEqual(2L, resultado.ArquivosEmGrupos);
        Assert.AreEqual(3L, resultado.BytesPotencialmenteRedundantes);
        Assert.IsFalse(resultado.Incompleto);
    }

    [TestMethod]
    public async Task TodosOsTamanhosUnicosNaoLeemConteudo()
    {
        var leitor = new LeitorFalso(new Dictionary<string, byte[]>());
        var resultado = await new DetectarDuplicatasExatas(
            new FonteFalsa(Arquivo("a", 1), Arquivo("b", 2), Arquivo("c", 3)), leitor)
            .ExecutarAsync(@"C:\Teste");
        Assert.HasCount(0, resultado.Grupos);
        Assert.AreEqual(0, leitor.ChamadasHash);
        Assert.AreEqual(0L, resultado.CandidatosPorTamanho);
    }

    [TestMethod]
    public async Task HashArtificialmenteIgualNaoCriaFalsoDuplicado()
    {
        var a = Arquivo("a", 2);
        var b = Arquivo("b", 2);
        var leitor = new LeitorFalso(new Dictionary<string, byte[]>
        {
            [a.CaminhoCompleto] = new byte[] { 1, 2 },
            [b.CaminhoCompleto] = new byte[] { 3, 4 }
        }) { ForcarHashIgual = true };

        var resultado = await new DetectarDuplicatasExatas(
            new FonteFalsa(a, b), leitor).ExecutarAsync(@"C:\Teste");

        Assert.HasCount(0, resultado.Grupos);
        Assert.IsGreaterThan(leitor.ChamadasComparacao, 0);
    }

    [TestMethod]
    public async Task ArquivosVaziosPodemSerDuplicados()
    {
        var a = Arquivo("vazio1", 0);
        var b = Arquivo("vazio2", 0);
        var leitor = new LeitorFalso(new Dictionary<string, byte[]>
        {
            [a.CaminhoCompleto] = Array.Empty<byte>(),
            [b.CaminhoCompleto] = Array.Empty<byte>()
        });
        var resultado = await new DetectarDuplicatasExatas(
            new FonteFalsa(a, b), leitor).ExecutarAsync(@"C:\Teste");
        Assert.HasCount(1, resultado.Grupos);
        Assert.AreEqual(0L, resultado.BytesPotencialmenteRedundantes);
    }

    [TestMethod]
    public async Task HashFalhouRegistraOcorrenciaENaoFormaGrupoSemConfirmacao()
    {
        var a = Arquivo("a", 1);
        var b = Arquivo("b", 1);
        var leitor = new LeitorFalso(new Dictionary<string, byte[]>
        {
            [a.CaminhoCompleto] = new byte[] { 1 },
            [b.CaminhoCompleto] = new byte[] { 1 }
        });
        leitor.FalharHashPara = b.CaminhoCompleto;

        var resultado = await new DetectarDuplicatasExatas(
            new FonteFalsa(a, b), leitor).ExecutarAsync(@"C:\Teste");

        Assert.AreEqual(1L, resultado.TotalOcorrencias);
        Assert.IsTrue(resultado.Incompleto);
        Assert.HasCount(0, resultado.Grupos);
        Assert.AreEqual("HashNaoCalculado", resultado.Ocorrencias[0].Tipo);
    }

    [TestMethod]
    public async Task ComparacaoFalhouRegistraOcorrenciaENaoAfirmaIdentidade()
    {
        var a = Arquivo("a", 1);
        var b = Arquivo("b", 1);
        var leitor = new LeitorFalso(new Dictionary<string, byte[]>
        {
            [a.CaminhoCompleto] = new byte[] { 1 },
            [b.CaminhoCompleto] = new byte[] { 1 }
        }) { FalharComparacao = true };

        var resultado = await new DetectarDuplicatasExatas(
            new FonteFalsa(a, b), leitor).ExecutarAsync(@"C:\Teste");

        Assert.AreEqual(1L, resultado.TotalOcorrencias);
        Assert.HasCount(0, resultado.Grupos);
    }

    [TestMethod]
    public async Task CancelamentoPropagaSemPublicarResultadoParcial()
    {
        var a = Arquivo("a", 1);
        var b = Arquivo("b", 1);
        var leitor = new LeitorFalso(new Dictionary<string, byte[]>
        {
            [a.CaminhoCompleto] = new byte[] { 1 },
            [b.CaminhoCompleto] = new byte[] { 1 }
        }) { CancelarNoHash = true };

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(async () =>
            await new DetectarDuplicatasExatas(new FonteFalsa(a, b), leitor)
                .ExecutarAsync(@"C:\Teste"));
    }

    [TestMethod]
    public async Task ResultadoDeUmaExecucaoNaoModificaOutra()
    {
        var a = Arquivo("a", 1);
        var b = Arquivo("b", 1);
        var leitor = new LeitorFalso(new Dictionary<string, byte[]>
        {
            [a.CaminhoCompleto] = new byte[] { 1 },
            [b.CaminhoCompleto] = new byte[] { 1 }
        });
        var detector = new DetectarDuplicatasExatas(new FonteFalsa(a, b), leitor);
        var um = await detector.ExecutarAsync(@"C:\Teste");
        var dois = await detector.ExecutarAsync(@"C:\Teste");
        Assert.HasCount(1, um.Grupos);
        Assert.HasCount(1, dois.Grupos);
    }

    private sealed class FonteFalsa(params ArquivoInventariado[] arquivos) : IFonteInventario
    {
        public async IAsyncEnumerable<ArquivoInventariado> EnumerarAsync(
            string diretorioRaiz, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            foreach (var item in arquivos)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Yield();
                yield return item;
            }
        }
    }

    private sealed class LeitorFalso(Dictionary<string, byte[]> conteudos) : ILeitorConteudoDuplicatas
    {
        public int ChamadasHash { get; private set; }
        public int ChamadasComparacao { get; private set; }
        public bool ForcarHashIgual { get; init; }
        public bool FalharComparacao { get; init; }
        public bool CancelarNoHash { get; init; }
        public string? FalharHashPara { get; set; }

        public Task<string> CalcularSha256Async(ArquivoInventariado arquivo,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (CancelarNoHash)
                throw new OperationCanceledException(cancellationToken);
            ChamadasHash++;
            if (arquivo.CaminhoCompleto == FalharHashPara)
                throw new IOException("Arquivo sem permissão.");
            return Task.FromResult(ForcarHashIgual ? new string('A', 64) :
                Convert.ToHexString(SHA256.HashData(conteudos[arquivo.CaminhoCompleto])));
        }

        public Task<bool> CompararBytesAsync(ArquivoInventariado primeiro,
            ArquivoInventariado segundo, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ChamadasComparacao++;
            if (FalharComparacao)
                throw new IOException("Arquivo foi modificado durante a leitura.");
            return Task.FromResult(conteudos[primeiro.CaminhoCompleto]
                .AsSpan().SequenceEqual(conteudos[segundo.CaminhoCompleto]));
        }
    }
}
