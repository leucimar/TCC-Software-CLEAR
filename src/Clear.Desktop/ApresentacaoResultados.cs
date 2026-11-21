using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Clear.Desktop;

internal static class ApresentacaoResultados
{
    public static string HorarioLocal(DateTimeOffset instante)
    {
        var local = instante.ToLocalTime();
        var data = local.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);
        var sinal = local.Offset < TimeSpan.Zero ? "-" : "+";
        var diferenca = local.Offset.Duration().ToString(@"hh\:mm", CultureInfo.InvariantCulture);
        return $"{data} (UTC {sinal} {diferenca})";
    }

    public static string HorarioLocal(string? valor) => DateTimeOffset.TryParse(valor,
        CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var instante)
            ? HorarioLocal(instante) : valor ?? "Indisponível";

    public static string Categorias(string valor)
    {
        try
        {
            using var json = JsonDocument.Parse(valor);
            return string.Join(", ", json.RootElement.EnumerateArray().Select(x => x.GetString() switch
            {
                "ArquivoVazio" => "Arquivo vazio",
                "CopiaBackupOuTemporario" => "Backup ou temporário",
                "SinalTemporal" => "Data antiga (critério da execução)",
                var outro => outro
            }));
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException) { return valor; }
    }

    public static void Preparar(DataGridView grade)
    {
        grade.DataBindingComplete += (_, _) =>
        {
            foreach (DataGridViewColumn coluna in grade.Columns)
            {
                coluna.HeaderText = coluna.Name switch
                {
                    "CategoriasJson" => "Indícios gerais",
                    "EhDuplicataExata" or "DuplicataExata" => "Duplicata exata?",
                    "AcimaDoLimitePessoal" => "Acima do limite pessoal?",
                    "InicioUtc" => "Início (Horário local - UTC)",
                    "FimUtc" => "Fim (Horário local - UTC)",
                    "ModificadoUtc" => "Modificação (horário local)",
                    "AcessadoUtc" or "UltimoAcessoUtc" => "Último acesso (horário local)",
                    "Sha256Hex" => "SHA-256",
                    _ => Regex.Replace(coluna.Name, "(?<=[a-z])(?=[A-Z])", " ")
                };
                if (coluna.Name is "EvidenciasJson" or "VerificacoesJson") coluna.Visible = false;
            }
        };
        grade.CellFormatting += (_, e) =>
        {
            if (e.ColumnIndex < 0) return;
            var coluna = grade.Columns[e.ColumnIndex].Name;
            if (coluna == "CategoriasJson" && e.Value is string valor)
            { e.Value = Categorias(valor); e.FormattingApplied = true; }
            else if (coluna is "InicioUtc" or "FimUtc" or "ModificadoUtc" or "AcessadoUtc" or "UltimoAcessoUtc")
            {
                if (e.Value is DateTimeOffset instante) e.Value = HorarioLocal(instante);
                else if (e.Value is string texto) e.Value = HorarioLocal(texto);
                else return;
                e.FormattingApplied = true;
            }
        };
    }
}
