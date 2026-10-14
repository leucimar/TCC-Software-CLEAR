# TCC-Software-CLEAR

Aplicativo desktop C#/.NET voltado à avaliação e ao saneamento **assistido** de arquivos antes de migrações para a nuvem. Desenvolvido para o TCC de Ciência da Computação.

## Tecnologias e estrutura

- .NET 10 LTS (SDK 10.0.402 fixado em `global.json`), C# e Windows Forms;
- MSTest para testes de domínio e aplicação;
- Arquitetura Clean Architecture simplificada com projetos `Clear.Domain`, `Clear.Application`, `Clear.Infrastructure` e `Clear.Desktop`.

## Compilar e testar

```powershell
dotnet build CLEAR.slnx -c Release
dotnet test CLEAR.slnx -c Release
```

## Estado

**M1 — Núcleo inicial publicado, aguardando validação local.**

Inclui representação de metadados de arquivos, evidências de análise multirrótulo e um caso de uso que resume uma fonte **simulada** de inventário por meio de contrato injetado. Não há varredura de disco nem classificação automática. A interface gráfica permanece inicial.

Mais detalhes em `docs/architecture/Marco_M1.md`.

## Restrições

O CLEAR não migra arquivos para a nuvem. Nenhuma categoria autoriza exclusão ou movimentação automática. Quarentena e rollback dependerão de aprovação humana, implementação e testes específicos.
