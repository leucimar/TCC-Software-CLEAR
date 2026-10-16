using System.Runtime.CompilerServices;
using System.Text.Json;
using Clear.Application.Duplicatas;
using Clear.Application.Inventario;
using Clear.Domain.Analise;
using Clear.Domain.Arquivos;
using Clear.Domain.Compatibilidade;
using Clear.Domain.Duplicatas;
using Clear.Domain.Inventario;
using Clear.Infrastructure.Historico;
using Microsoft.Data.Sqlite;

namespace Clear.Infrastructure.Tests;

[TestClass]
public sealed class HistoricoSqliteM6Tests
{
    [TestMethod]
    public async Task PersisteTodasAsEntradasMesmoComAmostraLimitada()
    {
        using var pasta = new PastaSintetica();
        var entradas = Enumerable.Range(0, 1201).Select(i =>
            EntradaInventariada.DeArquivo(new ArquivoInventariado(
                Path.Combine(pasta.Origem, $"item_{i:D4}.txt"), i))).ToArray();
        var fonte = new FonteMista(entradas);

        await using (var sessao = await SessaoHistoricoSqliteM6.IniciarAsync(
            pasta.Origem, pasta.Banco, ParametrosHistoricoM6.SemFiltros))
        {
            var resultado = await new InventariarDiretorio(fonte,
                limiteAmostra: 3, observador: sessao).ExecutarAsync(pasta.Origem);
            Assert.AreEqual(1, fonte.Chamadas);
            Assert.HasCount(3, resultado.AmostraAvaliacoes);
            Assert.AreEqual(1201L, sessao.EntradasPersistidas);
            await sessao.ConcluirAsync(resultado, null);
        }

        var historico = await SessaoHistoricoSqliteM6.ConsultarUltimasAsync(pasta.Banco);
        Assert.HasCount(1, historico);
        Assert.AreEqual(1201L, historico[0].QuantidadeArquivos);
        Assert.AreEqual(1201L, historico[0].EntradasPersistidas);
        Assert.AreEqual(1201L, await ContarAsync(pasta.Banco, "entradas"));
        Assert.AreEqual("Concluida", historico[0].Estado);
    }

    [TestMethod]
    public async Task DescarteSemConclusaoFazRollbackIntegral()
    {
        using var pasta = new PastaSintetica();
        await using (var sessao = await SessaoHistoricoSqliteM6.IniciarAsync(
            pasta.Origem, pasta.Banco, ParametrosHistoricoM6.SemFiltros))
        {
            var arquivo = new ArquivoInventariado(Path.Combine(pasta.Origem, "item.txt"), 3);
            var entrada = EntradaInventariada.DeArquivo(arquivo);
            await sessao.RegistrarAsync(entrada, null,
                new AvaliacaoEntrada(entrada, Array.Empty<ResultadoVerificacao>(), false));
        }

        var historico = await SessaoHistoricoSqliteM6.ConsultarUltimasAsync(pasta.Banco);
        Assert.HasCount(0, historico);
        Assert.AreEqual(0L, await ContarAsync(pasta.Banco, "execucoes"));
        Assert.AreEqual(0L, await ContarAsync(pasta.Banco, "entradas"));
    }

    [TestMethod]
    public async Task HistoricoPermaneceAposFecharEAbrirConexao()
    {
        using var pasta = new PastaSintetica();
        for (var i = 0; i < 2; i++)
        {
            await using var sessao = await SessaoHistoricoSqliteM6.IniciarAsync(
                pasta.Origem, pasta.Banco, ParametrosHistoricoM6.SemFiltros);
            var resultado = await new InventariarDiretorio(
                new FonteMista(EntradaInventariada.DeArquivo(
                    new ArquivoInventariado(Path.Combine(pasta.Origem, "arquivo.txt"), i + 1))),
                observador: sessao).ExecutarAsync(pasta.Origem);
            await sessao.ConcluirAsync(resultado, null);
        }
        var consultas = await SessaoHistoricoSqliteM6.ConsultarUltimasAsync(pasta.Banco);
        Assert.HasCount(2, consultas);
        Assert.AreNotEqual(consultas[0].Id, consultas[1].Id);
        Assert.AreEqual(2L, await ContarAsync(pasta.Banco, "entradas"));
    }

    [TestMethod]
    public async Task SalvaClassificacoesRegrasEParametrosSemReaproveitarCache()
    {
        using var pasta = new PastaSintetica();
        var parametros = new ParametrosHistoricoM6(
            new[] { "ArquivoVazio" },
            "SharePointOneDriveEmpresarial", "SharePointOnline",
            new[] { "SPO-NOME-004" }, 3, null, null, false);
        await using (var sessao = await SessaoHistoricoSqliteM6.IniciarAsync(
            pasta.Origem, pasta.Banco, parametros))
        {
            var arquivo = new ArquivoInventariado(
                Path.Combine(pasta.Origem, "_vti_.txt"), 0);
            var config = new ConfiguracaoCompatibilidade(
                PerfilDestino.SharePointEmpresarial(SubtipoSharePoint.SharePointOnline),
                new[] { "SPO-NOME-004" });
            var resultado = await new InventariarDiretorio(
                new FonteMista(EntradaInventariada.DeArquivo(arquivo)),
                configuracaoCompatibilidade: config,
                observador: sessao).ExecutarAsync(pasta.Origem);
            await sessao.ConcluirAsync(resultado, null);
        }

        await using var conexao = Abrir(pasta.Banco);
        await conexao.OpenAsync();
        await using var cmd = conexao.CreateCommand();
        cmd.CommandText = "SELECT categorias_json, verificacoes_json FROM entradas LIMIT 1;";
        await using var leitor = await cmd.ExecuteReaderAsync();
        Assert.IsTrue(await leitor.ReadAsync());
        StringAssert.Contains(leitor.GetString(0), "ArquivoVazio");
        StringAssert.Contains(leitor.GetString(1), "SPO-NOME-004");
        StringAssert.Contains(leitor.GetString(1), "IncompatibilidadeDocumentada");

        await using var cmd2 = conexao.CreateCommand();
        cmd2.CommandText = "SELECT parametros_json FROM execucoes LIMIT 1;";
        var parametrosJson = (string)(await cmd2.ExecuteScalarAsync() ?? "");
        StringAssert.Contains(parametrosJson, "SPO-NOME-004");
    }

    [TestMethod]
    public async Task PersisteGruposConfirmadosESeusMembros()
    {
        using var pasta = new PastaSintetica();
        var a = new ArquivoInventariado(Path.Combine(pasta.Origem, "a.txt"), 3);
        var b = new ArquivoInventariado(Path.Combine(pasta.Origem, "b.txt"), 3);
        var dup = new ResultadoDuplicatas(2, 2, 2,
            new[] { new GrupoDuplicatasExatas(3, new string('a', 64), new[] { a, b }) },
            Array.Empty<OcorrenciaInventario>(), 0);
        await using (var sessao = await SessaoHistoricoSqliteM6.IniciarAsync(
            pasta.Origem, pasta.Banco, ParametrosHistoricoM6.SemFiltros))
        {
            var inv = await new InventariarDiretorio(
                new FonteMista(EntradaInventariada.DeArquivo(a),
                    EntradaInventariada.DeArquivo(b)),
                observador: sessao).ExecutarAsync(pasta.Origem);
            await sessao.ConcluirAsync(inv, dup);
        }

        Assert.AreEqual(1L, await ContarAsync(pasta.Banco, "grupos_duplicatas"));
        Assert.AreEqual(2L, await ContarAsync(pasta.Banco, "membros_duplicatas"));
        var ultima = await SessaoHistoricoSqliteM6.ConsultarUltimasAsync(pasta.Banco);
        Assert.AreEqual(1L, ultima[0].GruposDuplicatas);
    }

    [TestMethod]
    public async Task NaoCriaBancoDentroDoAcervo()
    {
        using var pasta = new PastaSintetica();
        await Assert.ThrowsExactlyAsync<ArgumentException>(async () =>
            await SessaoHistoricoSqliteM6.IniciarAsync(
                pasta.Origem, Path.Combine(pasta.Origem, "historico.sqlite3"),
                ParametrosHistoricoM6.SemFiltros));
        Assert.IsFalse(File.Exists(Path.Combine(pasta.Origem, "historico.sqlite3")));
    }

    [TestMethod]
    public async Task BancoComVersaoFuturaNaoEhAbertoParaEscrita()
    {
        using var pasta = new PastaSintetica();
        // Este cenário cria o banco diretamente, sem passar pelo IniciarAsync
        // (que normalmente cria a pasta pai). Preparar a pasta do fixture.
        Directory.CreateDirectory(Path.GetDirectoryName(pasta.Banco)!);
        await using (var conn = Abrir(pasta.Banco))
        {
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "PRAGMA user_version = 99;";
            await cmd.ExecuteNonQueryAsync();
        }
        await Assert.ThrowsExactlyAsync<InvalidDataException>(async () =>
            await SessaoHistoricoSqliteM6.IniciarAsync(
                pasta.Origem, pasta.Banco, ParametrosHistoricoM6.SemFiltros));
    }

    [TestMethod]
    public async Task ConsultarBancoInexistenteRetornaListaVaziaSemCriarArquivo()
    {
        using var pasta = new PastaSintetica();
        var registros = await SessaoHistoricoSqliteM6.ConsultarUltimasAsync(pasta.Banco);
        Assert.HasCount(0, registros);
        Assert.IsFalse(File.Exists(pasta.Banco));
    }

    [TestMethod]
    public async Task FalhaNoObservadorNaoConsolidaHistorico()
    {
        using var pasta = new PastaSintetica();
        await using (var sessao = await SessaoHistoricoSqliteM6.IniciarAsync(
            pasta.Origem, pasta.Banco, ParametrosHistoricoM6.SemFiltros))
        {
            var arquivo = new ArquivoInventariado(Path.Combine(pasta.Origem, "arquivo.txt"), 5);
            var entrada = EntradaInventariada.DeArquivo(arquivo);
            var composto = new ObservadorInventarioComposto(sessao, new ObservadorComErro());
            await Assert.ThrowsExactlyAsync<IOException>(async () =>
                await new InventariarDiretorio(new FonteMista(entrada),
                    observador: composto).ExecutarAsync(pasta.Origem));
        }
        Assert.HasCount(0, await SessaoHistoricoSqliteM6.ConsultarUltimasAsync(pasta.Banco));
    }

    private static SqliteConnection Abrir(string path) => new(new SqliteConnectionStringBuilder
    {
        DataSource = path, Pooling = false
    }.ToString());

    private static async Task<long> ContarAsync(string path, string tabela)
    {
        await using var conn = Abrir(path);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT COUNT(*) FROM {tabela};";
        return Convert.ToInt64(await cmd.ExecuteScalarAsync());
    }

    private sealed class FonteMista(params EntradaInventariada[] entradas) : IFonteInventarioComEntradas
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

    private sealed class ObservadorComErro : IObservadorInventario
    {
        public ValueTask RegistrarAsync(EntradaInventariada entrada,
            ResultadoAnaliseArquivo? classificacaoGeral,
            AvaliacaoEntrada avaliacaoCompatibilidade,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromException(new IOException("Falha forçada."));
    }

    private sealed class PastaSintetica : IDisposable
    {
        private readonly string _base;
        public string Origem { get; }
        public string Banco { get; }
        public PastaSintetica()
        {
            _base = Path.Combine(Path.GetTempPath(),
                "CLEAR_M6_SQLite_" + Guid.NewGuid().ToString("N"));
            Origem = Directory.CreateDirectory(Path.Combine(_base, "Acervo")).FullName;
            Banco = Path.Combine(_base, "Historico", "analises.sqlite3");
        }

        public void Dispose()
        {
            if (Directory.Exists(_base))
                Directory.Delete(_base, true);
        }
    }
}
