using System.Text.Json;
using Clear.Application.Inventario;
using Clear.Application.Metricas;
using Clear.Domain.Analise;
using Clear.Domain.Arquivos;
using Clear.Infrastructure.Historico;
using Clear.Infrastructure.Inventario;
using Clear.Infrastructure.Metricas;
using Clear.Infrastructure.Relatorios;

namespace Clear.Infrastructure.Tests;
[TestClass]
public sealed class RelatoriosDatasPreservacaoTests
{
    [TestMethod]
    public async Task CriterioModificacaoEPreservacaoPermanecemNoJsonHistoricoEMetricasExcel()
    {
        var pasta=Path.Combine(Path.GetTempPath(),"CLEAR_RelatorioDatas_"+Guid.NewGuid().ToString("N"));
        var origem=Path.Combine(pasta,"origem");var saida=Path.Combine(pasta,"saida");
        Directory.CreateDirectory(origem);Directory.CreateDirectory(saida);
        try
        {
            var arquivo=Path.Combine(origem,"dado.txt");await File.WriteAllTextAsync(arquivo,"teste");
            var agora=DateTimeOffset.UtcNow;File.SetLastWriteTimeUtc(arquivo,agora.AddDays(-800).UtcDateTime);
            File.SetLastAccessTimeUtc(arquivo,agora.AddDays(-1).UtcDateTime);
            var resumo=new ResumoPreservacaoAcesso("LerComAviso",2,1,0,
                [new(arquivo,"LeituraSemGarantia","simulado",agora)],"UltimaModificacao");
            var parametros=ParametrosHistoricoM6.SemFiltros with { CriterioTemporal="UltimaModificacaoUtc",
                DiasSinalTemporal=365,PoliticaPreservacaoAcesso="LerComAviso" };
            var banco=Path.Combine(saida,"historico.sqlite3");
            await using var historico=await SessaoHistoricoSqliteM6.IniciarAsync(origem,banco,parametros);
            await using var exportador=ExportadorRelatoriosM5.Iniciar(origem,saida,FiltroLinhasRelatorio.TodasAsEntradas);
            var resultado=await new InventariarDiretorio(new FonteInventarioSistemaArquivos(),
                classificador:ClassificadorArquivos.CriarComFiltrosSelecionados(false,false,365,agora,CriterioTemporal.UltimaModificacao),
                observador:new ObservadorInventarioComposto(exportador,historico)).ExecutarAsync(origem);
            Assert.AreEqual(1L,resultado.ArquivosComIndicios);
            var arquivos=await exportador.ConcluirAsync(resultado,null,["SinalTemporal"],365,agora,
                criterioTemporal:CriterioTemporal.UltimaModificacao,preservacao:resumo);
            using(var json=JsonDocument.Parse(await File.ReadAllTextAsync(arquivos.ResumoJson)))
            {
                Assert.AreEqual("UltimaModificacaoUtc",json.RootElement.GetProperty("CriterioTemporal").GetString());
                Assert.AreEqual(1,json.RootElement.GetProperty("PreservacaoAcesso").GetProperty("LeiturasSemGarantia").GetInt32());
            }
            historico.PreservacaoAcesso=resumo;await historico.ConcluirAsync(resultado,null);
            var salvo=(await SessaoHistoricoSqliteM6.ConsultarUltimasAsync(banco)).Single();
            StringAssert.Contains(salvo.ParametrosJson!,"UltimaModificacaoUtc");
            StringAssert.Contains(salvo.ResumoJson!,"LeiturasSemGarantia");
            var m=MetricasAnalise.Criar(origem,agora,resultado,null,TimeSpan.FromSeconds(1),TimeSpan.Zero,
                TimeSpan.FromSeconds(1),1,"config",[CategoriaAnalise.SinalTemporal]) with { CriterioTemporal="UltimaModificacao",PreservacaoAcesso=resumo };
            var bm=Path.Combine(saida,"metricas.sqlite3");await RepositorioMetricasAnalise.SalvarAsync(bm,m);
            var medicoes=await RepositorioMetricasAnalise.ConsultarAsync(bm);
            Assert.AreEqual(1L,medicoes.Single().PreservacaoAcesso!.LeiturasSemGarantia);
            var csv=Path.Combine(saida,"metricas.csv");await RepositorioMetricasAnalise.ExportarCsvAsync(csv,medicoes);
            var linhas=await File.ReadAllLinesAsync(csv);StringAssert.Contains(linhas[0],"CriterioTemporal;PoliticaPreservacao");
            StringAssert.Contains(linhas[1],"UltimaModificacao");StringAssert.Contains(linhas[1],"LerComAviso");
        }
        finally{Directory.Delete(pasta,true);}
    }
}
