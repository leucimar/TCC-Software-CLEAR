using System.Globalization;
using Clear.Infrastructure.Historico;
using Microsoft.Data.Sqlite;

namespace Clear.Infrastructure.Quarentena;

public static partial class ServicoQuarentenaM7
{
    public const string EstadoExcluidoPeloUsuario = "ExcluidoPeloUsuario";

    /// <summary>Confirma ausência abaixo de uma raiz acessível e sem redirecionamentos.</summary>
    public static bool ArquivoAusenteEmRaizDisponivel(string caminho, string raiz)
    {
        var raizCompleta = Path.GetFullPath(raiz);
        var relativo = Path.GetRelativePath(raizCompleta, Path.GetFullPath(caminho));
        if (!EstaDentro(caminho, raizCompleta) || relativo == ".") return false;

        try { _ = File.GetAttributes(caminho); return false; }
        catch (Exception erro) when (erro is FileNotFoundException or DirectoryNotFoundException) { }
        catch (Exception erro) when (erro is IOException or UnauthorizedAccessException) { return false; }

        try
        {
            for (DirectoryInfo? pasta = new(raizCompleta); pasta is not null; pasta = pasta.Parent)
            {
                var atributos = File.GetAttributes(pasta.FullName);
                if ((atributos & (FileAttributes.Directory | FileAttributes.ReparsePoint)) != FileAttributes.Directory)
                    return false;
            }
        }
        catch (Exception erro) when (erro is IOException or UnauthorizedAccessException) { return false; }

        var atual = raizCompleta;
        var partes = relativo.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        for (var i = 0; i < partes.Length; i++)
        {
            atual = Path.Combine(atual, partes[i]);
            try
            {
                var atributos = File.GetAttributes(atual);
                if ((atributos & FileAttributes.ReparsePoint) != 0 ||
                    (i < partes.Length - 1 && (atributos & FileAttributes.Directory) == 0))
                    return false;
            }
            catch (Exception erro) when (erro is FileNotFoundException or DirectoryNotFoundException)
            {
                // Se o volume ou compartilhamento cair no intervalo, ele não prova exclusão.
                try
                {
                    return (File.GetAttributes(raizCompleta) & (FileAttributes.Directory | FileAttributes.ReparsePoint))
                        == FileAttributes.Directory;
                }
                catch (Exception falha) when (falha is IOException or UnauthorizedAccessException) { return false; }
            }
            catch (Exception erro) when (erro is IOException or UnauthorizedAccessException) { return false; }
        }
        return false;
    }

    public static async Task RegistrarExclusaoNaQuarentenaAsync(RegistroQuarentenaM7 registro,
        string raizAnalise, string banco, bool confirmado, CancellationToken ct = default)
    {
        ExigirConfirmacao(confirmado);
        var rota = ServicoRestauracaoM8.ConsultarRaizes(registro, raizAnalise);
        VerificarLocalAuditoria(banco, rota.Origem, rota.Destino);
        await using var conexao = await AbrirAuditoriaParaBaixaAsync(banco, criar: false, ct).ConfigureAwait(false);
        if (!ArquivoAusenteEmRaizDisponivel(registro.CaminhoDestino, rota.Origem))
            throw new IOException("A ausência do arquivo na quarentena não pôde ser confirmada.");
        await using var cmd = conexao.CreateCommand();
        cmd.CommandText = """
            UPDATE movimentacoes SET estado = $excluido,
                mensagem = CASE WHEN mensagem IS NULL OR mensagem = '' THEN $mensagem
                                ELSE mensagem || char(10) || $mensagem END
            WHERE id = $id AND execucao_id = $exec AND destino = $caminho
                AND estado = 'EmQuarentena';
            """;
        cmd.Parameters.AddWithValue("$excluido", EstadoExcluidoPeloUsuario);
        cmd.Parameters.AddWithValue("$mensagem", "Usuário confirmou a exclusão do item ausente na quarentena em UTC: " + AgoraUtc());
        cmd.Parameters.AddWithValue("$id", registro.IdOperacao);
        cmd.Parameters.AddWithValue("$exec", registro.IdExecucao);
        cmd.Parameters.AddWithValue("$caminho", registro.CaminhoDestino);
        if (await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false) != 1)
            throw new InvalidOperationException("O registro mudou desde a tentativa de restauração. Atualize o Inventário.");
    }

    public static async Task RegistrarExclusaoNaOrigemAsync(EntradaHistoricaM6 entrada,
        ResumoExecucaoHistoricaM6 execucao, string banco, bool confirmado, CancellationToken ct = default)
    {
        ExigirConfirmacao(confirmado);
        if (entrada.Tipo != "Arquivo" || execucao.Estado != "Concluida")
            throw new InvalidOperationException("Selecione um arquivo de uma análise concluída.");
        var origem = Path.GetFullPath(entrada.Caminho);
        var raiz = Path.GetFullPath(execucao.DiretorioRaiz);
        VerificarLocalAuditoria(banco, raiz);
        if (!ArquivoAusenteEmRaizDisponivel(origem, raiz))
            throw new IOException("A ausência do arquivo no acervo não pôde ser confirmada.");
        await using var conexao = await AbrirAuditoriaParaBaixaAsync(banco, criar: true, ct).ConfigureAwait(false);
        if (!ArquivoAusenteEmRaizDisponivel(origem, raiz))
            throw new IOException("A ausência do arquivo no acervo não pôde ser confirmada.");
        await using var cmd = conexao.CreateCommand();
        cmd.CommandText = """
            INSERT INTO movimentacoes (
                id, execucao_id, entrada_id, original, destino, tamanho, sha256,
                estado, criado_utc, mensagem, raiz_analisada, estrutura
            )
            SELECT $id, $exec, $entrada, $original, '', $tamanho, '', $estado, $utc,
                $mensagem, $raiz, NULL
            WHERE NOT EXISTS (
                SELECT 1 FROM movimentacoes WHERE original = $original COLLATE NOCASE AND (
                    estado IN ('Pendente', 'EmQuarentena', 'RestauracaoPendente')
                    OR (estado = $estado AND execucao_id = $exec)
                )
            );
            """;
        cmd.Parameters.AddWithValue("$id", Guid.NewGuid().ToString("N"));
        cmd.Parameters.AddWithValue("$exec", execucao.Id);
        cmd.Parameters.AddWithValue("$entrada", entrada.Id);
        cmd.Parameters.AddWithValue("$original", origem);
        cmd.Parameters.AddWithValue("$tamanho", entrada.TamanhoBytes ?? 0);
        cmd.Parameters.AddWithValue("$estado", EstadoExcluidoPeloUsuario);
        cmd.Parameters.AddWithValue("$utc", AgoraUtc());
        cmd.Parameters.AddWithValue("$mensagem", "Usuário confirmou a exclusão do arquivo ausente no acervo antes do envio à quarentena.");
        cmd.Parameters.AddWithValue("$raiz", raiz);
        if (await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false) != 1)
            throw new InvalidOperationException("O arquivo já possui uma operação ativa ou uma exclusão registrada nesta análise.");
    }

    private static void ExigirConfirmacao(bool confirmada)
    {
        if (!confirmada) throw new OperationCanceledException("O usuário não confirmou o registro de exclusão.");
    }

    private static string AgoraUtc() => DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture);

    private static void VerificarLocalAuditoria(string banco, params string[] raizes)
    {
        var caminho = Path.GetFullPath(banco);
        if (raizes.Any(r => EstaDentro(caminho, r)))
            throw new IOException("Banco de auditoria deve ficar fora do acervo e da quarentena.");
    }

    private static async Task<SqliteConnection> AbrirAuditoriaParaBaixaAsync(
        string banco, bool criar, CancellationToken ct)
    {
        var caminho = Path.GetFullPath(banco);
        var pasta = Path.GetDirectoryName(caminho)!;
        VerificarCaminhoSeguro(pasta, arquivo: false);
        if (File.Exists(caminho)) VerificarCaminhoSeguro(caminho, arquivo: true);
        if (criar) Directory.CreateDirectory(pasta);
        var conexao = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = caminho, Mode = criar ? SqliteOpenMode.ReadWriteCreate : SqliteOpenMode.ReadWrite,
            Pooling = false
        }.ToString());
        try
        {
            await conexao.OpenAsync(ct).ConfigureAwait(false);
            if (criar) await PrepararBancoAsync(conexao, ct).ConfigureAwait(false);
            else await ValidarVersaoAsync(conexao, ct).ConfigureAwait(false);
            return conexao;
        }
        catch
        {
            await conexao.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }
}
