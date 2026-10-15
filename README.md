# CLEAR — TCC

Aplicativo desktop gratuito em C#/.NET 10 para **pré-análise e saneamento assistido** de arquivos antes de futuras migrações para a nuvem. **O CLEAR não migra, renomeia nem descarta arquivos.**

## Tecnologias e arquitetura

- C#/.NET 10 LTS, SDK 10.0.402 (`global.json`);
- Windows Forms e quatro camadas: Domain, Application, Infrastructure, Desktop;
- MSTest; Git/GitHub, sem dependências pagas.

## Capacidades

- M2: inventário de arquivos/bytes/erros/cancelamento;
- M3 Parte 1: indícios de arquivos vazios e nomes temporários/backup;
- M3 Parte 2: sinal temporal configurável e desativado por padrão;
- M3 Parte 3A: inventário de subpastas, inclusive vazias, contratos para perfis e resultados;
- **M3 Parte 3B (branch de testes):** verificações locais por ambiente exclusivo, sete checkboxes independentes e filtro opcional de tamanho personalizado.

### Perfil de destino

Escolha **um**: Azure Files clássico SMB em cliente Windows; SharePoint Online; ou OneDrive for Business. Escolha *Sem destino* para executar só os filtros gerais. OneDrive pessoal e Azure NFS ainda não são contemplados. As checkboxes das regras específicas podem ser ativadas/desativadas individualmente.

O catálogo `M3-3B-1` oferece:
- Azure: pontuação proibida do nome e tamanho maior que 4 TiB;
- SharePoint/OneDrive empresarial: pontuação proibida, espaço inicial/final, sequência `_vti_` e tamanho maior que 250 GiB (base binária).

Os limites oficiais são imutáveis e não confundidos com o filtro operacional. O filtro pessoal aceita B, KiB, MiB, GiB e TiB, apenas quando marcado; identifica arquivos maiores que o valor informado, **mesmo quando válidos para o serviço**. Todos os resultados são hipóteses de pré-avaliação local: ausência de incompatibilidade documentada nas regras habilitadas **não equivale à garantia de migração**.

Pastas e arquivos são avaliados por nome; tamanho só se aplica a arquivos. Não há projeção de caminho remoto, verificação de quota, autenticação, Azure NFS, OneDrive pessoal, análise de conteúdo, hash, duplicatas, persistência ou ação destrutiva. Links/junctions conhecidos são ignorados. Até 1.000 entradas são exibidas, mas todas as entradas enumeradas são contabilizadas. Pastas sintéticas são obrigatórias para os testes do TCC.

## Compilação e teste

```powershell
dotnet restore CLEAR.slnx
dotnet build CLEAR.slnx -c Release --no-restore
dotnet test CLEAR.slnx -c Release --no-build --no-restore
dotnet run --project src/Clear.Desktop/Clear.Desktop.csproj
```

Veja `docs/architecture/Marco_M3_Parte3B.md` e `docs/tests/Roteiro_Validacao_M3_Parte3B.md`.

## Estado

M0–M2 estão na `main`; M3 Partes 1, 2 e 3A foram testadas pelo autor e integradas à `develop`. A Parte 3B está **em feature**, aguardando execução dos testes e validação manual no notebook. A `main` não foi alterada.
