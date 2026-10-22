using System.Buffers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Clear.Application.Amostragem;

namespace Clear.Infrastructure.Amostragem;

public sealed record ProgressoCopiaAmostra(
    int ArquivosCopiados, int TotalArquivos, long BytesCopiados, long TotalBytes,
    string UltimoCaminhoRelativo);

public sealed record SubBaseConcluida(
    string DiretorioFinal, string ManifestoResumo, string ManifestoArquivos,
    int ArquivosCopiados, long BytesCopiados);

/// <summary>
/// Somente COPIA arquivos da origem; não move, não elimina e não sobrescreve.
/// Publica a pasta final apenas após cópia integral, verificação SHA-256 de
/// cada destino e manifesto. Erros deixam pasta separada EM_CONSTRUCAO.
/// </summary>
public static class CriadorSubBaseExperimental
{
    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
    private static readonly JsonSerializerOptions JsonLinhas = new(Json)
    {
        WriteIndented = false
    };

    public static PlanoAmostraEstratificada Preparar(
        string origem, string destino, decimal percentual = 30M, int semente = 5721,
        CancellationToken cancelamento = default)
    {
        var raiz = ValidarOrigemDestino(origem, destino);
        if (Directory.Exists(destino) || File.Exists(destino))
            throw new IOException("A sub-base de destino já existe; escolha um novo nome.");
        var pilha = new Stack<DirectoryInfo>();
        pilha.Push(new DirectoryInfo(raiz));
        var arquivos = new List<ArquivoCandidatoAmostra>();
        while (pilha.Count > 0)
        {
            cancelamento.ThrowIfCancellationRequested();
            var pasta = pilha.Pop();
            foreach (var entrada in pasta.EnumerateFileSystemInfos())
            {
                cancelamento.ThrowIfCancellationRequested();
                entrada.Refresh();
                if ((entrada.Attributes & FileAttributes.ReparsePoint) != 0)
                    throw new IOException(
                        "A origem contém link/junction; análise interrompida com segurança: " +
                        entrada.FullName);
                if ((entrada.Attributes & FileAttributes.Directory) != 0)
                    pilha.Push(new DirectoryInfo(entrada.FullName));
                else
                {
                    var info = new FileInfo(entrada.FullName);
                    var relativo = Path.GetRelativePath(raiz, info.FullName);
                    arquivos.Add(new ArquivoCandidatoAmostra(
                        relativo, info.Length, info.LastWriteTimeUtc));
                }
            }
        }
        cancelamento.ThrowIfCancellationRequested();
        return PlanejadorAmostraEstratificada.Planejar(arquivos, percentual, semente);
    }

    public static async Task<SubBaseConcluida> ExecutarAsync(
        string origem, string destino, PlanoAmostraEstratificada plano,
        IProgress<ProgressoCopiaAmostra>? progresso = null,
        CancellationToken cancelamento = default)
    {
        ArgumentNullException.ThrowIfNull(plano);
        var raiz = ValidarOrigemDestino(origem, destino);
        var final = Path.GetFullPath(destino);
        cancelamento.ThrowIfCancellationRequested();
        // Evitar que a prévia antiga represente um acervo diferente no
        // momento da cópia. Reavaliar todos os metadados, inclusive de itens
        // que NÃO foram selecionados.
        var conferido = Preparar(raiz, final, plano.PercentualSolicitado,
            plano.Semente, cancelamento);
        if (!plano.Originais.SequenceEqual(conferido.Originais)
            || !plano.Selecionados.SequenceEqual(conferido.Selecionados))
            throw new IOException(
                "A origem mudou desde a prévia. Recalcule a amostra antes de copiar.");
        cancelamento.ThrowIfCancellationRequested();
        if (Directory.Exists(final) || File.Exists(final))
            throw new IOException("A sub-base de destino já existe; não será sobrescrita.");

        var unidade = new DriveInfo(Path.GetPathRoot(final)!);
        // Margem para diretórios/manifestos e diferenças na alocação em disco.
        var necessario = (decimal)plano.BytesSelecionados * 1.05M + 512M * 1024M * 1024M;
        if (unidade.AvailableFreeSpace < necessario)
            throw new IOException(
                "Espaço livre insuficiente no destino para a cópia e a margem de segurança. " +
                $"Necessário aproximadamente {necessario:N0} bytes; disponível " +
                $"{unidade.AvailableFreeSpace:N0} bytes.");

        var pai = Path.GetDirectoryName(final)
            ?? throw new IOException("Pasta de destino inválida.");
        VerificarCaminhoSemLinks(pai);
        Directory.CreateDirectory(pai);
        VerificarCaminhoSemLinks(pai);
        var estagio = Path.Combine(pai,
            Path.GetFileName(final) + ".CLEAR_EM_CONSTRUCAO_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(estagio);
        var conteudo = Path.Combine(estagio, "Acervo");
        Directory.CreateDirectory(conteudo);
        var manifestoArquivos = Path.Combine(estagio, "manifesto_arquivos.jsonl");
        var inicio = DateTimeOffset.UtcNow;
        await File.WriteAllTextAsync(Path.Combine(estagio, "LEIA-ME-INCOMPLETO.txt"),
            "CLEAR: este diretório é uma COPIA EM CONSTRUCAO. " +
            "Não usar como dataset concluído. A origem não foi movida ou excluída." +
            Environment.NewLine + "Origem: " + raiz +
            Environment.NewLine + "Destino esperado: " + final,
            cancelamento).ConfigureAwait(false);

        int quantidade = 0;
        long bytes = 0;
        try
        {
            await using (var fluxo = new FileStream(manifestoArquivos,
                FileMode.CreateNew, FileAccess.Write, FileShare.None,
                bufferSize: 65536, useAsync: true))
            await using (var escritor = new StreamWriter(fluxo, new UTF8Encoding(false)))
            {
                foreach (var item in plano.Selecionados)
                {
                    cancelamento.ThrowIfCancellationRequested();
                    var arquivoOrigem = Path.GetFullPath(Path.Combine(raiz, item.CaminhoRelativo));
                    var arquivoDestino = Path.GetFullPath(Path.Combine(conteudo, item.CaminhoRelativo));
                    ExigirDescendente(arquivoOrigem, raiz);
                    ExigirDescendente(arquivoDestino, conteudo);
                    var pastaArquivo = Path.GetDirectoryName(arquivoDestino)!;
                    VerificarCaminhoSemLinks(pastaArquivo);
                    Directory.CreateDirectory(pastaArquivo);
                    VerificarCaminhoSemLinks(pastaArquivo);
                    var hash = await CopiarEConferirAsync(
                        arquivoOrigem, arquivoDestino, item, cancelamento)
                        .ConfigureAwait(false);

                    var registro = JsonSerializer.Serialize(new
                    {
                        item.CaminhoRelativo,
                        item.TamanhoBytes,
                        item.UltimaModificacaoUtc,
                        item.GrupoOrigem,
                        item.Formato,
                        item.FaixaTamanho,
                        Sha256 = hash
                    }, JsonLinhas);
                    await escritor.WriteLineAsync(registro.AsMemory(), cancelamento)
                        .ConfigureAwait(false);
                    quantidade++;
                    bytes = checked(bytes + item.TamanhoBytes);
                    if (quantidade % 64 == 0 || quantidade == plano.Selecionados.Count)
                        progresso?.Report(new ProgressoCopiaAmostra(
                            quantidade, plano.Selecionados.Count, bytes,
                            plano.BytesSelecionados, item.CaminhoRelativo));
                }
                await escritor.FlushAsync(cancelamento).ConfigureAwait(false);
            }

            var resumo = new
            {
                Esquema = "CLEAR-SUBBASE-1",
                Estado = "Concluida",
                InicioUtc = inicio,
                ConclusaoUtc = DateTimeOffset.UtcNow,
                Origem = raiz,
                Destino = final,
                PercentualSolicitado = plano.PercentualSolicitado,
                plano.Semente,
                ArquivosOriginais = plano.Originais.Count,
                ArquivosSelecionados = quantidade,
                plano.BytesOriginais,
                BytesSelecionados = bytes,
                PercentualArquivosObtido = plano.PercentualArquivosReal,
                PercentualBytesObtido = plano.PercentualBytesReal,
                Estratos = plano.Estratos,
                Metodologia = "Amostragem estratificada por pasta principal, extensão e faixa " +
                    "de tamanho; quantis de tamanho com semente reprodutível. " +
                    "As quotas de arquivos são exatas globalmente; bytes são aproximados.",
                Garantias = "Cópia sem sobrescrita, SHA-256 origem/destino conferidos " +
                    "arquivo a arquivo; arquivos de origem não movidos ou excluídos.",
                Observacao = "Amostra de dados públicos; restrições de licença e riscos " +
                    "dos arquivos devem ser avaliados fora do aplicativo."
            };
            var manifesto = Path.Combine(estagio, "manifesto_resumo.json");
            await File.WriteAllTextAsync(manifesto, JsonSerializer.Serialize(resumo, Json),
                cancelamento).ConfigureAwait(false);
            cancelamento.ThrowIfCancellationRequested();
            VerificarCaminhoSemLinks(pai);
            if (Directory.Exists(final) || File.Exists(final))
                throw new IOException("Pasta final surgiu durante a cópia; não será substituída.");

            // Mesma pasta pai: a publicação do diretório concluído é um rename.
            // Em caso de falha, o estágio continua explicitamente incompleto.
            Directory.Move(estagio, final);
            // Apenas remover a etiqueta após a publicação. A falha na
            // limpeza da etiqueta não invalida dados já publicados.
            try { File.Delete(Path.Combine(final, "LEIA-ME-INCOMPLETO.txt")); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            return new SubBaseConcluida(final,
                Path.Combine(final, "manifesto_resumo.json"),
                Path.Combine(final, "manifesto_arquivos.jsonl"), quantidade, bytes);
        }
        catch
        {
            // Não remover arquivos nem pastas sem autorização: o estágio fica
            // identificado como incompleto para inspeção/limpeza manual.
            throw;
        }
    }

    private static async Task<string> CopiarEConferirAsync(
        string origem, string destino, ArquivoCandidatoAmostra esperado,
        CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        VerificarCaminhoSemLinks(Path.GetDirectoryName(origem)!);
        var info = new FileInfo(origem);
        info.Refresh();
        if (!info.Exists || (info.Attributes & FileAttributes.ReparsePoint) != 0 ||
            info.Length != esperado.TamanhoBytes ||
            info.LastWriteTimeUtc != esperado.UltimaModificacaoUtc)
            throw new IOException("Arquivo original mudou desde a prévia: " + origem);
        var temporario = destino + ".CLEAR_COPIANDO_" + Guid.NewGuid().ToString("N");
        string? hashCalculado = null;
        try
        {
            await using (var fonte = new FileStream(origem, new FileStreamOptions
            {
                Mode = FileMode.Open, Access = FileAccess.Read, Share = FileShare.Read,
                Options = FileOptions.Asynchronous | FileOptions.SequentialScan,
                BufferSize = 131072
            }))
            await using (var alvo = new FileStream(temporario, new FileStreamOptions
            {
                Mode = FileMode.CreateNew, Access = FileAccess.Write, Share = FileShare.None,
                Options = FileOptions.Asynchronous | FileOptions.SequentialScan,
                BufferSize = 131072
            }))
            using (var digest = IncrementalHash.CreateHash(HashAlgorithmName.SHA256))
            {
                if (fonte.Length != esperado.TamanhoBytes)
                    throw new IOException("Tamanho original alterado antes da cópia.");
                var buffer = ArrayPool<byte>.Shared.Rent(131072);
                long lidos = 0;
                try
                {
                    int bloco;
                    while ((bloco = await fonte.ReadAsync(buffer.AsMemory(0, buffer.Length), token)
                               .ConfigureAwait(false)) != 0)
                    {
                        digest.AppendData(buffer, 0, bloco);
                        await alvo.WriteAsync(buffer.AsMemory(0, bloco), token)
                            .ConfigureAwait(false);
                        lidos = checked(lidos + bloco);
                    }
                    await alvo.FlushAsync(token).ConfigureAwait(false);
                    if (lidos != esperado.TamanhoBytes)
                        throw new IOException("Quantidade de bytes da origem divergente.");
                    var hashFonte = Convert.ToHexString(digest.GetHashAndReset());
                    info.Refresh();
                    if (!info.Exists || info.Length != esperado.TamanhoBytes ||
                        info.LastWriteTimeUtc != esperado.UltimaModificacaoUtc)
                        throw new IOException("Origem alterada durante a cópia.");

                    // O arquivo precisa estar fechado antes de ser lido e renomeado.
                    // Por isso a validação do destino ocorre após este bloco.
                    hashCalculado = hashFonte;
                }
                finally { ArrayPool<byte>.Shared.Return(buffer); }
            }

            var hash = hashCalculado ?? throw new IOException("Hash não calculado.");
            await using (var validar = new FileStream(temporario, FileMode.Open,
                FileAccess.Read, FileShare.Read))
            {
                if (validar.Length != esperado.TamanhoBytes)
                    throw new IOException("Cópia ficou com tamanho divergente.");
                var hashCopia = Convert.ToHexString(
                    await SHA256.HashDataAsync(validar, token).ConfigureAwait(false));
                if (!string.Equals(hash, hashCopia, StringComparison.Ordinal))
                    throw new IOException("SHA-256 da cópia divergente; operação não publicada.");
            }
            File.SetLastWriteTimeUtc(temporario, esperado.UltimaModificacaoUtc);
            if (File.Exists(destino) || Directory.Exists(destino))
                throw new IOException("Já existe um arquivo no destino da cópia.");
            File.Move(temporario, destino, overwrite: false);
            return hash;
        }
        finally
        {
            if (File.Exists(temporario))
                File.Delete(temporario); // somente arquivo temporário criado nesta operação.
        }
    }

    private static string ValidarOrigemDestino(string origem, string destino)
    {
        if (string.IsNullOrWhiteSpace(origem) || string.IsNullOrWhiteSpace(destino)
            || !Path.IsPathFullyQualified(origem) || !Path.IsPathFullyQualified(destino))
            throw new ArgumentException("Origem e destino devem ser caminhos absolutos.");
        var raiz = Path.TrimEndingDirectorySeparator(Path.GetFullPath(origem));
        var final = Path.TrimEndingDirectorySeparator(Path.GetFullPath(destino));
        if (!Directory.Exists(raiz))
            throw new DirectoryNotFoundException("Pasta de origem não existe.");
        if (EstaDentro(raiz, final) || EstaDentro(final, raiz))
            throw new IOException("Origem e sub-base não podem conter uma à outra.");
        VerificarCaminhoSemLinks(raiz);
        VerificarCaminhoSemLinks(Path.GetDirectoryName(final)!);
        return raiz;
    }

    private static void ExigirDescendente(string caminho, string raiz)
    {
        if (!EstaDentro(caminho, raiz) ||
            string.Equals(caminho, raiz, StringComparison.OrdinalIgnoreCase))
            throw new IOException("Caminho escapou da raiz autorizada.");
    }

    private static bool EstaDentro(string caminho, string raiz)
    {
        var relativo = Path.GetRelativePath(raiz, caminho);
        return !Path.IsPathRooted(relativo) &&
            relativo != ".." && !relativo.StartsWith(".." + Path.DirectorySeparatorChar,
                StringComparison.Ordinal);
    }

    private static void VerificarCaminhoSemLinks(string caminho)
    {
        for (DirectoryInfo? pasta = new(Path.GetFullPath(caminho)); pasta is not null;
             pasta = pasta.Parent)
            if (pasta.Exists && (pasta.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Caminho com link/junction não é permitido: " + pasta.FullName);
    }
}
