using System.Text.Json;
using Clear.Infrastructure.Execucao;

namespace Clear.Infrastructure.Tests;

[TestClass]
public sealed class CheckpointAnaliseM9Tests
{
    [TestMethod]
    public void CheckpointArmazenaSomenteProgressoENaoResultados()
    {
        using var cena = new Cenario();
        using (var sessao = CheckpointAnaliseM9.Iniciar(
            cena.Raiz, "perfil=geral;regras=A", cena.Checkpoints))
        {
            sessao.Atualizar(300, 15000, forcar: true);
            var estado = CheckpointAnaliseM9.Consultar(cena.Raiz, cena.Checkpoints);
            Assert.IsNotNull(estado);
            Assert.AreEqual("EmAndamento", estado.Estado);
            Assert.AreEqual(300L, estado.ArquivosLidos);
            Assert.AreEqual(15000L, estado.BytesInformados);
            var json = File.ReadAllText(CheckpointAnaliseM9.Caminho(cena.Raiz, cena.Checkpoints));
            Assert.IsFalse(json.Contains("classificacoes", StringComparison.OrdinalIgnoreCase));
            Assert.IsFalse(json.Contains("conteudo", StringComparison.OrdinalIgnoreCase));
            Assert.IsFalse(json.Contains("hashes", StringComparison.OrdinalIgnoreCase));
        }
        Assert.AreEqual("Interrompida",
            CheckpointAnaliseM9.Consultar(cena.Raiz, cena.Checkpoints)?.Estado);
    }

    [TestMethod]
    public void CancelamentoRegistraProgressoEPermiteReinicioDoZero()
    {
        using var cena = new Cenario();
        using (var sessao = CheckpointAnaliseM9.Iniciar(
            cena.Raiz, "perfil=A", cena.Checkpoints))
        {
            sessao.Atualizar(500, 4096);
            sessao.MarcarCancelada();
        }
        var anterior = CheckpointAnaliseM9.Consultar(cena.Raiz, cena.Checkpoints);
        Assert.IsNotNull(anterior);
        Assert.AreEqual("Cancelada", anterior.Estado);
        Assert.IsTrue(anterior.PodeReiniciar);
        Assert.AreEqual(500L, anterior.ArquivosLidos);

        // Um reinício sempre começa com contadores zerados, não reaproveita
        // as 500 entradas nem a classificação de nenhuma delas.
        using var nova = CheckpointAnaliseM9.Iniciar(cena.Raiz, "perfil=A", cena.Checkpoints);
        Assert.AreEqual(0L, nova.EstadoAtual.ArquivosLidos);
        Assert.AreNotEqual(anterior.Id, nova.EstadoAtual.Id);
    }

    [TestMethod]
    public void FinalizacaoRemoveMarcadorDeInterrupcao()
    {
        using var cena = new Cenario();
        using (var sessao = CheckpointAnaliseM9.Iniciar(
            cena.Raiz, "perfil=B", cena.Checkpoints))
        {
            sessao.Atualizar(3, 100, forcar: true);
            sessao.MarcarConcluida();
        }
        Assert.IsNull(CheckpointAnaliseM9.Consultar(cena.Raiz, cena.Checkpoints));
    }

    [TestMethod]
    public void ConfiguracoesDiferentesNaoTemAssinaturaCompativel()
    {
        Assert.AreEqual(CheckpointAnaliseM9.Assinatura("a=1;b=2"),
            CheckpointAnaliseM9.Assinatura("a=1;b=2"));
        Assert.AreNotEqual(CheckpointAnaliseM9.Assinatura("a=1;b=2"),
            CheckpointAnaliseM9.Assinatura("a=2;b=2"));
    }

    [TestMethod]
    public void NaoPermiteDuasAnalisesSimultaneasDaMesmaPasta()
    {
        using var cena = new Cenario();
        using var primeira = CheckpointAnaliseM9.Iniciar(
            cena.Raiz, "perfil=A", cena.Checkpoints);
        Assert.ThrowsExactly<IOException>(() =>
            CheckpointAnaliseM9.Iniciar(cena.Raiz, "perfil=A", cena.Checkpoints));
    }

    [TestMethod]
    public void CheckpointNaoPodeFicarDentroDoAcervo()
    {
        using var cena = new Cenario();
        Assert.ThrowsExactly<IOException>(() =>
            CheckpointAnaliseM9.Iniciar(cena.Raiz, "perfil=A",
                Path.Combine(cena.Raiz, "Saida")));
        Assert.IsFalse(Directory.Exists(Path.Combine(cena.Raiz, "Saida")));
    }

    [TestMethod]
    public void CorrupcaoNoJsonEhRejeitadaSemReutilizacao()
    {
        using var cena = new Cenario();
        using (var sessao = CheckpointAnaliseM9.Iniciar(
            cena.Raiz, "perfil=A", cena.Checkpoints))
        {
            sessao.MarcarCancelada();
        }
        File.WriteAllText(CheckpointAnaliseM9.Caminho(cena.Raiz, cena.Checkpoints),
            "{ JSON corrompido");
        Assert.ThrowsExactly<InvalidDataException>(() =>
            CheckpointAnaliseM9.Consultar(cena.Raiz, cena.Checkpoints));
    }

    [TestMethod]
    public void AvancoNaoPodeRetroceder()
    {
        using var cena = new Cenario();
        using var sessao = CheckpointAnaliseM9.Iniciar(
            cena.Raiz, "perfil=A", cena.Checkpoints);
        sessao.Atualizar(25, 1000, forcar: true);
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            sessao.Atualizar(24, 1001));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            sessao.Atualizar(26, 999));
        Assert.AreEqual(25L, sessao.EstadoAtual.ArquivosLidos);
    }

    [TestMethod]
    public void EstadoFalhaEhPersistidoSemApresentarSucesso()
    {
        using var cena = new Cenario();
        using (var sessao = CheckpointAnaliseM9.Iniciar(
            cena.Raiz, "perfil=A", cena.Checkpoints))
        {
            sessao.MarcarFalha();
        }
        Assert.AreEqual("Falha",
            CheckpointAnaliseM9.Consultar(cena.Raiz, cena.Checkpoints)?.Estado);
    }

    private sealed class Cenario : IDisposable
    {
        private readonly string _base = Path.Combine(Path.GetTempPath(),
            "CLEAR_M9_CKPT_" + Guid.NewGuid().ToString("N"));
        public string Raiz { get; }
        public string Checkpoints => Path.Combine(_base, "Checkpoints");

        public Cenario()
        {
            Raiz = Directory.CreateDirectory(Path.Combine(_base, "Acervo")).FullName;
        }

        public void Dispose()
        {
            if (Directory.Exists(_base))
                Directory.Delete(_base, recursive: true);
        }
    }
}
