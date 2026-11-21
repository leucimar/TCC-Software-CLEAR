using Clear.Infrastructure.Historico;
using Clear.Infrastructure.Quarentena;

namespace Clear.Desktop;

internal sealed partial class PainelInventario
{
    private async Task SelecionarFiltradosAsync()
    {
        var execucao = _execucao!;
        var incluidos = Restaurando ? _emQuarentena.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase) : null;
        var ids = await Task.Run(() => ConsultaEntradasFiltradasM10.SelecionarTodosFiltradosAsync(
            _historico, execucao.Id, _filtro, _indisponiveis, caminhosIncluidos: incluidos));
        await MarcarEmLoteAsync(ids);
    }

    private async Task MarcarEmLoteAsync(IEnumerable<long> novos)
    {
        if (!PodeMarcar) return;
        var ids = _marcados.Keys.Concat(novos).Distinct().Take(LimiteLote + 1).ToArray();
        if (ids.Length == 0) return;
        if (ids.Length > LimiteLote)
            throw new InvalidOperationException($"O lote aceita até {LimiteLote:N0} arquivos.");
        var execucao = _execucao!;
        var entradas = await Task.Run(() => ConsultaEntradasFiltradasM10.ConsultarSelecionadasAsync(
            _historico, execucao.Id, ids));
        var elegiveis = entradas.Where(Elegivel).ToArray();
        if (Restaurando)
        {
            foreach (var entrada in elegiveis.Where(e => _emQuarentena.ContainsKey(e.Caminho)))
                _marcados[entrada.Id] = entrada;
        }
        else
        {
            var plano = await Task.Run(() => ProtecaoDuplicatasNaQuarentena.PrepararAsync(
                _historico, execucao.Id, elegiveis.Where(e => !_indisponiveis.Contains(e.Caminho)).ToArray(),
                _indisponiveis));
            AplicarPlano(plano);
        }
        await ContarForaDoFiltroAsync();
    }

    private async Task MarcarIndividualAsync(EntradaHistoricaM6 item, bool marcar)
    {
        if (!Elegivel(item)) return;
        if (!marcar)
        {
            _marcados.Remove(item.Id);
            if (item.GrupoDuplicata is long grupo) _mantidos.Remove(grupo);
        }
        else
        {
            if (_marcados.Count >= LimiteLote)
                throw new InvalidOperationException($"O lote aceita até {LimiteLote:N0} arquivos.");
            if (!Restaurando && item.GrupoDuplicata is not null)
            {
                var execucao = _execucao!;
                var caminhos = _marcados.Values.Select(x => x.Caminho).ToHashSet(StringComparer.OrdinalIgnoreCase);
                var regra = await Task.Run(() => ProtecaoDuplicatasNaQuarentena.AvaliarMarcacaoAsync(
                    _historico, execucao.Id, item, caminhos, _indisponiveis));
                if (regra.CaminhoProtegido is { } protegido) _mantidos[regra.Grupo] = protegido;
                else _mantidos.Remove(regra.Grupo);
                if (!regra.Permitida)
                {
                    _mensagem = $"Mantenha uma cópia do grupo {regra.Grupo}. Desmarque outra para trocar.";
                    return;
                }
            }
            _marcados[item.Id] = item;
        }
        await ContarForaDoFiltroAsync();
    }

    private void AplicarPlano(SelecaoQuarentenaProtegida plano)
    {
        LimparMarcacoes();
        foreach (var item in plano.Grupos.SelectMany(g => g.Entradas)) _marcados.Add(item.Id, item);
        foreach (var referencia in plano.ReferenciasUnicas)
            _mantidos.Add(referencia.Grupo, referencia.Caminho);
    }

    private void LimparMarcacoes()
    {
        _marcados.Clear();
        _mantidos.Clear();
        _foraDoFiltro = 0;
    }

    private async Task ContarForaDoFiltroAsync()
    {
        if (_execucao is null || _marcados.Count == 0)
        {
            _foraDoFiltro = 0;
            return;
        }
        var id = _execucao.Id;
        var ids = _marcados.Keys.ToArray();
        var visiveis = await Task.Run(() => ConsultaEntradasFiltradasM10.ContarMarcadosVisiveisAsync(
            _historico, id, _filtro, ids));
        _foraDoFiltro = ids.Length - visiveis;
    }

    private void AtualizarMarcacoes()
    {
        _atualizandoGrade = true;
        try
        {
            foreach (DataGridViewRow linha in _grade.Rows)
            {
                if (linha.Tag is not EntradaHistoricaM6 entrada) continue;
                linha.Cells[0].Value = _marcados.ContainsKey(entrada.Id);
                linha.Cells[0].ToolTipText = entrada.GrupoDuplicata is long grupo &&
                    _mantidos.TryGetValue(grupo, out var caminho) &&
                    string.Equals(caminho, entrada.Caminho, StringComparison.OrdinalIgnoreCase)
                    ? $"Cópia mantida no acervo (grupo {grupo})." : "";
            }
        }
        finally { _atualizandoGrade = false; }
    }

    private async Task ExecutarLoteAsync()
    {
        if (!PodeMarcar || _marcados.Count == 0) return;
        var execucao = _execucao!;
        if (Restaurando)
            await RestaurarAsync(execucao);
        else
            await QuarentenarAsync(execucao);
    }

    private async Task QuarentenarAsync(ResumoExecucaoHistoricaM6 execucao)
    {
        var destino = Path.GetFullPath(_raizQuarentena());
        var ids = _marcados.Keys.ToArray();
        var plano = await Task.Run(async () =>
        {
            var bloqueados = await ConsultaQuarentenaFiltradaM11.CaminhosIndisponiveisAsync(_auditoria);
            var marcados = await ConsultaEntradasFiltradasM10.ConsultarSelecionadasAsync(
                _historico, execucao.Id, ids);
            if (marcados.Any(e => bloqueados.Contains(e.Caminho)))
                throw new IOException("Parte da seleção já está em quarentena. Recarregue o Inventário.");
            return await ProtecaoDuplicatasNaQuarentena.PrepararAsync(_historico, execucao.Id,
                marcados, bloqueados);
        });
        // A prévia só pode diminuir o conjunto marcado. O serviço verifica a cópia em disco.
        if (plano.Grupos.Sum(g => g.Entradas.Count) != ids.Length) AplicarPlano(plano);
        AtualizarMarcacoes();
        await ContarForaDoFiltroAsync();
        if (_marcados.Count == 0)
        {
            _mensagem = "Os arquivos marcados são as cópias que precisam ficar no acervo.";
            return;
        }
        var rota = new RotaDeLote(execucao.DiretorioRaiz, destino, _marcados.Count);
        if (!DialogoLote.Confirmar(this, false, [rota], _foraDoFiltro)) return;
        await AcompanharLoteAsync(_marcados.Count, async (token, registrar) =>
        {
            foreach (var grupo in plano.Grupos)
            {
                token.ThrowIfCancellationRequested();
                FileStream? referencia;
                try
                {
                    referencia = await ProtecaoDuplicatasNaQuarentena.AbrirReferenciaAsync(
                        grupo, execucao.DiretorioRaiz, token);
                }
                catch (Exception erro) when (erro is IOException or UnauthorizedAccessException)
                {
                    foreach (var entrada in grupo.Entradas)
                        registrar(entrada, erro.Message);
                    continue;
                }
                await using var fluxo = referencia;
                foreach (var entrada in grupo.Entradas)
                {
                    token.ThrowIfCancellationRequested();
                    try
                    {
                        var movimento = await ProtecaoDuplicatasNaQuarentena.PrepararMovimentoAsync(
                            grupo, entrada, execucao, destino, token);
                        await ServicoQuarentenaM7.ExecutarAsync(movimento, true, _auditoria, token);
                        registrar(entrada, null);
                    }
                    catch (Exception erro) when (erro is not OperationCanceledException)
                    { registrar(entrada, erro.Message); }
                }
            }
        });
    }

    private async Task RestaurarAsync(ResumoExecucaoHistoricaM6 execucao)
    {
        var marcados = _marcados.Values.ToArray();
        var ids = marcados.Select(e => _emQuarentena[e.Caminho].IdOperacao).ToHashSet(StringComparer.Ordinal);
        var registros = await Task.Run(() => ConsultaQuarentenaFiltradaM11.ConsultarAtivasAsync(
            _auditoria, execucao.Id));
        var ativos = registros.Where(r => ids.Contains(r.IdOperacao))
            .ToDictionary(r => r.CaminhoOriginal, StringComparer.OrdinalIgnoreCase);
        if (ativos.Count != ids.Count)
            throw new InvalidOperationException("Parte da seleção já saiu da quarentena. Recarregue o Inventário.");
        var rotas = ativos.Values.Select(r => ServicoRestauracaoM8.ConsultarRaizes(r, execucao.DiretorioRaiz))
            .GroupBy(r => r.Origem, StringComparer.OrdinalIgnoreCase)
            .SelectMany(origens => origens.GroupBy(r => r.Destino, StringComparer.OrdinalIgnoreCase)
                .Select(destinos => new RotaDeLote(origens.Key, destinos.Key, destinos.Count()))).ToArray();
        if (!DialogoLote.Confirmar(this, true, rotas, _foraDoFiltro)) return;
        await AcompanharLoteAsync(marcados.Length, async (token, registrar) =>
        {
            foreach (var entrada in marcados)
            {
                token.ThrowIfCancellationRequested();
                try
                {
                    var movimento = await ServicoRestauracaoM8.PrepararAsync(
                        ativos[entrada.Caminho].IdOperacao, _auditoria, token);
                    await ServicoRestauracaoM8.RestaurarAsync(movimento, true, _auditoria, token);
                    registrar(entrada, null);
                }
                catch (Exception erro) when (erro is not OperationCanceledException)
                { registrar(entrada, erro.Message); }
            }
        });
    }

    private async Task AcompanharLoteAsync(int total,
        Func<CancellationToken, Action<EntradaHistoricaM6, string?>, Task> executar)
    {
        using var cancelamento = new CancellationTokenSource();
        _lote = cancelamento;
        AtualizarBotoes();
        var erros = new List<string>();
        int sucessos = 0;
        _estado.Text = $"Processando 0 de {total:N0} arquivos…";
        try
        {
            await executar(cancelamento.Token, (entrada, erro) =>
            {
                if (erro is null)
                {
                    sucessos++;
                    _marcados.Remove(entrada.Id);
                }
                else erros.Add(entrada.Caminho + Environment.NewLine + erro);
                _estado.Text = $"Processados: {sucessos + erros.Count:N0}/{total:N0} · " +
                    $"Concluídos: {sucessos:N0} · Falhas: {erros.Count:N0}";
            });
        }
        catch (OperationCanceledException) when (cancelamento.IsCancellationRequested) { }
        finally
        {
            _lote = null;
            await ConsultarAsync(_pagina);
            AuditoriaAlterada?.Invoke(this, EventArgs.Empty);
        }
        _mensagem = $"{(cancelamento.IsCancellationRequested ? "Lote interrompido" : "Lote encerrado")} · " +
            $"Concluídos: {sucessos:N0} · Falhas: {erros.Count:N0}";
        if (erros.Count == 0) return;
        MessageBox.Show(this, _mensagem + Environment.NewLine + Environment.NewLine +
            "Primeira falha:" + Environment.NewLine + erros[0],
            "CLEAR", MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }
}
