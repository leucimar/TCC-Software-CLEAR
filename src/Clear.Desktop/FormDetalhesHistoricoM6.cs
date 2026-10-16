using System.Text.Json;
using Clear.Infrastructure.Historico;
using Clear.Infrastructure.Quarentena;

namespace Clear.Desktop;

/// <summary>
/// Consulta histórica somente leitura. Tabelas paginadas, sem editar o SQLite
/// nem reabrir/alterar arquivos do acervo original.
/// </summary>
public sealed class FormDetalhesHistoricoM6 : Form
{
    private readonly string _id;
    private readonly string _banco;
    private readonly ResumoExecucaoHistoricaM6 _execucao;
    private readonly Button _quarentena = new()
    {
        Text = "Revisar envio à quarentena (M7)", AutoSize = true
    };
    private readonly DataGridView _auditoriaQuarentena = Grade();
    private readonly Button _atualizarAuditoria = new() { Text = "Atualizar auditoria", AutoSize = true };
    private readonly long _totalEntradas;
    private readonly DataGridView _entradas = Grade();
    private readonly DataGridView _duplicatas = Grade();
    private readonly DataGridView _ocorrencias = Grade();
    private readonly TextBox _detalhes = new()
    {
        Dock = DockStyle.Fill, ReadOnly = true, Multiline = true,
        ScrollBars = ScrollBars.Both, WordWrap = false,
        Font = new Font("Consolas", 9)
    };
    private readonly Label _paginaLabel = new()
    {
        AutoSize = true, Padding = new Padding(0, 7, 0, 0)
    };
    private readonly Button _anterior = new() { Text = "Anterior", AutoSize = true };
    private readonly Button _proxima = new() { Text = "Próxima", AutoSize = true };
    private readonly Label _paginaDuplicatasLabel = new()
    {
        AutoSize = true, Padding = new Padding(0, 7, 0, 0)
    };
    private readonly Button _anteriorDuplicatas = new() { Text = "Anterior", AutoSize = true };
    private readonly Button _proximaDuplicatas = new() { Text = "Próxima", AutoSize = true };
    private int _pagina;
    private int _paginaDuplicatas;
    private bool _carregando;

    public FormDetalhesHistoricoM6(ResumoExecucaoHistoricaM6 execucao, string banco)
    {
        ArgumentNullException.ThrowIfNull(execucao);
        _id = execucao.Id;
        _banco = banco;
        _execucao = execucao;
        _totalEntradas = execucao.EntradasPersistidas;
        Text = "CLEAR — Consulta histórica (somente leitura) — " +
               execucao.InicioUtc.ToString("dd/MM/yyyy HH:mm");
        MinimumSize = new Size(950, 640);
        Size = new Size(1220, 770);

        var raiz = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, Padding = new Padding(8),
            RowCount = 2, ColumnCount = 1
        };
        raiz.RowStyles.Add(new RowStyle(SizeType.Absolute, 68));
        raiz.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        raiz.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Text = $"Análise salva em {execucao.InicioUtc:dd/MM/yyyy HH:mm} UTC | " +
                   $"Estado: {execucao.Estado} | Entradas: {_totalEntradas:N0} | " +
                   $"Grupos: {execucao.GruposDuplicatas:N0}" + Environment.NewLine +
                   $"Origem da análise: {execucao.DiretorioRaiz}" + Environment.NewLine +
                   "Resultados históricos: não representam o estado atual dos arquivos.",
            AutoEllipsis = true
        }, 0, 0);

        var abas = new TabControl { Dock = DockStyle.Fill };
        var abaEntradas = new TabPage("Arquivos e classificações");
        var areaEntradas = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3 };
        areaEntradas.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        areaEntradas.RowStyles.Add(new RowStyle(SizeType.Percent, 60));
        areaEntradas.RowStyles.Add(new RowStyle(SizeType.Percent, 40));
        var navegacao = new FlowLayoutPanel { Dock = DockStyle.Fill };
        navegacao.Controls.Add(_anterior);
        navegacao.Controls.Add(_proxima);
        navegacao.Controls.Add(_paginaLabel);
        navegacao.Controls.Add(_quarentena);
        areaEntradas.Controls.Add(navegacao, 0, 0);
        areaEntradas.Controls.Add(_entradas, 0, 1);
        areaEntradas.Controls.Add(_detalhes, 0, 2);
        abaEntradas.Controls.Add(areaEntradas);
        abas.TabPages.Add(abaEntradas);

        var abaDuplicatas = new TabPage("Grupos de duplicatas");
        var areaDuplicatas = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2 };
        areaDuplicatas.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        areaDuplicatas.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var navegacaoDuplicatas = new FlowLayoutPanel { Dock = DockStyle.Fill };
        navegacaoDuplicatas.Controls.Add(_anteriorDuplicatas);
        navegacaoDuplicatas.Controls.Add(_proximaDuplicatas);
        navegacaoDuplicatas.Controls.Add(_paginaDuplicatasLabel);
        areaDuplicatas.Controls.Add(navegacaoDuplicatas, 0, 0);
        areaDuplicatas.Controls.Add(_duplicatas, 0, 1);
        abaDuplicatas.Controls.Add(areaDuplicatas);
        abas.TabPages.Add(abaDuplicatas);

        var abaOcorrencias = new TabPage("Ocorrências registradas");
        abaOcorrencias.Controls.Add(_ocorrencias);
        abas.TabPages.Add(abaOcorrencias);

        var abaAuditoria = new TabPage("Auditoria da quarentena — M7");
        var painelAuditoria = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2 };
        painelAuditoria.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        painelAuditoria.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var barraAuditoria = new FlowLayoutPanel { Dock = DockStyle.Fill };
        barraAuditoria.Controls.Add(_atualizarAuditoria);
        painelAuditoria.Controls.Add(barraAuditoria, 0, 0);
        painelAuditoria.Controls.Add(_auditoriaQuarentena, 0, 1);
        abaAuditoria.Controls.Add(painelAuditoria);
        abas.TabPages.Add(abaAuditoria);

        raiz.Controls.Add(abas, 0, 1);
        Controls.Add(raiz);

        _anterior.Click += async (_, _) => await CarregarEntradasAsync(_pagina - 1);
        _proxima.Click += async (_, _) => await CarregarEntradasAsync(_pagina + 1);
        _anteriorDuplicatas.Click += async (_, _) =>
            await CarregarDuplicatasAsync(_paginaDuplicatas - 1);
        _proximaDuplicatas.Click += async (_, _) =>
            await CarregarDuplicatasAsync(_paginaDuplicatas + 1);

        _entradas.SelectionChanged += (_, _) => ExibirEvidenciasSelecionadas();
        _quarentena.Click += async (_, _) => await RevisarQuarentenaAsync();
        _atualizarAuditoria.Click += async (_, _) => await AtualizarAuditoriaAsync();
        Shown += async (_, _) =>
        {
            await CarregarEntradasAsync(0);
            await CarregarDuplicatasAsync(0);
            await CarregarOcorrenciasAsync();
            await AtualizarAuditoriaAsync();
        };
    }

    private static DataGridView Grade() => new()
    {
        Dock = DockStyle.Fill, ReadOnly = true,
        AllowUserToAddRows = false, AllowUserToDeleteRows = false,
        RowHeadersVisible = false, MultiSelect = false,
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells,
        SelectionMode = DataGridViewSelectionMode.FullRowSelect
    };

    private async Task CarregarEntradasAsync(int pagina)
    {
        if (pagina < 0 || _carregando) return;
        _carregando = true;
        try
        {
            var entradas = await ConsultaHistoricoSqliteM6.ConsultarEntradasAsync(
                _banco, _id, pagina);
            if (pagina > 0 && entradas.Count == 0) return;
            _pagina = pagina;
            _entradas.DataSource = entradas.ToList();
            _paginaLabel.Text =
                $"Página {pagina + 1} | {entradas.Count:N0} linha(s) exibida(s) " +
                $"| {_totalEntradas:N0} registradas no total.";
            _anterior.Enabled = pagina > 0;
            _proxima.Enabled = (long)(pagina + 1) * ConsultaHistoricoSqliteM6.TamanhoPagina
                < _totalEntradas;
            ExibirEvidenciasSelecionadas();
        }
        catch (Exception ex)
        {
            _detalhes.Text = "Falha ao consultar histórico: " + ex.Message;
        }
        finally { _carregando = false; }
    }

    private async Task CarregarDuplicatasAsync(int pagina)
    {
        if (pagina < 0) return;
        try
        {
            var membros = await ConsultaHistoricoSqliteM6.ConsultarDuplicatasAsync(
                _banco, _id, pagina);
            if (pagina > 0 && membros.Count == 0) return;
            _paginaDuplicatas = pagina;
            _duplicatas.DataSource = membros.ToList();
            _paginaDuplicatasLabel.Text =
                $"Página {pagina + 1} | {membros.Count:N0} membro(s) nesta página.";
            _anteriorDuplicatas.Enabled = pagina > 0;
            _proximaDuplicatas.Enabled =
                membros.Count == ConsultaHistoricoSqliteM6.TamanhoPagina;
        }
        catch (Exception ex)
        {
            _paginaDuplicatasLabel.Text = "Falha: " + ex.Message;
        }
    }

    private async Task CarregarOcorrenciasAsync()
    {
        try
        {
            _ocorrencias.DataSource = (await ConsultaHistoricoSqliteM6
                .ConsultarOcorrenciasAsync(_banco, _id)).ToList();
        }
        catch (Exception ex)
        {
            _detalhes.Text = "Falha ao ler ocorrências: " + ex.Message;
        }
    }

    private async Task RevisarQuarentenaAsync()
    {
        if (_entradas.CurrentRow?.DataBoundItem is not EntradaHistoricaM6 item ||
            item.Tipo != "Arquivo")
        {
            MessageBox.Show(this, "Selecione um arquivo histórico, não uma pasta.",
                "CLEAR — Quarentena M7", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        _quarentena.Enabled = false;
        try
        {
            // Etapa 1: somente pré-verificação. Não move nem registra ações.
            var plano = await ServicoQuarentenaM7.PrepararAsync(
                item, _execucao, ServicoQuarentenaM7.RaizPadrao);

            // Etapa 2: a mudança de localização exige confirmação humana explícita.
            // O botão padrão é NÃO; fechar a janela também cancela.
            var consentimento = MessageBox.Show(this,
                "CONFIRMA ENVIO À QUARENTENA? ESTA AÇÃO MOVE O ARQUIVO." +
                Environment.NewLine + Environment.NewLine +
                "ORIGEM: " + plano.CaminhoOriginal + Environment.NewLine +
                "DESTINO: " + plano.CaminhoDestino + Environment.NewLine +
                "TAMANHO: " + plano.TamanhoBytes + " bytes" + Environment.NewLine +
                "SHA-256: " + plano.Sha256Hex + Environment.NewLine +
                "ANÁLISE: " + plano.IdExecucao + Environment.NewLine + Environment.NewLine +
                "A quarentena não é exclusão. A restauração automatizada será " +
                "desenvolvida no M8. Use SOMENTE arquivos sintéticos neste estágio.",
                "CLEAR — Confirmação humana obrigatória",
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2);
            if (consentimento != DialogResult.Yes)
                return;

            var registro = await ServicoQuarentenaM7.ExecutarAsync(
                plano, confirmacaoHumanaExplicita: true,
                ServicoQuarentenaM7.BancoAuditoriaPadrao);
            MessageBox.Show(this,
                "Arquivo enviado à quarentena. Operação registrada em SQLite." +
                Environment.NewLine + "Operação: " + registro.IdOperacao +
                Environment.NewLine + "Destino: " + registro.CaminhoDestino +
                Environment.NewLine + "Não exclua o arquivo de quarentena nem o banco de auditoria.",
                "CLEAR — Quarentena M7", MessageBoxButtons.OK, MessageBoxIcon.Information);
            await AtualizarAuditoriaAsync();
        }
        catch (OperationCanceledException)
        {
            // Negar a operação não pode alterar o arquivo original.
        }
        catch (Exception ex)
        {
            MessageBox.Show(this,
                "A movimentação não foi autorizada/concluída com segurança: " +
                ex.Message + Environment.NewLine +
                "Confira a auditoria M7 antes de qualquer nova tentativa.",
                "CLEAR — Quarentena M7", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally { _quarentena.Enabled = true; }
    }

    private async Task AtualizarAuditoriaAsync()
    {
        _atualizarAuditoria.Enabled = false;
        try
        {
            var lista = await ServicoQuarentenaM7.ConsultarAsync(
                ServicoQuarentenaM7.BancoAuditoriaPadrao, _id);
            _auditoriaQuarentena.DataSource = lista.ToList();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "Falha ao consultar auditoria: " + ex.Message,
                "CLEAR — Auditoria M7", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally { _atualizarAuditoria.Enabled = true; }
    }

    private void ExibirEvidenciasSelecionadas()
    {
        if (_entradas.CurrentRow?.DataBoundItem is not EntradaHistoricaM6 item)
        {
            _detalhes.Text = "Selecione uma entrada para consultar as evidências gravadas.";
            return;
        }
        _detalhes.Text =
            $"Caminho: {item.Caminho}" + Environment.NewLine +
            $"Tipo: {item.Tipo} | Tamanho: {item.TamanhoBytes?.ToString() ?? "N/D"} bytes" +
            Environment.NewLine + $"Modificado em UTC: {item.ModificadoUtc}" +
            Environment.NewLine + $"Acima do limite pessoal: {(item.AcimaDoLimitePessoal ? "Sim" : "Não")}" +
            Environment.NewLine + Environment.NewLine +
            "CATEGORIAS" + Environment.NewLine + FormatJson(item.CategoriasJson) +
            Environment.NewLine + Environment.NewLine +
            "EVIDÊNCIAS" + Environment.NewLine + FormatJson(item.EvidenciasJson) +
            Environment.NewLine + Environment.NewLine +
            "VERIFICAÇÕES" + Environment.NewLine + FormatJson(item.VerificacoesJson);
    }

    private static string FormatJson(string value)
    {
        try
        {
            using var json = JsonDocument.Parse(value);
            return JsonSerializer.Serialize(json.RootElement,
                new JsonSerializerOptions { WriteIndented = true });
        }
        catch (JsonException)
        {
            return value;
        }
    }
}
