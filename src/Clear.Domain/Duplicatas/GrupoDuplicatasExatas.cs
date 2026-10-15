using Clear.Domain.Arquivos;

namespace Clear.Domain.Duplicatas;

/// <summary>
/// Grupo de conteúdo idêntico após pré-filtro por tamanho, SHA-256 em fluxo
/// e comparação direta de bytes. Não seleciona cópia para descarte.
/// </summary>
public sealed class GrupoDuplicatasExatas
{
    public GrupoDuplicatasExatas(long tamanhoBytes, string sha256Hex,
        IEnumerable<ArquivoInventariado> arquivos)
    {
        if (tamanhoBytes < 0)
            throw new ArgumentOutOfRangeException(nameof(tamanhoBytes));
        if (string.IsNullOrWhiteSpace(sha256Hex) || sha256Hex.Length != 64 ||
            sha256Hex.Any(c => !Uri.IsHexDigit(c)))
            throw new ArgumentException("O SHA-256 deve ter 64 dígitos hexadecimais.", nameof(sha256Hex));
        ArgumentNullException.ThrowIfNull(arquivos);
        var itens = arquivos.ToArray();
        if (itens.Length < 2 || itens.Any(a => a is null || a.TamanhoBytes != tamanhoBytes))
            throw new ArgumentException("Um grupo exige dois ou mais arquivos do mesmo tamanho.", nameof(arquivos));
        if (itens.Select(a => a.CaminhoCompleto).Distinct(StringComparer.OrdinalIgnoreCase).Count() != itens.Length)
            throw new ArgumentException("Um caminho não pode aparecer duas vezes no mesmo grupo.", nameof(arquivos));

        TamanhoBytes = tamanhoBytes;
        Sha256Hex = sha256Hex.ToUpperInvariant();
        Arquivos = Array.AsReadOnly(itens);
        BytesPotencialmenteRedundantes = checked((itens.LongLength - 1) * tamanhoBytes);
    }

    public long TamanhoBytes { get; }
    public string Sha256Hex { get; }
    public IReadOnlyList<ArquivoInventariado> Arquivos { get; }
    public long BytesPotencialmenteRedundantes { get; }
}
