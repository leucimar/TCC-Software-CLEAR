using System.Text.Json;
using Clear.Infrastructure.Amostragem;

namespace Clear.Infrastructure.Tests;

[TestClass]
public sealed class CriadorSubBaseExperimentalTests
{
    [TestMethod]
    public async Task CopiaPreservaTodosOsOriginaisHierarquiaEConteudo()
    {
        using var cena = new Cenario();
        cena.GerarArquivos(30);
        var antes = cena.HashesOriginais();
        var plano = CriadorSubBaseExperimental.Preparar(
            cena.Origem, cena.Destino, 30M, 5721);
        Assert.HasCount(9, plano.Selecionados);
        var resultado = await CriadorSubBaseExperimental.ExecutarAsync(
            cena.Origem, cena.Destino, plano);

        Assert.IsTrue(Directory.Exists(resultado.DiretorioFinal));
        Assert.HasCount(9, plano.Selecionados);
        foreach (var selecionado in plano.Selecionados)
        {
            var origem = Path.Combine(cena.Origem, selecionado.CaminhoRelativo);
            var destino = Path.Combine(cena.Destino, "Acervo", selecionado.CaminhoRelativo);
            Assert.IsTrue(File.Exists(origem));
            Assert.IsTrue(File.Exists(destino));
            CollectionAssert.AreEqual(
                await File.ReadAllBytesAsync(origem),
                await File.ReadAllBytesAsync(destino));
            Assert.AreEqual(File.GetLastWriteTimeUtc(origem),
                File.GetLastWriteTimeUtc(destino));
        }
        CollectionAssert.AreEquivalent(antes, cena.HashesOriginais());
        using var documento = JsonDocument.Parse(
            await File.ReadAllTextAsync(resultado.ManifestoResumo));
        Assert.AreEqual("Concluida",
            documento.RootElement.GetProperty("estado").GetString());
        Assert.AreEqual(9,
            documento.RootElement.GetProperty("arquivosSelecionados").GetInt32());
        Assert.HasCount(9,
            await File.ReadAllLinesAsync(resultado.ManifestoArquivos));
        Assert.IsFalse(File.Exists(Path.Combine(cena.Destino, "LEIA-ME-INCOMPLETO.txt")));
    }

    [TestMethod]
    public void NaoCriaSubbaseDentroDaOrigemOuPastaAncestral()
    {
        using var cena = new Cenario();
        cena.GerarArquivos(5);
        Assert.ThrowsExactly<IOException>(() =>
            CriadorSubBaseExperimental.Preparar(
                cena.Origem, Path.Combine(cena.Origem, "NovaSubBase")));
        Assert.ThrowsExactly<IOException>(() =>
            CriadorSubBaseExperimental.Preparar(cena.Origem, cena.Raiz));
        Assert.IsFalse(Directory.Exists(Path.Combine(cena.Origem, "NovaSubBase")));
    }

    [TestMethod]
    public async Task DestinoJaExistenteNaoEhSobrescrito()
    {
        using var cena = new Cenario();
        cena.GerarArquivos(5);
        var plano = CriadorSubBaseExperimental.Preparar(cena.Origem, cena.Destino);
        Directory.CreateDirectory(cena.Destino);
        var protegido = Path.Combine(cena.Destino, "protegido.txt");
        await File.WriteAllTextAsync(protegido, "NÃO MEXER");
        await Assert.ThrowsExactlyAsync<IOException>(() =>
            CriadorSubBaseExperimental.ExecutarAsync(cena.Origem, cena.Destino, plano));
        Assert.AreEqual("NÃO MEXER", await File.ReadAllTextAsync(protegido));
    }

    [TestMethod]
    public async Task AlteracaoDoOriginalAposPreviaInterrompeSemCopiar()
    {
        using var cena = new Cenario();
        cena.GerarArquivos(10);
        var plano = CriadorSubBaseExperimental.Preparar(cena.Origem, cena.Destino);
        var caminho = Directory.EnumerateFiles(cena.Origem, "*",
            SearchOption.AllDirectories).First();
        await File.AppendAllTextAsync(caminho, "mudou");
        await Assert.ThrowsExactlyAsync<IOException>(() =>
            CriadorSubBaseExperimental.ExecutarAsync(cena.Origem, cena.Destino, plano));
        Assert.IsFalse(Directory.Exists(cena.Destino));
        Assert.HasCount(10, Directory.EnumerateFiles(
            cena.Origem, "*", SearchOption.AllDirectories).ToArray());
    }

    [TestMethod]
    public async Task CancelamentoDuranteACopiaNuncaPublicaPastaFinal()
    {
        using var cena = new Cenario();
        cena.GerarArquivos(90);
        var plano = CriadorSubBaseExperimental.Preparar(cena.Origem, cena.Destino, 100M);
        using var cts = new CancellationTokenSource();
        var relator = new ProgressoCancelador(cts);
        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() =>
            CriadorSubBaseExperimental.ExecutarAsync(
                cena.Origem, cena.Destino, plano, relator, cts.Token));
        Assert.IsFalse(Directory.Exists(cena.Destino));
        Assert.IsTrue(Directory.EnumerateDirectories(
            Path.GetDirectoryName(cena.Destino)!)
            .Any(x => x.Contains(".CLEAR_EM_CONSTRUCAO_", StringComparison.Ordinal)));
        Assert.HasCount(90, Directory.EnumerateFiles(
            cena.Origem, "*", SearchOption.AllDirectories).ToArray());
    }

    [TestMethod]
    public async Task CancelamentoAntesDaCopiaNaoCriaPastas()
    {
        using var cena = new Cenario();
        cena.GerarArquivos(5);
        var plano = CriadorSubBaseExperimental.Preparar(cena.Origem, cena.Destino);
        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() =>
            CriadorSubBaseExperimental.ExecutarAsync(
                cena.Origem, cena.Destino, plano,
                cancelamento: new CancellationToken(true)));
        Assert.IsFalse(Directory.Exists(cena.Destino));
    }

    [TestMethod]
    public void OrigemAusenteOuRelativaEhRecusada()
    {
        using var cena = new Cenario();
        Assert.ThrowsExactly<ArgumentException>(() =>
            CriadorSubBaseExperimental.Preparar("pasta_relativa", cena.Destino));
        Assert.ThrowsExactly<DirectoryNotFoundException>(() =>
            CriadorSubBaseExperimental.Preparar(
                Path.Combine(cena.Raiz, "inexistente"), cena.Destino));
    }

    private sealed class ProgressoCancelador(CancellationTokenSource cancelamento)
        : IProgress<ProgressoCopiaAmostra>
    {
        public void Report(ProgressoCopiaAmostra progresso)
        {
            if (progresso.ArquivosCopiados >= 64) cancelamento.Cancel();
        }
    }

    private sealed class Cenario : IDisposable
    {
        public string Raiz { get; } = Path.Combine(Path.GetTempPath(),
            "CLEAR_SubBase_" + Guid.NewGuid().ToString("N"));
        public string Origem => Path.Combine(Raiz, "02_original");
        public string Destino => Path.Combine(Raiz, "03_amostras", "CLEAR_30pct");

        public Cenario() => Directory.CreateDirectory(Origem);

        public void GerarArquivos(int quantidade)
        {
            for (var i = 0; i < quantidade; i++)
            {
                var pasta = Path.Combine(Origem, i % 2 == 0 ? "CSV" : "DocLayNet_core",
                    "Projeto", "2025");
                Directory.CreateDirectory(pasta);
                var arquivo = Path.Combine(pasta, $"a{i:D4}.{(i % 2 == 0 ? "csv" : "png")}");
                File.WriteAllBytes(arquivo, Enumerable.Repeat((byte)(i % 255),
                    i == 0 ? 0 : 256 + i).ToArray());
            }
        }

        public string[] HashesOriginais() =>
            Directory.EnumerateFiles(Origem, "*", SearchOption.AllDirectories)
                .OrderBy(x => x, StringComparer.Ordinal)
                .Select(x => x + ":" + Convert.ToHexString(
                    System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(x))))
                .ToArray();

        public void Dispose()
        {
            if (Directory.Exists(Raiz)) Directory.Delete(Raiz, recursive: true);
        }
    }
}
