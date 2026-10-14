# Marco M1 — Modelos de domínio e contrato de inventário

**Situação:** implementação publicada na branch `feature/bootstrap-solution`; correção de avisos pendente de validação local. Sem merge.

## Componentes

- `ArquivoInventariado`: metadados sem leitura física, datas opcionais em UTC e validação de tamanho.
- `CategoriaAnalise`, `EvidenciaAnalise` e `ResultadoAnaliseArquivo`: representação explicável de múltiplas categorias; nenhuma categoria autoriza descarte.
- `IFonteInventario`: contrato assíncrono somente leitura; implementação para filesystem fica para M2.
- `ResumirInventario`: totaliza quantidade e bytes de fonte injetada, com cancelamento e proteção contra overflow.
- MSTest: sete testes de domínio e seis de aplicação, com objetos sintéticos em memória.

## Decisões

A injeção de dependência é feita pelo construtor; não foi adicionado contêiner nem novo pacote NuGet. Não há leitura real de diretórios, classificação automática, SQLite, quarentena nem rollback. O teste vazio do bootstrap foi substituído por testes reais.

## Evidência de validação — notebook Windows 11

Em 14/10/2026, o autor enviou capturas de tela comprovando:
- `git pull --ff-only` na branch `feature/bootstrap-solution` até o commit `d811d59`;
- `dotnet restore CLEAR.slnx` bem-sucedido;
- `dotnet build CLEAR.slnx -c Release --no-restore`: cinco projetos compilados, quatro avisos MSTest;
- `dotnet test CLEAR.slnx -c Release --no-build --no-restore`: **13 testes aprovados, 0 falhas**.

Os avisos observados foram MSTEST0037 (três ocorrências no projeto Domain.Tests) e MSTEST0001 (uma no Application.Tests). Foi preparada uma alteração posterior que substitui comparações de quantidades por `Assert.HasCount` e explicita paralelização no segundo projeto de testes. **A ausência de avisos após a correção ainda não foi comprovada.**

## Nova validação solicitada

No notebook, após conferir `git status --short`, executar:

```powershell
git fetch origin
git switch feature/bootstrap-solution
git pull --ff-only
dotnet build CLEAR.slnx -c Release
dotnet test CLEAR.slnx -c Release --no-build --no-restore
```

Esperado: 13 testes aprovados e ausência dos quatro avisos anteriores. Não declarar sucesso antecipadamente.

No M2 poderá ser adicionada uma fonte de leitura real do filesystem somente após aprovação dos critérios relativos a permissões, links simbólicos/junctions, arquivos mutáveis e falhas parciais.
