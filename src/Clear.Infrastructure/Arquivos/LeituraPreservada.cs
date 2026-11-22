using System.Collections.Concurrent;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Win32.SafeHandles;
using Clear.Domain.Arquivos;

[assembly: InternalsVisibleTo("Clear.Infrastructure.Tests")]

namespace Clear.Infrastructure.Arquivos;

public sealed class FalhaPreservacaoAcessoException : IOException
{
    public FalhaPreservacaoAcessoException(string mensagem, Exception? inner=null) : base(mensagem,inner) { }
}

/// <summary>Execution-local policy; flows into Task.Run/parallel hashing, never a global override.</summary>
public sealed class SessaoPreservacaoAcesso : IDisposable
{
    private static readonly AsyncLocal<SessaoPreservacaoAcesso?> Corrente = new();
    private readonly SessaoPreservacaoAcesso? _anterior;
    private readonly ConcurrentQueue<OcorrenciaPreservacaoAcesso> _ocorrencias = new();
    private readonly Func<string,FileShare,int,bool,FileStream> _abrir;
    private long _protegidas, _semGarantia, _ignoradas;
    private bool _disposed;
    private StreamWriter? _diario;
    private readonly object _travaDiario = new();
    private string? _criterioTemporal;
    public PoliticaSemPreservacao Politica { get; }
    public SessaoPreservacaoAcesso(PoliticaSemPreservacao politica)
        : this(politica, AbrirNativo) { }
    public SessaoPreservacaoAcesso(PoliticaSemPreservacao politica, string diario, string? criterioTemporal = null)
        : this(politica, AbrirNativo)
    {
        try
        {
            _criterioTemporal = criterioTemporal;
            _diario = new StreamWriter(new FileStream(diario, FileMode.CreateNew,
                FileAccess.Write, FileShare.Read), new System.Text.UTF8Encoding(true));
            Registrar(new("", "Inicio", "Política autorizada: " + politica +
                ". Critério temporal: " + criterioTemporal + ". A proteção se refere às leituras deste processo, não a acessos externos.", DateTimeOffset.UtcNow));
        }
        catch { Dispose(); throw; }
    }
    internal SessaoPreservacaoAcesso(PoliticaSemPreservacao politica,
        Func<string,FileShare,int,bool,FileStream> abrir)
    {
        if(!Enum.IsDefined(politica))throw new ArgumentOutOfRangeException(nameof(politica));
        Politica=politica;_abrir=abrir;_anterior=Corrente.Value;Corrente.Value=this;
    }
    public ResumoPreservacaoAcesso Resumo => new(Politica.ToString(),
        Interlocked.Read(ref _protegidas),Interlocked.Read(ref _semGarantia),
        Interlocked.Read(ref _ignoradas),_ocorrencias.ToArray(),_criterioTemporal);

    public static FileStream Abrir(string caminho, FileShare share=FileShare.Read, int buffer=65536)
    {
        var sessao=Corrente.Value;
        var abrir=sessao?._abrir ?? AbrirNativo;
        try
        {
            var stream=abrir(caminho,share,buffer,true);
            if(sessao is not null)Interlocked.Increment(ref sessao._protegidas);
            return stream;
        }
        catch(FalhaPreservacaoAcessoException erro)
        {
            if(sessao?.Politica==PoliticaSemPreservacao.LerComAviso)
            {
                // Only consent captured before this execution permits unprotected content reads.
                sessao.Registrar(new(caminho,"FallbackAutorizado",erro.Message,DateTimeOffset.UtcNow));
                FileStream stream;
                try { stream=abrir(caminho,share,buffer,false); }
                catch (Exception falha) when (falha is IOException or UnauthorizedAccessException or System.Security.SecurityException)
                {
                    Interlocked.Increment(ref sessao._ignoradas);
                    var ocorrencia = new OcorrenciaPreservacaoAcesso(caminho,"FallbackFalhou",erro.Message + " Leitura também falhou: " + falha.Message,DateTimeOffset.UtcNow);
                    sessao._ocorrencias.Enqueue(ocorrencia); sessao.Registrar(ocorrencia);
                    throw;
                }
                Interlocked.Increment(ref sessao._semGarantia);
                sessao._ocorrencias.Enqueue(new(caminho,"LeituraSemGarantia",erro.Message,DateTimeOffset.UtcNow));
                return stream;
            }
            if(sessao is not null)
            {
                Interlocked.Increment(ref sessao._ignoradas);
                var ocorrencia = new OcorrenciaPreservacaoAcesso(caminho,"ConteudoIgnorado",erro.Message,DateTimeOffset.UtcNow);
                sessao._ocorrencias.Enqueue(ocorrencia);
                sessao.Registrar(ocorrencia);
            }
            throw;
        }
    }

    private void Registrar(OcorrenciaPreservacaoAcesso ocorrencia)
    {
        lock (_travaDiario)
        {
            if (_diario is null) return;
            // Failure to record consent prevents the unprotected read.
            _diario.WriteLine(JsonSerializer.Serialize(ocorrencia));
            _diario.Flush();
        }
    }

    public async Task SalvarAsync(string destino, CancellationToken token=default)
    {
        var path=Path.GetFullPath(destino);Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        // Dedicated audit output outside the content root is selected by the caller.
        await using var file=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.Read);
        await JsonSerializer.SerializeAsync(file,Resumo,new JsonSerializerOptions{WriteIndented=true},token);
        await using var csv = new StreamWriter(new FileStream(Path.ChangeExtension(path,"csv"),
            FileMode.CreateNew,FileAccess.Write,FileShare.Read),new System.Text.UTF8Encoding(true));
        await csv.WriteLineAsync("Caminho;Estado;Motivo;RegistradoUtc");
        foreach(var item in Resumo.Ocorrencias)
        {
            token.ThrowIfCancellationRequested();
            static string Q(string s) => "\"" + (s.Length>0 && "=+-@\t\r".Contains(s[0]) ? "'" : "") + s.Replace("\"","\"\"") + "\"";
            await csv.WriteLineAsync(string.Join(";",new[]{item.Caminho,item.Estado,item.Motivo,item.RegistradoUtc.ToString("O")}.Select(Q)));
        }
    }

    private static FileStream AbrirNativo(string caminho,FileShare share,int buffer,bool preservar)
    {
        if(!OperatingSystem.IsWindows())
        {
            if(preservar)throw new FalhaPreservacaoAcessoException("Preservação do último acesso indisponível neste sistema; conteúdo não lido sem autorização.");
            return new FileStream(caminho,new FileStreamOptions{Mode=FileMode.Open,Access=FileAccess.Read,Share=share,BufferSize=buffer,Options=FileOptions.Asynchronous|FileOptions.SequentialScan});
        }
        const uint genericRead=0x80000000,writeAttributes=0x100;
        const uint flags=0x40000000|0x08000000|0x00200000; // overlapped, sequential, open reparse point itself
        var handle=CreateFileW(CaminhoParaAberturaWindows(caminho),genericRead|(preservar?writeAttributes:0),
            (uint)share,IntPtr.Zero,3,flags,IntPtr.Zero);
        if(handle.IsInvalid)
        {
            var error=Marshal.GetLastWin32Error();handle.Dispose();
            var detail=new Win32Exception(error);
            if(preservar)throw new FalhaPreservacaoAcessoException(
                $"Não foi possível abrir com preservação de acesso ({error}): {detail.Message}",detail);
            throw new IOException($"Falha na leitura ({error}): {detail.Message}",detail);
        }
        try
        {
            if(preservar)
            {
                var sentinel=new NativeFileTime{Low=uint.MaxValue,High=uint.MaxValue};
                if(!SetFileTime(handle,IntPtr.Zero,ref sentinel,IntPtr.Zero))
                    throw new FalhaPreservacaoAcessoException("O Windows não aceitou a preservação do último acesso: "+new Win32Exception(Marshal.GetLastWin32Error()).Message);
            }
            if(!GetFileInformationByHandle(handle,out var info))throw new IOException("Não foi possível validar o arquivo aberto.",new Win32Exception(Marshal.GetLastWin32Error()));
            if((info.Attributes&(0x400u|0x10u))!=0)throw new IOException("Leitura de links/reparse points ou diretórios não permitida.");
            // Read-only FileStream even though the handle also permits attribute requests.
            return new FileStream(handle,FileAccess.Read,buffer,true);
        }
        catch{handle.Dispose();throw;}
    }

    internal static string CaminhoParaAberturaWindows(string caminho)
    {
        const int maxPath = 260; // Inclui o terminador nulo.
        const string prefixoEstendido = @"\\?\";
        if (caminho.StartsWith(prefixoEstendido, StringComparison.Ordinal) ||
            caminho.StartsWith(@"\\.\", StringComparison.Ordinal))
            return caminho;

        // O prefixo estendido desativa a normalização feita pelo Win32.
        var absoluto = Path.GetFullPath(caminho);
        if (absoluto.Length < maxPath)
            return absoluto;

        return absoluto.StartsWith(@"\\", StringComparison.Ordinal)
            ? @"\\?\UNC\" + absoluto[2..]
            : prefixoEstendido + absoluto;
    }

    public void Dispose()
    {
        if(_disposed)return;
        if(ReferenceEquals(Corrente.Value,this))Corrente.Value=_anterior;
        _diario?.Dispose();
        _disposed=true;
    }
    [StructLayout(LayoutKind.Sequential)] private struct NativeFileTime {public uint Low,High;}
    [StructLayout(LayoutKind.Sequential)] private struct NativeFileInfo
    { public uint Attributes; public NativeFileTime Creation,Access,Write;public uint Serial,SizeHigh,SizeLow,Links,IndexHigh,IndexLow; }
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]
    private static extern SafeFileHandle CreateFileW(string name,uint access,uint share,IntPtr security,uint disposition,uint flags,IntPtr template);
    [DllImport("kernel32.dll",SetLastError=true)] [return:MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetFileTime(SafeFileHandle file,IntPtr creation,ref NativeFileTime access,IntPtr write);
    [DllImport("kernel32.dll",SetLastError=true)] [return:MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileInformationByHandle(SafeFileHandle file,out NativeFileInfo info);
}
