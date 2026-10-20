# Entrega única M10 — Quarentena hierárquica, destinos, filtros e ETA

Status: apenas feature/fluxo-quarentena-filtros-eta. Ainda sem compilação/testes Windows. Merge pendente de aprovação.

## Quarentena e restauração

Nova estrutura física: raiz-quarentena / GUID da operação / caminho relativo integral desde a raiz analisada. Cada arquivo permanece isolado por operação, sem sobrescrita. O banco SQLite de auditoria M7 v1 preserva o caminho original e destino absoluto. A restauração M8 aceita estrutura nova e antiga (GUID / nome). Não move pastas inteiras nem recria diretórios originais ausentes. Exige confirmação humana, revalidação de metadados/SHA-256, mesma unidade e caminho original livre. Conflitos/estados pendentes requerem revisão manual.

## Configurações da interface

A pasta da quarentena pode ser escolhida ANTES da análise, com sugestão automática de pasta irmã, no mesmo volume da origem; escolha manual prevalece durante a sessão. A pasta de relatórios tem default LOCALAPPDATA/CLEAR/Relatorios, podendo ser escolhida na interface; somente é usada se M5 estiver ativo. Saídas não podem estar dentro do acervo ou conter a origem e links/junctions são rejeitados. Preferências não são gravadas permanentemente entre sessões.

## Filtros e seleção parcial

Exige histórico SQLite M6 habilitado durante a análise. Depois: Histórico / Consultar detalhes / Filtrar e selecionar para quarentena.

Combina filtros por categoria, extensão, intervalo de tamanho em MiB, datas de modificação, incompatibilidade, duplicatas, tamanho acima do personalizado e texto do caminho. Consulta em SQLite parametrizada, paginada (200 por tela). A seleção EXPLÍCITA guarda IDs históricos entre páginas e filtros; filtrar nunca marca automaticamente. Marcar página marca somente itens visíveis. Lote máximo de 10.000 itens marcados, confirmação global com quantidade, bytes, origem e destino. Cada item é novamente preparado e verificado pelo M7 antes de qualquer movimentação. Operações bem-sucedidas não são revertidas automaticamente se outro item falhar, e todas possuem auditoria individual.

## ETA observacional

Sub-base por cópia: peso 50% quantidade e 50% bytes; inventário/classificação: pré-contagem de arquivos, depois tempo observado por arquivo; hash: ETA somente da fase SHA (a confirmação byte a byte não tem previsão confiável); lote de quarentena: progresso dos itens revisados e bytes. Tempo decorrido e restante aproximado recalculados. Se não há dados suficientes, indicar calculando ou indisponível. ETA não é promessa de prazo e muda com distribuição de tamanhos e cache. Relatórios finais e commit SQLite ainda não expõem progresso necessário para ETA.

## Segurança e limites

Apenas cópias sintéticas em C:/Temp para validar movimentação/restauração. Nunca experimentar no acervo público original D:/Base_Dados_TCC/02_original. Nenhum upload, migração, exclusão automática ou sobrescrita. Quarentena e restauração permanecem opt-in explícito. Esta feature ainda requer build, testes, auditoria NuGet e validação manual conjunta antes de qualquer merge.