using System.Diagnostics;
using Clear.Application.Inventario;
using Clear.Application.Progresso;
using Clear.Application.Compatibilidade;
using Clear.Application.Duplicatas;
using Clear.Domain.Analise;
using Clear.Domain.Compatibilidade;
using Clear.Domain.Inventario;
using Clear.Infrastructure.Inventario;
using Clear.Infrastructure.Duplicatas;
using Clear.Infrastructure.Relatorios;
using Clear.Infrastructure.Historico;
using Clear.Infrastructure.Execucao;

namespace Clear.Desktop;

/// <summary>
/// Seleção exclusiva de destino, verificações independentes e filtros gerais.
/// Toda a análise é somente leitura; não há upload, edição ou descarte.
/// </summary>
public partial class Form1 : Form
{
    private readonly TextBox _caminho = new() { Dock = DockStyle.Fill, ReadOnly = true };
    private readonly Button _selecionar = new() { Text = "Escolher pasta", Dock = DockStyle.Fill };
    private readonly TextBox _pastaQuarentena = new()
        { Width = 370, ReadOnly = true,
          Text = Path.Combine(Environment.GetFolderPath(
              Environment.SpecialFolder.LocalApplicationData), "CLEAR", "Quarentena") };
    private readonly TextBox _pastaRelatorios = new()
        { Width = 370, ReadOnly = true,
          Text = Path.Combine(Environment.GetFolderPath(
              Environment.SpecialFolder.LocalApplicationData), "CLEAR", "Relatorios") };
    private readonly Button _escolherQuarentena = new()
        { Text = "Escolher quarentena", AutoSize = true };
    private readonly Button _escolherRelatorios = new()
        { Text = "Escolher relatórios", AutoSize = true };
    private bool _quarentenaManual;
    private readonly Button _iniciar = new() { Text = "Analisar (somente leitura)", Dock = DockStyle.Fill };
    private readonly Button _cancelar = new() { Text = "Cancelar", Dock = DockStyle.Fill, Enabled = false };
    private readonly Button _criarSubBase = new()
        { Text = "Criar sub-base experimental (SOMENTE CÓPIA)", AutoSize = true };
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
    private readonly NumericUpDown _maximoHashes = new()
        { Minimum = 1, Maximum = 8, Value = 1, Width = 55, Enabled = false };
    private readonly NumericUpDown _limiteIndexacao = new()
        { Minimum = 100, Maximum = 1_000_000, Value = 250_000, Increment = 1000,
          ThousandsSeparator = true, Width = 105, Enabled = false };
    private readonly CheckBox _salvarCheckpoint = new()
        { Text = "Salvar progresso para reinício seguro (M9; sem cache)", AutoSize = true };
    private readonly Button _reiniciarInterrompida = new()
        { Text = "Reiniciar análise interrompida (do início)", AutoSize = true };
    private readonly TextBox _metricasM9 = new()
    {
        Dock = DockStyle.Fill, ReadOnly = true, Multiline = true,
        ScrollBars = ScrollBars.Vertical,
        Text = "M9: execute uma análise para ver durações e taxas observadas. " +
               "Os resultados dependem do hardware e dos dados usados."
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
    private readonly Button _consultarDetalhesHistorico = new()
    {
        Text = "Consultar detalhes da execução", AutoSize = true
    };
    private IReadOnlyList<ResumoExecucaoHistoricaM6> _execucoesHistoricas =
        Array.Empty<ResumoExecucaoHistoricaM6>();
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
        { Text = "Último acesso registrado há mais de", AutoSize = true };
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
        principal.RowStyles.Add(new RowStyle(SizeType.Absolute, 142));
        principal.RowStyles.Add(new RowStyle(SizeType.Absolute, 102));
        principal.RowStyles.Add(new RowStyle(SizeType.Absolute, 233));
        principal.RowStyles.Add(new RowStyle(SizeType.Absolute, 62));
        principal.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        principal.RowStyles.Add(new RowStyle(SizeType.Absolute, 118));
        principal.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));

        var topo = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 3 };
        topo.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        topo.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 125));
        topo.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190));
        topo.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 95));
        topo.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        topo.RowStyles.Add(new RowStyle(SizeType.Absolute, 39));
        topo.RowStyles.Add(new RowStyle(SizeType.Absolute, 63));
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
        var locais = new FlowLayoutPanel { Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight, WrapContents = true };
        locais.Controls.Add(new Label { Text = "Quarentena:", AutoSize = true });
        locais.Controls.Add(_pastaQuarentena);
        locais.Controls.Add(_escolherQuarentena);
        locais.Controls.Add(new Label { Text = "Relatórios:", AutoSize = true });
        locais.Controls.Add(_pastaRelatorios);
        locais.Controls.Add(_escolherRelatorios);
        topo.Controls.Add(locais, 0, 2);
        topo.SetColumnSpan(locais, 4);


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
        var linhaConcorrencia = new FlowLayoutPanel
            { AutoSize = true, WrapContents = false, FlowDirection = FlowDirection.LeftToRight };
        linhaConcorrencia.Controls.Add(new Label
            { Text = "Leituras SHA-256 simultâneas (M9):", AutoSize = true,
              Padding = new Padding(0, 5, 0, 0) });
        linhaConcorrencia.Controls.Add(_maximoHashes);
        filtrosGerais.Controls.Add(linhaConcorrencia);
        var linhaMemoria = new FlowLayoutPanel
            { AutoSize = true, WrapContents = false, FlowDirection = FlowDirection.LeftToRight };
        linhaMemoria.Controls.Add(new Label
            { Text = "Máximo de arquivos indexados em RAM (M9):", AutoSize = true,
              Padding = new Padding(0, 5, 0, 0) });
        linhaMemoria.Controls.Add(_limiteIndexacao);
        filtrosGerais.Controls.Add(linhaMemoria);

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
        filtrosGerais.Controls.Add(_salvarCheckpoint);
        filtrosGerais.Controls.Add(_reiniciarInterrompida);
        filtrosGerais.Controls.Add(_criarSubBase);
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
        botoesHistorico.Controls.Add(_consultarDetalhesHistorico);
        painelHistorico.Controls.Add(botoesHistorico, 0, 0);
        painelHistorico.Controls.Add(_gradeHistorico, 0, 1);
        painelHistorico.Controls.Add(_infoHistorico, 0, 2);
        abaHistorico.Controls.Add(painelHistorico);
        abas.TabPages.Add(abaHistorico);
        var abaMetricas = new TabPage("Métricas — M9");
        abaMetricas.Controls.Add(_metricasM9);
        abas.TabPages.Add(abaMetricas);

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
        _ativarDuplicatas.CheckedChanged += (_, _) => AtualizarHabilitacao();
        _atualizarHistorico.Click += async (_, _) => await AtualizarHistoricoAsync();
        _consultarDetalhesHistorico.Click += (_, _) => AbrirDetalhesHistorico();
        _gradeHistorico.CellDoubleClick += (_, _) => AbrirDetalhesHistorico();
        _selecionar.Click += (_, _) => SelecionarPasta();
        _escolherQuarentena.Click += (_, _) =>
        {
            if (EscolherPastaDeSaida(_pastaQuarentena, "Selecione a pasta da quarentena. " +
                    "Ela deve ficar FORA do acervo e no MESMO volume."))
                _quarentenaManual = true;
        };
        _escolherRelatorios.Click += (_, _) =>
            EscolherPastaDeSaida(_pastaRelatorios,
                "Selecione a pasta para relatórios CSV/JSON, fora do acervo.");
        _cancelar.Click += (_, _) => _cancelamento?.Cancel();
        _iniciar.Click += async (_, _) => await IniciarAnaliseAsync();
        _reiniciarInterrompida.Click += async (_, _) => await ReiniciarInterrompidaAsync();
        _criarSubBase.Click += (_, _) =>
        {
            using var janela = new FormSubBaseExperimental(_caminho.Text.Trim());
            janela.ShowDialog(this);
        };
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
        _maximoHashes.Enabled = pronto && _ativarDuplicatas.Checked;
        _limiteIndexacao.Enabled = pronto && _ativarDuplicatas.Checked;
        _salvarCheckpoint.Enabled = pronto;
        _reiniciarInterrompida.Enabled = pronto;
        _criarSubBase.Enabled = pronto;
        _escolherQuarentena.Enabled = pronto;
        _escolherRelatorios.Enabled = pronto;
        _exportarRelatorio.Enabled = pronto;
        _filtroRelatorio.Enabled = pronto && _exportarRelatorio.Checked;
        _salvarHistorico.Enabled = pronto;
        _atualizarHistorico.Enabled = pronto;
        _consultarDetalhesHistorico.Enabled = pronto;
    }

    private void AbrirDetalhesHistorico()
    {
        // Identifica pelo ID, não pelo índice visual: o usuário pode ordenar
        // a grade por qualquer coluna sem consultar a execução errada.
        var id = Convert.ToString(_gradeHistorico.CurrentRow?.Cells["Id"].Value);
        var execucao = _execucoesHistoricas.FirstOrDefault(x => x.Id == id);
        if (execucao is null)
        {
            MessageBox.Show(this, "Selecione uma execução do histórico.",
                "CLEAR — Histórico SQLite", MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }
        using var janela = new FormDetalhesHistoricoM6(
            execucao, SessaoHistoricoSqliteM6.CaminhoPadrao,
            _pastaQuarentena.Text);
        janela.ShowDialog(this);
    }

    private async Task AtualizarHistoricoAsync()
    {
        _atualizarHistorico.Enabled = false;
        try
        {
            var registros = await SessaoHistoricoSqliteM6.ConsultarUltimasAsync(
                SessaoHistoricoSqliteM6.CaminhoPadrao);
            _execucoesHistoricas = registros;
            _gradeHistorico.DataSource = registros.Select(x => new
            {
                x.InicioUtc, x.FimUtc, x.Estado, x.DiretorioRaiz,
                x.QuantidadeArquivos, x.QuantidadeDiretorios, x.TotalBytes,
                x.EntradasPersistidas, x.GruposDuplicatas, x.TotalOcorrencias,
                x.Id
            }).ToList();
            _infoHistorico.Text =
                $"{registros.Count} execução(ões). Selecione uma e clique Consultar detalhes. Banco: " +
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
        {
            _caminho.Text = dialogo.SelectedPath;
            if (!_quarentenaManual)
                _pastaQuarentena.Text = SugerirQuarentena(dialogo.SelectedPath);
        }
    }

    private static string SugerirQuarentena(string origem)
    {
        var normalizado = Path.TrimEndingDirectorySeparator(Path.GetFullPath(origem));
        var pai = Path.GetDirectoryName(normalizado);
        return Path.Combine(pai ?? Path.GetPathRoot(normalizado)!,
            "CLEAR_Quarentena");
    }

    private bool EscolherPastaDeSaida(TextBox destino, string descricao)
    {
        using var dialogo = new FolderBrowserDialog
        {
            Description = descricao, UseDescriptionForTitle = true,
            ShowNewFolderButton = true,
            SelectedPath = Directory.Exists(destino.Text)
                ? destino.Text : PastaInicialDeSelecao
        };
        if (dialogo.ShowDialog(this) != DialogResult.OK) return false;
        destino.Text = dialogo.SelectedPath;
        return true;
    }

    private static void ValidarDestinoSaida(string origem, string destino,
        bool exigirMesmoVolume)
    {
        var raiz = Path.GetFullPath(origem);
        if (string.IsNullOrWhiteSpace(destino) || !Path.IsPathFullyQualified(destino))
            throw new IOException("Selecione uma pasta de saída absoluta.");
        var saida = Path.GetFullPath(destino);
        var relativo = Path.GetRelativePath(raiz, saida);
        if (!Path.IsPathRooted(relativo) &&
            relativo != ".." &&
            !relativo.StartsWith(".." + Path.DirectorySeparatorChar,
                StringComparison.Ordinal))
            throw new IOException("A pasta de saída não pode ficar dentro do acervo.");
        // Não admitir a pasta da saída como ancestral da origem.
        var inverso = Path.GetRelativePath(saida, raiz);
        if (!Path.IsPathRooted(inverso) && inverso != ".." &&
            !inverso.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw new IOException("A saída não pode conter a origem.");
        if (exigirMesmoVolume &&
            !string.Equals(Path.GetPathRoot(raiz), Path.GetPathRoot(saida),
                StringComparison.OrdinalIgnoreCase))
            throw new IOException("A quarentena deve ficar no mesmo volume da origem.");
        for (DirectoryInfo? item = new(saida); item is not null; item = item.Parent)
            if (item.Exists && (item.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new IOException("A pasta de saída não pode conter links/junctions.");
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

    private string AssinaturaConfiguracaoM9()
    {
        var destino = CriarConfiguracaoDestino();
        var filtro = CriarFiltroPersonalizado();
        return string.Join("\n", new[]
        {
            "M9-parte2-v1",
            $"Vazios={_identificarVazios.Checked}",
            $"Temporarios={_identificarTemporarios.Checked}",
            $"Temporal={_ativarSinalTemporal.Checked}",
            $"Dias={(_ativarSinalTemporal.Checked ? _dias.Value : 0)}",
            $"LimiteBytes={filtro?.LimiteBytes.ToString() ?? "desativado"}",
            $"Duplicatas={_ativarDuplicatas.Checked}",
            $"Hashes={(_ativarDuplicatas.Checked ? _maximoHashes.Value : 0)}",
            $"Indexacao={(_ativarDuplicatas.Checked ? _limiteIndexacao.Value : 0)}",
            $"Relatorio={_exportarRelatorio.Checked}",
            $"FiltroRelatorio={(_exportarRelatorio.Checked ? _filtroRelatorio.SelectedIndex : -1)}",
            $"Historico={_salvarHistorico.Checked}",
            $"Perfil={destino.Perfil?.Ambiente}",
            $"Subtipo={destino.Perfil?.Subtipo}",
            $"Regras={string.Join(",", destino.CodigosHabilitados.OrderBy(x => x, StringComparer.Ordinal))}"
        });
    }

    private async Task ReiniciarInterrompidaAsync()
    {
        if (_cancelamento is not null) return;
        try
        {
            var caminho = _caminho.Text.Trim();
            if (!Directory.Exists(caminho))
                throw new DirectoryNotFoundException("Selecione a pasta original da análise.");
            var anterior = CheckpointAnaliseM9.Consultar(caminho)
                ?? throw new InvalidOperationException("Não existe checkpoint para esta pasta.");
            if (anterior.AssinaturaConfiguracao !=
                CheckpointAnaliseM9.Assinatura(AssinaturaConfiguracaoM9()))
                throw new InvalidOperationException(
                    "A configuração atual difere da análise interrompida. " +
                    "Selecione as opções originais ou inicie uma NOVA análise normalmente.");
            if (!anterior.PodeReiniciar)
                throw new InvalidOperationException("Esta análise não está interrompida.");
            var escolha = MessageBox.Show(this,
                "Reiniciar a análise interrompida DO INÍCIO?" + Environment.NewLine +
                "Estado anterior: " + anterior.Estado + Environment.NewLine +
                $"Último progresso persistido: {anterior.ArquivosLidos:N0} arquivos." +
                Environment.NewLine + Environment.NewLine +
                "Nenhuma classificação, hash ou resultado parcial será reutilizado. " +
                "O CLEAR fará NOVA leitura do diretório e aplicará as regras atuais.",
                "CLEAR — Reinício seguro M9", MessageBoxButtons.YesNo,
                MessageBoxIcon.Question, MessageBoxDefaultButton.Button2);
            if (escolha != DialogResult.Yes) return;
            _salvarCheckpoint.Checked = true;
            await IniciarAnaliseAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Reinício seguro M9",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private sealed class ProgressoComCheckpoint(
        IProgress<ProgressoInventario> exibicao, CheckpointAnaliseM9? checkpoint)
        : IProgress<ProgressoInventario>
    {
        public void Report(ProgressoInventario valor)
        {
            // A gravação executa na thread de inventário: se falhar,
            // interrompe toda a análise (nunca publica resultado falso).
            checkpoint?.Atualizar(valor.QuantidadeArquivos, valor.TotalBytes);
            exibicao.Report(valor);
        }
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
        var maximoHashesSimultaneos = (int)_maximoHashes.Value;
        var maximoArquivosIndexados = (int)_limiteIndexacao.Value;
        var salvarCheckpoint = _salvarCheckpoint.Checked;
        var exportarRelatorio = _exportarRelatorio.Checked;
        var salvarHistorico = _salvarHistorico.Checked;
        var diasSelecionados = (int)_dias.Value;
        var referenciaTemporalUtc = DateTimeOffset.UtcNow;
        ExportadorRelatoriosM5? exportador = null;
        SessaoHistoricoSqliteM6? historico = null;
        CheckpointAnaliseM9? checkpoint = null;

        try
        {
            configuracao = CriarConfiguracaoDestino();
            filtro = CriarFiltroPersonalizado();
            classificador = ClassificadorArquivos.CriarComFiltrosSelecionados(
                identificarVazios,
                identificarTemporarios,
                usarSinalTemporal ? diasSelecionados : null,
                referenciaTemporalUtc);
            ValidarDestinoSaida(pasta, _pastaQuarentena.Text,
                exigirMesmoVolume: true);
            if (exportarRelatorio)
            {
                ValidarDestinoSaida(pasta, _pastaRelatorios.Text,
                    exigirMesmoVolume: false);
                Directory.CreateDirectory(_pastaRelatorios.Text);
                exportador = ExportadorRelatoriosM5.Iniciar(pasta,
                    _pastaRelatorios.Text,
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
        _metricasM9.Text = "Análise em andamento. Tempos serão publicados apenas após conclusão.";
        var cronometroTotal = Stopwatch.StartNew();
        TimeSpan duracaoInventario = TimeSpan.Zero;
        TimeSpan duracaoDuplicatas = TimeSpan.Zero;
        _resumo.Text = "Inventariando metadados e aplicando verificações habilitadas...";

        long totalArquivosPrevistos = 0;
        var relogioInventario = Stopwatch.StartNew();
        var progressoUi = new Progress<ProgressoInventario>(p =>
        {
            var previsao = EstimativaRestante.Calcular(
                p.QuantidadeArquivos, totalArquivosPrevistos, 0, 0,
                relogioInventario.Elapsed);
            _estado.Text = $"Inventário/classificação: {p.QuantidadeArquivos:N0}" +
                (totalArquivosPrevistos > 0 ? $"/{totalArquivosPrevistos:N0}" : "") +
                $" arquivo(s) | {p.TotalBytes:N0} bytes | " +
                $"decorrido {relogioInventario.Elapsed.ToString(@"hh\:mm\:ss")}" +
                $" | {(totalArquivosPrevistos > 0
                    ? EstimativaRestante.Exibir(previsao) : "ETA: indisponível")}";
        });
        if (salvarCheckpoint)
        {
            try
            {
                checkpoint = CheckpointAnaliseM9.Iniciar(pasta, AssinaturaConfiguracaoM9());
            }
            catch (Exception ex)
            {
                _estado.Text = "Checkpoint M9 indisponível: " + ex.Message;
                _metricasM9.Text = "Análise não iniciada; marcador de progresso falhou.";
                // Sem prosseguir silenciosamente sem a opção solicitada.
                _cancelamento.Dispose();
                _cancelamento = null;
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
                if (exportador is not null) await exportador.DisposeAsync();
                return;
            }
        }
        var progresso = new ProgressoComCheckpoint(progressoUi, checkpoint);

        try
        {
            _estado.Text = "Pré-contando arquivos para estimar o inventário (somente leitura)...";
            try
            {
                totalArquivosPrevistos = await Task.Run(() =>
                {
                    var opcoes = new EnumerationOptions
                    {
                        RecurseSubdirectories = true,
                        IgnoreInaccessible = true,
                        AttributesToSkip = FileAttributes.ReparsePoint
                    };
                    long total = 0;
                    foreach (var arquivo in Directory.EnumerateFiles(
                        pasta, "*", opcoes))
                    {
                        token.ThrowIfCancellationRequested();
                        total = checked(total + 1);
                    }
                    return total;
                }, token);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Sem população conhecida não fabricar ETA. Inventário oficial
                // continua tratando suas próprias falhas/ocorrências.
                totalArquivosPrevistos = 0;
            }
            relogioInventario.Restart();
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
            duracaoInventario = relogioInventario.Elapsed;

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
                (usarSinalTemporal ? $"Sinal temporal por ÚLTIMO ACESSO (metadado Windows): {diasSelecionados} dias. " : "") +
                "Nenhum arquivo alterado.";
            _dicas.SetToolTip(_estado, _estado.Text);

            ResultadoDuplicatas? resultadoDuplicatas = null;
            if (usarDuplicatas)
            {
                // A detecção usa outra varredura para não reter todos os caminhos
                // do inventário M3, cuja tabela segue limitada a 1.000 entradas.
                _resumoDuplicatas.Text = "Calculando grupos por tamanho e SHA-256; leitura somente...";
                var relogioSha = Stopwatch.StartNew();
                var progressoDuplicatas = new Progress<ProgressoDuplicatas>(p =>
                {
                    var eta = EstimativaRestante.Calcular(p.CandidatosComHash,
                        p.ArquivosCandidatos, 0, 0, relogioSha.Elapsed);
                    _resumoDuplicatas.Text =
                        $"SHA-256: {p.CandidatosComHash:N0} de {p.ArquivosCandidatos:N0} " +
                        $"candidatos | {EstimativaRestante.Exibir(eta)} " +
                        "(somente fase de hashes; não inclui confirmação byte a byte).";
                });
                var detectar = new DetectarDuplicatasExatas(
                    new FonteInventarioSistemaArquivos(),
                    new LeitorConteudoDuplicatasSistemaArquivos(),
                    maximoHashesSimultaneos, maximoArquivosIndexados);
                var relogioDuplicatas = Stopwatch.StartNew();
                resultadoDuplicatas = await Task.Run(
                    () => detectar.ExecutarAsync(pasta, progressoDuplicatas, token), token);
                duracaoDuplicatas = relogioDuplicatas.Elapsed;
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

            checkpoint?.MarcarConcluida();
            cronometroTotal.Stop();
            double segundos = duracaoInventario.TotalSeconds;
            double taxaArquivos = segundos > 0 ? resultado.QuantidadeArquivos / segundos : 0;
            double taxaMiB = segundos > 0
                ? resultado.TotalBytes / 1048576.0 / segundos : 0;
            _metricasM9.Text =
                "MEDIÇÕES OBSERVADAS (não são benchmark validado)" + Environment.NewLine +
                "Arquivos: " + resultado.QuantidadeArquivos.ToString("N0") +
                " | Pastas: " + resultado.QuantidadeDiretorios.ToString("N0") +
                " | Volume associado aos arquivos inventariados: " +
                resultado.TotalBytes.ToString("N0") + Environment.NewLine +
                "Inventário: " + duracaoInventario.TotalMilliseconds.ToString("F1") + " ms" +
                " | Taxa: " + taxaArquivos.ToString("F1") + " arquivos/s" +
                " | Volume inventariado: " + taxaMiB.ToString("F2") + " MiB/s" +
                Environment.NewLine +
                "Duplicatas (inclui segunda enumeração e SHA-256): " +
                duracaoDuplicatas.TotalMilliseconds.ToString("F1") + " ms" +
                " | Leitores SHA-256 simultâneos: " +
                (usarDuplicatas ? maximoHashesSimultaneos.ToString() : "desativado") +
                Environment.NewLine +
                "Total da análise (inclui relatórios/histórico quando ativos): " +
                cronometroTotal.Elapsed.TotalMilliseconds.ToString("F1") + " ms" +
                Environment.NewLine +
                "Inventário parcial: " + resultado.Incompleto +
                " | Duplicatas parciais: " + (resultadoDuplicatas?.Incompleto ?? false) +
                Environment.NewLine +
                "Medidas dependem de cache do SO, hardware, antivírus e outras cargas." +
                Environment.NewLine +
                "Apenas leitura e medição, sem alterações nos arquivos do acervo.";
        }
        catch (OperationCanceledException)
        {
            try { checkpoint?.MarcarCancelada(); }
            catch (Exception ex) { _estado.Text = "Falha ao registrar cancelamento: " + ex.Message; }
            _resumo.Text = "Inventário cancelado; resultados parciais descartados.";
            _estado.Text = "Cancelado pelo usuário.";
            _metricasM9.Text = "Análise cancelada; métricas incompletas não publicadas.";
        }
        catch (Exception ex)
        {
            try { checkpoint?.MarcarFalha(); }
            catch (Exception erro) { _metricasM9.Text = "Falha ao salvar checkpoint: " + erro.Message; }
            _resumo.Text = "Inventário não concluído.";
            _estado.Text = "Falha: " + ex.Message;
            _metricasM9.Text = "Análise interrompida por falha; métricas inconclusivas.";
            MessageBox.Show(this, ex.Message, "Falha no inventário",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally
        {
            try { checkpoint?.Dispose(); }
            catch (Exception ex)
            {
                _estado.Text = "Atenção: falha ao fechar checkpoint: " + ex.Message;
            }
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
