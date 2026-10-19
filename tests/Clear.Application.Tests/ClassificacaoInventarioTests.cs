using System.Runtime.CompilerServices;
using Clear.Application.Inventario;
using Clear.Domain.Analise;
using Clear.Domain.Arquivos;

namespace Clear.Application.Tests;

[TestClass]
public sealed class ClassificacaoInventarioTests
{
    [TestMethod]
    public async Task ContaCategoriasEmTodoOInventarioMesmoComAmostraLimitada()
    {
        var caso = new InventariarDiretorio(new FonteFalsa(
            new ArquivoInventariado(@"C:\Teste\normal.txt", 4),
            new ArquivoInventariado(@"C:\Teste\vazio.txt", 0),
            new ArquivoInventariado(@"C:\Teste\rascunho.tmp", 0),
            new ArquivoInventariado(@"C:\Teste\backup.BAK", 3)), limiteAmostra: 1);

        var resultado = await caso.ExecutarAsync(@"C:\Teste");

        Assert.AreEqual(4L, resultado.QuantidadeArquivos);
        Assert.AreEqual(7L, resultado.TotalBytes);
        Assert.AreEqual(3L, resultado.ArquivosComIndicios);
        Assert.AreEqual(2L, Contar(resultado, CategoriaAnalise.ArquivoVazio));
        Assert.AreEqual(2L, Contar(resultado, CategoriaAnalise.CopiaBackupOuTemporario));
        Assert.HasCount(1, resultado.ClassificacoesAmostra);
        Assert.HasCount(1, resultado.AmostraArquivos);
        Assert.IsTrue(resultado.AmostraLimitada);
    }

    [TestMethod]
    public async Task MesmaEntradaEmDuasCategoriasContaApenasUmaVezNoTotalDeArquivosComIndicios()
    {
        var caso = new InventariarDiretorio(new FonteFalsa(
            new ArquivoInventariado(@"C:\Teste\vazio.bak", 0)));

        var resultado = await caso.ExecutarAsync(@"C:\Teste");

        Assert.AreEqual(1L, resultado.ArquivosComIndicios);
        Assert.AreEqual(1L, Contar(resultado, CategoriaAnalise.ArquivoVazio));
        Assert.AreEqual(1L, Contar(resultado, CategoriaAnalise.CopiaBackupOuTemporario));
        Assert.HasCount(2, resultado.ClassificacoesAmostra[0].Evidencias);
    }

    [TestMethod]
    public async Task PermiteAmostraZeroComContagemIntegral()
    {
        var caso = new InventariarDiretorio(new FonteFalsa(
            new ArquivoInventariado(@"C:\Teste\vazio.txt", 0)), limiteAmostra: 0);

        var resultado = await caso.ExecutarAsync(@"C:\Teste");

        Assert.HasCount(0, resultado.ClassificacoesAmostra);
        Assert.AreEqual(1L, resultado.QuantidadeArquivos);
        Assert.AreEqual(1L, resultado.ArquivosComIndicios);
    }

    [TestMethod]
    public async Task FonteSemArquivosNaoProduzCategorias()
    {
        var resultado = await new InventariarDiretorio(new FonteFalsa())
            .ExecutarAsync(@"C:\Teste");

        Assert.AreEqual(0L, resultado.ArquivosComIndicios);
        Assert.HasCount(0, resultado.ContagensCategoria);
        Assert.HasCount(0, resultado.ClassificacoesAmostra);
    }

    private static long Contar(ResultadoInventario resultado, CategoriaAnalise categoria) =>
        resultado.ContagensCategoria.Single(item => item.Categoria == categoria).QuantidadeArquivos;

    private sealed class FonteFalsa(params ArquivoInventariado[] arquivos) : IFonteInventario
    {
        public async IAsyncEnumerable<ArquivoInventariado> EnumerarAsync(
            string diretorioRaiz,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            foreach (var arquivo in arquivos)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Yield();
                yield return arquivo;
            }
        }
    }
}
