# M8 — Parte 1: restauração assistida da quarentena

Estado: feature/restauracao-quarentena-m8, aguardando compilação, testes e aceite. Interface funcional, sem acabamento visual avançado.

## Objetivo

Permitir restaurar INDIVIDUALMENTE arquivos que o M7 registrou como EmQuarentena, devolvendo-os ao caminho original apenas após nova confirmação humana. O CLEAR não migra, exclui nem sobrescreve arquivos.

## Fluxo implementado

1. Histórico M6 → Consultar detalhes → Auditoria da quarentena M7 → escolha um registro EmQuarentena → Revisar restauração (M8).
2. Preparação sem mudanças: abre o banco SQLite do M7, valida status, caminho, pasta original existente, conflito, mesmo volume e SHA-256. O arquivo fica na quarentena enquanto o usuário revisa.
3. A caixa exibe origem, destino original, tamanho, hash e ID. NÃO é a resposta padrão e não realiza movimentação nem registra uma tentativa.
4. Após SIM, o serviço compara o plano aos dados do SQLite, recalcula SHA-256 e confere o caminho novamente.
5. Grava o estado RestauracaoPendente antes do File.Move sem sobrescrita; após concluir, grava Restaurado e o horário UTC no campo mensagem.
6. Se o arquivo continuar na quarentena e o destino estiver livre, uma falha devolve status a EmQuarentena com descrição do erro; situação ambígua permanece RestauracaoPendente para revisão assistida.

## Compatibilidade

- Utiliza tabela movimentacoes com esquema v1 do M7. NÃO migra nem apaga o banco existente.
- O histórico de análise M6 permanece como registro da avaliação passada, não é modificado pela restauração.
- Pendente do M7, RestauracaoPendente, Falha e Restaurado não são restauráveis novamente de forma automática.
- Quarentena: %LOCALAPPDATA%\CLEAR\Quarentena. Auditoria: %LOCALAPPDATA%\CLEAR\quarentena.sqlite3.
- Não cria pasta original ausente, não resolve conflitos por renomeação nem substituição, não age em lotes e não precisa de editor SQLite externo.

## Limites

- SHA-256 não elimina por completo condições de corrida entre conferência e File.Move, links hostis, alterações externas e falhas físicas.
- O filesystem e o SQLite não possuem transação distribuída: quedas podem deixar status RestauracaoPendente. Não anunciar sucesso sem gravação confirmada.
- A auditoria desta etapa mantém estado e data na linha do M7; trilha append-only e conciliação automatizada de estados incertos ainda são evoluções.
- Usar apenas arquivos sintéticos em C:\Temp até concluir testes. Cada ação de restauração requer confirmação explícita independente da confirmação M7.

## Critérios de aceite

- Esperados 193 testes: 172 anteriores do M6, 10 do M7 e 11 novos do M8; confirmar número real MSTest. Build Release e auditoria NuGet sem avisos.
- Negar não move, aceitar restaura bytes e registra Restaurado; estado persiste após reabrir a ferramenta.
- Bloquear arquivo novo no caminho, SHA divergente, estado pendente, plano modificado, segunda restauração e pasta original ausente.
- Compatibilidade com o banco M7 v1 do arquivo de teste já movido pelo usuário.
- Sem merge para develop ou main antes de nova aprovação explícita.
