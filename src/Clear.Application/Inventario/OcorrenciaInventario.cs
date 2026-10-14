namespace Clear.Application.Inventario;

/// <summary>Falha ou item ignorado durante a leitura de metadados.</summary>
public sealed record OcorrenciaInventario(string Caminho, string Tipo, string Descricao);

/// <summary>
/// Interface opcional para fontes que fornecem ocorrências além dos arquivos.
/// Os dados são consultados após o término da enumeração.
/// </summary>
public interface IFonteInventarioComOcorrencias : IFonteInventario
{
    IReadOnlyList<OcorrenciaInventario> Ocorrencias { get; }
    long TotalOcorrencias { get; }
}

/// <summary>Estado resumido para a interface; não representa porcentagem concluída.</summary>
public sealed record ProgressoInventario(long QuantidadeArquivos, long TotalBytes);
