# CLEAR — Sub-base experimental estratificada (primeira entrega)

**Branch:** feature/subbase-experimental-estratificada
**Estado:** implementação experimental, sem compilação nem testes Windows ainda; nenhum merge realizado.

## Operação obrigatória — SOMENTE CÓPIA

Cada arquivo selecionado é DUPLICADO em uma nova pasta, e TODOS os originais permanecem na origem. Nunca mover, renomear, excluir nem sobrescrever os arquivos originais. O sistema operacional pode atualizar datas de último acesso quando um arquivo é lido, mas o CLEAR não modifica conteúdos intencionalmente. A função de sub-base não utiliza quarentena nem altera o SQLite.

## Amostragem

- Meta global de quantidade EXATA, quando viável; meta de bytes APROXIMADA.
- Estratos por primeira pasta relativa à raiz (pacote de origem), extensão e faixa de tamanho (vazio; até 64 KiB; até 1 MiB; até 16 MiB; até 256 MiB; maior).
- Ao menos um arquivo por estrato quando a quantidade total solicitada permite representar todos.
- Ordem determinística por tamanho e caminho, seleção sistemática por quantis com 17 fases controladas por semente (padrão 5721) e escolha da fase com melhor aproximação de bytes por estrato.
- O percentual real de bytes pode divergir de 30%: arquivos inteiros são indivisíveis e formatos raros devem continuar presentes. Registrar desvios; não alegar amostragem probabilística perfeita.

## Destino, hierarquia e integridade

Origem de referência: D:\Base_Dados_TCC\02_original.
Destino sugerido: D:\Base_Dados_TCC\03_amostras\CLEAR_30pct.

- Destino final não pode existir e não pode conter nem estar contido na origem; verificar links/junctions existentes, inclusive na enumeração.
- Conteúdo sob a pasta Acervo do destino, preservando o caminho relativo completo de cada arquivo selecionado. Pastas vazias que não sejam ancestrais de arquivos selecionados podem não ser reproduzidas.
- Construir em pasta com sufixo CLEAR_EM_CONSTRUCAO_GUID; publicar a pasta final apenas após conclusão e verificação. Cancelamentos/falhas preservam a pasta incompleta claramente identificada para inspeção e limpeza manual.
- Antes da cópia, reenumerar e comparar metadados com a prévia para recusar alterações na população original.
- Cópia via streams de leitura da origem e gravação exclusiva de temporários de destino, sem sobrescrita; calcular SHA-256 durante a leitura e conferir novamente o hash do arquivo copiado. Preservar LastWriteTimeUtc.
- Reserva de espaço verificada (bytes-alvo + 5% + 512 MiB). Isso não equivale a medição exata de uso físico.
- Em casos de alteração adversarial concorrente existe janela de corrida; usar somente datasets autorizados, sem edições simultâneas.

## Manifestos gerados

- manifesto_resumo.json: esquema CLEAR-SUBBASE-1, metadados do experimento, contagens, bytes, percentuais, semente, estratos.
- manifesto_arquivos.jsonl: caminho relativo, tamanho, timestamps, estrato e hash SHA-256 para cada arquivo selecionado.
- Acervo: cópias dos conteúdos. Manifestos fora de Acervo não contam na amostra.

O código não efetua nem autoriza exclusão de originais. O arquivo JSONL e o resumo podem revelar nomes/caminhos e devem ser tratados como dados a revisar antes de publicação.

## Limitações e próximos incrementos

Esta amostra é determinística e estratificada, não um dataset automaticamente representativo de toda uma empresa. Medir desvio real de bytes e distribuições no acervo de 86,05 GB. Verificar direitos de reprodução/uso no notebook. Escolha de pasta de quarentena, hierarquia física da quarentena e seleção parcial por filtros ficam para outros incrementos, com aprovação separada. Nenhum merge automático.
