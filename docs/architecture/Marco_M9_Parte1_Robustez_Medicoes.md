# M9 Parte 1 — Concorrência limitada, medição e benchmark

Estado: desenvolvimento experimental na branch feature/robustez-medicoes-m9. Não compilado pelo assistente, sem merge.

## Objetivo

Tornar o processamento de grandes acervos mais mensurável e previsível, mantendo o CLEAR gratuito, não migratório e sem operações automáticas sobre arquivos. O M9 aborda robustez, desempenho, limites de concorrência, interrupções e futuramente retomada segura. Nesta primeira parte, implementam-se medição e limite de SHA-256, não retomada.

## Implementação

1. No M4, parâmetro maximoHashesSimultaneos de 1 a 8, padrão 1. Apenas a leitura SHA-256 dos candidatos por tamanho é concorrente; verificação de conteúdo byte a byte continua sequencial.
2. Tarefas trabalham em lotes pequenos de tamanho até N. A consolidação de hashes e falhas conserva a ordem dos caminhos, visando resultados determinísticos. O token de cancelamento é propagado; falhas recuperáveis geram ocorrências e resultado parcial.
3. A interface WinForms expõe o limite 1..8 somente quando deduplicação está selecionada. Não ativa o filtro por conta própria.
4. Nova aba Métricas M9 apresenta tempos observados do inventário, deduplicação, execução completa, arquivos/s e volume inventariado/s, com indicação de análise parcial. Cancelamento e falha não geram métricas conclusivas.
5. Console benchmarks/Clear.Benchmarks.M9 gera acervo sintético próprio em C:\Temp ou %TEMP%, com semente fixa 5721, executa 1/2/4 concorrências em três repetições por padrão e grava JSON fora da raiz analisada. Inclui SO, versão .NET, CPU processadores lógicos, tempos, grupos confirmados, ocorrências, alocações gerenciadas e memória de processo observadas.
6. Cinco testes de concorrência verificam limites, equivalência de resultados para 1/8, erro parcial, cancelamento e limites inválidos.

## Metodologia e cautelas

- Tempo do inventário é somente metadados; volume inventariado/s NÃO mede throughput físico de disco. M4 inclui segunda enumeração, SHA-256 e comparação byte a byte.
- O pico de working set é acumulado no processo, não representa pico por rodada. Cache do Windows, antivírus, hardware e tarefas de fundo não são controlados.
- Teste com dados sintéticos e compare repetidamente, sem alegar ganho até analisar evidências experimentais no M10.
- O console gera arquivos de tamanho limitado, fora das pastas do usuário; não modifica nenhum acervo externo.
- M4 ainda mantém lista de caminhos agrupados em RAM; limite de tarefas não resolve essa dependência de memória.
- Cache automático de classificações/hashes permanece proibido até haver invalidadores por alteração de conteúdo, versão de regra/perfil e parâmetros e ganho medido.
- Retomada automática ou reprocessamento de análise parcial não foi desenvolvido; interrupções ainda invalidam resultados, conforme a política existente.

## Critérios de aceite

- Restore/build Release e auditoria NuGet sem avisos, 198 testes esperados (193 anteriores + cinco novos).
- Duplicatas e contagens invariantes com concorrência 1 vs 4/8; limite de leituras observado respeitado.
- Cancelamento sem falsa conclusão, falhas parciais com ocorrência.
- Benchmark gera JSON com 9 amostras por padrão (3 repetições x 3 níveis), grupos idênticos entre parâmetros e saída separada dos arquivos sintéticos.
- Testes da interface WinForms por usuário, foco no núcleo. Não fazer merge sem aprovação e não alterar main.

## Próximos incrementos M9

Checkpoint e retomada com validação de integridade de origem, parâmetros e versões de regras; política para falhas e reinício; limites de memória e análise de throughput de grandes arquivos; otimizações somente após baseline reprodutível.
