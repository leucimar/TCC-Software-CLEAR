using System.Diagnostics;
using Clear.Application.Progresso;
using Clear.Infrastructure.Quarentena;

namespace Clear.Desktop;

/// <summary>
/// Restauração assistida. Seleção explícita por ID e filtros no banco
/// de auditoria; jamais restaura itens apenas por coincidirem com um filtro.
/// </summary>
public sealed class FormRestauracaoLoteM11 : Form
{
    private readonly string _idExecucao;
    private readonly string _banco;
    private readonly TextBox _extensao = new()
        { PlaceholderText = ".pdf", Width = 85 };
    private readonly TextBox _caminho = new()
        { PlaceholderText = "Pasta / nome", Width = 250 };
    private readonly NumericUpDown _minMiB = new()
        { Maximum = 10000000, DecimalPlaces = 2, Width = 85 };
    private readonly CheckBox _usarMax = new()
        { Text = "Máximo MiB", AutoSize = true };
    private readonly NumericUpDown _maxMiB = new()
        { Maximum = 10000000, DecimalPlaces = 2, Width = 85 };
    private readonly Button _filtrar = new() { Text = "Aplicar filtros", AutoSize = true };
    private readonly Button _anterior = new() { Text = "Anterior", AutoSize = true };
    private readonly Button _proxima = new() { Text = "Próxima", AutoSize = true };
    private readonly Button _selecionarPagina = new()
        { Text = "Selecionar página", AutoSize = true };
    private readonly Button _selecionarTodos = new()
        { Text = "Selecionar TODOS filtrados (até 10.000)", AutoSize = true };
    private readonly Button _limpar = new()
        { Text = "Limpar seleção", AutoSize = true };
    private readonly Button _restaurar = new()
        { Text = "Revisar e RESTAURAR selecionados", AutoSize = true };
    private readonly Button _cancelar = new()
        { Text = "Cancelar lote", AutoSize = true, Enabled = false };
    private readonly Label _estado = new()
        { Dock = DockStyle.Fill, AutoSize = false };
    private readonly TextBox _erros = new()
        { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true,
          ScrollBars = ScrollBars.Vertical };
    private readonly DataGridView _grade = new()
    {
        Dock = DockStyle.Fill, AllowUserToAddRows = false,
        AllowUserToDeleteRows = false, MultiSelect = false, RowHeadersVisible = false,
        SelectionMode = DataGridViewSelectionMode.FullRowSelect,
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
    };
    private readonly HashSet<string> _ids = new(StringComparer.Ordinal);
    private List<RegistroQuarentenaM7> _paginaAtual = [];
    private FiltroRestauracaoM11 _filtro = new();
    private int _pagina;
    private long _total;
    private bool _carregando;
    private bool _executando;
    private CancellationTokenSource? _cancelamento;

    public FormRestauracaoLoteM11(string idExecucao, string banco)
    {
        _idExecucao = idExecucao;
        _banco = banco;
        Text = "CLEAR — Restaurar arquivos em lote (CONFIRMAÇÃO OBRIGATÓRIA)";
        Size = new Size(1450, 840);
        MinimumSize = new Size(1050, 640);

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill,
            RowCount = 4, ColumnCount = 1, Padding = new Padding(8) };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 104));
        var linhaFiltros = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill,
            AutoScroll = true, WrapContents = true };
        foreach (Control campo in new Control[] {
            new Label { Text = "Extensão:", AutoSize = true }, _extensao,
            new Label { Text = "Caminho contém:", AutoSize = true }, _caminho,
            new Label { Text = "Mínimo MiB:", AutoSize = true }, _minMiB,
            _usarMax, _maxMiB, _filtrar
        }) linhaFiltros.Controls.Add(campo);
        layout.Controls.Add(linhaFiltros, 0, 0);
        var barra = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill };
        foreach (Control botao in new Control[] {
            _anterior, _proxima, _selecionarPagina, _selecionarTodos,
            _limpar, _restaurar, _cancelar
        }) barra.Controls.Add(botao);
        layout.Controls.Add(barra, 0, 1);

        _grade.Columns.Add(new DataGridViewCheckBoxColumn
            { Name = "Marcar", HeaderText = "Restaurar?", Width = 80 });
        _grade.Columns.Add("Original", "Caminho original");
        _grade.Columns.Add("Destino", "Caminho na quarentena");
        _grade.Columns.Add("Tamanho", "Bytes");
        _grade.Columns.Add("Id", "ID da operação");
        _grade.Columns["Id"]!.Visible = false;
        _grade.Columns["Marcar"]!.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
        _grade.Columns["Marcar"]!.Width = 85;
        _grade.Columns["Original"]!.FillWeight = 60;
        _grade.Columns["Destino"]!.FillWeight = 35;
        _grade.Columns["Tamanho"]!.FillWeight = 12;
        for (var i = 1; i < _grade.Columns.Count; i++)
            _grade.Columns[i].ReadOnly = true;
        layout.Controls.Add(_grade, 0, 2);
        var rodape = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2 };
        rodape.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        rodape.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        rodape.Controls.Add(_estado, 0, 0);
        rodape.Controls.Add(_erros, 0, 1);
        layout.Controls.Add(rodape, 0, 3);
        Controls.Add(layout);

        _grade.CurrentCellDirtyStateChanged += (_, _) =>
        {
            if (_grade.IsCurrentCellDirty)
                _grade.CommitEdit(DataGridViewDataErrorContexts.Commit);
        };
        _grade.CellValueChanged += (_, e) =>
        {
            if (_carregando || _executando || e.RowIndex < 0 || e.ColumnIndex != 0)
                return;
            if (_grade.Rows[e.RowIndex].Cells["Id"].Value is not string id)
                return;
            if (_grade.Rows[e.RowIndex].Cells["Marcar"].Value is true)
                _ids.Add(id);
            else _ids.Remove(id);
            ExibirEstado();
        };
        _filtrar.Click += async (_, _) =>
        {
            try
            {
                _filtro = LerFiltro();
                await CarregarAsync(0);
            }
            catch (Exception ex) { _estado.Text = "Filtros inválidos: " + ex.Message; }
        };
        _anterior.Click += async (_, _) => await CarregarAsync(_pagina - 1);
        _proxima.Click += async (_, _) => await CarregarAsync(_pagina + 1);
        _selecionarPagina.Click += (_, _) =>
        {
            if (_ids.Concat(_paginaAtual.Select(x => x.IdOperacao))
                    .Distinct(StringComparer.Ordinal).Take(10001).Count() > 10000)
            {
                MessageBox.Show(this, "O lote não pode exceder 10.000 arquivos.");
                return;
            }
            foreach (var item in _paginaAtual) _ids.Add(item.IdOperacao);
            AtualizarTabela();
            ExibirEstado();
        };
        _selecionarTodos.Click += async (_, _) =>
        {
            if (_executando) return;
            var resposta = MessageBox.Show(this,
                $"Selecionar TODOS os {_total:N0} registros atualmente FILTRADOS " +
                "com estado EmQuarentena? (Máximo 10.000.) " +
                "ISSO NÃO RESTAURA NENHUM ARQUIVO. A restauração exigirá " +
                "outra confirmação.",
                "CLEAR — Seleção explícita", MessageBoxButtons.YesNo,
                MessageBoxIcon.Question, MessageBoxDefaultButton.Button2);
            if (resposta != DialogResult.Yes) return;
            try
            {
                var encontrados = await ConsultaQuarentenaFiltradaM11
                    .SelecionarTodosFiltradosAsync(_banco, _idExecucao, _filtro);
                if (_ids.Concat(encontrados).Distinct(StringComparer.Ordinal)
                        .Take(10001).Count() > 10000)
                    throw new InvalidOperationException(
                        "Seleção acumulada excede 10.000 arquivos. Limpe a seleção ou refine filtros.");
                foreach (var id in encontrados) _ids.Add(id);
                AtualizarTabela();
                ExibirEstado();
            }
            catch (Exception ex) { _estado.Text = "Falha ao selecionar: " + ex.Message; }
        };
        _limpar.Click += (_, _) =>
        {
            _ids.Clear();
            AtualizarTabela();
            ExibirEstado();
        };
        _restaurar.Click += async (_, _) => await RestaurarSelecionadosAsync();
        _cancelar.Click += (_, _) => _cancelamento?.Cancel();
        TemaVisual.Registrar(this);
        Shown += async (_, _) => await CarregarAsync(0);
        FormClosing += (_, e) =>
        {
            if (!_executando) return;
            _cancelamento?.Cancel();
            e.Cancel = true;
            _estado.Text = "Aguarde a conclusão da operação auditada atual.";
        };
    }

    private FiltroRestauracaoM11 LerFiltro()
    {
        var ext = _extensao.Text.Trim();
        if (ext.Length > 0 && !ext.StartsWith('.')) ext = "." + ext;
        return new FiltroRestauracaoM11(
            Extensao: ext, TrechoCaminho: _caminho.Text.Trim(),
            MinimoBytes: _minMiB.Value == 0 ? null :
                (long)(_minMiB.Value * 1024M * 1024M),
            MaximoBytes: _usarMax.Checked ?
                (long)(_maxMiB.Value * 1024M * 1024M) : null);
    }

    private async Task CarregarAsync(int pagina)
    {
        if (_executando || _carregando || pagina < 0) return;
        _carregando = true;
        _filtrar.Enabled = false;
        try
        {
            var resultado = await ConsultaQuarentenaFiltradaM11.ConsultarAsync(
                _banco, _idExecucao, _filtro, pagina);
            if (pagina > 0 && resultado.Registros.Count == 0)
            {
                // Após restauro, a última página pode deixar de existir.
                // Retornar à última página válida, sem mostrar seleção obsoleta.
                pagina = (int)Math.Max(0, (resultado.Total - 1) /
                    ConsultaQuarentenaFiltradaM11.TamanhoPagina);
                resultado = await ConsultaQuarentenaFiltradaM11.ConsultarAsync(
                    _banco, _idExecucao, _filtro, pagina);
            }
            _pagina = pagina;
            _total = resultado.Total;
            _paginaAtual = resultado.Registros.ToList();
            AtualizarTabela();
            _anterior.Enabled = pagina > 0;
            _proxima.Enabled = (long)(pagina + 1) *
                ConsultaQuarentenaFiltradaM11.TamanhoPagina < _total;
            ExibirEstado();
        }
        catch (Exception ex) { _estado.Text = "Consulta falhou: " + ex.Message; }
        finally { _carregando = false; _filtrar.Enabled = true; }
    }

    private void AtualizarTabela()
    {
        _carregando = true;
        try
        {
            _grade.Rows.Clear();
            foreach (var item in _paginaAtual)
                _grade.Rows.Add(_ids.Contains(item.IdOperacao), item.CaminhoOriginal,
                    item.CaminhoDestino, item.TamanhoBytes, item.IdOperacao);
        }
        finally { _carregando = false; }
    }

    private void ExibirEstado() =>
        _estado.Text = $"Elegíveis para restauração: {_total:N0} | Página {_pagina + 1}" +
            $" | Marcados: {_ids.Count:N0}. " +
            "Itens restaurados somem da lista; o registro permanece na auditoria.";

    private void HabilitarExecucao(bool ativo)
    {
        _executando = ativo;
        foreach (Control controle in new Control[] { _filtrar, _anterior, _proxima,
            _selecionarPagina, _selecionarTodos, _limpar, _restaurar,
            _extensao, _caminho, _minMiB, _maxMiB, _usarMax })
            controle.Enabled = !ativo;
        _cancelar.Enabled = ativo;
        _grade.Enabled = !ativo;
    }

    private async Task RestaurarSelecionadosAsync()
    {
        if (_ids.Count == 0 || _executando) return;
        IReadOnlyList<RegistroQuarentenaM7> selecionados;
        try
        {
            selecionados = await ConsultaQuarentenaFiltradaM11.ConsultarSelecionadosAsync(
                _banco, _idExecucao, _ids);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "Seleção mudou desde a consulta: " + ex.Message,
                "CLEAR", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var bytes = selecionados.Aggregate(0L,
            (acc, x) => checked(acc + x.TamanhoBytes));
        var resumo = string.Join(Environment.NewLine,
            selecionados.Select(x => "  • " + x.CaminhoOriginal));
        var resposta = DialogoRevisao.Confirmar(this,
            $"RESTAURAR {selecionados.Count:N0} ARQUIVOS EXPLICITAMENTE MARCADOS?" +
            Environment.NewLine + $"Bytes: {bytes:N0} | Execução: {_idExecucao}" +
            Environment.NewLine + "Todos os caminhos originais selecionados:" +
            Environment.NewLine + resumo +

            Environment.NewLine + Environment.NewLine +
            "Cada arquivo será verificado com SHA-256, seu destino original " +
            "deverá estar livre e nenhum arquivo será sobrescrito. " +
            "Após restauração e auditoria confirmadas, pastas VAZIAS da " +
            "quarentena poderão ser removidas. Sucessos anteriores não " +
            "serão revertidos se outro arquivo falhar.",
            "CLEAR — CONFIRMAÇÃO HUMANA DE RESTAURAÇÃO EM LOTE",
            MessageBoxButtons.YesNo, MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2);
        if (resposta != DialogResult.Yes) return;

        HabilitarExecucao(true);
        _cancelamento = new CancellationTokenSource();
        var relogio = Stopwatch.StartNew();
        int concluidos = 0, movidos = 0, erros = 0;
        long processadosBytes = 0;
        _erros.Clear();
        try
        {
            foreach (var reg in selecionados)
            {
                if (_cancelamento.IsCancellationRequested) break;
                try
                {
                    var plano = await ServicoRestauracaoM8.PrepararAsync(
                        reg.IdOperacao, _banco, _cancelamento.Token);
                    await ServicoRestauracaoM8.RestaurarAsync(
                        plano, true, _banco, _cancelamento.Token);
                    movidos++;
                    _ids.Remove(reg.IdOperacao);
                }
                catch (OperationCanceledException) when (_cancelamento.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    erros++;
                    _erros.AppendText($"Operação {reg.IdOperacao}: {ex.Message}" +
                        Environment.NewLine);
                }
                finally
                {
                    concluidos++;
                    processadosBytes += reg.TamanhoBytes;
                    var eta = EstimativaRestante.Calcular(
                        concluidos, selecionados.Count,
                        processadosBytes, bytes, relogio.Elapsed);
                    _estado.Text = $"RESTAURO: {concluidos:N0}/{selecionados.Count:N0}" +
                        $" | concluídos {movidos:N0} | falhas {erros:N0}" +
                        $" | decorrido {relogio.Elapsed.ToString(@"hh\:mm\:ss")}" +
                        $" | {EstimativaRestante.Exibir(eta)}";
                }
            }
        }
        finally
        {
            _cancelamento.Dispose();
            _cancelamento = null;
            HabilitarExecucao(false);
            await CarregarAsync(_pagina);
        }
        MessageBox.Show(this,
            $"Restauração encerrada. Arquivos restaurados: {movidos:N0}; " +
            $"falhas: {erros:N0}. Verifique a auditoria individual." +
            Environment.NewLine + "Nenhum arquivo não marcado foi restaurado.",
            "CLEAR — Restauração em lote",
            MessageBoxButtons.OK, erros > 0 ? MessageBoxIcon.Warning
                                            : MessageBoxIcon.Information);
    }
}
