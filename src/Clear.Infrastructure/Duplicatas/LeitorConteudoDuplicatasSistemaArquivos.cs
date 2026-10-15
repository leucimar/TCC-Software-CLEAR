using System.Security.Cryptography;
using Clear.Application.Duplicatas;
using Clear.Domain.Arquivos;

namespace Clear.Infrastructure.Duplicatas;

/// <summary>
/// Acesso somente leitura, SHA-256 incremental via stream e confirmação
/// byte a byte. Restringe reparse points e rejeita metadados inconsistentes.
/// </summary>
public sealed class LeitorConteudoDuplicatasSistemaArquivos : ILeitorConteudoDuplicatas
{
    private const int BufferBytes = 64 * 1024;

    public async Task<string> CalcularSha256Async(
        ArquivoInventariado arquivo, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(arquivo);
        cancellationToken.ThrowIfCancellationRequested();

        VerificarAntesDaLeitura(arquivo);
        await using var fluxo = AbrirSomenteLeitura(arquivo);
        VerificarTamanhoAberto(fluxo, arquivo);

        var resumo = await SHA256.HashDataAsync(fluxo, cancellationToken).ConfigureAwait(false);

        // Mantém o handle aberto enquanto lê os metadados novamente.
        VerificarDepoisDaLeitura(arquivo, fluxo);
        return Convert.ToHexString(resumo);
    }

    public async Task<bool> CompararBytesAsync(
        ArquivoInventariado primeiro, ArquivoInventariado segundo,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(primeiro);
        ArgumentNullException.ThrowIfNull(segundo);
        cancellationToken.ThrowIfCancellationRequested();
        if (primeiro.TamanhoBytes != segundo.TamanhoBytes)
            return false;

        VerificarAntesDaLeitura(primeiro);
        VerificarAntesDaLeitura(segundo);

        await using var a = AbrirSomenteLeitura(primeiro);
        await using var b = AbrirSomenteLeitura(segundo);
        VerificarTamanhoAberto(a, primeiro);
        VerificarTamanhoAberto(b, segundo);

        var bufferA = new byte[BufferBytes];
        var bufferB = new byte[BufferBytes];
        long restante = primeiro.TamanhoBytes;
        var iguais = true;

        while (restante > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var bloco = (int)Math.Min(BufferBytes, restante);
            await a.ReadExactlyAsync(bufferA.AsMemory(0, bloco), cancellationToken)
                .ConfigureAwait(false);
            await b.ReadExactlyAsync(bufferB.AsMemory(0, bloco), cancellationToken)
                .ConfigureAwait(false);

            if (!bufferA.AsSpan(0, bloco).SequenceEqual(bufferB.AsSpan(0, bloco)))
            {
                iguais = false;
                break;
            }
            restante -= bloco;
        }

        VerificarDepoisDaLeitura(primeiro, a);
        VerificarDepoisDaLeitura(segundo, b);
        return iguais;
    }

    private static FileStream AbrirSomenteLeitura(ArquivoInventariado arquivo) =>
        new(arquivo.CaminhoCompleto, new FileStreamOptions
        {
            Mode = FileMode.Open,
            Access = FileAccess.Read,
            // Em Windows, evita escritores concorrentes durante a leitura.
            Share = FileShare.Read,
            BufferSize = BufferBytes,
            Options = FileOptions.Asynchronous | FileOptions.SequentialScan
        });

    private static void VerificarAntesDaLeitura(ArquivoInventariado arquivo)
    {
        var info = new FileInfo(arquivo.CaminhoCompleto);
        for (DirectoryInfo? diretorio = info.Directory;
             diretorio is not null; diretorio = diretorio.Parent)
        {
            if ((diretorio.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new IOException("O caminho contém uma pasta link/junction (reparse point).");
        }

        if ((info.Attributes & FileAttributes.ReparsePoint) != 0)
            throw new IOException("Não é permitido ler links simbólicos/reparse points.");
        if ((info.Attributes & FileAttributes.Directory) != 0)
            throw new IOException("A entrada informada não é um arquivo regular.");

        VerificarMetadados(info, arquivo);
    }

    private static void VerificarDepoisDaLeitura(ArquivoInventariado arquivo, FileStream fluxo)
    {
        VerificarTamanhoAberto(fluxo, arquivo);
        var info = new FileInfo(arquivo.CaminhoCompleto);
        if ((info.Attributes & FileAttributes.ReparsePoint) != 0)
            throw new IOException("A entrada tornou-se um link durante a análise.");
        VerificarMetadados(info, arquivo);
    }

    private static void VerificarTamanhoAberto(FileStream fluxo, ArquivoInventariado arquivo)
    {
        if (fluxo.Length != arquivo.TamanhoBytes)
            throw new IOException("O tamanho do arquivo mudou durante a análise.");
    }

    private static void VerificarMetadados(FileInfo info, ArquivoInventariado arquivo)
    {
        info.Refresh();
        if (info.Length != arquivo.TamanhoBytes)
            throw new IOException("O tamanho do arquivo mudou desde o inventário.");

        // Último acesso não é confiável para verificar estabilidade de conteúdo.
        if (arquivo.UltimaModificacaoUtc is DateTimeOffset esperado &&
            info.LastWriteTimeUtc != esperado.UtcDateTime)
            throw new IOException("A data de modificação mudou desde o inventário.");
    }
}
