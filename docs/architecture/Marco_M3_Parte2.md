# Marco M3 — Parte 2: sinal temporal configurável

**Estado:** desenvolvido na branch feature/classificacao-temporal-m3. Pendente de compilação e aceite no notebook; não integrado à develop nem à main.

## Objetivo

Adicionar classificação explicável por data da última modificação **somente mediante ativação do operador**, sem inferir que um arquivo antigo seja inútil ou descartável.

## Regra e arquitetura

- RegraSinalTemporal implementa IRegraClassificacao no domínio, reaproveitando a abordagem de regras independentes do M3 Parte 1;
- comparação estritamente anterior ao instante de referência UTC menos N dias; igualdade no limite não gera indício;
- referência UTC imutável por varredura; configuração escolhida ao iniciar;
- limiar N: interface oferece 30 a 3650 dias, começando em 365; o domínio aceita de 1 a 3650 para permitir reutilização e testes;
- regra temporal desligada por padrão; regras antigas continuam aplicadas;
- data ausente, futura ou recente não gera indício; **último acesso não é usado** para alegar desuso;
- justificativa informa data de modificação, intervalo e referência UTC, com alerta de não descarte;
- sinais temporais combinam com arquivo vazio e nomes de backup/temporário; todos os arquivos enumerados entram nas contagens, inclusive além da amostra exibida.

## Segurança e limites

A leitura de metadados permanece na infraestrutura do M2. Nenhuma operação nova modifica arquivos, datas, nomes ou conteúdo. Datas antigas podem ser legítimas; alterações concorrentes e erros de enumeração continuam afetando exatidão/cobertura do inventário. Classificação por caminho de destino, hashes/duplicatas, migração, SQLite, quarentena e rollback ficam fora desta entrega. Não foram acrescentadas dependências ou licenças.

## Qualidade e portão de aceite

Testes novos: datas-limite, um tick anterior, futura/recente/ausente, UTC com fusos, validação do intervalo, desativação, múltiplas categorias, contagem além da amostra e integração real apenas em pasta temporária criada pelo teste. Esperam-se 60 testes no total, sujeitos à descoberta real. A execução no notebook e o teste manual com cenário sintético ainda precisam ser confirmados pelo autor; nenhuma aprovação é presumida.

O desktop Windows 10 continua pendente. Não fazer merge sem autorização explícita.
