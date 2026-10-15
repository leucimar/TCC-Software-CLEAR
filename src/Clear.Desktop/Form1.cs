using Clear.Application.Inventario;
using Clear.Domain.Analise;
using Clear.Domain.Compatibilidade;
using Clear.Domain.Inventario;
using Clear.Infrastructure.Inventario;

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

    private readonly RadioButton _semDestino = new()
        { Text = "Sem destino (análise geral)", AutoSize = true, Checked = true };
    private readonly RadioButton _azure = new()
        { Text = "Azure Files (SMB/Windows)", AutoSize = true };
    private readonly RadioButton _sharePoint = new()
        { Text = "SharePoint / OneDrive empresarial", AutoSize = true };
    private readonly ComboBox _subtipo = new()
        { DropDownStyle = ComboBoxStyle.DropDownList, Width = 175 };
    private readonly FlowLayoutPanel _painelVerificacoes = new()
        { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true };
    private readonly Dictionary<string, CheckBox> _checks = new(StringComparer.Ordinal);

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
        Text = "CLEAR — Pré-análise de arquivos e compatibilidade de destinos (M3)";
        MinimumSize = new Size(1040, 680);
        Size = new Size(1350, 880);

        var principal = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 7,
            Padding = new Padding(12)
        };
        principal.RowStyles.Add(new RowStyle(SizeType.Absolute, 80));
        principal.RowStyles.Add(new RowStyle(SizeType.Absolute, 190));
        principal.RowStyles.Add(new RowStyle(SizeType.Absolute, 82));
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
            Text = "Somente leitura de metadados. Indícios não autorizam descarte nem garantem migração.",
            Dock = DockStyle.Fill, AutoSize = true
        };
        topo.Controls.Add(mensagem, 0, 0);
        topo.SetColumnSpan(mensagem, 4);
        topo.Controls.Add(_caminho, 0, 1);
        topo.Controls.Add(_selecionar, 1, 1);
        topo.Controls.Add(_iniciar, 2, 1);
        topo.Controls.Add(_cancelar, 3, 1);

        var seletor = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
        seletor.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 460));
        seletor.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        var grupoDestino = new GroupBox { Text = "Ambiente de destino (seleção exclusiva)", Dock = DockStyle.Fill };
        var radios = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown,
            WrapContents = false, Padding = new Padding(8)
        };
        radios.Controls.Add(_semDestino);
        radios.Controls.Add(_azure);
        radios.Controls.Add(_sharePoint);
        var subtipoLinha = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight };
        subtipoLinha.Controls.Add(new Label
            { Text = "Subtipo empresarial:", AutoSize = true, Padding = new Padding(0, 5, 0, 0) });
        _subtipo.Items.AddRange(new object[] { "SharePoint Online", "OneDrive for Business" });
        _subtipo.SelectedIndex = 0;
        subtipoLinha.Controls.Add(_subtipo);
        radios.Controls.Add(subtipoLinha);
        grupoDestino.Controls.Add(radios);

        var grupoRegras = new GroupBox
            { Text = "Verificações do destino selecionado — marque individualmente", Dock = DockStyle.Fill };
        grupoRegras.Controls.Add(_painelVerificacoes);

        foreach (var definicao in CatalogoVerificacoes.Definicoes)
        {
            var opcao = new CheckBox
            {
                Text = definicao.Rotulo + " [" + definicao.Codigo + "]",
                AutoSize = true,
                Checked = true
            };
            _dicas.SetToolTip(opcao, "Referência documental: " + definicao.FonteDocumental +
                ". Indícios locais; não há consulta ao serviço remoto.");
            _checks.Add(definicao.Codigo, opcao);
        }

        seletor.Controls.Add(grupoDestino, 0, 0);
        seletor.Controls.Add(grupoRegras, 1, 0);

        var grupoGeral = new GroupBox { Text = "Filtros gerais independentes do destino", Dock = DockStyle.Fill };
        var filtros = new FlowLayoutPanel
            { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = true,
              Padding = new Padding(8, 8, 8, 0) };
        filtros.Controls.Add(_ativarSinalTemporal);
        filtros.Controls.Add(_dias);
        filtros.Controls.Add(new Label
            { Text = "dias   |   ", AutoSize = true, Padding = new Padding(0, 5, 0, 0) });
        filtros.Controls.Add(_ativarTamanho);
        filtros.Controls.Add(_limite);
        _unidade.Items.AddRange(new object[] { "B", "KiB", "MiB", "GiB", "TiB" });
        _unidade.SelectedIndex = 3;
        filtros.Controls.Add(_unidade);
        grupoGeral.Controls.Add(filtros);
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
        principal.Controls.Add(seletor, 0, 1);
        principal.Controls.Add(grupoGeral, 0, 2);
        principal.Controls.Add(_resumo, 0, 3);
        principal.Controls.Add(_grade, 0, 4);
        principal.Controls.Add(areaOcorrencias, 0, 5);
        principal.Controls.Add(_estado, 0, 6);
        Controls.Add(principal);

        _semDestino.CheckedChanged += (_, _) => AtualizarPerfil();
        _azure.CheckedChanged += (_, _) => AtualizarPerfil();
        _sharePoint.CheckedChanged += (_, _) => AtualizarPerfil();
        _ativarSinalTemporal.CheckedChanged += (_, _) => AtualizarHabilitacao();
        _ativarTamanho.CheckedChanged += (_, _) => AtualizarHabilitacao();
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
        AtualizarPerfil();
        AtualizarHabilitacao();
    }

    private void AtualizarPerfil()
    {
        _painelVerificacoes.SuspendLayout();
        _painelVerificacoes.Controls.Clear();
        AmbienteDestino? ambiente = _azure.Checked
            ? AmbienteDestino.AzureFilesClassicoSmb
            : _sharePoint.Checked ? AmbienteDestino.SharePointOneDriveEmpresarial : null;

        if (ambiente is not null)
        {
            foreach (var definicao in CatalogoVerificacoes.Definicoes
                         .Where(x => x.Ambiente == ambiente))
                _painelVerificacoes.Controls.Add(_checks[definicao.Codigo]);
        }
        else
        {
            _painelVerificacoes.Controls.Add(new Label
            {
                Text = "Sem perfil: somente os filtros gerais serão executados.",
                AutoSize = true, Padding = new Padding(8)
            });
        }
        _painelVerificacoes.ResumeLayout();
        AtualizarHabilitacao();
    }

    private void AtualizarHabilitacao()
    {
        var pronto = _cancelamento is null;
        _subtipo.Enabled = pronto && _sharePoint.Checked;
        _painelVerificacoes.Enabled = pronto;
        _dias.Enabled = pronto && _ativarSinalTemporal.Checked;
        _limite.Enabled = pronto && _ativarTamanho.Checked;
        _unidade.Enabled = pronto && _ativarTamanho.Checked;
    }

    private void SelecionarPasta()
    {
        using var dialogo = new FolderBrowserDialog
        {
            Description = "Selecione uma pasta de testes sintéticos para análise somente leitura.",
            UseDescriptionForTitle = true,
            ShowNewFolderButton = false
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
        var diasSelecionados = (int)_dias.Value;

        try
        {
            configuracao = CriarConfiguracaoDestino();
            filtro = CriarFiltroPersonalizado();
            classificador = usarSinalTemporal
                ? ClassificadorArquivos.CriarComSinalTemporal(diasSelecionados, DateTimeOffset.UtcNow)
                : new ClassificadorArquivos();
        }
        catch (Exception ex) when (ex is ArgumentException or OverflowException or InvalidOperationException)
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
        AtualizarHabilitacao();
        _grade.DataSource = null;
        _ocorrencias.Items.Clear();
        _resumo.Text = "Inventariando metadados e aplicando verificações habilitadas...";

        var progresso = new Progress<ProgressoInventario>(p =>
            _estado.Text = $"Lidos: {p.QuantidadeArquivos:N0} arquivo(s), {p.TotalBytes:N0} bytes.");

        try
        {
            var casoDeUso = new InventariarDiretorio(
                new FonteInventarioSistemaArquivos(), limiteAmostra: 1000,
                classificador: classificador,
                configuracaoCompatibilidade: configuracao,
                filtroPersonalizado: filtro);
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
            _selecionar.Enabled = true;
            _iniciar.Enabled = true;
            _cancelar.Enabled = false;
            _semDestino.Enabled = true;
            _azure.Enabled = true;
            _sharePoint.Enabled = true;
            _ativarSinalTemporal.Enabled = true;
            _ativarTamanho.Enabled = true;
            AtualizarHabilitacao();
        }
    }
}
