namespace Clear.Infrastructure.Historico;

/// <summary>
/// Snapshot auditável de filtros e ambiente usado na execução.
/// Não é chave de cache nem autoriza reaproveitamento de resultados.
/// </summary>
public sealed record ParametrosHistoricoM6(
    IReadOnlyList<string> FiltrosGeraisHabilitados,
    string? AmbienteDestino,
    string? SubtipoDestino,
    IReadOnlyList<string> VerificacoesHabilitadas,
    long? LimitePersonalizadoBytes,
    int? DiasSinalTemporal,
    DateTimeOffset? ReferenciaTemporalUtc,
    bool DetectarDuplicatas,
    string? CriterioTemporal = null,
    string PoliticaPreservacaoAcesso = "NaoLerConteudo",
    string? CatalogoDestino = null, string? CaminhoBibliotecaDecodificado = null,
    string? SubpastaDestino = null, int? LeitoresSha256 = null,
    int? LimiteIndice = null, bool? ExportarRelatorio = null,
    bool? RegistrarProgresso = null)
{
    public static ParametrosHistoricoM6 SemFiltros { get; } = new(
        Array.Empty<string>(), null, null, Array.Empty<string>(), null, null, null, false);
}

public sealed record ResumoExecucaoHistoricaM6(
    string Id, DateTimeOffset InicioUtc, DateTimeOffset FimUtc,
    string DiretorioRaiz, string Estado, long QuantidadeArquivos,
    long QuantidadeDiretorios, long TotalBytes, long EntradasPersistidas,
    long GruposDuplicatas, long TotalOcorrencias,
    string? ParametrosJson = null, string? ResumoJson = null, long Sequencia = 0);
