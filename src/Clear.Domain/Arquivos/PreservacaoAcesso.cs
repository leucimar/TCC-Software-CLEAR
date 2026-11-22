namespace Clear.Domain.Arquivos;

public enum PoliticaSemPreservacao { NaoLerConteudo, LerComAviso }

public sealed record OcorrenciaPreservacaoAcesso(string Caminho, string Estado,
    string Motivo, DateTimeOffset RegistradoUtc);

/// <summary>Accepted per-handle requests do not freeze other applications' accesses.</summary>
public sealed record ResumoPreservacaoAcesso(string Politica, long AberturasProtegidas,
    long LeiturasSemGarantia, long LeiturasIgnoradas,
    IReadOnlyList<OcorrenciaPreservacaoAcesso> Ocorrencias,
    string? CriterioTemporal = null);
