using System.Diagnostics;
using Clear.Application.Progresso;
using Clear.Domain.Analise;
using Clear.Infrastructure.Historico;
using Clear.Infrastructure.Quarentena;

namespace Clear.Desktop;

/// <summary>
/// Seleção EXPLÍCITA, por ID histórico, preservada entre páginas. A interface
/// não seleciona automaticamente todos os resultados de um filtro. Toda ação
/// é precedida de confirmação global e revalidação individual M7.
/// </summary>
public sealed class FormSelecaoQuarentenaM10 : Form
{
    private readonly ResumoExecucaoHistoricaM6 _execucao;
    private readonly string _banco;
    private readonly string _destino;
    private readonly HashSet<long> _selecionados = [];
    private readonly HashSet<string> _caminhosIndisponiveis = new(StringComparer.OrdinalIgnoreCase);
    private readonly ComboBox _categorias = new()
        { DropDownStyle = ComboBoxStyle.DropDownList, Width = 150 };
    private readonly TextBox _extensao = new() { Width = 65, PlaceholderText = ".pdf" };
    private readonly NumericUpDown _minimoMiB = new()
        { Maximum = 10000000, DecimalPlaces = 2, Width = 90 };
    private readonly NumericUpDown _maximoMiB = new()
        { Maximum = 10000000, DecimalPlaces = 2, Width = 90 };
    private readonly CheckBox _filtrarMaximo = new()
        { Text = "Máx. ativo", AutoSize = true };
    private readonly CheckBox _incomp = new() { Text = "Incompatibilidade", AutoSize = true };
    private readonly CheckBox _duplicatas = new() { Text = "Duplicatas", AutoSize = true };
    private readonly CheckBox _limite = new() { Text = "Acima limite", AutoSize = true };
    private readonly CheckBox _filtrarData = new()
        { Text = "Último acesso entre datas", AutoSize = true };
    private readonly DateTimePicker _desde = new()
        { Format = DateTimePickerFormat.Short, Width = 110 };
    private readonly DateTimePicker _ate = new()
        { Format = DateTimePickerFormat.Short, Width = 110 };
    private readonly TextBox _trecho = new()
        { Width = 170, PlaceholderText = "Pasta ou nome" };
    private readonly Button _buscar = new() { Text = "Aplicar filtros", AutoSize = true };
    private readonly Button _anterior = new() { Text = "Anterior", AutoSize = true };
    private readonly Button _proximo = new() { Text = "Próxima", AutoSize = true };
    private readonly Button _selecionarPagina = new() { Text = "Selecionar página", AutoSize = true };
    private readonly Button _selecionarTodos = new()
        { Text = "Selecionar TODOS filtrados (até 10.000)", AutoSize = true };
    private readonly Button _limpar = new() { Text = "Limpar seleção", AutoSize = true };
    private readonly Button _mover = new()
        { Text = "Revisar e movimentar SELECIONADOS", AutoSize = true };
    private readonly Button _cancelar = new() { Text = "Cancelar lote", AutoSize = true, Enabled = false };
    private readonly Label _estado = new() { AutoSize = false, Dock = DockStyle.Fill };
    private readonly DataGridView _grade = new()
    {
        Dock = DockStyle.Fill, ReadOnly = false, AllowUserToAddRows = false,
        AllowUserToDeleteRows = false, MultiSelect = false,
        RowHeadersVisible = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
        SelectionMode = DataGridViewSelectionMode.FullRowSelect
    };
    private readonly TextBox _erros = new()
        { Dock = DockStyle.Fill, ReadOnly = true, Multiline = true, ScrollBars = ScrollBars.Vertical };
    private List<EntradaHistoricaM6> _paginaAtual = [];
    private FiltrosEntradasM10 _filtro = new();
    private int _pagina;
    private long _totalFiltrado;
    private bool _carregando;
    private bool _executando;
    private CancellationTokenSource? _cancelamento;

    public FormSelecaoQuarentenaM10(
        ResumoExecucaoHistoricaM6 execucao, string banco, string destino)
    {
        _execucao = execucao ?? throw new ArgumentNullException(nameof(execucao));
        _banco = banco;
        _destino = destino;
        Text = "CLEAR — Filtrar, selecionar e revisar quarentena (SEM AÇÃO AUTOMÁTICA)";
        MinimumSize = new Size(1120, 670);
        Size = new Size(1500, 860);

        _categorias.Items.Add("(todas)");
        foreach (var categoria in Enum.GetNames<CategoriaAnalise>())
            _categorias.Items.Add(categoria);
        _categorias.Items.Add("Duplicata exata");
        _categorias.Items.Add("Incompatibilidade documentada");
        _categorias.Items.Add("Sem alertas detectados");
        _categorias.Items.Add("Com indícios ou alertas");
        _categorias.SelectedIndex = 0;

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 4,
            ColumnCount = 1, Padding = new Padding(8) };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 104));

        var filtros = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true,
            FlowDirection = FlowDirection.LeftToRight, WrapContents = true,
            Padding = new Padding(4), BackColor = SystemColors.ControlLight };
        filtros.Controls.Add(new Label { Text = "Categoria:", AutoSize = true });
        filtros.Controls.Add(_categorias);
        filtros.Controls.Add(new Label { Text = "Extensão:", AutoSize = true });
        filtros.Controls.Add(_extensao);
        filtros.Controls.Add(new Label { Text = "Mín. MiB:", AutoSize = true });
        filtros.Controls.Add(_minimoMiB);
        filtros.Controls.Add(_filtrarMaximo);
        filtros.Controls.Add(_maximoMiB);
        filtros.Controls.Add(_incomp);
        filtros.Controls.Add(_duplicatas);
        filtros.Controls.Add(_limite);
        filtros.Controls.Add(_filtrarData);
        filtros.Controls.Add(_desde);
        filtros.Controls.Add(_ate);
        filtros.Controls.Add(new Label { Text = "Caminho contém:", AutoSize = true });
        filtros.Controls.Add(_trecho);
        filtros.Controls.Add(_buscar);
        layout.Controls.Add(filtros, 0, 0);

        var barra = new FlowLayoutPanel { Dock = DockStyle.Fill };
        foreach (var controle in new Control[] { _anterior, _proximo,
            _selecionarPagina, _selecionarTodos, _limpar, _mover, _cancelar })
            barra.Controls.Add(controle);
        layout.Controls.Add(barra, 0, 1);
        layout.Controls.Add(_grade, 0, 2);
        var rodape = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2 };
        rodape.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        rodape.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        rodape.Controls.Add(_estado, 0, 0);
        rodape.Controls.Add(_erros, 0, 1);
        layout.Controls.Add(rodape, 0, 3);
        Controls.Add(layout);

        _grade.Columns.Add(new DataGridViewCheckBoxColumn
        {
            Name = "Marcado", HeaderText = "Mover?", Width = 65
        });
        _grade.Columns.Add("Caminho", "Arquivo");
        _grade.Columns.Add("Bytes", "Tamanho em bytes");
        _grade.Columns.Add("Categorias", "Classificações / indícios simultâneos");
        _grade.Columns.Add("Acessado", "Último acesso UTC");
        _grade.Columns.Add("Modificado", "Modificado UTC (auditoria)");
        _grade.Columns.Add("Id", "ID");
        _grade.Columns["Id"]!.Visible = false;
        ((DataGridViewCheckBoxColumn)_grade.Columns["Marcado"]!).AutoSizeMode =
            DataGridViewAutoSizeColumnMode.None;
        _grade.Columns["Marcado"]!.Width = 75;
        _grade.Columns["Caminho"]!.FillWeight = 65;
        _grade.Columns["Categorias"]!.FillWeight = 30;
        _grade.Columns["Bytes"]!.FillWeight = 12;
        _grade.Columns["Acessado"]!.FillWeight = 22;
        _grade.Columns["Modificado"]!.FillWeight = 22;
        for (var i = 1; i < _grade.Columns.Count; i++)
            _grade.Columns[i].ReadOnly = true;

        _grade.CurrentCellDirtyStateChanged += (_, _) =>
        {
            if (_grade.IsCurrentCellDirty)
                _grade.CommitEdit(DataGridViewDataErrorContexts.Commit);
        };
        _grade.CellValueChanged += (_, e) =>
        {
            if (_carregando || e.RowIndex < 0 || e.ColumnIndex != 0) return;
            if (_grade.Rows[e.RowIndex].Cells["Id"].Value is not long id)
            {
                _estado.Text = "Identificador do arquivo inválido; seleção não modificada.";
                return;
            }
            if (_grade.Rows[e.RowIndex].Cells["Marcado"].Value is true)
                _selecionados.Add(id);
            else
                _selecionados.Remove(id);
            ExibirEstado();
        };

        _buscar.Click += async (_, _) =>
        {
            try
            {
                _filtro = LerFiltros();
                await CarregarAsync(0);
            }
            catch (Exception ex) { _estado.Text = "Filtros inválidos: " + ex.Message; }
        };
        _anterior.Click += async (_, _) => await CarregarAsync(_pagina - 1);
        _proximo.Click += async (_, _) => await CarregarAsync(_pagina + 1);
        _selecionarPagina.Click += (_, _) =>
        {
            if (_selecionados.Concat(_paginaAtual.Select(x => x.Id))
                    .Distinct().Take(10001).Count() > 10000)
            {
                MessageBox.Show(this, "O lote não pode exceder 10.000 arquivos.");
                return;
            }
            foreach (var item in _paginaAtual) _selecionados.Add(item.Id);
            AtualizarMarcacoes();
            ExibirEstado();
        };
        _selecionarTodos.Click += async (_, _) =>
        {
            if (_executando || _carregando) return;
            var resposta = MessageBox.Show(this,
                "Selecionar TODOS os arquivos elegíveis que atendem aos filtros atuais? " +
                "Limite: 10.000 itens por operação. Selecionar NÃO MOVIMENTA arquivos. " +
                "A movimentação exigirá confirmação posterior.",
                "CLEAR — Seleção global explícita", MessageBoxButtons.YesNo,
                MessageBoxIcon.Question, MessageBoxDefaultButton.Button2);
            if (resposta != DialogResult.Yes) return;
            try
            {
                await AtualizarCaminhosIndisponiveisAsync();
                var encontrados = await ConsultaEntradasFiltradasM10.SelecionarTodosFiltradosAsync(
                    _banco, _execucao.Id, _filtro, _caminhosIndisponiveis);
                if (_selecionados.Concat(encontrados).Distinct().Take(10001).Count() > 10000)
                    throw new InvalidOperationException(
                        "A seleção acumulada excede 10.000 arquivos. Limpe a seleção ou refine os filtros.");
                foreach (var id in encontrados) _selecionados.Add(id);
                AtualizarMarcacoes();
                ExibirEstado();
            }
            catch (Exception ex) { _estado.Text = "Falha na seleção global: " + ex.Message; }
        };
        _limpar.Click += (_, _) =>
        {
            _selecionados.Clear();
            AtualizarMarcacoes();
            ExibirEstado();
        };
        _mover.Click += async (_, _) => await ExecutarSelecaoAsync();
        _cancelar.Click += (_, _) => _cancelamento?.Cancel();
        Shown += async (_, _) => await CarregarAsync(0);
        FormClosing += (_, e) =>
        {
            if (!_executando) return;
            _cancelamento?.Cancel();
            e.Cancel = true;
            _estado.Text = "Finalizando operação auditada antes de fechar...";
        };
    }

    private FiltrosEntradasM10 LerFiltros()
    {
        long? minimo = _minimoMiB.Value > 0
            ? (long)(_minimoMiB.Value * 1024M * 1024M) : null;
        long? maximo = _filtrarMaximo.Checked
            ? (long)(_maximoMiB.Value * 1024M * 1024M) : null;
        return new FiltrosEntradasM10(
            Categoria: _categorias.SelectedIndex > 0 &&
                _categorias.Text is not ("Duplicata exata" or "Incompatibilidade documentada" or
                    "Sem alertas detectados" or "Com indícios ou alertas")
                    ? _categorias.Text : null,
            Extensao: string.IsNullOrWhiteSpace(_extensao.Text) ? null
                : _extensao.Text.Trim().StartsWith('.') ? _extensao.Text.Trim()
                : "." + _extensao.Text.Trim(),
            MinimoBytes: minimo, MaximoBytes: maximo,
            AcessadoDesde: _filtrarData.Checked
                ? DateOnly.FromDateTime(_desde.Value.Date) : null,
            AcessadoAte: _filtrarData.Checked
                ? DateOnly.FromDateTime(_ate.Value.Date) : null,
            ApenasIncompatibilidades: _incomp.Checked ||
                _categorias.Text == "Incompatibilidade documentada",
            ApenasDuplicatas: _duplicatas.Checked ||
                _categorias.Text == "Duplicata exata",
            ApenasAcimaLimite: _limite.Checked,
            TrechoCaminho: _trecho.Text.Trim(),
            ApenasSemAlertas: _categorias.Text == "Sem alertas detectados",
            ApenasComAlertas: _categorias.Text == "Com indícios ou alertas");
    }

    private async Task AtualizarCaminhosIndisponiveisAsync()
    {
        var bloqueados = await ConsultaQuarentenaFiltradaM11.CaminhosIndisponiveisAsync(
            ServicoQuarentenaM7.BancoAuditoriaPadrao);
        _caminhosIndisponiveis.Clear();
        _caminhosIndisponiveis.UnionWith(bloqueados);
    }

    private async Task CarregarAsync(int pagina)
    {
        if (_carregando || _executando || pagina < 0) return;
        _carregando = true;
        _buscar.Enabled = false;
        try
        {
            await AtualizarCaminhosIndisponiveisAsync();
            var consulta = await ConsultaEntradasFiltradasM10.ConsultarAsync(
                _banco, _execucao.Id, _filtro, pagina);
            if (pagina > 0 && consulta.Entradas.Count == 0)
            {
                pagina = (int)Math.Max(0, (consulta.Total - 1) /
                    ConsultaHistoricoSqliteM6.TamanhoPagina);
                consulta = await ConsultaEntradasFiltradasM10.ConsultarAsync(
                    _banco, _execucao.Id, _filtro, pagina);
            }
            _pagina = pagina;
            _totalFiltrado = consulta.Total;
            _paginaAtual = consulta.Entradas.Where(e =>
                !_caminhosIndisponiveis.Contains(e.Caminho)).ToList();
            AtualizarMarcacoes();
            _anterior.Enabled = pagina > 0;
            _proximo.Enabled = (long)(pagina + 1) *
                ConsultaHistoricoSqliteM6.TamanhoPagina < consulta.Total;
            ExibirEstado();
        }
        catch (Exception ex) { _estado.Text = "Consulta falhou: " + ex.Message; }
        finally { _carregando = false; _buscar.Enabled = true; }
    }

    private void AtualizarMarcacoes()
    {
        _carregando = true;
        try
        {
            _grade.Rows.Clear();
            foreach (var e in _paginaAtual)
            {
                _grade.Rows.Add(_selecionados.Contains(e.Id), e.Caminho,
                    e.TamanhoBytes ?? 0,
                    (e.CategoriasJson == "[]" ? "" : e.CategoriasJson) +
                        (e.AcimaDoLimitePessoal ? " | ACIMA LIMITE" : "") +
                        (e.EhDuplicataExata ? " | DUPLICATA EXATA" : "") +
                        (e.VerificacoesJson.Contains("IncompatibilidadeDocumentada",
                            StringComparison.Ordinal) ? " | INCOMPATIBILIDADE" : "") +
                        (e.VerificacoesJson.Contains("\"Estado\":\"Alerta\"",
                            StringComparison.Ordinal) ? " | ALERTA COMPATIBILIDADE" : "") +
                        (e.CategoriasJson == "[]" && !e.AcimaDoLimitePessoal &&
                         !e.EhDuplicataExata &&
                         !e.VerificacoesJson.Contains("IncompatibilidadeDocumentada",
                             StringComparison.Ordinal) &&
                         !e.VerificacoesJson.Contains("\"Estado\":\"Alerta\"",
                             StringComparison.Ordinal) ? "SEM ALERTAS DETECTADOS (não significa aprovado)" : ""),
                    e.AcessadoUtc ?? "Indisponível", e.ModificadoUtc ?? string.Empty, e.Id);
            }
        }
        finally { _carregando = false; }
    }

    private void ExibirEstado() =>
        _estado.Text = $"Histórico filtrado: {_totalFiltrado:N0} | Disponíveis nesta página: {_paginaAtual.Count:N0}" +
            $" | Página {_pagina + 1} | Marcados: {_selecionados.Count:N0}. " +
            "Itens em quarentena ficam ocultos. Sem alertas não significa aprovado.";

    private async Task ExecutarSelecaoAsync()
    {
        if (_selecionados.Count == 0 || _executando) return;
        if (_execucao.Estado != "Concluida")
        {
            MessageBox.Show(this, "Apenas análises concluídas podem enviar arquivos à quarentena.");
            return;
        }
        var ids = _selecionados.OrderBy(x => x).ToArray();
        // Não mudar a lista implicitamente após a autorização; o lote é
        // exatamente a fotografia dos IDs marcados no momento da confirmação.
        var quantidade = ids.Length;
        if (quantidade > 10000)
        {
            MessageBox.Show(this, "Limite por operação: 10.000 arquivos explicitamente marcados. " +
                "Divida a seleção em lotes menores para facilitar auditoria e retomada.");
            return;
        }
        IReadOnlyList<EntradaHistoricaM6> entradas;
        long tamanho;
        try
        {
            await AtualizarCaminhosIndisponiveisAsync();
            entradas = await ConsultaEntradasFiltradasM10.ConsultarSelecionadasAsync(
                _banco, _execucao.Id, ids);
            if (entradas.Any(e => _caminhosIndisponiveis.Contains(e.Caminho)))
                throw new InvalidOperationException(
                    "Há arquivos já ocupados pela quarentena. Limpe a seleção e recarregue a lista.");
            tamanho = entradas.Aggregate(0L,
                (soma, e) => checked(soma + (e.TamanhoBytes ?? 0)));
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "Não foi possível preparar a prévia: " + ex.Message);
            return;
        }
        var semAlertas = entradas.Count(e => e.CategoriasJson == "[]" &&
            !e.AcimaDoLimitePessoal && !e.EhDuplicataExata &&
            !e.VerificacoesJson.Contains("IncompatibilidadeDocumentada", StringComparison.Ordinal) &&
            !e.VerificacoesJson.Contains("\"Estado\":\"Alerta\"", StringComparison.Ordinal));
        var caminhosDeExemplo = string.Join(Environment.NewLine,
            entradas.Take(12).Select(e => "  • " + e.Caminho));
        var avisoExcedentes = entradas.Count > 12
            ? Environment.NewLine + $"... e mais {entradas.Count - 12:N0} arquivo(s) " +
              "marcados em outras páginas/filtros." : "";
        var resposta = MessageBox.Show(this,
            $"CONFIRMAR A MOVIMENTAÇÃO PARA QUARENTENA DE {quantidade:N0} ARQUIVOS?" +
            Environment.NewLine + $"Total histórico: {tamanho:N0} bytes" +
            Environment.NewLine + $"Sem alertas detectados no lote: {semAlertas:N0} (não significa aprovado)." +
            Environment.NewLine + $"Origem da análise: {_execucao.DiretorioRaiz}" +
            Environment.NewLine + $"Raiz da quarentena: {_destino}" +
            Environment.NewLine + "APENAS IDs INDIVIDUALMENTE SELECIONADOS." +
            Environment.NewLine + "Caminhos incluídos (amostra):" +
            Environment.NewLine + caminhosDeExemplo + avisoExcedentes +
            Environment.NewLine + Environment.NewLine +
            "Cada arquivo será revalidado pelo M7 (tamanho, data, SHA-256)." +
            Environment.NewLine +
            "Se algum arquivo mudou, somente esse item falhará. " +
            "Os sucessos anteriores NÃO serão revertidos automaticamente." +
            Environment.NewLine +
            "Não haverá exclusões nem substituições. Negar NÃO altera arquivos.",
            "CLEAR — Confirmação humana obrigatória de lote",
            MessageBoxButtons.YesNo, MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2);
        if (resposta != DialogResult.Yes) return;

        _executando = true;
        _cancelamento = new CancellationTokenSource();
        _mover.Enabled = false;
        _cancelar.Enabled = true;
        _buscar.Enabled = false;
        _selecionarPagina.Enabled = false;
        _selecionarTodos.Enabled = false;
        _grade.Enabled = false;
        _limpar.Enabled = false;
        var cronometro = Stopwatch.StartNew();
        long bytesConcluidos = 0;
        int sucesso = 0, falha = 0, processados = 0;
        _erros.Clear();
        try
        {
            foreach (var entrada in entradas)
            {
                if (_cancelamento.IsCancellationRequested) break;
                var id = entrada.Id;
                try
                {
                    var plano = await ServicoQuarentenaM7.PrepararAsync(
                        entrada, _execucao, _destino, _cancelamento.Token);
                    // Autorização global deste lote é explícita; não confundir
                    // com seleção inferida automaticamente de filtros.
                    await ServicoQuarentenaM7.ExecutarAsync(plano,
                        confirmacaoHumanaExplicita: true,
                        ServicoQuarentenaM7.BancoAuditoriaPadrao,
                        _cancelamento.Token);
                    sucesso++;
                    _selecionados.Remove(id);
                }
                catch (OperationCanceledException) when (_cancelamento.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    falha++;
                    _erros.AppendText($"ID {id}: {ex.Message}" +
                        Environment.NewLine);
                }
                finally
                {
                    processados++;
                    bytesConcluidos += entrada.TamanhoBytes ?? 0;
                    var eta = EstimativaRestante.Calcular(
                        processados, quantidade, bytesConcluidos, tamanho, cronometro.Elapsed);
                    _estado.Text = $"QUARENTENA: {processados:N0}/{quantidade:N0} revisados" +
                        $" | {sucesso:N0} movidos | {falha:N0} falhas" +
                        $" | decorrido {cronometro.Elapsed.ToString(@"hh\:mm\:ss")}" +
                        $" | {EstimativaRestante.Exibir(eta)}";
                }
            }
        }
        finally
        {
            _cancelamento.Dispose();
            _cancelamento = null;
            _executando = false;
            _mover.Enabled = true;
            _cancelar.Enabled = false;
            _buscar.Enabled = true;
            _selecionarPagina.Enabled = true;
            _selecionarTodos.Enabled = true;
            _grade.Enabled = true;
            _limpar.Enabled = true;
            await CarregarAsync(_pagina);
        }
        MessageBox.Show(this,
            $"Lote encerrado. Movidos: {sucesso:N0}. Falhas: {falha:N0}." +
            Environment.NewLine + "Verifique a aba Auditoria da quarentena." +
            Environment.NewLine + "Nenhum item não selecionado foi movido.",
            "CLEAR — Resultado da seleção",
            MessageBoxButtons.OK, falha > 0 ? MessageBoxIcon.Warning
                                            : MessageBoxIcon.Information);
    }
}
