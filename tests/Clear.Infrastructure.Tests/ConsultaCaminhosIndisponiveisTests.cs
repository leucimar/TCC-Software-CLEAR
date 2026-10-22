using Clear.Infrastructure.Quarentena;
using Microsoft.Data.Sqlite;

namespace Clear.Infrastructure.Tests;

[TestClass]
public sealed class ConsultaCaminhosIndisponiveisTests
{
    [TestMethod]
    public async Task BancoInexistenteNaoBloqueiaCaminhos()
    {
        var banco = Path.Combine(Path.GetTempPath(), "CLEAR_inexistente_" +
            Guid.NewGuid().ToString("N"), "auditoria.sqlite3");
        var caminhos = await ConsultaQuarentenaFiltradaM11.CaminhosIndisponiveisAsync(banco);
        Assert.HasCount(0, caminhos);
        Assert.IsFalse(File.Exists(banco));
    }

    [TestMethod]
    public async Task OcupaSomenteEstadosAtivosSemAlterarAuditoria()
    {
        var pasta = Path.Combine(Path.GetTempPath(), "CLEAR_AUD_" +
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(pasta);
        var banco = Path.Combine(pasta, "auditoria.sqlite3");
        try
        {
            await using (var db = new SqliteConnection(
                new SqliteConnectionStringBuilder { DataSource = banco, Pooling = false }.ToString()))
            {
                await db.OpenAsync();
                await using var cmd = db.CreateCommand();
                cmd.CommandText = """
                    CREATE TABLE movimentacoes (original TEXT NOT NULL, estado TEXT NOT NULL);
                    INSERT INTO movimentacoes VALUES ('C:\Base\A.txt', 'EmQuarentena');
                    INSERT INTO movimentacoes VALUES ('C:\Base\B.txt', 'Pendente');
                    INSERT INTO movimentacoes VALUES ('C:\Base\C.txt', 'RestauracaoPendente');
                    INSERT INTO movimentacoes VALUES ('C:\Base\D.txt', 'Restaurado');
                    INSERT INTO movimentacoes VALUES ('C:\Base\E.txt', 'Falha');
                    PRAGMA user_version = 2;
                    """;
                await cmd.ExecuteNonQueryAsync();
            }

            var caminhos = await ConsultaQuarentenaFiltradaM11.CaminhosIndisponiveisAsync(banco);
            Assert.HasCount(3, caminhos);
            Assert.IsTrue(caminhos.Contains(@"C:\Base\A.txt"));
            Assert.IsTrue(caminhos.Contains(@"c:\base\b.txt"));
            Assert.IsFalse(caminhos.Contains(@"C:\Base\D.txt"));
            Assert.IsFalse(caminhos.Contains(@"C:\Base\E.txt"));
        }
        finally
        {
            if (Directory.Exists(pasta)) Directory.Delete(pasta, recursive: true);
        }
    }
}
