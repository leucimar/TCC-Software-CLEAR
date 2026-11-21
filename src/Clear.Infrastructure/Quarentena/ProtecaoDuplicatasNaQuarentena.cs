using System.Security.Cryptography;
using Clear.Infrastructure.Arquivos;
using Clear.Infrastructure.Historico;

namespace Clear.Infrastructure.Quarentena;

public sealed record GrupoQuarentenaProtegido(
    IReadOnlyList<EntradaHistoricaM6> Entradas,
    IReadOnlyList<MembroDuplicataHistoricaM6> Referencias);

public sealed record SelecaoQuarentenaProtegida(
    IReadOnlyList<GrupoQuarentenaProtegido> Grupos,
    IReadOnlyList<EntradaHistoricaM6> MantidosNaOrigem,
    IReadOnlyList<MembroDuplicataHistoricaM6> ReferenciasUnicas);

public static class ProtecaoDuplicatasNaQuarentena
{
    public static async Task<PlanoQuarentenaM7> PrepararMovimentoAsync(GrupoQuarentenaProtegido grupo,
        EntradaHistoricaM6 entrada, ResumoExecucaoHistoricaM6 execucao, string destino,
        CancellationToken token = default)
    {
        var plano = await ServicoQuarentenaM7.PrepararAsync(entrada, execucao, destino, token)
            .ConfigureAwait(false);
        if (grupo.Referencias.Count > 0 && !string.Equals(plano.Sha256Hex, grupo.Referencias[0].Sha256,
                StringComparison.OrdinalIgnoreCase))
            throw new IOException("O arquivo deixou de corresponder ao grupo de duplicatas. Reanalise o acervo.");
        return plano;
    }

    public static async Task<SelecaoQuarentenaProtegida> PrepararAsync(string banco, string execucao,
        IReadOnlyList<EntradaHistoricaM6> entradas, IReadOnlySet<string> indisponiveis,
        CancellationToken token = default)
    {
        var marcados = entradas.Where(e => e.EhDuplicataExata)
            .ToDictionary(e => e.Caminho, StringComparer.OrdinalIgnoreCase);
        var membros = await ConsultaEntradasFiltradasM10.ConsultarGruposSelecionadosAsync(
            banco, execucao, marcados.Keys, token).ConfigureAwait(false);
        var grupos = new List<GrupoQuarentenaProtegido>();
        var mantidos = new List<EntradaHistoricaM6>();
        var referenciasUnicas = new List<MembroDuplicataHistoricaM6>();
        var semGrupo = entradas.Where(e => !e.EhDuplicataExata).ToArray();
        if (semGrupo.Length > 0) grupos.Add(new(semGrupo, []));

        var resolvidos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var grupo in membros.GroupBy(m => m.Grupo))
        {
            token.ThrowIfCancellationRequested();
            var selecionados = grupo.Where(m => marcados.ContainsKey(m.Caminho)).ToArray();
            var disponiveis = grupo.Where(m => !indisponiveis.Contains(m.Caminho) && File.Exists(m.Caminho)).ToArray();
            var referencias = disponiveis.Where(m => !marcados.ContainsKey(m.Caminho)).ToArray();
            var movimentar = selecionados.Select(m => marcados[m.Caminho]).ToList();
            if (referencias.Length == 0)
            {
                var candidatos = disponiveis.Where(m => marcados.ContainsKey(m.Caminho)).ToArray();
                if (candidatos.Length == 0)
                    throw new IOException("Nenhuma cópia do grupo está disponível na origem. Reanalise o acervo.");
                var referencia = candidatos[Random.Shared.Next(candidatos.Length)];
                var mantido = marcados[referencia.Caminho];
                movimentar.Remove(mantido);
                mantidos.Add(mantido);
                referencias = [referencia];
            }
            if (referencias.Length == 1) referenciasUnicas.Add(referencias[0]);
            if (movimentar.Count > 0) grupos.Add(new(movimentar.AsReadOnly(), referencias));
            foreach (var item in selecionados)
                if (!resolvidos.Add(item.Caminho))
                    throw new InvalidDataException("O histórico contém um arquivo em mais de um grupo.");
        }
        if (resolvidos.Count != marcados.Count)
            throw new InvalidDataException("O histórico não contém todos os grupos da seleção. Reanalise o acervo.");
        return new(grupos.AsReadOnly(), mantidos.AsReadOnly(), referenciasUnicas.AsReadOnly());
    }

    public static async Task<(long Grupo, bool Permitida, string? CaminhoProtegido)> AvaliarMarcacaoAsync(
        string banco, string execucao, EntradaHistoricaM6 entrada, IReadOnlySet<string> marcados,
        IReadOnlySet<string> indisponiveis, CancellationToken token = default)
    {
        if (indisponiveis.Contains(entrada.Caminho) || !File.Exists(entrada.Caminho))
            throw new IOException("O arquivo não está disponível na origem. Atualize a lista.");
        var membros = await ConsultaEntradasFiltradasM10.ConsultarGruposSelecionadosAsync(
            banco, execucao, [entrada.Caminho], token).ConfigureAwait(false);
        var membro = membros.SingleOrDefault(m => string.Equals(m.Caminho, entrada.Caminho,
            StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidDataException("Grupo de duplicatas ausente no histórico. Reanalise o acervo.");
        var alternativas = membros.Where(m => m.Grupo == membro.Grupo
            && !string.Equals(m.Caminho, entrada.Caminho, StringComparison.OrdinalIgnoreCase)
            && !marcados.Contains(m.Caminho) && !indisponiveis.Contains(m.Caminho)
            && File.Exists(m.Caminho)).Take(2).ToArray();
        return alternativas.Length switch
        {
            0 => (membro.Grupo, false, membro.Caminho),
            1 => (membro.Grupo, true, alternativas[0].Caminho),
            _ => (membro.Grupo, true, null)
        };
    }

    /// <summary>
    /// Confirma o conteúdo atual de uma cópia fora da seleção. O chamador mantém o handle
    /// aberto durante os movimentos do grupo, impedindo escrita ou remoção simultânea no Windows.
    /// </summary>
    public static async Task<FileStream?> AbrirReferenciaAsync(GrupoQuarentenaProtegido grupo,
        string raiz, CancellationToken token = default)
    {
        if (grupo.Referencias.Count == 0) return null;
        foreach (var referencia in grupo.Referencias)
        {
            token.ThrowIfCancellationRequested();
            FileStream? fluxo = null;
            bool confirmado = false;
            try
            {
                var caminho = Path.GetFullPath(referencia.Caminho);
                if (!ServicoQuarentenaM7.EstaDentro(caminho, Path.GetFullPath(raiz))) continue;
                ServicoQuarentenaM7.VerificarCaminhoSeguro(caminho, arquivo: true);
                fluxo = SessaoPreservacaoAcesso.Abrir(caminho, FileShare.Read);
                if (fluxo.Length != referencia.TamanhoBytes) continue;
                var sha = Convert.ToHexString(await SHA256.HashDataAsync(fluxo, token).ConfigureAwait(false));
                confirmado = string.Equals(sha, referencia.Sha256, StringComparison.OrdinalIgnoreCase);
                if (confirmado) return fluxo;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Tentar outra referência fora da seleção conserva a lista autorizada.
            }
            finally
            {
                if (!confirmado && fluxo is not null) await fluxo.DisposeAsync().ConfigureAwait(false);
            }
        }
        throw new IOException("Não foi possível confirmar uma cópia idêntica na origem. Grupo mantido para nova análise.");
    }
}
