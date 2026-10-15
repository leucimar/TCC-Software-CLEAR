# CLEAR — TCC

Aplicativo desktop gratuito em C#/.NET 10 para **pré-análise e saneamento assistido** de arquivos antes de futuras migrações para a nuvem. **O CLEAR não migra, renomeia, move ou descarta arquivos.**

## Tecnologias

C#/.NET 10 LTS (SDK 10.0.402), Windows Forms, projetos Domain/Application/Infrastructure/Desktop, MSTest, Git/GitHub. Sem ferramentas pagas.

## Funcionalidades disponíveis

- **M2:** inventário seguro de metadados de arquivos;
- **M3:** classificação de vazios, backups/temporários, sinais temporais opcionais, contagem de subpastas e perfis de compatibilidade exclusivos Azure Files SMB/Windows ou SharePoint Online/OneDrive for Business;
- **M4:** detecção opt-in de duplicatas exatas com pré-filtro por tamanho, SHA-256 em streaming e comparação byte a byte; grupos visualizados em aba própria;
- **M5 — Parte 1 (em feature):** geração opcional de relatório CSV completo por streaming, resumo JSON com parâmetros/totais/ocorrências e CSV de grupos M4 quando a deduplicação é executada.

### Relatórios M5

Marque **Exportar relatório completo (CSV + JSON — M5)** na interface e escolha **quais linhas serão exportadas**:
- Todas as entradas (arquivos e subpastas, mesmo além da amostra visual de 1.000);
- Somente indícios gerais;
- Somente incompatibilidades documentadas pelas regras habilitadas;
- Somente acima do limite pessoal;
- Somente achados: união das três situações anteriores.

Ao iniciar a análise, escolha uma **pasta de saída existente fora da pasta analisada**. Saída em CSV UTF-8 com BOM, separador `;`, escape de aspas e neutralização de fórmulas de planilhas. O JSON inclui contagens globais, configurações, horários UTC, avisos de incompletude e, se M4 estiver ativado, seus indicadores. O CSV de duplicatas lista todos os membros dos grupos, independentemente do limite de 200 linhas da aba visual.

O exportador recebe cada entrada durante o **inventário M3 original**, sem varredura adicional de metadados apenas para salvar o relatório. M4, se habilitado, já exige sua própria varredura e leitura de conteúdo. As linhas exportadas podem ser filtradas, mas o resumo sempre considera o inventário completo obtido.

A saída recebe nomes únicos com data e GUID, é publicada após conclusão e tem arquivos temporários removidos em caso de falha/cancelamento. **A geração cria arquivos de relatório na pasta indicada**, mas não altera o acervo analisado.

### Segurança e limites

- Nenhuma operação de migração, edição, movimentação ou exclusão do acervo.
- O conteúdo só é lido para SHA-256 quando solicitado. Duplicatas exatas não autorizam descarte.
- Relatórios podem conter caminhos completos e hashes sensíveis; trate-os com cuidado.
- A ausência de incompatibilidade detectada não garante migração.
- Exportação não é armazenamento SQLite, nem mecanismo de consultas persistentes/decisões humanas. **Reuso de classificações e hashes anteriores continua em estudo para M6/M9**, condicionado à segurança e medições.
- A memória usada pelo inventário visual é limitada por amostra; o índice de caminhos do M4 continua em RAM, e consultas/paginação sofisticadas ficam para incrementos posteriores.
- Teste apenas pastas sintéticas até concluir a validação.

## Compilar, testar e executar

```powershell
dotnet restore CLEAR.slnx
dotnet build CLEAR.slnx -c Release --no-restore
dotnet test CLEAR.slnx -c Release --no-build --no-restore
dotnet run --project src/Clear.Desktop/Clear.Desktop.csproj
```

Veja `docs/architecture/Marco_M5_Relatorios_Parte1.md` e `docs/tests/Roteiro_Validacao_M5_Parte1.md`.

## Estado

M0–M2 em `main`; M3 e M4 integrados à `develop`. M4 teve validação manual e a última captura do teste automatizado antes da correção mostrava 146/147; asserção foi corrigida em seguida, mas a execução completa final não foi registrada nesta conversa. **M5 Parte 1 em `feature/relatorios-completos-m5`, não testada localmente nem integrada.** `main` permanece inalterada.
