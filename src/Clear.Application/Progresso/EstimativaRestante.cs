namespace Clear.Application.Progresso;

/// <summary>
/// Estimativa observacional por volume de bytes e quantidade de arquivos.
/// Calculada somente após observar avanço e tempo mínimos, nunca como
/// garantia de conclusão. Os pesos evitam ETA enviesado exclusivamente por
/// arquivos grandes ou milhões de arquivos pequenos.
/// </summary>
public static class EstimativaRestante
{
    public static TimeSpan? Calcular(
        long feitos, long total, long bytesFeitos, long bytesTotal,
        TimeSpan decorrido)
    {
        if (feitos < 0 || total <= 0 || feitos > total ||
            bytesFeitos < 0 || bytesTotal < 0 || bytesFeitos > bytesTotal)
            return null;
        if (feitos == total && (bytesTotal == 0 || bytesFeitos == bytesTotal))
            return TimeSpan.Zero;
        if (decorrido < TimeSpan.FromSeconds(2) || feitos == 0)
            return null;

        var porItens = Math.Clamp((double)feitos / total, 0, 1);
        var porBytes = bytesTotal > 0
            ? Math.Clamp((double)bytesFeitos / bytesTotal, 0, 1)
            : porItens;
        var progresso = bytesTotal > 0
            ? 0.5 * porItens + 0.5 * porBytes : porItens;
        if (progresso < 0.002 || progresso >= 1)
            return null;
        var segundos = decorrido.TotalSeconds * (1 - progresso) / progresso;
        if (!double.IsFinite(segundos) || segundos < 0 ||
            segundos > TimeSpan.MaxValue.TotalSeconds)
            return null;
        return TimeSpan.FromSeconds(segundos);
    }

    public static string Exibir(TimeSpan? tempo)
    {
        if (tempo is null) return "ETA: calculando...";
        if (tempo.Value == TimeSpan.Zero) return "ETA: concluído";
        return tempo.Value.TotalDays >= 1
            ? $"ETA: ~{tempo.Value.TotalDays:F1} dia(s)"
            : $"ETA: ~{(int)tempo.Value.TotalHours:D2}:{tempo.Value.Minutes:D2}:{tempo.Value.Seconds:D2}";
    }
}
