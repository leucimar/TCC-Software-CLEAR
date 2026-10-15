using Clear.Domain.Inventario;

namespace Clear.Domain.Compatibilidade;

/// <summary>Estados distintos: nenhum significa autorização para migrar.</summary>
public enum EstadoVerificacao
{
    IncompatibilidadeDocumentada,
    Alerta,
    NaoAvaliado,
    SemIncompatibilidadeDetectada,
    Desabilitada,
    NaoAplicavel
}

/// <summary>Resultado de uma regra para uma entrada, em uma futura avaliação.</summary>
public sealed class ResultadoVerificacao
{
    public ResultadoVerificacao(
        string codigoRegra,
        EntradaInventariada entrada,
        EstadoVerificacao estado,
        string justificativa)
    {
        if (string.IsNullOrWhiteSpace(codigoRegra))
            throw new ArgumentException("A regra é obrigatória.", nameof(codigoRegra));
        ArgumentNullException.ThrowIfNull(entrada);
        if (!Enum.IsDefined(estado))
            throw new ArgumentOutOfRangeException(nameof(estado));
        if (string.IsNullOrWhiteSpace(justificativa))
            throw new ArgumentException("A justificativa é obrigatória.", nameof(justificativa));

        CodigoRegra = codigoRegra;
        Entrada = entrada;
        Estado = estado;
        Justificativa = justificativa;
    }

    public string CodigoRegra { get; }
    public EntradaInventariada Entrada { get; }
    public EstadoVerificacao Estado { get; }
    public string Justificativa { get; }
}

/// <summary>Modelo de execução futura. Não há implementações nesta parte 3A.</summary>
public interface IRegraCompatibilidade
{
    DefinicaoVerificacao Definicao { get; }
    ResultadoVerificacao Avaliar(EntradaInventariada entrada);
}

/// <summary>
/// Agregado conservador: somente resultados executados e sem pendências
/// permitem afirmar "nenhuma incompatibilidade detectada nas regras executadas".
/// </summary>
public sealed class ResumoVerificacoes
{
    public ResumoVerificacoes(IEnumerable<ResultadoVerificacao> resultados)
    {
        ArgumentNullException.ThrowIfNull(resultados);
        var itens = resultados.ToArray();
        if (itens.Any(item => item is null))
            throw new ArgumentException("Resultados nulos não são permitidos.", nameof(resultados));

        Resultados = Array.AsReadOnly(itens);
    }

    public IReadOnlyList<ResultadoVerificacao> Resultados { get; }
    public bool AlgumaVerificacaoExecutada => Resultados.Any(r => r.Estado is
        EstadoVerificacao.IncompatibilidadeDocumentada or
        EstadoVerificacao.Alerta or
        EstadoVerificacao.SemIncompatibilidadeDetectada);

    public bool ApenasSemIncompatibilidadeDetectada =>
        Resultados.Count > 0 &&
        Resultados.All(r => r.Estado == EstadoVerificacao.SemIncompatibilidadeDetectada);
}
