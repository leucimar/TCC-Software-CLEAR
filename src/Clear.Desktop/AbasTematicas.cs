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
        var cores = TemaVisual.Cores;
        var fundo = selecionada ? cores.Destaque : cores.Superficie;
        var texto = selecionada ? cores.TextoDestaque : cores.Texto;
        using var pincel = new SolidBrush(fundo);
        e.Graphics.FillRectangle(pincel, e.Bounds);
        TextRenderer.DrawText(e.Graphics, TabPages[e.Index].Text, Font, e.Bounds, texto,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        if ((e.State & DrawItemState.Focus) != 0) e.DrawFocusRectangle();
    }
}
