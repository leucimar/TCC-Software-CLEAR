using Clear.Application.Inventario;
using Clear.Domain.Analise;
using Clear.Infrastructure.Inventario;

namespace Clear.Desktop;

/// <summary>Inventário e classificação inicial do M3; nenhuma operação modifica arquivos.</summary>
public partial class Form1 : Form
{
    private readonly TextBox _caminho = new() { Dock = DockStyle.Fill, ReadOnly = true };
    private readonly Button _selecionar = new() { Text = "Escolher pasta", Dock = DockStyle.Fill };
    private readonly Button _iniciar = new() { Text = "Analisar (somente leitura)", Dock = DockStyle.Fill };
    private readonly Button _cancelar = new() { Text = "Cancelar", Dock = DockStyle.Fill, Enabled = false };
    private readonly Label _resumo = new() { Dock = DockStyle.Fill, AutoSize = false };
    private readonly Label _estado = new() { Dock = DockStyle.Fill, AutoSize = false };
    private readonly DataGridView _grade = new()
    {
        Dock = DockStyle.Fill,
        ReadOnly = true,
        AllowUserToAddRows = false,
        AllowUserToDeleteRows = false,
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
        RowHeadersVisible = false,
        MultiSelect = false
    };
    private readonly ListBox _ocorrencias = new()
    {
        Dock = DockStyle.Fill,
        HorizontalScrollbar = true
    };

    private readonly CheckBox _ativarSinalTemporal = new()
    {
        Text = "Sinal temporal (opcional): última modificação há mais de",
        AutoSize = true,
        Checked = false
    };
    private readonly NumericUpDown _diasSemModificacao = new()
    {
        Minimum = 30,
        Maximum = 3650,
        Value = 365,
        Width = 85,
        Enabled = false
    };

    private CancellationTokenSource? _cancelamento;

    public Form1()
    {
        InitializeComponent();
        MontarInterface();
    }

    private void MontarInterface()
    {
        Text = "CLEAR — Inventário e classificação (M3: sinais temporais)";
        MinimumSize = new Size(840, 520);
        Size = new Size(1120, 750);

        var principal = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            Padding = new Padding(12)
        };
        principal.RowStyles.Add(new RowStyle(SizeType.Absolute, 112));
        principal.RowStyles.Add(new RowStyle(SizeType.Absolute, 76));
        principal.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        principal.RowStyles.Add(new RowStyle(SizeType.Absolute, 154));
        principal.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));

        var topo = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 3 };
        topo.RowStyles.Add(new RowStyle(SizeType.Absolute, 29));
        topo.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        topo.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        topo.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        topo.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 122));
        topo.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 195));
        topo.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 95));
        topo.Controls.Add(_caminho, 0, 1);
        topo.Controls.Add(_selecionar, 1, 1);
        topo.Controls.Add(_iniciar, 2, 1);
        topo.Controls.Add(_cancelar, 3, 1);
        topo.Controls.Add(new Label
        {
            Text = "Pasta de teste: indícios não significam descarte. Nenhum arquivo será modificado.",
            Dock = DockStyle.Fill,
            AutoSize = true
        }, 0, 0);
        topo.SetColumnSpan(topo.GetControlFromPosition(0, 0)!, 4);

        var parametrosTemporais = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            AutoScroll = true
        };
        parametrosTemporais.Controls.Add(_ativarSinalTemporal);
        parametrosTemporais.Controls.Add(_diasSemModificacao);
        parametrosTemporais.Controls.Add(new Label
        {
            Text = "dias — só indício, sem exclusão",
            AutoSize = true,
            Padding = new Padding(0, 5, 0, 0)
        });
        topo.Controls.Add(parametrosTemporais, 0, 2);
        topo.SetColumnSpan(parametrosTemporais, 4);
        _ativarSinalTemporal.CheckedChanged += (_, _) =>
            _diasSemModificacao.Enabled = _ativarSinalTemporal.Checked && _cancelamento is null;

        _resumo.Text = "Nenhum inventário executado.";
        _resumo.Padding = new Padding(0, 12, 0, 0);
        _estado.Text = "Pronto — apenas leitura de metadados.";
        _estado.AutoEllipsis = true;

        var areaOcorrencias = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2 };
        areaOcorrencias.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        areaOcorrencias.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        areaOcorrencias.Controls.Add(new Label
        {
            Text = "Ocorrências (falhas e links ignorados; até 100 detalhes)",
            Dock = DockStyle.Fill,
            AutoSize = true
        }, 0, 0);
        areaOcorrencias.Controls.Add(_ocorrencias, 0, 1);

        principal.Controls.Add(topo, 0, 0);
        principal.Controls.Add(_resumo, 0, 1);
        principal.Controls.Add(_grade, 0, 2);
        principal.Controls.Add(areaOcorrencias, 0, 3);
        principal.Controls.Add(_estado, 0, 4);
        Controls.Add(principal);

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
    }

    private void SelecionarPasta()
    {
        using var dialogo = new FolderBrowserDialog
        {
            Description = "Selecione apenas uma pasta de teste para inventariar.",
            UseDescriptionForTitle = true,
            ShowNewFolderButton = false
        };

        if (dialogo.ShowDialog(this) == DialogResult.OK)
            _caminho.Text = dialogo.SelectedPath;
    }

    private async Task IniciarAnaliseAsync()
    {
        var pasta = _caminho.Text.Trim();
        if (!Directory.Exists(pasta))
        {
            MessageBox.Show(this, "Selecione uma pasta existente para o teste.",
                "CLEAR", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        // Política capturada uma única vez por execução; não muda durante a varredura.
        var usarSinalTemporal = _ativarSinalTemporal.Checked;
        var diasSelecionados = (int)_diasSemModificacao.Value;
        var referenciaUtc = DateTimeOffset.UtcNow;
        var classificador = usarSinalTemporal
            ? ClassificadorArquivos.CriarComSinalTemporal(diasSelecionados, referenciaUtc)
            : new ClassificadorArquivos();

        _cancelamento = new CancellationTokenSource();
        var token = _cancelamento.Token;
        _ativarSinalTemporal.Enabled = false;
        _diasSemModificacao.Enabled = false;
        _selecionar.Enabled = false;
        _iniciar.Enabled = false;
        _cancelar.Enabled = true;
        _grade.DataSource = null;
        _ocorrencias.Items.Clear();
        _resumo.Text = "Lendo metadados...";

        var progresso = new Progress<ProgressoInventario>(atual =>
        {
            _estado.Text = $"Lidos: {atual.QuantidadeArquivos:N0} arquivo(s), {atual.TotalBytes:N0} bytes.";
        });

        try
        {
            var fonte = new FonteInventarioSistemaArquivos();
            var casoDeUso = new InventariarDiretorio(fonte, limiteAmostra: 1000, classificador: classificador);

            // Executa a leitura fora da thread gráfica para não bloquear os botões.
            var resultado = await Task.Run(
                () => casoDeUso.ExecutarAsync(pasta, progresso, token), token);

            _grade.DataSource = resultado.ClassificacoesAmostra
                .Select(r => new
                {
                    Caminho = r.Arquivo.CaminhoCompleto,
                    Bytes = r.Arquivo.TamanhoBytes,
                    Categorias = string.Join(", ", r.Categorias),
                    Justificativas = string.Join(" | ", r.Evidencias.Select(e => e.Justificativa)),
                    ModificadoEmUtc = r.Arquivo.UltimaModificacaoUtc,
                    AcessadoEmUtc = r.Arquivo.UltimoAcessoUtc
                }).ToList();

            foreach (var item in resultado.Ocorrencias)
                _ocorrencias.Items.Add($"{item.Tipo}: {item.Caminho} — {item.Descricao}");

            var parcial = resultado.Incompleto ? " | inventário parcial (ocorrências registradas)" : "";
            var exibicao = resultado.AmostraLimitada ? " | exibindo os primeiros 1.000 arquivos" : "";
            _resumo.Text =
                $"{resultado.QuantidadeArquivos:N0} arquivo(s) | {resultado.TotalBytes:N0} bytes" +
                $" | {resultado.ArquivosComIndicios:N0} arquivo(s) com indícios" +
                $" | {resultado.TotalOcorrencias:N0} ocorrência(s){parcial}{exibicao}";

            var categorias = string.Join(" | ", resultado.ContagensCategoria.Select(c =>
                $"{c.Categoria}: {c.QuantidadeArquivos:N0}"));
            var criterio = usarSinalTemporal
                ? $" | análise temporal ativa: mais de {diasSelecionados} dias"
                : " | análise temporal desativada";
            _estado.Text = (categorias.Length == 0
                ? "Nenhum indício pelas regras atuais."
                : categorias) + criterio + " | Nenhum arquivo alterado.";
        }
        catch (OperationCanceledException)
        {
            _resumo.Text = "Inventário cancelado; resultado parcial descartado.";
            _estado.Text = "Cancelado pelo usuário.";
        }
        catch (Exception ex)
        {
            _resumo.Text = "Inventário não concluído.";
            _estado.Text = "Falha ao inventariar: " + ex.Message;
            MessageBox.Show(this, ex.Message, "Falha no inventário",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally
        {
            _cancelamento.Dispose();
            _cancelamento = null;
            _ativarSinalTemporal.Enabled = true;
            _diasSemModificacao.Enabled = _ativarSinalTemporal.Checked;
            _selecionar.Enabled = true;
            _iniciar.Enabled = true;
            _cancelar.Enabled = false;
        }
    }
}
