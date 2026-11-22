# Orientação para o artigo — CLEARv1.0

## Nome da versão

A primeira versão entregável consolidada do programa deve ser denominada **CLEARv1.0**.

Ao mencionar a versão consolidada do software no resumo, metodologia, resultados, tabelas, legendas ou conclusões, use **CLEARv1.0**. Não substitua essa denominação por números/identificadores de commits do GitHub. A identificação pública é a tag `CLEARv1.0`, disponibilizada a partir da branch `develop`.

Exemplo: “Os ensaios de validação foram executados com o CLEARv1.0 sobre uma base de validação controlada.”

## O que integra a versão

- Inventário e classificação por arquivos vazios, nomes de backups/temporários, critério temporal exclusivo por acesso ou modificação e limite de tamanho configurável.
- Verificações de compatibilidade para os perfis Azure Files e SharePoint/OneDrive; não há envio real de dados a esses serviços.
- Deduplicação exata por SHA-256 e confirmação byte a byte, com fila global e até oito leitores simultâneos.
- Quarentena e restauração mediante confirmação, com hierarquia preservada e auditoria local.
- Preservação explícita da segurança NTFS na árvore recriada: DACL, proprietário e grupo; comparação de segurança antes/depois das movimentações e bloqueio/estado pendente em caso de falha. Compartilhamentos SMB continuam manuais.
- Tentativa de preservação do último acesso nas leituras de conteúdo; política selecionável para arquivos sem preservação e registro das ocorrências. Não é congelamento global das datas contra acessos de terceiros.
- Relatórios, histórico SQLite, métricas de tempo/volume e exportações CSV para Excel.

## Validação de fechamento — 09/11/2026

- **283 testes automatizados aprovados, zero falhas:** 91 Domain, 71 Application, 118 Infrastructure e 3 Desktop.
- **63 verificações de integração aprovadas, zero falhas**, usando as rotinas reais compiladas do CLEARv1.0 sobre uma cópia da base controlada.
- Base controlada: **558 arquivos de conteúdo, 124.206.815 bytes**. Os originais tiveram integridade, datas e tamanhos conferidos; as operações de quarentena/restauração foram realizadas somente na cópia de teste.
- As passagens com 1 e 8 leitores produziram os mesmos resultados esperados pelo oráculo: **17 grupos de duplicatas, 57 membros e 67.139.344 bytes potencialmente redundantes**. Isso é potencial identificado, não espaço efetivamente apagado.
- Foram conferidas permissões locais NTFS de arquivo e proteção de herança da pasta espelhada, além dos quatro testes específicos de descritores/permissões. Não foram criadas contas de domínio.

Esses ensaios de fechamento são validação funcional local, não um benchmark de produção. Condições de cache, antivírus e outras cargas não foram controladas. A execução física de integração utilizou as APIs reais do programa; não deve ser descrita como automação visual completa da interface. A conferência visual foi realizada pelo autor do TCC em etapa anterior.

## Limites de afirmação no artigo

- Não afirmar que houve validação com AD, três logins de domínio, servidor remoto, VM, SMB ou vínculos entre planilhas: esses cenários não foram executados.
- Não afirmar que o fechamento utilizou a base de aproximadamente 100 GB. Nesta etapa foi utilizada somente a base controlada.
- Não dizer que “sem alertas” garante migração, nem que indícios autorizam descarte automático.
- Não somar categorias sobrepostas como se fossem arquivos distintos. Um arquivo pode ser vazio, antigo e temporário ao mesmo tempo; totais de candidatos devem considerar a união dos conjuntos.
- Preservar as datas, configurações, bases e resultados dos ensaios históricos. A denominação CLEARv1.0 não autoriza atribuir retroativamente recursos novos ou novos testes a execuções anteriores.

## Próxima etapa

A bateria comparativa para Azure Files e SharePoint **ainda não foi executada**. Ela está prevista para uma etapa posterior, com identificação de arquivos vazios, backups/temporários, última modificação superior a 365 dias, tamanho superior a 50 MB, duplicatas exatas, até 1.000.000 de arquivos indexados e oito leitores SHA-256. As tabelas de quantidades/volumes/percentuais serão produzidas a partir dos resultados efetivos, sem inventar valores ou antecipar conclusões.

Portanto, cite o programa como **CLEARv1.0** e mantenha separados os resultados já obtidos e os experimentos ainda planejados.
