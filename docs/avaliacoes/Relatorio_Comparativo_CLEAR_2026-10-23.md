# CLEAR — comparação da avaliação inicial com as correções e conferências finais

Data: 23/10/2026. Relatório inicial: [Avaliação de 22/10/2026](Relatorio_Avaliacao_CLEAR_2026-10-22.md).

## Resultado

As falhas comprovadas de apresentação de duplicatas, exportação de achados, indicador histórico, alcance da auditoria e contagem/paginação de arquivos disponíveis foram corrigidas. A medição de duração não aguarda mais o fechamento do diálogo do relatório. A interface foi reorganizada em WinForms, com temas claro e escuro persistentes, mantendo as bibliotecas C#, SQLite e os serviços existentes.

A regressão completa terminou com **253 testes aprovados: 87 de domínio, 67 de aplicação e 99 de infraestrutura**. As **28 verificações complementares no laboratório passaram**. A conferência final encontrou **467 arquivos presentes e 467 hashes SHA-256 correspondentes ao gabarito**, sem divergências de conteúdo.

O resultado foi integrado na `develop` pelo PR #21, merge `bbfc074b27b5546b2ed4bf2b3e51ed81bf37463f`. O repositório original e a cópia isolada foram atualizados por avanço direto; a compilação Release também foi aprovada no repositório original. A avaliação estética definitiva pertence ao usuário. A execução em Windows Server, a acessibilidade e a validação científica de desempenho continuam exigindo trabalhos específicos; não são declaradas aprovadas por esta rodada.

## Referências e alcance

- Base do relatório inicial: `581d965f26494f35b2c30710377c26a85e9c9092`.
- Primeiro commit de implementação publicado: `f344cde`, branch `feature/melhorias-relatorio-interface-winforms`.
- Esta revisão acrescenta ajustes encontrados durante a conferência visual e os documentos de continuidade; o histórico Git identifica os commits posteriores.
- Sistema de teste: Windows 11 da máquina do usuário, com escala de 150%. Não foi utilizado Windows Server real.
- Acervo: `C:\Temp\CLEAR_Laboratorio_Sintetico_TCC\Acervo`, com gabarito preservado fora dessa subpasta.
- Desenvolvimento isolado: `C:\Users\Leucimar\Documents\ChatGPT\TCC_Leucimar\CLEAR_Implementacao`.
- Repositório de uso no desktop: `C:\Users\Leucimar\Projetos\CLEAR`.
- A sincronização solicitada nesta etapa tem como destino a **develop**. A promoção da versão para a main é uma decisão posterior.

## Comparação dos problemas e melhorias

| Item do primeiro relatório | Antes | Resultado desta rodada | Situação / limite restante |
|---|---|---|---|
| P1 — situação consolidada do CSV | 63 duplicatas apareciam sem alertas | A exportação consolida as duplicatas após a análise de conteúdo, acrescenta `DuplicataExata` e classifica esses caminhos com alerta | **Corrigido**; validado em regressão e no laboratório |
| P1 — filtro “qualquer achado” | Inclusão decidida antes de conhecer as duplicatas | Filtro considera duplicatas, indícios gerais, incompatibilidades, alertas e limite pessoal | **Corrigido**; os cinco modos de CSV tiveram seus conjuntos de caminhos conferidos |
| P1 — grade principal | Situação era montada antes da etapa de duplicatas | Grade é montada após a detecção e leva em conta a duplicidade | **Corrigido** no fluxo; análise visual concluiu com 16 grupos/111 membros |
| P1 — histórico simples | Indicador de duplicata sempre falso | Consulta recupera o vínculo com os membros dos grupos da execução | **Corrigido**; 111 indicadores recuperados na validação do laboratório |
| Pastas na exportação | Pastas sem achados eram tratadas como arquivos sem alertas | Situação própria “Pasta (nao aplicavel)” quando não há achados; incompatibilidades de pastas continuam sendo sinalizadas | **Corrigido** |
| P2 — tempo total | Incluía espera humana no diálogo do relatório | Removido o diálogo bloqueante; persistência e processamento concluem antes da publicação final das métricas | **Falha corrigida**; separação detalhada de todas as fases e tempos no JSON permanece melhoria futura |
| P3 — auditoria | Consulta limitada aos 100 primeiros registros | Páginas de 200, contador total, navegação e exportação integral CSV em fluxo | **Corrigido**; conferidos 200 + 200 + 67 registros; teste automatizado cobre contagem e exportação de 467 |
| P3 — busca da auditoria | Proposta de busca e consulta mais ampla | Consulta por execução e navegação completa disponíveis | **Parcial quanto à proposta ampliada**: busca textual avançada na grade de auditoria ainda não foi acrescentada; filtros de seleção/restauração permanecem disponíveis |
| P4 — organização inicial | Configurações extensas comprimiam os resultados | Origem e ações no topo, configurações em Análise/Saídas/Avançado, resultados em abas com mais espaço | **Implementado e conferido**; aprovação estética e refinamentos de experiência ainda cabem ao usuário |
| P4 — barras e comandos escondidos | Altura fixa cortava ações quando quebravam linha | Barras com altura adaptável nas telas históricas e operacionais | **Corrigido nos cenários observados**; não equivale a uma matriz completa de resoluções e DPI |
| P4 — amostragem e cabeçalhos | Rótulos e botões cortados na escala do monitor | Linhas e colunas adaptáveis na amostragem; cabeçalho histórico cresce com o conteúdo | **Corrigido e reconferido visualmente** |
| P5 — confirmação de lote | Mensagem enorme podia deixar botões fora da área útil | Janela própria com texto rolável, todos os caminhos selecionados e ações fixas | **Corrigido**; confirmação de quarentena aberta e negada por Enter, sem movimento; recusa permanece ação padrão |
| P5 — nomes e evidências | JSON bruto e nomes internos predominavam | Categorias traduzidas, colunas renomeadas, campos JSON técnicos ocultos na grade e evidências formatadas | **Parcial**: alguns códigos/nomes internos continuam nos detalhes; não houve revisão editorial integral de todas as mensagens |
| P5 — títulos | Histórico dizia “somente leitura” mesmo oferecendo movimentações | Título identifica histórico e operações auditadas; tela principal deixa de se apresentar como apenas M6 | **Corrigido** |
| P6 — disponíveis | Bloqueios aplicados depois de contar/paginar causavam páginas vazias | Indisponibilidade é aplicada na consulta antes de COUNT/LIMIT; total e páginas refletem disponíveis | **Corrigido**; teste com 205 caminhos bloqueados e primeira página completa |
| Prévia de sub-base | Destino ocupado só era recusado na cópia | Recusa já na preparação da prévia | **Corrigido**, inclusive conferência visual com destino existente |
| Tema claro/escuro | Interface essencialmente branca, sem alternância | Paletas compartilhadas, escolha na tela principal e preferência persistente | **Implementado e conferido**; componentes nativos do Windows mantêm parte da aparência do sistema |
| Progresso e ETA | Apresentação por fase, estimativa inicial indisponível | Mantidos estados por fase, estimativa identificada e aviso de limites das métricas | **Parcial**: não foi quantificado o erro estatístico da ETA nem concluída uma padronização visual de todos os progressos |
| SHA-256 com 1/2/4/8 leitores | Sem comparação nesta avaliação inicial | Quatro configurações produziram os mesmos 16 grupos e 111 membros | **Correção funcional conferida**; resultados não constituem benchmark controlado |
| Conteúdo após os testes | Integridade já havia sido demonstrada após restauração | Novamente 467 arquivos presentes, 0 hashes divergentes | **Aprovado**; datas de último acesso não são usadas como prova de imutabilidade |

## Testes executados e interpretação

### Automação e laboratório

A suíte passou de 251 para 253 testes. Foram ampliados testes existentes para conferir duplicatas na consulta simples, seleção paginada após indisponibilidade e inclusão de duplicatas no filtro de achados. Novos testes cobrem auditoria com 467 registros/exportação/cancelamento e recusa de destino ocupado na prévia. O contador total da auditoria foi conferido adicionalmente no mesmo teste de 467 registros, sem alterar o total de casos da suíte.

A validação complementar conferiu inventário, 24 incompatibilidades SharePoint, 12 arquivos acima de 1 MiB, duplicatas com 1/2/4/8 leitores, cinco filtros de CSV, situação de pastas, indicadores históricos, página após exclusão de 205 caminhos e ausência de arquivos temporários de relatórios. Os arquivos de evidência estão em [evidencias_melhorias_2026-10-23](evidencias_melhorias_2026-10-23).

O laboratório complementar não ativou a regra temporal. Os 76 sinais da primeira execução não podem ser exigidos novamente, pois leituras anteriores atualizaram datas de último acesso. O gabarito não foi alterado para acomodar essa condição.

Os resultados completos de testes TRX estão, nesta máquina, em `C:\Users\Leucimar\Documents\ChatGPT\TCC_Leucimar\validacao_melhorias_CLEAR\regressao_final`. Relatórios CSV/JSON completos do laboratório estão em `validacao_melhorias_CLEAR\laboratorio_01`, fora do Git, para evitar publicar todos os caminhos e duplicar artefatos volumosos no código-fonte.

### Conferência visual

Foram inspecionados: inicialização, tema claro/escuro e persistência após reabrir; abas Análise/Saídas/Avançado; inventário, duplicatas e métricas; histórico e evidências; auditoria nas três páginas; seleção de quarentena; confirmação rolável e recusa por Enter; restauração com lista vazia após o ciclo anterior; amostragem e bloqueio de prévia com destino ocupado.

Na execução visual final, o inventário mostrou 467 arquivos e 43 pastas; a detecção mostrou 16 grupos, 111 membros e 10.221 bytes potencialmente redundantes. A prévia de 30% retornou 140 arquivos, 8.240.522 bytes e 24 estratos, como na primeira avaliação. Não foi feita uma nova cópia completa nem repetido o movimento/restauro de 467 arquivos, já aprovados anteriormente; a recusa da nova confirmação preservou o arquivo selecionado.

Os ajustes finais de quebra de texto do resumo principal, explicações laterais, largura do campo de data e contador global da auditoria foram compilados; a cobertura visual não abrange cada combinação desses controles em todas as dimensões possíveis.

### Desempenho

A execução visual final observou inventário de aproximadamente **157,7 ms**, duplicatas de **3.899,7 ms** e total de **4.232,5 ms**, sem relatório/histórico ativos nessa execução. A configuração difere da primeira avaliação; **não é válido calcular um ganho percentual comparando esse total aos 38.544,4 ms anteriores**, que incluíam espera humana e outras opções.

Na conferência de concorrência, foram observados aproximadamente 3.192 ms com 1 leitor, 301 ms com 2, 279 ms com 4 e 284 ms com 8. As execuções ocorreram em sequência e sofreram efeito de cache; esses números **não justificam alterar o padrão nem afirmar que mais leitores serão sempre mais rápidos**. O conjunto de grupos permaneceu idêntico. A memória observada nessa ferramenta ficou aproximadamente entre 62 e 69 MB; trata-se do processo de validação, não de um pico isolado atribuído ao núcleo.

A consolidação do CSV utiliza registros temporários em disco e leitura em fluxo para evitar materializar todo o inventário na memória. Isso introduz uma passagem adicional de escrita/leitura para garantir consistência após as duplicatas. O custo em grandes acervos ainda deve ser medido em benchmark específico.

## O que permanece pendente ou parcialmente atendido

1. **Windows Server 2019 e posteriores:** não houve acesso a uma instalação real com Desktop Experience. Não há certificação de compatibilidade nesta entrega.
2. **Cancelamento manual seguido de reinício e conclusão:** a tentativa visual de cancelar encontrou a análise já concluída; o ciclo completo não foi demonstrado. Existem testes automatizados de cancelamento/checkpoint, mas não substituem essa evidência manual. Checkpoint não retoma da metade: reinicia com revalidação integral.
3. **Benchmark científico:** faltam repetições controladas, cenários frios/quentes, variação de volume, consumo de memória isolado e análise de gargalos. A pré-contagem e as passagens de exportação ainda precisam ser avaliadas em acervos grandes.
4. **Acessibilidade e apresentação:** faltam leitor de tela, alto contraste, múltiplos monitores, matriz completa de DPI/resolução, revisão editorial e avaliação estética pelo usuário. As tabelas continuam exigindo rolagem horizontal para todos os detalhes; a configuração usa rolagem vertical. Barras, combos e títulos nativos podem conservar aparência do Windows no tema escuro.
5. **Tempos por fase no JSON e separação de interação:** a contaminação demonstrada pela espera do diálogo foi removida, mas ainda não há uma telemetria completa com todas as fases e um tempo separado de interação do operador.
6. **Busca avançada na auditoria:** paginação, total e exportação integral entregues; busca textual adicional na auditoria permanece melhoria.
7. **Cenários não repetidos:** OneDrive empresarial não teve execução visual específica; limpeza independente de pastas vazias não foi novamente acionada; a nova confirmação de restauração com lote não vazio não teve um novo ciclo visual, embora compartilhe o componente rolável da quarentena. As verificações aprovadas da rodada anterior continuam identificadas como anteriores.
8. **Avisos de testes:** permanecem quatro avisos MSTEST0037 preexistentes. Não são falhas dos 253 testes. O build específico do Desktop concluiu com 0 erros e 0 avisos.
9. **Refatoração estrutural:** não foi feita uma reescrita do núcleo nem uma extração completa da coordenação dos formulários para serviços de apresentação. Esse trabalho foi reservado pelo usuário para outra etapa.

Essas pendências são discriminadas para evitar uma declaração artificial de aprovação total. Não há, nos cenários executados, uma divergência funcional conhecida equivalente às falhas P1/P3 originais que tenha sido deixada sem correção.

## Continuidade no desktop

Leia [RETOMADA_CLEAR.md](../desenvolvimento/RETOMADA_CLEAR.md) e o registro [PROGRESSO_MELHORIAS_CLEAR.md](../desenvolvimento/PROGRESSO_MELHORIAS_CLEAR.md). O repositório original deve ser aberto na branch `develop`, após conferir sua sincronização. O executável fica em `src\Clear.Desktop\bin\Release\net10.0-windows\Clear.Desktop.exe` depois da compilação Release.

Os bancos locais de histórico e auditoria permanecem em `%LOCALAPPDATA%\CLEAR`; não são parte do repositório. A preferência de tema fica em `%LOCALAPPDATA%\CLEAR\preferencias-interface.json`. Não substituir esses bancos ao transportar código. O acervo e o gabarito são externos e devem ser preservados.

## Registro de entrega

Integração: https://github.com/leucimar/TCC-Software-CLEAR/pull/21. O código integrado corresponde ao merge `bbfc074`; a atualização documental final é posterior a esse merge na própria develop. No fechamento da entrega, conferir o SHA corrente da develop contra origin/develop; a divergência deve ser `0 0` e a árvore deve estar limpa. A mesma conferência foi utilizada nas duas cópias locais.
