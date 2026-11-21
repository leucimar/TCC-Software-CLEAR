using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using Clear.Desktop;

[assembly: DoNotParallelize]

namespace Clear.Desktop.Tests;
[TestClass]
public sealed class ConfiguracaoDatasTests
{
    private static T Campo<T>(Form1 f,string nome) => (T)typeof(Form1).GetField(nome,BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(f)!;
    private static object? Chamar(Form1 f,string nome,params object[] args) => typeof(Form1).GetMethod(nome,BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(f,args);
    private static Task Sta(Action acao)
    {
        var pronto=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread=new Thread(()=>{try{acao();pronto.SetResult();}catch(Exception ex){pronto.SetException(ex);}});
        thread.SetApartmentState(ApartmentState.STA);thread.Start();return pronto.Task;
    }
    [TestMethod]
    public async Task OpcoesSaoExclusivasEstritasPorPadraoEHabilitadasSomenteAntesDaAnalise()
    {
        await Sta(()=>
        {
            using var f=new Form1();
            var criterio=Campo<ComboBox>(f,"_criterioTemporal");var politica=Campo<ComboBox>(f,"_politicaPreservacao");
            Assert.AreEqual(ComboBoxStyle.DropDownList,criterio.DropDownStyle);
            Assert.AreEqual(2,criterio.Items.Count);Assert.AreEqual(0,criterio.SelectedIndex);
            Assert.AreEqual(0,politica.SelectedIndex);Assert.IsFalse(criterio.Enabled);Assert.IsTrue(politica.Enabled);
            Campo<CheckBox>(f,"_ativarSinalTemporal").Checked=true;Assert.IsTrue(criterio.Enabled);
            typeof(Form1).GetField("_cancelamento",BindingFlags.NonPublic|BindingFlags.Instance)!.SetValue(f,new CancellationTokenSource());
            Chamar(f,"AtualizarHabilitacao");Assert.IsFalse(criterio.Enabled);Assert.IsFalse(politica.Enabled);
            Assert.IsFalse(Campo<Button>(f,"_tentarAdministrador").Enabled);
            Campo<CancellationTokenSource>(f,"_cancelamento").Dispose();
            typeof(Form1).GetField("_cancelamento",BindingFlags.NonPublic|BindingFlags.Instance)!.SetValue(f,null);
        });
    }
    [TestMethod]
    public async Task ElevacaoRecuperaConfiguracaoCompletaSemIniciarAnalise()
    {
        await Sta(()=>
        {
            var caminho=Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString("N")+".json");
            try
            {
                using var original=new Form1();
                Campo<TextBox>(original,"_caminho").Text=@"D:\Acervo de teste";
                Campo<TextBox>(original,"_pastaRelatorios").Text=@"D:\Relatorios";
                Campo<RadioButton>(original,"_sharePoint").Checked=true;
                Campo<ComboBox>(original,"_subtipo").SelectedIndex=1;
                Campo<CheckBox>(original,"_ativarSinalTemporal").Checked=true;
                Campo<ComboBox>(original,"_criterioTemporal").SelectedIndex=1;
                Campo<ComboBox>(original,"_politicaPreservacao").SelectedIndex=1;
                Campo<NumericUpDown>(original,"_dias").Value=730;
                Campo<CheckBox>(original,"_ativarDuplicatas").Checked=true;
                Campo<NumericUpDown>(original,"_maximoHashes").Value=8;
                Campo<CheckBox>(original,"_exportarRelatorio").Checked=true;
                var antes=JsonSerializer.Serialize(Chamar(original,"CapturarConfiguracaoElevacao"));
                using var captura=JsonDocument.Parse(antes);
                Assert.IsTrue(captura.RootElement.GetProperty("Historico").GetBoolean());
                File.WriteAllText(caminho,antes.Replace("\"Historico\":true", "\"Historico\":false", StringComparison.Ordinal));
                using var nova=new Form1();Chamar(nova,"AplicarConfiguracaoElevacao",caminho);
                Assert.AreEqual(antes,JsonSerializer.Serialize(Chamar(nova,"CapturarConfiguracaoElevacao")));
                Assert.IsNull(typeof(Form1).GetField("_cancelamento",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(nova));
                Assert.IsTrue(Campo<Button>(nova,"_iniciar").Enabled);
                var processo=(ProcessStartInfo)typeof(Form1).GetMethod("CriarInicioElevacao",BindingFlags.NonPublic|BindingFlags.Static)!.Invoke(null,[caminho])!;
                Assert.AreEqual("runas",processo.Verb);Assert.IsTrue(processo.UseShellExecute);
                Assert.AreEqual("--configuracao-elevacao",processo.ArgumentList[^2]);
                Assert.AreEqual(caminho,processo.ArgumentList[^1]);
            }
            finally{File.Delete(caminho);}
        });
    }
    [TestMethod]
    public async Task CheckpointMudaQuandoCriterioOuPoliticaMudam()
    {
        await Sta(()=>
        {
            using var f=new Form1();Campo<CheckBox>(f,"_ativarSinalTemporal").Checked=true;
            var inicio=(string)Chamar(f,"AssinaturaConfiguracaoM9")!;
            Campo<ComboBox>(f,"_criterioTemporal").SelectedIndex=1;
            var modificado=(string)Chamar(f,"AssinaturaConfiguracaoM9")!;
            Campo<ComboBox>(f,"_politicaPreservacao").SelectedIndex=1;
            Assert.AreNotEqual(inicio,modificado);Assert.AreNotEqual(modificado,Chamar(f,"AssinaturaConfiguracaoM9"));
        });
    }
}
