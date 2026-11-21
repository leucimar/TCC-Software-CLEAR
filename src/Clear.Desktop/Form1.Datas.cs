using System.ComponentModel;
using System.Diagnostics;
using System.Security.Principal;
using System.Text.Json;
using Clear.Domain.Analise;
using Clear.Domain.Arquivos;

namespace Clear.Desktop;

public partial class Form1
{
    private readonly ComboBox _criterioTemporal = new()
        { DropDownStyle = ComboBoxStyle.DropDownList, Width = 280, AccessibleName = "Data usada na análise" };
    private readonly ComboBox _politicaPreservacao = new()
        { DropDownStyle = ComboBoxStyle.DropDownList, Width = 280, AccessibleName = "Se a preservação não for permitida" };
    private readonly Button _tentarAdministrador = new()
        { Text = "Tentar como administrador", AutoSize = true };
    private CriterioTemporal CriterioSelecionado => _criterioTemporal.SelectedIndex == 1
        ? CriterioTemporal.UltimaModificacao : CriterioTemporal.UltimoAcesso;
    private PoliticaSemPreservacao PoliticaSelecionada => _politicaPreservacao.SelectedIndex == 1
        ? PoliticaSemPreservacao.LerComAviso : PoliticaSemPreservacao.NaoLerConteudo;
    private static bool Administrador()
    {
        using var identidade = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(identidade).IsInRole(WindowsBuiltInRole.Administrator);
    }
    private static string DescreverPreservacao(ResumoPreservacaoAcesso resumo) =>
        $"Preservação aceita pelo Windows: {resumo.AberturasProtegidas:N0} abertura(s); " +
        $"sem garantia: {resumo.LeiturasSemGarantia:N0}; conteúdo ignorado: {resumo.LeiturasIgnoradas:N0}. " +
        "Contagens de aberturas, não de arquivos únicos. Acessos de outros processos não são controlados.";

    private void TentarAdministrador()
    {
        if (_cancelamento is not null || Administrador()) return;
        string? arquivo = null;
        try
        {
            var pasta = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CLEAR", "Elevacao");
            Directory.CreateDirectory(pasta);
            arquivo = Path.Combine(pasta, Guid.NewGuid().ToString("N") + ".json");
            var configuracao = CapturarConfiguracaoElevacao();
            File.WriteAllText(arquivo, JsonSerializer.Serialize(configuracao));
            var inicio = CriarInicioElevacao(arquivo);
            using var processo = Process.Start(inicio)
                ?? throw new InvalidOperationException("O Windows não iniciou a nova janela.");
            // Keep this window/settings as a fallback if the elevated process fails to load them.
            _estado.Text = "Janela administrativa solicitada. Confira as opções nela antes de iniciar. Esta janela continua disponível.";
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            if (arquivo is not null) File.Delete(arquivo);
            _estado.Text = "Elevação cancelada. Suas opções foram mantidas; você pode continuar sem administrador.";
        }
        catch (Exception ex)
        {
            if (arquivo is not null && File.Exists(arquivo)) File.Delete(arquivo);
            MessageBox.Show(this, ex.Message, "Não foi possível tentar como administrador",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private ConfiguracaoJanela CapturarConfiguracaoElevacao() => new ConfiguracaoJanela(
                _caminho.Text, _pastaRelatorios.Text, _pastaQuarentena.Text, _quarentenaManual,
                _azure.Checked ? 1 : _sharePoint.Checked ? 2 : 0, _subtipo.SelectedIndex,
                _identificarVazios.Checked, _identificarTemporarios.Checked, _ativarSinalTemporal.Checked,
                (int)_dias.Value, _criterioTemporal.SelectedIndex, _politicaPreservacao.SelectedIndex,
                _ativarDuplicatas.Checked, (int)_maximoHashes.Value, (int)_limiteIndexacao.Value,
                _ativarTamanho.Checked, _limite.Value, _unidade.SelectedIndex,
                _exportarRelatorio.Checked, _filtroRelatorio.SelectedIndex, true,
                _salvarCheckpoint.Checked, _checks.Where(x => x.Value.Checked).Select(x => x.Key).ToArray(),
                _caminhoBiblioteca.Text, _subpastaDestino.Text);

    private static ProcessStartInfo CriarInicioElevacao(string arquivo)
    {
        var inicio = new ProcessStartInfo(Environment.ProcessPath
                ?? throw new InvalidOperationException("Executável atual indisponível."))
                { UseShellExecute = true, Verb = "runas", WorkingDirectory = AppContext.BaseDirectory };
            if (string.Equals(Path.GetFileNameWithoutExtension(inicio.FileName), "dotnet", StringComparison.OrdinalIgnoreCase))
                inicio.ArgumentList.Add(typeof(Form1).Assembly.Location);
            inicio.ArgumentList.Add("--configuracao-elevacao");
            inicio.ArgumentList.Add(arquivo);
        return inicio;
    }

    internal void AplicarConfiguracaoElevacao(string arquivo)
    {
        var path = Path.GetFullPath(arquivo);
        if (Path.GetExtension(path) != ".json" || !Guid.TryParseExact(Path.GetFileNameWithoutExtension(path), "N", out _)
            || new FileInfo(path).Length > 65536 || (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidDataException("Arquivo de opções de elevação inválido.");
        var c = JsonSerializer.Deserialize<ConfiguracaoJanela>(File.ReadAllText(path))
            ?? throw new InvalidDataException("Não foi possível recuperar as opções.");
        if (c.Destino is < 0 or > 2 || c.Subtipo is < 0 or > 1 || c.Criterio is < 0 or > 1
            || c.Politica is < 0 or > 1 || c.Dias is < 30 or > 3650 || c.Hashes is < 1 or > 8
            || c.Indexacao is < 100 or > 1000000 || c.Limite is < 0.01m or > 1000000m
            || c.Unidade is < 0 or > 4 || c.Filtro is < 0 or > 4 || c.Regras is null)
            throw new InvalidDataException("Opções de elevação fora dos limites permitidos.");
        _caminho.Text = c.Origem; _pastaRelatorios.Text = c.Relatorios;
        _pastaQuarentena.Text = c.Quarentena; _quarentenaManual = c.QuarentenaManual;
        _semDestino.Checked = c.Destino == 0; _azure.Checked = c.Destino == 1; _sharePoint.Checked = c.Destino == 2;
        _subtipo.SelectedIndex = c.Subtipo;
        _caminhoBiblioteca.Text = c.Biblioteca ?? string.Empty;
        _subpastaDestino.Text = c.SubpastaDestino ?? string.Empty;
        _identificarVazios.Checked = c.Vazios; _identificarTemporarios.Checked = c.Temporarios;
        _ativarSinalTemporal.Checked = c.Temporal; _dias.Value = c.Dias;
        _criterioTemporal.SelectedIndex = c.Criterio; _politicaPreservacao.SelectedIndex = c.Politica;
        _ativarDuplicatas.Checked = c.Duplicatas; _maximoHashes.Value = c.Hashes; _limiteIndexacao.Value = c.Indexacao;
        _ativarTamanho.Checked = c.Tamanho; _limite.Value = c.Limite; _unidade.SelectedIndex = c.Unidade;
        _exportarRelatorio.Checked = c.Exportar; _filtroRelatorio.SelectedIndex = c.Filtro;
        _salvarCheckpoint.Checked = c.Checkpoint;
        foreach (var item in _checks) item.Value.Checked = c.Regras.Contains(item.Key, StringComparer.Ordinal);
        AtualizarHabilitacao();
        // Do not delete a path supplied on the command line under an elevated identity.
        // The tiny settings file stays with its original owner; never auto-start an analysis.
        _estado.Text = "Opções recuperadas. Revise-as e clique em Iniciar análise quando desejar. Elevação não garante acesso a todos os arquivos.";
    }

    private sealed record ConfiguracaoJanela(string Origem, string Relatorios, string Quarentena,
        bool QuarentenaManual, int Destino, int Subtipo, bool Vazios, bool Temporarios, bool Temporal,
        int Dias, int Criterio, int Politica, bool Duplicatas, int Hashes, int Indexacao, bool Tamanho,
        decimal Limite, int Unidade, bool Exportar, int Filtro, bool Historico, bool Checkpoint, string[] Regras,
        string? Biblioteca = null, string? SubpastaDestino = null);
}
