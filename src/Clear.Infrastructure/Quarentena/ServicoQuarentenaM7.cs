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
    DateTimeOffset ModificadoUtc, string Sha256Hex,
    string RaizQuarentena = "");

public sealed record RegistroQuarentenaM7(
    string IdOperacao, string Estado, string CaminhoOriginal,
    string CaminhoDestino, long TamanhoBytes, string Sha256Hex,
    string IdExecucao, string? Mensagem, string? CriadoUtc = null,
    string? RaizAnalisada = null, string Estrutura = "Legado");

/// <summary>
/// Movimentação individual para quarentena com aprovação humana explícita,
/// pré-verificação de metadados/SHA-256 e auditoria persistente. Não exclui.
/// Restauração e reconciliação de operações pendentes pertencem ao M8.
/// </summary>
public static class ServicoQuarentenaM7
{
    public const int VersaoEsquema = 2;

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
        // Diretórios compartilhados pela mesma raiz: Quarentena / NomeDaBase /
        // caminho relativo completo. O GUID identifica a auditoria, não um diretório.
        var relativo = Path.GetRelativePath(raiz, origem);
        var nomeRaiz = Path.GetFileName(Path.TrimEndingDirectorySeparator(raiz));
        if (string.IsNullOrWhiteSpace(nomeRaiz))
            throw new IOException("Selecione uma pasta raiz nomeada; a raiz do volume não é suportada.");
        var caminhoDestino = Path.Combine(destinoRaiz, nomeRaiz, relativo);
        return new PlanoQuarentenaM7(id, execucao.Id, entrada.Id, raiz,
            origem, caminhoDestino, esperado, modificado.ToUniversalTime(), sha, destinoRaiz);
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

        var relativo = Path.GetRelativePath(plano.RaizAnalisada, plano.CaminhoOriginal);
        if (!EstaDentro(plano.CaminhoOriginal, plano.RaizAnalisada) ||
            string.IsNullOrWhiteSpace(plano.RaizQuarentena))
            throw new IOException("Plano da quarentena sem raiz segura.");
        var raizDestino = Path.GetFullPath(plano.RaizQuarentena);
        var nomeRaiz = Path.GetFileName(
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(plano.RaizAnalisada)));
        if (string.IsNullOrWhiteSpace(nomeRaiz))
            throw new IOException("Raiz analisada inválida para quarentena.");
        var destinoEsperado = Path.GetFullPath(Path.Combine(raizDestino, nomeRaiz, relativo));
        if (!string.Equals(destinoEsperado, Path.GetFullPath(plano.CaminhoDestino),
                StringComparison.OrdinalIgnoreCase))
            throw new IOException("Plano adulterado: a estrutura de quarentena não corresponde à origem.");
        if (EstaDentro(raizDestino, plano.RaizAnalisada) ||
            EstaDentro(plano.CaminhoOriginal, raizDestino))
            throw new IOException("A quarentena e o acervo não podem conter um ao outro.");
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

        // Evita misturar duas raízes diferentes que tenham o mesmo nome
        // na mesma árvore compartilhada da quarentena.
        await using (var verificarDono = conexao.CreateCommand())
        {
            verificarDono.CommandText = """
                SELECT COUNT(*) FROM movimentacoes
                WHERE estrutura = 'Compartilhada'
                  AND lower(raiz_analisada) <> lower($raiz)
                  AND lower(substr(destino, 1, length($prefixo))) = lower($prefixo)
                  AND estado IN ('Pendente', 'EmQuarentena', 'RestauracaoPendente');
                """;
            verificarDono.Parameters.AddWithValue("$raiz", plano.RaizAnalisada);
            verificarDono.Parameters.AddWithValue("$prefixo",
                Path.Combine(raizDestino, nomeRaiz) + Path.DirectorySeparatorChar);
            var conflitos = Convert.ToInt64(
                await verificarDono.ExecuteScalarAsync(cancelamento).ConfigureAwait(false),
                CultureInfo.InvariantCulture);
            if (conflitos > 0)
                throw new IOException("Esta quarentena já possui outra pasta raiz " +
                    "com o mesmo nome. Escolha uma pasta de quarentena diferente.");
        }

        await ExecutarSqlAsync(conexao, """
            INSERT INTO movimentacoes (
                id, execucao_id, entrada_id, original, destino, tamanho, sha256,
                estado, criado_utc, mensagem, raiz_analisada, estrutura
            ) VALUES (
                $id, $exec, $entrada, $original, $destino, $tamanho, $sha,
                'Pendente', $utc, NULL, $raiz, 'Compartilhada'
            );
            """, cancelamento,
            ("$id", plano.IdOperacao), ("$exec", plano.IdExecucao),
            ("$entrada", plano.IdEntrada), ("$original", plano.CaminhoOriginal),
            ("$destino", plano.CaminhoDestino), ("$tamanho", plano.TamanhoBytes),
            ("$sha", plano.Sha256Hex),
            ("$raiz", plano.RaizAnalisada),
            ("$utc", DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture)))
            .ConfigureAwait(false);

        // A partir daqui, não aceitar cancelamento que deixe transferência
        // terminada e auditoria não atualizada por escolha do usuário.
        try
        {
            if (!OperatingSystem.IsWindows())
                throw new PlatformNotSupportedException("A quarentena com preservação de segurança requer Windows.");
            var permissoes = PreservacaoPermissoesQuarentena.Preparar(plano);
            var auditoriaPermissoes = "SegurancaNTFS-v1; DACL/proprietario/grupo; SHA256=" + permissoes.ImpressaoArquivo;
            await AtualizarEstadoAsync(conexao, plano.IdOperacao, "Pendente", auditoriaPermissoes)
                .ConfigureAwait(false);
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
            permissoes.ValidarAntesDaMovimentacao();
            File.Move(plano.CaminhoOriginal, plano.CaminhoDestino, overwrite: false);
            permissoes.ValidarDepoisDaMovimentacao();
            await AtualizarEstadoAsync(conexao, plano.IdOperacao, "EmQuarentena", auditoriaPermissoes)
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
        CancellationToken cancelamento = default, int pagina = 0)
    {
        if (pagina < 0) throw new ArgumentOutOfRangeException(nameof(pagina));
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
            SELECT id, estado, original, destino, tamanho, sha256, execucao_id, mensagem, criado_utc
            FROM movimentacoes
            WHERE ($execucao IS NULL OR execucao_id = $execucao)
            ORDER BY criado_utc DESC, id LIMIT 200 OFFSET $offset;
            """;
        comando.Parameters.AddWithValue("$execucao", (object?)idExecucao ?? DBNull.Value);
        comando.Parameters.AddWithValue("$offset", checked((long)pagina * 200));
        var itens = new List<RegistroQuarentenaM7>();
        await using var leitor = await comando.ExecuteReaderAsync(cancelamento).ConfigureAwait(false);
        while (await leitor.ReadAsync(cancelamento).ConfigureAwait(false))
            itens.Add(new RegistroQuarentenaM7(
                leitor.GetString(0), leitor.GetString(1), leitor.GetString(2),
                leitor.GetString(3), leitor.GetInt64(4), leitor.GetString(5),
                leitor.GetString(6), leitor.IsDBNull(7) ? null : leitor.GetString(7),
                leitor.GetString(8)));
        return itens.AsReadOnly();
    }

    public static async Task<long> ContarAuditoriaAsync(string bancoAuditoria,
        string idExecucao, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(idExecucao)) throw new ArgumentException("Selecione uma execução.");
        if (!File.Exists(bancoAuditoria)) return 0;
        await using var conexao = new SqliteConnection(new SqliteConnectionStringBuilder
        { DataSource = Path.GetFullPath(bancoAuditoria), Mode = SqliteOpenMode.ReadOnly,
            Pooling = false }.ToString());
        await conexao.OpenAsync(ct).ConfigureAwait(false);
        await ValidarVersaoAsync(conexao, ct).ConfigureAwait(false);
        await using var comando = conexao.CreateCommand();
        comando.CommandText = "SELECT COUNT(*) FROM movimentacoes WHERE execucao_id = $exec";
        comando.Parameters.AddWithValue("$exec", idExecucao);
        return Convert.ToInt64(await comando.ExecuteScalarAsync(ct).ConfigureAwait(false),
            CultureInfo.InvariantCulture);
    }

    /// <summary>Exporta toda a auditoria da execução em fluxo, sem o limite da página visual.</summary>
    public static async Task<long> ExportarAuditoriaCsvAsync(string bancoAuditoria,
        string idExecucao, string destino, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(idExecucao)) throw new ArgumentException("Selecione uma execução.");
        ct.ThrowIfCancellationRequested();
        var final = Path.GetFullPath(destino);
        if (File.Exists(final)) throw new IOException("O arquivo de destino já existe.");
        var temporario = final + "." + Guid.NewGuid().ToString("N") + ".part";
        try
        {
            await using var conexao = new SqliteConnection(new SqliteConnectionStringBuilder
            { DataSource = Path.GetFullPath(bancoAuditoria), Mode = SqliteOpenMode.ReadOnly,
                Pooling = false }.ToString());
            await conexao.OpenAsync(ct).ConfigureAwait(false);
            await ValidarVersaoAsync(conexao, ct).ConfigureAwait(false);
            await using var cmd = conexao.CreateCommand();
            cmd.CommandText = """
                SELECT id, estado, original, destino, tamanho, sha256, execucao_id, mensagem,
                       criado_utc FROM movimentacoes WHERE execucao_id = $exec
                ORDER BY criado_utc, id;
                """;
            cmd.Parameters.AddWithValue("$exec", idExecucao);
            long total = 0;
            await using (var leitor = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false))
            await using (var escritor = new StreamWriter(new FileStream(temporario,
                FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536, true),
                new System.Text.UTF8Encoding(true)))
            {
                await escritor.WriteLineAsync("Id;Estado;Original;Destino;TamanhoBytes;SHA256;Execucao;Mensagem;CriadoUtc".AsMemory(), ct);
                while (await leitor.ReadAsync(ct).ConfigureAwait(false))
                {
                    var valores = Enumerable.Range(0, 9).Select(i => leitor.IsDBNull(i) ? "" :
                        Convert.ToString(leitor.GetValue(i), CultureInfo.InvariantCulture));
                    await escritor.WriteLineAsync(Relatorios.ExportadorRelatoriosM5.LinhaCsv(valores).AsMemory(), ct);
                    total++;
                }
                await escritor.FlushAsync(ct);
            }
            ct.ThrowIfCancellationRequested();
            File.Move(temporario, final);
            return total;
        }
        finally { if (File.Exists(temporario)) File.Delete(temporario); }
    }

    private static async Task<string> CalcularShaEstavelAsync(
        string caminho, long esperado, DateTimeOffset modificado,
        CancellationToken cancelamento)
    {
        VerificarMetadados(caminho, esperado, modificado);
        // Permitir renomeação no mesmo volume enquanto handle está aberto,
        // mas bloquear escrita durante SHA. O movimento não é prova de cópia
        // para outra unidade e não substitui avaliação de permissões/hardlinks.
        await using var fluxo = Clear.Infrastructure.Arquivos.SessaoPreservacaoAcesso.Abrir(
            caminho, FileShare.Read | FileShare.Delete);
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

    internal static bool EstaDentro(string caminho, string raiz)
    {
        var relativo = Path.GetRelativePath(raiz, caminho);
        return !Path.IsPathRooted(relativo)
            && !string.Equals(relativo, "..", StringComparison.Ordinal)
            && !relativo.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal);
    }

    internal static void VerificarCaminhoSeguro(string caminho, bool arquivo)
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
        if (versao is not (0 or 1 or VersaoEsquema))
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
                    criado_utc TEXT NOT NULL, mensagem TEXT,
                    raiz_analisada TEXT, estrutura TEXT
                );
                CREATE INDEX idx_movimentacoes_data ON movimentacoes(criado_utc);
                PRAGMA user_version = 2;
                """;
            await criar.ExecuteNonQueryAsync(cancelamento).ConfigureAwait(false);
            await schemaTx.CommitAsync(cancelamento).ConfigureAwait(false);
        }
        else if (versao == 1)
        {
            // Os ~400 registros existentes permanecem com estrutura Legado,
            // sem movimentar arquivos ou alterar seus destinos armazenados.
            await using var migracao = (SqliteTransaction)await conexao
                .BeginTransactionAsync(cancelamento).ConfigureAwait(false);
            await using var migrarComando = conexao.CreateCommand();
            migrarComando.Transaction = migracao;
            migrarComando.CommandText = """
                ALTER TABLE movimentacoes ADD COLUMN raiz_analisada TEXT;
                ALTER TABLE movimentacoes ADD COLUMN estrutura TEXT;
                PRAGMA user_version = 2;
                """;
            await migrarComando.ExecuteNonQueryAsync(cancelamento).ConfigureAwait(false);
            await migracao.CommitAsync(cancelamento).ConfigureAwait(false);
        }
    }

    private static async Task ValidarVersaoAsync(
        SqliteConnection conexao, CancellationToken cancelamento)
    {
        await using var cmd = conexao.CreateCommand();
        cmd.CommandText = "PRAGMA user_version;";
        var versao = Convert.ToInt32(await cmd.ExecuteScalarAsync(cancelamento)
            .ConfigureAwait(false), CultureInfo.InvariantCulture);
        if (versao is not (1 or VersaoEsquema))
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
