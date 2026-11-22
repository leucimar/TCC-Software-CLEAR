using System.Runtime.CompilerServices;
using Clear.Application.Duplicatas;
using Clear.Application.Inventario;
using Clear.Domain.Arquivos;

namespace Clear.Application.Tests;

[TestClass]
public sealed class FilaGlobalDuplicatasTests
{
    [TestMethod]
    public async Task WorkersOcupamTamanhosDiferentesSemBarreira()
    {
        var chegaram = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var liberar = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int ativos = 0;
        var leitor = new Leitor(async (f, ct) => {
            if (Interlocked.Increment(ref ativos) == 4) chegaram.TrySetResult();
            await liberar.Task.WaitAsync(ct);return f.CaminhoCompleto;
        });
        var tarefa = new DetectarDuplicatasExatas(new Fonte([
            new("a",10),new("b",10),new("c",11),new("d",11)]),leitor,4).ExecutarAsync("C:\\Teste");
        try { await chegaram.Task.WaitAsync(TimeSpan.FromSeconds(5)); }
        finally { liberar.TrySetResult();await tarefa; }
        Assert.AreEqual(4,ativos);
    }

    [TestMethod]
    public async Task SlotLiberadoIniciaProximoAntesDoArquivoLentoTerminar()
    {
        var liberar = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var terceiro = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var leitor = new Leitor(async (f, ct) => {
            if (f.CaminhoCompleto == "a") await liberar.Task.WaitAsync(ct);
            if (f.CaminhoCompleto == "c") terceiro.TrySetResult();
            return f.CaminhoCompleto;
        });
        var tarefa = new DetectarDuplicatasExatas(new Fonte([
            new("a",10),new("b",10),new("c",10),new("d",10)]),leitor,2).ExecutarAsync("C:\\Teste");
        try { await terceiro.Task.WaitAsync(TimeSpan.FromSeconds(5)); }
        finally { liberar.TrySetResult();await tarefa; }
    }

    [TestMethod]
    public async Task ProgressoFinalIncluiFalhasSemContarHashInvalido()
    {
        var eventos = new Progresso();
        var leitor = new Leitor((f,ct) => f.CaminhoCompleto == "b"
            ? Task.FromException<string>(new IOException("falha controlada")) : Task.FromResult(f.CaminhoCompleto));
        var r = await new DetectarDuplicatasExatas(new Fonte([
            new("a",10),new("b",10),new("c",11),new("d",11)]),leitor,4)
            .ExecutarAsync("C:\\Teste",eventos);
        var ultimo = eventos.Itens.Last();
        Assert.AreEqual(4L,ultimo.CandidatosProcessados);
        Assert.AreEqual(4L,ultimo.ArquivosCandidatos);
        Assert.AreEqual(3L,ultimo.CandidatosComHash);
        Assert.AreEqual(1L,ultimo.Falhas);
        Assert.AreEqual(3L,r.ArquivosComHash);
        Assert.IsTrue(r.Incompleto);
        CollectionAssert.AreEqual(eventos.Itens.Select(x=>x.CandidatosProcessados).Order().ToArray(),
            eventos.Itens.Select(x=>x.CandidatosProcessados).ToArray());
    }

    [TestMethod]
    public async Task SemCandidatosPublicaConclusaoSemLeitura()
    {
        var p=new Progresso();
        await new DetectarDuplicatasExatas(new Fonte([new("a",10)]),
            new Leitor((_,_)=>throw new InvalidOperationException())).ExecutarAsync("C:\\Teste",p);
        Assert.AreEqual(0L,p.Itens.Last().CandidatosProcessados);
        Assert.AreEqual(0L,p.Itens.Last().ArquivosCandidatos);
    }

    private sealed class Progresso:IProgress<ProgressoDuplicatas> {
        public List<ProgressoDuplicatas> Itens {get;}=[];
        public void Report(ProgressoDuplicatas p)=>Itens.Add(p);
    }
    private sealed class Fonte(ArquivoInventariado[] arquivos):IFonteInventario {
        public async IAsyncEnumerable<ArquivoInventariado> EnumerarAsync(string raiz,
            [EnumeratorCancellation] CancellationToken ct=default) {
            await Task.Yield();foreach(var f in arquivos){ct.ThrowIfCancellationRequested();yield return f;}
        }
    }
    private sealed class Leitor(Func<ArquivoInventariado,CancellationToken,Task<string>> hash):ILeitorConteudoDuplicatas {
        public Task<string> CalcularSha256Async(ArquivoInventariado a,CancellationToken ct=default)=>hash(a,ct);
        public Task<bool> CompararBytesAsync(ArquivoInventariado a,ArquivoInventariado b,CancellationToken ct=default)=>Task.FromResult(false);
    }
}
