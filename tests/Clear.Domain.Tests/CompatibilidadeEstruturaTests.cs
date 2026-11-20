using Clear.Domain.Analise;
using Clear.Domain.Arquivos;
using Clear.Domain.Compatibilidade;
using Clear.Domain.Inventario;

namespace Clear.Domain.Tests;

[TestClass]
public sealed class CompatibilidadeEstruturaTests
{
    [TestMethod]
    public void PerfisSaoExclusivosETemSubtipoExplicito()
    {
        var azure = PerfilDestino.AzureFilesSmb();
        var sharepoint = PerfilDestino.SharePointEmpresarial(SubtipoSharePoint.SharePointOnline);
        var oneDrive = PerfilDestino.SharePointEmpresarial(SubtipoSharePoint.OneDriveForBusiness);

        Assert.AreEqual(AmbienteDestino.AzureFilesClassicoSmb, azure.Ambiente);
        Assert.IsNull(azure.Subtipo);
        Assert.AreEqual(SubtipoSharePoint.SharePointOnline, sharepoint.Subtipo);
        Assert.AreEqual(SubtipoSharePoint.OneDriveForBusiness, oneDrive.Subtipo);
        Assert.AreNotEqual(azure.Ambiente, sharepoint.Ambiente);
    }

    [TestMethod]
    public void RejeitaSubtipoInvalido()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            PerfilDestino.SharePointEmpresarial((SubtipoSharePoint)999));
    }

    [TestMethod]
    public void CatalogoContemTrezeDefinicoesComCodigosUnicos()
    {
        Assert.HasCount(13, CatalogoVerificacoes.Definicoes);
        Assert.AreEqual(13, CatalogoVerificacoes.Definicoes.Select(x => x.Codigo).Distinct().Count());
        Assert.AreEqual(5, CatalogoVerificacoes.Definicoes.Count(
            x => x.Ambiente == AmbienteDestino.AzureFilesClassicoSmb));
        Assert.AreEqual(8, CatalogoVerificacoes.Definicoes.Count(
            x => x.Ambiente == AmbienteDestino.SharePointOneDriveEmpresarial));
        Assert.IsNotNull(CatalogoVerificacoes.Encontrar("SPO-TAM-001"));
        Assert.IsNull(CatalogoVerificacoes.Encontrar("DESCONHECIDA"));
    }

    [TestMethod]
    public void NaoPermiteRegrasDeAmbientesDiferentes()
    {
        Assert.ThrowsExactly<ArgumentException>(() => new ConfiguracaoCompatibilidade(
            PerfilDestino.AzureFilesSmb(), new[] { "SPO-TAM-001" }));

        Assert.ThrowsExactly<ArgumentException>(() => new ConfiguracaoCompatibilidade(
            PerfilDestino.SharePointEmpresarial(SubtipoSharePoint.OneDriveForBusiness),
            new[] { "AZF-NOME-001" }));
    }

    [TestMethod]
    public void NaoPermiteVerificacaoSemDestino()
    {
        Assert.ThrowsExactly<ArgumentException>(() => new ConfiguracaoCompatibilidade(
            null, new[] { "AZF-NOME-001" }));
    }

    [TestMethod]
    public void PermiteNenhumPerfilSemNenhumaRegra()
    {
        var config = new ConfiguracaoCompatibilidade(null, Array.Empty<string>());
        Assert.IsNull(config.Perfil);
        Assert.IsFalse(config.HaVerificacoesHabilitadas);
    }

    [TestMethod]
    public void PermiteSelecionarIndividualmenteRegrasDoMesmoDestino()
    {
        var config = new ConfiguracaoCompatibilidade(
            PerfilDestino.AzureFilesSmb(), new[] { "AZF-NOME-001" });
        Assert.IsTrue(config.HaVerificacoesHabilitadas);
        Assert.HasCount(1, config.CodigosHabilitados);
        Assert.AreEqual("AZF-NOME-001", config.CodigosHabilitados[0]);
    }

    [TestMethod]
    public void NaoPermiteCodigoRepetidoOuDesconhecido()
    {
        Assert.ThrowsExactly<ArgumentException>(() => new ConfiguracaoCompatibilidade(
            PerfilDestino.AzureFilesSmb(), new[] { "AZF-TAM-001", "AZF-TAM-001" }));
        Assert.ThrowsExactly<ArgumentException>(() => new ConfiguracaoCompatibilidade(
            PerfilDestino.AzureFilesSmb(), new[] { "AZF-TAM-999" }));
    }

    [TestMethod]
    public void ConfiguracaoCongelaListaOriginal()
    {
        var codigos = new List<string> { "AZF-NOME-001" };
        var configuracao = new ConfiguracaoCompatibilidade(PerfilDestino.AzureFilesSmb(), codigos);
        codigos.Clear();
        Assert.HasCount(1, configuracao.CodigosHabilitados);
    }

    [TestMethod]
    public void ResumoVazioNaoDeclaraAusenciaDeIncompatibilidades()
    {
        var resumo = new ResumoVerificacoes(Array.Empty<ResultadoVerificacao>());
        Assert.IsFalse(resumo.AlgumaVerificacaoExecutada);
        Assert.IsFalse(resumo.ApenasSemIncompatibilidadeDetectada);
    }

    [TestMethod]
    public void ResultadoInconclusivoImpedeConclusaoGlobalFavoravel()
    {
        var entrada = EntradaInventariada.DeDiretorio(@"C:\Teste\Subpasta");
        var resumo = new ResumoVerificacoes(new[]
        {
            new ResultadoVerificacao("SPO-NOME-001", entrada,
                EstadoVerificacao.SemIncompatibilidadeDetectada, "Nenhuma pontuação proibida identificada."),
            new ResultadoVerificacao("SPO-NOME-002", entrada,
                EstadoVerificacao.NaoAvaliado, "Dado indisponível.")
        });

        Assert.IsTrue(resumo.AlgumaVerificacaoExecutada);
        Assert.IsFalse(resumo.ApenasSemIncompatibilidadeDetectada);
    }

    [TestMethod]
    public void SomenteNaoAplicavelOuDesabilitadoNaoEquivaleATesteExecutado()
    {
        var entrada = EntradaInventariada.DeDiretorio(@"C:\Teste\Subpasta");
        var resumo = new ResumoVerificacoes(new[]
        {
            new ResultadoVerificacao("SPO-NOME-001", entrada, EstadoVerificacao.Desabilitada, "Operador desmarcou."),
            new ResultadoVerificacao("SPO-NOME-002", entrada, EstadoVerificacao.NaoAplicavel, "Escopo diferente.")
        });
        Assert.IsFalse(resumo.AlgumaVerificacaoExecutada);
        Assert.IsFalse(resumo.ApenasSemIncompatibilidadeDetectada);
    }

    [TestMethod]
    public void ResultadoFavoravelExigeUmaOuMaisRegrasExecutadasETodasFavoraveis()
    {
        var entrada = EntradaInventariada.DeDiretorio(@"C:\Teste\Subpasta");
        var resumo = new ResumoVerificacoes(new[]
        {
            new ResultadoVerificacao("AZF-NOME-001", entrada,
                EstadoVerificacao.SemIncompatibilidadeDetectada,
                "Sem caractere proibido pela regra, não é garantia de migração.")
        });
        Assert.IsTrue(resumo.AlgumaVerificacaoExecutada);
        Assert.IsTrue(resumo.ApenasSemIncompatibilidadeDetectada);
    }

    [TestMethod]
    public void LimitePersonalizadoEhIndependenteEComparacaoEhEstrita()
    {
        var filtro = new FiltroTamanhoPersonalizado(1024);
        Assert.IsFalse(filtro.Excede(0));
        Assert.IsFalse(filtro.Excede(1024));
        Assert.IsTrue(filtro.Excede(1025));
        Assert.AreEqual(1024L, filtro.LimiteBytes);
    }

    [TestMethod]
    public void LimitePersonalizadoRejeitaDadosInvalidos()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new FiltroTamanhoPersonalizado(0));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new FiltroTamanhoPersonalizado(-1));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new FiltroTamanhoPersonalizado(10).Excede(-1));
    }

    [TestMethod]
    public void EntradaDistingueDiretorioDeArquivoSemInventarTamanho()
    {
        var arquivo = EntradaInventariada.DeArquivo(new ArquivoInventariado(@"C:\Teste\a.txt", 3));
        var pasta = EntradaInventariada.DeDiretorio(@"C:\Teste\vazio.bak");

        Assert.AreEqual(TipoEntradaInventario.Arquivo, arquivo.Tipo);
        Assert.AreEqual(3L, arquivo.TamanhoBytes);
        Assert.AreEqual(TipoEntradaInventario.Diretorio, pasta.Tipo);
        Assert.IsNull(pasta.TamanhoBytes);
        Assert.IsNull(pasta.Arquivo);
    }

    [TestMethod]
    public void EntradaRejeitaNulosECaminhoVazio()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => EntradaInventariada.DeArquivo(null!));
        Assert.ThrowsExactly<ArgumentException>(() => EntradaInventariada.DeDiretorio(" "));
    }
}
