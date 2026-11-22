namespace Clear.Desktop;

internal sealed record RotaDeLote(string Origem, string Destino, int Quantidade);

internal static class DialogoLote
{
    public static bool Confirmar(IWin32Window dono, bool restaurar,
        IReadOnlyCollection<RotaDeLote> rotas, int foraDosFiltros)
    {
        var quantidade = rotas.Sum(r => r.Quantidade);
        using var janela = new Form
        {
            Text = "CLEAR", FormBorderStyle = FormBorderStyle.FixedDialog,
            StartPosition = FormStartPosition.CenterParent, ShowInTaskbar = false,
            MaximizeBox = false, MinimizeBox = false,
            ClientSize = new Size(530, rotas.Count > 1 ? 310 : 235)
        };
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, Padding = new Padding(13), ColumnCount = 1, RowCount = 4
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        using var fonteTitulo = new Font(janela.Font.FontFamily, 13, FontStyle.Bold);
        layout.Controls.Add(new Label
        {
            Text = restaurar ? "Restaurar arquivos?" : "Mover arquivos para quarentena?",
            Font = fonteTitulo, AutoSize = true, Margin = new Padding(3, 2, 3, 6)
        }, 0, 0);
        layout.Controls.Add(new Label
        {
            Text = $"{quantidade:N0} arquivo(s) selecionado(s)" +
                (foraDosFiltros > 0 ? $" · {foraDosFiltros:N0} fora dos filtros atuais" : ""),
            AutoSize = true, Margin = new Padding(3, 2, 3, 8)
        }, 0, 1);
        var texto = new TextBox
        {
            Dock = DockStyle.Fill, ReadOnly = true, Multiline = true, ScrollBars = ScrollBars.Vertical,
            TabStop = false, Text = string.Join(Environment.NewLine + Environment.NewLine,
                rotas.Select(r => $"Origem: {r.Origem}" + Environment.NewLine + $"Destino: {r.Destino}" +
                    (rotas.Count > 1 ? Environment.NewLine + $"Arquivos: {r.Quantidade:N0}" : "")))
        };
        layout.Controls.Add(texto, 0, 2);
        var cancelar = PainelInventario.Botao("Cancelar");
        cancelar.DialogResult = DialogResult.Cancel;
        var confirmar = PainelInventario.Botao(restaurar ? "Restaurar" : "Mover para quarentena");
        confirmar.DialogResult = DialogResult.Yes;
        confirmar.Tag = "primary";
        var acoes = PainelInventario.Faixa(confirmar, cancelar);
        acoes.FlowDirection = FlowDirection.RightToLeft;
        acoes.Margin = new Padding(0, 7, 0, 0);
        layout.Controls.Add(acoes, 0, 3);
        janela.Controls.Add(layout);
        janela.AcceptButton = cancelar;
        janela.CancelButton = cancelar;
        TemaVisual.Registrar(janela);
        janela.StartPosition = FormStartPosition.CenterParent;
        janela.ActiveControl = cancelar;
        return janela.ShowDialog(dono) == DialogResult.Yes;
    }
}
