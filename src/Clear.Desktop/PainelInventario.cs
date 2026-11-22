using System.Text.Json;
using Clear.Infrastructure.Historico;
using Clear.Infrastructure.Quarentena;

namespace Clear.Desktop;

internal sealed partial class PainelInventario : UserControl
{
    private sealed record Categoria(string Rotulo, string? Codigo = null)
    {
        public override string ToString() => Rotulo;
    }

    private const int LimiteLote = ConsultaQuarentenaFiltradaM11.MaximoLote;
    private const int TamanhoPagina = ConsultaHistoricoSqliteM6.TamanhoPagina;
    private readonly string _historico;
    private readonly string _auditoria;
    private readonly Func<string> _raizQuarentena;
    private readonly ComboBox _visualizacao = Lista("Arquivos", "Ocorrências");
    private readonly ComboBox _situacao = Lista("No acervo", "Em quarentena");
    private readonly ComboBox _categoria = Lista();
    private readonly NumericUpDown _grupo = new() { Minimum = 0, Maximum = 1, Width = 72 };
    private readonly TextBox _extensao = new() { Width = 74, PlaceholderText = ".pdf" };
    private readonly TextBox _caminho = new() { Width = 151, PlaceholderText = "Pasta ou arquivo" };
    private readonly NumericUpDown _minimo = Tamanho();
    private readonly NumericUpDown _maximo = Tamanho();
    private readonly CheckBox _usarMaximo = new() { Text = "Máx. MiB", AutoSize = true };
    private readonly DateTimePicker _desde = DataOpcional();
    private readonly DateTimePicker _ate = DataOpcional();
    private readonly Button _aplicar = Botao("Aplicar filtros");
    private readonly Button _datas = Botao("Período de acesso ▾");
    private readonly Button _selecionarPagina = Botao("Selecionar página");
    private readonly Button _selecionarTudo = Botao("Selecionar filtrados (até 10.000)");
    private readonly Button _limpar = Botao("Limpar seleção");
    private readonly BotaoLote _operacao = new() { Text = "Mover para quarentena", AutoSize = true };
    private readonly Button _cancelar = Botao("Cancelar lote");
    private readonly Button _anterior = Botao("Anterior");
    private readonly Button _proxima = Botao("Próxima");
    private readonly Button _abrirDetalhes = Botao("Detalhes do arquivo ▾");
    private readonly Label _estado = new() { AutoSize = true, Margin = new Padding(8, 9, 3, 3) };
    private readonly TextBox _detalhes = new()
    {
        Dock = DockStyle.Fill, ReadOnly = true, Multiline = true, ScrollBars = ScrollBars.Vertical
    };
    private readonly DataGridView _grade = new()
    {
        Dock = DockStyle.Fill, AllowUserToAddRows = false, AllowUserToDeleteRows = false,
        AllowUserToResizeColumns = true, MultiSelect = false, RowHeadersVisible = false,
        SelectionMode = DataGridViewSelectionMode.FullRowSelect, AutoGenerateColumns = false
    };
    private readonly FlowLayoutPanel _filtros;
    private readonly FlowLayoutPanel _periodo;
    private readonly FlowLayoutPanel _acoes;
    private readonly Control _campoGrupo;
    private readonly Control _campoSituacao;
    private readonly Dictionary<long, EntradaHistoricaM6> _marcados = [];
    private readonly Dictionary<long, string> _mantidos = [];
    private Dictionary<string, RegistroQuarentenaM7> _emQuarentena = new(StringComparer.OrdinalIgnoreCase);
    private IReadOnlySet<string> _indisponiveis = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private IReadOnlyList<EntradaHistoricaM6> _itens = [];
    private ResumoExecucaoHistoricaM6? _execucao;
    private FiltrosEntradasM10 _filtro = new();
    private CancellationTokenSource? _lote;
    private long _total;
    private int _pagina;
    private int _foraDoFiltro;
    private bool _ocupado;
    private bool _atualizandoGrade;
    private bool _mostrarDatas;
    private string? _mensagem;

    public bool Ocupado => _ocupado;
    public event EventHandler? OcupacaoAlterada;
    public event EventHandler? AuditoriaAlterada;
    private bool Restaurando => _situacao.SelectedIndex == 1;
    private bool Ocorrencias => _visualizacao.SelectedIndex == 1;
    private bool PodeMarcar => _execucao is not null && !Ocorrencias &&
        (Restaurando || _execucao.Estado == "Concluida");

    public PainelInventario(string historico, string auditoria, Func<string> raizQuarentena)
    {
        _historico = historico;
        _auditoria = auditoria;
        _raizQuarentena = raizQuarentena;
        Dock = DockStyle.Fill;
        SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
        _categoria.Width = 215;
        _categoria.Items.AddRange([
            new Categoria("Todas"),
            new Categoria("Arquivo vazio", "ArquivoVazio"),
            new Categoria("Backup ou temporário", "CopiaBackupOuTemporario"),
            new Categoria("Data antiga", "SinalTemporal"),
            new Categoria("Duplicata exata", "DuplicataExata"),
            new Categoria("Caminho potencialmente incompatível", "CaminhoPotencialmenteIncompativel"),
            new Categoria("Incompatibilidade documentada", "Incompatibilidade"),
            new Categoria("Acima do limite pessoal", "Limite"),
            new Categoria("Sem alertas", "SemAlertas"),
            new Categoria("Com indícios ou alertas", "ComAlertas")
        ]);
        _categoria.SelectedIndex = 0;
        _grupo.AccessibleName = "Grupo de duplicatas; zero exibe todos os grupos";
        _campoGrupo = Campo("Grupo (0 = todos)", _grupo);
        _campoSituacao = Campo("Situação", _situacao);
        _filtros = Faixa(Campo("Categoria", _categoria), _campoGrupo, Campo("Extensão", _extensao),
            Campo("Caminho contém", _caminho), Campo("Mín. MiB", _minimo),
            Campo(_usarMaximo, _maximo), _datas, _aplicar);
        _periodo = Faixa(Campo("Acessado desde", _desde), Campo("Acessado até", _ate));
        _acoes = Faixa(_selecionarPagina, _selecionarTudo, _limpar, _operacao, _cancelar);
        _cancelar.Visible = false;
        var painelDetalhes = new Panel { Dock = DockStyle.Fill, Height = 112, Visible = false };
        painelDetalhes.Controls.Add(_detalhes);
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1,
            RowCount = 7, Padding = new Padding(8) };
        for (var i = 0; i < 7; i++)
            layout.RowStyles.Add(i == 4 ? new RowStyle(SizeType.Percent, 100) : new RowStyle());
        layout.Controls.Add(Faixa(Campo("Visualização", _visualizacao), _campoSituacao), 0, 0);
        layout.Controls.Add(_filtros, 0, 1);
        layout.Controls.Add(_periodo, 0, 2);
        layout.Controls.Add(_acoes, 0, 3);
        layout.Controls.Add(_grade, 0, 4);
        layout.Controls.Add(Faixa(_anterior, _proxima, _abrirDetalhes, _estado), 0, 5);
        layout.Controls.Add(painelDetalhes, 0, 6);
        Controls.Add(layout);

        _categoria.SelectedIndexChanged += (_, _) => AtualizarBotoes();
        _usarMaximo.CheckedChanged += (_, _) => _maximo.Enabled = _usarMaximo.Checked;
        _maximo.Enabled = false;
        _datas.Click += (_, _) =>
        {
            _mostrarDatas = !_mostrarDatas;
            _datas.Text = _mostrarDatas ? "Período de acesso ▴" : "Período de acesso ▾";
            AtualizarBotoes();
        };
        _abrirDetalhes.Click += (_, _) =>
        {
            painelDetalhes.Visible = !painelDetalhes.Visible;
            _abrirDetalhes.Text = painelDetalhes.Visible ? "Detalhes do arquivo ▴" : "Detalhes do arquivo ▾";
            ExibirDetalhes();
        };
        _visualizacao.SelectedIndexChanged += async (_, _) => await OperarAsync(() => ConsultarAsync(0));
        _situacao.SelectedIndexChanged += async (_, _) => await OperarAsync(async () =>
        {
            LimparMarcacoes();
            await ConsultarAsync(0);
        });
        _aplicar.Click += async (_, _) => await OperarAsync(async () =>
        {
            var novoFiltro = LerFiltros();
            if (novoFiltro.MaximoBytes < novoFiltro.MinimoBytes || novoFiltro.AcessadoAte < novoFiltro.AcessadoDesde)
                throw new ArgumentException("O limite final deve ser igual ou maior que o inicial.");
            _filtro = novoFiltro;
            await ConsultarAsync(0);
        });
        _anterior.Click += async (_, _) => await OperarAsync(() => ConsultarAsync(_pagina - 1));
        _proxima.Click += async (_, _) => await OperarAsync(() => ConsultarAsync(_pagina + 1));
        _selecionarPagina.Click += async (_, _) => await OperarAsync(() =>
            MarcarEmLoteAsync(_itens.Where(Elegivel).Select(e => e.Id)));
        _selecionarTudo.Click += async (_, _) => await OperarAsync(SelecionarFiltradosAsync);
        _limpar.Click += (_, _) => { LimparMarcacoes(); AtualizarMarcacoes(); AtualizarBotoes(); };
        _operacao.Click += async (_, _) => await OperarAsync(ExecutarLoteAsync);
        _cancelar.Click += (_, _) => _lote?.Cancel();
        _grade.SelectionChanged += (_, _) => ExibirDetalhes();
        _grade.CurrentCellDirtyStateChanged += (_, _) =>
        {
            if (_grade.IsCurrentCellDirty) _grade.CommitEdit(DataGridViewDataErrorContexts.Commit);
        };
        _grade.CellValueChanged += async (_, evento) =>
        {
            if (_atualizandoGrade || evento.RowIndex < 0 || evento.ColumnIndex != 0 ||
                _grade.Rows[evento.RowIndex].Tag is not EntradaHistoricaM6 item) return;
            bool marcar = _grade.Rows[evento.RowIndex].Cells[0].Value is true;
            await OperarAsync(() => MarcarIndividualAsync(item, marcar));
        };
        AtualizarBotoes();
    }

    public Task ExibirAnaliseAsync(ResumoExecucaoHistoricaM6? execucao) => OperarAsync(async () =>
    {
        if (_execucao?.Id != execucao?.Id) LimparMarcacoes();
        _execucao = execucao;
        _grupo.Maximum = Math.Max(1, execucao?.GruposDuplicatas ?? 0);
        if (_filtro.GrupoDuplicata > _grupo.Maximum)
            _filtro = _filtro with { GrupoDuplicata = _grupo.Value > 0 ? (long)_grupo.Value : null };
        await ConsultarAsync(0);
    });

    private async Task OperarAsync(Func<Task> acao)
    {
        if (_ocupado) return;
        _ocupado = true;
        _mensagem = null;
        AtualizarBotoes();
        OcupacaoAlterada?.Invoke(this, EventArgs.Empty);
        try { await acao(); }
        catch (Exception erro) { _mensagem = erro.Message; }
        finally
        {
            _ocupado = false;
            AtualizarMarcacoes();
            AtualizarBotoes();
            OcupacaoAlterada?.Invoke(this, EventArgs.Empty);
        }
    }

    private FiltrosEntradasM10 LerFiltros()
    {
        var codigo = (_categoria.SelectedItem as Categoria)?.Codigo;
        var extensao = _extensao.Text.Trim();
        if (extensao.Length > 0 && !extensao.StartsWith('.')) extensao = "." + extensao;
        return new(
            Categoria: codigo is "ArquivoVazio" or "CopiaBackupOuTemporario" or "SinalTemporal"
                or "CaminhoPotencialmenteIncompativel" ? codigo : null,
            Extensao: extensao,
            MinimoBytes: _minimo.Value > 0 ? (long)(_minimo.Value * 1048576) : null,
            MaximoBytes: _usarMaximo.Checked ? (long)(_maximo.Value * 1048576) : null,
            AcessadoDesde: _desde.Checked ? DateOnly.FromDateTime(_desde.Value) : null,
            AcessadoAte: _ate.Checked ? DateOnly.FromDateTime(_ate.Value) : null,
            ApenasIncompatibilidades: codigo == "Incompatibilidade",
            ApenasDuplicatas: codigo == "DuplicataExata",
            ApenasAcimaLimite: codigo == "Limite",
            TrechoCaminho: _caminho.Text.Trim(),
            ApenasSemAlertas: codigo == "SemAlertas",
            ApenasComAlertas: codigo == "ComAlertas",
            FusoHorarioAcesso: TimeZoneInfo.Local,
            GrupoDuplicata: codigo == "DuplicataExata" && _grupo.Value > 0 ? (long)_grupo.Value : null);
    }

    private async Task ConsultarAsync(int pagina)
    {
        _pagina = Math.Max(0, pagina);
        _grade.Rows.Clear();
        _itens = [];
        _total = 0;
        if (_execucao is not { } execucao) return;
        if (Ocorrencias)
        {
            var (lista, totalOcorrencias) = await Task.Run(() => ConsultaHistoricoSqliteM6.ConsultarPaginaOcorrenciasAsync(
                _historico, execucao.Id, _pagina,
                Form1.LerJson<Form1.ResumoSalvo>(execucao.ResumoJson)?.PreservacaoAcesso?.Ocorrencias));
            CriarColunas(("Origem", 150), ("Ocorrência", 235), ("Caminho", 375), ("Detalhe", 465));
            foreach (var item in lista)
                _grade.Rows.Add(item.Origem, item.Tipo,
                    string.IsNullOrWhiteSpace(item.Caminho) ? "Execução" : item.Caminho, item.Descricao);
            _total = totalOcorrencias;
            return;
        }
        var restaurando = Restaurando;
        var (itens, total, ativos, indisponiveis) = await Task.Run(async () =>
        {
            var quarentena = restaurando
                ? await ConsultaQuarentenaFiltradaM11.ConsultarAtivasAsync(_auditoria, execucao.Id)
                : [];
            var mapa = quarentena.GroupBy(r => r.CaminhoOriginal, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.Last(), StringComparer.OrdinalIgnoreCase);
            var bloqueados = restaurando ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                : await ConsultaQuarentenaFiltradaM11.CaminhosIndisponiveisAsync(_auditoria, execucao: execucao.Id);
            var incluidos = restaurando ? mapa.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase) : null;
            var resultado = await ConsultaEntradasFiltradasM10.ConsultarAsync(
                _historico, execucao.Id, _filtro, _pagina, caminhosIndisponiveis: bloqueados,
                caminhosIncluidos: incluidos);
            return (resultado.Entradas, resultado.Total, mapa, bloqueados);
        });
        _emQuarentena = ativos;
        _indisponiveis = indisponiveis;
        if (_pagina > 0 && itens.Count == 0 && _pagina * (long)TamanhoPagina >= total)
        {
            await ConsultarAsync((int)Math.Max(0, (total - 1) / TamanhoPagina));
            return;
        }
        _itens = itens;
        _total = total;
        if (restaurando) RemoverMarcados(e => !_emQuarentena.ContainsKey(e.Caminho));
        else RemoverMarcados(e => _indisponiveis.Contains(e.Caminho));
        AtualizarGrade();
        await ContarForaDoFiltroAsync();
    }

    private void RemoverMarcados(Func<EntradaHistoricaM6, bool> remover)
    {
        foreach (var entrada in _marcados.Values.Where(remover).ToArray()) _marcados.Remove(entrada.Id);
    }

    private void AtualizarGrade()
    {
        var colunas = new List<(string, int)>
        {
            ("Marcar", 58), ("Arquivo", 420), ("Bytes", 105), ("Classificação", 275)
        };
        if (_filtro.ApenasDuplicatas) colunas.Add(("Grupo", 76));
        colunas.Add(("Último acesso (horário local)", 218));
        colunas.Add(("Modificação (horário local)", 218));
        if (Restaurando) colunas.Add(("Caminho na quarentena", 370));
        CriarColunas(colunas.ToArray());
        foreach (var entrada in _itens)
        {
            var valores = new List<object> { _marcados.ContainsKey(entrada.Id), entrada.Caminho,
                (object?)entrada.TamanhoBytes ?? "", Classificacoes(entrada) };
            if (_filtro.ApenasDuplicatas) valores.Add((object?)entrada.GrupoDuplicata ?? "");
            valores.Add(ApresentacaoResultados.HorarioLocal(entrada.AcessadoUtc));
            valores.Add(ApresentacaoResultados.HorarioLocal(entrada.ModificadoUtc));
            if (Restaurando) valores.Add(_emQuarentena.GetValueOrDefault(entrada.Caminho)?.CaminhoDestino ?? "");
            var linha = _grade.Rows[_grade.Rows.Add(valores.ToArray())];
            linha.Tag = entrada;
            linha.Cells[0].ReadOnly = !Elegivel(entrada);
        }
    }

    private void CriarColunas(params (string Nome, int Largura)[] colunas)
    {
        if (_grade.Columns.Cast<DataGridViewColumn>().Select(c => c.HeaderText)
            .SequenceEqual(colunas.Select(c => c.Nome))) return;
        var larguras = _grade.Columns.Cast<DataGridViewColumn>().ToDictionary(c => c.HeaderText, c => c.Width);
        _grade.Columns.Clear();
        foreach (var (nome, largura) in colunas)
        {
            DataGridViewColumn coluna = nome == "Marcar"
                ? new DataGridViewCheckBoxColumn()
                : new DataGridViewTextBoxColumn { ReadOnly = true };
            coluna.HeaderText = nome;
            coluna.Width = larguras.GetValueOrDefault(nome, largura);
            coluna.SortMode = DataGridViewColumnSortMode.NotSortable;
            _grade.Columns.Add(coluna);
        }
    }

    private bool Elegivel(EntradaHistoricaM6 entrada) => PodeMarcar && entrada.Tipo == "Arquivo" &&
        (Restaurando || entrada.TamanhoBytes.HasValue && !string.IsNullOrWhiteSpace(entrada.ModificadoUtc));

    private void ExibirDetalhes()
    {
        if (!_detalhes.Visible) return;
        if (_grade.CurrentRow?.Tag is not EntradaHistoricaM6 item)
        {
            _detalhes.Text = "Selecione um arquivo.";
            return;
        }
        _detalhes.Text = item.Caminho + Environment.NewLine + Classificacoes(item) +
            (item.GrupoDuplicata is long grupo ? $" · Grupo {grupo}" : "") + Environment.NewLine +
            (Restaurando ? "Quarentena: " + _emQuarentena.GetValueOrDefault(item.Caminho)?.CaminhoDestino +
                Environment.NewLine : "") +
            LinhasJson(item.EvidenciasJson) + Environment.NewLine + LinhasJson(item.VerificacoesJson);
    }

    private static string LinhasJson(string valor)
    {
        try
        {
            using var json = JsonDocument.Parse(valor);
            return string.Join(Environment.NewLine, json.RootElement.EnumerateArray().Select(item =>
                "• " + string.Join(" · ", item.EnumerateObject().Select(p => p.Value.ToString()))));
        }
        catch (Exception erro) when (erro is JsonException or InvalidOperationException) { return valor; }
    }

    private static string Classificacoes(EntradaHistoricaM6 item)
    {
        var rotulos = new List<string>();
        if (item.CategoriasJson != "[]") rotulos.Add(ApresentacaoResultados.Categorias(item.CategoriasJson));
        if (item.EhDuplicataExata) rotulos.Add("Duplicata exata");
        if (item.AcimaDoLimitePessoal) rotulos.Add("Acima do limite pessoal");
        if (item.VerificacoesJson.Contains("IncompatibilidadeDocumentada", StringComparison.Ordinal))
            rotulos.Add("Incompatibilidade documentada");
        if (item.VerificacoesJson.Contains("\"Estado\":\"Alerta\"", StringComparison.Ordinal))
            rotulos.Add("Alerta de compatibilidade");
        return rotulos.Count == 0 ? "Sem alertas" : string.Join(" · ", rotulos);
    }

    private void AtualizarBotoes()
    {
        bool podeEditar = _execucao is not null && !_ocupado;
        _visualizacao.Enabled = _situacao.Enabled = podeEditar;
        _campoSituacao.Visible = !Ocorrencias;
        _filtros.Visible = _acoes.Visible = !Ocorrencias;
        _filtros.Enabled = _periodo.Enabled = podeEditar;
        _periodo.Visible = !Ocorrencias && _mostrarDatas;
        _campoGrupo.Visible = _categoria.SelectedItem is Categoria { Codigo: "DuplicataExata" };
        _abrirDetalhes.Visible = !Ocorrencias;
        _anterior.Enabled = podeEditar && _pagina > 0;
        _proxima.Enabled = podeEditar && (_pagina + 1L) * TamanhoPagina < _total;
        _grade.Enabled = !_ocupado;
        _selecionarPagina.Enabled = _selecionarTudo.Enabled = podeEditar && PodeMarcar && _itens.Any(Elegivel);
        _limpar.Enabled = _operacao.Enabled = podeEditar && PodeMarcar && _marcados.Count > 0;
        _operacao.Text = Restaurando ? "Restaurar selecionados" : "Mover para quarentena";
        _cancelar.Visible = _lote is not null;
        if (_lote is null)
            _estado.Text = _execucao is null ? "Selecione uma análise ou inicie uma nova."
                : Ocorrencias ? $"{_total:N0} ocorrência(s) · Página {_pagina + 1:N0}" :
                $"{_total:N0} arquivo(s) · Página {_pagina + 1:N0} de {Math.Max(1, (_total + TamanhoPagina - 1) / TamanhoPagina):N0}" +
                $" · Marcados: {_marcados.Count:N0}" +
                (_foraDoFiltro > 0 ? $" ({_foraDoFiltro:N0} fora dos filtros)" : "") +
                (_mantidos.Count > 0 ? $" · Cópias mantidas no acervo: {_mantidos.Count:N0}" : "");
        if (!string.IsNullOrWhiteSpace(_mensagem) && _lote is null) _estado.Text += " · " + _mensagem;
    }

    private static NumericUpDown Tamanho() => new() { Minimum = 0, Maximum = 10000000,
        DecimalPlaces = 2, Width = 75 };
    private static DateTimePicker DataOpcional() => new() { ShowCheckBox = true,
        Checked = false, Format = DateTimePickerFormat.Short, Width = 126 };
    private static ComboBox Lista(params string[] opcoes)
    {
        var lista = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 153 };
        lista.Items.AddRange(opcoes);
        if (opcoes.Length > 0) lista.SelectedIndex = 0;
        return lista;
    }

    internal static Button Botao(string texto) => new() { Text = texto, AutoSize = true };
    internal static FlowLayoutPanel Faixa(params Control[] controles)
    {
        var faixa = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
            WrapContents = true, Margin = new Padding(0, 0, 0, 4)
        };
        faixa.Controls.AddRange(controles);
        return faixa;
    }
    private static Control Campo(string titulo, Control controle)
        => Campo(new Label { Text = titulo, AutoSize = true, Margin = new Padding(2, 0, 0, 1) }, controle);

    private static Control Campo(Control rotulo, Control controle)
    {
        var painel = new FlowLayoutPanel { AutoSize = true, WrapContents = false,
            FlowDirection = FlowDirection.TopDown, Margin = new Padding(3, 2, 7, 2) };
        painel.Controls.Add(rotulo);
        painel.Controls.Add(controle);
        return painel;
    }
}

internal sealed class BotaoLote : Button
{
    protected override void OnPaint(PaintEventArgs e)
    {
        if (Enabled)
        {
            base.OnPaint(e);
            return;
        }

        // O desenho padrão do Windows substitui a cor do texto de botões desabilitados.
        using var fundo = new SolidBrush(BackColor);
        e.Graphics.FillRectangle(fundo, ClientRectangle);
        TextRenderer.DrawText(e.Graphics, Text, Font, ClientRectangle, ForeColor,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
            TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
    }
}
