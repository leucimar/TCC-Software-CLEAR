using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace Clear.Application.Amostragem;

/// <summary>
/// Metadados de um arquivo; nunca inclui conteúdo ou classificação reutilizada.
/// O caminho relativo é sempre relativo à raiz explicitamente inventariada.
/// </summary>
public sealed record ArquivoCandidatoAmostra(
    string CaminhoRelativo, long TamanhoBytes, DateTime UltimaModificacaoUtc)
{
    public string Formato
    {
        get
        {
            var extensao = Path.GetExtension(CaminhoRelativo);
            return string.IsNullOrWhiteSpace(extensao)
                ? "[sem extensão]" : extensao.ToLowerInvariant();
        }
    }

    public string GrupoOrigem
    {
        get
        {
            var indice = CaminhoRelativo.IndexOfAny(['/', '\\']);
            return indice < 0 ? "[raiz]" : CaminhoRelativo[..indice];
        }
    }

    public string FaixaTamanho => TamanhoBytes switch
    {
        0 => "0",
        <= 64 * 1024 => "1-64KiB",
        <= 1024 * 1024 => "64KiB-1MiB",
        <= 16 * 1024 * 1024 => "1-16MiB",
        <= 256 * 1024 * 1024 => "16-256MiB",
        _ => ">256MiB"
    };

    public string Estrato => GrupoOrigem + "\u001f" + Formato + "\u001f" + FaixaTamanho;
}

public sealed record ResumoEstratoAmostra(
    string GrupoOrigem, string Formato, string FaixaTamanho,
    int QuantidadeOriginal, int QuantidadeSelecionada,
    long BytesOriginais, long BytesSelecionados);

public sealed class PlanoAmostraEstratificada
{
    public PlanoAmostraEstratificada(
        IReadOnlyList<ArquivoCandidatoAmostra> originais,
        IReadOnlyList<ArquivoCandidatoAmostra> selecionados,
        IReadOnlyList<ResumoEstratoAmostra> estratos,
        decimal percentual, int semente)
    {
        Originais = originais;
        Selecionados = selecionados;
        Estratos = estratos;
        PercentualSolicitado = percentual;
        Semente = semente;
        BytesOriginais = originais.Aggregate(0L,
            (total, f) => checked(total + f.TamanhoBytes));
        BytesSelecionados = selecionados.Aggregate(0L,
            (total, f) => checked(total + f.TamanhoBytes));
    }

    public IReadOnlyList<ArquivoCandidatoAmostra> Originais { get; }
    public IReadOnlyList<ArquivoCandidatoAmostra> Selecionados { get; }
    public IReadOnlyList<ResumoEstratoAmostra> Estratos { get; }
    public decimal PercentualSolicitado { get; }
    public int Semente { get; }
    public long BytesOriginais { get; }
    public long BytesSelecionados { get; }
    public decimal PercentualArquivosReal =>
        Originais.Count == 0 ? 0 : 100M * Selecionados.Count / Originais.Count;
    public decimal PercentualBytesReal =>
        BytesOriginais == 0 ? 0 : 100M * BytesSelecionados / BytesOriginais;
}

/// <summary>
/// Amostragem determinística por pacote/pasta principal, extensão e faixa de
/// tamanho. Preserva representação de cada estrato e busca simultaneamente
/// ~p% dos arquivos e ~p% dos bytes, sem prometer erro zero no volume.
/// O código não acessa nem modifica o sistema de arquivos.
/// </summary>
public static class PlanejadorAmostraEstratificada
{
    private sealed class Grupo(string chave, ArquivoCandidatoAmostra[] arquivos,
        decimal quotaIdeal, int quota)
    {
        public string Chave { get; } = chave;
        public ArquivoCandidatoAmostra[] Arquivos { get; } = arquivos;
        public decimal QuotaIdeal { get; } = quotaIdeal;
        public int Quota { get; set; } = quota;
    }

    public static PlanoAmostraEstratificada Planejar(
        IEnumerable<ArquivoCandidatoAmostra> arquivos,
        decimal percentual = 30M, int semente = 5721)
    {
        ArgumentNullException.ThrowIfNull(arquivos);
        if (percentual is <= 0 or > 100)
            throw new ArgumentOutOfRangeException(nameof(percentual),
                "Percentual deve ser maior que 0 e menor ou igual a 100.");
        if (semente < 0)
            throw new ArgumentOutOfRangeException(nameof(semente));

        var originais = arquivos.OrderBy(x => x.CaminhoRelativo,
            StringComparer.Ordinal).ToArray();
        if (originais.Length == 0)
            throw new ArgumentException("O acervo não possui arquivos regulares.", nameof(arquivos));

        // Windows não permite dois destinos que diferem apenas na caixa.
        var caminhos = new HashSet<string>(
            OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase
                                        : StringComparer.Ordinal);
        foreach (var arquivo in originais)
        {
            ArgumentNullException.ThrowIfNull(arquivo);
            if (arquivo.TamanhoBytes < 0 || string.IsNullOrWhiteSpace(arquivo.CaminhoRelativo)
                || Path.IsPathRooted(arquivo.CaminhoRelativo)
                || arquivo.CaminhoRelativo.Split('/', '\\')
                    .Any(parte => parte is "" or "." or "..")
                || !caminhos.Add(arquivo.CaminhoRelativo))
                throw new ArgumentException("Metadado inválido, caminho duplicado ou inseguro.",
                    nameof(arquivos));
        }

        var metaArquivos = checked((int)decimal.Round(
            originais.Length * percentual / 100M, 0, MidpointRounding.AwayFromZero));
        var grupos = originais.GroupBy(x => x.Estrato, StringComparer.Ordinal)
            .OrderBy(x => x.Key, StringComparer.Ordinal)
            .Select(g =>
            {
                var candidatos = g.ToArray();
                var ideal = candidatos.Length * percentual / 100M;
                return new Grupo(g.Key, candidatos, ideal,
                    Math.Clamp((int)decimal.Floor(ideal), 1, candidatos.Length));
            }).ToArray();

        if (grupos.Length > metaArquivos)
            throw new InvalidOperationException(
                "O percentual solicitado é pequeno demais para representar todos " +
                "os estratos (origem, formato e faixa de tamanho). Aumente o percentual.");

        var alocados = grupos.Sum(x => x.Quota);
        // Arredondamento global com cotas ajustadas: quantidade exata de arquivos.
        while (alocados < metaArquivos)
        {
            var candidato = grupos.Where(x => x.Quota < x.Arquivos.Length)
                .OrderByDescending(x => x.QuotaIdeal - x.Quota)
                .ThenBy(x => x.Chave, StringComparer.Ordinal).FirstOrDefault()
                ?? throw new InvalidOperationException("Não é possível completar a amostra.");
            candidato.Quota++;
            alocados++;
        }
        while (alocados > metaArquivos)
        {
            var candidato = grupos.Where(x => x.Quota > 1)
                .OrderByDescending(x => x.Quota - x.QuotaIdeal)
                .ThenBy(x => x.Chave, StringComparer.Ordinal).FirstOrDefault()
                ?? throw new InvalidOperationException("Não é possível reduzir a amostra.");
            candidato.Quota--;
            alocados--;
        }

        var selecionados = new List<ArquivoCandidatoAmostra>(metaArquivos);
        var resumos = new List<ResumoEstratoAmostra>(grupos.Length);
        foreach (var grupo in grupos)
        {
            var ordenados = grupo.Arquivos.OrderBy(x => x.TamanhoBytes)
                .ThenBy(x => x.CaminhoRelativo, StringComparer.Ordinal).ToArray();
            var totalBytes = ordenados.Aggregate(0L,
                (total, x) => checked(total + x.TamanhoBytes));
            var metaBytes = totalBytes * percentual / 100M;

            // Amostragem sistemática nos quantis de tamanho: não concentra
            // acidentalmente só os menores ou só os maiores arquivos.
            // Testa 17 fases determinísticas e prefere a mais próxima dos
            // bytes-alvo. Não há divisão de arquivos ou cópia parcial.
            var faseInicial = Fase(semente: semente, chave: grupo.Chave);
            decimal melhorErro = decimal.MaxValue;
            ArquivoCandidatoAmostra[] melhor = [];
            long bytesEscolhidos = 0;
            for (var rodada = 0; rodada < 17; rodada++)
            {
                var fase = ((faseInicial + rodada) % 17 + 0.5M) / 17M;
                var amostra = new ArquivoCandidatoAmostra[grupo.Quota];
                long soma = 0;
                for (var i = 0; i < amostra.Length; i++)
                {
                    var indice = (int)decimal.Floor(
                        (i + fase) * ordenados.Length / grupo.Quota);
                    var escolhido = ordenados[Math.Min(indice, ordenados.Length - 1)];
                    amostra[i] = escolhido;
                    soma = checked(soma + escolhido.TamanhoBytes);
                }
                var erro = Math.Abs(soma - metaBytes);
                if (erro < melhorErro)
                {
                    melhorErro = erro;
                    melhor = amostra;
                    bytesEscolhidos = soma;
                }
            }
            selecionados.AddRange(melhor);
            resumos.Add(new ResumoEstratoAmostra(
                grupo.Arquivos[0].GrupoOrigem, grupo.Arquivos[0].Formato,
                grupo.Arquivos[0].FaixaTamanho, grupo.Arquivos.Length, melhor.Length,
                totalBytes, bytesEscolhidos));
        }

        return new PlanoAmostraEstratificada(
            Array.AsReadOnly(originais),
            Array.AsReadOnly(selecionados.OrderBy(x => x.CaminhoRelativo,
                StringComparer.Ordinal).ToArray()),
            Array.AsReadOnly(resumos.ToArray()),
            percentual, semente);
    }

    private static int Fase(int semente, string chave)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(
            semente.ToString(System.Globalization.CultureInfo.InvariantCulture) + "\n" + chave));
        return (int)(BinaryPrimitives.ReadUInt64LittleEndian(bytes) % 17UL);
    }
}
