using Clear.Application.Inventario;
using Clear.Application.Compatibilidade;
using Clear.Application.Duplicatas;
using Clear.Domain.Analise;
using Clear.Domain.Compatibilidade;
using Clear.Domain.Inventario;
using Clear.Infrastructure.Inventario;
using Clear.Infrastructure.Duplicatas;
using Clear.Infrastructure.Relatorios;
using Clear.Infrastructure.Historico;

namespace Clear.Desktop;

/// <summary>
/// Seleção exclusiva de destino, verificações independentes e filtros gerais.
/// Toda a análise é somente leitura; não há upload, edição ou descarte.
/// </summary>
public partial class Form1 : Form
{
    private readonly TextBox _caminho = new() { Dock = DockStyle.Fill, ReadOnly = true };
    private readonly Button _selecionar = new() { Text = "Escolher pasta", Dock = DockStyle.Fill };
    private readonly Button _iniciar = new() { Text = "Analisar (somente leitura)", Dock = DockStyle.Fill };
    private readonly Button _cancelar = new() { Text = "Cancelar", Dock = DockStyle.Fill, Enabled = false };
    private readonly Label _resumo = new() { Dock = DockStyle.Fill };
    private readonly Label _estado = new() { Dock = DockStyle.Fill, AutoEllipsis = true };
    private readonly ToolTip _dicas = new();
    private readonly DataGridView _grade = new()
    {
        Dock = DockStyle.Fill,
        ReadOnly = true,
        AllowUserToAddRows = false,
        AllowUserToDeleteRows = false,
        RowHeadersVisible = false,
        MultiSelect = false,
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None,
        SelectionMode = DataGridViewSelectionMode.FullRowSelect
    };
    private readonly ListBox _ocorrencias = new() { Dock = DockStyle.Fill, HorizontalScrollbar = true };
    private readonly CheckBox _ativarDuplicatas = new()
    {
        Text = "Detectar duplicatas exatas (SHA-256; leitura do conteúdo)",
        AutoSize = true, Checked = false
    };
    private readonly CheckBox _exportarRelatorio = new()
    {
        Text = "Exportar relatório completo (CSV + JSON — M5)",
        AutoSize = true, Checked = false
    };
    private readonly ComboBox _filtroRelatorio = new()
    {
        DropDownStyle = ComboBoxStyle.DropDownList, Width = 190, Enabled = false
    };
    private readonly CheckBox _salvarHistorico = new()
    {
        Text = "Salvar histórico local SQLite (M6; não reutiliza classificações)",
        AutoSize = true, Checked = false
    };
    private readonly Button _atualizarHistorico = new()
    {
        Text = "Atualizar histórico", AutoSize = true
    };
    private readonly Label _infoHistorico = new()
    {
        Dock = DockStyle.Fill,
        Text = "Histórico opcional armazenado em %LOCALAPPDATA%\\CLEAR\\historico.sqlite3.",
        AutoEllipsis = true
    };
    private readonly DataGridView _gradeHistorico = new()
    {
        Dock = DockStyle.Fill, ReadOnly = true, MultiSelect = false,
        AllowUserToAddRows = false, AllowUserToDeleteRows = false,
        RowHeadersVisible = false,
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells,
        SelectionMode = DataGridViewSelectionMode.FullRowSelect
    };
    private readonly DataGridView _gradeDuplicatas = new()
    {
        Dock = DockStyle.Fill, ReadOnly = true,
        AllowUserToAddRows = false, AllowUserToDeleteRows = false,
        RowHeadersVisible = false, MultiSelect = false,
        SelectionMode = DataGridViewSelectionMode.FullRowSelect,
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None
    };
    private readonly Label _resumoDuplicatas = new()
    {
        Dock = DockStyle.Fill,
        Text = "Busca de duplicatas desativada. Marque o filtro geral para habilitar.",
        AutoEllipsis = true
    };

    private readonly RadioButton _semDestino = new()
        { Text = "Sem destino (análise geral)", AutoSize = true, Checked = true };
    private readonly RadioButton _azure = new()
        { Text = "Azure Files (SMB/Windows)", AutoSize = true };
    private readonly RadioButton _sharePoint = new()
        { Text = "SharePoint / OneDrive empresarial", AutoSize = true };
    private readonly ComboBox _subtipo = new()
        { DropDownStyle = ComboBoxStyle.DropDownList, Width = 175 };
    // Todos os filtros permanecem visíveis, inclusive os de outro destino.
    private readonly GroupBox _grupoAzure = new()
        { Text = "Azure Files — SMB/Windows", Dock = DockStyle.Fill };
    private readonly GroupBox _grupoSharePoint = new()
        { Text = "SharePoint Online / OneDrive empresarial", Dock = DockStyle.Fill };
    private readonly FlowLayoutPanel _painelAzure = new()
        { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown,
          WrapContents = false, AutoScroll = true, Padding = new Padding(8) };
    private readonly FlowLayoutPanel _painelSharePoint = new()
        { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown,
          WrapContents = false, AutoScroll = true, Padding = new Padding(8) };
    private readonly Dictionary<string, CheckBox> _checks = new(StringComparer.Ordinal);

    private readonly CheckBox _identificarVazios = new()
        { Text = "Identificar arquivos vazios", AutoSize = true, Checked = true };
    private readonly CheckBox _identificarTemporarios = new()
        { Text = "Identificar nomes de backups e temporários", AutoSize = true, Checked = true };
    private readonly CheckBox _ativarSinalTemporal = new()
        { Text = "Última modificação há mais de", AutoSize = true };
    private readonly NumericUpDown _dias = new()
        { Minimum = 30, Maximum = 3650, Value = 365, Width = 72, Enabled = false };
    private readonly CheckBox _ativarTamanho = new()
        { Text = "Avisar se o arquivo for maior que", AutoSize = true };
    private readonly NumericUpDown _limite = new()
        { Minimum = 0.01M, Maximum = 1000000, Value = 2, DecimalPlaces = 2,
          Increment = 0.25M, Width = 95, Enabled = false };
    private readonly ComboBox _unidade = new()
        { DropDownStyle = ComboBoxStyle.DropDownList, Width = 82, Enabled = false };

    private CancellationTokenSource? _cancelamento;

    public Form1()
    {
        InitializeComponent();
        MontarInterface();
    }

    private void MontarInterface()
    {
        Text = "CLEAR — Inventário, duplicatas, relatórios e histórico (M6)";
        MinimumSize = new Size(1190, 760);
        Size = new Size(1500, 930);

        var principal = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 7,
            Padding = new Padding(12)
        };
        principal.RowStyles.Add(new RowStyle(SizeType.Absolute, 80));
        principal.RowStyles.Add(new RowStyle(SizeType.Absolute, 102));
        principal.RowStyles.Add(new RowStyle(SizeType.Absolute, 233));
        principal.RowStyles.Add(new RowStyle(SizeType.Absolute, 62));
        principal.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        principal.RowStyles.Add(new RowStyle(SizeType.Absolute, 118));
        principal.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));

        var topo = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 2 };
        topo.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        topo.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 125));
        topo.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190));
        topo.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 95));
        topo.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        topo.RowStyles.Add(new RowStyle(SizeType.Absolute, 39));
        var mensagem = new Label
        {
            Text = "Análise somente leitura. Duplicatas usam leitura de conteúdo; nada é movido, modificado ou excluído.",
            Dock = DockStyle.Fill, AutoSize = true
        };
        topo.Controls.Add(mensagem, 0, 0);
        topo.SetColumnSpan(mensagem, 4);
        topo.Controls.Add(_caminho, 0, 1);
        topo.Controls.Add(_selecionar, 1, 1);
        topo.Controls.Add(_iniciar, 2, 1);
        topo.Controls.Add(_cancelar, 3, 1);


        // O ambiente escolhido é exclusivo. Os filtros continuam visíveis,
        // mas as regras do ambiente não selecionado não podem ser habilitadas.
        var grupoDestino = new GroupBox
            { Text = "Ambiente foco — ao selecionar, o CLEAR sugere as verificações daquele destino", Dock = DockStyle.Fill };
        var radios = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true, Padding = new Padding(8, 6, 8, 0)
        };
        radios.Controls.Add(_semDestino);
        radios.Controls.Add(_azure);
        radios.Controls.Add(_sharePoint);
        var subtipoLinha = new FlowLayoutPanel
            { AutoSize = true, FlowDirection = FlowDirection.LeftToRight };
        subtipoLinha.Controls.Add(new Label
            { Text = "Subtipo empresarial:", AutoSize = true, Padding = new Padding(0, 5, 0, 0) });
        _subtipo.Items.AddRange(new object[] { "SharePoint Online", "OneDrive for Business" });
        _subtipo.SelectedIndex = 0;
        subtipoLinha.Controls.Add(_subtipo);
        radios.Controls.Add(subtipoLinha);
        grupoDestino.Controls.Add(radios);

        var painelFiltros = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1,
            Padding = new Padding(0)
        };
        painelFiltros.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 520));
        painelFiltros.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 320));
        painelFiltros.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        var grupoGeral = new GroupBox
            { Text = "Filtros gerais — independentes do ambiente", Dock = DockStyle.Fill };
        var filtrosGerais = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown,
            WrapContents = false, AutoScroll = true,
            Padding = new Padding(8, 5, 8, 0)
        };
        filtrosGerais.Controls.Add(_identificarVazios);
        filtrosGerais.Controls.Add(_identificarTemporarios);

        var linhaTemporal = new FlowLayoutPanel
            { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = false };
        linhaTemporal.Controls.Add(_ativarSinalTemporal);
        linhaTemporal.Controls.Add(_dias);
        linhaTemporal.Controls.Add(new Label
            { Text = "dias", AutoSize = true, Padding = new Padding(0, 5, 0, 0) });
        filtrosGerais.Controls.Add(linhaTemporal);

        var linhaTamanho = new FlowLayoutPanel
            { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = false };
        linhaTamanho.Controls.Add(_ativarTamanho);
        linhaTamanho.Controls.Add(_limite);
        _unidade.Items.AddRange(new object[] { "B", "KiB", "MiB", "GiB", "TiB" });
        _unidade.SelectedIndex = 3;
        linhaTamanho.Controls.Add(_unidade);
        filtrosGerais.Controls.Add(linhaTamanho);
        filtrosGerais.Controls.Add(_ativarDuplicatas);

        var linhaRelatorio = new FlowLayoutPanel
        {
            AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = false
        };
        linhaRelatorio.Controls.Add(_exportarRelatorio);
        filtrosGerais.Controls.Add(linhaRelatorio);

        var linhaFiltroRelatorio = new FlowLayoutPanel
        {
            AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = false
        };
        linhaFiltroRelatorio.Controls.Add(new Label
        {
            Text = "Linhas CSV:", AutoSize = true, Padding = new Padding(0, 5, 0, 0)
        });
        _filtroRelatorio.Items.AddRange(new object[]
        {
            "Todas as entradas",
            "Somente indícios gerais",
            "Somente incompatibilidades",
            "Somente tamanho pessoal",
            "Somente achados (qualquer tipo)"
        });
        _filtroRelatorio.SelectedIndex = 0;
        linhaFiltroRelatorio.Controls.Add(_filtroRelatorio);
        filtrosGerais.Controls.Add(linhaFiltroRelatorio);
        filtrosGerais.Controls.Add(_salvarHistorico);
        grupoGeral.Controls.Add(filtrosGerais);

        foreach (var definicao in CatalogoVerificacoes.Definicoes)
        {
            var opcao = new CheckBox
            {
                Text = definicao.Rotulo + " [" + definicao.Codigo + "]",
                AutoSize = true,
                Checked = false
            };
            _dicas.SetToolTip(opcao,
                "Referência documental: " + definicao.FonteDocumental +
                ". Pode desmarcar após aplicar as sugestões.");
            _checks.Add(definicao.Codigo, opcao);
            if (definicao.Ambiente == AmbienteDestino.AzureFilesClassicoSmb)
                _painelAzure.Controls.Add(opcao);
            else
                _painelSharePoint.Controls.Add(opcao);
        }

        _grupoAzure.Controls.Add(_painelAzure);
        _grupoSharePoint.Controls.Add(_painelSharePoint);
        painelFiltros.Controls.Add(grupoGeral, 0, 0);
        painelFiltros.Controls.Add(_grupoAzure, 1, 0);
        painelFiltros.Controls.Add(_grupoSharePoint, 2, 0);

        _dicas.SetToolTip(_ativarDuplicatas,
            "Leitura de conteúdo desativada por padrão. Ative expressamente para " +
            "calcular SHA-256 e confirmar duplicatas byte a byte.");
        _dicas.SetToolTip(_ativarTamanho,
            "Filtro operacional: sinaliza arquivos maiores que o valor escolhido. " +
            "Não substitui nem altera os limites oficiais dos destinos.");

        _resumo.Text = "Nenhum inventário executado.";
        _resumo.Padding = new Padding(0, 9, 0, 0);
        _estado.Text = "Pronto. Selecione uma pasta sintética para analisar.";

        var areaOcorrencias = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2 };
        areaOcorrencias.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
        areaOcorrencias.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        areaOcorrencias.Controls.Add(new Label
        {
            Text = "Ocorrências (falhas/links ignorados; até 100 detalhes)",
            Dock = DockStyle.Fill
        }, 0, 0);
        areaOcorrencias.Controls.Add(_ocorrencias, 0, 1);

        principal.Controls.Add(topo, 0, 0);
        principal.Controls.Add(grupoDestino, 0, 1);
        principal.Controls.Add(painelFiltros, 0, 2);
        principal.Controls.Add(_resumo, 0, 3);
        var abas = new TabControl { Dock = DockStyle.Fill };
        var abaInventario = new TabPage("Inventário e compatibilidade");
        var abaDuplicatas = new TabPage("Duplicatas exatas — M4");
        abaInventario.Controls.Add(_grade);
        var painelDuplicatas = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2
        };
        painelDuplicatas.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        painelDuplicatas.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        painelDuplicatas.Controls.Add(_resumoDuplicatas, 0, 0);
        painelDuplicatas.Controls.Add(_gradeDuplicatas, 0, 1);
        abaDuplicatas.Controls.Add(painelDuplicatas);
        abas.TabPages.Add(abaInventario);
        abas.TabPages.Add(abaDuplicatas);

        var abaHistorico = new TabPage("Histórico SQLite — M6");
        var painelHistorico = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3
        };
        painelHistorico.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        painelHistorico.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        painelHistorico.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        var botoesHistorico = new FlowLayoutPanel { Dock = DockStyle.Fill };
        botoesHistorico.Controls.Add(_atualizarHistorico);
        painelHistorico.Controls.Add(botoesHistorico, 0, 0);
        painelHistorico.Controls.Add(_gradeHistorico, 0, 1);
        painelHistorico.Controls.Add(_infoHistorico, 0, 2);
        abaHistorico.Controls.Add(painelHistorico);
        abas.TabPages.Add(abaHistorico);

        principal.Controls.Add(abas, 0, 4);
        principal.Controls.Add(areaOcorrencias, 0, 5);
        principal.Controls.Add(_estado, 0, 6);
        Controls.Add(principal);

        _semDestino.CheckedChanged += (_, _) =>
        {
            if (_semDestino.Checked) AplicarRecomendacoesDoDestino();
        };
        _azure.CheckedChanged += (_, _) =>
        {
            if (_azure.Checked) AplicarRecomendacoesDoDestino();
        };
        _sharePoint.CheckedChanged += (_, _) =>
        {
            if (_sharePoint.Checked) AplicarRecomendacoesDoDestino();
        };
        _ativarSinalTemporal.CheckedChanged += (_, _) => AtualizarHabilitacao();
        _ativarTamanho.CheckedChanged += (_, _) => AtualizarHabilitacao();
        _exportarRelatorio.CheckedChanged += (_, _) => AtualizarHabilitacao();
        _atualizarHistorico.Click += async (_, _) => await AtualizarHistoricoAsync();
        _selecionar.Click += (_, _) => SelecionarPasta();
        _cancelar.Click += (_, _) => _cancelamento?.Cancel();
        _iniciar.Click += async (_, _) => await IniciarAnaliseAsync();
        FormClosing += (_, args) =>
        {
            if (_cancelamento is not null)
            {
                _cancelamento.Cancel();
                args.Cancel = true;
                _estado.Text = "Aguardando cancelamento para fechar...";
            }
        };

        _gradeDuplicatas.DataBindingComplete += (_, _) =>
        {
            foreach (DataGridViewColumn coluna in _gradeDuplicatas.Columns)
                coluna.Width = coluna.Name switch
                {
                    "Grupo" => 85,
                    "TamanhoBytes" => 130,
                    "Sha256" => 520,
                    _ => 470
                };
            foreach (DataGridViewRow linha in _gradeDuplicatas.Rows)
                foreach (DataGridViewCell celula in linha.Cells)
                    celula.ToolTipText = Convert.ToString(celula.Value) ?? "";
        };
        _grade.DataBindingComplete += (_, _) =>
        {
            var larguras = new Dictionary<string, int>
            {
                ["Tipo"] = 75, ["Caminho"] = 300, ["Bytes"] = 100,
                ["IndiciosGerais"] = 185, ["Compatibilidade"] = 220,
                ["Justificativas"] = 380, ["FiltroPersonalizado"] = 160,
                ["ModificadoUtc"] = 180
            };
            foreach (DataGridViewColumn coluna in _grade.Columns)
                if (larguras.TryGetValue(coluna.Name, out var largura))
                    coluna.Width = largura;
            foreach (DataGridViewRow linha in _grade.Rows)
                foreach (DataGridViewCell celula in linha.Cells)
                    celula.ToolTipText = Convert.ToString(celula.Value) ?? "";
        };
        AplicarRecomendacoesDoDestino();
        AtualizarHabilitacao();
    }

    private void AplicarRecomendacoesDoDestino()
    {
        // A seleção do ambiente aplica um preset apenas às regras oficiais.
        // Nenhum filtro geral é ativado/desativado ao mudar de destino;
        // especialmente SHA-256 permanece exclusivamente opt-in.
        AmbienteDestino? ambiente = _azure.Checked
            ? AmbienteDestino.AzureFilesClassicoSmb
            : _sharePoint.Checked
                ? AmbienteDestino.SharePointOneDriveEmpresarial
                : null;

        var sugeridas = new HashSet<string>(
            PerfisRecomendados.Para(ambiente), StringComparer.Ordinal);

        foreach (var (codigo, opcao) in _checks)
            opcao.Checked = sugeridas.Contains(codigo);

        AtualizarHabilitacao();
    }

    private void AtualizarHabilitacao()
    {
        var pronto = _cancelamento is null;
        _subtipo.Enabled = pronto && _sharePoint.Checked;
        _grupoAzure.Enabled = pronto && _azure.Checked;
        _grupoSharePoint.Enabled = pronto && _sharePoint.Checked;
        _identificarVazios.Enabled = pronto;
        _identificarTemporarios.Enabled = pronto;
        _dias.Enabled = pronto && _ativarSinalTemporal.Checked;
        _limite.Enabled = pronto && _ativarTamanho.Checked;
        _unidade.Enabled = pronto && _ativarTamanho.Checked;
        _ativarDuplicatas.Enabled = pronto;
        _exportarRelatorio.Enabled = pronto;
        _filtroRelatorio.Enabled = pronto && _exportarRelatorio.Checked;
        _salvarHistorico.Enabled = pronto;
        _atualizarHistorico.Enabled = pronto;
    }

    private async Task AtualizarHistoricoAsync()
    {
        _atualizarHistorico.Enabled = false;
        try
        {
            var registros = await SessaoHistoricoSqliteM6.ConsultarUltimasAsync(
                SessaoHistoricoSqliteM6.CaminhoPadrao);
            _gradeHistorico.DataSource = registros.Select(x => new
            {
                x.InicioUtc, x.FimUtc, x.Estado, x.DiretorioRaiz,
                x.QuantidadeArquivos, x.QuantidadeDiretorios, x.TotalBytes,
                x.EntradasPersistidas, x.GruposDuplicatas, x.TotalOcorrencias,
                x.Id
            }).ToList();
            _infoHistorico.Text =
                $"{registros.Count} execução(ões) exibida(s), até 30. Banco: " +
                SessaoHistoricoSqliteM6.CaminhoPadrao +
                " | Consulta somente leitura; classificações antigas não são reutilizadas.";
        }
        catch (Exception ex)
        {
            _infoHistorico.Text = "Não foi possível consultar histórico: " + ex.Message;
        }
        finally
        {
            _atualizarHistorico.Enabled = _cancelamento is null;
        }
    }

    private static string PastaInicialDeSelecao =>
        Directory.Exists(@"C:\Temp") ? @"C:\Temp" : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    private void SelecionarPasta()
    {
        using var dialogo = new FolderBrowserDialog
        {
            Description = "Selecione uma pasta de testes sintéticos para análise somente leitura.",
            UseDescriptionForTitle = true,
            ShowNewFolderButton = false,
            SelectedPath = Directory.Exists(_caminho.Text) ? _caminho.Text : PastaInicialDeSelecao
        };
        if (dialogo.ShowDialog(this) == DialogResult.OK)
            _caminho.Text = dialogo.SelectedPath;
    }

    private ConfiguracaoCompatibilidade CriarConfiguracaoDestino()
    {
        PerfilDestino? perfil = _azure.Checked ? PerfilDestino.AzureFilesSmb()
            : _sharePoint.Checked
                ? PerfilDestino.SharePointEmpresarial(
                    _subtipo.SelectedIndex == 1 ? SubtipoSharePoint.OneDriveForBusiness
                                                : SubtipoSharePoint.SharePointOnline)
                : null;

        var habilitadas = perfil is null
            ? Array.Empty<string>()
            : CatalogoVerificacoes.Definicoes.Where(x => x.Ambiente == perfil.Ambiente &&
                _checks[x.Codigo].Checked).Select(x => x.Codigo).ToArray();

        return new ConfiguracaoCompatibilidade(perfil, habilitadas);
    }

    private FiltroTamanhoPersonalizado? CriarFiltroPersonalizado()
    {
        if (!_ativarTamanho.Checked)
            return null;

        decimal fator = _unidade.SelectedIndex switch
        {
            0 => 1M, 1 => 1024M, 2 => 1048576M,
            3 => 1073741824M, 4 => 1099511627776M,
            _ => throw new InvalidOperationException("Selecione uma unidade de tamanho.")
        };
        // Arredondamento para cima evita sinalizar um arquivo no próprio limiar.
        var bytes = checked((long)decimal.Ceiling(_limite.Value * fator));
        return new FiltroTamanhoPersonalizado(bytes);
    }

    private async Task IniciarAnaliseAsync()
    {
        var pasta = _caminho.Text.Trim();
        if (!Directory.Exists(pasta))
        {
            MessageBox.Show(this, "Selecione uma pasta existente de testes.",
                "CLEAR", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        ConfiguracaoCompatibilidade configuracao;
        FiltroTamanhoPersonalizado? filtro;
        ClassificadorArquivos classificador;
        var usarSinalTemporal = _ativarSinalTemporal.Checked;
        var identificarVazios = _identificarVazios.Checked;
        var identificarTemporarios = _identificarTemporarios.Checked;
        var usarDuplicatas = _ativarDuplicatas.Checked;
        var exportarRelatorio = _exportarRelatorio.Checked;
        var salvarHistorico = _salvarHistorico.Checked;
        var diasSelecionados = (int)_dias.Value;
        var referenciaTemporalUtc = DateTimeOffset.UtcNow;
        ExportadorRelatoriosM5? exportador = null;
        SessaoHistoricoSqliteM6? historico = null;

        try
        {
            configuracao = CriarConfiguracaoDestino();
            filtro = CriarFiltroPersonalizado();
            classificador = ClassificadorArquivos.CriarComFiltrosSelecionados(
                identificarVazios,
                identificarTemporarios,
                usarSinalTemporal ? diasSelecionados : null,
                referenciaTemporalUtc);
            if (exportarRelatorio)
            {
                using var pastaSaida = new FolderBrowserDialog
                {
                    Description = "Escolha uma pasta de saída FORA do acervo analisado. " +
                                  "Os relatórios incluem caminhos e possíveis dados sensíveis.",
                    UseDescriptionForTitle = true,
                    ShowNewFolderButton = true,
                    SelectedPath = PastaInicialDeSelecao
                };
                if (pastaSaida.ShowDialog(this) != DialogResult.OK)
                    return;

                exportador = ExportadorRelatoriosM5.Iniciar(pasta,
                    pastaSaida.SelectedPath,
                    (FiltroLinhasRelatorio)_filtroRelatorio.SelectedIndex);
            }
        }
        catch (Exception ex) when (ex is ArgumentException or OverflowException or InvalidOperationException
                or IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(this, ex.Message, "Parâmetros inválidos",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _cancelamento = new CancellationTokenSource();
        var token = _cancelamento.Token;
        _selecionar.Enabled = false;
        _iniciar.Enabled = false;
        _cancelar.Enabled = true;
        _semDestino.Enabled = false;
        _azure.Enabled = false;
        _sharePoint.Enabled = false;
        _ativarSinalTemporal.Enabled = false;
        _ativarTamanho.Enabled = false;
        _identificarVazios.Enabled = false;
        _identificarTemporarios.Enabled = false;
        AtualizarHabilitacao();
        _grade.DataSource = null;
        _gradeDuplicatas.DataSource = null;
        _resumoDuplicatas.Text = usarDuplicatas
            ? "Aguardando o inventário para iniciar a busca de duplicatas..."
            : "Busca de duplicatas desativada. Marque o filtro geral para habilitar.";
        _ocorrencias.Items.Clear();
        _resumo.Text = "Inventariando metadados e aplicando verificações habilitadas...";

        var progresso = new Progress<ProgressoInventario>(p =>
            _estado.Text = $"Lidos: {p.QuantidadeArquivos:N0} arquivo(s), {p.TotalBytes:N0} bytes.");

        try
        {
            if (salvarHistorico)
            {
                var filtrosAtivos = new List<string>();
                if (identificarVazios) filtrosAtivos.Add("ArquivoVazio");
                if (identificarTemporarios) filtrosAtivos.Add("CopiaBackupOuTemporario");
                if (usarSinalTemporal) filtrosAtivos.Add("SinalTemporal");
                if (filtro is not null) filtrosAtivos.Add("TamanhoPersonalizado");
                if (usarDuplicatas) filtrosAtivos.Add("DuplicatasSHA256");
                var parametros = new ParametrosHistoricoM6(
                    filtrosAtivos,
                    configuracao.Perfil?.Ambiente.ToString(),
                    configuracao.Perfil?.Subtipo?.ToString(),
                    configuracao.CodigosHabilitados,
                    filtro?.LimiteBytes,
                    usarSinalTemporal ? diasSelecionados : null,
                    usarSinalTemporal ? referenciaTemporalUtc : null,
                    usarDuplicatas);

                historico = await SessaoHistoricoSqliteM6.IniciarAsync(
                    pasta, SessaoHistoricoSqliteM6.CaminhoPadrao, parametros, token);
            }

            IObservadorInventario? observador = exportador is not null && historico is not null
                ? new ObservadorInventarioComposto(exportador, historico)
                : (IObservadorInventario?)exportador ?? historico;

            var casoDeUso = new InventariarDiretorio(
                new FonteInventarioSistemaArquivos(), limiteAmostra: 1000,
                classificador: classificador,
                configuracaoCompatibilidade: configuracao,
                filtroPersonalizado: filtro,
                observador: observador);
            var resultado = await Task.Run(
                () => casoDeUso.ExecutarAsync(pasta, progresso, token), token);

            _grade.DataSource = resultado.AmostraAvaliacoes.Select(item =>
            {
                var arquivo = item.Entrada.Arquivo;
                var gerais = arquivo is null ? "" :
                    string.Join(", ", classificador.Classificar(arquivo).Categorias);
                var relevantes = item.Verificacoes.Where(x => x.Estado is
                    EstadoVerificacao.IncompatibilidadeDocumentada or
                    EstadoVerificacao.Alerta or EstadoVerificacao.NaoAvaliado).ToArray();

                return new
                {
                    Tipo = item.Entrada.Tipo == TipoEntradaInventario.Arquivo ? "Arquivo" : "Pasta",
                    Caminho = item.Entrada.CaminhoCompleto,
                    Bytes = item.Entrada.TamanhoBytes,
                    IndiciosGerais = gerais,
                    Compatibilidade = string.Join(" | ", relevantes.Select(x =>
                        x.CodigoRegra + ": " + x.Estado)),
                    Justificativas = string.Join(" | ", relevantes.Select(x =>
                        x.CodigoRegra + ": " + x.Justificativa)),
                    FiltroPersonalizado = item.AcimaDoLimitePersonalizado
                        ? "Acima do limite pessoal" : "",
                    ModificadoUtc = arquivo?.UltimaModificacaoUtc
                };
            }).ToList();

            foreach (var item in resultado.Ocorrencias)
                _ocorrencias.Items.Add($"{item.Tipo}: {item.Caminho} — {item.Descricao}");

            var parcial = resultado.Incompleto ? " | inventário parcial" : "";
            var exibicao = resultado.QuantidadeArquivos + resultado.QuantidadeDiretorios >
                resultado.AmostraAvaliacoes.Count ? " | amostra limitada a 1.000 entradas" : "";

            _resumo.Text =
                $"{resultado.QuantidadeArquivos:N0} arquivo(s) | {resultado.QuantidadeDiretorios:N0} pasta(s) | " +
                $"{resultado.TotalBytes:N0} bytes | {resultado.ArquivosComIndicios:N0} arquivo(s) com indícios gerais" +
                $" | {resultado.ItensComIncompatibilidade:N0} item(ns) com incompatibilidade de destino" +
                $" | {resultado.ArquivosAcimaLimitePersonalizado:N0} acima do limite pessoal" +
                $" | {resultado.TotalOcorrencias:N0} ocorrência(s){parcial}{exibicao}";

            var textoPerfil = configuracao.Perfil is null ? "Sem perfil de destino"
                : configuracao.Perfil.Ambiente == AmbienteDestino.AzureFilesClassicoSmb
                    ? "Azure Files SMB" : configuracao.Perfil.Subtipo?.ToString() ?? "SPO";

            var estado = !configuracao.HaVerificacoesHabilitadas
                ? "Nenhuma verificação de destino habilitada."
                : resultado.ApenasSemIncompatibilidadeDetectada
                    ? "Sem incompatibilidade detectada pelas verificações executadas (não é garantia de migração)."
                    : $"Resultados locais: {resultado.AvaliacoesExecutadas:N0} avaliações executadas; " +
                      $"{resultado.AvaliacoesNaoAvaliadas:N0} não avaliadas.";

            _estado.Text = $"{textoPerfil} | {estado} " +
                (usarSinalTemporal ? $"Sinal temporal: {diasSelecionados} dias. " : "") +
                "Nenhum arquivo alterado.";
            _dicas.SetToolTip(_estado, _estado.Text);

            ResultadoDuplicatas? resultadoDuplicatas = null;
            if (usarDuplicatas)
            {
                // A detecção usa outra varredura para não reter todos os caminhos
                // do inventário M3, cuja tabela segue limitada a 1.000 entradas.
                _resumoDuplicatas.Text = "Calculando grupos por tamanho e SHA-256; leitura somente...";
                var progressoDuplicatas = new Progress<ProgressoDuplicatas>(p =>
                    _resumoDuplicatas.Text =
                        $"Hash calculado: {p.CandidatosComHash:N0} de {p.ArquivosCandidatos:N0} candidatos.");
                var detectar = new DetectarDuplicatasExatas(
                    new FonteInventarioSistemaArquivos(),
                    new LeitorConteudoDuplicatasSistemaArquivos());
                resultadoDuplicatas = await Task.Run(
                    () => detectar.ExecutarAsync(pasta, progressoDuplicatas, token), token);
                var duplicatas = resultadoDuplicatas;

                // Apenas as primeiras 200 linhas são renderizadas. Os totais
                // incluem todos os grupos e arquivos validados.
                _gradeDuplicatas.DataSource = duplicatas.Grupos
                    .SelectMany((grupo, indice) => grupo.Arquivos.Select(arquivo => new
                    {
                        Grupo = indice + 1,
                        TamanhoBytes = grupo.TamanhoBytes,
                        Arquivo = arquivo.CaminhoCompleto,
                        Sha256 = grupo.Sha256Hex
                    }))
                    .Take(200).ToList();

                _resumoDuplicatas.Text =
                    $"{duplicatas.Grupos.Count:N0} grupo(s) exato(s) | " +
                    $"{duplicatas.ArquivosEmGrupos:N0} arquivo(s) em grupos | " +
                    $"{duplicatas.CandidatosPorTamanho:N0} candidato(s) por tamanho | " +
                    $"{duplicatas.ArquivosComHash:N0} hashes | " +
                    $"{duplicatas.BytesPotencialmenteRedundantes:N0} bytes potencialmente redundantes " +
                    "(estimativa; sem descarte)" +
                    (duplicatas.Incompleto
                        ? $" | ATENÇÃO: {duplicatas.TotalOcorrencias:N0} ocorrência(s), resultado parcial"
                        : "") +
                    (duplicatas.ArquivosEmGrupos > 200 ? " | exibindo apenas 200 linhas" : "");

                foreach (var ocorrencia in duplicatas.Ocorrencias)
                    _ocorrencias.Items.Add(
                        $"M4 — {ocorrencia.Tipo}: {ocorrencia.Caminho} — {ocorrencia.Descricao}");
                _estado.Text = duplicatas.Incompleto
                    ? "M4 parcial: houve falhas; nenhuma ação foi executada. " +
                      "Os grupos listados passaram por comparação byte a byte."
                    : "M4 concluído: SHA-256 e confirmação byte a byte. Nenhum arquivo alterado.";
            }

            if (exportador is not null)
            {
                _estado.Text = "Finalizando relatório CSV e resumo JSON...";
                var filtrosGerais = new List<string>();
                if (identificarVazios) filtrosGerais.Add("ArquivoVazio");
                if (identificarTemporarios) filtrosGerais.Add("CopiaBackupOuTemporario");
                if (usarSinalTemporal) filtrosGerais.Add("SinalTemporal");
                if (filtro is not null) filtrosGerais.Add("TamanhoPersonalizado");
                if (usarDuplicatas) filtrosGerais.Add("DuplicatasSHA256");

                var arquivos = await exportador.ConcluirAsync(resultado, resultadoDuplicatas,
                    filtrosGerais, usarSinalTemporal ? diasSelecionados : null,
                    usarSinalTemporal ? referenciaTemporalUtc : null, token);

                _estado.Text = $"M5 concluído: {arquivos.LinhasExportadas:N0} linha(s) CSV " +
                    "(amostra visual não limita o relatório). Arquivos originais inalterados.";
                MessageBox.Show(this,
                    "Relatório completo gerado:\n\n" +
                    "Inventário: " + arquivos.InventarioCsv + "\n" +
                    "Resumo: " + arquivos.ResumoJson +
                    (arquivos.DuplicatasCsv is null ? "" :
                        "\nDuplicatas: " + arquivos.DuplicatasCsv) +
                    "\n\nOs caminhos exportados podem ser sensíveis. Guarde os relatórios com cuidado.",
                    "CLEAR — Relatórios M5", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }

            if (historico is not null)
            {
                _estado.Text = "Confirmando transação SQLite da análise...";
                await historico.ConcluirAsync(resultado, resultadoDuplicatas, token);
                _estado.Text = $"Histórico M6 salvo: {historico.EntradasPersistidas:N0} entrada(s). " +
                    "Sem reutilização de classificações. Nenhum arquivo original alterado.";
                await AtualizarHistoricoAsync();
            }
        }
        catch (OperationCanceledException)
        {
            _resumo.Text = "Inventário cancelado; resultados parciais descartados.";
            _estado.Text = "Cancelado pelo usuário.";
        }
        catch (Exception ex)
        {
            _resumo.Text = "Inventário não concluído.";
            _estado.Text = "Falha: " + ex.Message;
            MessageBox.Show(this, ex.Message, "Falha no inventário",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally
        {
            _cancelamento.Dispose();
            _cancelamento = null;
            if (exportador is not null)
                await exportador.DisposeAsync();
            if (historico is not null)
                await historico.DisposeAsync();
            _selecionar.Enabled = true;
            _iniciar.Enabled = true;
            _cancelar.Enabled = false;
            _semDestino.Enabled = true;
            _azure.Enabled = true;
            _sharePoint.Enabled = true;
            _ativarSinalTemporal.Enabled = true;
            _ativarTamanho.Enabled = true;
            _identificarVazios.Enabled = true;
            _identificarTemporarios.Enabled = true;
            AtualizarHabilitacao();
        }
    }
}
