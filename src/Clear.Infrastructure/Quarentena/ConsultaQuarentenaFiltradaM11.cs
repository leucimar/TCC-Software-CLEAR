using System.Globalization;
using Microsoft.Data.Sqlite;

namespace Clear.Infrastructure.Quarentena;

/// <summary>Filtra registros auditados, não faz nenhuma movimentação.</summary>
public sealed record FiltroRestauracaoM11(
    string? Extensao = null, string? TrechoCaminho = null,
    long? MinimoBytes = null, long? MaximoBytes = null);

public static class ConsultaQuarentenaFiltradaM11
{
    public const int TamanhoPagina = 200;
    public const int MaximoLote = 10000;

    private const string Condicoes = """
        execucao_id = $exec
        AND estado = 'EmQuarentena'
        AND ($ext = '' OR lower(substr(original, -length($ext))) = lower($ext))
        AND ($nome = '' OR instr(lower(original), lower($nome)) > 0)
        AND ($min < 0 OR tamanho >= $min)
        AND ($max < 0 OR tamanho <= $max)
        """;

    public static async Task<(IReadOnlyList<RegistroQuarentenaM7> Registros, long Total)>
        ConsultarAsync(string banco, string execucao, FiltroRestauracaoM11 filtro,
            int pagina, CancellationToken ct = default)
    {
        Validar(execucao, filtro);
        if (pagina < 0) throw new ArgumentOutOfRangeException(nameof(pagina));
        await using var db = await AbrirAsync(banco, ct).ConfigureAwait(false);
        await using var count = db.CreateCommand();
        count.CommandText = "SELECT COUNT(*) FROM movimentacoes WHERE " + Condicoes;
        Parametros(count, execucao, filtro);
        var total = Convert.ToInt64(await count.ExecuteScalarAsync(ct).ConfigureAwait(false),
            CultureInfo.InvariantCulture);
        await using var cmd = db.CreateCommand();
        cmd.CommandText = """
            SELECT id, estado, original, destino, tamanho, sha256, execucao_id, mensagem
            FROM movimentacoes WHERE
            """ + Condicoes + " ORDER BY original, id LIMIT $lim OFFSET $offset";
        Parametros(cmd, execucao, filtro);
        cmd.Parameters.AddWithValue("$lim", TamanhoPagina);
        cmd.Parameters.AddWithValue("$offset", checked((long)pagina * TamanhoPagina));
        var registros = new List<RegistroQuarentenaM7>();
        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
            registros.Add(new RegistroQuarentenaM7(reader.GetString(0), reader.GetString(1),
                reader.GetString(2), reader.GetString(3), reader.GetInt64(4),
                reader.GetString(5), reader.GetString(6),
                reader.IsDBNull(7) ? null : reader.GetString(7)));
        return (registros.AsReadOnly(), total);
    }

    /// <summary>
    /// Somente uma ação explícita do usuário deve chamar este método para
    /// selecionar todo o subconjunto filtrado. Não move e não restaura.
    /// </summary>
    public static async Task<IReadOnlyList<string>> SelecionarTodosFiltradosAsync(
        string banco, string execucao, FiltroRestauracaoM11 filtro,
        CancellationToken ct = default)
    {
        Validar(execucao, filtro);
        await using var db = await AbrirAsync(banco, ct).ConfigureAwait(false);
        await using var cmd = db.CreateCommand();
        cmd.CommandText = "SELECT id FROM movimentacoes WHERE " + Condicoes +
            " ORDER BY original, id LIMIT $max";
        Parametros(cmd, execucao, filtro);
        cmd.Parameters.AddWithValue("$max", MaximoLote + 1);
        var ids = new List<string>();
        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            ids.Add(reader.GetString(0));
            if (ids.Count > MaximoLote)
                throw new InvalidOperationException(
                    "Mais de 10.000 arquivos elegíveis. Divida em lotes menores.");
        }
        return ids.AsReadOnly();
    }

    public static async Task<IReadOnlyList<RegistroQuarentenaM7>> ConsultarSelecionadosAsync(
        string banco, string execucao, IReadOnlyCollection<string> ids,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(ids);
        if (string.IsNullOrWhiteSpace(execucao) ||
            ids.Count is < 1 or > MaximoLote || ids.Any(string.IsNullOrWhiteSpace) ||
            ids.Distinct(StringComparer.Ordinal).Count() != ids.Count)
            throw new ArgumentException("Seleção inválida.");
        await using var db = await AbrirAsync(banco, ct).ConfigureAwait(false);
        var registros = new List<RegistroQuarentenaM7>(ids.Count);
        foreach (var lote in ids.OrderBy(x => x, StringComparer.Ordinal).Chunk(200))
        {
            await using var cmd = db.CreateCommand();
            cmd.Parameters.AddWithValue("$exec", execucao);
            var nomes = new List<string>();
            for (var i = 0; i < lote.Length; i++)
            {
                var nome = "$id" + i;
                nomes.Add(nome);
                cmd.Parameters.AddWithValue(nome, lote[i]);
            }
            cmd.CommandText = """
                SELECT id, estado, original, destino, tamanho, sha256, execucao_id, mensagem
                FROM movimentacoes WHERE execucao_id = $exec AND estado = 'EmQuarentena'
                AND id IN (
                """ + string.Join(",", nomes) + ") ORDER BY original, id";
            await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while (await reader.ReadAsync(ct).ConfigureAwait(false))
                registros.Add(new RegistroQuarentenaM7(reader.GetString(0), reader.GetString(1),
                    reader.GetString(2), reader.GetString(3), reader.GetInt64(4),
                    reader.GetString(5), reader.GetString(6),
                    reader.IsDBNull(7) ? null : reader.GetString(7)));
        }
        if (registros.Count != ids.Count)
            throw new InvalidOperationException(
                "Parte da seleção não está mais em quarentena. Recarregue antes de confirmar.");
        return registros.AsReadOnly();
    }

    private static void Validar(string exec, FiltroRestauracaoM11 filtro)
    {
        ArgumentNullException.ThrowIfNull(filtro);
        if (string.IsNullOrWhiteSpace(exec)) throw new ArgumentException("Execução obrigatória.");
        if (filtro.MinimoBytes is < 0 || filtro.MaximoBytes is < 0 ||
            (filtro.MinimoBytes.HasValue && filtro.MaximoBytes.HasValue &&
             filtro.MinimoBytes.Value > filtro.MaximoBytes.Value))
            throw new ArgumentException("Limite de tamanho inválido.");
    }

    private static void Parametros(SqliteCommand cmd, string exec, FiltroRestauracaoM11 f)
    {
        cmd.Parameters.AddWithValue("$exec", exec);
        cmd.Parameters.AddWithValue("$ext", f.Extensao ?? "");
        cmd.Parameters.AddWithValue("$nome", f.TrechoCaminho ?? "");
        cmd.Parameters.AddWithValue("$min", f.MinimoBytes ?? -1);
        cmd.Parameters.AddWithValue("$max", f.MaximoBytes ?? -1);
    }

    private static async Task<SqliteConnection> AbrirAsync(
        string banco, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(banco) || !File.Exists(banco))
            throw new FileNotFoundException("Auditoria indisponível.", banco);
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = Path.GetFullPath(banco), Mode = SqliteOpenMode.ReadOnly,
            Pooling = false
        }.ToString());
        try
        {
            await connection.OpenAsync(ct).ConfigureAwait(false);
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = "PRAGMA user_version";
            var versao = Convert.ToInt32(await cmd.ExecuteScalarAsync(ct).ConfigureAwait(false),
                CultureInfo.InvariantCulture);
            if (versao is not (1 or ServicoQuarentenaM7.VersaoEsquema))
                throw new InvalidDataException("Banco de auditoria incompatível.");
            return connection;
        }
        catch
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }
}
