using System.Security.Cryptography;
using Clear.Application.Duplicatas;
using Clear.Domain.Arquivos;
using Clear.Infrastructure.Duplicatas;
using Clear.Infrastructure.Inventario;

namespace Clear.Infrastructure.Tests;

[TestClass]
public sealed class LeitorDuplicatasIntegracaoTests
{
    [TestMethod]
    public async Task DetectaTresGruposExatosSemModificarArquivos()
    {
        var raiz = CriarPasta();
        try
        {
            var copia = Directory.CreateDirectory(Path.Combine(raiz, "Copia")).FullName;
            Directory.CreateDirectory(Path.Combine(raiz, "Vazia"));
            var caminhos = new[]
            {
                Path.Combine(raiz, "relatorio.txt"),
                Path.Combine(copia, "relatorio-copia.dat"),
                Path.Combine(raiz, "diferente-mesmo-tamanho.bin"),
                Path.Combine(raiz, "vazio1.txt"),
                Path.Combine(copia, "vazio2.bak"),
                Path.Combine(raiz, "curto.txt"),
                Path.Combine(copia, "curto.tmp"),
                Path.Combine(raiz, "unico.txt")
            };
            var conteudos = new byte[][]
            {
                new byte[] { 65,66,67,49,50,51 },
                new byte[] { 65,66,67,49,50,51 },
                new byte[] { 88,89,90,55,56,57 },
                Array.Empty<byte>(), Array.Empty<byte>(),
                new byte[] { 1,2 }, new byte[] { 1,2 }, new byte[] { 9,9,9 }
            };
            for (var i = 0; i < caminhos.Length; i++)
                await File.WriteAllBytesAsync(caminhos[i], conteudos[i]);
            var datasAntes = caminhos.Select(File.GetLastWriteTimeUtc).ToArray();

            var resultado = await new DetectarDuplicatasExatas(
                new FonteInventarioSistemaArquivos(), new LeitorConteudoDuplicatasSistemaArquivos())
                .ExecutarAsync(raiz);

            Assert.AreEqual(8L, resultado.ArquivosInventariados);
            Assert.AreEqual(7L, resultado.CandidatosPorTamanho);
            Assert.AreEqual(7L, resultado.ArquivosComHash);
            Assert.HasCount(3, resultado.Grupos);
            Assert.AreEqual(6L, resultado.ArquivosEmGrupos);
            Assert.AreEqual(8L, resultado.BytesPotencialmenteRedundantes);
            Assert.AreEqual(0L, resultado.TotalOcorrencias);
            Assert.IsTrue(resultado.Grupos.Any(g => g.TamanhoBytes == 0));
            Assert.IsFalse(resultado.Grupos.SelectMany(g => g.Arquivos).Any(a =>
                a.CaminhoCompleto.EndsWith("diferente-mesmo-tamanho.bin", StringComparison.Ordinal)));
            for (var i = 0; i < caminhos.Length; i++)
            {
                CollectionAssert.AreEqual(conteudos[i], await File.ReadAllBytesAsync(caminhos[i]));
                Assert.AreEqual(datasAntes[i], File.GetLastWriteTimeUtc(caminhos[i]));
            }
        }
        finally { Apagar(raiz); }
    }

    [TestMethod]
    public async Task Sha256ConfereComImplementacaoPadraoERespeitaLeituraEmFluxo()
    {
        var raiz = CriarPasta();
        try
        {
            var caminho = Path.Combine(raiz, "grande.bin");
            var conteudo = Enumerable.Range(0, 256 * 1024 + 17)
                .Select(i => (byte)(i % 253)).ToArray();
            await File.WriteAllBytesAsync(caminho, conteudo);
            var arquivo = Inventariar(caminho);
            var leitor = new LeitorConteudoDuplicatasSistemaArquivos();

            var real = await leitor.CalcularSha256Async(arquivo);
            var esperado = Convert.ToHexString(SHA256.HashData(conteudo));

            Assert.AreEqual(esperado, real);
        }
        finally { Apagar(raiz); }
    }

    [TestMethod]
    public async Task DiferencaDeBytesMesmoComTamanhoIgualEhDetectada()
    {
        var raiz = CriarPasta();
        try
        {
            var caminhoA = Path.Combine(raiz, "a.txt");
            var caminhoB = Path.Combine(raiz, "b.txt");
            await File.WriteAllBytesAsync(caminhoA, new byte[] { 1, 2, 3 });
            await File.WriteAllBytesAsync(caminhoB, new byte[] { 1, 2, 4 });

            var leitor = new LeitorConteudoDuplicatasSistemaArquivos();
            Assert.IsFalse(await leitor.CompararBytesAsync(
                Inventariar(caminhoA), Inventariar(caminhoB)));
        }
        finally { Apagar(raiz); }
    }

    [TestMethod]
    public async Task ArquivoAlteradoAposInventarioEhRejeitado()
    {
        var raiz = CriarPasta();
        try
        {
            var caminho = Path.Combine(raiz, "a.txt");
            await File.WriteAllBytesAsync(caminho, new byte[] { 1, 2, 3 });
            var arquivo = Inventariar(caminho);
            await File.WriteAllBytesAsync(caminho, new byte[] { 4, 5, 6, 7 });

            var leitor = new LeitorConteudoDuplicatasSistemaArquivos();
            await Assert.ThrowsExactlyAsync<IOException>(async () =>
                await leitor.CalcularSha256Async(arquivo));
        }
        finally { Apagar(raiz); }
    }

    [TestMethod]
    public async Task CancelamentoJaSinalizadoNaoAbreConteudo()
    {
        var raiz = CriarPasta();
        try
        {
            var caminho = Path.Combine(raiz, "a.txt");
            await File.WriteAllBytesAsync(caminho, new byte[] { 1 });
            var arquivo = Inventariar(caminho);
            var leitor = new LeitorConteudoDuplicatasSistemaArquivos();
            await Assert.ThrowsExactlyAsync<OperationCanceledException>(async () =>
                await leitor.CalcularSha256Async(arquivo, new CancellationToken(true)));
        }
        finally { Apagar(raiz); }
    }

    private static ArquivoInventariado Inventariar(string caminho)
    {
        var info = new FileInfo(caminho);
        return new ArquivoInventariado(info.FullName, info.Length,
            new DateTimeOffset(info.LastWriteTimeUtc, TimeSpan.Zero));
    }

    private static string CriarPasta()
    {
        var raiz = Path.Combine(Path.GetTempPath(),
            "CLEAR_M4_TEST_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(raiz);
        return raiz;
    }

    private static void Apagar(string raiz)
    {
        if (Directory.Exists(raiz))
            Directory.Delete(raiz, recursive: true);
    }
}
