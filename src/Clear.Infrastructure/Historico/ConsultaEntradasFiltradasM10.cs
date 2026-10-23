using System.Globalization;
using Microsoft.Data.Sqlite;

namespace Clear.Infrastructure.Historico;

/// <summary>
/// Consulta paginada e parametrizada de arquivos do M6, sem materializar o
/// acervo na interface. Resultados históricos NÃO dispensam revalidação M7.
/// </summary>
public sealed record FiltrosEntradasM10(
    string? Categoria = null,
    string? Extensao = null,
    long? MinimoBytes = null,
    long? MaximoBytes = null,
    DateOnly? AcessadoDesde = null,
    DateOnly? AcessadoAte = null,
    bool ApenasIncompatibilidades = false,
    bool ApenasDuplicatas = false,
    bool ApenasAcimaLimite = false,
    string? TrechoCaminho = null,
    bool ApenasSemAlertas = false,
    bool ApenasComAlertas = false);

public static class ConsultaEntradasFiltradasM10
{
    private const string Colunas = """
        e.id, e.tipo, e.caminho, e.tamanho_bytes, e.modificado_utc,
        e.categorias_json, e.evidencias_json, e.verificacoes_json,
        e.acima_limite_pessoal,
        CASE WHEN e.caminho IN
          (SELECT caminho FROM membros_duplicatas WHERE execucao_id = $exec)
          THEN 1 ELSE 0 END AS eh_duplicata,
        e.acessado_utc
        """;

    private const string Condicoes = """
        e.execucao_id = $exec AND e.tipo = 'Arquivo'
        AND ($cat = '' OR EXISTS
             (SELECT 1 FROM json_each(e.categorias_json) AS c
              WHERE c.value = $cat))
        AND ($ext = '' OR lower(substr(e.caminho, -length($ext))) = lower($ext))
        AND ($minimo < 0 OR e.tamanho_bytes >= $minimo)
        AND ($maximo < 0 OR e.tamanho_bytes <= $maximo)
        AND ($desde = '' OR substr(e.acessado_utc, 1, 10) >= $desde)
        AND ($ate = '' OR substr(e.acessado_utc, 1, 10) <= $ate)
        AND ($incomp = 0 OR EXISTS
             (SELECT 1 FROM json_each(e.verificacoes_json) AS v
              WHERE json_extract(v.value, '$.Estado') = 'IncompatibilidadeDocumentada'))
        AND ($dup = 0 OR e.caminho IN
             (SELECT m.caminho FROM membros_duplicatas AS m
              WHERE m.execucao_id = $exec))
        AND ($limite = 0 OR e.acima_limite_pessoal = 1)
        AND ($trecho = '' OR instr(lower(e.caminho), lower($trecho)) > 0)
        AND ($sem = 0 OR (
            json_array_length(e.categorias_json) = 0 AND e.acima_limite_pessoal = 0
            AND e.caminho NOT IN (SELECT caminho FROM membros_duplicatas WHERE execucao_id = $exec)
            AND NOT EXISTS (SELECT 1 FROM json_each(e.verificacoes_json) AS v
                WHERE json_extract(v.value, '$.Estado') IN ('IncompatibilidadeDocumentada', 'Alerta'))))
        AND ($com = 0 OR (
            json_array_length(e.categorias_json) > 0 OR e.acima_limite_pessoal = 1
            OR e.caminho IN (SELECT caminho FROM membros_duplicatas WHERE execucao_id = $exec)
            OR EXISTS (SELECT 1 FROM json_each(e.verificacoes_json) AS v
                WHERE json_extract(v.value, '$.Estado') IN ('IncompatibilidadeDocumentada', 'Alerta'))))
        """;

    public static async Task<(IReadOnlyList<EntradaHistoricaM6> Entradas, long Total)>
        ConsultarAsync(string banco, string execucao, FiltrosEntradasM10 filtro,
            int pagina, CancellationToken token = default,
            IReadOnlySet<string>? caminhosIndisponiveis = null)
    {
        Validar(execucao, filtro, pagina);
        await using var db = await AbrirAsync(banco, token).ConfigureAwait(false);
        var condicoes = Condicoes;
        if (caminhosIndisponiveis is { Count: > 0 })
        {
            // A exclusão precisa preceder COUNT/LIMIT, para não produzir páginas vazias.
            // A tabela é temporária e não modifica o histórico aberto somente para leitura.
            await using var criar = db.CreateCommand();
            criar.CommandText = "CREATE TEMP TABLE indisponiveis(caminho TEXT PRIMARY KEY COLLATE NOCASE);";
            await criar.ExecuteNonQueryAsync(token).ConfigureAwait(false);
            await using var inserir = db.CreateCommand();
            inserir.CommandText = "INSERT OR IGNORE INTO temp.indisponiveis VALUES ($c);";
            var parametro = inserir.Parameters.Add("$c", SqliteType.Text);
            foreach (var caminho in caminhosIndisponiveis)
            {
                token.ThrowIfCancellationRequested();
                parametro.Value = caminho;
                await inserir.ExecuteNonQueryAsync(token).ConfigureAwait(false);
            }
            condicoes += " AND NOT EXISTS (SELECT 1 FROM temp.indisponiveis i WHERE i.caminho = e.caminho)";
        }
        await using var contagem = db.CreateCommand();
        contagem.CommandText = "SELECT COUNT(*) FROM entradas AS e WHERE " + condicoes;
        Parametros(contagem, execucao, filtro);
        var total = Convert.ToInt64(
            await contagem.ExecuteScalarAsync(token).ConfigureAwait(false),
            CultureInfo.InvariantCulture);
        await using var cmd = db.CreateCommand();
        cmd.CommandText = $"SELECT {Colunas} FROM entradas AS e WHERE " + condicoes +
            " ORDER BY e.id LIMIT $lim OFFSET $offset";
        Parametros(cmd, execucao, filtro);
        cmd.Parameters.AddWithValue("$lim", ConsultaHistoricoSqliteM6.TamanhoPagina);
        cmd.Parameters.AddWithValue("$offset",
            checked((long)pagina * ConsultaHistoricoSqliteM6.TamanhoPagina));
        var entries = new List<EntradaHistoricaM6>();
        await using var reader = await cmd.ExecuteReaderAsync(token).ConfigureAwait(false);
        while (await reader.ReadAsync(token).ConfigureAwait(false))
        {
            entries.Add(Mapear(reader));
        }
        return (entries.AsReadOnly(), total);
    }

    /// <summary>
    /// Seleção GLOBAL de resultados filtrados, acionada somente após confirmação na interface.
    /// Descarta caminhos com operações ainda ativas na quarentena e nunca trunca silenciosamente.
    /// </summary>
    public static async Task<IReadOnlyList<long>> SelecionarTodosFiltradosAsync(
        string banco, string execucao, FiltrosEntradasM10 filtro,
        IReadOnlySet<string> caminhosIndisponiveis, CancellationToken token = default)
    {
        Validar(execucao, filtro, 0);
        ArgumentNullException.ThrowIfNull(caminhosIndisponiveis);
        await using var db = await AbrirAsync(banco, token).ConfigureAwait(false);
        await using var cmd = db.CreateCommand();
        cmd.CommandText = "SELECT e.id, e.caminho FROM entradas AS e WHERE " + Condicoes +
            " ORDER BY e.id";
        Parametros(cmd, execucao, filtro);
        var ids = new List<long>();
        await using var reader = await cmd.ExecuteReaderAsync(token).ConfigureAwait(false);
        while (await reader.ReadAsync(token).ConfigureAwait(false))
        {
            if (caminhosIndisponiveis.Contains(reader.GetString(1)))
                continue;
            ids.Add(reader.GetInt64(0));
            if (ids.Count > 10000)
                throw new InvalidOperationException(
                    "Mais de 10.000 arquivos elegíveis. Refine os filtros ou divida em lotes.");
        }
        return ids.AsReadOnly();
    }

    public static async Task<EntradaHistoricaM6> ConsultarPorIdAsync(
        string banco, string execucao, long entradaId,
        CancellationToken token = default)
    {
        if (string.IsNullOrWhiteSpace(execucao) || entradaId <= 0)
            throw new ArgumentException("Informe execução e entrada válidas.");
        await using var db = await AbrirAsync(banco, token).ConfigureAwait(false);
        await using var cmd = db.CreateCommand();
        cmd.CommandText = $"SELECT {Colunas} FROM entradas AS e " +
            "WHERE e.execucao_id = $exec AND e.id = $id AND e.tipo = 'Arquivo'";
        cmd.Parameters.AddWithValue("$exec", execucao);
        cmd.Parameters.AddWithValue("$id", entradaId);
        await using var leitor = await cmd.ExecuteReaderAsync(token).ConfigureAwait(false);
        if (!await leitor.ReadAsync(token).ConfigureAwait(false))
            throw new InvalidOperationException("Arquivo histórico não encontrado nesta execução.");
        return Mapear(leitor);
    }

    /// <summary>
    /// Resolve somente os IDs explicitamente marcados, em lotes SQL limitados,
    /// sem abrir uma nova conexão para cada arquivo. Nenhuma seleção implícita.
    /// </summary>
    public static async Task<IReadOnlyList<EntradaHistoricaM6>> ConsultarSelecionadasAsync(
        string banco, string execucao, IReadOnlyCollection<long> ids,
        CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(ids);
        if (string.IsNullOrWhiteSpace(execucao))
            throw new ArgumentException("Execução obrigatória.", nameof(execucao));
        if (ids.Count is < 1 or > 10000 || ids.Any(x => x <= 0) ||
            ids.Distinct().Count() != ids.Count)
            throw new ArgumentException(
                "Selecione entre 1 e 10.000 IDs positivos distintos.", nameof(ids));

        await using var db = await AbrirAsync(banco, token).ConfigureAwait(false);
        var encontrados = new List<EntradaHistoricaM6>(ids.Count);
        foreach (var lote in ids.OrderBy(x => x).Chunk(200))
        {
            token.ThrowIfCancellationRequested();
            await using var cmd = db.CreateCommand();
            cmd.Parameters.AddWithValue("$exec", execucao);
            var parametros = new List<string>(lote.Length);
            for (var i = 0; i < lote.Length; i++)
            {
                var chave = "$i" + i;
                parametros.Add(chave);
                cmd.Parameters.AddWithValue(chave, lote[i]);
            }
            cmd.CommandText = $"SELECT {Colunas} FROM entradas AS e " +
                $"WHERE e.execucao_id = $exec AND e.tipo = 'Arquivo' " +
                $"AND e.id IN ({string.Join(",", parametros)}) ORDER BY e.id";
            await using var leitor = await cmd.ExecuteReaderAsync(token).ConfigureAwait(false);
            while (await leitor.ReadAsync(token).ConfigureAwait(false))
                encontrados.Add(Mapear(leitor));
        }

        if (encontrados.Count != ids.Count)
            throw new InvalidOperationException(
                "A seleção contém registros ausentes ou de outra execução. " +
                "Recarregue o histórico antes de autorizar movimentação.");
        return encontrados.AsReadOnly();
    }

    private static EntradaHistoricaM6 Mapear(SqliteDataReader r) => new(
        r.GetInt64(0), r.GetString(1), r.GetString(2),
        r.IsDBNull(3) ? null : r.GetInt64(3),
        r.IsDBNull(4) ? null : r.GetString(4),
        r.GetString(5), r.GetString(6), r.GetString(7),
        r.GetInt64(8) == 1, r.GetInt64(9) == 1,
        r.IsDBNull(10) ? null : r.GetString(10));

    private static void Validar(string execucao, FiltrosEntradasM10 filtro, int pagina)
    {
        ArgumentNullException.ThrowIfNull(filtro);
        if (string.IsNullOrWhiteSpace(execucao))
            throw new ArgumentException("Execução histórica obrigatória.");
        if (pagina < 0) throw new ArgumentOutOfRangeException(nameof(pagina));
        if (filtro.MinimoBytes is < 0 || filtro.MaximoBytes is < 0 ||
            filtro.MinimoBytes > filtro.MaximoBytes &&
            filtro.MaximoBytes is not null)
            throw new ArgumentException("Intervalo de tamanho inválido.");
        if (filtro.ApenasSemAlertas && filtro.ApenasComAlertas)
            throw new ArgumentException("Escolha somente um filtro de alertas.");
        if (filtro.AcessadoDesde > filtro.AcessadoAte)
            throw new ArgumentException("Intervalo de datas inválido.");
    }

    private static void Parametros(SqliteCommand cmd, string id, FiltrosEntradasM10 f)
    {
        cmd.Parameters.AddWithValue("$exec", id);
        cmd.Parameters.AddWithValue("$cat", f.Categoria ?? "");
        cmd.Parameters.AddWithValue("$ext", f.Extensao ?? "");
        cmd.Parameters.AddWithValue("$minimo", f.MinimoBytes ?? -1);
        cmd.Parameters.AddWithValue("$maximo", f.MaximoBytes ?? -1);
        cmd.Parameters.AddWithValue("$desde", f.AcessadoDesde?.ToString("yyyy-MM-dd",
            CultureInfo.InvariantCulture) ?? "");
        cmd.Parameters.AddWithValue("$ate", f.AcessadoAte?.ToString("yyyy-MM-dd",
            CultureInfo.InvariantCulture) ?? "");
        cmd.Parameters.AddWithValue("$incomp", f.ApenasIncompatibilidades ? 1 : 0);
        cmd.Parameters.AddWithValue("$dup", f.ApenasDuplicatas ? 1 : 0);
        cmd.Parameters.AddWithValue("$limite", f.ApenasAcimaLimite ? 1 : 0);
        cmd.Parameters.AddWithValue("$trecho", f.TrechoCaminho ?? "");
        cmd.Parameters.AddWithValue("$sem", f.ApenasSemAlertas ? 1 : 0);
        cmd.Parameters.AddWithValue("$com", f.ApenasComAlertas ? 1 : 0);
    }

    private static async Task<SqliteConnection> AbrirAsync(string banco, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(banco) || !File.Exists(banco))
            throw new FileNotFoundException("Histórico SQLite não encontrado.", banco);
        var db = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = Path.GetFullPath(banco),
            Mode = SqliteOpenMode.ReadOnly,
            Pooling = false
        }.ToString());
        try
        {
            await db.OpenAsync(token).ConfigureAwait(false);
            await using var cmd = db.CreateCommand();
            cmd.CommandText = "PRAGMA user_version";
            if (Convert.ToInt32(await cmd.ExecuteScalarAsync(token).ConfigureAwait(false),
                    CultureInfo.InvariantCulture) != SessaoHistoricoSqliteM6.VersaoEsquema)
                throw new InvalidDataException("Versão do histórico incompatível.");
            return db;
        }
        catch
        {
            await db.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }
}
