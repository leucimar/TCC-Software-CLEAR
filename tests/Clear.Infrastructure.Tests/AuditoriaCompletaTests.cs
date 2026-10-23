using Clear.Infrastructure.Quarentena;
using Microsoft.Data.Sqlite;

namespace Clear.Infrastructure.Tests;

[TestClass]
public sealed class AuditoriaCompletaTests
{
    [TestMethod]
    public async Task PaginaEExportaTodosOs467RegistrosSemTruncamento()
    {
        var pasta = Directory.CreateTempSubdirectory("CLEAR_Auditoria_").FullName;
        try
        {
            var banco = Path.Combine(pasta, "auditoria.sqlite3");
            await using (var db = new SqliteConnection(new SqliteConnectionStringBuilder
                { DataSource = banco, Pooling = false }.ToString()))
            {
                await db.OpenAsync();
                await using var cmd = db.CreateCommand();
                cmd.CommandText = """
                    CREATE TABLE movimentacoes(id TEXT, estado TEXT, original TEXT, destino TEXT,
                        tamanho INTEGER, sha256 TEXT, execucao_id TEXT, mensagem TEXT, criado_utc TEXT);
                    WITH RECURSIVE n(x) AS (VALUES(1) UNION ALL SELECT x+1 FROM n WHERE x<467)
                    INSERT INTO movimentacoes SELECT printf('%04d',x), 'Restaurado',
                        '=nome;' || x, 'destino' || x, x, 'SHA', 'exec', NULL, '2026-10-22' FROM n;
                    PRAGMA user_version = 2;
                    """;
                await cmd.ExecuteNonQueryAsync();
            }
            Assert.AreEqual(467L, await ServicoQuarentenaM7.ContarAuditoriaAsync(banco, "exec"));
            Assert.AreEqual(0L, await ServicoQuarentenaM7.ContarAuditoriaAsync(banco, "outra"));
            var paginas = new List<RegistroQuarentenaM7>();
            for (var i = 0; i < 3; i++)
                paginas.AddRange(await ServicoQuarentenaM7.ConsultarAsync(banco, "exec", pagina: i));
            Assert.HasCount(467, paginas);
            Assert.AreEqual(467, paginas.Select(p => p.IdOperacao).Distinct().Count());
            Assert.HasCount(0, await ServicoQuarentenaM7.ConsultarAsync(banco, "exec", pagina: 3));
            var csv = Path.Combine(pasta, "completo.csv");
            Assert.AreEqual(467L, await ServicoQuarentenaM7.ExportarAuditoriaCsvAsync(banco, "exec", csv));
            Assert.HasCount(468, File.ReadAllLines(csv));
            StringAssert.Contains(await File.ReadAllTextAsync(csv), "'=nome;");
            var cancelado = Path.Combine(pasta, "cancelado.csv");
            await Assert.ThrowsExactlyAsync<OperationCanceledException>(() =>
                ServicoQuarentenaM7.ExportarAuditoriaCsvAsync(banco, "exec", cancelado, new CancellationToken(true)));
            Assert.IsFalse(File.Exists(cancelado));
            Assert.HasCount(0, Directory.GetFiles(pasta, "*.part"));
        }
        finally { Directory.Delete(pasta, true); }
    }
}
