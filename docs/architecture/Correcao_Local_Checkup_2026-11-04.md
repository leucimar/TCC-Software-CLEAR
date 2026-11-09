# CLEAR — correções locais e reteste

Data local: 04/11/2026. Branch **fix/checkup-concorrencia-metricas**, criada da develop em 90ffe08a3788b2b01085b8cfdbd50a0d31073070. Alterações locais sem commit, merge, push ou publicação no GitHub.

## Resultado

P01–P03 corrigidos e reproduções transformadas em testes de regressão. P04 corrigido no fluxo de análise, compilado e acompanhado de teste que preserva a recusa de quarentena entre volumes; a interface não foi operada nesta etapa, por exclusão expressa do usuário. L01 implementado com banco próprio de métricas e exportações tabulares. Não se alteraram as regras de compatibilidade/classificação, a exigência de confirmação humana ou a comparação byte a byte.

## Alterações

- **P01/P02:** fila global limitada de candidatos, com workers que repõem slots livres continuamente. Não há barreira por lote/tamanho. Só arquivos candidatos por tamanho são lidos. Consolidação de hashes, erros e grupos mantém ordem determinística; confirmação de bytes segue sequencial. Permanecem limites de leitores 1–8 e quantidade de arquivos indexados; o índice e os resultados de hash usam memória O(N), sem promessa de limite exato em bytes.
- **P03:** progresso distingue candidatos processados, hashes válidos e falhas. Conclusão 4/4 com um erro informa 3 hashes e 1 falha, nunca 4 hashes válidos. Erros continuam marcando resultado parcial. Eventos são limitados a aproximadamente um a cada 100ms, além do primeiro/final, para reduzir mensagens à UI sem afirmar um teste visual de responsividade.
- **P04:** a análise somente leitura não valida antecipadamente a pasta de uma quarentena não utilizada. M7 continua validando origem, destino, mesmo volume, integridade e confirmação antes de movimentar. O teste novo de outro volume não cria pasta nem move arquivo.
- **L01:** MetricasAnalise + RepositorioMetricasAnalise. Persistência independente em `%LOCALAPPDATA%/CLEAR/metricas.sqlite3`, tabela `metricas_analises`. O histórico M6 e sua versão de esquema são preservados. Salva execuções concluídas, incluindo as concluídas com ocorrências; canceladas/falhas sem resultado não são publicadas como medição completa.

## Como acessar a versão corrigida

Atalho: `D:\TCC_Leucimar\Programa_CLEAR\CLEAR - Correções locais.lnk`.

Executável corrigido: `D:\TCC_Leucimar\Programa_CLEAR\artifacts\correcao_2026-11-04\bin\Clear.Desktop\release\Clear.Desktop.exe`.

A janela que já estava aberta não foi encerrada nem substituída. Ela usa a versão anterior em src/Clear.Desktop/bin/Release/net10.0-windows. Feche-a normalmente e use o atalho novo para testar a correção. O build isolado em artifacts é ignorado pelo Git. Não atribuir as correções ao executável antigo nem somente ao SHA do commit, pois ainda não houve commit local; os hashes da compilação e das fontes estão nas evidências.

## Métricas e Excel

Os dados são salvos ao concluir a análise, independentemente de habilitar M6. Com a opção já existente de exportar relatório ativada, o desktop gera também CLEAR_Metricas_<id>.csv (execução atual) e CLEAR_Metricas_Historico_<id>.csv (todas as medições persistidas). Os caminhos são informados na aba Desempenho. Não houve redesenho da interface. ConsultarAsync e ExportarCsvAsync também permitem a exportação consolidada via API.

CSV UTF-8 com BOM, separador ponto e vírgula e números pt-BR, adequado para importação no Excel em português. Cada linha representa uma categoria da execução e repete volume, contagens, leitores e tempos de inventário, duplicatas e total. Percentuais de categorias não avaliadas ou população vazia ficam em branco; zero significa avaliação feita sem achados. A contagem de compatibilidade inclui pastas e usa como denominador arquivos + diretórios; as demais categorias usam arquivos. Categorias podem se sobrepor e não devem ser somadas como porcentagem global de descarte. Percentuais são de indícios/achados, não de arquivos automaticamente condenados à migração.

O tempo total inclui relatórios/histórico quando ativos, mas exclui preparação anterior ao cronômetro e gravação/exportação das próprias métricas. Falha de persistência/exportação de métricas é informada sem reclassificar uma análise concluída como falha. Arquivos existentes não são sobrescritos. Os registros ficam fora do acervo; saídas internas e links/junctions são recusados.

## Reteste na SubBase 30%

93.458 arquivos, 24 diretórios, 26.297.356.057 bytes. Comparação com o resultado anterior e com o oráculo independente já registrado: **105 grupos, 228 membros e 43.458.881 bytes potencialmente redundantes**, sem divergências. SQLite validado; 93.482 entradas. Nenhum arquivo do acervo foi movido, excluído ou reescrito. Metadados de todos os arquivos e SHA de 40 amostras conferem com o manifesto histórico; isso não é hash integral.

| Leitores | Antes, reteste com cache aquecido (s) | Depois (s) | Pico observado depois |
|---|---:|---:|---:|
| 4 | 7.1944 | 3.3089 | 4 |
| 8 | 6.9933 | 2.6073 | 8 |
| 1 | 7.9594 | 7.7232 | 1 |

A primeira passagem corrigida com um leitor foi 8.0202s. A primeira passagem antiga, 205.5900s, não foi usada para estimar ganho, por condições distintas de cache. Os números são observacionais locais, sem controle de cache, antivírus ou uso do computador; não constituem promessa de aceleração nem tempo medido do desktop.

### Execução completa adicional via APIs

- Inventário/classificação: 6.7371s.
- Deduplicação, 8 leitores: 2.7721s.
- Total observado do ensaio completo: 10.7075s.
- Métricas gravadas, reabertas do SQLite e exportadas: 6 linhas de categorias; 228/93.458 = 0,24395985% de membros de grupos de duplicatas. Sinal temporal e limite pessoal estavam desativados e não foram apresentados como avaliados.

A execução completa_final é a referência para a tabela de tempos completos. O ensaio reteste contém quatro passagens M4 para comparação, não uma execução simples; seus totais/descrições são de instrumento e não devem ser usados como tempo de uma única análise. A pasta execucao_completa anterior é preliminar e contém uma descrição do instrumento posteriormente corrigida; não é a referência final.

## Testes

261 aprovados: Domain 87, Application 71, Infrastructure 103. Oito testes novos: quatro de fila/progresso, três de métricas/persistência/exportação e um de segurança entre volumes. Quatro avisos MSTEST0037 preexistentes permanecem. Desktop compilado com zero erros e zero avisos.

As reproduções controladas demonstram ocupação entre tamanhos, reposição de slot antes do arquivo lento finalizar, progresso final com falha sem falso hash e conclusão sem candidatos. Os testes anteriores continuam cobrindo cancelamento, limite, colisão artificial de hash, estabilidade e operações auditadas. A prova visual de fluxos do desktop não foi realizada.

## Evidências e continuação

Evidências: `D:\TCC_Leucimar\Base_Dados\02_SubBase_30pct\09_evidencias\Correcao_2026-11-04`.

regressao_fechamento/*.trx; reteste/concorrencia.json, falha_progresso.json e concorrencia_por_faixa.json; execucao_completa_final/ contém métricas, relatórios e bancos isolados; validacao_final.json confere a entrega; codigo_local/ e versao_local.json identificam fontes e binários.

As limitações intencionais de seleção global até 10.000, amostra visual e reinício integral não foram removidas. A falha da ferramenta de captura/clique não foi tratada como defeito de produto. Os diagramas anteriores retratam a versão 90ffe08 e ainda não mostram a nova tabela/banco de métricas; precisam ser revisados antes de representar esta branch.

Toda a correção está local e não foi enviada ao GitHub. A branch de testes de integração que exige código igual ao da develop não foi utilizada para modificar código. As correções foram isoladas em uma branch fix.
