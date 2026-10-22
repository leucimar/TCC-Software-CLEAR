using System.Runtime.CompilerServices;
using Clear.Application.Inventario;
using Clear.Domain.Analise;
using Clear.Domain.Arquivos;

namespace Clear.Application.Tests;

[TestClass]
public sealed class InventarioTemporalTests
{
    private static readonly DateTimeOffset Referencia = new(2026, 10, 14, 20, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public async Task ContabilizaSinaisTemporaisMesmoForaDaAmostra()
    {
        var fonte = new FonteFalsa(
            Arquivo("atual.txt", 7, -1),
            Arquivo("antigo.txt", 3, -500),
            Arquivo("antigo.tmp", 0, -500),
            Arquivo("recente.bak", 8, -10));
        var classificador = ClassificadorArquivos.CriarComSinalTemporal(365, Referencia);
        var resultado = await new InventariarDiretorio(fonte, limiteAmostra: 1,
            classificador: classificador).ExecutarAsync(@"C:\Teste");

        Assert.AreEqual(4L, resultado.QuantidadeArquivos);
        Assert.AreEqual(18L, resultado.TotalBytes);
        Assert.AreEqual(3L, resultado.ArquivosComIndicios);
        Assert.AreEqual(2L, Contagem(resultado, CategoriaAnalise.SinalTemporal));
        Assert.AreEqual(1L, Contagem(resultado, CategoriaAnalise.ArquivoVazio));
        Assert.AreEqual(2L, Contagem(resultado, CategoriaAnalise.CopiaBackupOuTemporario));
        Assert.HasCount(1, resultado.ClassificacoesAmostra);
        Assert.IsTrue(resultado.AmostraLimitada);
    }

    [TestMethod]
    public async Task DesativadoMantemComportamentoDaParte1()
    {
        var resultado = await new InventariarDiretorio(
            new FonteFalsa(Arquivo("antigo.txt", 3, -500)))
            .ExecutarAsync(@"C:\Teste");

        Assert.AreEqual(0L, resultado.ArquivosComIndicios);
        Assert.HasCount(0, resultado.ContagensCategoria);
    }

    [TestMethod]
    public async Task AumentarLimiteDeDiasReduzSinaisMasNaoAlteraOutrasCategorias()
    {
        var fonte1 = new FonteFalsa(Arquivo("copia.tmp", 0, -200));
        var fonte2 = new FonteFalsa(Arquivo("copia.tmp", 0, -200));

        var regra100 = ClassificadorArquivos.CriarComSinalTemporal(100, Referencia);
        var regra365 = ClassificadorArquivos.CriarComSinalTemporal(365, Referencia);

        var resultado100 = await new InventariarDiretorio(fonte1, classificador: regra100)
            .ExecutarAsync(@"C:\Teste");
        var resultado365 = await new InventariarDiretorio(fonte2, classificador: regra365)
            .ExecutarAsync(@"C:\Teste");

        Assert.HasCount(3, resultado100.ClassificacoesAmostra[0].Categorias);
        Assert.HasCount(2, resultado365.ClassificacoesAmostra[0].Categorias);
        Assert.AreEqual(1L, resultado100.ArquivosComIndicios);
        Assert.AreEqual(1L, resultado365.ArquivosComIndicios);
    }

    private static long Contagem(ResultadoInventario resultado, CategoriaAnalise categoria) =>
        resultado.ContagensCategoria.Single(item => item.Categoria == categoria).QuantidadeArquivos;

    private static ArquivoInventariado Arquivo(string nome, long bytes, int dias) =>
        new(@"C:\Teste\" + nome, bytes, Referencia.AddDays(dias), Referencia.AddDays(dias));

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
