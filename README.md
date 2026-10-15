# CLEAR — TCC

Aplicativo desktop gratuito em C#/.NET 10 para **pré-análise e saneamento assistido** de arquivos antes de futuras migrações para a nuvem. **O CLEAR não migra, renomeia, move nem descarta arquivos.**

## Tecnologias e arquitetura

- C#/.NET 10 LTS, SDK 10.0.402 (`global.json`);
- Windows Forms e quatro camadas: Domain, Application, Infrastructure, Desktop;
- MSTest; Git/GitHub, sem dependências pagas.

## Capacidades

- M2: inventário de arquivos/bytes/ocorrências/cancelamento;
- M3 Parte 1: indícios de arquivos vazios, nomes temporários e backup;
- M3 Parte 2: sinal temporal configurável, desligado por padrão;
- M3 Parte 3A: inventário de subpastas e contratos de compatibilidade;
- M3 Parte 3B: sete verificações individuais de nomes/tamanho específicas por destino e filtro pessoal de tamanho;
- **M4 (feature em validação): detecção opcional de duplicatas exatas** com pré-agrupamento por tamanho, SHA-256 em fluxo e confirmação por comparação byte a byte.

### Compatibilidade por ambiente (M3)

Escolha um único cenário: Azure Files clássico via SMB/Windows, SharePoint Online, OneDrive for Business ou *Sem destino*. As verificações de destino podem ser habilitadas/desabilitadas individualmente. Azure usa caracteres de nome e 4 TiB; SharePoint/OneDrive empresarial usam caracteres de nome, espaço inicial/final, sequência `_vti_` e 250 GiB (base binária), sempre conforme o recorte documental aprovado. O filtro pessoal de tamanho funciona sem perfil e não substitui o limite oficial.

### Duplicatas exatas (M4)

A busca de duplicatas é **desligada por padrão** e deve ser habilitada em **Filtros gerais independentes do destino**. A busca:
1. Faz uma nova enumeração somente leitura de metadados e agrupa arquivos por tamanho;
2. Calcula **SHA-256 por stream apenas dos arquivos que compartilham tamanho**;
3. Compara diretamente o conteúdo em blocos antes de declarar um grupo de duplicatas exatas;
4. Mostra grupos e membros na aba `Duplicatas exatas — M4`. Os totais consideram todos os grupos; até 200 linhas de membros são renderizadas.

A leitura do conteúdo é **somente leitura**, mas aumenta I/O e tempo de processamento. Um arquivo alterado entre o inventário e a leitura é rejeitado quando as verificações de tamanho/data detectam a mudança. Arquivos inacessíveis, links/reparse points e falhas tornam o resultado **parcial**, nunca uma conclusão de que não existem duplicatas. Não há garantias transacionais em ambientes com escrita concorrente ou alterações adversariais de metadados; não usar resultados como autorização para descartar.

O indicador de bytes potencialmente redundantes assume **uma cópia conservada por grupo** e não equivale a espaço efetivamente liberado, economia monetária nem decisão de exclusão. O conteúdo de arquivos nunca é carregado inteiro na RAM; o índice de metadados e os grupos são mantidos em memória neste M4 (otimização para acervos enormes fica para M5/M6/M9). Hashes não são persistidos ou reutilizados automaticamente.

### Limites

O CLEAR ainda não exporta relatórios, persiste resultados em SQLite, permite decisões persistentes de usuário, faz quarentena, rollback ou migração. A análise lê metadados e, quando habilitada, conteúdo para SHA-256/comparação. Links identificados não são seguidos. Melhorias de layout e apresentações para grandes acervos estão planejadas em M5. Use apenas dados sintéticos em testes.

## Restaurar, compilar, testar, executar

```powershell
dotnet restore CLEAR.slnx
dotnet build CLEAR.slnx -c Release --no-restore
dotnet test CLEAR.slnx -c Release --no-build --no-restore
dotnet run --project src/Clear.Desktop/Clear.Desktop.csproj
```

Leia `docs/architecture/Marco_M4_Duplicatas_Exatas.md` e `docs/tests/Roteiro_Validacao_M4.md`.

## Estado

M0–M2 estão na `main`; M3 Partes 1, 2, 3A e 3B estão aprovadas na `develop` (119 testes no notebook após M3). **M4 está em `feature/duplicatas-exatas-m4`, sem compilação/teste local nem aceite neste momento.** A `main` permanece inalterada.
