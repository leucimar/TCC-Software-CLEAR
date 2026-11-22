using System.Security.AccessControl;
using System.Security.Principal;
using Clear.Infrastructure.Arquivos;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using Clear.Application.Duplicatas;
using Clear.Application.Inventario;
using Clear.Application.Metricas;
using Clear.Domain.Analise;
using Clear.Domain.Arquivos;
using Clear.Domain.Compatibilidade;
using Clear.Domain.Inventario;
using Clear.Infrastructure.Duplicatas;
using Clear.Infrastructure.Execucao;
using Clear.Infrastructure.Historico;
using Clear.Infrastructure.Inventario;
using Clear.Infrastructure.Metricas;
using Clear.Infrastructure.Quarentena;
using Clear.Infrastructure.Relatorios;

var basePath=Path.GetFullPath(args[0]);
if(!string.Equals(basePath,@"D:\TCC_Leucimar\Base_Dados\Validacao_Controlada_CLEAR",StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Only controlled corpus is authorized.");
var original=Path.Combine(basePath,"02_original");
var output=Path.Combine(basePath,"08_relatorios","CLEARv1_0_"+DateTime.UtcNow.ToString("yyyyMMdd_HHmmss"));Directory.CreateDirectory(output);
using var preservacao=new SessaoPreservacaoAcesso(PoliticaSemPreservacao.NaoLerConteudo,Path.Combine(output,"preservacao_execucao.jsonl"),"Validacao controlada CLEARv1.0");
var work=Path.Combine(basePath,"03_experimental",Path.GetFileName(output),"AcervoTeste");
var oracle=JsonDocument.Parse(File.ReadAllText(Path.Combine(basePath,"04_manifestos","oraculo.json"))).RootElement;
var expected=File.ReadLines(Path.Combine(basePath,"04_manifestos","arquivos_controlados.jsonl")).Select(s=>JsonDocument.Parse(s).RootElement.Clone()).ToArray();
var reference=DateTimeOffset.Parse(oracle.GetProperty("reference_utc").GetString()!);
var customLimit=oracle.GetProperty("custom_limit_bytes").GetInt64();
var checks=new List<object>();var timing=new List<object>();var signatures=new List<string>();
async Task Save(string name,object value)=>await File.WriteAllTextAsync(Path.Combine(output,name),JsonSerializer.Serialize(value,new JsonSerializerOptions{WriteIndented=true}));
void Check(string name,bool pass,object? details=null){checks.Add(new{Name=name,Pass=pass,Details=details});Console.WriteLine($"{(pass?"PASS":"FAIL")} {name}");}
string Rel(string p)=>Path.GetRelativePath(work,p);
void ResetDates(){foreach(var row in expected){var p=Path.Combine(work,row.GetProperty("relative").GetString()!);File.SetLastWriteTimeUtc(p,DateTimeOffset.Parse(row.GetProperty("modified_utc").GetString()!).UtcDateTime);File.SetLastAccessTimeUtc(p,DateTimeOffset.Parse(row.GetProperty("access_utc").GetString()!).UtcDateTime);}}
string Hash(string p){using var stream=SessaoPreservacaoAcesso.Abrir(p);return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();}
void CopyTree(string source,string destination){Directory.CreateDirectory(destination);foreach(var d in Directory.GetDirectories(source,"*",SearchOption.AllDirectories))Directory.CreateDirectory(Path.Combine(destination,Path.GetRelativePath(source,d)));foreach(var f in Directory.GetFiles(source,"*",SearchOption.AllDirectories)){var p=Path.Combine(destination,Path.GetRelativePath(source,f));using(var sourceStream=SessaoPreservacaoAcesso.Abrir(f))using(var destStream=new FileStream(p,FileMode.CreateNew,FileAccess.Write))sourceStream.CopyTo(destStream);File.SetLastWriteTimeUtc(p,File.GetLastWriteTimeUtc(f));}}

Dictionary<string,string> Snapshot(string root)=>Directory.GetFiles(root,"*",SearchOption.AllDirectories).ToDictionary(p=>Path.GetRelativePath(root,p),p=>File.GetLastAccessTimeUtc(p).ToString("O")+"|"+File.GetLastWriteTimeUtc(p).ToString("O")+"|"+new FileInfo(p).Length);
var originalAntes=Snapshot(original);
const AccessControlSections secoes=AccessControlSections.Access|AccessControlSections.Owner|AccessControlSections.Group;
string Seguranca(string p)=>new FileInfo(p).GetAccessControl(secoes).GetSecurityDescriptorSddlForm(secoes);
try{
 CopyTree(original,work);
 var dlls=new[]{typeof(InventariarDiretorio).Assembly,typeof(ClassificadorArquivos).Assembly,typeof(FonteInventarioSistemaArquivos).Assembly}.Select(a=>new{a.FullName,a.Location,Sha256=Hash(a.Location)}).ToArray();await Save("versoes_dll.json",dlls);
 Check("Original_controlado_integro_antes",expected.All(r=>Hash(Path.Combine(original,r.GetProperty("relative").GetString()!))==r.GetProperty("sha256").GetString()));
 var config=new ConfiguracaoCompatibilidade(PerfilDestino.SharePointEmpresarial(SubtipoSharePoint.SharePointOnline),new[]{"SPO-NOME-001","SPO-NOME-002","SPO-NOME-003","SPO-NOME-004","SPO-TAM-001"});
 string? lastHistory=null,lastExecution=null;
 foreach(var readers in new[]{1,8}){
  ResetDates();var runOut=Path.Combine(output,$"Leitores_{readers}");Directory.CreateDirectory(runOut);
  var total=Stopwatch.StartNew();var beginning=DateTimeOffset.UtcNow;
  var classifier=ClassificadorArquivos.CriarComFiltrosSelecionados(true,true,180,reference);
  await using var exporter=ExportadorRelatoriosM5.Iniciar(work,runOut,FiltroLinhasRelatorio.TodasAsEntradas);
  var history=Path.Combine(runOut,"historico.sqlite3");
  await using var session=await SessaoHistoricoSqliteM6.IniciarAsync(work,history,new ParametrosHistoricoM6(new[]{"ArquivoVazio","CopiaBackupOuTemporario","SinalTemporal","DuplicatasSHA256"},"SharePointOneDriveEmpresarial","SharePointOnline",config.CodigosHabilitados,customLimit,180,reference,true,"Access time controlled for validation"));
  var phase=Stopwatch.StartNew();
  var inventory=await new InventariarDiretorio(new FonteInventarioSistemaArquivos(),10000,classifier,config,new FiltroTamanhoPersonalizado(customLimit),new ObservadorInventarioComposto(exporter,session)).ExecutarAsync(work);
  var invTime=phase.Elapsed;
  Check($"M1_totais_{readers}",inventory.QuantidadeArquivos==oracle.GetProperty("files").GetInt64()&&inventory.QuantidadeDiretorios==oracle.GetProperty("directories").GetInt64()&&inventory.TotalBytes==oracle.GetProperty("bytes").GetInt64(),new{inventory.QuantidadeArquivos,inventory.QuantidadeDiretorios,inventory.TotalBytes});
  Check($"M1_sem_ocorrencias_{readers}",inventory.TotalOcorrencias==0);
  var classified=inventory.ClassificacoesAmostra.ToDictionary(c=>Rel(c.Arquivo.CaminhoCompleto),StringComparer.OrdinalIgnoreCase);
  var mismatches=new List<object>();
  foreach(var row in expected){var rel=row.GetProperty("relative").GetString()!;var want=row.GetProperty("expected_categories").EnumerateArray().Select(v=>v.GetString()!).Order().ToArray();var actual=classified[rel].Categorias.Select(v=>v.ToString()).Order().ToArray();if(!want.SequenceEqual(actual))mismatches.Add(new{Path=rel,Expected=want,Actual=actual});}
  Check($"M2_classificacao_arquivo_a_arquivo_{readers}",mismatches.Count==0,mismatches);
  Check($"M3_limite_personalizado_{readers}",inventory.ArquivosAcimaLimitePersonalizado==oracle.GetProperty("above_custom_limit").GetInt64());
  var incompatibleExpected=oracle.GetProperty("incompatible_files").EnumerateArray().Concat(oracle.GetProperty("incompatible_directories").EnumerateArray()).Select(e=>e.GetString()!).Order().ToArray();
  var incompatibleActual=inventory.AmostraAvaliacoes.Where(a=>a.Verificacoes.Any(v=>v.Estado==EstadoVerificacao.IncompatibilidadeDocumentada)).Select(a=>Rel(a.Entrada.CaminhoCompleto)).Order().ToArray();
  Check($"M3_nomes_reais_{readers}",incompatibleExpected.SequenceEqual(incompatibleActual),new{Expected=incompatibleExpected,Actual=incompatibleActual});
  var tracked=new TrackedReader(new LeitorConteudoDuplicatasSistemaArquivos());var progress=new List<ProgressoDuplicatas>();phase.Restart();var cpu=Process.GetCurrentProcess().TotalProcessorTime;
  var duplicates=await new DetectarDuplicatasExatas(new FonteInventarioSistemaArquivos(),tracked,readers).ExecutarAsync(work,new ProgressInline(progress));
  var dupTime=phase.Elapsed;var cpuTime=Process.GetCurrentProcess().TotalProcessorTime-cpu;
  string Signature(IEnumerable<IEnumerable<string>> groups)=>string.Join("\n",groups.Select(g=>string.Join("|",g.Order())).Order());
  var actualSignature=Signature(duplicates.Grupos.Select(g=>g.Arquivos.Select(f=>Rel(f.CaminhoCompleto))));
  var expectedSignature=Signature(oracle.GetProperty("duplicate_groups").EnumerateArray().Select(g=>g.GetProperty("members").EnumerateArray().Select(m=>m.GetString()!)));
  signatures.Add(actualSignature);
  Check($"M4_grupos_exatos_{readers}",actualSignature==expectedSignature);
  Check($"M4_economia_potencial_{readers}",duplicates.BytesPotencialmenteRedundantes==oracle.GetProperty("potential_redundant_bytes").GetInt64()&&duplicates.ArquivosEmGrupos==oracle.GetProperty("duplicate_members").GetInt64(),new{duplicates.BytesPotencialmenteRedundantes,duplicates.ArquivosEmGrupos});
  Check($"M4_limite_concorrencia_{readers}",tracked.Peak<=readers&&tracked.Peak>=1,new{Configured=readers,tracked.Peak,tracked.Calls,tracked.Bytes});
  Check($"M4_progresso_completo_{readers}",duplicates.TotalOcorrencias==0&&progress.Count>0&&progress.Last().CandidatosProcessados==duplicates.CandidatosPorTamanho,progress.LastOrDefault());
  var reports=await exporter.ConcluirAsync(inventory,duplicates,new[]{"ArquivoVazio","CopiaBackupOuTemporario","SinalTemporal","DuplicatasSHA256"},180,reference);
  session.PreservacaoAcesso=preservacao.Resumo;
  await session.ConcluirAsync(inventory,duplicates);
  var overall=total.Elapsed;
  session.PreservacaoAcesso=preservacao.Resumo;
  var metric=MetricasAnalise.Criar(work,beginning,inventory,duplicates,invTime,dupTime,overall,readers,"Controlled corpus; physical files; frozen temporal reference; all selected filters and SPO rules; total includes inventory, M4, report/history finalization; excludes metrics persistence. Warm cache uncontrolled. External API instrument, not desktop UI.",new[]{CategoriaAnalise.ArquivoVazio,CategoriaAnalise.CopiaBackupOuTemporario,CategoriaAnalise.SinalTemporal});
  await RepositorioMetricasAnalise.SalvarAsync(Path.Combine(output,"metricas.sqlite3"),metric);
  var records=await RepositorioMetricasAnalise.ConsultarAsync(Path.Combine(output,"metricas.sqlite3"));await RepositorioMetricasAnalise.ExportarCsvAsync(Path.Combine(output,$"metricas_excel_leitores_{readers}.csv"),records);
  var page=await ConsultaEntradasFiltradasM10.ConsultarAsync(history,session.IdExecucao,new(),0);
  var second=await ConsultaEntradasFiltradasM10.ConsultarAsync(history,session.IdExecucao,new(),1);
  Check($"M6_M10_paginacao_{readers}",page.Total==inventory.QuantidadeArquivos&&page.Entradas.Count==200&&second.Entradas.Count>0&&!page.Entradas.Select(e=>e.Id).Intersect(second.Entradas.Select(e=>e.Id)).Any(),new{page.Total,First=page.Entradas.Count,Second=second.Entradas.Count});
  var selection=await ConsultaEntradasFiltradasM10.SelecionarTodosFiltradosAsync(history,session.IdExecucao,new(),new HashSet<string>());
  Check($"M10_selecao_global_{readers}",selection.Count==inventory.QuantidadeArquivos);
  var empty=await ConsultaEntradasFiltradasM10.ConsultarAsync(history,session.IdExecucao,new(Categoria:"ArquivoVazio"),0);
  Check($"M10_filtro_categoria_{readers}",empty.Total==oracle.GetProperty("categories").GetProperty("ArquivoVazio").GetProperty("files").GetInt64());
  var dupPage=await ConsultaEntradasFiltradasM10.ConsultarAsync(history,session.IdExecucao,new(ApenasDuplicatas:true),0);
  Check($"M10_filtro_duplicatas_{readers}",dupPage.Total==duplicates.ArquivosEmGrupos);
  timing.Add(new{Readers=readers,InventorySeconds=invTime.TotalSeconds,DuplicateSeconds=dupTime.TotalSeconds,TotalSeconds=overall.TotalSeconds,HashCPUSeconds=cpuTime.TotalSeconds,tracked.Peak,tracked.Calls,tracked.Bytes,inventory.QuantidadeArquivos,inventory.TotalBytes,Groups=duplicates.Grupos.Count,duplicates.ArquivosEmGrupos,duplicates.BytesPotencialmenteRedundantes,Reports=reports,History=history});
  await Save("desempenho.json",timing);await Save("verificacoes.json",checks);
  lastHistory=history;lastExecution=session.IdExecucao;
 }
 Check("M4_equivalencia_1_e_8_leitores",signatures.Count==2&&signatures[0]==signatures[1]);
 var consolidatedMetrics=await RepositorioMetricasAnalise.ConsultarAsync(Path.Combine(output,"metricas.sqlite3"));
 await RepositorioMetricasAnalise.ExportarCsvAsync(Path.Combine(output,"metricas_excel.csv"),consolidatedMetrics);
 Check("Metricas_exportacao_consolidada",consolidatedMetrics.Count==2&&File.Exists(Path.Combine(output,"metricas_excel.csv")));
 // Virtual boundary tests are expressly separate from physical corpus/storage totals.
 var spo=new AvaliadorCompatibilidade(config);
 bool Has(string path,long bytes,string code)=>spo.Avaliar(EntradaInventariada.DeArquivo(new ArquivoInventariado(path,bytes))).Single(x=>x.CodigoRegra==code).Estado==EstadoVerificacao.IncompatibilidadeDocumentada;
 foreach(var c in new[]{'"',':','|','<','>','*','?'})Check("M3_virtual_pontuacao_"+(int)c,Has(@"D:\virtual\nome"+c+".txt",1,"SPO-NOME-001"));
 Check("M3_virtual_espaco_final",Has(@"D:\virtual\arquivo.txt ",1,"SPO-NOME-003"));
 foreach(var delta in new long[]{-1,0,1})Check("M3_virtual_SPO_250GiB_"+delta,Has(@"D:\virtual\grande.dat",250L*1024*1024*1024+delta,"SPO-TAM-001")== (delta>0));
 var azure=new AvaliadorCompatibilidade(new ConfiguracaoCompatibilidade(PerfilDestino.AzureFilesSmb(),new[]{"AZF-NOME-001","AZF-TAM-001"}));
 foreach(var delta in new long[]{-1,0,1}){
  var virtualEntry=EntradaInventariada.DeArquivo(new ArquivoInventariado(@"D:\virtual\grande.dat",4L*1024*1024*1024*1024+delta));
  var actual=azure.Avaliar(virtualEntry).Single(v=>v.CodigoRegra=="AZF-TAM-001").Estado==EstadoVerificacao.IncompatibilidadeDocumentada;
  Check("M3_virtual_Azure_4TiB_"+delta,actual==(delta>0));
 }
 // Temporal equality, absent date and opt-out are independent functional controls.
 var temporal=new RegraSinalTemporal(180,reference);
 Check("M2_virtual_data_ausente",temporal.Avaliar(new ArquivoInventariado(@"D:\virtual\semdata.txt",1)) is null);
 Check("M2_filtros_desativados",ClassificadorArquivos.CriarComFiltrosSelecionados(false,false,null,reference).Classificar(new ArquivoInventariado(@"D:\virtual\vazio.tmp",0)).Categorias.Count==0);
 var delay=new DelayReader();await new DetectarDuplicatasExatas(new VirtualSource(),delay,8).ExecutarAsync("virtual");Check("M4_fila_global_multiplos_tamanhos",delay.Peak>2&&delay.Peak<=8,new{delay.Peak});
 var failureProgress=new List<ProgressoDuplicatas>();var failed=await new DetectarDuplicatasExatas(new VirtualSource(),new FaultReader(),8).ExecutarAsync("virtual",new ProgressInline(failureProgress));
 Check("M4_falha_contabilizada_progresso",failed.TotalOcorrencias==1&&failureProgress.Last().Falhas==1&&failureProgress.Last().CandidatosProcessados==6,failureProgress.Last());
 using(var cancel=new CancellationTokenSource()){cancel.CancelAfter(10);try{await new DetectarDuplicatasExatas(new VirtualSource(),new DelayReader(),8).ExecutarAsync("virtual",cancellationToken:cancel.Token);Check("M4_cancelamento",false);}catch(OperationCanceledException){Check("M4_cancelamento",true);}}
 try{await new DetectarDuplicatasExatas(new VirtualSource(),new DelayReader(),8,3).ExecutarAsync("virtual");Check("M4_limite_indexacao_explicito",false);}catch(InvalidOperationException){Check("M4_limite_indexacao_explicito",true);}
 var checkpointDir=Path.Combine(output,"checkpoints");using(var marker=CheckpointAnaliseM9.Iniciar(work,"controlled-validation",checkpointDir)){marker.Atualizar(5,100,true);marker.MarcarCancelada();}
 var checkpoint=CheckpointAnaliseM9.Consultar(work,checkpointDir);Check("M9_checkpoint_cancelado",checkpoint?.PodeReiniciar==true&&checkpoint.ArquivosLidos==5&&checkpoint.BytesInformados==100);
 using(var marker=CheckpointAnaliseM9.Iniciar(work,"controlled-validation",checkpointDir))marker.MarcarConcluida();Check("M9_conclusao_remove_checkpoint",CheckpointAnaliseM9.Consultar(work,checkpointDir) is null);
 // Only a disposable work copy can be moved. This test never moves 02_original.
 var q=await ConsultaEntradasFiltradasM10.ConsultarAsync(lastHistory!,lastExecution!,new(TrechoCaminho:"rascunho.tmp"),0);var entry=q.Entradas.Single(e=>e.Caminho.EndsWith(Path.Combine("05_Temporarios_backup","rascunho.tmp"),StringComparison.OrdinalIgnoreCase));
 var execution=(await SessaoHistoricoSqliteM6.ConsultarUltimasAsync(lastHistory!)).Single(e=>e.Id==lastExecution);
 var quarantine=Path.Combine(basePath,"07_quarentena",Path.GetFileName(output));Directory.CreateDirectory(quarantine);var audit=Path.Combine(output,"quarentena.sqlite3");
 var plan=await ServicoQuarentenaM7.PrepararAsync(entry,execution,quarantine);var hashBefore=Hash(entry.Caminho);var segurancaAntes=Seguranca(entry.Caminho);
 try{await ServicoQuarentenaM7.ExecutarAsync(plan,false,audit);Check("M7_sem_confirmacao_negado",false);}catch(OperationCanceledException){Check("M7_sem_confirmacao_negado",File.Exists(entry.Caminho)&&!File.Exists(plan.CaminhoDestino));}
 await ServicoQuarentenaM7.ExecutarAsync(plan,true,audit);Check("M7_envio_confirmado_integro",!File.Exists(entry.Caminho)&&File.Exists(plan.CaminhoDestino)&&Hash(plan.CaminhoDestino)==hashBefore);
 Check("M7_seguranca_arquivo_preservada",Seguranca(plan.CaminhoDestino)==segurancaAntes);
 var pastaEspelhada=new DirectoryInfo(Path.GetDirectoryName(plan.CaminhoDestino)!).GetAccessControl(secoes);
 Check("M7_pasta_espelhada_protegida",pastaEspelhada.AreAccessRulesProtected);
 var restore=await ServicoRestauracaoM8.PrepararAsync(plan.IdOperacao,audit);
 try{await ServicoRestauracaoM8.RestaurarAsync(restore,false,audit);Check("M8_sem_confirmacao_negado",false);}catch(OperationCanceledException){Check("M8_sem_confirmacao_negado",File.Exists(plan.CaminhoDestino)&&!File.Exists(entry.Caminho));}
 await File.WriteAllTextAsync(entry.Caminho,"CONTROLLED RESTORE CONFLICT SENTINEL");
 try{await ServicoRestauracaoM8.PrepararAsync(plan.IdOperacao,audit);Check("M8_conflito_nao_sobrescreve",false);}catch(IOException){Check("M8_conflito_nao_sobrescreve",File.ReadAllText(entry.Caminho)=="CONTROLLED RESTORE CONFLICT SENTINEL");}
 File.Delete(entry.Caminho);restore=await ServicoRestauracaoM8.PrepararAsync(plan.IdOperacao,audit);await ServicoRestauracaoM8.RestaurarAsync(restore,true,audit);Check("M8_restauracao_integra",File.Exists(entry.Caminho)&&!File.Exists(plan.CaminhoDestino)&&Hash(entry.Caminho)==hashBefore);
 Check("M8_seguranca_restaurada",Seguranca(entry.Caminho)==segurancaAntes);
 var auditRows=await ServicoQuarentenaM7.ConsultarAsync(audit);Check("M7_M8_auditoria_persistente",auditRows.Any(r=>r.IdOperacao==plan.IdOperacao&&r.Estado=="Restaurado"));
 Check("Original_controlado_integro_depois",expected.All(r=>Hash(Path.Combine(original,r.GetProperty("relative").GetString()!))==r.GetProperty("sha256").GetString()));

 var originalDepois=Snapshot(original);Check("Original_datas_e_tamanhos_inalterados",originalAntes.Count==originalDepois.Count&&originalAntes.All(x=>originalDepois.TryGetValue(x.Key,out var v)&&v==x.Value));
 Check("Preservacao_sem_fallback",preservacao.Resumo.LeiturasSemGarantia==0&&preservacao.Resumo.LeiturasIgnoradas==0);
 await preservacao.SalvarAsync(Path.Combine(output,"preservacao_execucao.json"));
}catch(Exception ex){Check("Unexpected_exception",false,ex.ToString());}
await Save("verificacoes.json",checks);await Save("desempenho.json",timing);
var failures=checks.Count(x=>!(bool)x.GetType().GetProperty("Pass")!.GetValue(x)!);
await Save("conclusao.json",new{Version="CLEARv1.0",Checks=checks.Count,Failures=failures,Passed=failures==0,Output=output,PhysicalCorpus=original,WorkCopy=work,Scope="Real compiled CLEAR domain/application/infrastructure DLLs. Physical controlled corpus plus explicitly virtual boundary/failure cases. Desktop visual flows and external servers not tested. No realistic corpus analysis; uses CLEARv1.0 with explicit NTFS security preservation. AD/SMB and spreadsheet links are not validated."});
Console.WriteLine($"FINISHED checks={checks.Count} failures={failures} output={output}");Environment.ExitCode=failures==0?0:2;

sealed class ProgressInline(List<ProgressoDuplicatas> records):IProgress<ProgressoDuplicatas>{public void Report(ProgressoDuplicatas value)=>records.Add(value);}
sealed class TrackedReader(ILeitorConteudoDuplicatas inner):ILeitorConteudoDuplicatas{
 int active;public int Peak;public long Calls,Bytes;
 public async Task<string> CalcularSha256Async(ArquivoInventariado file,CancellationToken token=default){int value=Interlocked.Increment(ref active),old;do{old=Peak;if(old>=value)break;}while(Interlocked.CompareExchange(ref Peak,value,old)!=old);Interlocked.Increment(ref Calls);Interlocked.Add(ref Bytes,file.TamanhoBytes);try{return await inner.CalcularSha256Async(file,token);}finally{Interlocked.Decrement(ref active);}}
 public Task<bool> CompararBytesAsync(ArquivoInventariado a,ArquivoInventariado b,CancellationToken token=default)=>inner.CompararBytesAsync(a,b,token);
}
sealed class VirtualSource:IFonteInventario{public async IAsyncEnumerable<ArquivoInventariado> EnumerarAsync(string root,[System.Runtime.CompilerServices.EnumeratorCancellation]CancellationToken token=default){await Task.Yield();for(int i=0;i<6;i++){token.ThrowIfCancellationRequested();yield return new ArquivoInventariado("virtual-"+i,100+i/2);}}}
sealed class DelayReader:ILeitorConteudoDuplicatas{int active;public int Peak;public async Task<string> CalcularSha256Async(ArquivoInventariado a,CancellationToken token=default){var n=Interlocked.Increment(ref active);int old;do{old=Peak;if(old>=n)break;}while(Interlocked.CompareExchange(ref Peak,n,old)!=old);try{await Task.Delay(50,token);return a.CaminhoCompleto;}finally{Interlocked.Decrement(ref active);}}public Task<bool> CompararBytesAsync(ArquivoInventariado a,ArquivoInventariado b,CancellationToken token=default)=>Task.FromResult(false);}
sealed class FaultReader:ILeitorConteudoDuplicatas{public Task<string> CalcularSha256Async(ArquivoInventariado a,CancellationToken token=default)=>a.CaminhoCompleto=="virtual-1"?Task.FromException<string>(new IOException("Controlled injected I/O failure")):Task.FromResult(a.CaminhoCompleto);public Task<bool> CompararBytesAsync(ArquivoInventariado a,ArquivoInventariado b,CancellationToken token=default)=>Task.FromResult(false);}
