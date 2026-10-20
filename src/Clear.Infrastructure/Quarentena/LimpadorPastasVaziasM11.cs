using Microsoft.Data.Sqlite;

namespace Clear.Infrastructure.Quarentena;

/// <summary>
/// Limpa SOMENTE diretórios vazios que são ancestrais de destinos
/// registrados na auditoria M7. Nunca elimina arquivos nem a raiz.
/// O usuário deve autorizar a operação separadamente pela interface.
/// </summary>
public static class LimpadorPastasVaziasM11
{
    public static async Task<int> LimparAsync(
        string banco, string raizQuarentena, bool confirmacaoHumanaExplicita,
        CancellationToken ct = default)
    {
        if (!confirmacaoHumanaExplicita)
            throw new OperationCanceledException("Limpeza de diretórios não autorizada.");
        if (string.IsNullOrWhiteSpace(raizQuarentena) ||
            !Path.IsPathFullyQualified(raizQuarentena))
            throw new ArgumentException("Quarentena precisa ter caminho absoluto.");
        var raiz = Path.TrimEndingDirectorySeparator(Path.GetFullPath(raizQuarentena));
        if (string.Equals(raiz, Path.GetPathRoot(raiz),
                StringComparison.OrdinalIgnoreCase))
            throw new IOException("Nunca limpar a raiz de um volume.");
        if (!Directory.Exists(raiz)) return 0;
        if (!File.Exists(banco))
            throw new FileNotFoundException("Banco de auditoria não encontrado.", banco);

        VerificarSemLinks(raiz);
        var destinos = new List<string>();
        await using (var db = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = Path.GetFullPath(banco), Mode = SqliteOpenMode.ReadOnly,
            Pooling = false
        }.ToString()))
        {
            await db.OpenAsync(ct).ConfigureAwait(false);
            await using var versao = db.CreateCommand();
            versao.CommandText = "PRAGMA user_version;";
            var v = Convert.ToInt32(await versao.ExecuteScalarAsync(ct).ConfigureAwait(false));
            if (v is not (1 or ServicoQuarentenaM7.VersaoEsquema))
                throw new InvalidDataException("Auditoria incompatível.");
            await using var cmd = db.CreateCommand();
            cmd.CommandText = "SELECT destino FROM movimentacoes";
            await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while (await reader.ReadAsync(ct).ConfigureAwait(false))
                destinos.Add(reader.GetString(0));
        }

        var candidatas = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var original in destinos)
        {
            ct.ThrowIfCancellationRequested();
            if (!Path.IsPathFullyQualified(original)) continue;
            var destino = Path.GetFullPath(original);
            if (!Dentro(destino, raiz)) continue;
            for (var dir = Path.GetDirectoryName(destino);
                 dir is not null && !string.Equals(dir, raiz,
                     StringComparison.OrdinalIgnoreCase);
                 dir = Path.GetDirectoryName(dir))
                candidatas.Add(dir);
        }

        var excluidas = 0;
        foreach (var dir in candidatas.OrderByDescending(
            x => x.Count(c => c == Path.DirectorySeparatorChar)))
        {
            ct.ThrowIfCancellationRequested();
            if (!Directory.Exists(dir)) continue;
            VerificarSemLinks(dir);
            if (Directory.EnumerateFileSystemEntries(dir).Any()) continue;
            try
            {
                Directory.Delete(dir, recursive: false);
                excluidas++;
            }
            catch (IOException) { } // concorrência: pasta deixou de estar vazia
            catch (UnauthorizedAccessException) { }
        }
        return excluidas;
    }

    private static bool Dentro(string arquivo, string pasta)
    {
        var relativo = Path.GetRelativePath(pasta, arquivo);
        return !Path.IsPathRooted(relativo) &&
            relativo != "." && relativo != ".." &&
            !relativo.StartsWith(".." + Path.DirectorySeparatorChar,
                StringComparison.Ordinal);
    }

    private static void VerificarSemLinks(string caminho)
    {
        for (DirectoryInfo? dir = new(Path.GetFullPath(caminho));
             dir is not null; dir = dir.Parent)
            if (dir.Exists && (dir.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Diretório contém junction ou link simbólico: " +
                    dir.FullName);
    }
}
