using Clear.Infrastructure.Historico;
using Clear.Infrastructure.Quarentena;
using Microsoft.Data.Sqlite;

namespace Clear.Infrastructure.Tests;

[TestClass]
public sealed class ServicoRestauracaoM8Tests
{
    [TestMethod]
    public async Task PrepararEhSomenteLeituraEConservaEstadoDeQuarentena()
    {
        using var c = new Cenario();
        var plano = await c.EnviarEPrepararAsync();
        Assert.IsFalse(File.Exists(c.Arquivo));
        Assert.IsTrue(File.Exists(plano.CaminhoQuarentena));
        Assert.AreEqual(c.Arquivo, plano.CaminhoOriginal);
        Assert.AreEqual((long)c.Conteudo.Length, plano.TamanhoBytes);
        Assert.AreEqual("EmQuarentena", (await c.RegistrosAsync()).Single().Estado);
    }

    [TestMethod]
    public async Task NegarConfirmacaoNaoMoveNemAlteraAuditoria()
    {
        using var c = new Cenario();
        var plano = await c.EnviarEPrepararAsync();
        await Assert.ThrowsExactlyAsync<OperationCanceledException>(async () =>
            await ServicoRestauracaoM8.RestaurarAsync(plano, false, c.Banco));
        Assert.IsFalse(File.Exists(c.Arquivo));
        Assert.IsTrue(File.Exists(plano.CaminhoQuarentena));
        Assert.AreEqual("EmQuarentena", (await c.RegistrosAsync()).Single().Estado);
    }

    [TestMethod]
    public async Task RestaurarDevolveBytesOriginaisEAtualizaAuditoriaPersistente()
    {
        using var c = new Cenario();
        var plano = await c.EnviarEPrepararAsync();
        var resultado = await ServicoRestauracaoM8.RestaurarAsync(plano, true, c.Banco);
        Assert.AreEqual("Restaurado", resultado.Estado);
        Assert.IsTrue(File.Exists(c.Arquivo));
        Assert.IsFalse(File.Exists(plano.CaminhoQuarentena));
        CollectionAssert.AreEqual(c.Conteudo, await File.ReadAllBytesAsync(c.Arquivo));

        // Abre outro SQLite em modo leitura e valida o estado consolidado.
        Assert.AreEqual("Restaurado", (await c.RegistrosAsync()).Single().Estado);
        Assert.IsTrue((await c.RegistrosAsync()).Single().Mensagem?.Contains(
            "Restauração confirmada", StringComparison.Ordinal) == true);
    }

    [TestMethod]
    public async Task ConflitoComArquivoNovoNuncaEhSobrescrito()
    {
        using var c = new Cenario();
        var plano = await c.EnviarEPrepararAsync();
        byte[] novo = [88, 77, 66];
        await File.WriteAllBytesAsync(c.Arquivo, novo);
        await Assert.ThrowsExactlyAsync<IOException>(async () =>
            await ServicoRestauracaoM8.RestaurarAsync(plano, true, c.Banco));
        CollectionAssert.AreEqual(novo, await File.ReadAllBytesAsync(c.Arquivo));
        Assert.IsTrue(File.Exists(plano.CaminhoQuarentena));
        Assert.AreEqual("EmQuarentena", (await c.RegistrosAsync()).Single().Estado);
    }

    [TestMethod]
    public async Task IntegridadeShaDetectaModificacaoComMesmoTamanho()
    {
        using var c = new Cenario();
        var plano = await c.EnviarEPrepararAsync();
        var data = File.GetLastWriteTimeUtc(plano.CaminhoQuarentena);
        await File.WriteAllBytesAsync(plano.CaminhoQuarentena,
            new byte[] { 9, 8, 7, 6, 5, 4 });
        File.SetLastWriteTimeUtc(plano.CaminhoQuarentena, data);
        await Assert.ThrowsExactlyAsync<IOException>(async () =>
            await ServicoRestauracaoM8.PrepararAsync(plano.IdOperacao, c.Banco));
        Assert.IsFalse(File.Exists(c.Arquivo));
        Assert.AreEqual("EmQuarentena", (await c.RegistrosAsync()).Single().Estado);
    }

    [TestMethod]
    public async Task SegundaRestauracaoDoMesmoRegistroEhRejeitada()
    {
        using var c = new Cenario();
        var plano = await c.EnviarEPrepararAsync();
        await ServicoRestauracaoM8.RestaurarAsync(plano, true, c.Banco);
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(async () =>
            await ServicoRestauracaoM8.PrepararAsync(plano.IdOperacao, c.Banco));
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(async () =>
            await ServicoRestauracaoM8.RestaurarAsync(plano, true, c.Banco));
        Assert.IsTrue(File.Exists(c.Arquivo));
    }

    [TestMethod]
    public async Task OperacaoPendenteNaoPodeSerRestauradaAutomaticamente()
    {
        using var c = new Cenario();
        var plano = await c.EnviarEPrepararAsync();
        await c.AtualizarEstadoAsync("Pendente");
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(async () =>
            await ServicoRestauracaoM8.PrepararAsync(plano.IdOperacao, c.Banco));
        Assert.IsTrue(File.Exists(plano.CaminhoQuarentena));
    }

    [TestMethod]
    public async Task FuncionaSobreBancoCriadoNoM7SemMigracaoDestrutiva()
    {
        using var c = new Cenario();
        var plano = await c.EnviarEPrepararAsync();
        await using (var conexao = new SqliteConnection(new SqliteConnectionStringBuilder
            { DataSource = c.Banco, Pooling = false }.ToString()))
        {
            await conexao.OpenAsync();
            await using var comando = conexao.CreateCommand();
            comando.CommandText = "PRAGMA user_version;";
            Assert.AreEqual(1L, Convert.ToInt64(await comando.ExecuteScalarAsync()));
        }
        var result = await ServicoRestauracaoM8.RestaurarAsync(plano, true, c.Banco);
        Assert.AreEqual("Restaurado", result.Estado);
        Assert.HasCount(1, await c.RegistrosAsync());
    }

    [TestMethod]
    public async Task PlanoModificadoNaoPermiteMoverParaOutroDestino()
    {
        using var c = new Cenario();
        var plano = await c.EnviarEPrepararAsync();
        var outro = Path.Combine(c.Origem, "substituto.txt");
        await Assert.ThrowsExactlyAsync<IOException>(async () =>
            await ServicoRestauracaoM8.RestaurarAsync(
                plano with { CaminhoOriginal = outro }, true, c.Banco));
        Assert.IsFalse(File.Exists(outro));
        Assert.IsTrue(File.Exists(plano.CaminhoQuarentena));
        Assert.AreEqual("EmQuarentena", (await c.RegistrosAsync()).Single().Estado);
    }

    [TestMethod]
    public async Task PastaOriginalAusenteNaoEhRecriadaSemAutorizacao()
    {
        using var c = new Cenario();
        var plano = await c.EnviarEPrepararAsync();
        Directory.Delete(c.Origem);
        await Assert.ThrowsExactlyAsync<DirectoryNotFoundException>(async () =>
            await ServicoRestauracaoM8.PrepararAsync(plano.IdOperacao, c.Banco));
        Assert.IsFalse(Directory.Exists(c.Origem));
        Assert.IsTrue(File.Exists(plano.CaminhoQuarentena));
    }

    [TestMethod]
    public async Task CancelamentoAnteriorAoRegistroPreservaQuarentena()
    {
        using var c = new Cenario();
        var plano = await c.EnviarEPrepararAsync();
        await Assert.ThrowsExactlyAsync<OperationCanceledException>(async () =>
            await ServicoRestauracaoM8.RestaurarAsync(
                plano, true, c.Banco, new CancellationToken(true)));
        Assert.IsFalse(File.Exists(c.Arquivo));
        Assert.IsTrue(File.Exists(plano.CaminhoQuarentena));
        Assert.AreEqual("EmQuarentena", (await c.RegistrosAsync()).Single().Estado);
    }

    private sealed class Cenario : IDisposable
    {
        private readonly string _base = Path.Combine(Path.GetTempPath(),
            "CLEAR_M8_Teste_" + Guid.NewGuid().ToString("N"));
        public byte[] Conteudo { get; } = [65, 66, 67, 49, 50, 51];
        public string Origem { get; }
        public string Arquivo { get; }
        public string Banco { get; }
        public string Quarentena { get; }
        private string _id = "";

        public Cenario()
        {
            Origem = Directory.CreateDirectory(Path.Combine(_base, "Acervo")).FullName;
            Quarentena = Path.Combine(_base, "Quarentena");
            Banco = Path.Combine(_base, "Auditoria", "movimentos.sqlite3");
            Arquivo = Path.Combine(Origem, "arquivo-teste.bak");
            File.WriteAllBytes(Arquivo, Conteudo);
        }

        public async Task<PlanoRestauracaoM8> EnviarEPrepararAsync()
        {
            var entrada = new EntradaHistoricaM6(1, "Arquivo", Arquivo,
                Conteudo.Length, File.GetLastWriteTimeUtc(Arquivo).ToString("O"),
                "[\"CopiaBackupOuTemporario\"]", "[]", "[]", false);
            var execucao = new ResumoExecucaoHistoricaM6(
                "analise-teste-m8", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow,
                Origem, "Concluida", 1, 0, Conteudo.Length, 1, 0, 0);
            var plano = await ServicoQuarentenaM7.PrepararAsync(
                entrada, execucao, Quarentena);
            await ServicoQuarentenaM7.ExecutarAsync(plano, true, Banco);
            _id = plano.IdOperacao;
            return await ServicoRestauracaoM8.PrepararAsync(_id, Banco);
        }

        public Task<IReadOnlyList<RegistroQuarentenaM7>> RegistrosAsync() =>
            ServicoQuarentenaM7.ConsultarAsync(Banco);

        public async Task AtualizarEstadoAsync(string estado)
        {
            await using var conexao = new SqliteConnection(
                new SqliteConnectionStringBuilder { DataSource = Banco, Pooling = false }
                    .ToString());
            await conexao.OpenAsync();
            await using var cmd = conexao.CreateCommand();
            cmd.CommandText = "UPDATE movimentacoes SET estado = $estado WHERE id = $id;";
            cmd.Parameters.AddWithValue("$estado", estado);
            cmd.Parameters.AddWithValue("$id", _id);
            await cmd.ExecuteNonQueryAsync();
        }

        public void Dispose()
        {
            if (Directory.Exists(_base)) Directory.Delete(_base, recursive: true);
        }
    }
}
