# TCC-Software-CLEAR

Aplicativo desktop desenvolvido em C#/.NET para avaliação, categorização e saneamento assistido de arquivos, antes de processos de migração para ambientes em nuvem.

Projeto desenvolvido como parte do TCC de Ciência da Computação.

## Tecnologias

- C# e .NET 10 LTS
- Windows Forms
- MSTest para testes automatizados
- Git e GitHub para versionamento

## Arquitetura

O projeto utiliza Clean Architecture simplificada, com quatro camadas:

- **Clear.Domain:** entidades, regras e contratos essenciais.
- **Clear.Application:** casos de uso e coordenação das operações.
- **Clear.Infrastructure:** acesso ao sistema de arquivos e persistência.
- **Clear.Desktop:** interface gráfica Windows Forms.

## Compilação e testes

Compilar a solução:

`dotnet build CLEAR.slnx -c Release`

Executar os testes:

`dotnet test CLEAR.slnx -c Release`

## Estado atual

**Marco M0 — Bootstrap inicial.**

A estrutura da solução foi criada e validada. As funcionalidades de análise, classificação, quarentena e rollback ainda não foram implementadas.

## Restrições

O CLEAR não realiza migração de arquivos para a nuvem. Operações modificadoras dependerão de seleção e confirmação explícitas do usuário.