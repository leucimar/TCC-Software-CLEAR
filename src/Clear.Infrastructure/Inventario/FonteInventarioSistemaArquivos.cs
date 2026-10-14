using System.Runtime.CompilerServices;
using System.Security;
using Clear.Application.Inventario;
using Clear.Domain.Arquivos;

namespace Clear.Infrastructure.Inventario;

/// <summary>
/// Enumeração iterativa somente leitura. Não abre conteúdos, não segue reparse
/// points identificados e não modifica, renomeia ou remove arquivos/diretórios.
/// </summary>
public sealed class FonteInventarioSistemaArquivos : IFonteInventarioComOcorrencias
{
    private const int MaximoOcorrenciasExibidas = 100;
    private readonly List<OcorrenciaInventario> _ocorrencias = new();
    private long _totalOcorrencias;
    private int _emExecucao;

    public IReadOnlyList<OcorrenciaInventario> Ocorrencias =>
        Array.AsReadOnly(_ocorrencias.ToArray());

    public long TotalOcorrencias => _totalOcorrencias;

    public async IAsyncEnumerable<ArquivoInventariado> EnumerarAsync(
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

            // Não aceitar caminho escolhido sob um ancestral explicitamente marcado
            // como reparse point (junction ou link). Isso evita seguir esses links
            // silenciosamente antes de iniciar a travessia.
            for (DirectoryInfo? componente = raiz; componente is not null; componente = componente.Parent)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if ((componente.Attributes & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("A raiz ou um ancestral é um link/junction; escolha outra pasta de teste.");
            }

            var pendentes = new Stack<DirectoryInfo>();
            pendentes.Push(raiz);
            long arquivosEnumerados = 0;

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

                        ArquivoInventariado arquivo;
                        try
                        {
                            var atributos = entrada.Attributes;

                            if ((atributos & FileAttributes.ReparsePoint) != 0)
                            {
                                Registrar(entrada.FullName, "LinkIgnorado",
                                    "Link simbólico/junction/reparse point não seguido.");
                                continue;
                            }

                            if ((atributos & FileAttributes.Directory) != 0)
                            {
                                pendentes.Push(new DirectoryInfo(entrada.FullName));
                                continue;
                            }

                            var info = new FileInfo(entrada.FullName);
                            arquivo = new ArquivoInventariado(
                                info.FullName,
                                info.Length,
                                new DateTimeOffset(info.LastWriteTimeUtc, TimeSpan.Zero),
                                new DateTimeOffset(info.LastAccessTimeUtc, TimeSpan.Zero));
                        }
                        catch (Exception ex) when (Recuperavel(ex))
                        {
                            Registrar(entrada.FullName, "ArquivoInacessivel", ex.Message);
                            continue;
                        }

                        yield return arquivo;
                        arquivosEnumerados = checked(arquivosEnumerados + 1);

                        // Mantém a enumeração responsiva ao cancelamento, sem abrir o arquivo.
                        if (arquivosEnumerados % 64 == 0)
                            await Task.Yield();
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
