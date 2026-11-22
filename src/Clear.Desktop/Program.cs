namespace Clear.Desktop;

static class Program
{
    /// <summary>
    ///  The main entry point for the application.
    /// </summary>
    [STAThread]
    static void Main(string[] args)
    {
        // To customize application configuration such as set high DPI settings or default font,
        // see https://aka.ms/applicationconfiguration.
        ApplicationConfiguration.Initialize();
        using var janela = new Form1();
        if (args.Length == 2 && args[0] == "--configuracao-elevacao")
        {
            try { janela.AplicarConfiguracaoElevacao(args[1]); }
            catch (Exception ex)
            {
                MessageBox.Show("Não foi possível recuperar as opções: " + ex.Message +
                    "\nA janela original continua disponível. Nenhuma análise foi iniciada.", "CLEAR — Elevação",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
        }
        System.Windows.Forms.Application.Run(janela);
    }
}