using System.Globalization;
using System.Text;
using System.Text.Json;
using Clear.Application.Metricas;
using Microsoft.Data.Sqlite;

namespace Clear.Infrastructure.Metricas;

/// <summary>Banco independente: não migra nem reescreve o histórico M6 existente.</summary>
public static class RepositorioMetricasAnalise
{
    public static string CaminhoPadrao => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CLEAR", "metricas.sqlite3");

    public static async Task SalvarAsync(string banco, MetricasAnalise m,
        CancellationToken ct = default)
    {
        ValidarDestino(m.PastaRaiz, banco);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(banco))!);
        await using var db = new SqliteConnection(new SqliteConnectionStringBuilder {
            DataSource = Path.GetFullPath(banco), Pooling = false }.ToString());
        await db.OpenAsync(ct).ConfigureAwait(false);
        await using var tx = await db.BeginTransactionAsync(ct).ConfigureAwait(false);
        await using var cmd = db.CreateCommand();cmd.Transaction = (SqliteTransaction)tx;
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS metricas_analises (
                id TEXT PRIMARY KEY, pasta_raiz TEXT NOT NULL, inicio_utc TEXT NOT NULL,
                arquivos INTEGER NOT NULL, bytes INTEGER NOT NULL, leitores INTEGER NOT NULL,
                inventario_segundos REAL NOT NULL, duplicatas_segundos REAL NOT NULL,
                total_segundos REAL NOT NULL, parcial INTEGER NOT NULL, dados_json TEXT NOT NULL);
            INSERT INTO metricas_analises VALUES
                ($id,$pasta,$inicio,$arquivos,$bytes,$leitores,$inventario,$duplicatas,$total,$parcial,$json);
            """;
        cmd.Parameters.AddWithValue("$id",m.Id);cmd.Parameters.AddWithValue("$pasta",m.PastaRaiz);
        cmd.Parameters.AddWithValue("$inicio",m.InicioUtc.ToString("O"));
        cmd.Parameters.AddWithValue("$arquivos",m.Arquivos);cmd.Parameters.AddWithValue("$bytes",m.Bytes);
        cmd.Parameters.AddWithValue("$leitores",m.Leitores);cmd.Parameters.AddWithValue("$inventario",m.InventarioSegundos);
        cmd.Parameters.AddWithValue("$duplicatas",m.DuplicatasSegundos);cmd.Parameters.AddWithValue("$total",m.TotalSegundos);
        cmd.Parameters.AddWithValue("$parcial",m.Parcial ? 1 : 0);cmd.Parameters.AddWithValue("$json",JsonSerializer.Serialize(m));
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        await tx.CommitAsync(ct).ConfigureAwait(false);
    }

    public static async Task<IReadOnlyList<MetricasAnalise>> ConsultarAsync(string banco,
        CancellationToken ct = default)
    {
        await using var db = new SqliteConnection(new SqliteConnectionStringBuilder {
            DataSource = Path.GetFullPath(banco), Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ToString());
        await db.OpenAsync(ct).ConfigureAwait(false);
        await using var cmd = db.CreateCommand();cmd.CommandText = "SELECT dados_json FROM metricas_analises ORDER BY inicio_utc, id";
        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        var rows = new List<MetricasAnalise>();
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
            rows.Add(JsonSerializer.Deserialize<MetricasAnalise>(reader.GetString(0))
                ?? throw new InvalidDataException("Registro de métricas inválido."));
        return rows;
    }

    public static async Task ExportarCsvAsync(string destino, IReadOnlyList<MetricasAnalise> medicoes,
        CancellationToken ct = default)
    {
        foreach (var m in medicoes) ValidarDestino(m.PastaRaiz, destino);
        var temporario = Path.GetFullPath(destino) + "." + Guid.NewGuid().ToString("N") + ".part";
        try
        {
            await using (var writer = new StreamWriter(new FileStream(temporario,
                FileMode.CreateNew,FileAccess.Write,FileShare.None),new UTF8Encoding(true)))
            {
                await writer.WriteLineAsync("Execucao;InicioUtc;Origem;Arquivos;Bytes;Leitores;InventarioSegundos;DuplicatasSegundos;TotalSegundos;Parcial;Categoria;Avaliada;Quantidade;Denominador;UnidadeDenominador;Percentual;Nota;CriterioTemporal;PoliticaPreservacao;AberturasProtegidas;LeiturasSemGarantia;LeiturasIgnoradas");
                foreach (var m in medicoes)
                    foreach (var c in m.Categorias)
                    {
                        ct.ThrowIfCancellationRequested();
                        // Cultura pt-BR: separador ; e decimal , para o Excel local.
                        var fields = new object?[] {m.Id,m.InicioUtc.ToString("O"),m.PastaRaiz,m.Arquivos,m.Bytes,m.Leitores,
                            m.InventarioSegundos,m.DuplicatasSegundos,m.TotalSegundos,m.Parcial,c.Categoria,c.Avaliada,
                            c.Quantidade,c.Denominador,c.UnidadeDenominador,c.Percentual,
                            "Categorias podem se sobrepor; indícios não autorizam descarte. Total exclui persistência das próprias métricas.",
                            m.CriterioTemporal, m.PreservacaoAcesso?.Politica, m.PreservacaoAcesso?.AberturasProtegidas,
                            m.PreservacaoAcesso?.LeiturasSemGarantia, m.PreservacaoAcesso?.LeiturasIgnoradas};
                        await writer.WriteLineAsync(string.Join(";",fields.Select(Quote)));
                    }
            }
            ct.ThrowIfCancellationRequested();
            File.Move(temporario,Path.GetFullPath(destino),overwrite:false);
        }
        finally { if (File.Exists(temporario)) File.Delete(temporario); }
    }

    private static string Quote(object? value) => "\"" +
        (Convert.ToString(value,CultureInfo.GetCultureInfo("pt-BR")) ?? "").Replace("\"","\"\"") + "\"";

    private static void ValidarDestino(string raiz,string arquivo)
    {
        var path = Path.GetFullPath(arquivo);
        var relativo = Path.GetRelativePath(Path.GetFullPath(raiz),path);
        if (!Path.IsPathRooted(relativo) && relativo != ".." &&
            !relativo.StartsWith(".."+Path.DirectorySeparatorChar,StringComparison.Ordinal))
            throw new IOException("Salve métricas fora do acervo analisado.");
        for (var dir = new DirectoryInfo(Path.GetDirectoryName(path)!);dir is not null;dir=dir.Parent)
            if (dir.Exists && (dir.Attributes & FileAttributes.ReparsePoint)!=0)
                throw new IOException("Saída de métricas não admite links/junctions.");
        if (File.Exists(path) && (File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0)
            throw new IOException("Arquivo de métricas não admite links.");
    }
}
