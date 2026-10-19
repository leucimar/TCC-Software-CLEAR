using Clear.Application.Amostragem;

namespace Clear.Application.Tests;

[TestClass]
public sealed class PlanejadorAmostraEstratificadaTests
{
    private static readonly DateTime Data = new(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    [TestMethod]
    public void TrintaPorCentoMantemQuantidadeVolumeEFormatos()
    {
        var arquivos = new List<ArquivoCandidatoAmostra>();
        foreach (var (pasta, extensao, tamanho) in new[]
        {
            ("DocLayNet_core", ".png", 4096L),
            ("DocLayNet_extra", ".json", 2048L),
            ("PDF", ".pdf", 1024L),
            ("DOCX", ".docx", 4096L),
            ("TIF-RESIZED", ".tif", 16384L)
        })
        {
            for (var i = 0; i < 100; i++)
                arquivos.Add(new ArquivoCandidatoAmostra(
                    $"{pasta}/Projeto/2025/arquivo{i:D4}{extensao}", tamanho, Data));
        }

        var resultado = PlanejadorAmostraEstratificada.Planejar(arquivos, 30M, 5721);
        Assert.HasCount(150, resultado.Selecionados);
        Assert.AreEqual(30M, resultado.PercentualArquivosReal);
        Assert.AreEqual(30M, resultado.PercentualBytesReal);
        Assert.HasCount(5, resultado.Estratos);
        CollectionAssert.AreEquivalent(
            new[] { ".png", ".json", ".pdf", ".docx", ".tif" },
            resultado.Selecionados.Select(x => x.Formato).Distinct().ToArray());
        foreach (var estrato in resultado.Estratos)
        {
            Assert.AreEqual(30, estrato.QuantidadeSelecionada);
            Assert.AreEqual(estrato.BytesOriginais * 3 / 10, estrato.BytesSelecionados);
        }
    }

    [TestMethod]
    public void MesmaSementeIndependeDaOrdemDoInventario()
    {
        var a = Enumerable.Range(1, 300)
            .Select(i => new ArquivoCandidatoAmostra(
                $"Pasta/A/arquivo{i:D4}.dat", i * 137L, Data))
            .ToArray();
        var p1 = PlanejadorAmostraEstratificada.Planejar(a, 30M, 42);
        var p2 = PlanejadorAmostraEstratificada.Planejar(a.Reverse(), 30M, 42);
        CollectionAssert.AreEqual(
            p1.Selecionados.Select(x => x.CaminhoRelativo).ToArray(),
            p2.Selecionados.Select(x => x.CaminhoRelativo).ToArray());
        Assert.AreEqual(p1.BytesSelecionados, p2.BytesSelecionados);
    }

    [TestMethod]
    public void ArquivosRarosEOsSemExtensaoEstaoRepresentados()
    {
        var arquivos = Enumerable.Range(1, 90)
            .Select(x => new ArquivoCandidatoAmostra(
                $"Base/a{x:D3}.txt", 200, Data)).ToList();
        arquivos.Add(new ArquivoCandidatoAmostra("Base/isolado.raro", 200, Data));
        arquivos.Add(new ArquivoCandidatoAmostra("Base/semextensao", 0, Data));
        var plano = PlanejadorAmostraEstratificada.Planejar(arquivos, 30M);
        Assert.HasCount(28, plano.Selecionados);
        Assert.IsTrue(plano.Selecionados.Any(x => x.Formato == ".raro"));
        Assert.IsTrue(plano.Selecionados.Any(x => x.Formato == "[sem extensão]"));
        Assert.IsTrue(plano.Selecionados.Any(x => x.TamanhoBytes == 0));
    }

    [TestMethod]
    public void NaoExigeVolumeExatoQuandoArquivosSaoIndivisiveis()
    {
        var arquivos = new[]
        {
            new ArquivoCandidatoAmostra("Pasta/1.bin", 1, Data),
            new ArquivoCandidatoAmostra("Pasta/2.bin", 2, Data),
            new ArquivoCandidatoAmostra("Pasta/3.bin", 9000, Data)
        };
        var plano = PlanejadorAmostraEstratificada.Planejar(arquivos, 30M, 5721);
        Assert.HasCount(1, plano.Selecionados);
        Assert.IsTrue(plano.PercentualBytesReal != 30M);
    }

    [TestMethod]
    public void PercentualPequenoNaoPodeApagarRepresentacaoDosEstratos()
    {
        var arquivos = Enumerable.Range(1, 10)
            .Select(x => new ArquivoCandidatoAmostra(
                $"Base/arquivo{x}.ext{x}", 1, Data)).ToArray();
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            PlanejadorAmostraEstratificada.Planejar(arquivos, 10M));
    }

    [TestMethod]
    public void MetadadosInvalidosNaoSaoAceitos()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            PlanejadorAmostraEstratificada.Planejar(
                [new ArquivoCandidatoAmostra("x.txt", 1, Data)], 0M));
        Assert.ThrowsExactly<ArgumentException>(() =>
            PlanejadorAmostraEstratificada.Planejar(
                [new ArquivoCandidatoAmostra("../escapa.txt", 1, Data)]));
        Assert.ThrowsExactly<ArgumentException>(() =>
            PlanejadorAmostraEstratificada.Planejar(
                [new ArquivoCandidatoAmostra("a.txt", -1, Data)]));
    }

    [TestMethod]
    public void CemPorCentoMantemTodosOsItensIncluindoVazios()
    {
        var dados = Enumerable.Range(0, 21)
            .Select(i => new ArquivoCandidatoAmostra(
                $"pasta/file{i}.dat", i == 0 ? 0 : i * 100L, Data)).ToArray();
        var plano = PlanejadorAmostraEstratificada.Planejar(dados, 100M);
        Assert.HasCount(21, plano.Selecionados);
        Assert.AreEqual(plano.BytesOriginais, plano.BytesSelecionados);
    }
}
