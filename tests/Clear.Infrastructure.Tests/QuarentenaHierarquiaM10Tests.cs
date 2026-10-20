using Clear.Infrastructure.Historico;
using Clear.Infrastructure.Quarentena;

namespace Clear.Infrastructure.Tests;

[TestClass]
public sealed class QuarentenaHierarquiaM10Tests
{
    [TestMethod]
    public async Task ArquivoProfundoPreservaHierarquiaFisicaEAceitaRestauracao()
    {
        using var pasta = new Cenario();
        var entrada = pasta.Entrada;
        var plano = await ServicoQuarentenaM7.PrepararAsync(
            entrada, pasta.Execucao, pasta.Quarentena);
        var esperado = Path.Combine(pasta.Quarentena, "Acervo",
            "Financeiro", "2025", "Relatorios", "balanco.xlsx");
        Assert.AreEqual(esperado, plano.CaminhoDestino);
        Assert.IsTrue(File.Exists(pasta.Arquivo));
        Assert.IsFalse(File.Exists(pasta.Banco));

        var enviado = await ServicoQuarentenaM7.ExecutarAsync(plano, true, pasta.Banco);
        Assert.AreEqual("EmQuarentena", enviado.Estado);
        Assert.IsFalse(File.Exists(pasta.Arquivo));
        Assert.IsTrue(File.Exists(esperado));
        CollectionAssert.AreEqual(pasta.Conteudo, await File.ReadAllBytesAsync(esperado));

        var restauracao = await ServicoRestauracaoM8.PrepararAsync(
            plano.IdOperacao, pasta.Banco);
        var resultado = await ServicoRestauracaoM8.RestaurarAsync(
            restauracao, true, pasta.Banco);
        Assert.AreEqual("Restaurado", resultado.Estado);
        Assert.IsTrue(File.Exists(pasta.Arquivo));
        Assert.IsFalse(File.Exists(esperado));
        CollectionAssert.AreEqual(pasta.Conteudo, await File.ReadAllBytesAsync(pasta.Arquivo));
        Assert.IsFalse(Directory.Exists(Path.Combine(pasta.Quarentena, "Acervo")),
            "Diretórios vazios da quarentena devem ser removidos após a restauração.");
    }

    [TestMethod]
    public async Task PlanoAdulteradoParaOutraHierarquiaEhRecusadoAntesDaAuditoria()
    {
        using var pasta = new Cenario();
        var plano = await ServicoQuarentenaM7.PrepararAsync(
            pasta.Entrada, pasta.Execucao, pasta.Quarentena);
        var adulterado = plano with
        {
            CaminhoDestino = Path.Combine(pasta.Quarentena, plano.IdOperacao,
                "Outra", "balanco.xlsx")
        };
        await Assert.ThrowsExactlyAsync<IOException>(async () =>
            await ServicoQuarentenaM7.ExecutarAsync(adulterado, true, pasta.Banco));
        Assert.IsFalse(File.Exists(pasta.Banco));
        Assert.IsTrue(File.Exists(pasta.Arquivo));
    }

    [TestMethod]
    public async Task NegacaoPreservaAcervoEAuditoriaMesmoEmPastaProfunda()
    {
        using var pasta = new Cenario();
        var plano = await ServicoQuarentenaM7.PrepararAsync(
            pasta.Entrada, pasta.Execucao, pasta.Quarentena);
        await Assert.ThrowsExactlyAsync<OperationCanceledException>(async () =>
            await ServicoQuarentenaM7.ExecutarAsync(plano, false, pasta.Banco));
        Assert.IsTrue(File.Exists(pasta.Arquivo));
        Assert.IsFalse(File.Exists(pasta.Banco));
        Assert.IsFalse(File.Exists(plano.CaminhoDestino));
    }

    private sealed class Cenario : IDisposable
    {
        private readonly string _base = Path.Combine(Path.GetTempPath(),
            "CLEAR_M10_Hierarquia_" + Guid.NewGuid().ToString("N"));
        public byte[] Conteudo { get; } = [10, 22, 44, 88, 7];
        public string Origem { get; }
        public string Arquivo { get; }
        public string Quarentena => Path.Combine(_base, "Quarentena");
        public string Banco => Path.Combine(_base, "auditoria", "quarentena.sqlite3");
        public EntradaHistoricaM6 Entrada { get; }
        public ResumoExecucaoHistoricaM6 Execucao { get; }

        public Cenario()
        {
            Origem = Path.Combine(_base, "Acervo");
            Arquivo = Path.Combine(Origem,
                "Financeiro", "2025", "Relatorios", "balanco.xlsx");
            Directory.CreateDirectory(Path.GetDirectoryName(Arquivo)!);
            File.WriteAllBytes(Arquivo, Conteudo);
            Entrada = new EntradaHistoricaM6(1, "Arquivo", Arquivo,
                Conteudo.Length, File.GetLastWriteTimeUtc(Arquivo).ToString("O"),
                "[]", "[]", "[]", false);
            Execucao = new ResumoExecucaoHistoricaM6(
                "teste-hierarquia", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow,
                Origem, "Concluida", 1, 3, Conteudo.Length, 4, 0, 0);
        }

        public void Dispose()
        {
            if (Directory.Exists(_base))
                Directory.Delete(_base, recursive: true);
        }
    }
}
