# M5 — Parte 1: exportação de relatórios completos

**Estado:** branch `feature/relatorios-completos-m5`, em validação no notebook Windows 11. Nenhum merge autorizado para `develop` ou `main`.

## Objetivo

Permitir análise de acervos com milhares de entradas sem depender de uma tabela de amostra na interface, preservando os resultados completos da mesma varredura e respeitando o caráter **assistido, explicável e não destrutivo** do CLEAR.

## Entregas técnicas

1. **Observador opcional na aplicação:** `IObservadorInventario` recebe cada arquivo ou diretório já classificado durante o inventário original. O caso de uso continua limitando a amostra na interface (até 1.000 linhas), mas emite todas as entradas enumeradas para a exportação. Não introduz segunda varredura de metadados no M3.
2. **CSV completo por streaming:** `ExportadorRelatoriosM5`, na Infrastructure, escreve uma linha para cada entrada escolhida pelo filtro. Colunas: tipo, nome, caminho, tamanho, UTC da última modificação, categorias e evidências gerais, estados e justificativas de regras de destino e indicador do tamanho personalizado. CSV UTF-8 com BOM, delimitador `;`, aspas escapadas e proteção de valores que iniciem como fórmulas para abertura em planilhas.
3. **Filtro somente na exportação:** todas as entradas, apenas indícios gerais, apenas incompatibilidades documentadas, apenas limite pessoal ou união desses três grupos de achados. A filtragem **não muda a classificação, os totais globais ou a leitura** e não inclui duplicatas isoladamente, pois o M4 ainda é uma etapa independente.
4. **Resumo JSON:** data de início e fim em UTC, pasta analisada, tipo de filtro, total de linhas gravadas, configurações gerais selecionadas (inclusive referência temporal), perfil de destino, limites de tamanho, totais globais, categorias, estados de regras, ocorrências (até 100 descrições), condição parcial e indicadores de M4 se executado.
5. **CSV de duplicatas opcional:** se o M4 foi habilitado e concluído, gera todas as linhas de grupos exatos e seus membros (sem limite visual de 200) e um resumo com contagens. Não refaz o SHA-256; utiliza o resultado validado da mesma execução.
6. **Transação por arquivos temporários:** os relatórios são escritos com extensão `.part` e publicados com nome definitivo apenas quando todas as etapas e o resumo terminam. Cancelamento ou falha remove temporários e finais que tenham sido publicados nessa tentativa.
7. **Proteção da origem:** saída obrigatoriamente em pasta existente fora do acervo analisado e sem links/junctions conhecidos nos ancestrais. Nomes de saída incluem data e GUID, sem sobrescrever relatórios anteriores. A escolha da pasta é explícita e só acontece quando a exportação está marcada.

## Distinções fundamentais

- **Revisão humana:** toda futura ação que altere nome, localização, conteúdo ou remova um arquivo exigirá confirmação explícita. O M5 não oferece operações desse tipo.
- **Relatório não é persistência de decisões:** CSV/JSON permitem consulta/exportação, mas não implementam histórico relacional, SQLite, revisão de decisões, deduplicação incremental nem reutilização segura de hashes/classificações. Esses aspectos são para M6/M9, após medições e regras de invalidação.
- **Origem pode mudar durante a análise:** M3 e M4 não são snapshots transacionais; se houver ocorrências, o resumo marca a saída como parcial. Em M4, SHA-256 é somente leitura de conteúdo e é executado apenas quando explicitamente habilitado.
- **Dado sensível:** nomes, caminhos, datas, hashes e mensagens de erro podem revelar informações confidenciais. CSV neutraliza possíveis fórmulas na visualização, mas continua contendo informações sensíveis; guardar e remover os relatórios conforme políticas locais.
- **Escalabilidade:** a quantidade de linhas CSV não é limitada pela amostra da interface. A RAM continua consumida pelo índice por tamanho e grupos do M4, se essa opção estiver ativa. O M5 não afirma uso de memória constante para a deduplicação.
- **Consulta na interface:** por enquanto, há seleção do tipo de linhas a exportar e resumo JSON; painéis de pesquisa e consultas interativas completas, paginação e refinamentos visuais continuam como futuras evoluções.

## Critérios de aceite

- Compilação Release e testes MSTest sem avisos/falhas (baseline M4: 147 testes previstos após correção).
- Cenário com mais de 1.000 entradas e amostra visual menor: CSV contém todas as entradas selecionadas, e JSON confirma os totais.
- CSV com arquivo vazio, temporário, regra SharePoint e limite pessoal; filtro `ApenasAchados` exclui itens sem classificação.
- Exportação de grupos M4 preserva 3 grupos e 6 arquivos no teste sintético.
- Cancelamento limpa `.part` e não deixa relatórios parciais anunciados como concluídos.
- Pasta de saída interna ao acervo é rejeitada.
- Original permanece intacto; exportação não executa migração, exclusão, renomeação nem quarentena.

**Execução local ainda não confirmada.** O ambiente do assistente não tem SDK .NET; o autor deve compilar e testar no Visual Studio. Não fazer merge sem aceite expresso.
