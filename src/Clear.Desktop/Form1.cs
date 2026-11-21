using Clear.Domain.Arquivos;
using Clear.Infrastructure.Arquivos;
using Clear.Application.Metricas;
using Clear.Infrastructure.Metricas;
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
    private readonly Label _estado = new() { Dock = DockStyle.Fill, AutoEllipsis = true };
    private readonly ToolTip _dicas = new();
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
    private readonly CheckBox _exportarRelatorio = new()
    {
        Text = "Exportar relatório completo (CSV + JSON — M5)",
        AutoSize = true, Checked = false
    };
    private readonly ComboBox _filtroRelatorio = new()
    {
        DropDownStyle = ComboBoxStyle.DropDownList, Width = 190, Enabled = false
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
    private readonly TextBox _caminhoBiblioteca = new()
        { PlaceholderText = "Ex.: sites/Equipe/Documentos", MaxLength = 400, AccessibleName = "Caminho da biblioteca" };
    private readonly TextBox _subpastaDestino = new()
        { PlaceholderText = "Em branco para a raiz", MaxLength = 2048, AccessibleName = "Subpasta no destino" };
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
        Font = new Font("Segoe UI", 10);
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96, 96);
        MontarInterface();
        PerformAutoScale();
        var area = Screen.FromControl(this).WorkingArea;
        MinimumSize = new Size(Math.Min(MinimumSize.Width, area.Width), Math.Min(MinimumSize.Height, area.Height));
        Size = new Size(Math.Min(Width, area.Width), Math.Min(Height, area.Height));
    }

    private void MontarInterface()
    {
        MontarApresentacao();

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
        Shown += async (_, _) => await AtualizarAnalisesAsync();
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
            if (_inventario.Ocupado || _carregandoAnalises || _auditando) { args.Cancel = true; return; }
            if (_cancelamento is not null)
            {
                _cancelamento.Cancel();
                args.Cancel = true;
                _estado.Text = "Aguardando cancelamento para fechar...";
            }
        };

        AplicarRecomendacoesDoDestino();
        AtualizarHabilitacao();
        TemaVisual.Registrar(this);
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
        _criterioTemporal.Enabled = pronto && _ativarSinalTemporal.Checked;
        _politicaPreservacao.Enabled = pronto;
        _tentarAdministrador.Enabled = pronto && !Administrador();
        _tentarAdministrador.Text = Administrador() ? "Executando como administrador" : "Tentar como administrador";
        _subtipo.Enabled = pronto && _sharePoint.Checked;
        _caminhoBiblioteca.Enabled = pronto && _sharePoint.Checked;
        _subpastaDestino.Enabled = pronto && (_azure.Checked || _sharePoint.Checked);
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
        AtualizarAcoesDaTela();
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
                ? PerfilDestino.SharePointEmpresarial(SubtipoSharePoint.SharePointOnline)
                : null;

        var habilitadas = perfil is null
            ? Array.Empty<string>()
            : CatalogoVerificacoes.Definicoes.Where(x => x.Ambiente == perfil.Ambiente &&
                _checks[x.Codigo].Checked).Select(x => x.Codigo).ToArray();

        return new ConfiguracaoCompatibilidade(perfil, habilitadas,
            _sharePoint.Checked && !string.IsNullOrWhiteSpace(_caminhoBiblioteca.Text)
                ? _caminhoBiblioteca.Text : null,
            perfil is not null ? _subpastaDestino.Text : null);
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
            "M9-datas-preservacao-v2",
            $"CriterioTemporal={CriterioSelecionado}",
            $"PoliticaPreservacao={PoliticaSelecionada}",
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
            "Historico=True",
            $"Perfil={destino.Perfil?.Ambiente}",
            $"Subtipo={destino.Perfil?.Subtipo}",
            $"CatalogoDestino={CatalogoVerificacoes.Versao}",
            $"Biblioteca={destino.CaminhoBibliotecaDecodificado}",
            $"SubpastaDestino={destino.SubpastaDestino}",
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
        if (_cancelamento is not null || _inventario.Ocupado || _carregandoAnalises || _auditando) return;
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
        var criterioSelecionado = CriterioSelecionado;
        var politicaSelecionada = PoliticaSelecionada;
        if (politicaSelecionada == PoliticaSemPreservacao.LerComAviso && MessageBox.Show(this,
            "Você autoriza a leitura do conteúdo quando o Windows não aceitar a preservação do último acesso? " +
            "Nesses casos a data de acesso poderá mudar. Cada tentativa será registrada. " +
            "A autorização vale somente para esta análise.", "Leitura sem garantia de preservação",
            MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes)
            return;
        SessaoPreservacaoAcesso? preservacao = null;
        var caminhoAuditoria = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CLEAR", "Preservacao", DateTimeOffset.UtcNow.ToString("yyyyMMdd_HHmmss") + "_" + Guid.NewGuid().ToString("N") + ".json");
        var identificarVazios = _identificarVazios.Checked;
        var identificarTemporarios = _identificarTemporarios.Checked;
        var usarDuplicatas = _ativarDuplicatas.Checked;
        var maximoHashesSimultaneos = (int)_maximoHashes.Value;
        var maximoArquivosIndexados = (int)_limiteIndexacao.Value;
        var salvarCheckpoint = _salvarCheckpoint.Checked;
        var exportarRelatorio = _exportarRelatorio.Checked;
        var diasSelecionados = (int)_dias.Value;
        var referenciaTemporalLocal = DateTimeOffset.Now;
        var referenciaTemporalUtc = referenciaTemporalLocal.ToUniversalTime();
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
                referenciaTemporalLocal, criterioSelecionado);
            ValidarDestinoSaida(pasta, caminhoAuditoria, exigirMesmoVolume: false);
            Directory.CreateDirectory(Path.GetDirectoryName(caminhoAuditoria)!);
            // A análise é somente leitura. O destino de quarentena é validado
            // pelo serviço M7 somente ao preparar uma movimentação confirmada.
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
        var inicioMedicaoUtc = DateTimeOffset.UtcNow;
        var configuracaoMedicao = AssinaturaConfiguracaoM9();
        var cronometroTotal = Stopwatch.StartNew();
        TimeSpan duracaoInventario = TimeSpan.Zero;
        TimeSpan duracaoDuplicatas = TimeSpan.Zero;
        _estado.Text = "Inventariando metadados e aplicando verificações habilitadas…";

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
        try
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
                usarDuplicatas,
                usarSinalTemporal ? criterioSelecionado.ToString() + "Utc" : null,
                politicaSelecionada.ToString(),
                CatalogoVerificacoes.Versao, configuracao.CaminhoBibliotecaDecodificado,
                configuracao.SubpastaDestino, usarDuplicatas ? maximoHashesSimultaneos : null,
                usarDuplicatas ? maximoArquivosIndexados : null, exportarRelatorio, salvarCheckpoint);

            historico = await Task.Run(() => SessaoHistoricoSqliteM6.IniciarAsync(
                pasta, BancoHistorico, parametros, CancellationToken.None));
            if (salvarCheckpoint) checkpoint = CheckpointAnaliseM9.Iniciar(pasta, configuracaoMedicao);
            var progresso = new ProgressoComCheckpoint(progressoUi, checkpoint);
            preservacao = new SessaoPreservacaoAcesso(politicaSelecionada, Path.ChangeExtension(caminhoAuditoria, ".jsonl"),
                usarSinalTemporal ? criterioSelecionado.ToString() : "Desativado");
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
            IObservadorInventario observador = exportador is null
                ? historico : new ObservadorInventarioComposto(exportador, historico);

            var casoDeUso = new InventariarDiretorio(
                new FonteInventarioSistemaArquivos(), limiteAmostra: 1000,
                classificador: classificador,
                configuracaoCompatibilidade: configuracao,
                filtroPersonalizado: filtro,
                observador: observador);
            var resultado = await Task.Run(
                () => casoDeUso.ExecutarAsync(pasta, progresso, token), token);
            duracaoInventario = relogioInventario.Elapsed;

            var textoPerfil = configuracao.Perfil is null ? "Sem perfil de destino"
                : configuracao.Perfil.Ambiente == AmbienteDestino.AzureFilesClassicoSmb
                    ? "Azure Files SMB" : "SharePoint";

            var estado = !configuracao.HaVerificacoesHabilitadas
                ? "Nenhuma verificação de destino habilitada."
                : resultado.ApenasSemIncompatibilidadeDetectada
                    ? "Sem incompatibilidade detectada pelas verificações executadas (não é garantia de migração)."
                    : $"Resultados locais: {resultado.AvaliacoesExecutadas:N0} avaliações executadas; " +
                      $"{resultado.AvaliacoesNaoAvaliadas:N0} não avaliadas.";

            _estado.Text = $"{textoPerfil} | {estado} " +
                (usarSinalTemporal ? $"Sinal temporal por {criterioSelecionado}: {diasSelecionados} dias. " : "") +
                "Conteúdo dos arquivos inalterado.";
            _dicas.SetToolTip(_estado, _estado.Text);

            ResultadoDuplicatas? resultadoDuplicatas = null;
            if (usarDuplicatas)
            {
                // A detecção usa outra varredura para não reter todos os caminhos
                // do inventário M3, cuja tabela segue limitada a 1.000 entradas.
                _estado.Text = "Calculando grupos por tamanho e SHA-256; leitura somente...";
                var relogioSha = Stopwatch.StartNew();
                var progressoDuplicatas = new Progress<ProgressoDuplicatas>(p =>
                {
                    var eta = EstimativaRestante.Calcular(p.CandidatosProcessados,
                        p.ArquivosCandidatos, 0, 0, relogioSha.Elapsed);
                    _estado.Text =
                        $"SHA-256: {p.CandidatosProcessados:N0} de {p.ArquivosCandidatos:N0} processados " +
                        $"({p.CandidatosComHash:N0} hashes; {p.Falhas:N0} falhas) | " +
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

                _estado.Text = duplicatas.Incompleto
                    ? "M4 parcial: houve falhas; nenhuma ação foi executada. " +
                      "Os grupos listados passaram por comparação byte a byte."
                    : "M4 concluído: SHA-256 e confirmação byte a byte. Conteúdo dos arquivos inalterado.";
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
                    usarSinalTemporal ? referenciaTemporalUtc : null, token, criterioSelecionado, preservacao.Resumo);

                _estado.Text = $"M5 concluído: {arquivos.LinhasExportadas:N0} linha(s) CSV " +
                    "(amostra visual não limita o relatório). Conteúdo dos arquivos originais inalterado.";
            }

            _estado.Text = "Salvando a análise no histórico…";
            historico.PreservacaoAcesso = preservacao.Resumo;
            await historico.ConcluirAsync(resultado, resultadoDuplicatas, token);
            checkpoint?.MarcarConcluida();
            cronometroTotal.Stop();
            // Salvar medições não deve converter uma análise concluída em falha.
            string estadoMetricas;
            try
            {
                var categoriasMedidas = new List<CategoriaAnalise>();
                if (identificarVazios) categoriasMedidas.Add(CategoriaAnalise.ArquivoVazio);
                if (identificarTemporarios) categoriasMedidas.Add(CategoriaAnalise.CopiaBackupOuTemporario);
                if (usarSinalTemporal) categoriasMedidas.Add(CategoriaAnalise.SinalTemporal);
                var medicao = MetricasAnalise.Criar(pasta, inicioMedicaoUtc, resultado,
                    resultadoDuplicatas, duracaoInventario, duracaoDuplicatas,
                    cronometroTotal.Elapsed, maximoHashesSimultaneos, configuracaoMedicao, categoriasMedidas) with
                    { Id = historico.IdExecucao, CriterioTemporal = usarSinalTemporal ? criterioSelecionado.ToString() : "Desativado",
                      PreservacaoAcesso = preservacao.Resumo };
                var bancoMetricas = RepositorioMetricasAnalise.CaminhoPadrao;
                await RepositorioMetricasAnalise.SalvarAsync(bancoMetricas, medicao);
                estadoMetricas = "Métricas persistidas em: " + bancoMetricas;
                if (exportarRelatorio)
                {
                    var csvMetricas = Path.Combine(_pastaRelatorios.Text,
                        "CLEAR_Metricas_" + medicao.Id + ".csv");
                    await RepositorioMetricasAnalise.ExportarCsvAsync(csvMetricas, new[] { medicao });
                    var consolidado = Path.Combine(_pastaRelatorios.Text,
                        "CLEAR_Metricas_Historico_" + medicao.Id + ".csv");
                    var medicoesSalvas = await RepositorioMetricasAnalise.ConsultarAsync(bancoMetricas);
                    await RepositorioMetricasAnalise.ExportarCsvAsync(consolidado, medicoesSalvas);
                    estadoMetricas += " | Tabela Excel: " + csvMetricas + " | Consolidado: " + consolidado;
                }
            }
            catch (Exception ex)
            {
                estadoMetricas = "A análise concluiu, mas houve falha ao salvar/exportar métricas: " + ex.Message;
            }
            _estado.Text = (resultado.Incompleto || resultadoDuplicatas?.Incompleto == true
                ? "Concluída com ocorrências · Inventário / Ocorrências." : "Análise concluída e salva no histórico.") +
                (exportador is not null ? " Relatórios em: " + _pastaRelatorios.Text : "") +
                (estadoMetricas.StartsWith("A análise", StringComparison.Ordinal) ? " " + estadoMetricas : "");
            _dicas.SetToolTip(_estado, _estado.Text + Environment.NewLine + estadoMetricas);
            DefinirConfiguracoesExpandidas(false);
            _abasResultados.SelectedIndex = 0;
        }
        catch (OperationCanceledException)
        {
            _estado.Text = "Análise cancelada pelo usuário.";
            try { checkpoint?.MarcarCancelada(); }
            catch (Exception erro) { _estado.Text += " Falha no checkpoint: " + erro.Message; }
            await RegistrarInterrupcaoAsync(historico, true, "Cancelada pelo usuário.");
        }
        catch (Exception ex)
        {
            _estado.Text = "Falha: " + ex.Message;
            try { checkpoint?.MarcarFalha(); }
            catch (Exception erro) { _estado.Text += " Falha no checkpoint: " + erro.Message; }
            await RegistrarInterrupcaoAsync(historico, false, ex.Message);
            MessageBox.Show(this, ex.Message, "Falha no inventário",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally
        {
            if (preservacao is not null)
            {
                var resumoPreservacao = preservacao.Resumo;
                if (resumoPreservacao.LeiturasIgnoradas > 0 || resumoPreservacao.LeiturasSemGarantia > 0)
                    _estado.Text += " Houve conteúdo ignorado ou lido sem garantia. Consulte Inventário / Ocorrências.";
                _dicas.SetToolTip(_estado, _estado.Text);
                try { await preservacao.SalvarAsync(caminhoAuditoria); }
                catch (Exception erro)
                {
                    _estado.Text += " FALHA ao salvar auditoria de preservação: " + erro.Message;
                }
                finally { preservacao.Dispose(); }
            }
            try { checkpoint?.Dispose(); }
            catch (Exception ex)
            {
                _estado.Text = "Atenção: falha ao fechar checkpoint: " + ex.Message;
            }
            if (exportador is not null)
                await exportador.DisposeAsync();
            if (historico is not null)
            {
                await historico.DisposeAsync();
                await AtualizarAnalisesAsync(historico.IdExecucao);
            }
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
        }
    }

    private async Task RegistrarInterrupcaoAsync(SessaoHistoricoSqliteM6? historico, bool cancelada, string motivo)
    {
        if (historico is null) return;
        try { await historico.RegistrarInterrupcaoAsync(cancelada, motivo); }
        catch (Exception erro) { _estado.Text += " Não foi possível salvar a tentativa: " + erro.Message; }
    }
}
