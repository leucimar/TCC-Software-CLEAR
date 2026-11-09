using System.Text.Json;
using System.Text.RegularExpressions;

namespace Clear.Desktop;

internal static class ApresentacaoResultados
{
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
                    "ModificadoUtc" => "Modificação (UTC)",
                    "AcessadoUtc" or "UltimoAcessoUtc" => "Último acesso (UTC)",
                    "Sha256Hex" => "SHA-256",
                    _ => Regex.Replace(coluna.Name, "(?<=[a-z])(?=[A-Z])", " ")
                };
                if (coluna.Name is "EvidenciasJson" or "VerificacoesJson") coluna.Visible = false;
            }
        };
        grade.CellFormatting += (_, e) =>
        {
            if (e.ColumnIndex >= 0 && grade.Columns[e.ColumnIndex].Name == "CategoriasJson"
                && e.Value is string valor)
            { e.Value = Categorias(valor); e.FormattingApplied = true; }
        };
    }
}
