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
    private readonly Dictionary<long, (EntradaHistoricaM6 Entrada, long? Grupo)> _selecionados = [];
    private readonly Dictionary<long, string> _protegidos = [];
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
        { Format = DateTimePickerFormat.Short, Width = 165 };
    private readonly DateTimePicker _ate = new()
        { Format = DateTimePickerFormat.Short, Width = 165 };
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
    private bool _ajustandoSelecao;
    private bool _atualizandoGrade;
    private CancellationTokenSource? _cancelamento;
    private bool PodeEditarSelecao => !_carregando && !_executando && !_ajustandoSelecao && _mover.Enabled;

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
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 104));

        var filtros = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, AutoScroll = true,
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

        var barra = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill };
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
        _grade.Columns.Add("Acessado", "Último acesso (horário local)");
        _grade.Columns.Add("Modificado", "Modificação (horário local)");
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
        _grade.CellBeginEdit += (_, e) => e.Cancel = !PodeEditarSelecao;
        _grade.CellValueChanged += async (_, e) =>
        {
            if (_atualizandoGrade || !PodeEditarSelecao || e.RowIndex < 0 || e.ColumnIndex != 0) return;
            var linha = _grade.Rows[e.RowIndex];
            if (linha.Tag is not EntradaHistoricaM6 entrada) return;
            if (linha.Cells["Marcado"].Value is not true)
            {
                if (_selecionados.Remove(entrada.Id, out var removido) && removido.Grupo is { } grupo)
                    _protegidos.Remove(grupo);
                AtualizarSelecaoVisual();
                ExibirEstado();
                return;
            }
            AtualizarSelecaoVisual();
            await AjustarSelecaoAsync(async () =>
            {
                if (_selecionados.Count >= 10000)
                    throw new InvalidOperationException("O lote não pode exceder 10.000 arquivos.");
                if (!entrada.EhDuplicataExata)
                {
                    _selecionados[entrada.Id] = (entrada, null);
                    return;
                }
                var caminhos = _selecionados.Values.Select(v => v.Entrada.Caminho)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
                var marcacao = await ProtecaoDuplicatasNaQuarentena.AvaliarMarcacaoAsync(
                    _banco, _execucao.Id, entrada, caminhos, _caminhosIndisponiveis);
                if (marcacao.CaminhoProtegido is { } protegido)
                    _protegidos[marcacao.Grupo] = protegido;
                else
                    _protegidos.Remove(marcacao.Grupo);
                if (!marcacao.Permitida)
                    throw new InvalidOperationException("Para marcar esta cópia, desmarque outra do mesmo grupo.");
                _selecionados[entrada.Id] = (entrada, marcacao.Grupo);
            });
        };

        _buscar.Click += async (_, _) =>
        {
            if (!PodeEditarSelecao) return;
            try
            {
                _filtro = LerFiltros();
                await CarregarAsync(0);
            }
            catch (Exception ex) { _estado.Text = "Filtros inválidos: " + ex.Message; }
        };
        _anterior.Click += async (_, _) => { if (PodeEditarSelecao) await CarregarAsync(_pagina - 1); };
        _proximo.Click += async (_, _) => { if (PodeEditarSelecao) await CarregarAsync(_pagina + 1); };
        _selecionarPagina.Click += async (_, _) => await AjustarSelecaoAsync(async () =>
        {
            await AtualizarCaminhosIndisponiveisAsync();
            await SelecionarEmLoteAsync(_paginaAtual.Select(x => x.Id));
        });
        _selecionarTodos.Click += async (_, _) =>
        {
            if (!PodeEditarSelecao) return;
            var resposta = MessageBox.Show(this,
                "Selecionar TODOS os arquivos elegíveis que atendem aos filtros atuais? " +
                "O CLEAR reserva automaticamente uma cópia por grupo quando necessário. " +
                "Limite: 10.000 itens por operação. " +
                "A movimentação exigirá confirmação posterior.",
                "CLEAR — Seleção global explícita", MessageBoxButtons.YesNo,
                MessageBoxIcon.Question, MessageBoxDefaultButton.Button2);
            if (resposta != DialogResult.Yes) return;
            await AjustarSelecaoAsync(async () =>
            {
                await AtualizarCaminhosIndisponiveisAsync();
                var encontrados = await ConsultaEntradasFiltradasM10.SelecionarTodosFiltradosAsync(
                    _banco, _execucao.Id, _filtro, _caminhosIndisponiveis);
                await SelecionarEmLoteAsync(encontrados);
            });
        };
        _limpar.Click += (_, _) =>
        {
            if (!PodeEditarSelecao) return;
            _selecionados.Clear();
            _protegidos.Clear();
            AtualizarSelecaoVisual();
            ExibirEstado();
        };
        _mover.Click += async (_, _) =>
        {
            if (!PodeEditarSelecao) return;
            _mover.Enabled = false;
            try { await ExecutarSelecaoAsync(); }
            finally { _mover.Enabled = true; }
        };
        _cancelar.Click += (_, _) => _cancelamento?.Cancel();
        TemaVisual.Registrar(this);
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
            ApenasComAlertas: _categorias.Text == "Com indícios ou alertas",
            FusoHorarioAcesso: _filtrarData.Checked ? TimeZoneInfo.Local : null);
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
                _banco, _execucao.Id, _filtro, pagina,
                    caminhosIndisponiveis: _caminhosIndisponiveis);
            if (pagina > 0 && consulta.Entradas.Count == 0)
            {
                pagina = (int)Math.Max(0, (consulta.Total - 1) /
                    ConsultaHistoricoSqliteM6.TamanhoPagina);
                consulta = await ConsultaEntradasFiltradasM10.ConsultarAsync(
                    _banco, _execucao.Id, _filtro, pagina,
                    caminhosIndisponiveis: _caminhosIndisponiveis);
            }
            _pagina = pagina;
            _totalFiltrado = consulta.Total;
            _paginaAtual = consulta.Entradas.ToList();
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
        _atualizandoGrade = true;
        try
        {
            _grade.Rows.Clear();
            foreach (var e in _paginaAtual)
            {
                var indice = _grade.Rows.Add(_selecionados.ContainsKey(e.Id), e.Caminho,
                    e.TamanhoBytes ?? 0,
                    TextoClassificacoes(e),
                    ApresentacaoResultados.HorarioLocal(e.AcessadoUtc),
                    ApresentacaoResultados.HorarioLocal(e.ModificadoUtc), e.Id);
                _grade.Rows[indice].Tag = e;
            }
        }
        finally { _atualizandoGrade = false; }
        AtualizarSelecaoVisual();
    }

    private void AtualizarSelecaoVisual()
    {
        var protegidos = _protegidos.Values.ToHashSet(StringComparer.OrdinalIgnoreCase);
        _atualizandoGrade = true;
        try
        {
            foreach (DataGridViewRow linha in _grade.Rows)
            {
                if (linha.Tag is not EntradaHistoricaM6 entrada) continue;
                var protegido = protegidos.Contains(entrada.Caminho);
                var marcador = linha.Cells["Marcado"];
                marcador.Value = _selecionados.ContainsKey(entrada.Id);
                marcador.ReadOnly = protegido;
                marcador.ToolTipText = protegido ? "Desmarque outra cópia do grupo para selecionar esta." : "";
                linha.Cells["Categorias"].Value = (protegido ? "MANTER NA ORIGEM | " : "") +
                    TextoClassificacoes(entrada);
            }
        }
        finally { _atualizandoGrade = false; }
    }

    private async Task AjustarSelecaoAsync(Func<Task> alterar)
    {
        if (!PodeEditarSelecao) return;
        _ajustandoSelecao = true;
        string? falha = null;
        try { await alterar(); }
        catch (Exception ex) { falha = ex.Message; }
        finally { _ajustandoSelecao = false; }
        AtualizarSelecaoVisual();
        ExibirEstado(falha);
    }

    private async Task SelecionarEmLoteAsync(IEnumerable<long> novos)
    {
        var ids = _selecionados.Keys.Concat(novos).Distinct().Take(10001).ToArray();
        if (ids.Length == 0) return;
        if (ids.Length > 10000)
            throw new InvalidOperationException("O lote não pode exceder 10.000 arquivos.");
        var entradas = await ConsultaEntradasFiltradasM10.ConsultarSelecionadasAsync(_banco, _execucao.Id, ids);
        var disponiveis = entradas.Where(e => !_caminhosIndisponiveis.Contains(e.Caminho)).ToArray();
        var plano = await ProtecaoDuplicatasNaQuarentena.PrepararAsync(_banco, _execucao.Id,
            disponiveis, _caminhosIndisponiveis);
        AplicarSelecao(plano);
    }

    private void AplicarSelecao(SelecaoQuarentenaProtegida plano)
    {
        _selecionados.Clear();
        _protegidos.Clear();
        foreach (var grupo in plano.Grupos)
        {
            var numeroGrupo = grupo.Referencias.FirstOrDefault()?.Grupo;
            foreach (var entrada in grupo.Entradas) _selecionados.Add(entrada.Id, (entrada, numeroGrupo));
        }
        foreach (var referencia in plano.ReferenciasUnicas)
            _protegidos.Add(referencia.Grupo, referencia.Caminho);
    }

    private static bool SemAlertasDetectados(EntradaHistoricaM6 e) =>
        e.CategoriasJson == "[]" && !e.AcimaDoLimitePessoal && !e.EhDuplicataExata &&
        !e.VerificacoesJson.Contains("IncompatibilidadeDocumentada", StringComparison.Ordinal) &&
        !e.VerificacoesJson.Contains("\"Estado\":\"Alerta\"", StringComparison.Ordinal);

    private static string TextoClassificacoes(EntradaHistoricaM6 e)
    {
        if (SemAlertasDetectados(e))
            return "Sem alertas detectados (não significa aprovado)";
        var marcadores = new List<string>();
        if (e.CategoriasJson != "[]") marcadores.Add(ApresentacaoResultados.Categorias(e.CategoriasJson));
        if (e.AcimaDoLimitePessoal) marcadores.Add("ACIMA LIMITE");
        if (e.EhDuplicataExata) marcadores.Add("DUPLICATA EXATA");
        if (e.VerificacoesJson.Contains("IncompatibilidadeDocumentada", StringComparison.Ordinal))
            marcadores.Add("INCOMPATIBILIDADE");
        if (e.VerificacoesJson.Contains("\"Estado\":\"Alerta\"", StringComparison.Ordinal))
            marcadores.Add("ALERTA COMPATIBILIDADE");
        return string.Join(" | ", marcadores);
    }

    private void ExibirEstado(string? aviso = null) =>
        _estado.Text = $"Disponíveis nos filtros: {_totalFiltrado:N0} | Página {_pagina + 1}" +
            $" | Marcados para mover: {_selecionados.Count:N0} | Mantidos na origem: {_protegidos.Count:N0}. " +
            (aviso ?? (_protegidos.Count > 0 ? "Para trocar a cópia mantida, desmarque outra do mesmo grupo." :
                "Itens em quarentena ficam ocultos."));

    private async Task ExecutarSelecaoAsync()
    {
        if (_selecionados.Count == 0 || _executando) return;
        if (_execucao.Estado != "Concluida")
        {
            MessageBox.Show(this, "Apenas análises concluídas podem enviar arquivos à quarentena.");
            return;
        }
        var ids = _selecionados.Keys.OrderBy(x => x).ToArray();
        if (ids.Length > 10000)
        {
            MessageBox.Show(this, "Limite por operação: 10.000 arquivos explicitamente marcados. " +
                "Divida a seleção em lotes menores para facilitar auditoria e retomada.");
            return;
        }
        SelecaoQuarentenaProtegida selecao;
        try
        {
            await AtualizarCaminhosIndisponiveisAsync();
            var marcadas = await ConsultaEntradasFiltradasM10.ConsultarSelecionadasAsync(
                _banco, _execucao.Id, ids);
            if (marcadas.Any(e => _caminhosIndisponiveis.Contains(e.Caminho)))
                throw new InvalidOperationException(
                    "Há arquivos já ocupados pela quarentena. Limpe a seleção e recarregue a lista.");
            selecao = await ProtecaoDuplicatasNaQuarentena.PrepararAsync(
                _banco, _execucao.Id, marcadas, _caminhosIndisponiveis);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "Não foi possível preparar a prévia: " + ex.Message);
            return;
        }
        AplicarSelecao(selecao);
        AtualizarSelecaoVisual();
        ExibirEstado();
        var entradas = selecao.Grupos.SelectMany(g => g.Entradas).OrderBy(e => e.Id).ToArray();
        var quantidade = entradas.Length;
        if (quantidade == 0)
        {
            MessageBox.Show(this, "Os arquivos marcados são as cópias que precisam ficar na origem.",
                "CLEAR", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        var tamanho = entradas.Sum(e => e.TamanhoBytes ?? 0);
        var semAlertas = entradas.Count(SemAlertasDetectados);
        var caminhos = string.Join(Environment.NewLine,
            entradas.Select(e => "  • " + e.Caminho));
        var mantidos = selecao.ReferenciasUnicas.Count == 0 ? "" :
            $"{selecao.ReferenciasUnicas.Count:N0} cópia(s) que permanecem na origem:" +
            Environment.NewLine + string.Join(Environment.NewLine,
                selecao.ReferenciasUnicas.Select(e => "  • " + e.Caminho)) + Environment.NewLine;
        var resposta = DialogoRevisao.Confirmar(this,
            $"CONFIRMAR A MOVIMENTAÇÃO PARA QUARENTENA DE {quantidade:N0} ARQUIVOS?" +
            Environment.NewLine + $"Total histórico: {tamanho:N0} bytes" +
            Environment.NewLine + $"Sem alertas detectados no lote: {semAlertas:N0} (não significa aprovado)." +
            Environment.NewLine + $"Origem da análise: {_execucao.DiretorioRaiz}" +
            Environment.NewLine + $"Raiz da quarentena: {_destino}" +
            Environment.NewLine + mantidos + "Arquivos que serão movidos:" +
            Environment.NewLine + caminhos +
            Environment.NewLine + Environment.NewLine +
            "Cada arquivo será revalidado pelo M7 (tamanho, data, SHA-256)." +
            Environment.NewLine +
            "Se algum arquivo mudou, somente esse item falhará. " +
            "Os sucessos anteriores NÃO serão revertidos automaticamente." +
            Environment.NewLine +
            "Não haverá exclusões nem substituições. Negar NÃO altera arquivos.");
        if (resposta != DialogResult.Yes) return;

        _executando = true;
        _cancelamento = new CancellationTokenSource();
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
            foreach (var grupo in selecao.Grupos)
            {
                if (_cancelamento.IsCancellationRequested) break;
                FileStream? fluxoReferencia;
                try
                {
                    fluxoReferencia = await ProtecaoDuplicatasNaQuarentena.AbrirReferenciaAsync(
                        grupo, _execucao.DiretorioRaiz, _cancelamento.Token);
                }
                catch (OperationCanceledException) when (_cancelamento.IsCancellationRequested)
                {
                    break;
                }
                catch (IOException ex)
                {
                    falha += grupo.Entradas.Count;
                    _erros.AppendText($"Grupo com {grupo.Entradas.Count:N0} arquivo(s): {ex.Message}" +
                        Environment.NewLine);
                    foreach (var entrada in grupo.Entradas) RegistrarProgresso(entrada);
                    continue;
                }
                await using var referencia = fluxoReferencia;
                foreach (var entrada in grupo.Entradas)
                {
                    if (_cancelamento.IsCancellationRequested) break;
                    try
                    {
                        var plano = await ProtecaoDuplicatasNaQuarentena.PrepararMovimentoAsync(
                            grupo, entrada, _execucao, _destino, _cancelamento.Token);
                        await ServicoQuarentenaM7.ExecutarAsync(plano, confirmacaoHumanaExplicita: true,
                            ServicoQuarentenaM7.BancoAuditoriaPadrao, _cancelamento.Token);
                        sucesso++;
                        _selecionados.Remove(entrada.Id);
                    }
                    catch (OperationCanceledException) when (_cancelamento.IsCancellationRequested) { break; }
                    catch (Exception ex)
                    {
                        falha++;
                        _erros.AppendText($"ID {entrada.Id}: {ex.Message}" + Environment.NewLine);
                    }
                    finally { RegistrarProgresso(entrada); }
                }
            }
        }
        finally
        {
            _cancelamento.Dispose();
            _cancelamento = null;
            _executando = false;
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
            Environment.NewLine + $"Cópias reservadas para permanecer na origem: {selecao.ReferenciasUnicas.Count:N0}." +
            Environment.NewLine + "Verifique a aba Auditoria da quarentena." +
            Environment.NewLine + "Nenhum item não selecionado foi movido.",
            "CLEAR — Resultado da seleção",
            MessageBoxButtons.OK, falha > 0 ? MessageBoxIcon.Warning
                                            : MessageBoxIcon.Information);

        void RegistrarProgresso(EntradaHistoricaM6 entrada)
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
