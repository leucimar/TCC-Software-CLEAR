using Clear.Application.Duplicatas;
using Clear.Application.Inventario;
using Clear.Domain.Analise;

namespace Clear.Application.Metricas;

public sealed record MetricaCategoria(string Categoria, long Quantidade,
    long Denominador, string UnidadeDenominador, bool Avaliada)
{
    public double? Percentual => Avaliada && Denominador > 0
        ? 100.0 * Quantidade / Denominador : null;
}

/// <summary>Medição observacional, sem equivaler a aprovação/descarte ou velocidade física de disco.</summary>
public sealed record MetricasAnalise(string Id, string PastaRaiz,
    DateTimeOffset InicioUtc, DateTimeOffset FimUtc, long Arquivos, long Diretorios,
    long Bytes, double InventarioSegundos, double DuplicatasSegundos,
    double TotalSegundos, int Leitores, bool Parcial, string Configuracao,
    IReadOnlyList<MetricaCategoria> Categorias)
{
    public string? CriterioTemporal { get; init; }
    public Clear.Domain.Arquivos.ResumoPreservacaoAcesso? PreservacaoAcesso { get; init; }
    public double? ArquivosPorSegundo => InventarioSegundos > 0 ? Arquivos / InventarioSegundos : null;
    public double? MiBInventariadosPorSegundo => InventarioSegundos > 0 ? Bytes / 1048576.0 / InventarioSegundos : null;

    public static MetricasAnalise Criar(string pasta, DateTimeOffset inicio,
        ResultadoInventario inventario, ResultadoDuplicatas? duplicatas,
        TimeSpan inventarioTempo, TimeSpan duplicatasTempo, TimeSpan totalTempo,
        int leitores, string configuracao, IReadOnlyCollection<CategoriaAnalise> categoriasAvaliadas)
    {
        if (inventarioTempo < TimeSpan.Zero || duplicatasTempo < TimeSpan.Zero ||
            totalTempo < inventarioTempo + duplicatasTempo || leitores is < 1 or > 8)
            throw new ArgumentOutOfRangeException(nameof(totalTempo));
        var categorias = new List<MetricaCategoria>();
        foreach (var categoria in new[] { CategoriaAnalise.ArquivoVazio,
                     CategoriaAnalise.CopiaBackupOuTemporario, CategoriaAnalise.SinalTemporal })
            categorias.Add(new(categoria.ToString(),
                inventario.ContagensCategoria.FirstOrDefault(c => c.Categoria == categoria)?.QuantidadeArquivos ?? 0,
                inventario.QuantidadeArquivos, "Arquivos", categoriasAvaliadas.Contains(categoria)));
        categorias.Add(new("DuplicataExata", duplicatas?.ArquivosEmGrupos ?? 0,
            inventario.QuantidadeArquivos, "Arquivos", duplicatas is not null));
        // O contador de compatibilidade do inventário inclui diretórios.
        categorias.Add(new("IncompatibilidadeDestino", inventario.ItensComIncompatibilidade,
            inventario.QuantidadeArquivos + inventario.QuantidadeDiretorios, "Itens (arquivos e pastas)",
            inventario.ConfiguracaoCompatibilidade.HaVerificacoesHabilitadas));
        categorias.Add(new("AcimaLimitePersonalizado", inventario.ArquivosAcimaLimitePersonalizado,
            inventario.QuantidadeArquivos, "Arquivos", inventario.FiltroPersonalizado is not null));
        return new(Guid.NewGuid().ToString("N"), Path.GetFullPath(pasta), inicio,
            DateTimeOffset.UtcNow, inventario.QuantidadeArquivos, inventario.QuantidadeDiretorios,
            inventario.TotalBytes, inventarioTempo.TotalSeconds, duplicatasTempo.TotalSeconds,
            totalTempo.TotalSeconds, duplicatas is null ? 0 : leitores,
            inventario.Incompleto || duplicatas?.Incompleto == true, configuracao, categorias);
    }
}
