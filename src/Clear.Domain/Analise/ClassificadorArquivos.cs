using Clear.Domain.Arquivos;

namespace Clear.Domain.Analise;

/// <summary>
/// Aplica todas as regras independentes. Suporta múltiplas categorias e justificativas.
/// Não executa operações sobre arquivos, nem escolhe arquivos para remoção.
/// </summary>
public sealed class ClassificadorArquivos
{
    private readonly IReadOnlyList<IRegraClassificacao> _regras;

    public ClassificadorArquivos(IEnumerable<IRegraClassificacao>? regras = null)
    {
        var itens = (regras ??
            new IRegraClassificacao[] { new RegraArquivoVazio(), new RegraNomeTemporarioBackup() })
            .ToArray();

        if (itens.Any(regra => regra is null))
            throw new ArgumentException("As regras não podem ser nulas.", nameof(regras));

        _regras = Array.AsReadOnly(itens);
    }

    /// <summary>
    /// Ativa a análise temporal mediante decisão explícita do operador.
    /// A referência é congelada uma única vez para toda a execução.
    /// </summary>
    public static ClassificadorArquivos CriarComSinalTemporal(
        int diasSemModificacao, DateTimeOffset referencia)
    {
        return new ClassificadorArquivos(new IRegraClassificacao[]
        {
            new RegraArquivoVazio(),
            new RegraNomeTemporarioBackup(),
            new RegraSinalTemporal(diasSemModificacao, referencia)
        });
    }

    public ResultadoAnaliseArquivo Classificar(ArquivoInventariado arquivo)
    {
        ArgumentNullException.ThrowIfNull(arquivo);

        var evidencias = new List<EvidenciaAnalise>();

        foreach (var regra in _regras)
        {
            var evidencia = regra.Avaliar(arquivo);
            if (evidencia is not null)
                evidencias.Add(evidencia);
        }

        return new ResultadoAnaliseArquivo(arquivo, evidencias);
    }
}
