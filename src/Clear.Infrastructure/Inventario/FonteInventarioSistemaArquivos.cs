using System.Runtime.CompilerServices;
using System.Security;
using Clear.Application.Inventario;
using Clear.Domain.Arquivos;
using Clear.Domain.Inventario;

namespace Clear.Infrastructure.Inventario;

/// <summary>
/// Enumera arquivos e subpastas uma única vez, somente lendo metadados.
/// Não segue reparse points, nem modifica ou abre conteúdo dos arquivos.
/// </summary>
public sealed class FonteInventarioSistemaArquivos :
    IFonteInventarioComOcorrencias, IFonteInventarioComEntradas
{
    private const int MaximoOcorrenciasExibidas = 100;
    private readonly List<OcorrenciaInventario> _ocorrencias = new();
    private long _totalOcorrencias;
    private int _emExecucao;

    public IReadOnlyList<OcorrenciaInventario> Ocorrencias =>
        Array.AsReadOnly(_ocorrencias.ToArray());

    public long TotalOcorrencias => _totalOcorrencias;

    // Mantém o contrato original do M1–M3 para consumidores de apenas arquivos.
    public async IAsyncEnumerable<ArquivoInventariado> EnumerarAsync(
        string diretorioRaiz,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var entrada in EnumerarEntradasAsync(diretorioRaiz, cancellationToken)
                           .WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            if (entrada.Arquivo is { } arquivo)
                yield return arquivo;
        }
    }

    public async IAsyncEnumerable<EntradaInventariada> EnumerarEntradasAsync(
        string diretorioRaiz,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (Interlocked.CompareExchange(ref _emExecucao, 1, 0) != 0)
            throw new InvalidOperationException("A fonte já está executando um inventário.");

        try
        {
            _ocorrencias.Clear();
            _totalOcorrencias = 0;

            if (string.IsNullOrWhiteSpace(diretorioRaiz))
                throw new ArgumentException("O diretório raiz é obrigatório.", nameof(diretorioRaiz));
            if (!Path.IsPathFullyQualified(diretorioRaiz))
                throw new ArgumentException("Informe um caminho absoluto.", nameof(diretorioRaiz));

            cancellationToken.ThrowIfCancellationRequested();

            var raiz = new DirectoryInfo(Path.GetFullPath(diretorioRaiz));
            if (!raiz.Exists)
                throw new DirectoryNotFoundException("Diretório raiz inexistente: " + raiz.FullName);

            // Omitir links selecionados como raiz ou existentes em seus ancestrais.
            for (DirectoryInfo? componente = raiz; componente is not null; componente = componente.Parent)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if ((componente.Attributes & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("A raiz ou um ancestral é link/junction; escolha outra pasta.");
            }

            var pendentes = new Stack<DirectoryInfo>();
            pendentes.Push(raiz);
            long entradasEnumeradas = 0;

            while (pendentes.Count > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var diretorio = pendentes.Pop();

                IEnumerator<FileSystemInfo> enumerador;
                try
                {
                    enumerador = diretorio.EnumerateFileSystemInfos().GetEnumerator();
                }
                catch (Exception ex) when (Recuperavel(ex))
                {
                    Registrar(diretorio.FullName, "DiretorioInacessivel", ex.Message);
                    continue;
                }

                using (enumerador)
                {
                    while (true)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        FileSystemInfo entrada;
                        try
                        {
                            if (!enumerador.MoveNext())
                                break;
                            entrada = enumerador.Current;
                        }
                        catch (Exception ex) when (Recuperavel(ex))
                        {
                            Registrar(diretorio.FullName, "EnumeracaoInterrompida", ex.Message);
                            break;
                        }

                        EntradaInventariada? item = null;
                        try
                        {
                            var atributos = entrada.Attributes;
                            if ((atributos & FileAttributes.ReparsePoint) != 0)
                            {
                                Registrar(entrada.FullName, "LinkIgnorado",
                                    "Reparse point/junction/link não seguido.");
                                continue;
                            }

                            if ((atributos & FileAttributes.Directory) != 0)
                            {
                                var subpasta = new DirectoryInfo(entrada.FullName);
                                item = EntradaInventariada.DeDiretorio(subpasta.FullName);
                                pendentes.Push(subpasta);
                            }
                            else
                            {
                                var info = new FileInfo(entrada.FullName);
                                var arquivo = new ArquivoInventariado(
                                    info.FullName,
                                    info.Length,
                                    new DateTimeOffset(info.LastWriteTimeUtc, TimeSpan.Zero),
                                    new DateTimeOffset(info.LastAccessTimeUtc, TimeSpan.Zero));
                                item = EntradaInventariada.DeArquivo(arquivo);
                            }
                        }
                        catch (Exception ex) when (Recuperavel(ex))
                        {
                            Registrar(entrada.FullName, "EntradaInacessivel", ex.Message);
                            continue;
                        }

                        if (item is not null)
                        {
                            yield return item;
                            entradasEnumeradas = checked(entradasEnumeradas + 1);
                            if (entradasEnumeradas % 64 == 0)
                                await Task.Yield();
                        }
                    }
                }
            }
        }
        finally
        {
            Volatile.Write(ref _emExecucao, 0);
        }
    }

    private void Registrar(string caminho, string tipo, string descricao)
    {
        _totalOcorrencias = checked(_totalOcorrencias + 1);
        if (_ocorrencias.Count < MaximoOcorrenciasExibidas)
            _ocorrencias.Add(new OcorrenciaInventario(caminho, tipo, descricao));
    }

    private static bool Recuperavel(Exception erro) =>
        erro is UnauthorizedAccessException or IOException or SecurityException;
}
