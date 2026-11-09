# CLEAR — conferência de concorrência, métricas e preservação de acesso

Data: 09/11/2026. Branch: `fix/checkup-concorrencia-metricas`, baseada no commit `90ffe08` da develop. Este documento complementa o registro histórico de 04/11/2026. A autorização de 09/11 inclui commit e envio desta branch ao GitHub; não inclui merge na develop/main.

## Comportamento implementado

- A fila global de hashes respeita o limite de 1–8 leitores, repõe os slots livres e contabiliza separadamente hashes válidos e falhas. As duplicatas continuam exigindo SHA-256 e confirmação byte a byte.
- A análise temporal usa exclusivamente último acesso ou última modificação. Data ausente não é substituída pelo outro campo. O critério e a política entram na assinatura do checkpoint, nos parâmetros históricos e nas métricas.
- As leituras de conteúdo solicitam ao Windows a supressão de atualização do último acesso para cada handle, inclusive quando o critério escolhido é modificação ou quando o filtro temporal está desligado. A rotina também é usada nas leituras de integridade de quarentena/restauração e cópia experimental; essas operações mantêm suas confirmações próprias.
- O padrão é não ler conteúdo sem preservação. Os metadados podem ser inventariados, mas falhas de leitura tornam a cobertura da deduplicação incompleta; arquivos não lidos não são declarados únicos.
- A alternativa de ler sem garantia exige confirmação explícita antes de cada análise. Tentativas de fallback são registradas antes da abertura sem proteção. O diário, o resumo JSON e o CSV ficam em `%LOCALAPPDATA%\CLEAR\Preservacao`, mesmo quando a exportação completa está desligada. Cancelamentos/falhas também tentam salvar o resumo e exibem falhas de auditoria.
- “Tentar como administrador” solicita o UAC real e abre outra janela com as opções recuperadas, sem iniciar análise automaticamente. A janela original permanece disponível. Cancelar o UAC conserva suas opções. Elevação não garante permissões em todos os arquivos ou servidores.
- Histórico e métricas registram a preservação; o histórico apresenta os parâmetros/resumo da execução. Registros antigos não são reescritos nem interpretados como se tivessem proteção comprovada. A tabela de métricas exportável para Excel inclui o critério, a política e as contagens de preservação.

Não há restauração forçada da data antiga após a leitura, alteração de ACLs do acervo ou alteração global das configurações de último acesso do Windows. O teste de permissão altera e restaura somente a ACL de um arquivo temporário criado pelo próprio teste.

## Validação de 09/11

`dotnet test CLEAR.slnx -c Release --artifacts-path artifacts/conferencia_2026-11-09 -p:NuGetAudit=false -p:RestoreIgnoreFailedSources=true`

| Projeto | Aprovados | Falhas |
|---|---:|---:|
| Domain | 91 | 0 |
| Application | 71 | 0 |
| Infrastructure | 114 | 0 |
| Desktop | 3 | 0 |
| Total | 279 | 0 |

Os testes cobrem critérios exclusivos e datas ausentes/fronteiras, permissão real negada de `WriteAttributes` em arquivo temporário, políticas estrita/alternativa, propagação para leitores concorrentes, resultados incompletos, persistência/exportação e recuperação das opções de elevação sem início automático. Permanecem seis avisos de estilo MSTEST0037; a compilação não apresentou erros.

Somente a validação controlada foi utilizada: **558 arquivos e 124.206.815 bytes**, em uma cópia dentro de `Validacao_Controlada_CLEAR\03_experimental`. A base de aproximadamente 100 GB não foi analisada nem usada como entrada.

**29 verificações de integração aprovadas, zero falhas:** acesso com 1 e 8 leitores, modificação com 8 e filtro temporal desligado com 8. As categorias por acesso coincidiram arquivo a arquivo com o oráculo; as contagens por modificação foram calculadas independentemente a partir dos metadados esperados. As quatro rodadas encontraram os mesmos **17 grupos, 57 membros e 67.139.344 bytes potencialmente redundantes**. Cada rodada registrou 585 aberturas protegidas aceitas, nenhuma leitura sem garantia e nenhuma leitura ignorada. Datas da cópia e dos originais foram conferidas; os hashes dos 558 arquivos da cópia coincidiram com o manifesto. Não houve descarte.

Os tempos completos e por fase estão em `desempenho.json`. São observações locais com cache/antivírus/carga não controlados; não constituem benchmark nem promessa de aceleração.

Evidências locais:

`D:\TCC_Leucimar\Base_Dados\Validacao_Controlada_CLEAR\08_relatorios\Datas_20261109_215110`

Essa pasta contém conclusão, verificações, desempenho, relatórios por rodada, auditorias de preservação e os quatro arquivos TRX em `regressao`. Os arquivos da base, SQLite de execução, TRX e executáveis não integram o commit.

## Atalho e executável

Atalho conferido e atualizado:

`D:\TCC_Leucimar\Programa_CLEAR\CLEAR - Correções locais.lnk`

Destino:

`D:\TCC_Leucimar\Programa_CLEAR\artifacts\conferencia_2026-11-09\bin\Clear.Desktop\release\Clear.Desktop.exe`

A compilação de 06/11 e a de 04/11 permanecem disponíveis. O atalho é local e não entra no Git, pois contém o caminho absoluto desta máquina. As alterações locais anteriores de código/testes foram preservadas e incluídas com as correções relacionadas. Os diagramas locais em `docs/diagramas` foram preservados fora deste commit, cujo escopo é a aplicação e sua validação.

## Pendências e limites

1. A janela iniciou, mas a captura visual falhou nas duas tentativas: `FrameArrived timed out: timed out waiting on channel` e `window capture timed out: timed out waiting on channel`. A conferência visual/interativa permanece pendente; essa falha da ferramenta não foi atribuída ao CLEAR. Os três testes Desktop verificam os controles/configurações, não substituem avaliação visual.
2. O aceite/cancelamento real do UAC não foi automatizado. O botão e os argumentos de elevação, a configuração recuperada e a ausência de início automático são cobertos por testes; o diálogo de segurança precisa ser operado pelo usuário.
3. A preservação significa que o Windows aceitou a solicitação para os handles usados pelo CLEAR. Não congela acessos de antivírus, outras aplicações/usuários, nem garante comportamento idêntico em SMB ou outros sistemas de arquivos. O ensaio local também não constitui prova sob atualização global de acesso habilitada. A VM/servidor permanece para uma etapa futura.
4. No modo alternativo, leitura completa e preservação completa são condições distintas: uma análise pode concluir com conteúdo verificado e avisos de acesso sem garantia. As contagens são de aberturas, não de arquivos únicos, porque hash e comparação podem reabrir o mesmo arquivo.

O envio ao GitHub desta branch conserva essas limitações explicitamente; nenhuma delas é apresentada como resolvida apenas pela publicação.
