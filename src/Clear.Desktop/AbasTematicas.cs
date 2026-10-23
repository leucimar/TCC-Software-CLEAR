namespace Clear.Desktop;

internal sealed class AbasTematicas : TabControl
{
    public AbasTematicas()
    {
        DrawMode = TabDrawMode.OwnerDrawFixed;
        Padding = new Point(14, 8);
    }

    protected override void OnDrawItem(DrawItemEventArgs e)
    {
        var selecionada = e.Index == SelectedIndex;
        var fundo = selecionada ? Color.FromArgb(15, 118, 128) :
            TemaVisual.Escuro ? Color.FromArgb(29, 41, 58) : Color.FromArgb(222, 231, 240);
        var texto = selecionada || TemaVisual.Escuro ? Color.White : Color.FromArgb(29, 45, 65);
        using var pincel = new SolidBrush(fundo);
        e.Graphics.FillRectangle(pincel, e.Bounds);
        TextRenderer.DrawText(e.Graphics, TabPages[e.Index].Text, Font, e.Bounds, texto,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        if ((e.State & DrawItemState.Focus) != 0) e.DrawFocusRectangle();
    }
}
