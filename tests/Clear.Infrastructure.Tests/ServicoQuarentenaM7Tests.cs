using Clear.Infrastructure.Historico;
using Clear.Infrastructure.Quarentena;

namespace Clear.Infrastructure.Tests;

[TestClass]
public sealed class ServicoQuarentenaM7Tests
{
    [TestMethod]
    public async Task PrepararNaoMoveArquivoNemCriaBanco()
    {
        using var c = new Cenario();
        var plano = await c.PrepararAsync();
        Assert.IsTrue(File.Exists(c.Arquivo));
        Assert.IsFalse(File.Exists(plano.CaminhoDestino));
        Assert.IsFalse(File.Exists(c.Banco));
        Assert.HasCount(64, plano.Sha256Hex);
    }

    [TestMethod]
    public async Task NegarConfirmacaoNaoMoveNemCriaAuditoria()
    {
        using var c = new Cenario();
        var plano = await c.PrepararAsync();
        await Assert.ThrowsExactlyAsync<OperationCanceledException>(async () =>
            await ServicoQuarentenaM7.ExecutarAsync(plano, false, c.Banco));
        Assert.IsTrue(File.Exists(c.Arquivo));
        Assert.IsFalse(File.Exists(plano.CaminhoDestino));
        Assert.IsFalse(File.Exists(c.Banco));
    }

    [TestMethod]
    public async Task ConfirmarMoveParaQuarentenaEAuditaPersistindoReabertura()
    {
        using var c = new Cenario();
        var plano = await c.PrepararAsync();
        var registro = await ServicoQuarentenaM7.ExecutarAsync(plano, true, c.Banco);
        Assert.AreEqual("EmQuarentena", registro.Estado);
        Assert.IsFalse(File.Exists(c.Arquivo));
        Assert.IsTrue(File.Exists(plano.CaminhoDestino));
        CollectionAssert.AreEqual(c.Conteudo, await File.ReadAllBytesAsync(plano.CaminhoDestino));
        var registros = await ServicoQuarentenaM7.ConsultarAsync(c.Banco);
        Assert.HasCount(1, registros);
        Assert.AreEqual("EmQuarentena", registros[0].Estado);
        Assert.AreEqual(plano.IdExecucao, registros[0].IdExecucao);
        Assert.AreEqual(plano.Sha256Hex, registros[0].Sha256Hex);
    }

    [TestMethod]
    public async Task AlteracaoDeConteudoMesmoTamanhoSemAlterarDataEhDetectada()
    {
        using var c = new Cenario();
        var plano = await c.PrepararAsync();
        var originalUtc = File.GetLastWriteTimeUtc(c.Arquivo);
        await File.WriteAllBytesAsync(c.Arquivo, new byte[] { 9, 8, 7, 6, 5 });
        File.SetLastWriteTimeUtc(c.Arquivo, originalUtc);
        await Assert.ThrowsExactlyAsync<IOException>(async () =>
            await ServicoQuarentenaM7.ExecutarAsync(plano, true, c.Banco));
        Assert.IsTrue(File.Exists(c.Arquivo));
        Assert.IsFalse(File.Exists(plano.CaminhoDestino));
        Assert.IsFalse(File.Exists(c.Banco));
    }

    [TestMethod]
    public async Task ArquivoAlteradoDesdeHistoricoExigeNovaAnalise()
    {
        using var c = new Cenario();
        File.SetLastWriteTimeUtc(c.Arquivo, File.GetLastWriteTimeUtc(c.Arquivo).AddDays(-1));
        await Assert.ThrowsExactlyAsync<IOException>(async () => await c.PrepararAsync());
        Assert.IsTrue(File.Exists(c.Arquivo));
    }

    [TestMethod]
    public async Task HistoricoParcialNaoAutorizaMovimentacao()
    {
        using var c = new Cenario();
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(async () =>
            await ServicoQuarentenaM7.PrepararAsync(c.Entrada,
                c.Execucao with { Estado = "Parcial" }, c.RaizQuarentena));
        Assert.IsTrue(File.Exists(c.Arquivo));
    }

    [TestMethod]
    public async Task NaoMovimentaDiretorioComoSeFosseArquivo()
    {
        using var c = new Cenario();
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(async () =>
            await ServicoQuarentenaM7.PrepararAsync(
                c.Entrada with { Tipo = "Diretorio" }, c.Execucao, c.RaizQuarentena));
        Assert.IsTrue(File.Exists(c.Arquivo));
    }

    [TestMethod]
    public async Task QuarentenaDentroDoAcervoEhRejeitada()
    {
        using var c = new Cenario();
        await Assert.ThrowsExactlyAsync<IOException>(async () =>
            await ServicoQuarentenaM7.PrepararAsync(c.Entrada, c.Execucao,
                Path.Combine(c.Origem, "Interna")));
        Assert.IsTrue(File.Exists(c.Arquivo));
    }

    [TestMethod]
    public async Task AuditoriaNaoPodeSerGravadaDentroDoAcervo()
    {
        using var c = new Cenario();
        var plano = await c.PrepararAsync();
        await Assert.ThrowsExactlyAsync<IOException>(async () =>
            await ServicoQuarentenaM7.ExecutarAsync(plano, true,
                Path.Combine(c.Origem, "auditoria.sqlite3")));
        Assert.IsTrue(File.Exists(c.Arquivo));
    }

    [TestMethod]
    public async Task SegundaTentativaDoMesmoPlanoNaoMoveOutraVez()
    {
        using var c = new Cenario();
        var plano = await c.PrepararAsync();
        await ServicoQuarentenaM7.ExecutarAsync(plano, true, c.Banco);
        await Assert.ThrowsExactlyAsync<IOException>(async () =>
            await ServicoQuarentenaM7.ExecutarAsync(plano, true, c.Banco));
        Assert.HasCount(1, await ServicoQuarentenaM7.ConsultarAsync(c.Banco));
        Assert.IsFalse(File.Exists(c.Arquivo));
    }

    private sealed class Cenario : IDisposable
    {
        private readonly string _base = Path.Combine(Path.GetTempPath(),
            "CLEAR_M7_Teste_" + Guid.NewGuid().ToString("N"));

        public byte[] Conteudo { get; } = [1, 2, 3, 4, 5];
        public string Origem { get; }
        public string Arquivo { get; }
        public string RaizQuarentena { get; }
        public string Banco { get; }
        public EntradaHistoricaM6 Entrada { get; }
        public ResumoExecucaoHistoricaM6 Execucao { get; }

        public Cenario()
        {
            Origem = Directory.CreateDirectory(Path.Combine(_base, "Acervo")).FullName;
            Arquivo = Path.Combine(Origem, "relatorio.bak");
            File.WriteAllBytes(Arquivo, Conteudo);
            RaizQuarentena = Path.Combine(_base, "Quarentena");
            Banco = Path.Combine(_base, "Auditoria", "movimentos.sqlite3");
            Entrada = new EntradaHistoricaM6(1L, "Arquivo", Arquivo, Conteudo.Length,
                File.GetLastWriteTimeUtc(Arquivo).ToString("O"),
                "[\"CopiaBackupOuTemporario\"]", "[]", "[]", false);
            Execucao = new ResumoExecucaoHistoricaM6(
                "analise-teste-m7", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow,
                Origem, "Concluida", 1, 0, Conteudo.Length, 1, 0, 0);
        }

        public Task<PlanoQuarentenaM7> PrepararAsync() =>
            ServicoQuarentenaM7.PrepararAsync(Entrada, Execucao, RaizQuarentena);

        public void Dispose()
        {
            if (Directory.Exists(_base))
                Directory.Delete(_base, recursive: true);
        }
    }
}
