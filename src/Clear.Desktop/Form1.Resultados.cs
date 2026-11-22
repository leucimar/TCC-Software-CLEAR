using System.Text.Json;
using Clear.Application.Metricas;
using Clear.Domain.Arquivos;
using Clear.Domain.Compatibilidade;
using Clear.Infrastructure.Historico;
using Clear.Infrastructure.Metricas;
using Clear.Infrastructure.Quarentena;

namespace Clear.Desktop;

public partial class Form1
{
    private sealed record AnaliseNaLista(ResumoExecucaoHistoricaM6 Dados)
    {
        public override string ToString()
        {
            var pasta = Path.GetFileName(Path.TrimEndingDirectorySeparator(Dados.DiretorioRaiz));
            return $"#{Dados.Sequencia:000} · {Dados.InicioUtc.ToLocalTime():dd/MM/yyyy HH:mm} · " +
                (string.IsNullOrEmpty(pasta) ? Dados.DiretorioRaiz : pasta);
        }
    }

    private readonly ComboBox _analises = new()
    {
        Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList,
        AccessibleName = "Análise exibida"
    };
    private readonly Button _alternarConfiguracoes = PainelInventario.Botao("▴  Ocultar configurações");
    private readonly Button _atualizarAnalises = PainelInventario.Botao("Atualizar");
    private readonly Panel _configuracaoRegistrada = new() { AutoScroll = true, Dock = DockStyle.Fill };
    private readonly Panel _desempenho = new() { AutoScroll = true, Dock = DockStyle.Fill };
    private readonly DataGridView _auditoriaGrade = new()
    {
        Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false,
        AllowUserToDeleteRows = false, RowHeadersVisible = false, AutoGenerateColumns = false,
        AllowUserToResizeColumns = true, SelectionMode = DataGridViewSelectionMode.FullRowSelect
    };
    private readonly Label _auditoriaEstado = new() { AutoSize = true, Margin = new Padding(8, 9, 3, 3) };
    private readonly Button _auditoriaAnterior = PainelInventario.Botao("Anterior");
    private readonly Button _auditoriaProxima = PainelInventario.Botao("Próxima");
    private readonly Button _auditoriaExportar = PainelInventario.Botao("Exportar auditoria CSV");
    private readonly Button _auditoriaLimpar = PainelInventario.Botao("Limpar pastas vazias");
    private readonly ComboBox _auditoriaFiltro = new()
    {
        DropDownStyle = ComboBoxStyle.DropDownList, AccessibleName = "Filtrar auditoria por estado",
        Width = 175
    };
    private PainelInventario _inventario = null!;
    private TabControl _abasResultados = null!;
    private bool _carregandoAnalises;
    private bool _auditando;
    private int _paginaAuditada;
    private IReadOnlyList<ResumoExecucaoHistoricaM6> _analisesSalvas = [];
    private ResumoExecucaoHistoricaM6? AnaliseExibida => (_analises.SelectedItem as AnaliseNaLista)?.Dados;
    private static string BancoHistorico => SessaoHistoricoSqliteM6.CaminhoPadrao;
    private static string BancoAuditoria => ServicoQuarentenaM7.BancoAuditoriaPadrao;
    private string? EstadoFiltradoAuditoria => _auditoriaFiltro.SelectedIndex switch
    {
        1 => "EmQuarentena",
        2 => "Restaurado",
        3 => ServicoQuarentenaM7.EstadoExcluidoPeloUsuario,
        _ => null
    };

    private Control MontarSeletorDeAnalise()
    {
        var linha = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3,
            RowCount = 1, AutoSize = true, Margin = new Padding(2, 6, 2, 3) };
        linha.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        linha.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        linha.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        linha.Controls.Add(Legenda("Análise exibida"), 0, 0);
        _analises.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        linha.Controls.Add(_analises, 1, 0);
        linha.Controls.Add(_atualizarAnalises, 2, 0);
        _atualizarAnalises.Click += async (_, _) => await AtualizarAnalisesAsync();
        _analises.SelectedIndexChanged += async (_, _) =>
        {
            if (_carregandoAnalises) return;
            await MostrarAnaliseAsync();
        };
        return linha;
    }

    private AbasTematicas MontarAbasDeResultados()
    {
        var abas = new AbasTematicas { Dock = DockStyle.Fill };
        _abasResultados = abas;
        _inventario = new PainelInventario(BancoHistorico, BancoAuditoria, () => _pastaQuarentena.Text.Trim());
        _inventario.OcupacaoAlterada += (_, _) => AtualizarAcoesDaTela();
        _inventario.AuditoriaAlterada += async (_, _) => await CarregarAuditoriaAsync(0);
        Aba(abas, "Inventário", _inventario);
        Aba(abas, "Configurações da análise", _configuracaoRegistrada);
        Aba(abas, "Desempenho", _desempenho);
        Aba(abas, "Auditoria", MontarPainelAuditoria());
        MostrarConfiguracao(null);
        MostrarDesempenho(null);
        return abas;
    }

    private Control MontarPainelAuditoria()
    {
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _auditoriaFiltro.Items.AddRange(["Todos", "Em quarentena", "Restaurado", "Excluído pelo usuário"]);
        _auditoriaFiltro.SelectedIndex = 0;
        _auditoriaFiltro.SelectedIndexChanged += async (_, _) => await CarregarAuditoriaAsync(0);
        layout.Controls.Add(PainelInventario.Faixa(
            new Label { Text = "Estado", AutoSize = true, Margin = new Padding(4, 9, 3, 3) },
            _auditoriaFiltro, _auditoriaExportar, _auditoriaLimpar), 0, 0);
        layout.Controls.Add(_auditoriaGrade, 0, 1);
        layout.Controls.Add(PainelInventario.Faixa(_auditoriaAnterior, _auditoriaProxima, _auditoriaEstado), 0, 2);
        foreach (var (titulo, largura) in new (string, int)[]
        {
            ("Registro (horário local)", 221), ("Estado", 178), ("Caminho original", 350),
            ("Caminho na quarentena", 350), ("Bytes", 95), ("Detalhe da auditoria", 335)
        })
            _auditoriaGrade.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = titulo, Width = largura });
        _auditoriaAnterior.Click += async (_, _) => await CarregarAuditoriaAsync(_paginaAuditada - 1);
        _auditoriaProxima.Click += async (_, _) => await CarregarAuditoriaAsync(_paginaAuditada + 1);
        _auditoriaExportar.Click += async (_, _) => await ExportarAuditoriaAsync();
        _auditoriaLimpar.Click += async (_, _) => await LimparPastasAuditadasAsync();
        return layout;
    }

    private async Task AtualizarAnalisesAsync(string? selecionar = null)
    {
        var id = selecionar ?? AnaliseExibida?.Id;
        _carregandoAnalises = true;
        AtualizarAcoesDaTela();
        try
        {
            _analisesSalvas = await Task.Run(() => SessaoHistoricoSqliteM6.ConsultarUltimasAsync(
                BancoHistorico, incluirInterrompidas: true, incluirTodas: true));
            _analises.BeginUpdate();
            try
            {
                _analises.Items.Clear();
                _analises.Items.AddRange(_analisesSalvas.Select(x => new AnaliseNaLista(x)).ToArray());
                var indice = _analisesSalvas.ToList().FindIndex(x => x.Id == id);
                if (_analises.Items.Count > 0) _analises.SelectedIndex = Math.Max(0, indice);
            }
            finally { _analises.EndUpdate(); }
            await CarregarAnaliseExibidaAsync();
        }
        catch (Exception erro) { _estado.Text = "Histórico indisponível. " + erro.Message; }
        finally { _carregandoAnalises = false; AtualizarAcoesDaTela(); }
    }

    private async Task MostrarAnaliseAsync()
    {
        _carregandoAnalises = true;
        AtualizarAcoesDaTela();
        try { await CarregarAnaliseExibidaAsync(); }
        catch (Exception erro) { _estado.Text = "Falha ao abrir a análise. " + erro.Message; }
        finally { _carregandoAnalises = false; AtualizarAcoesDaTela(); }
    }

    private async Task CarregarAnaliseExibidaAsync()
    {
        var execucao = AnaliseExibida;
        MostrarConfiguracao(execucao);
        MostrarDesempenho(null, execucao is not null);
        await _inventario.ExibirAnaliseAsync(execucao);
        await CarregarAuditoriaAsync(0);
        if (execucao is null) return;
        var metricas = await Task.Run(() => RepositorioMetricasAnalise.ConsultarAsync(
            RepositorioMetricasAnalise.CaminhoPadrao));
        var medicao = metricas.FirstOrDefault(m => m.Id == execucao.Id);
        if (medicao is null && execucao.Estado is ("Concluida" or "Parcial"))
        {
            // Métricas do v1.0 não têm o ID do histórico. Só mostrar uma associação inequívoca.
            var legado = metricas.Where(m => CompativelComRegistroLegado(m, execucao)).Take(2).ToArray();
            if (legado.Length == 1 && _analisesSalvas.Count(e => CompativelComRegistroLegado(legado[0], e)) == 1)
                medicao = legado[0];
        }
        MostrarDesempenho(medicao, true);
    }

    private static bool CompativelComRegistroLegado(MetricasAnalise m, ResumoExecucaoHistoricaM6 e) =>
        e.Estado is "Concluida" or "Parcial" &&
        string.Equals(m.PastaRaiz, e.DiretorioRaiz, StringComparison.OrdinalIgnoreCase) &&
        m.Arquivos == e.QuantidadeArquivos && m.Diretorios == e.QuantidadeDiretorios &&
        m.Bytes == e.TotalBytes && m.InicioUtc <= e.InicioUtc &&
        m.FimUtc >= e.FimUtc && m.FimUtc - e.FimUtc <= TimeSpan.FromSeconds(5);

    private void AtualizarAcoesDaTela()
    {
        bool livre = !_carregandoAnalises && _cancelamento is null && !_inventario.Ocupado && !_auditando;
        _analises.Enabled = livre && _analises.Items.Count > 0;
        _atualizarAnalises.Enabled = livre;
        _iniciar.Enabled = livre;
        _selecionar.Enabled = livre;
        if (_cancelamento is not null) _inventario.Enabled = false;
        else if (!_auditando && !_carregandoAnalises) _inventario.Enabled = true;
    }

    private static string EstadoLegivel(string estado) => estado switch
    {
        "Concluida" => "Concluída",
        "Parcial" => "Concluída com ocorrências",
        "Falhou" => "Falhou",
        "Cancelada" => "Cancelada",
        "EmQuarentena" => "Em quarentena",
        "Restaurado" => "Restaurado",
        ServicoQuarentenaM7.EstadoExcluidoPeloUsuario => "Excluído pelo usuário",
        "RestauracaoPendente" => "Restauração pendente",
        _ => estado
    };

    private void MostrarConfiguracao(ResumoExecucaoHistoricaM6? execucao)
    {
        DescartarConteudo(_configuracaoRegistrada);
        if (execucao is null)
        {
            _configuracaoRegistrada.Controls.Add(MensagemResultados("Selecione uma análise para consultar os parâmetros utilizados."));
            TemaVisual.Aplicar(_configuracaoRegistrada);
            return;
        }
        var parametros = LerJson<ParametrosHistoricoM6>(execucao.ParametrosJson);
        var resumo = LerJson<ResumoSalvo>(execucao.ResumoJson);
        var criterios = parametros?.FiltrosGeraisHabilitados?.Select(x => x switch
        {
            "ArquivoVazio" => "Arquivos vazios",
            "CopiaBackupOuTemporario" => "Backups e temporários",
            "SinalTemporal" => "Idade dos arquivos",
            "TamanhoPersonalizado" => "Limite personalizado de tamanho",
            "DuplicatasSHA256" => "Duplicatas exatas",
            _ => x
        }).ToArray();
        var regras = parametros?.VerificacoesHabilitadas?.Select(c =>
            CatalogoVerificacoes.Definicoes.FirstOrDefault(d => d.Codigo == c)?.Rotulo ?? c).ToArray();
        var preservacao = resumo?.PreservacaoAcesso;
        var painel = NovaTabela(50, 50);
        painel.Controls.Add(SecaoResultados("Execução e origem",
            ("Pasta analisada", execucao.DiretorioRaiz),
            ("Início", ApresentacaoResultados.HorarioLocal(execucao.InicioUtc)),
            ("Fim", ApresentacaoResultados.HorarioLocal(execucao.FimUtc)),
            ("Estado", EstadoLegivel(execucao.Estado)),
            ("Inventário", $"{execucao.QuantidadeArquivos:N0} arquivo(s) · {execucao.QuantidadeDiretorios:N0} pasta(s)"),
            ("Grupos de duplicatas", execucao.GruposDuplicatas.ToString("N0")),
            ("Ocorrências", (execucao.TotalOcorrencias + (preservacao?.Ocorrencias.Count ?? 0)).ToString("N0")),
            ("Motivo da interrupção", resumo?.Motivo)), 0, 0);
        painel.Controls.Add(SecaoResultados("Critérios de identificação",
            ("Objetivo", parametros?.AmbienteDestino switch
            {
                "AzureFilesClassicoSmb" => "Pré-saneamento para Azure Files",
                "SharePointOneDriveEmpresarial" => "Pré-saneamento para SharePoint",
                _ => "Pré-saneamento geral"
            }),
            ("Filtros gerais", criterios?.Length > 0 ? string.Join(", ", criterios) : "Nenhum"),
            ("Idade", parametros?.DiasSinalTemporal is int dias
                ? $"{dias:N0} dias · {(parametros.CriterioTemporal?.Contains("Modifica", StringComparison.Ordinal) == true ? "última modificação" : "último acesso")}"
                : "Desativada"),
            ("Tamanho personalizado", parametros?.LimitePersonalizadoBytes is long bytes
                ? $"{bytes:N0} bytes" : "Desativado"),
            ("Regras do objetivo", regras?.Length > 0 ? string.Join(", ", regras) : "Nenhuma")), 1, 0);
        painel.Controls.Add(SecaoResultados("Preservação do último acesso",
            ("Política em caso de falha", parametros?.PoliticaPreservacaoAcesso == "LerComAviso"
                ? "Ler com aviso e registrar a falha" : "Não ler conteúdo sem preservação"),
            ("Aberturas protegidas", preservacao?.AberturasProtegidas.ToString("N0") ?? "Indisponível"),
            ("Leituras ignoradas", preservacao?.LeiturasIgnoradas.ToString("N0") ?? "Indisponível"),
            ("Leituras sem garantia", preservacao?.LeiturasSemGarantia.ToString("N0") ?? "Indisponível")), 0, 1);
        painel.Controls.Add(SecaoResultados("Saídas e execução",
            ("Leitores SHA-256", parametros?.DetectarDuplicatas == false ? "Desativados" :
                parametros?.LeitoresSha256?.ToString("N0") ?? "Indisponível"),
            ("Limite do índice", parametros?.LimiteIndice?.ToString("N0") ?? "Indisponível"),
            ("Histórico local", "Salvo"),
            ("Relatório CSV + JSON", SimOuNao(parametros?.ExportarRelatorio)),
            ("Registro de progresso", SimOuNao(parametros?.RegistrarProgresso))), 1, 1);
        _configuracaoRegistrada.Controls.Add(painel);
        TemaVisual.Aplicar(_configuracaoRegistrada);
    }

    internal sealed record ResumoSalvo(ResumoPreservacaoAcesso? PreservacaoAcesso, string? Motivo);
    internal static T? LerJson<T>(string? valor) where T : class
    {
        try { return valor is null ? null : JsonSerializer.Deserialize<T>(valor); }
        catch (JsonException) { return null; }
    }

    private static string SimOuNao(bool? valor) => valor switch { true => "Sim", false => "Não", _ => "Indisponível" };
    private static Label MensagemResultados(string texto) => new()
        { Text = texto, Dock = DockStyle.Top, Height = 39, Padding = new Padding(7) };

    private static TableLayoutPanel NovaTabela(params float[] colunas)
    {
        var painel = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink, ColumnCount = colunas.Length, Padding = new Padding(4) };
        foreach (var largura in colunas) painel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, largura));
        return painel;
    }

    private static Control SecaoResultados(string titulo, params (string Rotulo, string? Valor)[] linhas)
    {
        var grupo = new GroupBox { Text = titulo, Dock = DockStyle.Fill, AutoSize = true, Padding = new Padding(9) };
        var tabela = NovaTabela(35, 65);
        foreach (var (rotulo, valor) in linhas.Where(x => x.Valor is not null))
        {
            var i = tabela.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            tabela.Controls.Add(new Label { Text = rotulo, AutoSize = true, Dock = DockStyle.Fill,
                Margin = new Padding(4, 3, 4, 3) }, 0, i);
            tabela.Controls.Add(new Label { Text = valor!, AutoSize = true, Dock = DockStyle.Fill,
                Margin = new Padding(4, 3, 4, 3) }, 1, i);
        }
        grupo.Controls.Add(tabela);
        return grupo;
    }

    private static void DescartarConteudo(Control controle)
    {
        while (controle.Controls.Count > 0) controle.Controls[0].Dispose();
    }

    private void MostrarDesempenho(MetricasAnalise? dados, bool temAnalise = false)
    {
        DescartarConteudo(_desempenho);
        if (dados is null)
        {
            _desempenho.Controls.Add(MensagemResultados(temAnalise ?
                "Medições indisponíveis para esta análise." : "Selecione uma análise para ver as medições."));
            TemaVisual.Aplicar(_desempenho);
            return;
        }
        string Tempo(double segundos) => $"{segundos:N3} s";
        var layout = NovaTabela(100);
        var indicadores = NovaTabela(25, 25, 25, 25);
        foreach (var (nome, valor) in new (string, string)[]
        {
            ("Tempo total", Tempo(dados.TotalSegundos)), ("Arquivos inventariados", dados.Arquivos.ToString("N0")),
            ("Volume inventariado", $"{dados.Bytes / 1048576.0:N2} MiB"),
            ("Leitores SHA-256", dados.Leitores > 0 ? dados.Leitores.ToString("N0") : "Desativados")
        })
            indicadores.Controls.Add(MontarSecao(new GroupBox { Text = nome }, new Label
                { Text = valor, AutoSize = true, Font = FonteIndicador }));
        layout.Controls.Add(indicadores, 0, 0);
        var grafico = NovaTabela(24, 63, 13);
        var etapas = new List<(string Nome, double Segundos, string Observacao)>
        {
            ("Inventário", dados.InventarioSegundos,
                $"{dados.ArquivosPorSegundo:N1} arquivos/s · {dados.MiBInventariadosPorSegundo:N2} MiB/s inventariados")
        };
        if (dados.Leitores > 0)
            etapas.Add(("Duplicatas exatas", dados.DuplicatasSegundos, "Enumeração, SHA-256 e comparação de conteúdo"));
        var maximo = Math.Max(0.001, etapas.Max(x => x.Segundos));
        foreach (var etapa in etapas)
        {
            int linha = grafico.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            grafico.Controls.Add(Legenda(etapa.Nome), 0, linha);
            grafico.Controls.Add(new BarraDuracao(etapa.Segundos / maximo)
            {
                Height = 12, Anchor = AnchorStyles.Left | AnchorStyles.Right,
                AccessibleName = $"{etapa.Nome}: {Tempo(etapa.Segundos)}"
            }, 1, linha);
            grafico.Controls.Add(Legenda(Tempo(etapa.Segundos)), 2, linha);
        }
        layout.Controls.Add(MontarSecao(new GroupBox { Text = "Duração por etapa" }, grafico), 0, 1);
        var tabela = new DataGridView
        {
            Dock = DockStyle.Fill, Height = 155, ReadOnly = true, RowHeadersVisible = false,
            AllowUserToAddRows = false, AllowUserToDeleteRows = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, Visible = false
        };
        tabela.Columns.Add("etapa", "Etapa");
        tabela.Columns.Add("tempo", "Tempo");
        tabela.Columns.Add("observacao", "Medição");
        foreach (var etapa in etapas) tabela.Rows.Add(etapa.Nome, Tempo(etapa.Segundos), etapa.Observacao);
        tabela.Rows.Add("Total", Tempo(dados.TotalSegundos), "Inclui as demais fases e as saídas habilitadas");
        var ver = PainelInventario.Botao("▾  Ver tabela de desempenho");
        ver.Click += (_, _) =>
        {
            tabela.Visible = !tabela.Visible;
            ver.Text = tabela.Visible ? "▴  Ocultar tabela de desempenho" : "▾  Ver tabela de desempenho";
        };
        var exportar = PainelInventario.Botao("Exportar métricas CSV");
        exportar.Click += async (_, _) =>
        {
            using var arquivo = new SaveFileDialog { Filter = "CSV|*.csv",
                FileName = $"CLEAR_Metricas_{AnaliseExibida?.Sequencia:000}.csv" };
            if (arquivo.ShowDialog(this) != DialogResult.OK) return;
            try { await RepositorioMetricasAnalise.ExportarCsvAsync(arquivo.FileName, [dados]); }
            catch (Exception erro) { MessageBox.Show(this, erro.Message, "CLEAR", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        };
        layout.Controls.Add(PainelInventario.Faixa(ver, exportar), 0, 2);
        layout.Controls.Add(tabela, 0, 3);
        if (dados.Parcial) layout.Controls.Add(MensagemResultados("Medições de uma execução parcial."), 0, 4);
        _desempenho.Controls.Add(layout);
        TemaVisual.Aplicar(_desempenho);
    }

    private sealed class BarraDuracao(double proporcao) : Control
    {
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var cores = TemaVisual.Cores;
            using var trilho = new SolidBrush(cores.CabecalhoGrade);
            using var valor = new SolidBrush(cores.Destaque);
            e.Graphics.FillRectangle(trilho, ClientRectangle);
            var largura = (float)(ClientSize.Width * Math.Clamp(proporcao, 0, 1));
            e.Graphics.FillRectangle(valor, 0, 0, largura, ClientSize.Height);
        }
    }

    private async Task CarregarAuditoriaAsync(int pagina)
    {
        if (_auditando) return;
        _auditando = true;
        AtualizarAcoesDaTela();
        _auditoriaAnterior.Enabled = _auditoriaProxima.Enabled = _auditoriaExportar.Enabled = _auditoriaLimpar.Enabled = false;
        _auditoriaFiltro.Enabled = false;
        _auditoriaGrade.Rows.Clear();
        try
        {
            if (AnaliseExibida is not { } execucao) { _auditoriaEstado.Text = "Selecione uma análise."; return; }
            _paginaAuditada = Math.Max(0, pagina);
            var estado = EstadoFiltradoAuditoria;
            var (registros, total) = await Task.Run(async () =>
                (await ServicoQuarentenaM7.ConsultarAsync(BancoAuditoria, execucao.Id, pagina: _paginaAuditada, estado: estado),
                 await ServicoQuarentenaM7.ContarAuditoriaAsync(BancoAuditoria, execucao.Id, estado: estado)));
            foreach (var r in registros)
                _auditoriaGrade.Rows.Add(ApresentacaoResultados.HorarioLocal(r.CriadoUtc), EstadoLegivel(r.Estado),
                    r.CaminhoOriginal, r.CaminhoDestino, r.TamanhoBytes, r.Mensagem ?? "");
            _auditoriaEstado.Text = $"{total:N0} registro(s) · Página {_paginaAuditada + 1:N0}";
            _auditoriaAnterior.Enabled = _paginaAuditada > 0;
            _auditoriaProxima.Enabled = (_paginaAuditada + 1L) * 200 < total;
            _auditoriaExportar.Enabled = total > 0;
            _auditoriaLimpar.Enabled = total > 0;
        }
        catch (Exception erro) { _auditoriaEstado.Text = "Auditoria indisponível. " + erro.Message; }
        finally
        {
            _auditando = false;
            _auditoriaFiltro.Enabled = AnaliseExibida is not null;
            AtualizarAcoesDaTela();
        }
    }

    private static readonly Font FonteIndicador = new("Segoe UI", 13, FontStyle.Bold);

    private async Task ExportarAuditoriaAsync()
    {
        if (AnaliseExibida is not { } execucao) return;
        using var arquivo = new SaveFileDialog { Filter = "CSV|*.csv",
            FileName = $"CLEAR_Auditoria_{execucao.Sequencia:000}.csv" };
        if (arquivo.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            var total = await ServicoQuarentenaM7.ExportarAuditoriaCsvAsync(
                BancoAuditoria, execucao.Id, arquivo.FileName, estado: EstadoFiltradoAuditoria);
            _auditoriaEstado.Text = $"{total:N0} registros exportados.";
        }
        catch (Exception erro) { MessageBox.Show(this, erro.Message, "CLEAR", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
    }

    private async Task LimparPastasAuditadasAsync()
    {
        var raiz = _pastaQuarentena.Text;
        if (MessageBox.Show(this, "Remover as pastas vazias auditadas em " + raiz + "?",
                "CLEAR", MessageBoxButtons.YesNo, MessageBoxIcon.Question,
                MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
        _auditoriaLimpar.Enabled = false;
        try
        {
            var total = await LimpadorPastasVaziasM11.LimparAsync(BancoAuditoria, raiz, true);
            _auditoriaEstado.Text = $"{total:N0} pastas vazias removidas.";
        }
        catch (Exception erro) { MessageBox.Show(this, erro.Message, "CLEAR", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        finally { _auditoriaLimpar.Enabled = true; }
    }
}
