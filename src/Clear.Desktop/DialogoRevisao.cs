namespace Clear.Desktop;

/// <summary>Confirmação explícita com conteúdo rolável e recusa como ação padrão.</summary>
internal sealed class DialogoRevisao : Form
{
    public static DialogResult Confirmar(IWin32Window owner, string texto, string titulo,
        MessageBoxButtons botoes, MessageBoxIcon icone, MessageBoxDefaultButton padrao)
    {
        using var janela = new DialogoRevisao(texto, titulo);
        return janela.ShowDialog(owner);
    }

    private DialogoRevisao(string texto, string titulo)
    {
        Text = titulo;
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(640, 440);
        Size = new Size(840, 610);
        MinimizeBox = false;
        var painel = new TableLayoutPanel
        { Dock = DockStyle.Fill, Padding = new Padding(20), RowCount = 3 };
        painel.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        painel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        painel.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
        painel.Controls.Add(new Label { Text = "Revise os arquivos e confirme a operação",
            Dock = DockStyle.Fill, Font = new Font("Segoe UI", 13, FontStyle.Bold) }, 0, 0);
        painel.Controls.Add(new TextBox { Text = texto, ReadOnly = true, Multiline = true,
            ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Fill }, 0, 1);
        var acoes = new FlowLayoutPanel { Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(0, 8, 0, 0) };
        var cancelar = new Button { Text = "Voltar sem alterar", AutoSize = true,
            DialogResult = DialogResult.No };
        var confirmar = new Button { Text = "Confirmar operação", AutoSize = true,
            DialogResult = DialogResult.Yes, Tag = "primary" };
        acoes.Controls.Add(cancelar);
        acoes.Controls.Add(confirmar);
        painel.Controls.Add(acoes, 0, 2);
        Controls.Add(painel);
        AcceptButton = cancelar;
        CancelButton = cancelar;
        TemaVisual.Registrar(this);
    }
}
