# CLEAR — TCC

Aplicativo desktop gratuito C#/.NET 10 para **pré-análise e saneamento assistido** de arquivos antes de futuras migrações para a nuvem. **O CLEAR não realiza migração nem modifica o acervo analisado.**

## Tecnologias

.NET 10 LTS (SDK 10.0.402), WinForms, quatro camadas (Domain, Application, Infrastructure, Desktop), MSTest e **SQLite via Microsoft.Data.Sqlite 10.0.13 e SQLitePCLRaw.bundle_e_sqlite3 3.0.5** (dependência NuGet gratuita, restaurada por `dotnet restore`).

## Funcionalidades

- M2: inventário de arquivos/diretórios, tamanhos e ocorrências;
- M3: classificação de arquivos vazios, backups/temporários e sinais temporais; verificações independentes de compatibilidade para Azure Files SMB/Windows ou SharePoint Online/OneDrive empresarial; limite de tamanho personalizado;
- M4: detecção opt-in de duplicatas exatas por tamanho, SHA-256 em streaming e comparação byte a byte;
- M5 Parte 1: exportação CSV completa (com filtros de achados), resumo JSON e CSV de membros de grupos M4;
- **M6 (integrado à develop):** histórico SQLite opcional e transacional, armazenando entradas, evidências, parâmetros, ocorrências e grupos confirmados, com consulta detalhada paginada na interface;
- **M7 Parte 1 (feature em validação):** envio individual à quarentena mediante revisão e confirmação humanas explícitas, com revalidação de conteúdo por SHA-256 e auditoria SQLite. Testes devem usar exclusivamente acervos sintéticos; o M8 implementará restauração.

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

Roteiros: `docs/tests/Roteiro_Validacao_M6_Parte1.md` e `docs/tests/Roteiro_Validacao_M7_Parte1.md`.

## Quarentena M7 — somente com confirmação humana

Na consulta detalhada de uma execução M6 concluída, selecione um arquivo e clique **Revisar envio à quarentena (M7)**. O CLEAR não move arquivos por conta própria: primeiro verifica metadados e SHA-256, apresenta origem e destino e exige resposta afirmativa explícita. Uma nova verificação antes de mover rejeita arquivos modificados desde a análise.

O destino padrão é %LOCALAPPDATA%\CLEAR\Quarentena. Uma auditoria independente é mantida em %LOCALAPPDATA%\CLEAR\quarentena.sqlite3, com estados Pendente/EmQuarentena/Falha. A aba de auditoria permite consultar operações vinculadas à análise histórica.

**Atenção:** esta etapa muda a localização de arquivos quando autorizada; o M8 ainda não disponibiliza restauração. **Use apenas dados sintéticos em C:\Temp.** Nunca exclua manualmente arquivos de quarentena nem o banco de auditoria. Não há limpeza automática, exclusão, migração ou escolha automática de arquivos.

## Estado

M0–M2 integrados à main. M3–M6 integrados à develop e validados (M6: 172/172 testes e auditoria NuGet sem vulnerabilidades detectadas). M7 Parte 1 está na branch feature/quarentena-auditavel-m7, **ainda não compilada ou validada localmente**, com 10 testes adicionais previstos. A main permanece inalterada.
