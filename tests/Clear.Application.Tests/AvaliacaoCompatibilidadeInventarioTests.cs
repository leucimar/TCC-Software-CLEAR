using System.Runtime.CompilerServices;
using Clear.Application.Inventario;
using Clear.Domain.Analise;
using Clear.Domain.Arquivos;
using Clear.Domain.Compatibilidade;
using Clear.Domain.Inventario;

namespace Clear.Application.Tests;

[TestClass]
public sealed class AvaliacaoCompatibilidadeInventarioTests
{
    private static readonly ConfiguracaoCompatibilidade Azure = new(
        PerfilDestino.AzureFilesSmb(), new[] { "AZF-NOME-001", "AZF-TAM-001" });

    [TestMethod]
    public async Task ContaIncompatibilidadesDeArquivosEPastasEmUmaSoTravessia()
    {
        var fonte = new FonteMista(
            EntradaInventariada.DeArquivo(new ArquivoInventariado(@"C:\Teste\normal.txt", 2)),
            EntradaInventariada.DeArquivo(new ArquivoInventariado(@"C:\Teste\erro?.tmp",
                AvaliadorCompatibilidade.LimiteAzureFilesBytes + 1)),
            EntradaInventariada.DeDiretorio(@"C:\Teste\Pasta?"));

        var resultado = await new InventariarDiretorio(
            fonte, limiteAmostra: 1, configuracaoCompatibilidade: Azure,
            filtroPersonalizado: new FiltroTamanhoPersonalizado(3)).ExecutarAsync(@"C:\Teste");

        Assert.AreEqual(1, fonte.Chamadas);
        Assert.AreEqual(2L, resultado.QuantidadeArquivos);
        Assert.AreEqual(1L, resultado.QuantidadeDiretorios);
        Assert.AreEqual(2L, resultado.ItensComIncompatibilidade);
        Assert.AreEqual(1L, resultado.ArquivosAcimaLimitePersonalizado);
        Assert.AreEqual(2L, Quantidade(resultado, "AZF-NOME-001",
            EstadoVerificacao.IncompatibilidadeDocumentada));
        Assert.AreEqual(1L, Quantidade(resultado, "AZF-TAM-001",
            EstadoVerificacao.IncompatibilidadeDocumentada));
        Assert.AreEqual(1L, Quantidade(resultado, "AZF-TAM-001",
            EstadoVerificacao.NaoAplicavel));
        Assert.HasCount(1, resultado.AmostraAvaliacoes);
        Assert.AreEqual(1L, resultado.ArquivosComIndicios);
        Assert.IsFalse(resultado.ApenasSemIncompatibilidadeDetectada);
    }

    [TestMethod]
    public async Task FiltroPersonalizadoFuncionaSemDestino()
    {
        var fonte = new FonteMista(
            EntradaInventariada.DeArquivo(new ArquivoInventariado(@"C:\Teste\pequeno.txt", 2)),
            EntradaInventariada.DeArquivo(new ArquivoInventariado(@"C:\Teste\grande.txt", 10)),
            EntradaInventariada.DeDiretorio(@"C:\Teste\pasta?"));

        var resultado = await new InventariarDiretorio(
            fonte, filtroPersonalizado: new FiltroTamanhoPersonalizado(2))
            .ExecutarAsync(@"C:\Teste");

        Assert.AreEqual(1L, resultado.ArquivosAcimaLimitePersonalizado);
        Assert.AreEqual(0L, resultado.ItensComIncompatibilidade);
        Assert.AreEqual(0L, resultado.AvaliacoesExecutadas);
        Assert.IsFalse(resultado.ApenasSemIncompatibilidadeDetectada);
        Assert.HasCount(0, resultado.ContagensVerificacoes);
    }

    [TestMethod]
    public async Task PerfilSemChecksNaoEmiteSinalFalsoDeCompatibilidade()
    {
        var configuracao = new ConfiguracaoCompatibilidade(
            PerfilDestino.AzureFilesSmb(), Array.Empty<string>());
        var resultado = await new InventariarDiretorio(
            new FonteMista(EntradaInventariada.DeArquivo(
                new ArquivoInventariado(@"C:\Teste\nome?.txt", 1))),
            configuracaoCompatibilidade: configuracao).ExecutarAsync(@"C:\Teste");

        Assert.AreEqual(0L, resultado.ItensComIncompatibilidade);
        Assert.AreEqual(0L, resultado.AvaliacoesExecutadas);
        Assert.IsFalse(resultado.ApenasSemIncompatibilidadeDetectada);
        Assert.AreEqual(7L, resultado.ContagensVerificacoes
            .Where(x => x.Estado == EstadoVerificacao.Desabilitada)
            .Sum(x => x.Quantidade));
    }

    [TestMethod]
    public async Task AdvertenciaDeTipoNaoContaComoIncompatibilidadeNemComoResultadoSemAlertas()
    {
        var configuracao = new ConfiguracaoCompatibilidade(
            PerfilDestino.SharePointEmpresarial(SubtipoSharePoint.SharePointOnline), ["SPO-TIPO-001"]);
        var resultado = await new InventariarDiretorio(
            new FonteMista(EntradaInventariada.DeArquivo(new ArquivoInventariado(@"C:\Teste\dado.tmp", 10))),
            configuracaoCompatibilidade: configuracao).ExecutarAsync(@"C:\Teste");

        Assert.AreEqual(1L, Quantidade(resultado, "SPO-TIPO-001", EstadoVerificacao.Alerta));
        Assert.AreEqual(0L, resultado.ItensComIncompatibilidade);
        Assert.IsFalse(resultado.ApenasSemIncompatibilidadeDetectada);
    }

    [TestMethod]
    public async Task VerificacaoDeTamanhoSoPraArquivosNaoParaPastas()
    {
        var config = new ConfiguracaoCompatibilidade(
            PerfilDestino.AzureFilesSmb(), new[] { "AZF-TAM-001" });
        var resultado = await new InventariarDiretorio(
            new FonteMista(EntradaInventariada.DeDiretorio(@"C:\Teste\pasta")),
            configuracaoCompatibilidade: config).ExecutarAsync(@"C:\Teste");

        Assert.AreEqual(0L, resultado.AvaliacoesExecutadas);
        Assert.AreEqual(1L, Quantidade(resultado, "AZF-TAM-001", EstadoVerificacao.NaoAplicavel));
        Assert.IsFalse(resultado.ApenasSemIncompatibilidadeDetectada);
    }

    [TestMethod]
    public async Task ContagensContinuaIntegraisAlemDaAmostra()
    {
        var entradas = Enumerable.Range(1, 8).Select(i =>
            EntradaInventariada.DeArquivo(new ArquivoInventariado(
                @"C:\Teste\item?.txt" + i, 4))).ToArray();
        var config = new ConfiguracaoCompatibilidade(
            PerfilDestino.AzureFilesSmb(), new[] { "AZF-NOME-001" });
        var resultado = await new InventariarDiretorio(
            new FonteMista(entradas), limiteAmostra: 2,
            configuracaoCompatibilidade: config,
            filtroPersonalizado: new FiltroTamanhoPersonalizado(3))
            .ExecutarAsync(@"C:\Teste");

        Assert.AreEqual(8L, resultado.ItensComIncompatibilidade);
        Assert.AreEqual(8L, resultado.ArquivosAcimaLimitePersonalizado);
        Assert.HasCount(2, resultado.AmostraAvaliacoes);
        Assert.AreEqual(8L, Quantidade(resultado, "AZF-NOME-001",
            EstadoVerificacao.IncompatibilidadeDocumentada));
    }

    [TestMethod]
    public async Task RegrasDeOutroAmbienteNuncaSaoAplicadas()
    {
        var config = new ConfiguracaoCompatibilidade(
            PerfilDestino.SharePointEmpresarial(SubtipoSharePoint.OneDriveForBusiness),
            new[] { "SPO-NOME-004" });
        var resultado = await new InventariarDiretorio(
            new FonteMista(EntradaInventariada.DeDiretorio(@"C:\Teste\_vti_")),
            configuracaoCompatibilidade: config).ExecutarAsync(@"C:\Teste");

        Assert.AreEqual(1L, resultado.ItensComIncompatibilidade);
        Assert.IsFalse(resultado.ContagensVerificacoes
            .Any(x => x.CodigoRegra.StartsWith("AZF-", StringComparison.Ordinal)));
        Assert.AreEqual(1L, Quantidade(resultado, "SPO-NOME-004",
            EstadoVerificacao.IncompatibilidadeDocumentada));
    }

    [TestMethod]
    public async Task ArquivoVazioPodeTerIndicioGeralEIncompatibilidadeDeDestino()
    {
        var config = new ConfiguracaoCompatibilidade(
            PerfilDestino.SharePointEmpresarial(SubtipoSharePoint.SharePointOnline),
            new[] { "SPO-NOME-004" });
        var resultado = await new InventariarDiretorio(
            new FonteMista(EntradaInventariada.DeArquivo(
                new ArquivoInventariado(@"C:\Teste\_vti_.txt", 0))),
            configuracaoCompatibilidade: config).ExecutarAsync(@"C:\Teste");
        Assert.AreEqual(1L, resultado.ArquivosComIndicios);
        Assert.AreEqual(1L, resultado.ItensComIncompatibilidade);
    }

    [TestMethod]
    public async Task PositivoLocalNaoEhGarantiaDeMigração()
    {
        var resultado = await new InventariarDiretorio(
            new FonteMista(EntradaInventariada.DeArquivo(
                new ArquivoInventariado(@"C:\Teste\normal.txt", 3))),
            configuracaoCompatibilidade: Azure).ExecutarAsync(@"C:\Teste");
        Assert.IsTrue(resultado.ApenasSemIncompatibilidadeDetectada);
        Assert.AreEqual(0L, resultado.ItensComIncompatibilidade);
        Assert.AreEqual(2L, resultado.AvaliacoesExecutadas);
    }

    private static long Quantidade(ResultadoInventario r, string codigo, EstadoVerificacao estado) =>
        r.ContagensVerificacoes.Single(x => x.CodigoRegra == codigo && x.Estado == estado).Quantidade;

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
}
