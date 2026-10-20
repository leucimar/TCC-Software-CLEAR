using System.Security.Cryptography;
using Clear.Infrastructure.Historico;
using Clear.Infrastructure.Quarentena;
using Microsoft.Data.Sqlite;

namespace Clear.Infrastructure.Tests;

[TestClass]
public sealed class FluxoQuarentenaCompartilhadaM11Tests
{
    [TestMethod]
    public async Task DoisArquivosCompartilhamArvoreEConservamAuditoriasIndividuais()
    {
        using var c = new Cenario();
        var a = await c.EnviarAsync("Financeiro/2025/RelatoriosPDF/01.pdf");
        var b = await c.EnviarAsync("Financeiro/2025/RelatoriosPDF/02.pdf");
        Assert.AreEqual(Path.GetDirectoryName(a.CaminhoDestino),
            Path.GetDirectoryName(b.CaminhoDestino));
        Assert.AreNotEqual(a.IdOperacao, b.IdOperacao);
        Assert.IsFalse(a.CaminhoDestino.Contains(a.IdOperacao, StringComparison.Ordinal));
        Assert.IsTrue(File.Exists(a.CaminhoDestino));
        Assert.IsTrue(File.Exists(b.CaminhoDestino));

        var planoA = await ServicoRestauracaoM8.PrepararAsync(a.IdOperacao, c.Banco);
        await ServicoRestauracaoM8.RestaurarAsync(planoA, true, c.Banco);
        Assert.IsTrue(File.Exists(b.CaminhoDestino));
        Assert.IsTrue(Directory.Exists(Path.GetDirectoryName(b.CaminhoDestino)!));
        Assert.IsFalse(File.Exists(a.CaminhoDestino));

        var planoB = await ServicoRestauracaoM8.PrepararAsync(b.IdOperacao, c.Banco);
        await ServicoRestauracaoM8.RestaurarAsync(planoB, true, c.Banco);
        Assert.IsFalse(Directory.Exists(Path.Combine(c.Quarentena, "Acervo")));
        Assert.IsTrue(File.Exists(a.CaminhoOriginal));
        Assert.IsTrue(File.Exists(b.CaminhoOriginal));
        Assert.IsFalse(Directory.Exists(Path.Combine(c.Quarentena, a.IdOperacao)));
    }

    [TestMethod]
    public async Task FiltrosESlecaoExplicitaRecusamIdsDeOutraExecucao()
    {
        using var c = new Cenario();
        var a = await c.EnviarAsync("A/1.pdf");
        var b = await c.EnviarAsync("A/2.xml");
        var (pdfs, total) = await ConsultaQuarentenaFiltradaM11.ConsultarAsync(
            c.Banco, c.Execucao.Id, new FiltroRestauracaoM11(Extensao: ".pdf"), 0);
        Assert.AreEqual(1L, total);
        Assert.HasCount(1, pdfs);
        Assert.AreEqual(a.IdOperacao, pdfs[0].IdOperacao);
        var ids = await ConsultaQuarentenaFiltradaM11.SelecionarTodosFiltradosAsync(
            c.Banco, c.Execucao.Id, new FiltroRestauracaoM11(
                TrechoCaminho: "A", Extensao: ".xml"));
        Assert.HasCount(1, ids);
        Assert.AreEqual(b.IdOperacao, ids[0]);
        var selecionados = await ConsultaQuarentenaFiltradaM11.ConsultarSelecionadosAsync(
            c.Banco, c.Execucao.Id, [a.IdOperacao]);
        Assert.HasCount(1, selecionados);
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            ConsultaQuarentenaFiltradaM11.ConsultarSelecionadosAsync(
                c.Banco, "execucao-errada", [a.IdOperacao]));
        Assert.IsTrue(File.Exists(a.CaminhoDestino));
        Assert.IsTrue(File.Exists(b.CaminhoDestino));
    }

    [TestMethod]
    public async Task ConsultaSemFiltroListaRegistrosEASelecaoExplicitamenteMarcada()
    {
        using var c = new Cenario();
        var a = await c.EnviarAsync("Relatorios/relatorio1.pdf");
        var b = await c.EnviarAsync("Planilhas/planilha1.xlsx");
        var (primeiraPagina, total) = await ConsultaQuarentenaFiltradaM11.ConsultarAsync(
            c.Banco, c.Execucao.Id, new FiltroRestauracaoM11(), 0);
        Assert.AreEqual(2L, total);
        Assert.HasCount(2, primeiraPagina);
        var (paginaVazia, mesmoTotal) = await ConsultaQuarentenaFiltradaM11.ConsultarAsync(
            c.Banco, c.Execucao.Id, new FiltroRestauracaoM11(), 1);
        Assert.AreEqual(2L, mesmoTotal);
        Assert.HasCount(0, paginaVazia);
        var todos = await ConsultaQuarentenaFiltradaM11.SelecionarTodosFiltradosAsync(
            c.Banco, c.Execucao.Id, new FiltroRestauracaoM11());
        Assert.HasCount(2, todos);
        CollectionAssert.AreEquivalent(new[] { a.IdOperacao, b.IdOperacao }, todos.ToArray());
        var apenasUm = await ConsultaQuarentenaFiltradaM11.ConsultarSelecionadosAsync(
            c.Banco, c.Execucao.Id, [b.IdOperacao]);
        Assert.HasCount(1, apenasUm);
        Assert.AreEqual(b.IdOperacao, apenasUm[0].IdOperacao);
        // Consultas e seleções não devem movimentar arquivos.
        Assert.IsTrue(File.Exists(a.CaminhoDestino));
        Assert.IsTrue(File.Exists(b.CaminhoDestino));
    }

    [TestMethod]
    public async Task NegacaoDeLimpezaNaoExcluiPastasNemArquivos()
    {
        using var c = new Cenario();
        var a = await c.EnviarAsync("B/a.pdf");
        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() =>
            LimpadorPastasVaziasM11.LimparAsync(c.Banco, c.Quarentena, false));
        Assert.IsTrue(File.Exists(a.CaminhoDestino));
    }

    [TestMethod]
    public async Task RestauracaoDeBancoLegadoV1RemoveSomentePastaGuidVazia()
    {
        using var c = new Cenario();
        var id = Guid.NewGuid().ToString("N");
        var original = Path.Combine(c.Origem, "Financeiro", "2024", "relatorio.pdf");
        Directory.CreateDirectory(Path.GetDirectoryName(original)!);
        var destino = Path.Combine(c.Quarentena, id,
            "Financeiro", "2024", "relatorio.pdf");
        Directory.CreateDirectory(Path.GetDirectoryName(destino)!);
        await File.WriteAllBytesAsync(destino, [1, 2, 3, 4]);
        var sha = Convert.ToHexString(SHA256.HashData(new byte[] { 1, 2, 3, 4 }));
        Directory.CreateDirectory(Path.GetDirectoryName(c.Banco)!);
        await using (var db = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = c.Banco, Pooling = false
        }.ToString()))
        {
            await db.OpenAsync();
            await using var cmd = db.CreateCommand();
            cmd.CommandText = """
                CREATE TABLE movimentacoes (
                    id TEXT PRIMARY KEY, execucao_id TEXT NOT NULL,
                    entrada_id INTEGER NOT NULL, original TEXT NOT NULL,
                    destino TEXT NOT NULL, tamanho INTEGER NOT NULL,
                    sha256 TEXT NOT NULL, estado TEXT NOT NULL,
                    criado_utc TEXT NOT NULL, mensagem TEXT);
                PRAGMA user_version = 1;
                INSERT INTO movimentacoes VALUES
                    ($id, 'analise-antiga', 1, $origem, $destino, 4,
                     $sha, 'EmQuarentena', '2025-01-01T00:00:00Z', NULL);
                """;
            cmd.Parameters.AddWithValue("$id", id);
            cmd.Parameters.AddWithValue("$origem", original);
            cmd.Parameters.AddWithValue("$destino", destino);
            cmd.Parameters.AddWithValue("$sha", sha);
            await cmd.ExecuteNonQueryAsync();
        }
        // Banco v1 ainda deve permitir localizar arquivos para restauração
        // em lote; a consulta é somente leitura e não altera o esquema.
        var (legados, totalLegados) = await ConsultaQuarentenaFiltradaM11.ConsultarAsync(
            c.Banco, "analise-antiga", new FiltroRestauracaoM11(Extensao: ".pdf"), 0);
        Assert.AreEqual(1L, totalLegados);
        Assert.HasCount(1, legados);
        Assert.AreEqual(id, legados[0].IdOperacao);
        var plano = await ServicoRestauracaoM8.PrepararAsync(id, c.Banco);
        Assert.AreEqual("Legado", plano.Estrutura);
        await ServicoRestauracaoM8.RestaurarAsync(plano, true, c.Banco);
        Assert.IsTrue(File.Exists(original));
        Assert.IsFalse(Directory.Exists(Path.Combine(c.Quarentena, id)));
    }

    [TestMethod]
    public async Task LimpezaAuditadaRemovePastaAntigaJaVaziaSemApagarOutraPasta()
    {
        using var c = new Cenario();
        var r = await c.EnviarAsync("A/antigo.pdf");
        var plano = await ServicoRestauracaoM8.PrepararAsync(r.IdOperacao, c.Banco);
        await ServicoRestauracaoM8.RestaurarAsync(plano, true, c.Banco);
        var restante = Path.Combine(c.Quarentena, "Acervo", "A");
        Directory.CreateDirectory(restante);
        var outra = Path.Combine(c.Quarentena, "outro-usuario");
        Directory.CreateDirectory(outra);
        await File.WriteAllTextAsync(Path.Combine(outra, "preservar.txt"), "NÃO APAGAR");
        var removidas = await LimpadorPastasVaziasM11.LimparAsync(
            c.Banco, c.Quarentena, true);
        Assert.IsGreaterThan(0, removidas);
        Assert.IsFalse(Directory.Exists(restante));
        Assert.IsTrue(File.Exists(Path.Combine(outra, "preservar.txt")));
        Assert.IsTrue(Directory.Exists(c.Quarentena));
    }

    private sealed class Cenario : IDisposable
    {
        private readonly string _base = Path.Combine(Path.GetTempPath(),
            "CLEAR_M11_Teste_" + Guid.NewGuid().ToString("N"));
        public string Origem { get; }
        public string Quarentena { get; }
        public string Banco { get; }
        public ResumoExecucaoHistoricaM6 Execucao { get; }

        public Cenario()
        {
            Origem = Directory.CreateDirectory(Path.Combine(_base, "Acervo")).FullName;
            Quarentena = Path.Combine(_base, "Quarentena");
            Banco = Path.Combine(_base, "Audit", "quarentena.sqlite3");
            Execucao = new ResumoExecucaoHistoricaM6(
                "execucao-m11", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow,
                Origem, "Concluida", 10, 5, 4096, 15, 0, 0);
        }

        public async Task<RegistroQuarentenaM7> EnviarAsync(string relativo)
        {
            var caminho = Path.Combine(Origem, relativo);
            Directory.CreateDirectory(Path.GetDirectoryName(caminho)!);
            await File.WriteAllBytesAsync(caminho, [10, 20, 30]);
            var entrada = new EntradaHistoricaM6(1, "Arquivo", caminho, 3,
                File.GetLastWriteTimeUtc(caminho).ToString("O"),
                "[]", "[]", "[]", false);
            var plano = await ServicoQuarentenaM7.PrepararAsync(
                entrada, Execucao, Quarentena);
            return await ServicoQuarentenaM7.ExecutarAsync(plano, true, Banco);
        }

        public void Dispose()
        {
            if (Directory.Exists(_base))
                Directory.Delete(_base, recursive: true);
        }
    }
}
