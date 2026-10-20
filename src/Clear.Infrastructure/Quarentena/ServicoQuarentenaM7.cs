using System.Globalization;
using System.Security.Cryptography;
using Clear.Infrastructure.Historico;
using Microsoft.Data.Sqlite;

namespace Clear.Infrastructure.Quarentena;

/// <summary>
/// Plano somente leitura. A intenção precisa ser confirmada explicitamente
/// antes de qualquer movimentação; resultados antigos não são autorização.
/// </summary>
public sealed record PlanoQuarentenaM7(
    string IdOperacao, string IdExecucao, long IdEntrada, string RaizAnalisada,
    string CaminhoOriginal, string CaminhoDestino, long TamanhoBytes,
    DateTimeOffset ModificadoUtc, string Sha256Hex);

public sealed record RegistroQuarentenaM7(
    string IdOperacao, string Estado, string CaminhoOriginal,
    string CaminhoDestino, long TamanhoBytes, string Sha256Hex,
    string IdExecucao, string? Mensagem);

/// <summary>
/// Movimentação individual para quarentena com aprovação humana explícita,
/// pré-verificação de metadados/SHA-256 e auditoria persistente. Não exclui.
/// Restauração e reconciliação de operações pendentes pertencem ao M8.
/// </summary>
public static class ServicoQuarentenaM7
{
    public const int VersaoEsquema = 1;

    public static string RaizPadrao => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "CLEAR", "Quarentena");

    public static string BancoAuditoriaPadrao => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "CLEAR", "quarentena.sqlite3");

    public static async Task<PlanoQuarentenaM7> PrepararAsync(
        EntradaHistoricaM6 entrada, ResumoExecucaoHistoricaM6 execucao,
        string raizQuarentena, CancellationToken cancelamento = default)
    {
        ArgumentNullException.ThrowIfNull(entrada);
        ArgumentNullException.ThrowIfNull(execucao);
        if (entrada.Tipo != "Arquivo" || entrada.TamanhoBytes is null ||
            string.IsNullOrWhiteSpace(entrada.ModificadoUtc))
            throw new InvalidOperationException("Somente arquivos com metadados completos podem ser movimentados.");
        if (execucao.Estado != "Concluida")
            throw new InvalidOperationException("Não é permitido movimentar itens de análises parciais.");
        if (string.IsNullOrWhiteSpace(raizQuarentena))
            throw new ArgumentException("A pasta de quarentena é obrigatória.", nameof(raizQuarentena));

        var origem = Path.GetFullPath(entrada.Caminho);
        var raiz = Path.GetFullPath(execucao.DiretorioRaiz);
        var destinoRaiz = Path.GetFullPath(raizQuarentena);
        if (!EstaDentro(origem, raiz) || string.Equals(origem, raiz, StringComparison.OrdinalIgnoreCase))
            throw new IOException("O arquivo não pertence à raiz da análise selecionada.");
        if (EstaDentro(destinoRaiz, raiz) || EstaDentro(origem, destinoRaiz))
            throw new IOException("A quarentena e o acervo não podem conter um ao outro.");
        if (!string.Equals(Path.GetPathRoot(origem), Path.GetPathRoot(destinoRaiz),
                StringComparison.OrdinalIgnoreCase))
            throw new IOException("A quarentena deve estar no mesmo volume do arquivo (movimentação local).");

        VerificarCaminhoSeguro(origem, arquivo: true);
        VerificarCaminhoSeguro(destinoRaiz, arquivo: false);
        if (!DateTimeOffset.TryParse(entrada.ModificadoUtc, CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind, out var modificado))
            throw new InvalidDataException("Data do inventário não pôde ser interpretada.");

        var esperado = entrada.TamanhoBytes.Value;
        var sha = await CalcularShaEstavelAsync(origem, esperado, modificado, cancelamento)
            .ConfigureAwait(false);
        var id = Guid.NewGuid().ToString("N");
        var nome = Path.GetFileName(origem);
        if (string.IsNullOrEmpty(nome))
            throw new IOException("Nome de arquivo inválido.");
        // Cada operação é isolada por GUID, MAS mantém toda a estrutura
        // relativa do acervo abaixo dessa pasta.
        var relativo = Path.GetRelativePath(raiz, origem);
        var caminhoDestino = Path.Combine(destinoRaiz, id, relativo);
        return new PlanoQuarentenaM7(id, execucao.Id, entrada.Id, raiz,
            origem, caminhoDestino, esperado, modificado.ToUniversalTime(), sha);
    }

    /// <summary>
    /// Só chama File.Move após confirmação recebida da interface.
    /// Cria registro Pendente antes de mover, para auditoria de falha/crash.
    /// </summary>
    public static async Task<RegistroQuarentenaM7> ExecutarAsync(
        PlanoQuarentenaM7 plano, bool confirmacaoHumanaExplicita,
        string bancoAuditoria, CancellationToken cancelamento = default)
    {
        ArgumentNullException.ThrowIfNull(plano);
        if (!Guid.TryParseExact(plano.IdOperacao, "N", out _))
            throw new IOException("ID da operação de quarentena inválido.");
        if (!confirmacaoHumanaExplicita)
            throw new OperationCanceledException("O usuário não confirmou a movimentação.");
        if (string.IsNullOrWhiteSpace(bancoAuditoria))
            throw new ArgumentException("Banco de auditoria obrigatório.", nameof(bancoAuditoria));
        cancelamento.ThrowIfCancellationRequested();

        // Recupera a pasta de operação pela hierarquia relativa planejada.
        // Não confia em planos manipulados pelo chamador.
        var relativo = Path.GetRelativePath(plano.RaizAnalisada, plano.CaminhoOriginal);
        if (!EstaDentro(plano.CaminhoOriginal, plano.RaizAnalisada))
            throw new IOException("Arquivo fora da raiz original.");
        var pastaOperacao = plano.CaminhoDestino;
        foreach (var _ in relativo.Split(Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar))
            pastaOperacao = Path.GetDirectoryName(pastaOperacao)
                ?? throw new IOException("Destino da quarentena inválido.");
        if (!string.Equals(Path.GetFileName(pastaOperacao), plano.IdOperacao,
                StringComparison.OrdinalIgnoreCase)
            || !string.Equals(Path.GetFullPath(Path.Combine(pastaOperacao, relativo)),
                Path.GetFullPath(plano.CaminhoDestino), StringComparison.OrdinalIgnoreCase))
            throw new IOException("O plano da quarentena não preserva o caminho relativo.");
        var raizDestino = Path.GetDirectoryName(pastaOperacao)
            ?? throw new IOException("Raiz da quarentena inválida.");
        var banco = Path.GetFullPath(bancoAuditoria);
        var pastaBanco = Path.GetDirectoryName(banco)
            ?? throw new IOException("Caminho SQLite inválido.");
        if (EstaDentro(banco, plano.RaizAnalisada) || EstaDentro(banco, raizDestino))
            throw new IOException("Banco de auditoria deve ficar fora do acervo e da quarentena.");
        VerificarCaminhoSeguro(pastaBanco, arquivo: false);
        VerificarCaminhoSeguro(raizDestino, arquivo: false);
        if (File.Exists(banco) &&
            (File.GetAttributes(banco) & FileAttributes.ReparsePoint) != 0)
            throw new IOException("O banco SQLite de auditoria não pode ser link simbólico.");
        if (!string.Equals(Path.GetPathRoot(plano.CaminhoOriginal),
                Path.GetPathRoot(plano.CaminhoDestino), StringComparison.OrdinalIgnoreCase))
            throw new IOException("Movimentação entre volumes é proibida.");
        if (File.Exists(plano.CaminhoDestino))
            throw new IOException("O destino já existe.");

        // Não usar os metadados históricos para agir sem revalidar o arquivo.
        VerificarCaminhoSeguro(plano.CaminhoOriginal, arquivo: true);
        var shaAtual = await CalcularShaEstavelAsync(plano.CaminhoOriginal,
            plano.TamanhoBytes, plano.ModificadoUtc, cancelamento).ConfigureAwait(false);
        if (!string.Equals(shaAtual, plano.Sha256Hex, StringComparison.Ordinal))
            throw new IOException("Conteúdo mudou após a preparação; nova revisão humana necessária.");
        cancelamento.ThrowIfCancellationRequested();

        Directory.CreateDirectory(pastaBanco);
        await using var conexao = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = banco, Mode = SqliteOpenMode.ReadWriteCreate, Pooling = false
        }.ToString());
        await conexao.OpenAsync(cancelamento).ConfigureAwait(false);
        await PrepararBancoAsync(conexao, cancelamento).ConfigureAwait(false);

        await ExecutarSqlAsync(conexao, """
            INSERT INTO movimentacoes (
                id, execucao_id, entrada_id, original, destino, tamanho, sha256,
                estado, criado_utc, mensagem
            ) VALUES (
                $id, $exec, $entrada, $original, $destino, $tamanho, $sha,
                'Pendente', $utc, NULL
            );
            """, cancelamento,
            ("$id", plano.IdOperacao), ("$exec", plano.IdExecucao),
            ("$entrada", plano.IdEntrada), ("$original", plano.CaminhoOriginal),
            ("$destino", plano.CaminhoDestino), ("$tamanho", plano.TamanhoBytes),
            ("$sha", plano.Sha256Hex),
            ("$utc", DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture)))
            .ConfigureAwait(false);

        // A partir daqui, não aceitar cancelamento que deixe transferência
        // terminada e auditoria não atualizada por escolha do usuário.
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(plano.CaminhoDestino)!);
            VerificarCaminhoSeguro(plano.CaminhoOriginal, arquivo: true);
            VerificarCaminhoSeguro(Path.GetDirectoryName(plano.CaminhoDestino)!, arquivo: false);
            if (File.Exists(plano.CaminhoDestino))
                throw new IOException("Destino ocupado antes da movimentação.");
            if (!string.Equals(
                await CalcularShaEstavelAsync(plano.CaminhoOriginal,
                    plano.TamanhoBytes, plano.ModificadoUtc, CancellationToken.None)
                    .ConfigureAwait(false),
                plano.Sha256Hex, StringComparison.Ordinal))
                throw new IOException("Arquivo alterado antes da movimentação.");

            // Mesmo volume e overwrite=false: não sobrescreve arquivos.
            File.Move(plano.CaminhoOriginal, plano.CaminhoDestino, overwrite: false);
            await AtualizarEstadoAsync(conexao, plano.IdOperacao, "EmQuarentena", null)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // Se falhar após File.Move, conserva Pendente para reconciliação M8.
            if (File.Exists(plano.CaminhoOriginal) && !File.Exists(plano.CaminhoDestino))
                await AtualizarEstadoAsync(conexao, plano.IdOperacao, "Falha", ex.Message)
                    .ConfigureAwait(false);
            throw;
        }

        return new RegistroQuarentenaM7(plano.IdOperacao, "EmQuarentena",
            plano.CaminhoOriginal, plano.CaminhoDestino, plano.TamanhoBytes,
            plano.Sha256Hex, plano.IdExecucao, null);
    }

    public static async Task<IReadOnlyList<RegistroQuarentenaM7>> ConsultarAsync(
        string bancoAuditoria, string? idExecucao = null,
        CancellationToken cancelamento = default)
    {
        if (!File.Exists(bancoAuditoria))
            return Array.Empty<RegistroQuarentenaM7>();
        await using var conexao = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = Path.GetFullPath(bancoAuditoria),
            Mode = SqliteOpenMode.ReadOnly, Pooling = false
        }.ToString());
        await conexao.OpenAsync(cancelamento).ConfigureAwait(false);
        await ValidarVersaoAsync(conexao, cancelamento).ConfigureAwait(false);
        await using var comando = conexao.CreateCommand();
        comando.CommandText = """
            SELECT id, estado, original, destino, tamanho, sha256, execucao_id, mensagem
            FROM movimentacoes
            WHERE ($execucao IS NULL OR execucao_id = $execucao)
            ORDER BY criado_utc DESC LIMIT 100;
            """;
        comando.Parameters.AddWithValue("$execucao", (object?)idExecucao ?? DBNull.Value);
        var itens = new List<RegistroQuarentenaM7>();
        await using var leitor = await comando.ExecuteReaderAsync(cancelamento).ConfigureAwait(false);
        while (await leitor.ReadAsync(cancelamento).ConfigureAwait(false))
            itens.Add(new RegistroQuarentenaM7(
                leitor.GetString(0), leitor.GetString(1), leitor.GetString(2),
                leitor.GetString(3), leitor.GetInt64(4), leitor.GetString(5),
                leitor.GetString(6), leitor.IsDBNull(7) ? null : leitor.GetString(7)));
        return itens.AsReadOnly();
    }

    private static async Task<string> CalcularShaEstavelAsync(
        string caminho, long esperado, DateTimeOffset modificado,
        CancellationToken cancelamento)
    {
        VerificarMetadados(caminho, esperado, modificado);
        // Permitir renomeação no mesmo volume enquanto handle está aberto,
        // mas bloquear escrita durante SHA. O movimento não é prova de cópia
        // para outra unidade e não substitui avaliação de permissões/hardlinks.
        await using var fluxo = new FileStream(caminho, new FileStreamOptions
        {
            Mode = FileMode.Open, Access = FileAccess.Read,
            Share = FileShare.Read | FileShare.Delete,
            BufferSize = 65536, Options = FileOptions.Asynchronous | FileOptions.SequentialScan
        });
        if (fluxo.Length != esperado)
            throw new IOException("O tamanho do arquivo mudou desde a análise.");
        var bytes = await SHA256.HashDataAsync(fluxo, cancelamento).ConfigureAwait(false);
        VerificarMetadados(caminho, esperado, modificado);
        return Convert.ToHexString(bytes);
    }

    private static void VerificarMetadados(
        string caminho, long esperado, DateTimeOffset modificado)
    {
        VerificarCaminhoSeguro(caminho, arquivo: true);
        var info = new FileInfo(caminho);
        info.Refresh();
        if (info.Length != esperado || info.LastWriteTimeUtc != modificado.UtcDateTime)
            throw new IOException("Arquivo modificado desde a análise; reanalise antes de prosseguir.");
    }

    private static bool EstaDentro(string caminho, string raiz)
    {
        var relativo = Path.GetRelativePath(raiz, caminho);
        return !Path.IsPathRooted(relativo)
            && !string.Equals(relativo, "..", StringComparison.Ordinal)
            && !relativo.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal);
    }

    private static void VerificarCaminhoSeguro(string caminho, bool arquivo)
    {
        var completo = Path.GetFullPath(caminho);
        if (arquivo)
        {
            var fi = new FileInfo(completo);
            if (!fi.Exists || (fi.Attributes & (FileAttributes.ReparsePoint |
                    FileAttributes.Directory)) != 0)
                throw new IOException("O arquivo não existe ou é link/reparse point.");
        }
        for (DirectoryInfo? dir = new(arquivo
                ? Path.GetDirectoryName(completo)! : completo);
             dir is not null; dir = dir.Parent)
        {
            if (dir.Exists && (dir.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Não operar por caminho contendo link/junction.");
        }
    }

    private static async Task PrepararBancoAsync(
        SqliteConnection conexao, CancellationToken cancelamento)
    {
        await using var cmd = conexao.CreateCommand();
        cmd.CommandText = "PRAGMA user_version;";
        var versao = Convert.ToInt32(await cmd.ExecuteScalarAsync(cancelamento)
            .ConfigureAwait(false), CultureInfo.InvariantCulture);
        if (versao != 0 && versao != VersaoEsquema)
            throw new InvalidDataException("Versão de auditoria SQLite não suportada.");
        if (versao == 0)
        {
            await using var detectar = conexao.CreateCommand();
            detectar.CommandText =
                "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%';";
            if (Convert.ToInt64(await detectar.ExecuteScalarAsync(cancelamento)
                .ConfigureAwait(false), CultureInfo.InvariantCulture) != 0)
                throw new InvalidDataException("Banco SQLite existente não é banco de auditoria CLEAR.");
            await using var schemaTx = (SqliteTransaction)await conexao
                .BeginTransactionAsync(cancelamento).ConfigureAwait(false);
            await using var criar = conexao.CreateCommand();
            criar.Transaction = schemaTx;
            criar.CommandText = """
                CREATE TABLE movimentacoes (
                    id TEXT PRIMARY KEY, execucao_id TEXT NOT NULL,
                    entrada_id INTEGER NOT NULL, original TEXT NOT NULL,
                    destino TEXT NOT NULL, tamanho INTEGER NOT NULL,
                    sha256 TEXT NOT NULL, estado TEXT NOT NULL,
                    criado_utc TEXT NOT NULL, mensagem TEXT
                );
                CREATE INDEX idx_movimentacoes_data ON movimentacoes(criado_utc);
                PRAGMA user_version = 1;
                """;
            await criar.ExecuteNonQueryAsync(cancelamento).ConfigureAwait(false);
            await schemaTx.CommitAsync(cancelamento).ConfigureAwait(false);
        }
    }

    private static async Task ValidarVersaoAsync(
        SqliteConnection conexao, CancellationToken cancelamento)
    {
        await using var cmd = conexao.CreateCommand();
        cmd.CommandText = "PRAGMA user_version;";
        var versao = Convert.ToInt32(await cmd.ExecuteScalarAsync(cancelamento)
            .ConfigureAwait(false), CultureInfo.InvariantCulture);
        if (versao != VersaoEsquema)
            throw new InvalidDataException("Versão da auditoria SQLite não suportada.");
    }

    private static async Task AtualizarEstadoAsync(
        SqliteConnection conexao, string id, string estado, string? mensagem)
    {
        await ExecutarSqlAsync(conexao, """
            UPDATE movimentacoes SET estado = $estado, mensagem = $mensagem WHERE id = $id;
            """, CancellationToken.None,
            ("$estado", estado), ("$mensagem", mensagem), ("$id", id)).ConfigureAwait(false);
    }

    private static async Task ExecutarSqlAsync(SqliteConnection conexao,
        string sql, CancellationToken cancelamento, params (string nome, object? valor)[] parametros)
    {
        await using var cmd = conexao.CreateCommand();
        cmd.CommandText = sql;
        foreach (var (nome, valor) in parametros)
            cmd.Parameters.AddWithValue(nome, valor ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync(cancelamento).ConfigureAwait(false);
    }
}
