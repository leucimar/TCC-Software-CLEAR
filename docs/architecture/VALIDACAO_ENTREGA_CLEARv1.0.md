# CLEARv1.0 — validação e entrega

Data: 09/11/2026. Identificação pública: **CLEARv1.0**. Versão dos assemblies: `1.0.0.0`; versão informativa/produto: `CLEARv1.0`. `Directory.Build.props` fixa a identificação e evita usar hash de commit no nome informativo da versão.

## Resultado

Regressão: **283 testes aprovados, zero falhas** (Domain 91, Application 71, Infrastructure 118, Desktop 3). Inclui os quatro testes antes pendentes de descritores/permissões NTFS. A compilação do Desktop passou sem erros/avisos; permanecem seis sugestões de estilo MSTEST0037 nos testes.

Integração física: **63 verificações aprovadas, zero falhas**, com o instrumento versionado em `benchmarks/Clear.ValidacaoControlada`, referenciando os projetos reais da solução. Conclusão final em:

`D:\TCC_Leucimar\Base_Dados\Validacao_Controlada_CLEAR\08_relatorios\CLEARv1_0_20261109_230442`

Uma passagem preliminar, também com 63 verificações aprovadas, está em `CLEARv1_0_20261109_230215`; a passagem final do instrumento versionado é a referência desta entrega.

## Escopo efetivamente executado

- Base controlada de 558 arquivos e 124.206.815 bytes, copiada para nova árvore em `03_experimental`. A base de aproximadamente 100 GB não foi usada.
- Integridade do acervo original antes/depois, sem reescrever suas datas. Leituras protegidas, sem fallback. Datas controladas aplicadas apenas à cópia.
- Inventário, categorias arquivo a arquivo contra o oráculo, filtros, incompatibilidades selecionadas, deduplicação com 1/8 leitores, progresso/falhas/cancelamento, checkpoint, relatórios, SQLite, seleção/paginação e métricas Excel.
- Quarentena e restauração reais na cópia: recusa sem confirmação, movimentação íntegra, comparação de DACL/proprietário/grupo do arquivo, proteção de herança da pasta espelhada, conflito de restauração sem sobrescrita e auditoria persistida.
- Mesmos grupos nas duas passagens: 17 grupos, 57 membros, 67.139.344 bytes potencialmente redundantes.
- Casos virtuais de limites de tamanho/nome e falhas são explicitamente separados dos totais físicos e não representam criação de arquivos com terabytes.

Não foi necessário alterar as regras de classificação para os testes passarem. A publicação inclui a rotina de segurança implementada anteriormente e agora exercitada localmente. Não equivale a teste de acesso efetivo com três usuários AD: SIDs sintéticos nos testes de descritores não são logins reais. Não houve alteração de AD nem criação/alteração de compartilhamentos SMB. Preservam-se os limites documentados de privilégios, concorrência externa de ACL e estados pendentes em falha após movimento.

## Evidências

`verificacoes.json`, `conclusao.json`, `desempenho.json`, relatórios CSV/JSON por passagem, métricas SQLite/CSV, auditoria de quarentena e `preservacao_execucao.json/jsonl/csv`. Os TRX da regressão final são copiados para `regressao` na mesma pasta.

Os tempos são observações locais com cache/carga não controlados. As APIs do programa foram executadas sobre arquivos físicos; não houve nova automação visual completa nem teste de AD/SMB/VM/planilhas. A interface já havia sido conferida pelo usuário. Não ampliar essas conclusões no artigo.

## Entrega

- Código consolidado publicado na `develop` e identificado pela tag `CLEARv1.0`; sem alteração da `main`.
- Executável local: `artifacts/CLEARv1.0_validacao/bin/Clear.Desktop/release/Clear.Desktop.exe`.
- Atalho local `CLEAR - Correções locais.lnk` atualizado para esse executável.
- Documento para o outro chat: `docs/architecture/ORIENTACOES_ARTIGO_CLEARv1.0.md`.
- Dados, executáveis e bancos da execução permanecem locais. Fontes, testes, instrumento de reprodução e documentação integram a entrega. Os diagramas locais não rastreados permanecem preservados fora deste commit de código.

A parte 2 não foi iniciada. Aguardar comando explícito do usuário para configurar e executar a bateria Azure Files/SharePoint.
