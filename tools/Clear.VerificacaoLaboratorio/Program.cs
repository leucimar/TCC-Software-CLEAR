using System.Diagnostics;
using System.Text.Json;
using Microsoft.VisualBasic.FileIO;
using Clear.Application.Inventario;
using Clear.Application.Duplicatas;
using Clear.Application.Compatibilidade;
using Clear.Domain.Analise;
using Clear.Domain.Compatibilidade;
using Clear.Domain.Inventario;
using Clear.Infrastructure.Inventario;
using Clear.Infrastructure.Duplicatas;
using Clear.Infrastructure.Relatorios;
using Clear.Infrastructure.Historico;

var origem = Path.GetFullPath(args[0]);
var saida = Path.GetFullPath(args[1]);
Directory.CreateDirectory(saida);
var resultados = new List<object>();
int falhas = 0;
void Verificar(string nome, bool ok) { resultados.Add(new { nome, aprovado = ok }); if (!ok) falhas++; }
var exportadores = Enum.GetValues<FiltroLinhasRelatorio>().Select(f =>
    ExportadorRelatoriosM5.Iniciar(origem, saida, f)).ToArray();
var banco = Path.Combine(saida, "historico.sqlite3");
await using var sessao = await SessaoHistoricoSqliteM6.IniciarAsync(origem, banco, ParametrosHistoricoM6.SemFiltros);
try
{
    var config = new ConfiguracaoCompatibilidade(
        PerfilDestino.SharePointEmpresarial(SubtipoSharePoint.SharePointOnline),
        PerfisRecomendados.Para(AmbienteDestino.SharePointOneDriveEmpresarial));
    var observador = new ObservadorInventarioComposto(exportadores.Cast<IObservadorInventario>().Append(sessao).ToArray());
    var inv = await new InventariarDiretorio(new FonteInventarioSistemaArquivos(), limiteAmostra: 1000,
        configuracaoCompatibilidade: config, filtroPersonalizado: new FiltroTamanhoPersonalizado(1048576),
        observador: observador).ExecutarAsync(origem);
    Verificar("Inventário 467 arquivos / 43 pastas / 25.723.860 bytes", inv.QuantidadeArquivos == 467 && inv.QuantidadeDiretorios == 43 && inv.TotalBytes == 25723860);
    Verificar("24 incompatibilidades SharePoint", inv.ItensComIncompatibilidade == 24);
    Verificar("12 arquivos acima de 1 MiB", inv.ArquivosAcimaLimitePersonalizado == 12);
    ResultadoDuplicatas? referencia = null;
    string[]? gruposReferencia = null;
    var medicoes = new List<object>();
    foreach (var leitores in new[] { 1, 2, 4, 8 })
    {
        var tempo = Stopwatch.StartNew();
        var dup = await new DetectarDuplicatasExatas(new FonteInventarioSistemaArquivos(),
            new LeitorConteudoDuplicatasSistemaArquivos(), leitores).ExecutarAsync(origem);
        tempo.Stop();
        var grupos = dup.Grupos.Select(g => string.Join("|", g.Arquivos.Select(a => a.CaminhoCompleto).Order()))
            .Order().ToArray();
        Verificar($"Duplicatas com {leitores} leitor(es): 16 grupos e 111 membros", dup.Grupos.Count == 16 && dup.ArquivosEmGrupos == 111 && !dup.Incompleto);
        if (gruposReferencia is not null) Verificar($"Mesmos grupos com {leitores} leitores", grupos.SequenceEqual(gruposReferencia));
        gruposReferencia ??= grupos;
        referencia ??= dup;
        medicoes.Add(new { leitores, milissegundos = tempo.Elapsed.TotalMilliseconds,
            workingSetBytes = Process.GetCurrentProcess().WorkingSet64 });
    }
    var duplicados = referencia!.Grupos.SelectMany(g => g.Arquivos).Select(a => a.CaminhoCompleto).ToHashSet(StringComparer.OrdinalIgnoreCase);
    foreach (var filtro in Enum.GetValues<FiltroLinhasRelatorio>())
    {
        var gerados = await exportadores[(int)filtro].ConcluirAsync(inv, referencia,
            new[] { "ArquivoVazio", "CopiaBackupOuTemporario", "DuplicatasSHA256", "TamanhoPersonalizado" }, null, null);
        var linhas = LerCsv(gerados.InventarioCsv);
        var esperado = inv.AmostraAvaliacoes.Where(a =>
        {
            var geral = a.Entrada.Arquivo is {} arq && new ClassificadorArquivos().Classificar(arq).Categorias.Count > 0;
            var incompat = a.Verificacoes.Any(v => v.Estado == EstadoVerificacao.IncompatibilidadeDocumentada);
            return filtro switch
            {
                FiltroLinhasRelatorio.TodasAsEntradas => true,
                FiltroLinhasRelatorio.ApenasIndiciosGerais => geral,
                FiltroLinhasRelatorio.ApenasIncompatibilidades => incompat,
                FiltroLinhasRelatorio.ApenasTamanhoPersonalizado => a.AcimaDoLimitePersonalizado,
                _ => geral || incompat || a.AcimaDoLimitePersonalizado || duplicados.Contains(a.Entrada.CaminhoCompleto)
            };
        }).Select(a => a.Entrada.CaminhoCompleto).ToHashSet(StringComparer.OrdinalIgnoreCase);
        Verificar($"Filtro CSV {filtro}: todos os caminhos esperados", esperado.SetEquals(linhas.Select(l => l[2])) && linhas.Count == esperado.Count);
        Verificar($"Filtro CSV {filtro}: duplicatas nunca sem alertas", linhas.Where(l => duplicados.Contains(l[2])).All(l => l[7] == "Com indicios/alertas" && l[12] == "Sim"));
        Verificar($"Filtro CSV {filtro}: pastas sem alerta com situação própria", linhas.Where(l => l[0] == "Pasta" && l[6] == "" && l[10] == "").All(l => l[7] == "Pasta (nao aplicavel)"));
    }
    await sessao.ConcluirAsync(inv, referencia);
    var entradas = new List<EntradaHistoricaM6>();
    for (int i = 0; i < 3; i++) entradas.AddRange(await ConsultaHistoricoSqliteM6.ConsultarEntradasAsync(banco, sessao.IdExecucao, i));
    Verificar("Histórico simples recupera os 111 indicadores de duplicata", entradas.Count(e => e.EhDuplicataExata) == 111);
    var indisponiveis = entradas.Where(e => e.Tipo == "Arquivo").Take(205).Select(e => e.Caminho).ToHashSet(StringComparer.OrdinalIgnoreCase);
    var disponiveis = await ConsultaEntradasFiltradasM10.ConsultarAsync(banco, sessao.IdExecucao, new(), 0, caminhosIndisponiveis: indisponiveis);
    Verificar("Página disponível completa após excluir 205", disponiveis.Total == 262 && disponiveis.Entradas.Count == 200 && disponiveis.Entradas.All(e => !indisponiveis.Contains(e.Caminho)));
    await File.WriteAllTextAsync(Path.Combine(saida, "medicoes.json"), JsonSerializer.Serialize(medicoes, new JsonSerializerOptions { WriteIndented = true }));
}
finally { foreach (var exportador in exportadores) await exportador.DisposeAsync(); }
Verificar("Nenhum relatório temporário restante", Directory.GetFiles(saida, "*.part").Length == 0);
await File.WriteAllTextAsync(Path.Combine(saida, "resultados.json"), JsonSerializer.Serialize(new { falhas, resultados }, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine(JsonSerializer.Serialize(new { falhas, verificacoes = resultados.Count }));
return falhas == 0 ? 0 : 1;

static List<string[]> LerCsv(string caminho)
{
    using var leitor = new TextFieldParser(caminho) { TextFieldType = FieldType.Delimited, HasFieldsEnclosedInQuotes = true, TrimWhiteSpace = false };
    leitor.SetDelimiters(";");
    leitor.ReadFields();
    var linhas = new List<string[]>();
    while (!leitor.EndOfData) linhas.Add(leitor.ReadFields()!);
    return linhas;
}
