using System.Runtime.CompilerServices;
using System.Text.Json;
using Clear.Application.Duplicatas;
using Clear.Application.Inventario;
using Clear.Domain.Analise;
using Clear.Domain.Arquivos;
using Clear.Domain.Compatibilidade;
using Clear.Domain.Duplicatas;
using Clear.Domain.Inventario;
using Clear.Infrastructure.Relatorios;

namespace Clear.Infrastructure.Tests;

[TestClass]
public sealed class ExportadorRelatoriosM5Tests
{
    [TestMethod]
    public async Task ExportacaoEmFluxoUltrapassaLimiteDaAmostraSemPerderLinhas()
    {
        var (diretorio, raiz, saida) = CriarPastas();
        try
        {
            var fonte = new FonteSintetica(Enumerable.Range(0, 1205)
                .Select(i => EntradaInventariada.DeArquivo(
                    new ArquivoInventariado(Path.Combine(raiz, $"documento_{i:D4}.txt"), 1)))
                .ToArray());
            await using var exportador = ExportadorRelatoriosM5.Iniciar(
                raiz, saida, FiltroLinhasRelatorio.TodasAsEntradas);
            var inventario = await new InventariarDiretorio(fonte,
                limiteAmostra: 10, observador: exportador).ExecutarAsync(raiz);
            var arquivos = await exportador.ConcluirAsync(inventario, null,
                new[] { "ArquivoVazio", "CopiaBackupOuTemporario" }, null, null);

            Assert.AreEqual(1205L, inventario.QuantidadeArquivos);
            Assert.AreEqual(1, fonte.Chamadas);
            Assert.HasCount(10, inventario.AmostraArquivos);
            Assert.AreEqual(1205L, arquivos.LinhasExportadas);
            Assert.HasCount(1206, File.ReadAllLines(arquivos.InventarioCsv));
            Assert.IsNull(arquivos.DuplicatasCsv);

            using var json = JsonDocument.Parse(await File.ReadAllTextAsync(arquivos.ResumoJson));
            Assert.AreEqual(1205L, json.RootElement
                .GetProperty("Inventario").GetProperty("QuantidadeArquivos").GetInt64());
            Assert.AreEqual(1205L, json.RootElement
                .GetProperty("LinhasExportadas").GetInt64());
            Assert.IsFalse(json.RootElement.GetProperty("Duplicatas")
                .GetProperty("Executada").GetBoolean());
        }
        finally { Apagar(diretorio); }
    }

    [TestMethod]
    public async Task FiltroAchadosNaoPerdeTotaisNemConfundeTipos()
    {
        var (diretorio, raiz, saida) = CriarPastas();
        try
        {
            var fonte = new FonteSintetica(
                EntradaInventariada.DeArquivo(new ArquivoInventariado(Path.Combine(raiz, "vazio.txt"), 0)),
                EntradaInventariada.DeArquivo(new ArquivoInventariado(Path.Combine(raiz, "backup.bak"), 2)),
                EntradaInventariada.DeDiretorio(Path.Combine(raiz, "projeto_vti_docs")),
                EntradaInventariada.DeArquivo(new ArquivoInventariado(Path.Combine(raiz, "grande.txt"), 20)),
                EntradaInventariada.DeArquivo(new ArquivoInventariado(Path.Combine(raiz, "normal.txt"), 5)));
            var config = new ConfiguracaoCompatibilidade(
                PerfilDestino.SharePointEmpresarial(SubtipoSharePoint.SharePointOnline),
                new[] { "SPO-NOME-004" });

            await using var exportador = ExportadorRelatoriosM5.Iniciar(
                raiz, saida, FiltroLinhasRelatorio.ApenasAchados);
            var resultado = await new InventariarDiretorio(fonte,
                configuracaoCompatibilidade: config,
                filtroPersonalizado: new FiltroTamanhoPersonalizado(10),
                observador: exportador).ExecutarAsync(raiz);
            var caminhos = await exportador.ConcluirAsync(resultado, null,
                new[] { "ArquivoVazio", "CopiaBackupOuTemporario", "TamanhoPersonalizado" },
                null, null);
            Assert.AreEqual(4L, resultado.QuantidadeArquivos);
            Assert.AreEqual(1L, resultado.QuantidadeDiretorios);
            Assert.AreEqual(4L, caminhos.LinhasExportadas);
            Assert.HasCount(5, File.ReadAllLines(caminhos.InventarioCsv));
            Assert.AreEqual(1L, resultado.ItensComIncompatibilidade);
            Assert.AreEqual(1L, resultado.ArquivosAcimaLimitePersonalizado);
            var csv = await File.ReadAllTextAsync(caminhos.InventarioCsv);
            StringAssert.Contains(csv, "projeto_vti_docs");
            StringAssert.Contains(csv, "SPO-NOME-004");
            Assert.IsFalse(csv.Contains("normal.txt", StringComparison.Ordinal));

            using var json = JsonDocument.Parse(await File.ReadAllTextAsync(caminhos.ResumoJson));
            Assert.AreEqual(4L, json.RootElement.GetProperty("Inventario")
                .GetProperty("QuantidadeArquivos").GetInt64());
        }
        finally { Apagar(diretorio); }
    }

    [TestMethod]
    public async Task FormulaDeNomeEhNeutralizadaESeparadoresSaoEscapados()
    {
        var (diretorio, raiz, saida) = CriarPastas();
        try
        {
            var fonte = new FonteSintetica(EntradaInventariada.DeArquivo(
                new ArquivoInventariado(Path.Combine(raiz, "=SUM(1;2).txt"), 1)));
            await using var exportador = ExportadorRelatoriosM5.Iniciar(
                raiz, saida, FiltroLinhasRelatorio.TodasAsEntradas);
            var inventario = await new InventariarDiretorio(fonte,
                observador: exportador).ExecutarAsync(raiz);
            var arquivos = await exportador.ConcluirAsync(inventario, null,
                Array.Empty<string>(), null, null);
            var csv = await File.ReadAllTextAsync(arquivos.InventarioCsv);
            StringAssert.Contains(csv, "\"'=SUM(1;2).txt\"");
            // As strings já estão todas entre aspas. Um ; no nome não
            // introduz uma coluna nova no arquivo.
            Assert.HasCount(2, File.ReadAllLines(arquivos.InventarioCsv));
        }
        finally { Apagar(diretorio); }
    }

    [TestMethod]
    public async Task GeraCsvDeDuplicatasEResumoComEstadoExecutado()
    {
        var (diretorio, raiz, saida) = CriarPastas();
        try
        {
            var a = new ArquivoInventariado(Path.Combine(raiz, "a.txt"), 3);
            var b = new ArquivoInventariado(Path.Combine(raiz, "b.txt"), 3);
            var grupo = new GrupoDuplicatasExatas(3, new string('A', 64), new[] { a, b });
            var duplicatas = new ResultadoDuplicatas(2, 2, 2,
                new[] { grupo }, Array.Empty<OcorrenciaInventario>(), 0);

            await using var exportador = ExportadorRelatoriosM5.Iniciar(
                raiz, saida, FiltroLinhasRelatorio.TodasAsEntradas);
            var inventario = await new InventariarDiretorio(
                new FonteSintetica(EntradaInventariada.DeArquivo(a),
                    EntradaInventariada.DeArquivo(b)),
                observador: exportador).ExecutarAsync(raiz);
            var arquivos = await exportador.ConcluirAsync(inventario, duplicatas,
                new[] { "DuplicatasSHA256" }, null, null);

            Assert.IsNotNull(arquivos.DuplicatasCsv);
            Assert.HasCount(3, File.ReadAllLines(arquivos.DuplicatasCsv!));
            using var json = JsonDocument.Parse(await File.ReadAllTextAsync(arquivos.ResumoJson));
            Assert.IsTrue(json.RootElement.GetProperty("Duplicatas").GetProperty("Executada").GetBoolean());
            Assert.AreEqual(3L, json.RootElement.GetProperty("Duplicatas")
                .GetProperty("BytesPotencialmenteRedundantes").GetInt64());
        }
        finally { Apagar(diretorio); }
    }

    [TestMethod]
    public async Task CancelarExcluiPartesENaoPublicaRelatorio()
    {
        var (diretorio, raiz, saida) = CriarPastas();
        try
        {
            await using (var exportador = ExportadorRelatoriosM5.Iniciar(
                raiz, saida, FiltroLinhasRelatorio.TodasAsEntradas))
            {
                var arquivo = new ArquivoInventariado(Path.Combine(raiz, "a.txt"), 1);
                var entrada = EntradaInventariada.DeArquivo(arquivo);
                var avaliacao = new AvaliacaoEntrada(entrada,
                    Array.Empty<ResultadoVerificacao>(), false);
                await exportador.RegistrarAsync(entrada, null, avaliacao);

                await Assert.ThrowsExactlyAsync<OperationCanceledException>(async () =>
                    await exportador.RegistrarAsync(entrada, null, avaliacao,
                        new CancellationToken(true)));
            }
            Assert.HasCount(0, Directory.GetFiles(saida));
        }
        finally { Apagar(diretorio); }
    }

    [TestMethod]
    public void PastaDeSaidaDentroDoAcervoEhRejeitada()
    {
        var (diretorio, raiz, saida) = CriarPastas();
        try
        {
            var dentro = Directory.CreateDirectory(Path.Combine(raiz, "Resultados")).FullName;
            Assert.ThrowsExactly<ArgumentException>(() => ExportadorRelatoriosM5.Iniciar(
                raiz, dentro, FiltroLinhasRelatorio.TodasAsEntradas));
            Assert.ThrowsExactly<ArgumentException>(() => ExportadorRelatoriosM5.Iniciar(
                raiz, raiz, FiltroLinhasRelatorio.TodasAsEntradas));
            Assert.HasCount(0, Directory.GetFiles(dentro));
        }
        finally { Apagar(diretorio); }
    }

    [TestMethod]
    public void FiltroInvalidoEhRejeitadoAntesDeCriarArquivos()
    {
        var (diretorio, raiz, saida) = CriarPastas();
        try
        {
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
                ExportadorRelatoriosM5.Iniciar(raiz, saida, (FiltroLinhasRelatorio)42));
            Assert.HasCount(0, Directory.GetFiles(saida));
        }
        finally { Apagar(diretorio); }
    }

    private static (string diretorio, string raiz, string saida) CriarPastas()
    {
        var diretorio = Path.Combine(Path.GetTempPath(),
            "CLEAR_M5_Testes_" + Guid.NewGuid().ToString("N"));
        var raiz = Directory.CreateDirectory(Path.Combine(diretorio, "Origem")).FullName;
        var saida = Directory.CreateDirectory(Path.Combine(diretorio, "Exportados")).FullName;
        return (diretorio, raiz, saida);
    }

    private static void Apagar(string diretorio)
    {
        if (Directory.Exists(diretorio))
            Directory.Delete(diretorio, recursive: true);
    }

    private sealed class FonteSintetica(params EntradaInventariada[] entradas) : IFonteInventarioComEntradas
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
