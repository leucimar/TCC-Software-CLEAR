using System.Runtime.CompilerServices;
using Clear.Application.Inventario;
using Clear.Domain.Arquivos;
using Clear.Domain.Inventario;
using Clear.Infrastructure.Historico;

namespace Clear.Infrastructure.Tests;

[TestClass]
public sealed class ConsultaEntradasFiltradasM10Tests
{
    [TestMethod]
    public async Task ConsultaFiltraCategoriasExtensaoTamanhoEDatasCombinadas()
    {
        using var c = new Cenario();
        var id = await c.CriarHistoricoAsync();
        var filtro = new FiltrosEntradasM10(
            Categoria: "CopiaBackupOuTemporario",
            Extensao: ".bak", MinimoBytes: 5, MaximoBytes: 50,
            AcessadoDesde: new DateOnly(2025, 1, 1),
            AcessadoAte: new DateOnly(2025, 12, 31));
        var (dados, total) = await ConsultaEntradasFiltradasM10.ConsultarAsync(
            c.Banco, id, filtro, 0);
        Assert.AreEqual(1L, total);
        Assert.HasCount(1, dados);
        StringAssert.EndsWith(dados[0].Caminho, "relatorio.bak");
    }

    [TestMethod]
    public void PreviaDeAmostraRecusaDestinoOcupado()
    {
        var pasta = Directory.CreateTempSubdirectory("CLEAR_Previa_").FullName;
        try
        {
            var origem = Directory.CreateDirectory(Path.Combine(pasta, "origem")).FullName;
            var destino = Directory.CreateDirectory(Path.Combine(pasta, "ocupado")).FullName;
            File.WriteAllText(Path.Combine(destino, "preservar.txt"), "conteudo");
            Assert.ThrowsExactly<IOException>(() =>
                Clear.Infrastructure.Amostragem.CriadorSubBaseExperimental.Preparar(origem, destino));
            Assert.AreEqual("conteudo", File.ReadAllText(Path.Combine(destino, "preservar.txt")));
        }
        finally { Directory.Delete(pasta, true); }
    }

    [TestMethod]
    public async Task ConsultaRespeitaPaginaESomenteArquivos()
    {
        using var c = new Cenario();
        var id = await c.CriarHistoricoAsync();
        var primeira = await ConsultaEntradasFiltradasM10.ConsultarAsync(
            c.Banco, id, new FiltrosEntradasM10(), 0);
        Assert.AreEqual(4L, primeira.Total);
        Assert.HasCount(4, primeira.Entradas);
        var vazia = await ConsultaEntradasFiltradasM10.ConsultarAsync(
            c.Banco, id, new FiltrosEntradasM10(), 1);
        Assert.AreEqual(4L, vazia.Total);
        Assert.HasCount(0, vazia.Entradas);
    }

    [TestMethod]
    public async Task ConsultaPorIdNuncaRetornaRegistroDeOutraExecucao()
    {
        using var c = new Cenario();
        var id = await c.CriarHistoricoAsync();
        var resultados = await ConsultaEntradasFiltradasM10.ConsultarAsync(
            c.Banco, id, new FiltrosEntradasM10(), 0);
        var entradaId = resultados.Entradas[0].Id;
        var arquivo = await ConsultaEntradasFiltradasM10.ConsultarPorIdAsync(
            c.Banco, id, entradaId);
        Assert.AreEqual(entradaId, arquivo.Id);
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            ConsultaEntradasFiltradasM10.ConsultarPorIdAsync(
                c.Banco, id + "outro", entradaId));
    }

    [TestMethod]
    public async Task SelecionaApenasIdsExplicitamenteMarcadosEPreservaExecucao()
    {
        using var c = new Cenario();
        var id = await c.CriarHistoricoAsync();
        var dados = await ConsultaEntradasFiltradasM10.ConsultarAsync(
            c.Banco, id, new FiltrosEntradasM10(), 0);
        var escolhido = dados.Entradas[1].Id;
        var selecionados = await ConsultaEntradasFiltradasM10.ConsultarSelecionadasAsync(
            c.Banco, id, [escolhido]);
        Assert.HasCount(1, selecionados);
        Assert.AreEqual(escolhido, selecionados[0].Id);
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            ConsultaEntradasFiltradasM10.ConsultarSelecionadasAsync(
                c.Banco, id, [escolhido, long.MaxValue]));
        await Assert.ThrowsExactlyAsync<ArgumentException>(() =>
            ConsultaEntradasFiltradasM10.ConsultarSelecionadasAsync(
                c.Banco, id, [escolhido, escolhido]));
    }

    [TestMethod]
    public async Task DuplicataEhClassificacaoAdicionalENaoApagaCategorias()
    {
        using var cena = new Cenario();
        var id = await cena.CriarHistoricoAsync();
        var pdf = (await ConsultaEntradasFiltradasM10.ConsultarAsync(
            cena.Banco, id, new FiltrosEntradasM10(Extensao: ".pdf"), 0))
            .Entradas.Single();

        await using (var db = new Microsoft.Data.Sqlite.SqliteConnection(
            new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder
            { DataSource = cena.Banco, Pooling = false }.ToString()))
        {
            await db.OpenAsync();
            await using var comando = db.CreateCommand();
            comando.CommandText = """
                INSERT INTO grupos_duplicatas
                    (execucao_id, ordem, tamanho_bytes, sha256, bytes_potenciais)
                VALUES ($exec, 1, 0, $sha, 0);
                INSERT INTO membros_duplicatas (execucao_id, ordem_grupo, caminho)
                VALUES ($exec, 1, $caminho);
                """;
            comando.Parameters.AddWithValue("$exec", id);
            comando.Parameters.AddWithValue("$sha", new string('A', 64));
            comando.Parameters.AddWithValue("$caminho", pdf.Caminho);
            await comando.ExecuteNonQueryAsync();
        }

        var resultado = await ConsultaEntradasFiltradasM10.ConsultarAsync(
            cena.Banco, id, new FiltrosEntradasM10(
                ApenasDuplicatas: true, Extensao: ".pdf"), 0);
        Assert.AreEqual(1L, resultado.Total);
        Assert.HasCount(1, resultado.Entradas);
        Assert.IsTrue(resultado.Entradas[0].EhDuplicataExata);
        Assert.AreEqual(pdf.CategoriasJson, resultado.Entradas[0].CategoriasJson);
    }

    [TestMethod]
    public async Task FiltrosInvalidosNaoConsultamDados()
    {
        using var c = new Cenario();
        await Assert.ThrowsExactlyAsync<ArgumentException>(() =>
            ConsultaEntradasFiltradasM10.ConsultarAsync(
                c.Banco, "id", new FiltrosEntradasM10(
                    MinimoBytes: 200, MaximoBytes: 100), 0));
        await Assert.ThrowsExactlyAsync<ArgumentOutOfRangeException>(() =>
            ConsultaEntradasFiltradasM10.ConsultarAsync(
                c.Banco, "id", new FiltrosEntradasM10(), -1));
    }

    [TestMethod]
    public async Task SelecaoGlobalUltrapassaDuasPaginasEExcluiCaminhosIndisponiveis()
    {
        using var c = new Cenario();
        var id = await c.CriarHistoricoAsync(adicionais: 240);
        var indisponivel = Path.Combine(c.Origem, "extra000.txt");
        var bloqueados = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { indisponivel };

        var ids = await ConsultaEntradasFiltradasM10.SelecionarTodosFiltradosAsync(
            c.Banco, id, new FiltrosEntradasM10(), bloqueados);

        Assert.HasCount(243, ids);
        Assert.HasCount(243, ids.Distinct().ToArray());
        var entradas = await ConsultaEntradasFiltradasM10.ConsultarSelecionadasAsync(
            c.Banco, id, ids);
        Assert.IsFalse(entradas.Any(x => x.Caminho == indisponivel));
    }

    [TestMethod]
    public async Task SemAlertasNaoSignificaAprovacaoEExcluiIndiciosClassificados()
    {
        using var c = new Cenario();
        var id = await c.CriarHistoricoAsync();
        var sem = await ConsultaEntradasFiltradasM10.ConsultarAsync(
            c.Banco, id, new FiltrosEntradasM10(ApenasSemAlertas: true), 0);
        var com = await ConsultaEntradasFiltradasM10.ConsultarAsync(
            c.Banco, id, new FiltrosEntradasM10(ApenasComAlertas: true), 0);
        Assert.AreEqual(1L, sem.Total);
        Assert.AreEqual(3L, com.Total);
        StringAssert.EndsWith(sem.Entradas.Single().Caminho, "tabela.csv");
    }

    private sealed class Cenario : IDisposable
    {
        private readonly string _base = Path.Combine(Path.GetTempPath(),
            "CLEAR_M10_Filtro_" + Guid.NewGuid().ToString("N"));
        public string Origem { get; }
        public string Banco { get; }

        public Cenario()
        {
            Origem = Directory.CreateDirectory(Path.Combine(_base, "Acervo")).FullName;
            Banco = Path.Combine(_base, "Saida", "historico.sqlite3");
        }

        public async Task<string> CriarHistoricoAsync(int adicionais = 0)
        {
            var data = new DateTimeOffset(2025, 4, 15, 0, 0, 0, TimeSpan.Zero);
            var entradas = new[]
            {
                new ArquivoInventariado(Path.Combine(Origem, "relatorio.bak"), 20, data, data),
                new ArquivoInventariado(Path.Combine(Origem, "outro.bak"), 100, data, data),
                new ArquivoInventariado(Path.Combine(Origem, "arquivo.pdf"), 0, data, data),
                new ArquivoInventariado(Path.Combine(Origem, "tabela.csv"), 32, data, data)
            }.Concat(Enumerable.Range(0, adicionais)
                .Select(i => new ArquivoInventariado(
                    Path.Combine(Origem, $"extra{i:000}.txt"), 10, data, data)))
                .Select(EntradaInventariada.DeArquivo).ToArray();
            await using var sessao = await SessaoHistoricoSqliteM6.IniciarAsync(
                Origem, Banco, ParametrosHistoricoM6.SemFiltros);
            var resumo = await new InventariarDiretorio(new Fonte(entradas),
                observador: sessao).ExecutarAsync(Origem);
            await sessao.ConcluirAsync(resumo, null);
            return sessao.IdExecucao;
        }

        public void Dispose()
        {
            if (Directory.Exists(_base)) Directory.Delete(_base, recursive: true);
        }
    }

    private sealed class Fonte(params EntradaInventariada[] entradas)
        : IFonteInventarioComEntradas
    {
        public async IAsyncEnumerable<EntradaInventariada> EnumerarEntradasAsync(
            string diretorioRaiz, [EnumeratorCancellation] CancellationToken ct = default)
        {
            foreach (var item in entradas)
            {
                ct.ThrowIfCancellationRequested();
                await Task.Yield();
                yield return item;
            }
        }

        public async IAsyncEnumerable<ArquivoInventariado> EnumerarAsync(
            string diretorioRaiz, [EnumeratorCancellation] CancellationToken ct = default)
        {
            await Task.Yield();
            yield break;
        }
    }
}
