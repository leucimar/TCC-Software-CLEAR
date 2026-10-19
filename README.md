# CLEAR — TCC

Aplicativo desktop gratuito C#/.NET 10 para **pré-análise e saneamento assistido** de arquivos antes de futuras migrações para a nuvem. **O CLEAR não realiza migração para a nuvem.** Inventário, classificação, relatórios e histórico são somente leitura; o M7 e o M8 podem movimentar arquivos individualmente, **apenas após confirmação humana explícita**.

## Tecnologias

.NET 10 LTS (SDK 10.0.402), WinForms, quatro camadas (Domain, Application, Infrastructure, Desktop), MSTest e **SQLite via Microsoft.Data.Sqlite 10.0.13 e SQLitePCLRaw.bundle_e_sqlite3 3.0.5** (dependência NuGet gratuita, restaurada por `dotnet restore`).

## Funcionalidades

- M2: inventário de arquivos/diretórios, tamanhos e ocorrências;
- M3: classificação de arquivos vazios, backups/temporários e sinais temporais; verificações independentes de compatibilidade para Azure Files SMB/Windows ou SharePoint Online/OneDrive empresarial; limite de tamanho personalizado;
- M4: detecção opt-in de duplicatas exatas por tamanho, SHA-256 em streaming e comparação byte a byte;
- M5 Parte 1: exportação CSV completa (com filtros de achados), resumo JSON e CSV de membros de grupos M4;
- **M6 (integrado à develop):** histórico SQLite opcional e transacional, armazenando entradas, evidências, parâmetros, ocorrências e grupos confirmados, com consulta detalhada paginada na interface;
- **M7 (integrado à develop):** quarentena individual mediante confirmação humana, validação SHA-256 e auditoria SQLite;
- **M8 (integrado à develop):** restauração individual de um arquivo auditado como EmQuarentena, com confirmação humana adicional, SHA-256, proteção contra sobrescrita e estados RestauracaoPendente / Restaurado;
- **M9 Parte 1 (integrada à develop):** concorrência limitada (1–8) no SHA-256 de M4, métricas observacionais e benchmark sintético com resultados JSON;
- **M9 Parte 2 (feature em validação):** checkpoints opcionais de progresso para **reinício integral assistido**, sem reutilização de resultados, e limite defensivo configurável de arquivos indexados na deduplicação.

## Histórico SQLite (M6)

Marque **Salvar histórico local SQLite (M6; não reutiliza classificações)** nos filtros gerais. O histórico é gravado em `%LOCALAPPDATA%\CLEAR\historico.sqlite3`, fora do acervo de testes `C:\Temp`. Os dados são escritos em uma transação por execução; interrupção/falha antes da conclusão causa rollback. Falhas individuais enumeradas que permitam concluir geram status **Parcial**, sem sinalizar análise integral.

A aba **Histórico SQLite — M6** permite atualizar e consultar resumos de execuções passadas, inclusive após fechar o programa. Selecione uma execução e clique **Consultar detalhes da execução** (ou dê duplo clique na linha) para ver seus arquivos e classificações em páginas de 200 itens, justificativas/evidências, verificações e membros dos grupos de duplicatas. Tudo é consultado no banco, sem reanalisar a pasta. Todos os registros de entradas são persistidos, mesmo além da amostra de 1.000 linhas. A exportação M5 continua opcional e pode ser usada ao mesmo tempo, sem uma nova varredura apenas para salvar os dados.

**Importante:** o banco pode conter nomes, caminhos e hashes confidenciais. O SQLite **não criptografa os registros automaticamente**. O histórico ainda não é cache; o software **não reaproveita classificações ou hashes** de execuções anteriores. A otimização depende de estudo de invalidação e medições em M6 posterior/M9. A aba permite consulta histórica paginada, mas filtros de pesquisa avançada, edição de decisões e reutilização automática dos resultados ficam para outros incrementos.

## Compilar e testar

```powershell
dotnet restore CLEAR.slnx
dotnet build CLEAR.slnx -c Release --no-restore
dotnet test CLEAR.slnx -c Release --no-build --no-restore
dotnet run --project src/Clear.Desktop/Clear.Desktop.csproj
```

Roteiros: `docs/tests/Roteiro_Validacao_M6_Parte1.md`, `docs/tests/Roteiro_Validacao_M7_Parte1.md` e `docs/tests/Roteiro_Validacao_M8_Parte1.md` e `docs/tests/Roteiro_Validacao_M9_Parte1.md` e `docs/tests/Roteiro_Validacao_M9_Parte2.md`.

## Quarentena M7 — somente com confirmação humana

Na consulta detalhada de uma execução M6 concluída, selecione um arquivo e clique **Revisar envio à quarentena (M7)**. O CLEAR não move arquivos por conta própria: primeiro verifica metadados e SHA-256, apresenta origem e destino e exige resposta afirmativa explícita. Uma nova verificação antes de mover rejeita arquivos modificados desde a análise.

O destino padrão é %LOCALAPPDATA%\CLEAR\Quarentena. Uma auditoria independente é mantida em %LOCALAPPDATA%\CLEAR\quarentena.sqlite3, com estados Pendente/EmQuarentena/Falha. A aba de auditoria permite consultar operações vinculadas à análise histórica.

**Atenção:** esta etapa muda a localização de arquivos quando autorizada. O M8 experimental permite restauração individual confirmada pelo usuário. **Use apenas dados sintéticos em C:\Temp enquanto validamos.** Nunca exclua manualmente arquivos de quarentena nem o banco de auditoria. Não há limpeza automática, exclusão, migração ou escolha automática de arquivos.

## Restauração assistida M8 — sem sobrescrita

Na aba Auditoria da quarentena (dentro da consulta histórica M6), selecione uma operação EmQuarentena e clique **Revisar restauração (M8)**. A preparação verifica o conteúdo com SHA-256 e se a pasta original ainda existe e está livre. Responder Não não altera o arquivo nem o status. Responder Sim provoca nova revalidação, grava RestauracaoPendente, move o arquivo de volta com overwrite=false e registra Restaurado ao concluir.

Status Pendente do M7 e RestauracaoPendente do M8 são situações incertas: não repetir a ação automaticamente. O M8 não corrige caminhos ausentes, não sobrescreve nomes existentes e não restaura em lotes. O banco SQLite da auditoria M7 permanece no esquema v1, sem migrações destrutivas.

## M9 — Robustez e medições reproduzíveis

Com a detecção de duplicatas ativada, configure de 1 a 8 **leituras SHA-256 simultâneas** (padrão 1); os resultados são consolidados na ordem dos caminhos. A comparação byte a byte continua sequencial, e o limite evita criar tarefas para todos os arquivos ao mesmo tempo. A nova aba **Métricas — M9** exibe tempo de inventário, deduplicação e total, arquivos/s e volume inventariado/s. Estas são medições locais e **não** conclusões científicas.

Execute o benchmark sintético gratuito:

```powershell
dotnet run --project benchmarks/Clear.Benchmarks.M9/Clear.Benchmarks.M9.csproj -c Release -- --arquivos 500 --bytes 4096 --repeticoes 3
```

O programa criará em C:\Temp (ou %TEMP%) um acervo sintético com conteúdo determinístico e gravará **medicoes_m9.json** na pasta pai. A saída contém 9 amostras (3 concorrências x 3 repetições), tempos, CPU, hashes, grupos e observações de memória; ver roteiro de validação M9.

Nenhuma classificação ou hash antigo é reutilizado automaticamente. A Parte 2 acrescenta um **checkpoint de progresso** fora da origem, em %LOCALAPPDATA%\CLEAR\Checkpoints, contendo estado, contadores e assinatura das configurações. Após uma interrupção, o usuário pode confirmar **Reiniciar análise interrompida (do início)**: os arquivos são lidos NOVAMENTE desde zero; não se trata de retomada incremental.

A Parte 2 também limita a quantidade de arquivos indexados simultaneamente em RAM pelo M4 (padrão 250.000, ajustável de 100 a 1.000.000 na interface). Ultrapassar o limite interrompe a deduplicação com mensagem explícita; não gera resultado completo. Esse limite não é limite exato de bytes de memória.

Uma retomada incremental real, com checkpoints de dados validados, limitação mais precisa de memória e reutilização segura de classificações/hashes continuam pendentes. Ações de quarentena/restauração continuam exigindo confirmação humana independente.

## Estado

M0–M2 integrados à main. M3–M8 e M9 Parte 1 integrados à develop. O usuário confirmou 198/198 testes, compilação sem avisos, benchmarks de 500 e 2.000 arquivos e controle de concorrência funcional na interface M9 Parte 1. M9 Parte 2 está em feature/checkpoint-reinicio-seguro-m9-parte2, sem compilação ou testes locais ainda (210 previstos); nenhum merge desta parte foi feito. A main permanece inalterada.
