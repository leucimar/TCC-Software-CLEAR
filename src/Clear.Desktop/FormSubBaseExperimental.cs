using System.Diagnostics;
using Clear.Application.Amostragem;
using Clear.Application.Progresso;
using Clear.Infrastructure.Amostragem;

namespace Clear.Desktop;

/// <summary>
/// Ferramenta auxiliar para preparar uma cópia experimental reproduzível.
/// Não faz quarentena, não altera, renomeia ou exclui a origem.
/// </summary>
public sealed class FormSubBaseExperimental : Form
{
    private readonly TextBox _origem = new() { Dock = DockStyle.Fill };
    private readonly TextBox _destino = new() { Dock = DockStyle.Fill };
    private readonly NumericUpDown _percentual = new()
        { Minimum = 1, Maximum = 100, Value = 30, Width = 65 };
    private readonly NumericUpDown _semente = new()
        { Minimum = 0, Maximum = int.MaxValue, Value = 5721, Width = 100 };
    private readonly Button _escolherOrigem = new() { Text = "Escolher", AutoSize = true };
    private readonly Button _escolherDestino = new() { Text = "Escolher pasta pai", AutoSize = true };
    private readonly Button _preparar = new() { Text = "1. Calcular prévia", AutoSize = true };
    private readonly Button _copiar = new()
        { Text = "2. Confirmar e COPIAR arquivos", AutoSize = true, Enabled = false };
    private readonly Button _cancelar = new()
        { Text = "Cancelar operação", AutoSize = true, Enabled = false };
    private readonly Label _estado = new()
        { AutoSize = false, Dock = DockStyle.Fill, Text = "Calcule a prévia antes de copiar." };
    private readonly TextBox _resumo = new()
        { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true,
          ScrollBars = ScrollBars.Vertical, WordWrap = true };

    private PlanoAmostraEstratificada? _plano;
    private string? _origemPlanejada;
    private string? _destinoPlanejado;
    private CancellationTokenSource? _cancelamento;

    public FormSubBaseExperimental(string origemInicial)
    {
        Text = "CLEAR — Criar sub-base experimental (SOMENTE CÓPIA)";
        MinimumSize = new Size(880, 560);
        Size = new Size(1050, 650);

        if (Directory.Exists(origemInicial))
        {
            _origem.Text = Path.GetFullPath(origemInicial);
            _destino.Text = SugerirDestino(_origem.Text);
        }

        var painel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, Padding = new Padding(14),
            ColumnCount = 3, RowCount = 7
        };
        painel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        painel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        painel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        for (var i = 0; i < 4; i++)
            painel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        painel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        painel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        painel.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));

        painel.Controls.Add(new Label
        {
            Text = "Acervo original", AutoSize = true,
            Padding = new Padding(0, 10, 0, 0)
        }, 0, 0);
        painel.Controls.Add(_origem, 1, 0);
        painel.Controls.Add(_escolherOrigem, 2, 0);
        painel.Controls.Add(new Label
        {
            Text = "Sub-base de destino", AutoSize = true,
            Padding = new Padding(0, 10, 0, 0)
        }, 0, 1);
        painel.Controls.Add(_destino, 1, 1);
        painel.Controls.Add(_escolherDestino, 2, 1);

        painel.Controls.Add(new Label
        {
            Text = "Percentual (%)", AutoSize = true,
            Padding = new Padding(0, 10, 0, 0)
        }, 0, 2);
        painel.Controls.Add(_percentual, 1, 2);
        painel.Controls.Add(new Label
        {
            Text = "Semente de seleção", AutoSize = true,
            Padding = new Padding(0, 10, 0, 0)
        }, 0, 3);
        painel.Controls.Add(_semente, 1, 3);

        var botoes = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill };
        botoes.Controls.Add(_preparar);
        botoes.Controls.Add(_copiar);
        botoes.Controls.Add(_cancelar);
        painel.Controls.Add(botoes, 0, 4);
        painel.SetColumnSpan(botoes, 3);
        painel.Controls.Add(_resumo, 0, 5);
        painel.SetColumnSpan(_resumo, 3);
        painel.Controls.Add(_estado, 0, 6);
        painel.SetColumnSpan(_estado, 3);
        Controls.Add(painel);
        TemaVisual.Registrar(this);

        _resumo.Text = "Os arquivos escolhidos serão COPIADOS e conferidos via SHA-256. " +
            "Nada será movido, apagado ou sobrescrito na origem. " +
            "A estrutura de pastas será mantida em Acervo\\... dentro da nova sub-base." +
            Environment.NewLine + Environment.NewLine +
            "As metas de 30% do número de arquivos e do volume em bytes são distintas; " +
            "o número de arquivos é exato, e os bytes têm aproximação medida.";
        _escolherOrigem.Click += (_, _) => EscolherPasta(_origem, escolhaDeOrigem: true);
        _escolherDestino.Click += (_, _) => EscolherPasta(_destino, escolhaDeOrigem: false);
        _origem.TextChanged += (_, _) => InvalidarPrevia();
        _destino.TextChanged += (_, _) => InvalidarPrevia();
        _percentual.ValueChanged += (_, _) => InvalidarPrevia();
        _semente.ValueChanged += (_, _) => InvalidarPrevia();
        _preparar.Click += async (_, _) => await PrepararAsync();
        _copiar.Click += async (_, _) => await CopiarAsync();
        _cancelar.Click += (_, _) => _cancelamento?.Cancel();
        FormClosing += (_, args) =>
        {
            if (_cancelamento is null) return;
            _cancelamento.Cancel();
            args.Cancel = true;
            _estado.Text = "Aguarde a interrupção controlada antes de fechar...";
        };
    }

    private static string SugerirDestino(string origem)
    {
        var pai = Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(origem));
        return pai is null
            ? Path.Combine(Path.GetTempPath(), "CLEAR_Amostras", "CLEAR_30pct")
            : Path.Combine(pai, "03_amostras", "CLEAR_30pct");
    }

    private void EscolherPasta(TextBox campo, bool escolhaDeOrigem)
    {
        using var dialogo = new FolderBrowserDialog
        {
            Description = escolhaDeOrigem
                ? "Escolha a raiz da base original, que será somente lida."
                : "Escolha a pasta PAI onde será criada uma nova sub-base.",
            ShowNewFolderButton = !escolhaDeOrigem
        };
        if (dialogo.ShowDialog(this) != DialogResult.OK) return;
        campo.Text = escolhaDeOrigem
            ? dialogo.SelectedPath
            : Path.Combine(dialogo.SelectedPath, "CLEAR_30pct");
        if (escolhaDeOrigem)
            _destino.Text = SugerirDestino(campo.Text);
    }

    private void InvalidarPrevia()
    {
        _plano = null;
        _origemPlanejada = null;
        _destinoPlanejado = null;
        _copiar.Enabled = false;
    }

    private void EmExecucao(bool ativo)
    {
        _preparar.Enabled = !ativo;
        _copiar.Enabled = !ativo && _plano is not null;
        _escolherOrigem.Enabled = !ativo;
        _escolherDestino.Enabled = !ativo;
        _origem.Enabled = !ativo;
        _destino.Enabled = !ativo;
        _percentual.Enabled = !ativo;
        _semente.Enabled = !ativo;
        _cancelar.Enabled = ativo;
    }

    private async Task PrepararAsync()
    {
        InvalidarPrevia();
        _cancelamento = new CancellationTokenSource();
        EmExecucao(true);
        _estado.Text = "Inventariando metadados para calcular a amostra (sem copiar)...";
        try
        {
            var origem = _origem.Text.Trim();
            var destino = _destino.Text.Trim();
            var percentual = _percentual.Value;
            var semente = (int)_semente.Value;
            var token = _cancelamento.Token;
            var plano = await Task.Run(() =>
                CriadorSubBaseExperimental.Preparar(
                    origem, destino, percentual, semente, token), token);
            _plano = plano;
            _origemPlanejada = origem;
            _destinoPlanejado = destino;
            _resumo.Text =
                $"Arquivos originais: {plano.Originais.Count:N0}" + Environment.NewLine +
                $"Arquivos selecionados: {plano.Selecionados.Count:N0}" +
                $" ({plano.PercentualArquivosReal:F3}%)" + Environment.NewLine +
                $"Bytes originais: {plano.BytesOriginais:N0}" + Environment.NewLine +
                $"Bytes selecionados: {plano.BytesSelecionados:N0}" +
                $" ({plano.PercentualBytesReal:F3}%)" + Environment.NewLine +
                $"Estratos representados: {plano.Estratos.Count:N0}" + Environment.NewLine +
                "Critérios: pasta principal + extensão + faixa de tamanho." +
                Environment.NewLine +
                "A amostra é determinística, mas os bytes podem não ser 30% exatos." +
                Environment.NewLine + Environment.NewLine +
                "A sub-base preservará o caminho relativo dos arquivos selecionados " +
                "dentro de uma pasta Acervo. As cópias serão verificadas por SHA-256.";
            _estado.Text = "Prévia pronta. Verifique volume e destino antes de copiar.";
        }
        catch (OperationCanceledException)
        {
            _estado.Text = "Prévia cancelada. Nenhum arquivo foi copiado.";
        }
        catch (Exception ex)
        {
            _estado.Text = "Prévia não concluída: " + ex.Message;
            MessageBox.Show(this, ex.Message, "CLEAR — Prévia da sub-base",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally
        {
            _cancelamento.Dispose();
            _cancelamento = null;
            EmExecucao(false);
        }
    }

    private async Task CopiarAsync()
    {
        if (_plano is null || _origemPlanejada != _origem.Text.Trim()
            || _destinoPlanejado != _destino.Text.Trim()) return;
        var plano = _plano;
        var resposta = MessageBox.Show(this,
            "CRIAR SUB-BASE POR CÓPIA?" + Environment.NewLine +
            $"Origem: {_origemPlanejada}" + Environment.NewLine +
            $"Destino novo: {_destinoPlanejado}" + Environment.NewLine +
            $"Arquivos a duplicar: {plano.Selecionados.Count:N0}" + Environment.NewLine +
            $"Volume estimado: {plano.BytesSelecionados:N0} bytes" +
            Environment.NewLine + Environment.NewLine +
            "A origem não será movida, apagada nem sobrescrita. " +
            "A cópia poderá ocupar dezenas de GB e será conferida por SHA-256. " +
            "Se houver interrupção, somente uma pasta de estágio incompleta ficará no destino.",
            "CLEAR — Confirmar cópia de sub-base",
            MessageBoxButtons.YesNo, MessageBoxIcon.Question,
            MessageBoxDefaultButton.Button2);
        if (resposta != DialogResult.Yes) return;

        _cancelamento = new CancellationTokenSource();
        EmExecucao(true);
        _estado.Text = "Copiando arquivos e verificando integridade...";
        try
        {
            var relogio = Stopwatch.StartNew();
            var progresso = new Progress<ProgressoCopiaAmostra>(p =>
            {
                var estimado = EstimativaRestante.Calcular(p.ArquivosCopiados,
                    p.TotalArquivos, p.BytesCopiados, p.TotalBytes, relogio.Elapsed);
                var percentual = p.TotalBytes > 0
                    ? 100.0 * p.BytesCopiados / p.TotalBytes : 0;
                _estado.Text =
                    $"COPIANDO: {p.ArquivosCopiados:N0}/{p.TotalArquivos:N0} " +
                    $"arquivos | {p.BytesCopiados:N0}/{p.TotalBytes:N0} bytes " +
                    $"({percentual:F1}% volume) | decorrido {relogio.Elapsed.ToString(@"hh\:mm\:ss")} " +
                    $"| {EstimativaRestante.Exibir(estimado)}";
            });
            var resultado = await CriadorSubBaseExperimental.ExecutarAsync(
                _origemPlanejada!, _destinoPlanejado!, plano, progresso,
                _cancelamento.Token);
            _estado.Text = "Sub-base concluída e publicada: " + resultado.DiretorioFinal;
            _resumo.AppendText(Environment.NewLine + Environment.NewLine +
                "CONCLUÍDA. Manifestos: " + resultado.ManifestoResumo + " | " +
                resultado.ManifestoArquivos);
            MessageBox.Show(this,
                $"Sub-base concluída! {resultado.ArquivosCopiados:N0} arquivos " +
                $"COPIADOS e verificados. A base original foi preservada." +
                Environment.NewLine + "Destino: " + resultado.DiretorioFinal,
                "CLEAR — Sub-base experimental", MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        catch (OperationCanceledException)
        {
            _estado.Text = "Cópia cancelada. A origem não foi movimentada. " +
                "Caso haja estágio incompleto, ele NÃO é uma sub-base válida.";
        }
        catch (Exception ex)
        {
            _estado.Text = "Cópia não concluída: " + ex.Message +
                ". Verifique eventual pasta CLEAR_EM_CONSTRUCAO.";
            MessageBox.Show(this, ex.Message,
                "CLEAR — Sub-base incompleta", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally
        {
            _cancelamento.Dispose();
            _cancelamento = null;
            InvalidarPrevia();
            EmExecucao(false);
        }
    }
}
