using Clear.Domain.Compatibilidade;

namespace Clear.Desktop;

public partial class Form1
{
    private static bool LayoutSuperior =>
        !AppContext.TryGetSwitch("Clear.LayoutSuperior", out var superior) || superior;

    private void MontarApresentacao()
    {
        Text = "CLEARv1.0";
        MinimumSize = new Size(920, 560);
        Size = new Size(1480, 920);
        var principal = new TableLayoutPanel { Dock = DockStyle.Fill,
            ColumnCount = 1, RowCount = 5, Padding = new Padding(16) };
        principal.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
        principal.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        principal.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        principal.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        principal.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        var cabecalho = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2,
            Padding = new Padding(16, 8, 16, 8), Tag = "header" };
        cabecalho.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        cabecalho.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190));
        cabecalho.Controls.Add(new Label { Text = Text,
            Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Segoe UI", 17, FontStyle.Bold), Tag = "header" }, 0, 0);
        var tema = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList,
            Dock = DockStyle.Fill, AccessibleName = "Tema da interface", Margin = new Padding(8) };
        tema.Items.AddRange(new object[] { "Tema claro", "Tema escuro" });
        tema.SelectedIndex = TemaVisual.Escuro ? 1 : 0;
        tema.SelectedIndexChanged += (_, _) =>
        {
            var erro = TemaVisual.Alterar(tema.SelectedIndex == 1);
            if (erro is not null) _estado.Text = erro;
        };
        cabecalho.Controls.Add(tema, 1, 0);
        principal.Controls.Add(cabecalho, 0, 0);

        var origem = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 1,
            Margin = new Padding(3, 2, 3, 0) };
        origem.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        origem.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        origem.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        origem.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        origem.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _caminho.AccessibleName = "Pasta de origem da análise";
        _caminho.Dock = DockStyle.None;
        _caminho.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        _selecionar.AutoSize = _iniciar.AutoSize = _cancelar.AutoSize = true;
        _iniciar.Text = "Iniciar análise";
        _iniciar.Tag = "primary";
        origem.Controls.Add(_caminho, 0, 0);
        origem.Controls.Add(_selecionar, 1, 0);
        origem.Controls.Add(_iniciar, 2, 0);
        origem.Controls.Add(_cancelar, 3, 0);
        principal.Controls.Add(origem, 0, 1);
        _resumo.Text = "Selecione uma pasta e configure sua análise. A preservação do último acesso é solicitada antes das leituras de conteúdo.";
        _resumo.Padding = new Padding(4);
        _resumo.AutoSize = true;
        _resumo.MinimumSize = new Size(0, 24);
        principal.ClientSizeChanged += (_, _) =>
            _resumo.MaximumSize = new Size(Math.Max(100, principal.ClientSize.Width - 40), 0);
        principal.Controls.Add(_resumo, 0, 2);

        ConfigurarControles();
        var resultados = MontarAbasDeResultados();
        principal.Controls.Add(LayoutSuperior ? MontarAreaSuperior(resultados) : MontarAreaLateral(resultados), 0, 3);
        _estado.Text = "Pronto • Análise somente leitura. Nenhuma movimentação automática.";
        _estado.Padding = new Padding(8, 5, 8, 5);
        principal.Controls.Add(_estado, 0, 4);
        Controls.Add(principal);
    }

    private void ConfigurarControles()
    {
        _semDestino.Text = "Pré-saneamento geral";
        _azure.Text = "Pré-saneamento para Azure Files";
        _sharePoint.Text = "Pré-saneamento para SharePoint";
        _subtipo.Items.AddRange(new object[] { "SharePoint Online", "OneDrive for Business" });
        _subtipo.SelectedIndex = 0;
        _identificarTemporarios.Text = "Nomes de backups e temporários";
        _ativarSinalTemporal.Text = "Data registrada há mais de:";
        _criterioTemporal.Items.AddRange(new object[] { "Último acesso", "Última modificação" });
        _criterioTemporal.SelectedIndex = 0;
        _politicaPreservacao.Items.AddRange(new object[] { "Não ler conteúdo sem preservação", "Ler mesmo assim, com aviso e registro" });
        _politicaPreservacao.SelectedIndex = 0;
        _tentarAdministrador.Click += (_, _) => TentarAdministrador();
        _ativarTamanho.Text = "Arquivos maiores que:";
        _ativarDuplicatas.Text = "Duplicatas exatas • SHA-256 e conteúdo";
        _unidade.Items.AddRange(new object[] { "B", "KiB", "MiB", "GiB", "TiB" });
        _unidade.SelectedIndex = 3;
        _exportarRelatorio.Text = "Exportar relatório CSV + JSON";
        _salvarHistorico.Text = "Salvar análise no histórico local";
        _filtroRelatorio.Items.AddRange(new object[] { "Todas as entradas", "Somente indícios gerais",
            "Somente incompatibilidades", "Somente tamanho pessoal", "Achados de qualquer tipo" });
        _filtroRelatorio.SelectedIndex = 0;
        _criarSubBase.Text = "Criar amostra por cópia";
        _salvarCheckpoint.Text = "Registrar progresso para reinício seguro";
        _reiniciarInterrompida.Text = "Reiniciar análise interrompida";

        foreach (var definicao in CatalogoVerificacoes.Definicoes)
        {
            var rotulo = LayoutSuperior ? definicao.Codigo switch
            {
                "AZF-NOME-001" or "SPO-NOME-001" => "Sinais proibidos",
                "AZF-NOME-002" => "Nomes reservados",
                "AZF-TAM-001" => "Limite de 4 TiB",
                "AZF-CAM-001" => "Caminho (2.048)",
                "AZF-CAM-002" => "Item: 255 car.",
                "SPO-NOME-005" => "Nomes reservados",
                "SPO-NOME-006" => "Forms na raiz",
                "SPO-CAM-001" => "Caminho: 400 car.",
                "SPO-TAM-001" => "Limite de 250 GiB",
                _ => definicao.Rotulo
            } : definicao.Rotulo;
            var opcao = new CheckBox { Text = rotulo, AutoSize = true };
            _dicas.SetToolTip(opcao, $"{definicao.Rotulo} • {definicao.Codigo} • {definicao.FonteDocumental}");
            _checks.Add(definicao.Codigo, opcao);
        }
    }

    private IEnumerable<CheckBox> OpcoesDoAmbiente(AmbienteDestino ambiente) =>
        CatalogoVerificacoes.Definicoes.Where(regra => regra.Ambiente == ambiente)
            .Select(regra => _checks[regra.Codigo]);

    private TableLayoutPanel MontarAreaLateral(Control resultados)
    {
        var conteudo = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
        conteudo.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 360));
        conteudo.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        var configuracoes = new AbasTematicas { Dock = DockStyle.Fill };
        var analise = AdicionarConfiguracao(configuracoes, "Análise");
        var saidas = AdicionarConfiguracao(configuracoes, "Saídas");
        var avancado = AdicionarConfiguracao(configuracoes, "Avançado");
        Titulo(analise, "Objetivo Esperado:");
        var radios = new Panel { Height = 104, Dock = DockStyle.Fill };
        var radioPilha = Pilha();
        radioPilha.Padding = Padding.Empty;
        foreach (var radio in new[] { _semDestino, _azure, _sharePoint })
        { radio.Height = 27; radio.AutoSize = false; Adicionar(radioPilha, radio); }
        radios.Controls.Add(radioPilha);
        radios.SizeChanged += (_, _) => AjustarLargura(radioPilha, radios, false);
        AjustarLargura(radioPilha, radios, false);
        Adicionar(analise, radios);
        Adicionar(analise, _subtipo);
        Titulo(analise, "O que identificar");
        foreach (var check in new[] { _identificarVazios, _identificarTemporarios,
            _ativarDuplicatas, _ativarSinalTemporal }) AdicionarOpcao(analise, check);
        Adicionar(analise, _criterioTemporal);
        Adicionar(analise, Linha(_dias, new Label { Text = "dias", AutoSize = true }));
        AdicionarOpcao(analise, _ativarTamanho);
        Adicionar(analise, Linha(_limite, _unidade));
        Adicionar(analise, new Label { Text = "O último acesso depende dos metadados do Windows.",
            Height = 44, Dock = DockStyle.Fill });

        Titulo(analise, "Verificações do destino");
        Adicionar(analise, new Label { Text = "SharePoint / OneDrive: biblioteca (caminho decodificado, sem domínio)", AutoSize = true });
        Adicionar(analise, _caminhoBiblioteca);
        Adicionar(analise, new Label { Text = "Subpasta no destino (SharePoint ou Azure Files)", AutoSize = true });
        Adicionar(analise, _subpastaDestino);
        Adicionar(analise, MontarGrupoDeRegras(_grupoAzure, AmbienteDestino.AzureFilesClassicoSmb));
        Adicionar(analise, MontarGrupoDeRegras(_grupoSharePoint, AmbienteDestino.SharePointOneDriveEmpresarial));

        Titulo(saidas, "Relatórios e histórico");
        AdicionarOpcao(saidas, _exportarRelatorio);
        Adicionar(saidas, _filtroRelatorio);
        Adicionar(saidas, _pastaRelatorios);
        Adicionar(saidas, _escolherRelatorios);
        AdicionarOpcao(saidas, _salvarHistorico);
        Titulo(saidas, "Quarentena");
        Adicionar(saidas, new Label { Text = "As movimentações exigem seleção e confirmação no histórico.",
            Height = 55, Dock = DockStyle.Fill });
        Adicionar(saidas, _pastaQuarentena);
        Adicionar(saidas, _escolherQuarentena);
        Titulo(saidas, "Base experimental");
        Adicionar(saidas, _criarSubBase);

        Titulo(avancado, "Preservação do último acesso");
        Adicionar(avancado, new Label { Text = "Sempre solicitada, qualquer que seja a data analisada. Se o Windows não permitir:",
            Height = 64, Dock = DockStyle.Fill });
        Adicionar(avancado, _politicaPreservacao);
        Adicionar(avancado, _tentarAdministrador);
        Adicionar(avancado, new Label { Text = "Administrador pode ajudar em arquivos locais; não garante permissão em servidores. Outros processos podem alterar datas. Falhas ficam registradas.",
            Height = 110, Dock = DockStyle.Fill });
        Titulo(avancado, "Desempenho");
        Rotulo(avancado, "Leituras SHA-256 simultâneas");
        Adicionar(avancado, _maximoHashes);
        Rotulo(avancado, "Limite de arquivos indexados na memória");
        Adicionar(avancado, _limiteIndexacao);
        Titulo(avancado, "Recuperação da execução");
        AdicionarOpcao(avancado, _salvarCheckpoint);
        Adicionar(avancado, _reiniciarInterrompida);
        Adicionar(avancado, new Label { Text = "O reinício revalida todos os arquivos. Não reaproveita classificações antigas.",
            Height = 66, Dock = DockStyle.Fill });
        conteudo.Controls.Add(configuracoes, 0, 0);
        conteudo.Controls.Add(resultados, 1, 0);
        return conteudo;
    }

    private AbasTematicas MontarAbasDeResultados()
    {
        var resultados = new AbasTematicas { Dock = DockStyle.Fill };
        Aba(resultados, "Inventário", _grade);
        var duplicatas = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2 };
        duplicatas.RowStyles.Add(new RowStyle(SizeType.Absolute, 90));
        duplicatas.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        duplicatas.Controls.Add(_resumoDuplicatas, 0, 0);
        duplicatas.Controls.Add(_gradeDuplicatas, 0, 1);
        Aba(resultados, "Duplicatas", duplicatas);
        var historico = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3 };
        historico.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        historico.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        historico.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        historico.Controls.Add(Linha(_atualizarHistorico, _consultarDetalhesHistorico), 0, 0);
        historico.Controls.Add(_gradeHistorico, 0, 1);
        historico.Controls.Add(_infoHistorico, 0, 2);
        Aba(resultados, "Histórico", historico);
        Aba(resultados, "Desempenho", _metricasM9);
        Aba(resultados, "Ocorrências", _ocorrencias);
        return resultados;
    }

    private static TableLayoutPanel Pilha() => new() { Dock = DockStyle.Top,
        AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, ColumnCount = 1,
        Padding = new Padding(8), ColumnStyles = { new ColumnStyle(SizeType.Percent, 100) } };

    private static TableLayoutPanel AdicionarConfiguracao(TabControl abas, string titulo)
    {
        var pilha = Pilha();
        var rolagem = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
        rolagem.Controls.Add(pilha);
        rolagem.ClientSizeChanged += (_, _) => AjustarLargura(pilha, rolagem);
        AjustarLargura(pilha, rolagem);
        Aba(abas, titulo, rolagem);
        return pilha;
    }

    private static void AjustarLargura(TableLayoutPanel pilha, Control pai, bool rolagem = true)
    {
        var largura = Math.Max(100, pai.ClientSize.Width - (rolagem ? SystemInformation.VerticalScrollBarWidth : 0));
        if (pilha.MinimumSize.Width == largura) return;
        pilha.MaximumSize = new Size(largura, 0);
        pilha.MinimumSize = new Size(largura, 0);
        pilha.Width = largura;
    }

    private static void Adicionar(TableLayoutPanel painel, Control controle)
    {
        if (controle is Label rotulo && rotulo.Font.Size <= 10)
        {
            rotulo.AutoSize = true;
            rotulo.MinimumSize = new Size(0, controle.Height);
        }
        controle.Dock = DockStyle.Fill;
        controle.Margin = new Padding(4, 3, 4, 3);
        painel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        painel.Controls.Add(controle, 0, painel.RowStyles.Count - 1);
    }

    private static void AdicionarOpcao(TableLayoutPanel painel, CheckBox opcao)
    { opcao.AutoSize = false; opcao.Height = 38; Adicionar(painel, opcao); }

    private static void Titulo(TableLayoutPanel painel, string texto) => Adicionar(painel,
        new Label { Text = texto, Height = 30, TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Segoe UI", 11, FontStyle.Bold) });
    private static void Rotulo(TableLayoutPanel painel, string texto) => Adicionar(painel,
        new Label { Text = texto, Height = 40, TextAlign = ContentAlignment.BottomLeft });
    private static FlowLayoutPanel Linha(params Control[] controles)
    {
        var linha = new FlowLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Dock = DockStyle.Fill,
            WrapContents = true, Margin = new Padding(0) };
        linha.Controls.AddRange(controles);
        return linha;
    }
    private static void Aba(TabControl abas, string titulo, Control conteudo)
    {
        var aba = new TabPage(titulo) { Padding = new Padding(8) };
        aba.Controls.Add(conteudo);
        abas.TabPages.Add(aba);
    }
}
