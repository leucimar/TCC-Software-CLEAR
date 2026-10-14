# Marco M3 — Parte 1: classificação explicável dos metadados

**Estado:** implementação pronta para validação pelo autor no notebook; sem aceite e sem merge.

## Escopo desta entrega

A primeira parte do M3 adiciona uma classificação determinística baseada **somente em metadados**:

1. **Arquivo vazio:** tamanho informado igual a zero bytes.
2. **Indício de temporário ou backup pelo nome:** somente prefixo `~$` ou terminações `.tmp`, `.bak`, `.backup`, com comparação sem diferenciação de maiúsculas/minúsculas nas terminações.

As regras são extensíveis por `IRegraClassificacao`; não foi acrescentado pacote NuGet. O mesmo arquivo pode receber mais de uma categoria e várias justificativas. Indício **não comprova** que um arquivo pode ser descartado e não causa alteração de arquivos.

A classificação ocorre para **todos os arquivos enumerados** (não apenas para os primeiros 1.000). A interface mostra a amostra de até 1.000 com categorias e justificativas e totais por categoria do inventário completo. A soma das categorias pode ultrapassar o total de arquivos com indícios, pois existe sobreposição.

## O que permanece fora do escopo

- Sinais temporais (`SinalTemporal`): dependem de política de limiar, referência temporal e distinção entre modificação e acesso.
- Caminhos potencialmente incompatíveis: dependem das regras documentadas para cada destino.
- Duplicatas exatas: pertencem ao M4 e precisam de algoritmo de hashing e testes específicos.
- Remoção, movimentação, quarentena, rollback e reparo de vínculos: proibidos nesta fase.
- Classificação por conteúdo, inferência de propriedade de documento ou recomendação automática de descarte.
- Testes em dados corporativos reais.

## Segurança e limites

A leitura continua somente de metadados, reutilizando o M2. A classificação é uma hipótese sobre nomenclatura; um arquivo legítimo pode terminar em `.bak` ou `.tmp`. A interface deve apresentar justificativas e não oferecer botão de remover.

Resultados podem ser parciais por erros de leitura; as categorias representam somente os arquivos efetivamente enumerados. O inventário não é transacional; arquivos podem mudar durante a execução. O limite de 1.000 se refere só à exibição, não à contagem e classificação.

## Critérios para aceite

- Compilação Release sem erros; execução da suíte automatizada.
- Testar pasta sintética com arquivo vazio, sufixos de backup/temporário, nome `~$` e arquivo normal.
- Verificar contagens multirrótulo e justificativas por linha; preservar os arquivos sem alterações.
- Executar testes negativos (nome normal e sufixos parecidos não recebem indício).
- Nenhum merge para `develop` nem `main` antes da aprovação explícita.

**Validação no desktop Windows 10:** ainda pendente. **Testes do código M3:** ainda não executados pelo autor.
