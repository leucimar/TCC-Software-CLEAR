using System.Globalization;
using System.Text.Json;
using Clear.Application.Duplicatas;
using Clear.Application.Inventario;
using Clear.Domain.Analise;
using Clear.Domain.Compatibilidade;
using Clear.Domain.Inventario;
using Microsoft.Data.Sqlite;

namespace Clear.Infrastructure.Historico;

/// <summary>
/// Grava uma execução em transação SQLite. Cancelar, falhar ou descartar sem
/// concluir causa rollback; nunca altera o acervo. Não utiliza cache.
/// </summary>
public sealed class SessaoHistoricoSqliteM6 : IObservadorInventario, IAsyncDisposable
{
    public const int VersaoEsquema = 1;
    public Clear.Domain.Arquivos.ResumoPreservacaoAcesso? PreservacaoAcesso { get; set; }

    private readonly SqliteConnection _conexao;
    private readonly SqliteTransaction _transacao;
    private readonly string _id;
    private readonly string _diretorioRaiz;
    private readonly DateTimeOffset _inicioUtc;
    private bool _finalizada;
    private long _entradasPersistidas;

    private SessaoHistoricoSqliteM6(SqliteConnection conexao,
        SqliteTransaction transacao, string id, string diretorioRaiz, DateTimeOffset inicioUtc)
    {
        _conexao = conexao;
        _transacao = transacao;
        _id = id;
        _diretorioRaiz = diretorioRaiz;
        _inicioUtc = inicioUtc;
    }

    public string IdExecucao => _id;
    public long EntradasPersistidas => _entradasPersistidas;

    public static string CaminhoPadrao =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CLEAR", "historico.sqlite3");

    public static async Task<SessaoHistoricoSqliteM6> IniciarAsync(
        string diretorioRaiz, string caminhoBanco, ParametrosHistoricoM6 parametros,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(parametros);
        ArgumentNullException.ThrowIfNull(parametros.FiltrosGeraisHabilitados);
        ArgumentNullException.ThrowIfNull(parametros.VerificacoesHabilitadas);
        if (string.IsNullOrWhiteSpace(diretorioRaiz) || string.IsNullOrWhiteSpace(caminhoBanco))
            throw new ArgumentException("Origem e banco são obrigatórios.");

        var raiz = Path.GetFullPath(diretorioRaiz);
        var banco = Path.GetFullPath(caminhoBanco);
        if (!Directory.Exists(raiz))
            throw new DirectoryNotFoundException("Diretório do inventário não existe.");
        if (Directory.Exists(banco))
            throw new IOException("O caminho do banco aponta para uma pasta.");

        var pastaBanco = Path.GetDirectoryName(banco)
            ?? throw new ArgumentException("Caminho do banco inválido.", nameof(caminhoBanco));

        // Nunca criar o banco, seus arquivos de journal nem novas pastas
        // dentro do acervo sendo analisado.
        var relativo = Path.GetRelativePath(raiz, pastaBanco);
        if (!Path.IsPathRooted(relativo) &&
            !string.Equals(relativo, "..", StringComparison.Ordinal) &&
            !relativo.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw new ArgumentException("O SQLite deve ficar fora da pasta analisada.");

        for (DirectoryInfo? dir = new(pastaBanco); dir is not null; dir = dir.Parent)
        {
            if (dir.Exists && (dir.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new IOException("A saída SQLite não admite diretórios com links/junctions.");
        }
        cancellationToken.ThrowIfCancellationRequested();
        Directory.CreateDirectory(pastaBanco);

        var conexao = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = banco, Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = false
        }.ToString());
        try
        {
            await conexao.OpenAsync(cancellationToken).ConfigureAwait(false);
            await PrepararEsquemaAsync(conexao, cancellationToken).ConfigureAwait(false);
            var transacao = (SqliteTransaction)await conexao.BeginTransactionAsync(cancellationToken)
                .ConfigureAwait(false);
            try
            {
                var id = Guid.NewGuid().ToString("N");
                var inicio = DateTimeOffset.UtcNow;
                await ExecutarAsync(conexao, transacao, """
                    INSERT INTO execucoes (id, inicio_utc, pasta_raiz, parametros_json)
                    VALUES ($id, $inicio, $raiz, $parametros);
                    """, cancellationToken,
                    ("$id", id), ("$inicio", inicio.ToString("O", CultureInfo.InvariantCulture)),
                    ("$raiz", raiz), ("$parametros", JsonSerializer.Serialize(parametros)))
                    .ConfigureAwait(false);
                return new SessaoHistoricoSqliteM6(conexao, transacao, id, raiz, inicio);
            }
            catch
            {
                await transacao.DisposeAsync().ConfigureAwait(false);
                throw;
            }
        }
        catch
        {
            await conexao.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    public async ValueTask RegistrarAsync(EntradaInventariada entrada,
        ResultadoAnaliseArquivo? classificacaoGeral,
        AvaliacaoEntrada avaliacaoCompatibilidade,
        CancellationToken cancellationToken = default)
    {
        if (_finalizada)
            throw new InvalidOperationException("Execução histórica já finalizada.");
        ArgumentNullException.ThrowIfNull(entrada);
        ArgumentNullException.ThrowIfNull(avaliacaoCompatibilidade);
        cancellationToken.ThrowIfCancellationRequested();

        var arquivo = entrada.Arquivo;
        var categorias = classificacaoGeral?.Categorias.Select(x => x.ToString()).ToArray()
            ?? Array.Empty<string>();
        var evidencias = classificacaoGeral?.Evidencias.Select(x => new
        {
            Categoria = x.Categoria.ToString(), x.Justificativa
        }).ToArray();
        var regras = avaliacaoCompatibilidade.Verificacoes.Select(x => new
        {
            x.CodigoRegra, Estado = x.Estado.ToString(), x.Justificativa
        }).ToArray();

        await ExecutarAsync(_conexao, _transacao, """
            INSERT INTO entradas
              (execucao_id, tipo, caminho, tamanho_bytes, modificado_utc, acessado_utc,
               categorias_json, evidencias_json, verificacoes_json, acima_limite_pessoal)
            VALUES
              ($exec, $tipo, $caminho, $bytes, $modificado, $acessado,
               $categorias, $evidencias, $regras, $acima);
            """, cancellationToken,
            ("$exec", _id),
            ("$tipo", entrada.Tipo.ToString()),
            ("$caminho", entrada.CaminhoCompleto),
            ("$bytes", entrada.TamanhoBytes),
            ("$modificado", arquivo?.UltimaModificacaoUtc?.ToString("O", CultureInfo.InvariantCulture)),
            ("$acessado", arquivo?.UltimoAcessoUtc?.ToString("O", CultureInfo.InvariantCulture)),
            ("$categorias", JsonSerializer.Serialize(categorias)),
            ("$evidencias", evidencias is null ? "[]" : JsonSerializer.Serialize(evidencias)),
            ("$regras", JsonSerializer.Serialize(regras)),
            ("$acima", avaliacaoCompatibilidade.AcimaDoLimitePersonalizado ? 1 : 0))
            .ConfigureAwait(false);
        _entradasPersistidas = checked(_entradasPersistidas + 1);
    }

    public async Task ConcluirAsync(ResultadoInventario inventario,
        ResultadoDuplicatas? duplicatas,
        CancellationToken cancellationToken = default)
    {
        if (_finalizada)
            throw new InvalidOperationException("Execução histórica já finalizada.");
        ArgumentNullException.ThrowIfNull(inventario);
        cancellationToken.ThrowIfCancellationRequested();

        // Registra apenas grupos confirmados por comparação de bytes no M4.
        if (duplicatas is not null)
        {
            var ordem = 0;
            foreach (var grupo in duplicatas.Grupos)
            {
                cancellationToken.ThrowIfCancellationRequested();
                ordem++;
                await ExecutarAsync(_conexao, _transacao, """
                    INSERT INTO grupos_duplicatas
                        (execucao_id, ordem, tamanho_bytes, sha256, bytes_potenciais)
                    VALUES ($id, $ordem, $bytes, $sha, $pot);
                    """, cancellationToken,
                    ("$id", _id), ("$ordem", ordem), ("$bytes", grupo.TamanhoBytes),
                    ("$sha", grupo.Sha256Hex), ("$pot", grupo.BytesPotencialmenteRedundantes))
                    .ConfigureAwait(false);

                foreach (var membro in grupo.Arquivos)
                {
                    await ExecutarAsync(_conexao, _transacao, """
                        INSERT INTO membros_duplicatas (execucao_id, ordem_grupo, caminho)
                        VALUES ($id, $ordem, $caminho);
                        """, cancellationToken, ("$id", _id),
                        ("$ordem", ordem), ("$caminho", membro.CaminhoCompleto))
                        .ConfigureAwait(false);
                }
            }
        }

        foreach (var ocorrencia in inventario.Ocorrencias)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await ExecutarAsync(_conexao, _transacao, """
                INSERT INTO ocorrencias (execucao_id, origem, tipo, caminho, descricao)
                VALUES ($id, 'Inventario', $tipo, $caminho, $desc);
                """, cancellationToken, ("$id", _id), ("$tipo", ocorrencia.Tipo),
                ("$caminho", ocorrencia.Caminho), ("$desc", ocorrencia.Descricao))
                .ConfigureAwait(false);
        }
        if (duplicatas is not null)
            foreach (var ocorrencia in duplicatas.Ocorrencias)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await ExecutarAsync(_conexao, _transacao, """
                    INSERT INTO ocorrencias (execucao_id, origem, tipo, caminho, descricao)
                    VALUES ($id, 'Duplicatas', $tipo, $caminho, $desc);
                    """, cancellationToken, ("$id", _id), ("$tipo", ocorrencia.Tipo),
                    ("$caminho", ocorrencia.Caminho), ("$desc", ocorrencia.Descricao))
                    .ConfigureAwait(false);
            }

        var totalOcorrencias = checked(inventario.TotalOcorrencias +
            (duplicatas?.TotalOcorrencias ?? 0L));
        var resumo = new
        {
            PreservacaoAcesso,
            Versao = "CLEAR-M6-1", DiretorioRaiz = _diretorioRaiz, InicioUtc = _inicioUtc,
            inventario.QuantidadeArquivos, inventario.QuantidadeDiretorios,
            inventario.TotalBytes, inventario.ArquivosComIndicios,
            inventario.ItensComIncompatibilidade,
            inventario.ArquivosAcimaLimitePersonalizado,
            InventarioParcial = inventario.Incompleto,
            DuplicatasExecutadas = duplicatas is not null,
            DuplicatasParciais = duplicatas?.Incompleto,
            GruposDuplicatas = duplicatas?.Grupos.Count ?? 0,
            ArquivosEmGrupos = duplicatas?.ArquivosEmGrupos ?? 0,
            EntradasPersistidas = _entradasPersistidas,
            TotalOcorrencias = totalOcorrencias
        };
        var estado = totalOcorrencias > 0 ? "Parcial" : "Concluida";
        await ExecutarAsync(_conexao, _transacao, """
            UPDATE execucoes
            SET fim_utc = $fim, estado = $estado, resumo_json = $resumo,
                arquivos = $arquivos, diretorios = $diretorios, bytes = $bytes,
                entradas_persistidas = $entradas, grupos_duplicatas = $grupos,
                total_ocorrencias = $ocorrencias
            WHERE id = $id;
            """, cancellationToken,
            ("$fim", DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture)),
            ("$estado", estado),
            ("$resumo", JsonSerializer.Serialize(resumo)),
            ("$arquivos", inventario.QuantidadeArquivos),
            ("$diretorios", inventario.QuantidadeDiretorios),
            ("$bytes", inventario.TotalBytes),
            ("$entradas", _entradasPersistidas),
            ("$grupos", duplicatas?.Grupos.Count ?? 0),
            ("$ocorrencias", totalOcorrencias),
            ("$id", _id)).ConfigureAwait(false);

        cancellationToken.ThrowIfCancellationRequested();
        await _transacao.CommitAsync(cancellationToken).ConfigureAwait(false);
        _finalizada = true;
    }

    public async ValueTask DisposeAsync()
    {
        // Descarte antes de ConcluirAsync = rollback integral dos registros desta execução.
        await _transacao.DisposeAsync().ConfigureAwait(false);
        await _conexao.DisposeAsync().ConfigureAwait(false);
    }

    /// <summary>Registra a tentativa sem publicar as entradas incompletas.</summary>
    public async Task RegistrarInterrupcaoAsync(bool cancelada, string motivo)
    {
        if (_finalizada) return;
        await ExecutarAsync(_conexao, _transacao,
            "DELETE FROM entradas WHERE execucao_id = $id;", CancellationToken.None,
            ("$id", _id)).ConfigureAwait(false);
        await ExecutarAsync(_conexao, _transacao,
            "DELETE FROM grupos_duplicatas WHERE execucao_id = $id;", CancellationToken.None,
            ("$id", _id)).ConfigureAwait(false);
        await ExecutarAsync(_conexao, _transacao,
            "DELETE FROM membros_duplicatas WHERE execucao_id = $id;", CancellationToken.None,
            ("$id", _id)).ConfigureAwait(false);
        await ExecutarAsync(_conexao, _transacao,
            "DELETE FROM ocorrencias WHERE execucao_id = $id;", CancellationToken.None,
            ("$id", _id)).ConfigureAwait(false);
        await ExecutarAsync(_conexao, _transacao, """
            UPDATE execucoes SET fim_utc = $fim, estado = $estado, resumo_json = $resumo,
                arquivos = 0, diretorios = 0, bytes = 0, entradas_persistidas = 0,
                grupos_duplicatas = 0, total_ocorrencias = 0 WHERE id = $id;
            """, CancellationToken.None, ("$id", _id),
            ("$fim", DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture)),
            ("$estado", cancelada ? "Cancelada" : "Falhou"),
            ("$resumo", JsonSerializer.Serialize(new { Motivo = motivo }))).ConfigureAwait(false);
        await _transacao.CommitAsync(CancellationToken.None).ConfigureAwait(false);
        _finalizada = true;
    }

    public static async Task<IReadOnlyList<ResumoExecucaoHistoricaM6>> ConsultarUltimasAsync(
        string caminhoBanco, int limite = 30, CancellationToken cancellationToken = default,
        bool incluirInterrompidas = false, bool incluirTodas = false)
    {
        if (limite is < 1 or > 1000)
            throw new ArgumentOutOfRangeException(nameof(limite));
        if (!File.Exists(caminhoBanco))
            return Array.Empty<ResumoExecucaoHistoricaM6>();

        await using var conexao = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = Path.GetFullPath(caminhoBanco),
            Mode = SqliteOpenMode.ReadOnly, Pooling = false
        }.ToString());
        await conexao.OpenAsync(cancellationToken).ConfigureAwait(false);
        var versao = await LerVersaoAsync(conexao, cancellationToken).ConfigureAwait(false);
        if (versao != VersaoEsquema)
            throw new InvalidDataException($"Histórico SQLite com versão não suportada: {versao}.");

        await using var comando = conexao.CreateCommand();
        comando.CommandText = """
            SELECT id, inicio_utc, fim_utc, pasta_raiz, estado, arquivos,
                   diretorios, bytes, entradas_persistidas, grupos_duplicatas, total_ocorrencias,
                   parametros_json, resumo_json, rowid
            FROM execucoes
            WHERE estado IN ('Concluida', 'Parcial')
                OR ($interrompidas = 1 AND estado IN ('Cancelada', 'Falhou'))
            ORDER BY inicio_utc DESC, id DESC
            LIMIT $limite;
            """;
        comando.Parameters.AddWithValue("$limite", incluirTodas ? -1 : limite);
        comando.Parameters.AddWithValue("$interrompidas", incluirInterrompidas ? 1 : 0);
        var result = new List<ResumoExecucaoHistoricaM6>();
        await using var leitor = await comando.ExecuteReaderAsync(cancellationToken)
            .ConfigureAwait(false);
        while (await leitor.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            result.Add(new ResumoExecucaoHistoricaM6(
                leitor.GetString(0),
                DateTimeOffset.Parse(leitor.GetString(1), CultureInfo.InvariantCulture),
                DateTimeOffset.Parse(leitor.GetString(2), CultureInfo.InvariantCulture),
                leitor.GetString(3), leitor.GetString(4),
                leitor.GetInt64(5), leitor.GetInt64(6), leitor.GetInt64(7),
                leitor.GetInt64(8), leitor.GetInt64(9), leitor.GetInt64(10),
                leitor.GetString(11), leitor.IsDBNull(12) ? null : leitor.GetString(12),
                leitor.GetInt64(13)));
        }
        return result.AsReadOnly();
    }

    private static async Task PrepararEsquemaAsync(SqliteConnection conexao,
        CancellationToken cancellationToken)
    {
        await using var pragma = conexao.CreateCommand();
        pragma.CommandText = "PRAGMA foreign_keys = ON; PRAGMA busy_timeout = 5000;";
        await pragma.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        var versao = await LerVersaoAsync(conexao, cancellationToken).ConfigureAwait(false);
        if (versao != 0 && versao != VersaoEsquema)
            throw new InvalidDataException($"Versão do banco não suportada: {versao}.");

        if (versao == 0)
        {
            // Não anexar as tabelas CLEAR a um SQLite não reconhecido.
            await using var detectar = conexao.CreateCommand();
            detectar.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%';";
            var tabelas = Convert.ToInt64(await detectar.ExecuteScalarAsync(cancellationToken),
                CultureInfo.InvariantCulture);
            if (tabelas != 0)
                throw new InvalidDataException("Banco preexistente não pertence ao CLEAR.");

            await using var migracao = (SqliteTransaction)await conexao
                .BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            await using var criar = conexao.CreateCommand();
            criar.Transaction = migracao;
            criar.CommandText = """
                CREATE TABLE execucoes (
                    id TEXT PRIMARY KEY,
                    inicio_utc TEXT NOT NULL,
                    fim_utc TEXT,
                    pasta_raiz TEXT NOT NULL,
                    parametros_json TEXT NOT NULL,
                    estado TEXT NOT NULL DEFAULT 'EmAndamento',
                    resumo_json TEXT,
                    arquivos INTEGER NOT NULL DEFAULT 0,
                    diretorios INTEGER NOT NULL DEFAULT 0,
                    bytes INTEGER NOT NULL DEFAULT 0,
                    entradas_persistidas INTEGER NOT NULL DEFAULT 0,
                    grupos_duplicatas INTEGER NOT NULL DEFAULT 0,
                    total_ocorrencias INTEGER NOT NULL DEFAULT 0
                );
                CREATE TABLE entradas (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    execucao_id TEXT NOT NULL REFERENCES execucoes(id) ON DELETE CASCADE,
                    tipo TEXT NOT NULL,
                    caminho TEXT NOT NULL,
                    tamanho_bytes INTEGER,
                    modificado_utc TEXT,
                    acessado_utc TEXT,
                    categorias_json TEXT NOT NULL,
                    evidencias_json TEXT NOT NULL,
                    verificacoes_json TEXT NOT NULL,
                    acima_limite_pessoal INTEGER NOT NULL
                );
                CREATE TABLE grupos_duplicatas (
                    execucao_id TEXT NOT NULL REFERENCES execucoes(id) ON DELETE CASCADE,
                    ordem INTEGER NOT NULL,
                    tamanho_bytes INTEGER NOT NULL,
                    sha256 TEXT NOT NULL,
                    bytes_potenciais INTEGER NOT NULL,
                    PRIMARY KEY (execucao_id, ordem)
                );
                CREATE TABLE membros_duplicatas (
                    execucao_id TEXT NOT NULL,
                    ordem_grupo INTEGER NOT NULL,
                    caminho TEXT NOT NULL,
                    FOREIGN KEY (execucao_id, ordem_grupo)
                        REFERENCES grupos_duplicatas(execucao_id, ordem) ON DELETE CASCADE
                );
                CREATE TABLE ocorrencias (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    execucao_id TEXT NOT NULL REFERENCES execucoes(id) ON DELETE CASCADE,
                    origem TEXT NOT NULL,
                    tipo TEXT NOT NULL,
                    caminho TEXT NOT NULL,
                    descricao TEXT NOT NULL
                );
                CREATE INDEX idx_entradas_execucao ON entradas(execucao_id);
                CREATE INDEX idx_entradas_caminho ON entradas(execucao_id, caminho);
                CREATE INDEX idx_membros_execucao ON membros_duplicatas(execucao_id, ordem_grupo);
                CREATE INDEX idx_ocorrencias_execucao ON ocorrencias(execucao_id);
                PRAGMA user_version = 1;
                """;
            await criar.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            await migracao.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task<long> LerVersaoAsync(SqliteConnection conexao,
        CancellationToken cancellationToken)
    {
        await using var comando = conexao.CreateCommand();
        comando.CommandText = "PRAGMA user_version;";
        return Convert.ToInt64(await comando.ExecuteScalarAsync(cancellationToken),
            CultureInfo.InvariantCulture);
    }

    private static async Task ExecutarAsync(SqliteConnection conexao, SqliteTransaction transacao,
        string sql, CancellationToken cancellationToken, params (string nome, object? valor)[] parametros)
    {
        await using var comando = conexao.CreateCommand();
        comando.Transaction = transacao;
        comando.CommandText = sql;
        foreach (var (nome, valor) in parametros)
            comando.Parameters.AddWithValue(nome, valor ?? DBNull.Value);
        await comando.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}
