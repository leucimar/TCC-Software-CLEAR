using Clear.Domain.Compatibilidade;

namespace Clear.Desktop;

public partial class Form1
{
    private TableLayoutPanel MontarAreaSuperior(Control resultados)
    {
        var secoes = new TableLayoutPanel
        {
            Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 4, RowCount = 3, Padding = new Padding(0, 0, 0, 2)
        };
        for (int coluna = 0; coluna < secoes.ColumnCount; coluna++)
            secoes.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        secoes.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        secoes.RowStyles.Add(new RowStyle(SizeType.Absolute, 8));
        secoes.RowStyles.Add(new RowStyle(SizeType.Percent, 50));

        _criterioTemporal.DropDownWidth = 165;
        _dias.Width = 54;
        _identificarVazios.Text = "Arquivos vazios";
        _identificarTemporarios.Text = "Backups e temp.";
        _identificarTemporarios.AccessibleName = "Nomes de backups e temporários";
        _ativarSinalTemporal.Text = "Mais de (dias)";
        _ativarSinalTemporal.AccessibleName = "Idade acima do limite em dias";
        _ativarTamanho.Text = "Tamanho acima de";
        _ativarDuplicatas.Text = "Duplicatas exatas";
        _dicas.SetToolTip(_identificarTemporarios, "Identifica nomes de backups e arquivos temporários.");
        _dicas.SetToolTip(_ativarDuplicatas, "Calcula SHA-256 e confirma a igualdade do conteúdo.");
        _grupoAzure.Text = "Regras do Azure Files";
        _grupoSharePoint.Text = "Regras do SharePoint";
        secoes.Controls.Add(MontarSecao(new GroupBox { Text = "Objetivo Esperado:" },
            _semDestino, _azure, _sharePoint), 0, 0);
        secoes.Controls.Add(MontarSecao(new GroupBox { Text = "O que identificar" },
            ColunasCompactas([_identificarVazios, _identificarTemporarios, _ativarDuplicatas],
                [_ativarSinalTemporal, CampoComComplemento(_criterioTemporal, _dias),
                    _ativarTamanho, CampoComComplemento(_limite, _unidade)])), 1, 0);
        secoes.Controls.Add(MontarGrupoDeRegras(_grupoAzure, AmbienteDestino.AzureFilesClassicoSmb), 2, 0);
        secoes.Controls.Add(MontarGrupoDeRegras(_grupoSharePoint, AmbienteDestino.SharePointOneDriveEmpresarial), 3, 0);

        secoes.Controls.Add(MontarSecao(new GroupBox { Text = "Relatórios e histórico" },
            _exportarRelatorio, _filtroRelatorio, CampoComComplemento(_pastaRelatorios, _escolherRelatorios),
            _salvarHistorico), 0, 2);
        secoes.Controls.Add(MontarSecao(new GroupBox { Text = "Quarentena e amostra" },
            CampoComComplemento(_pastaQuarentena, _escolherQuarentena), _criarSubBase,
            Legenda("Confirme movimentações no Histórico.")), 1, 2);
        secoes.Controls.Add(MontarSecao(new GroupBox { Text = "Preservação e desempenho" },
            Legenda("Se a preservação do último acesso falhar"), _politicaPreservacao, _tentarAdministrador,
            MontarCamposDeDesempenho()), 2, 2);
        secoes.Controls.Add(MontarSecao(new GroupBox { Text = "Recuperação da execução" },
            _salvarCheckpoint, _reiniciarInterrompida,
            Legenda("O reinício revalida os arquivos desde o começo.")), 3, 2);

        var rolagem = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Margin = Padding.Empty };
        rolagem.Controls.Add(secoes);
        var area = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
        var alturaOpcoes = new RowStyle(SizeType.Absolute, 310);
        area.RowStyles.Add(alturaOpcoes);
        area.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        area.Controls.Add(rolagem, 0, 0);
        area.Controls.Add(resultados, 0, 1);

        void AjustarAltura(object? sender, EventArgs args)
        {
            var alturaDisponivel = Math.Max(0, area.ClientSize.Height - 170 * DeviceDpi / 96);
            var altura = Math.Min(secoes.Height, alturaDisponivel);
            if (alturaOpcoes.Height != altura)
                alturaOpcoes.Height = altura;
        }

        secoes.SizeChanged += AjustarAltura;
        area.ClientSizeChanged += AjustarAltura;
        return area;
    }

    private GroupBox MontarGrupoDeRegras(GroupBox grupo, AmbienteDestino ambiente)
    {
        var regras = OpcoesDoAmbiente(ambiente).ToArray();
        if (!LayoutSuperior) return MontarSecao(grupo, regras);

        int divisao = (regras.Length + 1) / 2;
        return MontarSecao(grupo, ColunasCompactas(regras[..divisao], regras[divisao..]));
    }

    private static TableLayoutPanel FaixaDeSecoes(params float[] larguras)
    {
        var faixa = new TableLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Fill, ColumnCount = larguras.Length, RowCount = 1, Margin = Padding.Empty };
        foreach (float largura in larguras) faixa.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, largura));
        return faixa;
    }

    private static TableLayoutPanel ColunasCompactas(Control[] esquerda, Control[] direita)
    {
        var colunas = FaixaDeSecoes(50, 50);
        var primeira = PilhaCompacta(esquerda);
        primeira.Margin = new Padding(0, 3, 2, 3);
        var segunda = PilhaCompacta(direita);
        segunda.Margin = new Padding(2, 3, 0, 3);
        colunas.Controls.Add(primeira, 0, 0);
        colunas.Controls.Add(segunda, 1, 0);
        return colunas;
    }

    private static TableLayoutPanel CampoComComplemento(Control campo, Control complemento)
    {
        var linha = new TableLayoutPanel { ColumnCount = 2, RowCount = 1,
            AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink };
        linha.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        linha.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        campo.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        if (complemento is Button botao) botao.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        linha.Controls.Add(campo, 0, 0);
        linha.Controls.Add(complemento, 1, 0);
        return linha;
    }

    private TableLayoutPanel MontarCamposDeDesempenho()
    {
        var campos = new TableLayoutPanel { ColumnCount = 2, RowCount = 2,
            AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink };
        campos.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        campos.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        campos.Controls.Add(Legenda("Leitores SHA-256"), 0, 0);
        campos.Controls.Add(Legenda("Limite do índice"), 1, 0);
        campos.Controls.Add(_maximoHashes, 0, 1);
        campos.Controls.Add(_limiteIndexacao, 1, 1);
        return campos;
    }

    private static GroupBox MontarSecao(GroupBox grupo, params Control[] controles)
    {
        grupo.AutoSize = true;
        grupo.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        grupo.Dock = DockStyle.Fill;
        int margemHorizontal = LayoutSuperior ? 6 : 8;
        grupo.Padding = new Padding(margemHorizontal, 3, margemHorizontal, 5);
        grupo.Controls.Add(PilhaCompacta(controles));
        return grupo;
    }

    private static TableLayoutPanel PilhaCompacta(params Control[] controles)
    {
        var pilha = Pilha();
        pilha.Padding = Padding.Empty;
        foreach (var controle in controles)
        {
            controle.Dock = DockStyle.Fill;
            controle.Margin = new Padding(2, 0, 2, 1);
            if (controle is Button botao) botao.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            pilha.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            pilha.Controls.Add(controle, 0, pilha.RowStyles.Count - 1);
        }
        if (LayoutSuperior) return pilha;

        pilha.ClientSizeChanged += (_, _) =>
        {
            foreach (var controle in controles)
            {
                if (controle is not (Label or CheckBox or RadioButton)) continue;
                var largura = Math.Max(1, pilha.ClientSize.Width - controle.Margin.Horizontal);
                if (controle.MaximumSize.Width != largura)
                    controle.MaximumSize = new Size(largura, 0);
            }
        };
        return pilha;
    }

    private static Label Legenda(string texto) => new()
    {
        Text = texto, AutoSize = true, Anchor = AnchorStyles.Left
    };
}
