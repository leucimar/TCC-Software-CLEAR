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
        int diasSemAcesso, DateTimeOffset referencia)
    {
        return new ClassificadorArquivos(new IRegraClassificacao[]
        {
            new RegraArquivoVazio(),
            new RegraNomeTemporarioBackup(),
            new RegraSinalTemporal(diasSemAcesso, referencia)
        });
    }

    /// <summary>
    /// Monta exclusivamente as verificações gerais escolhidas na interface.
    /// Não ativa SHA-256: a deduplicação possui caso de uso próprio e opt-in.
    /// </summary>
    public static ClassificadorArquivos CriarComFiltrosSelecionados(
        bool identificarVazios, bool identificarTemporarios,
        int? diasSemAcesso, DateTimeOffset referencia)
    {
        var regras = new List<IRegraClassificacao>();
        if (identificarVazios)
            regras.Add(new RegraArquivoVazio());
        if (identificarTemporarios)
            regras.Add(new RegraNomeTemporarioBackup());
        if (diasSemAcesso is int dias)
            regras.Add(new RegraSinalTemporal(dias, referencia));
        return new ClassificadorArquivos(regras);
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
