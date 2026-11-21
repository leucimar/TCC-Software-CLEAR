using Clear.Domain.Compatibilidade;

namespace Clear.Desktop;

public partial class Form1
{
    private void MontarApresentacao()
    {
        Text = "CLEARv2.0";
        MinimumSize = new Size(920, 560);
        Size = new Size(1480, 920);
        var principal = new TableLayoutPanel { Dock = DockStyle.Fill,
            ColumnCount = 1, RowCount = 4, Padding = new Padding(12) };
        principal.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
        principal.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
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

        var origem = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 5, RowCount = 1,
            Margin = new Padding(3, 2, 3, 0) };
        origem.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        origem.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
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
        origem.Controls.Add(Legenda("Pasta a analisar"), 0, 0);
        origem.Controls.Add(_caminho, 1, 0);
        origem.Controls.Add(_selecionar, 2, 0);
        origem.Controls.Add(_iniciar, 3, 0);
        origem.Controls.Add(_cancelar, 4, 0);
        principal.Controls.Add(origem, 0, 1);
        ConfigurarControles();
        var resultados = MontarAbasDeResultados();
        principal.Controls.Add(MontarAreaSuperior(resultados), 0, 2);
        _estado.Text = "Pronto • Análise somente leitura. Nenhuma movimentação automática.";
        _estado.Padding = new Padding(8, 5, 8, 5);
        principal.Controls.Add(_estado, 0, 3);
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
        _filtroRelatorio.Items.AddRange(new object[] { "Todas as entradas", "Somente indícios gerais",
            "Somente incompatibilidades", "Somente tamanho pessoal", "Achados de qualquer tipo" });
        _filtroRelatorio.SelectedIndex = 0;
        _criarSubBase.Text = "Criar amostra por cópia";
        _salvarCheckpoint.Text = "Registrar progresso para reinício seguro";
        _reiniciarInterrompida.Text = "Reiniciar análise interrompida";

        foreach (var definicao in CatalogoVerificacoes.Definicoes)
        {
            var rotulo = definicao.Codigo switch
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
            };
            var opcao = new CheckBox { Text = rotulo, AutoSize = true };
            _dicas.SetToolTip(opcao, $"{definicao.Rotulo} • {definicao.Codigo} • {definicao.FonteDocumental}");
            _checks.Add(definicao.Codigo, opcao);
        }
    }

    private IEnumerable<CheckBox> OpcoesDoAmbiente(AmbienteDestino ambiente) =>
        CatalogoVerificacoes.Definicoes.Where(regra => regra.Ambiente == ambiente)
            .Select(regra => _checks[regra.Codigo]);

    private static TableLayoutPanel Pilha() => new() { Dock = DockStyle.Top,
        AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, ColumnCount = 1,
        Padding = new Padding(8), ColumnStyles = { new ColumnStyle(SizeType.Percent, 100) } };

    private static void Aba(TabControl abas, string titulo, Control conteudo)
    {
        var aba = new TabPage(titulo) { Padding = new Padding(8) };
        aba.Controls.Add(conteudo);
        abas.TabPages.Add(aba);
    }
}
