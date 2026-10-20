using System.Globalization;
using System.Security.Cryptography;
using Microsoft.Data.Sqlite;

namespace Clear.Infrastructure.Quarentena;

/// <summary>
/// Plano criado a partir de um registro M7 persistido, depois de verificar
/// arquivo, SHA-256 e ausência de conflito no caminho original.
/// </summary>
public sealed record PlanoRestauracaoM8(
    string IdOperacao, string IdExecucao,
    string CaminhoQuarentena, string CaminhoOriginal,
    long TamanhoBytes, string Sha256Hex);

public sealed record ResultadoRestauracaoM8(
    string IdOperacao, string Estado, string CaminhoOriginal,
    string CaminhoQuarentena, DateTimeOffset ConcluidoUtc);

/// <summary>
/// Reversão individual assistida do M7. Não sobrescreve, não exclui arquivos,
/// exige nova confirmação humana e registra o estado no SQLite existente.
/// Não considera operações pendentes como elegíveis para restauração.
/// </summary>
public static class ServicoRestauracaoM8
{
    public static async Task<PlanoRestauracaoM8> PrepararAsync(
        string idOperacao, string bancoAuditoria,
        CancellationToken cancelamento = default)
    {
        var registro = await LerRegistroAsync(idOperacao, bancoAuditoria, true, cancelamento)
            .ConfigureAwait(false);
        if (registro.Estado != "EmQuarentena")
            throw new InvalidOperationException(
                "Somente movimentações confirmadas como EmQuarentena podem ser restauradas.");

        var plano = new PlanoRestauracaoM8(
            registro.IdOperacao, registro.IdExecucao,
            registro.CaminhoDestino, registro.CaminhoOriginal,
            registro.TamanhoBytes, registro.Sha256Hex);
        await ValidarIntegridadeAsync(plano, cancelamento).ConfigureAwait(false);
        return plano;
    }

    public static async Task<ResultadoRestauracaoM8> RestaurarAsync(
        PlanoRestauracaoM8 plano, bool confirmacaoHumanaExplicita,
        string bancoAuditoria, CancellationToken cancelamento = default)
    {
        ArgumentNullException.ThrowIfNull(plano);
        if (!confirmacaoHumanaExplicita)
            throw new OperationCanceledException("A restauração foi recusada pelo usuário.");
        cancelamento.ThrowIfCancellationRequested();

        // Um registro persistido em SQLite, e não o plano enviado pelo chamador,
        // é a fonte de verdade. Verifica novamente estado e campos essenciais.
        await using var conexao = await AbrirBancoAsync(
            bancoAuditoria, somenteLeitura: false, cancelamento).ConfigureAwait(false);
        var registro = await CarregarAsync(conexao, plano.IdOperacao, cancelamento)
            .ConfigureAwait(false);
        if (registro.Estado != "EmQuarentena")
            throw new InvalidOperationException(
                "Esta operação não está mais elegível para restauração.");

        if (registro.IdExecucao != plano.IdExecucao
            || registro.CaminhoDestino != plano.CaminhoQuarentena
            || registro.CaminhoOriginal != plano.CaminhoOriginal
            || registro.TamanhoBytes != plano.TamanhoBytes
            || registro.Sha256Hex != plano.Sha256Hex)
            throw new IOException("O plano não corresponde ao registro de auditoria atual.");

        await ValidarIntegridadeAsync(plano, cancelamento).ConfigureAwait(false);
        cancelamento.ThrowIfCancellationRequested();

        // Reserva persistente do direito de movimentar: evita duas restaurações
        // simultâneas e registra um estado incerto no caso de crash.
        var atualizados = await AtualizarEstadoAsync(conexao, plano.IdOperacao,
            "EmQuarentena", "RestauracaoPendente",
            "Restauração confirmada; movimentação em andamento.", cancelamento)
            .ConfigureAwait(false);
        if (atualizados != 1)
            throw new InvalidOperationException("A operação foi alterada por outra sessão.");

        try
        {
            // Após marcar Pendente não honrar cancelamento solicitado,
            // para não interromper a finalização da auditoria.
            await ValidarIntegridadeAsync(plano, CancellationToken.None)
                .ConfigureAwait(false);

            // O destino livre é checado novamente imediatamente antes do Move.
            // Sem fallback de sobrescrita, renomeação, criação de diretórios ou cópia.
            if (File.Exists(plano.CaminhoOriginal) || Directory.Exists(plano.CaminhoOriginal))
                throw new IOException("Conflito: o caminho original está ocupado.");
            File.Move(plano.CaminhoQuarentena, plano.CaminhoOriginal, overwrite: false);

            var fim = DateTimeOffset.UtcNow;
            var gravados = await AtualizarEstadoAsync(conexao, plano.IdOperacao,
                "RestauracaoPendente", "Restaurado",
                "Restauração confirmada e concluída em UTC: " +
                fim.ToString("O", CultureInfo.InvariantCulture),
                CancellationToken.None).ConfigureAwait(false);
            if (gravados != 1)
                throw new IOException(
                    "Arquivo restaurado, mas não foi possível confirmar o status na auditoria.");
            return new ResultadoRestauracaoM8(plano.IdOperacao, "Restaurado",
                plano.CaminhoOriginal, plano.CaminhoQuarentena, fim);
        }
        catch (Exception ex)
        {
            // Apenas se o arquivo comprovadamente permaneceu na quarentena
            // e o destino original ainda está livre, podemos voltar ao estado
            // anterior, com a falha registrada para nova revisão humana.
            // Cenários ambíguos permanecem RestauracaoPendente para conciliação.
            if (File.Exists(plano.CaminhoQuarentena)
                && !File.Exists(plano.CaminhoOriginal)
                && !Directory.Exists(plano.CaminhoOriginal))
            {
                try
                {
                    await AtualizarEstadoAsync(conexao, plano.IdOperacao,
                        "RestauracaoPendente", "EmQuarentena",
                        "Tentativa de restauração não concluída em UTC " +
                        DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture) +
                        ": " + ex.Message, CancellationToken.None).ConfigureAwait(false);
                }
                catch (Exception)
                {
                    // O erro primário é mantido. Revisão da auditoria é obrigatória.
                }
            }
            throw;
        }
    }

    private static async Task ValidarIntegridadeAsync(
        PlanoRestauracaoM8 plano, CancellationToken cancelamento)
    {
        var fonte = Path.GetFullPath(plano.CaminhoQuarentena);
        var destino = Path.GetFullPath(plano.CaminhoOriginal);
        if (string.Equals(fonte, destino, StringComparison.OrdinalIgnoreCase))
            throw new IOException("Origem e destino não podem ser iguais.");
        if (plano.Sha256Hex.Length != 64 || !plano.Sha256Hex.All(Uri.IsHexDigit))
            throw new InvalidDataException("SHA-256 na auditoria está inválido.");
        // Suporta registros legados M7: <raiz>/<GUID>/<nome> e registros
        // novos: <raiz>/<GUID>/<caminho relativo completo da origem>.
        var diretorio = new DirectoryInfo(Path.GetDirectoryName(fonte)
            ?? throw new IOException("Caminho da quarentena inválido."));
        if (!Guid.TryParseExact(plano.IdOperacao, "N", out _))
            throw new IOException("Identificador de operação inválido.");
        while (diretorio.Parent is not null &&
            !string.Equals(diretorio.Name, plano.IdOperacao,
                StringComparison.OrdinalIgnoreCase))
            diretorio = diretorio.Parent;
        if (!string.Equals(diretorio.Name, plano.IdOperacao,
                StringComparison.OrdinalIgnoreCase) || diretorio.Parent is null)
            throw new IOException("Pasta da operação auditada não encontrada.");
        var raizQuarentena = diretorio.Parent.FullName;
        var relativo = Path.GetRelativePath(diretorio.FullName, fonte);
        if (Path.IsPathRooted(relativo) || relativo is "." or ".." ||
            relativo.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            || !destino.EndsWith(Path.DirectorySeparatorChar + relativo,
                StringComparison.OrdinalIgnoreCase))
            throw new IOException("Hierarquia da quarentena incompatível com a origem.");
        if (string.Equals(raizQuarentena, Path.GetDirectoryName(destino),
                StringComparison.OrdinalIgnoreCase))
            throw new IOException("Quarentena e origem não podem ser a mesma pasta.");
        if (!string.Equals(Path.GetPathRoot(fonte),
            Path.GetPathRoot(destino), StringComparison.OrdinalIgnoreCase))
            throw new IOException("Restauração entre volumes não é permitida.");
        var relativoAoDestino = Path.GetRelativePath(raizQuarentena, destino);
        if (!Path.IsPathRooted(relativoAoDestino)
            && !string.Equals(relativoAoDestino, "..", StringComparison.Ordinal)
            && !relativoAoDestino.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw new IOException("Destino original não pode estar dentro da quarentena.");

        VerificarDiretorios(fonte);
        VerificarDiretorios(destino);
        if (File.Exists(destino) || Directory.Exists(destino))
            throw new IOException("Já existe arquivo ou pasta no caminho original; nada será sobrescrito.");
        var info = new FileInfo(fonte);
        if (!info.Exists || (info.Attributes & FileAttributes.ReparsePoint) != 0)
            throw new IOException("Arquivo da quarentena ausente ou link simbólico.");
        if (info.Length != plano.TamanhoBytes)
            throw new IOException("Arquivo da quarentena mudou de tamanho.");

        // Share.Read | Share.Delete: permite leitura mas bloqueia escrita normal
        // durante a conferência. Há janela TOCTOU entre esta leitura e File.Move.
        var antesUtc = info.LastWriteTimeUtc;
        await using var fluxo = new FileStream(fonte, new FileStreamOptions
        {
            Mode = FileMode.Open, Access = FileAccess.Read,
            Share = FileShare.Read | FileShare.Delete,
            Options = FileOptions.Asynchronous | FileOptions.SequentialScan,
            BufferSize = 65536
        });
        if (fluxo.Length != plano.TamanhoBytes)
            throw new IOException("Tamanho alterado durante verificação.");
        var hash = Convert.ToHexString(
            await SHA256.HashDataAsync(fluxo, cancelamento).ConfigureAwait(false));
        info.Refresh();
        if (!info.Exists || info.Length != plano.TamanhoBytes
            || info.LastWriteTimeUtc != antesUtc || hash != plano.Sha256Hex)
            throw new IOException("SHA-256 ou metadados da quarentena divergentes; restauração bloqueada.");
    }

    private static void VerificarDiretorios(string caminho)
    {
        for (DirectoryInfo? pasta = new(Path.GetDirectoryName(caminho)
                ?? throw new IOException("Caminho inválido."));
            pasta is not null; pasta = pasta.Parent)
        {
            if (!pasta.Exists)
                throw new DirectoryNotFoundException(
                    "Uma pasta da origem ou do destino foi removida; recrie-a manualmente antes de restaurar.");
            if ((pasta.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Caminho contém junction ou link simbólico.");
        }
    }

    private static async Task<(string IdOperacao, string IdExecucao, string Estado,
            string CaminhoOriginal, string CaminhoDestino, long TamanhoBytes, string Sha256Hex)>
        LerRegistroAsync(string idOperacao, string banco, bool leitura,
            CancellationToken cancelamento)
    {
        await using var conexao = await AbrirBancoAsync(banco, leitura, cancelamento)
            .ConfigureAwait(false);
        return await CarregarAsync(conexao, idOperacao, cancelamento)
            .ConfigureAwait(false);
    }

    private static async Task<(string IdOperacao, string IdExecucao, string Estado,
            string CaminhoOriginal, string CaminhoDestino, long TamanhoBytes, string Sha256Hex)>
        CarregarAsync(SqliteConnection conexao, string idOperacao,
            CancellationToken cancelamento)
    {
        if (string.IsNullOrWhiteSpace(idOperacao))
            throw new ArgumentException("Selecione uma movimentação auditada.", nameof(idOperacao));
        await using var cmd = conexao.CreateCommand();
        cmd.CommandText = """
            SELECT id, execucao_id, estado, original, destino, tamanho, sha256
            FROM movimentacoes WHERE id = $id LIMIT 1;
            """;
        cmd.Parameters.AddWithValue("$id", idOperacao);
        await using var leitor = await cmd.ExecuteReaderAsync(cancelamento)
            .ConfigureAwait(false);
        if (!await leitor.ReadAsync(cancelamento).ConfigureAwait(false))
            throw new InvalidOperationException("Movimentação não encontrada na auditoria M7.");
        return (leitor.GetString(0), leitor.GetString(1), leitor.GetString(2),
            leitor.GetString(3), leitor.GetString(4), leitor.GetInt64(5),
            leitor.GetString(6));
    }

    private static async Task<SqliteConnection> AbrirBancoAsync(
        string banco, bool somenteLeitura, CancellationToken cancelamento)
    {
        if (string.IsNullOrWhiteSpace(banco) || !File.Exists(banco))
            throw new FileNotFoundException("Auditoria M7 não encontrada.", banco);
        if ((File.GetAttributes(banco) & FileAttributes.ReparsePoint) != 0)
            throw new IOException("Auditoria não pode ser link simbólico.");
        VerificarDiretorios(banco);
        var conexao = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = Path.GetFullPath(banco),
            Mode = somenteLeitura ? SqliteOpenMode.ReadOnly : SqliteOpenMode.ReadWrite,
            Pooling = false
        }.ToString());
        try
        {
            await conexao.OpenAsync(cancelamento).ConfigureAwait(false);
            await using var cmd = conexao.CreateCommand();
            cmd.CommandText = "PRAGMA user_version;";
            var versao = Convert.ToInt32(await cmd.ExecuteScalarAsync(cancelamento)
                .ConfigureAwait(false), CultureInfo.InvariantCulture);
            if (versao != ServicoQuarentenaM7.VersaoEsquema)
                throw new InvalidDataException("Versão do banco de auditoria não suportada.");
            return conexao;
        }
        catch
        {
            await conexao.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    private static async Task<int> AtualizarEstadoAsync(
        SqliteConnection conexao, string id, string esperado, string novo,
        string mensagem, CancellationToken cancelamento)
    {
        await using var cmd = conexao.CreateCommand();
        cmd.CommandText = """
            UPDATE movimentacoes SET estado = $novo, mensagem = $mensagem
            WHERE id = $id AND estado = $esperado;
            """;
        cmd.Parameters.AddWithValue("$id", id);
        cmd.Parameters.AddWithValue("$esperado", esperado);
        cmd.Parameters.AddWithValue("$novo", novo);
        cmd.Parameters.AddWithValue("$mensagem", mensagem);
        return await cmd.ExecuteNonQueryAsync(cancelamento).ConfigureAwait(false);
    }
}
