namespace Clear.Domain.Arquivos;

/// <summary>Metadados recebidos de uma análise; não acessa o arquivo físico.</summary>
public sealed class ArquivoInventariado
{
    public ArquivoInventariado(
        string caminhoCompleto,
        long tamanhoBytes,
        DateTimeOffset? ultimaModificacaoUtc = null,
        DateTimeOffset? ultimoAcessoUtc = null)
    {
        if (string.IsNullOrWhiteSpace(caminhoCompleto))
            throw new ArgumentException("O caminho é obrigatório.", nameof(caminhoCompleto));
        if (tamanhoBytes < 0)
            throw new ArgumentOutOfRangeException(nameof(tamanhoBytes));

        CaminhoCompleto = caminhoCompleto;
        TamanhoBytes = tamanhoBytes;
        UltimaModificacaoUtc = ultimaModificacaoUtc?.ToUniversalTime();
        UltimoAcessoUtc = ultimoAcessoUtc?.ToUniversalTime();
    }

    public string CaminhoCompleto { get; }
    public long TamanhoBytes { get; }
    public DateTimeOffset? UltimaModificacaoUtc { get; }
    public DateTimeOffset? UltimoAcessoUtc { get; }
}
