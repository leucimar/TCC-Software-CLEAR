using Microsoft.Data.Sqlite;

namespace Clear.Infrastructure.Historico;

/// <summary>Registro de análise anterior; não representa o estado atual do arquivo.</summary>
public sealed record EntradaHistoricaM6(
    long Id, string Tipo, string Caminho, long? TamanhoBytes,
    string? ModificadoUtc, string CategoriasJson, string EvidenciasJson,
    string VerificacoesJson, bool AcimaDoLimitePessoal,
    bool EhDuplicataExata = false);

public sealed record MembroDuplicataHistoricaM6(
    long Grupo, long TamanhoBytes, string Sha256,
    long BytesPotenciaisGrupo, string Caminho);

public sealed record OcorrenciaHistoricaM6(
    string Origem, string Tipo, string Caminho, string Descricao);

/// <summary>
/// Consultas somente leitura e paginadas do esquema SQLite já existente.
/// Parâmetros de entrada não são interpolados nas instruções SQL.
/// </summary>
public static class ConsultaHistoricoSqliteM6
{
    public const int TamanhoPagina = 200;

    public static async Task<IReadOnlyList<EntradaHistoricaM6>> ConsultarEntradasAsync(
        string caminhoBanco, string idExecucao, int pagina,
        CancellationToken cancellationToken = default)
    {
        ValidarPaginaEExecucao(idExecucao, pagina);
        await using var conexao = await AbrirSomenteLeituraAsync(caminhoBanco, cancellationToken)
            .ConfigureAwait(false);
        await using var comando = conexao.CreateCommand();
        comando.CommandText = """
            SELECT id, tipo, caminho, tamanho_bytes, modificado_utc,
                   categorias_json, evidencias_json, verificacoes_json,
                   acima_limite_pessoal
            FROM entradas WHERE execucao_id = $id
            ORDER BY id LIMIT $limite OFFSET $offset;
            """;
        comando.Parameters.AddWithValue("$id", idExecucao);
        comando.Parameters.AddWithValue("$limite", TamanhoPagina);
        comando.Parameters.AddWithValue("$offset", checked((long)pagina * TamanhoPagina));
        var result = new List<EntradaHistoricaM6>();
        await using var leitor = await comando.ExecuteReaderAsync(cancellationToken)
            .ConfigureAwait(false);
        while (await leitor.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            result.Add(new EntradaHistoricaM6(
                leitor.GetInt64(0), leitor.GetString(1), leitor.GetString(2),
                leitor.IsDBNull(3) ? null : leitor.GetInt64(3),
                leitor.IsDBNull(4) ? null : leitor.GetString(4),
                leitor.GetString(5), leitor.GetString(6), leitor.GetString(7),
                leitor.GetInt64(8) == 1));
        }
        return result.AsReadOnly();
    }

    public static async Task<IReadOnlyList<MembroDuplicataHistoricaM6>> ConsultarDuplicatasAsync(
        string caminhoBanco, string idExecucao, int pagina = 0,
        CancellationToken cancellationToken = default)
    {
        ValidarPaginaEExecucao(idExecucao, pagina);
        await using var conexao = await AbrirSomenteLeituraAsync(caminhoBanco, cancellationToken)
            .ConfigureAwait(false);
        await using var comando = conexao.CreateCommand();
        comando.CommandText = """
            SELECT g.ordem, g.tamanho_bytes, g.sha256, g.bytes_potenciais, m.caminho
            FROM grupos_duplicatas AS g
            INNER JOIN membros_duplicatas AS m
              ON m.execucao_id = g.execucao_id AND m.ordem_grupo = g.ordem
            WHERE g.execucao_id = $id
            ORDER BY g.ordem, m.caminho LIMIT $limite OFFSET $offset;
            """;
        comando.Parameters.AddWithValue("$id", idExecucao);
        comando.Parameters.AddWithValue("$limite", TamanhoPagina);
        comando.Parameters.AddWithValue("$offset", checked((long)pagina * TamanhoPagina));
        var result = new List<MembroDuplicataHistoricaM6>();
        await using var leitor = await comando.ExecuteReaderAsync(cancellationToken)
            .ConfigureAwait(false);
        while (await leitor.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            result.Add(new MembroDuplicataHistoricaM6(
                leitor.GetInt64(0), leitor.GetInt64(1), leitor.GetString(2),
                leitor.GetInt64(3), leitor.GetString(4)));
        }
        return result.AsReadOnly();
    }

    public static async Task<IReadOnlyList<OcorrenciaHistoricaM6>> ConsultarOcorrenciasAsync(
        string caminhoBanco, string idExecucao, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(idExecucao))
            throw new ArgumentException("Selecione uma execução histórica.", nameof(idExecucao));
        await using var conexao = await AbrirSomenteLeituraAsync(caminhoBanco, cancellationToken)
            .ConfigureAwait(false);
        await using var comando = conexao.CreateCommand();
        comando.CommandText = """
            SELECT origem, tipo, caminho, descricao
            FROM ocorrencias WHERE execucao_id = $id ORDER BY id LIMIT 200;
            """;
        comando.Parameters.AddWithValue("$id", idExecucao);
        var result = new List<OcorrenciaHistoricaM6>();
        await using var leitor = await comando.ExecuteReaderAsync(cancellationToken)
            .ConfigureAwait(false);
        while (await leitor.ReadAsync(cancellationToken).ConfigureAwait(false))
            result.Add(new OcorrenciaHistoricaM6(
                leitor.GetString(0), leitor.GetString(1),
                leitor.GetString(2), leitor.GetString(3)));
        return result.AsReadOnly();
    }

    private static void ValidarPaginaEExecucao(string idExecucao, int pagina)
    {
        if (string.IsNullOrWhiteSpace(idExecucao))
            throw new ArgumentException("Selecione uma execução histórica.", nameof(idExecucao));
        if (pagina < 0)
            throw new ArgumentOutOfRangeException(nameof(pagina));
    }

    private static async Task<SqliteConnection> AbrirSomenteLeituraAsync(
        string caminhoBanco, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(caminhoBanco) || !File.Exists(caminhoBanco))
            throw new FileNotFoundException("Banco SQLite do histórico não encontrado.", caminhoBanco);
        var conexao = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = Path.GetFullPath(caminhoBanco),
            Mode = SqliteOpenMode.ReadOnly,
            Pooling = false
        }.ToString());
        try
        {
            await conexao.OpenAsync(cancellationToken).ConfigureAwait(false);
            await using var cmd = conexao.CreateCommand();
            cmd.CommandText = "PRAGMA user_version;";
            var versao = Convert.ToInt32(
                await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false));
            if (versao != SessaoHistoricoSqliteM6.VersaoEsquema)
                throw new InvalidDataException($"Versão SQLite não suportada: {versao}.");
            return conexao;
        }
        catch
        {
            await conexao.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }
}
