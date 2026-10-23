using System.Text.Json;

namespace Clear.Desktop;

/// <summary>Paleta nativa compartilhada, sem bibliotecas ou processos adicionais.</summary>
internal static class TemaVisual
{
    private static readonly string Preferencias = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "CLEAR", "preferencias-interface.json");
    public static bool Escuro { get; private set; } = LerPreferencia();
    private static readonly Font Fonte = new("Segoe UI", 10);

    private static bool LerPreferencia()
    {
        try { return File.Exists(Preferencias) &&
            JsonSerializer.Deserialize<bool>(File.ReadAllText(Preferencias)); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        { return false; }
    }

    public static string? Alterar(bool escuro)
    {
        Escuro = escuro;
        foreach (Form janela in System.Windows.Forms.Application.OpenForms) Aplicar(janela);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Preferencias)!);
            File.WriteAllText(Preferencias, JsonSerializer.Serialize(escuro));
            return null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { return "Tema aplicado nesta sessão; não foi possível salvar a preferência: " + ex.Message; }
    }

    public static void Registrar(Form janela)
    {
        janela.Font = Fonte;
        janela.StartPosition = FormStartPosition.CenterScreen;
        PrepararGrades(janela);
        Aplicar(janela);
    }

    private static void PrepararGrades(Control controle)
    {
        if (controle is DataGridView grade) ApresentacaoResultados.Preparar(grade);
        foreach (Control filho in controle.Controls) PrepararGrades(filho);
    }

    public static void Aplicar(Control controle)
    {
        var fundo = Escuro ? Color.FromArgb(20, 29, 43) : Color.FromArgb(235, 240, 246);
        var superficie = Escuro ? Color.FromArgb(29, 41, 58) : Color.White;
        var texto = Escuro ? Color.FromArgb(226, 234, 244) : Color.FromArgb(29, 45, 65);
        var borda = Escuro ? Color.FromArgb(57, 76, 99) : Color.FromArgb(202, 213, 225);
        var acento = Color.FromArgb(15, 118, 128);
        controle.BackColor = controle is TextBoxBase or ListBox or ComboBox or NumericUpDown
            or TabPage ? superficie : fundo;
        controle.ForeColor = texto;
        if (controle.Tag as string == "header")
        { controle.BackColor = Color.FromArgb(20, 42, 65); controle.ForeColor = Color.White; }
        if (controle is Button botao)
        {
            botao.FlatStyle = FlatStyle.Flat;
            botao.FlatAppearance.BorderColor = borda;
            botao.Padding = new Padding(10, 4, 10, 4);
            botao.MinimumSize = new Size(0, 34);
            botao.UseVisualStyleBackColor = false;
            botao.BackColor = botao.Tag as string == "primary" ? acento : superficie;
            if (botao.Tag as string == "primary") botao.ForeColor = Color.White;
        }
        if (controle is DataGridView grade)
        {
            grade.BackgroundColor = superficie;
            grade.BorderStyle = BorderStyle.None;
            grade.GridColor = borda;
            grade.EnableHeadersVisualStyles = false;
            grade.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(20, 42, 65);
            grade.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            grade.ColumnHeadersDefaultCellStyle.SelectionBackColor = Color.FromArgb(20, 42, 65);
            grade.ColumnHeadersHeight = 38;
            grade.DefaultCellStyle.BackColor = superficie;
            grade.DefaultCellStyle.ForeColor = texto;
            grade.DefaultCellStyle.SelectionBackColor = acento;
            grade.DefaultCellStyle.SelectionForeColor = Color.White;
            grade.AlternatingRowsDefaultCellStyle.BackColor = Escuro
                ? Color.FromArgb(34, 48, 66) : Color.FromArgb(244, 247, 250);
            grade.RowTemplate.Height = 32;
        }
        foreach (Control filho in controle.Controls) Aplicar(filho);
        controle.Invalidate();
    }
}
