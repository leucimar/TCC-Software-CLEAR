using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using Clear.Application.Duplicatas;
using Clear.Application.Inventario;
using Clear.Infrastructure.Duplicatas;
using Clear.Infrastructure.Inventario;

// Experimento reprodutível do M9: somente acervo sintético recém-gerado.
// Este executável não acessa dados de usuários nem executa movimentações.
var parametros = LerParametros(args);
var destino = Directory.Exists(@"C:\Temp") ? @"C:\Temp" : Path.GetTempPath();
var pasta = Path.Combine(destino, "CLEAR_M9_Benchmark_" + Guid.NewGuid().ToString("N"));
var acervo = Path.Combine(pasta, "Acervo");
Directory.CreateDirectory(acervo);

Console.WriteLine("Criando acervo sintético de " + parametros.Arquivos + " arquivos em " + acervo);
for (int i = 0; i < parametros.Arquivos; i++)
{
    // Arquivos do mesmo grupo possuem exatamente o mesmo conteúdo.
    // Dados e grupos são estáveis entre execuções (semente documentada).
    byte[] conteudo = new byte[parametros.BytesPorArquivo];
    new Random(5721 + i % Math.Max(1, parametros.Arquivos / 4)).NextBytes(conteudo);
    await File.WriteAllBytesAsync(
        Path.Combine(acervo, "arquivo_" + i.ToString("D6") + ".bin"), conteudo);
}

var amostras = new List<object>();
var processo = Process.GetCurrentProcess();
// Rodadas intercaladas para mitigar diferenças óbvias de aquecimento/caching.
// Mesmo assim, o benchmark não controla a cache do SO/antivírus.
for (int rodada = 1; rodada <= parametros.Repeticoes; rodada++)
{
    int[] ordem = rodada % 2 == 0 ? [4, 2, 1] : [1, 2, 4];
    foreach (int concorrencia in ordem)
    {
        var processoAntes = processo.TotalProcessorTime;
        var alocadosAntes = GC.GetTotalAllocatedBytes(precise: true);
        var geral = Stopwatch.StartNew();

        var inventarioStopwatch = Stopwatch.StartNew();
        var inventario = await new InventariarDiretorio(
            new FonteInventarioSistemaArquivos(), limiteAmostra: 0)
            .ExecutarAsync(acervo);
        inventarioStopwatch.Stop();

        var duplicatasStopwatch = Stopwatch.StartNew();
        var duplicatas = await new DetectarDuplicatasExatas(
            new FonteInventarioSistemaArquivos(),
            new LeitorConteudoDuplicatasSistemaArquivos(),
            maximoHashesSimultaneos: concorrencia).ExecutarAsync(acervo);
        duplicatasStopwatch.Stop();
        geral.Stop();
        processo.Refresh();

        var leitura = new
        {
            Rodada = rodada,
            ConcorrenciaHashes = concorrencia,
            DuracaoInventarioMs = inventarioStopwatch.Elapsed.TotalMilliseconds,
            DuracaoDuplicatasMs = duplicatasStopwatch.Elapsed.TotalMilliseconds,
            DuracaoTotalMs = geral.Elapsed.TotalMilliseconds,
            ArquivosInventariados = inventario.QuantidadeArquivos,
            SubpastasInventariadas = inventario.QuantidadeDiretorios,
            VolumeInventariadoBytes = inventario.TotalBytes,
            ArquivosPorSegundoInventario = inventarioStopwatch.Elapsed.TotalSeconds <= 0
                ? 0 : inventario.QuantidadeArquivos / inventarioStopwatch.Elapsed.TotalSeconds,
            MiBInventariadosPorSegundo = inventarioStopwatch.Elapsed.TotalSeconds <= 0
                ? 0 : inventario.TotalBytes / 1048576d / inventarioStopwatch.Elapsed.TotalSeconds,
            HashesCalculados = duplicatas.ArquivosComHash,
            GruposDuplicadosConfirmados = duplicatas.Grupos.Count,
            BytesPotencialmenteRedundantes = duplicatas.BytesPotencialmenteRedundantes,
            OcorrenciasInventario = inventario.TotalOcorrencias,
            OcorrenciasDuplicatas = duplicatas.TotalOcorrencias,
            CpuDoProcessoMs = (processo.TotalProcessorTime - processoAntes).TotalMilliseconds,
            AlocacoesManagedObservadasBytes = GC.GetTotalAllocatedBytes(precise: true)
                - alocadosAntes,
            WorkingSetAoFinalBytes = processo.WorkingSet64,
            PicoWorkingSetDoProcessoAteAgoraBytes = processo.PeakWorkingSet64
        };
        amostras.Add(leitura);
        Console.WriteLine($"Rodada {rodada}, SHA x{concorrencia}: " +
            $"inventário {leitura.DuracaoInventarioMs:F1} ms, " +
            $"duplicatas {leitura.DuracaoDuplicatasMs:F1} ms, " +
            $"grupos {leitura.GruposDuplicadosConfirmados}");
    }
}

var registro = new
{
    Esquema = "CLEAR-M9-BENCH-1",
    GeradoUtc = DateTimeOffset.UtcNow,
    Projeto = "CLEAR — M9 Parte 1",
    DadosSinteticos = new
    {
        Semente = 5721, parametros.Arquivos, parametros.BytesPorArquivo,
        parametros.Repeticoes, Raiz = acervo,
        Distintos = Math.Max(1, parametros.Arquivos / 4),
        Padrao = "Mesmo conteúdo para arquivos com o mesmo resto por Distintos"
    },
    Ambiente = new
    {
        SO = RuntimeInformation.OSDescription, Arquitetura = RuntimeInformation.ProcessArchitecture.ToString(),
        DotNet = RuntimeInformation.FrameworkDescription,
        ProcessadoresLogicos = Environment.ProcessorCount,
        OrcamentoDeMemoriaGCBytes = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes
    },
    Metodologia = new
    {
        Concorrencias = new[] { 1, 2, 4 },
        RepeticoesPorConcorrencia = parametros.Repeticoes,
        Aquecimento = "Sem rodada exclusiva de aquecimento; alternância de ordem entre rodadas",
        Inventario = "Somente metadados, amostra de 0 arquivos",
        Duplicatas = "Varredura separada, SHA-256 e comparação byte a byte",
        Limites = "SO, cache de disco/antivírus, carga externa e filesystem não controlados",
        IONaoMedido = "MiB inventariados/s não equivale a throughput de leitura de disco",
        Validade = "Dados brutos observados; sem conclusão de ganho antes de análise estatística"
    },
    Amostras = amostras
};
var destinoJson = Path.Combine(pasta, "medicoes_m9.json");
await File.WriteAllTextAsync(destinoJson,
    JsonSerializer.Serialize(registro, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine("Resultado completo em: " + destinoJson);
Console.WriteLine("Acervo sintético mantido em: " + acervo);

static (int Arquivos, int BytesPorArquivo, int Repeticoes) LerParametros(string[] argumentos)
{
    int arquivos = 500, bytes = 4096, repeticoes = 3;
    for (int i = 0; i < argumentos.Length; i += 2)
    {
        if (i + 1 >= argumentos.Length
            || !int.TryParse(argumentos[i + 1], out int valor))
            throw new ArgumentException("Uso: --arquivos 500 --bytes 4096 --repeticoes 3");
        switch (argumentos[i])
        {
            case "--arquivos": arquivos = valor; break;
            case "--bytes": bytes = valor; break;
            case "--repeticoes": repeticoes = valor; break;
            default: throw new ArgumentException("Parâmetro desconhecido: " + argumentos[i]);
        }
    }
    if (arquivos is < 8 or > 10000 || bytes is < 0 or > 65536
        || repeticoes is < 1 or > 10)
        throw new ArgumentOutOfRangeException(nameof(argumentos),
            "Limites: 8–10000 arquivos, 0–65536 bytes, 1–10 repetições.");
    return (arquivos, bytes, repeticoes);
}
