using Clear.Application.Inventario;
using Clear.Domain.Analise;
using Clear.Domain.Compatibilidade;
using Clear.Infrastructure.Inventario;

namespace Clear.Infrastructure.Tests;

[TestClass]
public sealed class CompatibilidadeIntegracaoTests
{
    [TestMethod]
    public async Task NomeDePastaVtiEFiltroPersonalizadoSemAlteracoesNosArquivos()
    {
        var raiz = Path.Combine(Path.GetTempPath(), "CLEAR_3B_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(raiz);
        try
        {
            var pasta = Directory.CreateDirectory(Path.Combine(raiz, "Projeto_vti_docs"));
            var arquivo = Path.Combine(pasta.FullName, "relatorio.txt");
            var curto = Path.Combine(raiz, "pequeno.txt");
            await File.WriteAllBytesAsync(arquivo, new byte[] { 1, 2, 3, 4 });
            await File.WriteAllBytesAsync(curto, new byte[] { 5 });
            var antes = await File.ReadAllBytesAsync(arquivo);
            var modificacao = File.GetLastWriteTimeUtc(arquivo);

            var config = new ConfiguracaoCompatibilidade(
                PerfilDestino.SharePointEmpresarial(SubtipoSharePoint.OneDriveForBusiness),
                new[] { "SPO-NOME-004", "SPO-TAM-001" });
            var resultado = await new InventariarDiretorio(
                new FonteInventarioSistemaArquivos(),
                configuracaoCompatibilidade: config,
                filtroPersonalizado: new FiltroTamanhoPersonalizado(2))
                .ExecutarAsync(raiz);

            Assert.AreEqual(2L, resultado.QuantidadeArquivos);
            Assert.AreEqual(1L, resultado.QuantidadeDiretorios);
            Assert.AreEqual(5L, resultado.TotalBytes);
            Assert.AreEqual(1L, resultado.ItensComIncompatibilidade);
            Assert.AreEqual(1L, resultado.ArquivosAcimaLimitePersonalizado);
            Assert.AreEqual(0L, resultado.TotalOcorrencias);
            Assert.AreEqual(1L, resultado.ContagensVerificacoes.Single(x =>
                x.CodigoRegra == "SPO-NOME-004" &&
                x.Estado == EstadoVerificacao.IncompatibilidadeDocumentada).Quantidade);
            CollectionAssert.AreEqual(antes, await File.ReadAllBytesAsync(arquivo));
            Assert.AreEqual(modificacao, File.GetLastWriteTimeUtc(arquivo));
        }
        finally
        {
            if (Directory.Exists(raiz))
                Directory.Delete(raiz, recursive: true);
        }
    }
}
