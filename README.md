# CLEARv2.0 — TCC

Aplicativo desktop gratuito C#/.NET 10 para **pré-análise e saneamento assistido** de arquivos antes de futuras migrações para a nuvem. **O CLEAR não realiza migração para a nuvem.** Inventário, classificação, relatórios e histórico são somente leitura; o M7 e o M8 podem movimentar arquivos individualmente, **apenas após confirmação humana explícita**.

## Tecnologias

.NET 10 LTS (SDK 10.0.402), WinForms, quatro camadas (Domain, Application, Infrastructure, Desktop), MSTest e **SQLite via Microsoft.Data.Sqlite 10.0.13 e SQLitePCLRaw.bundle_e_sqlite3 3.0.5** (dependência NuGet gratuita, restaurada por `dotnet restore`).

## Funcionalidades

- M2: inventário de arquivos/diretórios, tamanhos e ocorrências;
- M3: classificação de arquivos vazios, backups/temporários e sinais temporais; verificações independentes de compatibilidade para Azure Files SMB/Windows ou SharePoint Online/OneDrive empresarial; limite de tamanho personalizado;
- M4: detecção opt-in de duplicatas exatas por tamanho, SHA-256 em streaming e comparação byte a byte;
- M5 Parte 1: exportação CSV completa (com filtros de achados), resumo JSON e CSV de membros de grupos M4;
- **M6 (integrado à develop):** histórico SQLite automático e transacional, armazenando entradas, evidências, parâmetros, ocorrências e grupos confirmados, com consulta detalhada paginada na interface;
- **M7 (integrado à develop):** quarentena individual mediante confirmação humana, validação SHA-256 e auditoria SQLite;
- **M8 (integrado à develop):** restauração individual de um arquivo auditado como EmQuarentena, com confirmação humana adicional, SHA-256, proteção contra sobrescrita e estados RestauracaoPendente / Restaurado;
- **M9 Parte 1 (integrada à develop):** concorrência limitada (1–8) no SHA-256 de M4, métricas observacionais e benchmark sintético com resultados JSON;
- **M9 Parte 2 (feature em validação):** checkpoints opcionais de progresso para **reinício integral assistido**, sem reutilização de resultados, e limite defensivo configurável de arquivos indexados na deduplicação.

## Compatibilidade de destino (M3-3B-3)

Selecione um perfil em **Objetivo Esperado**. Para SharePoint, preencha **Biblioteca do SharePoint** com o caminho decodificado do site e da biblioteca, por exemplo `sites/Equipe/Documentos`, sem o domínio. A **Subpasta de destino** é opcional nos dois perfis e fica em branco quando o acervo vai para a raiz da biblioteca ou do compartilhamento. O CLEAR soma o destino projetado e o caminho relativo do item. SharePoint sem biblioteca informa **Não avaliado** na regra de comprimento.

- **Azure Files SMB.** O catálogo verifica nomes, caracteres inválidos, 255 caracteres por componente, 2.048 no caminho, 250 subpastas e 4 TiB por arquivo. Particularidades de ponto final e `U+E000` são alertas de FileREST.
- **SharePoint Online.** O catálogo verifica nomes reservados, 400 caracteres no caminho decodificado e 250 GB por arquivo, conforme a leitura decimal do valor publicado. Tipos condicionados ao site ou ao cliente OneDrive aparecem como **alertas de compatibilidade**. Políticas específicas da organização exigem confirmação no ambiente de destino.

## Histórico SQLite (M6)

O CLEAR salva as análises em %LOCALAPPDATA%\CLEAR\historico.sqlite3. Análises concluídas ou parciais conservam suas entradas. Uma tentativa interrompida registra o estado e o motivo, com as entradas incompletas descartadas.

Use o seletor **Análise exibida** para consultar o histórico. Ele mostra número sequencial, horário local e nome da pasta. O **Inventário** traz os arquivos, classificações, grupos de duplicatas, justificativas e ocorrências. As abas **Configurações da análise**, **Desempenho** e **Auditoria** acompanham a mesma análise. O painel superior abre expandido e se recolhe após a conclusão.

**Importante:** o banco pode conter nomes, caminhos e hashes confidenciais. O SQLite **não criptografa os registros automaticamente**. O histórico ainda não é cache; o software **não reaproveita classificações ou hashes** de execuções anteriores. A otimização depende de estudo de invalidação e medições em M6 posterior/M9. O Inventário usa consultas paginadas.

## Compilar e testar

```powershell
dotnet restore CLEAR.slnx
dotnet build CLEAR.slnx -c Release --no-restore
dotnet test CLEAR.slnx -c Release --no-build --no-restore
dotnet run --project src/Clear.Desktop/Clear.Desktop.csproj
```

Roteiros: `docs/tests/Roteiro_Validacao_M6_Parte1.md`, `docs/tests/Roteiro_Validacao_M7_Parte1.md` e `docs/tests/Roteiro_Validacao_M8_Parte1.md` e `docs/tests/Roteiro_Validacao_M9_Parte1.md` e `docs/tests/Roteiro_Validacao_M9_Parte2.md`.

## Quarentena M7 — somente com confirmação humana

No **Inventário**, selecione **No acervo**, aplique os filtros e marque os arquivos. Ao selecionar duplicatas em lote, o CLEAR mantém pelo menos uma cópia de cada grupo na origem. Para trocar a cópia preservada, desmarque primeiro outra cópia do grupo.

Clique em **Mover para quarentena** e confira a quantidade, a origem e o destino. Após sua confirmação, o serviço revalida os itens e preserva o controle de acesso NTFS. A quarentena deve ficar no mesmo volume do acervo e fora dele. O histórico de movimentos é salvo em %LOCALAPPDATA%\CLEAR\quarentena.sqlite3.

## Restauração assistida

No **Inventário**, selecione **Em quarentena**, marque os arquivos e clique em **Restaurar selecionados**. A confirmação agrupa a origem e o destino quando há mais de uma raiz no lote. A restauração revalida os arquivos por SHA-256 e recusa caminhos ocupados. Consulte o estado das operações ou exporte o CSV pela aba **Auditoria**.

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

## Sub-base experimental estratificada (feature em validação)

Botão **Criar sub-base experimental (SOMENTE CÓPIA)** nos filtros gerais. Selecione origem, pasta de destino **nova e fora da origem**, percentual (padrão 30%) e semente. **Calcular prévia** estima quantidade, volume e distribuição por pacote/pasta principal, extensão e faixa de tamanho; **Confirmar e COPIAR** exige resposta humana afirmativa.

**Invariante:** todos os arquivos escolhidos são COPIADOS e continuam presentes na base original; a ferramenta não move, exclui nem sobrescreve arquivos da origem. As cópias preservam seus caminhos relativos dentro de Acervo e são verificadas via SHA-256 origem/destino. Quantidade de arquivos segue a meta exata quando possível; bytes ficam próximos do alvo, com percentuais efetivos expostos, já que arquivos não podem ser divididos. Pastas sem nenhum arquivo escolhido podem não aparecer.

Os manifestos resumo JSON e arquivos JSONL tornam a seleção reproduzível. A cópia é construída em um diretório temporário marcado como EM_CONSTRUCAO e somente publicada ao concluir; cancelamento ou falha deixam esse estágio incompleto para inspeção/limpeza manual. O destino não pode conter o acervo nem já existir. Primeiro valide com dados sintéticos em C:\Temp; não executar imediatamente sobre os 86 GB.

Detalhes e riscos: docs/architecture/Marco_SubBase_Experimental_Estratificada.md e docs/tests/Roteiro_Validacao_SubBase_Experimental.md. **Novos testes ainda precisam ser executados no Windows antes de qualquer integração.** A seleção de pasta de quarentena, preservação física da hierarquia na quarentena e seleção parcial por filtros são outros incrementos.

## Seleção e progresso

- Os filtros do Inventário podem ser usados no acervo e na quarentena. A seleção permanece ao mudar de página ou aplicar filtros. O contador informa quando há arquivos marcados fora dos filtros. Trocar a análise ou alternar entre acervo e quarentena limpa as marcações.
- A aba Desempenho apresenta indicadores, gráfico por etapa e uma tabela de medições exportável em CSV. O histórico conserva os horários em UTC e a interface apresenta o horário local.

## Versões

O CLEARv2.0 está em validação na branch CLEARv2.0-Refactory-Layout e na develop. A branch CLEARv1.0 preserva a versão anterior.
