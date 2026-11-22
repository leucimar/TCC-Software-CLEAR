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

    internal sealed record Paleta(
        Color Fundo, Color Superficie, Color LinhaAlternada, Color Borda, Color Texto,
        Color Cabecalho, Color TextoCabecalho, Color CabecalhoGrade, Color TextoCabecalhoGrade,
        Color Destaque, Color TextoDestaque, Color Selecao, Color TextoSelecao);

    private static readonly Paleta Clara = new(
        Fundo: Cor("#F4F5F7"), Superficie: Color.White, LinhaAlternada: Cor("#F7F8FA"),
        Borda: Cor("#C9CED7"), Texto: Cor("#202530"),
        Cabecalho: Color.White, TextoCabecalho: Cor("#214089"),
        CabecalhoGrade: Cor("#EDF0F4"), TextoCabecalhoGrade: Cor("#242B38"),
        Destaque: Cor("#1E50C5"), TextoDestaque: Color.White,
        Selecao: Cor("#DEEAFF"), TextoSelecao: Cor("#173C89"));

    private static readonly Paleta Escura = new(
        Fundo: Cor("#191A1D"), Superficie: Cor("#222327"), LinhaAlternada: Cor("#292A2E"),
        Borda: Cor("#434750"), Texto: Cor("#F2F3F5"),
        Cabecalho: Cor("#222327"), TextoCabecalho: Cor("#C6D7FF"),
        CabecalhoGrade: Cor("#2E3137"), TextoCabecalhoGrade: Cor("#F2F3F5"),
        Destaque: Cor("#82AAFF"), TextoDestaque: Cor("#111D34"),
        Selecao: Cor("#293E61"), TextoSelecao: Cor("#EFF4FF"));

    internal static Paleta Cores => Escuro ? Escura : Clara;

    private static Color Cor(string hexadecimal) => ColorTranslator.FromHtml(hexadecimal);

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
        var cores = Cores;
        controle.BackColor = controle is TextBoxBase or ListBox or ComboBox or NumericUpDown
            or TabPage ? cores.Superficie : cores.Fundo;
        controle.ForeColor = cores.Texto;
        if (controle.Tag as string == "header")
        {
            controle.BackColor = cores.Cabecalho;
            controle.ForeColor = cores.TextoCabecalho;
        }
        if (controle is Button botao)
        {
            var acaoDeLote = botao is BotaoLote;
            var principal = acaoDeLote || botao.Tag as string == "primary";
            botao.FlatStyle = FlatStyle.Flat;
            botao.FlatAppearance.BorderColor = principal ? cores.Destaque : cores.Borda;
            botao.Padding = new Padding(10, 4, 10, 4);
            botao.MinimumSize = new Size(0, 34);
            botao.UseVisualStyleBackColor = false;
            botao.BackColor = principal ? cores.Destaque : cores.Superficie;
            botao.ForeColor = acaoDeLote ? Color.White : principal ? cores.TextoDestaque : cores.Texto;
        }
        if (controle is DataGridView grade)
        {
            grade.BackgroundColor = cores.Superficie;
            grade.BorderStyle = BorderStyle.None;
            grade.GridColor = cores.Borda;
            grade.EnableHeadersVisualStyles = false;
            grade.ColumnHeadersDefaultCellStyle.BackColor = cores.CabecalhoGrade;
            grade.ColumnHeadersDefaultCellStyle.ForeColor = cores.TextoCabecalhoGrade;
            grade.ColumnHeadersDefaultCellStyle.SelectionBackColor = cores.CabecalhoGrade;
            grade.ColumnHeadersDefaultCellStyle.SelectionForeColor = cores.TextoCabecalhoGrade;
            grade.ColumnHeadersHeight = 38;
            grade.DefaultCellStyle.BackColor = cores.Superficie;
            grade.DefaultCellStyle.ForeColor = cores.Texto;
            grade.DefaultCellStyle.SelectionBackColor = cores.Selecao;
            grade.DefaultCellStyle.SelectionForeColor = cores.TextoSelecao;
            grade.AlternatingRowsDefaultCellStyle.BackColor = cores.LinhaAlternada;
            grade.RowTemplate.Height = 32;
        }
        foreach (Control filho in controle.Controls) Aplicar(filho);
        controle.Invalidate();
    }
}
