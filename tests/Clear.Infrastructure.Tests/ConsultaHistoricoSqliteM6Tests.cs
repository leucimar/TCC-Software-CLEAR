using Clear.Application.Inventario;
using Clear.Domain.Arquivos;
using Clear.Domain.Duplicatas;
using Clear.Domain.Inventario;
using Clear.Application.Duplicatas;
using Clear.Infrastructure.Historico;
using System.Runtime.CompilerServices;

namespace Clear.Infrastructure.Tests;

[TestClass]
public sealed class ConsultaHistoricoSqliteM6Tests
{
    [TestMethod]
    public async Task ConsultaPaginasSemLimitarDadosPersistidos()
    {
        using var pasta = new TesteBanco();
        var arquivos = Enumerable.Range(0, 405).Select(i =>
            EntradaInventariada.DeArquivo(new ArquivoInventariado(
                Path.Combine(pasta.Origem, $"arquivo_{i:D4}.txt"), i))).ToArray();
        string id;
        await using (var sessao = await SessaoHistoricoSqliteM6.IniciarAsync(
            pasta.Origem, pasta.Banco, ParametrosHistoricoM6.SemFiltros))
        {
            id = sessao.IdExecucao;
            var inv = await new InventariarDiretorio(new Fonte(arquivos),
                limiteAmostra: 3, observador: sessao).ExecutarAsync(pasta.Origem);
            await sessao.ConcluirAsync(inv, null);
        }

        var p1 = await ConsultaHistoricoSqliteM6.ConsultarEntradasAsync(pasta.Banco, id, 0);
        var p2 = await ConsultaHistoricoSqliteM6.ConsultarEntradasAsync(pasta.Banco, id, 1);
        var p3 = await ConsultaHistoricoSqliteM6.ConsultarEntradasAsync(pasta.Banco, id, 2);
        var p4 = await ConsultaHistoricoSqliteM6.ConsultarEntradasAsync(pasta.Banco, id, 3);
        Assert.HasCount(200, p1);
        Assert.HasCount(200, p2);
        Assert.HasCount(5, p3);
        Assert.HasCount(0, p4);
        Assert.AreNotEqual(p1[0].Caminho, p2[0].Caminho);
        Assert.AreEqual(405, p1.Count + p2.Count + p3.Count);
    }

    [TestMethod]
    public async Task ConsultaClassificacoesSalvasSemReexaminarArquivo()
    {
        using var pasta = new TesteBanco();
        string id;
        await using (var sessao = await SessaoHistoricoSqliteM6.IniciarAsync(
            pasta.Origem, pasta.Banco, ParametrosHistoricoM6.SemFiltros))
        {
            id = sessao.IdExecucao;
            var entrada = EntradaInventariada.DeArquivo(
                new ArquivoInventariado(Path.Combine(pasta.Origem, "vazio.bak"), 0));
            var inv = await new InventariarDiretorio(new Fonte(entrada),
                observador: sessao).ExecutarAsync(pasta.Origem);
            await sessao.ConcluirAsync(inv, null);
        }

        // A consulta funciona mesmo sem arquivo real em disco: usa apenas SQLite.
        var registros = await ConsultaHistoricoSqliteM6.ConsultarEntradasAsync(pasta.Banco, id, 0);
        Assert.HasCount(1, registros);
        StringAssert.Contains(registros[0].CategoriasJson, "ArquivoVazio");
        StringAssert.Contains(registros[0].CategoriasJson, "CopiaBackupOuTemporario");
        StringAssert.Contains(registros[0].EvidenciasJson, "Justificativa");
        Assert.IsFalse(registros[0].AcimaDoLimitePessoal);
    }

    [TestMethod]
    public async Task ConsultaMembrosDeGruposPersistidos()
    {
        using var pasta = new TesteBanco();
        var a = new ArquivoInventariado(Path.Combine(pasta.Origem, "a.txt"), 3);
        var b = new ArquivoInventariado(Path.Combine(pasta.Origem, "b.txt"), 3);
        var dup = new ResultadoDuplicatas(2, 2, 2,
            new[] { new GrupoDuplicatasExatas(3, new string('A', 64), new[] { a, b }) },
            Array.Empty<OcorrenciaInventario>(), 0);
        string id;
        await using (var sessao = await SessaoHistoricoSqliteM6.IniciarAsync(
            pasta.Origem, pasta.Banco, ParametrosHistoricoM6.SemFiltros))
        {
            id = sessao.IdExecucao;
            var inv = await new InventariarDiretorio(new Fonte(
                EntradaInventariada.DeArquivo(a),
                EntradaInventariada.DeArquivo(b)), observador: sessao)
                .ExecutarAsync(pasta.Origem);
            await sessao.ConcluirAsync(inv, dup);
        }

        var grupos = await ConsultaHistoricoSqliteM6.ConsultarDuplicatasAsync(pasta.Banco, id);
        Assert.HasCount(2, grupos);
        Assert.AreEqual(1L, grupos[0].Grupo);
        Assert.AreEqual(3L, grupos[0].TamanhoBytes);
        Assert.AreEqual(new string('A', 64), grupos[0].Sha256);
        Assert.AreEqual(3L, grupos[0].BytesPotenciaisGrupo);
        Assert.HasCount(0, await ConsultaHistoricoSqliteM6.ConsultarOcorrenciasAsync(pasta.Banco, id));
    }

    [TestMethod]
    public async Task ConsultaNaoAceitaPaginaNegativaNemCriaBancoAusente()
    {
        using var pasta = new TesteBanco();
        await Assert.ThrowsExactlyAsync<ArgumentOutOfRangeException>(async () =>
            await ConsultaHistoricoSqliteM6.ConsultarEntradasAsync(
                pasta.Banco, "123", -1));
        await Assert.ThrowsExactlyAsync<FileNotFoundException>(async () =>
            await ConsultaHistoricoSqliteM6.ConsultarEntradasAsync(
                pasta.Banco, "123", 0));
        Assert.IsFalse(File.Exists(pasta.Banco));
    }

    [TestMethod]
    public async Task IdExecutadoEhParametroEImpedeRetornarOutraExecucao()
    {
        using var pasta = new TesteBanco();
        string id;
        await using (var sessao = await SessaoHistoricoSqliteM6.IniciarAsync(
            pasta.Origem, pasta.Banco, ParametrosHistoricoM6.SemFiltros))
        {
            id = sessao.IdExecucao;
            var inv = await new InventariarDiretorio(new Fonte(
                EntradaInventariada.DeArquivo(
                    new ArquivoInventariado(Path.Combine(pasta.Origem, "teste.txt"), 1))),
                observador: sessao).ExecutarAsync(pasta.Origem);
            await sessao.ConcluirAsync(inv, null);
        }
        Assert.HasCount(1, await ConsultaHistoricoSqliteM6.ConsultarEntradasAsync(
            pasta.Banco, id, 0));
        Assert.HasCount(0, await ConsultaHistoricoSqliteM6.ConsultarEntradasAsync(
            pasta.Banco, id + "' OR '1'='1", 0));
    }

    private sealed class TesteBanco : IDisposable
    {
        private readonly string _base = Path.Combine(Path.GetTempPath(),
            "CLEAR_M6_Consulta_" + Guid.NewGuid().ToString("N"));
        public string Origem { get; }
        public string Banco { get; }

        public TesteBanco()
        {
            Origem = Directory.CreateDirectory(Path.Combine(_base, "Acervo")).FullName;
            Banco = Path.Combine(_base, "Saida", "historico.sqlite3");
        }

        public void Dispose()
        {
            if (Directory.Exists(_base)) Directory.Delete(_base, recursive: true);
        }
    }

    private sealed class Fonte(params EntradaInventariada[] entradas) : IFonteInventarioComEntradas
    {
        public async IAsyncEnumerable<EntradaInventariada> EnumerarEntradasAsync(
            string diretorioRaiz, [EnumeratorCancellation] CancellationToken token = default)
        {
            foreach (var entrada in entradas)
            {
                token.ThrowIfCancellationRequested();
                await Task.Yield();
                yield return entrada;
            }
        }

        public async IAsyncEnumerable<ArquivoInventariado> EnumerarAsync(
            string diretorioRaiz, [EnumeratorCancellation] CancellationToken token = default)
        {
            await Task.Yield();
            yield break;
        }
    }
}
