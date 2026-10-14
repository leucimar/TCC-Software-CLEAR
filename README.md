# CLEAR — TCC

Aplicativo desktop em C#/.NET 10 para avaliação e saneamento **assistido** de arquivos antes de migrações para a nuvem. O CLEAR **não** migra arquivos nem executa descarte automático.

## Tecnologias e arquitetura

- C# e .NET 10 LTS (SDK 10.0.402 fixado);
- Windows Forms e Clean Architecture simplificada com camadas Domain, Application, Infrastructure e Desktop;
- MSTest para testes unitários e de integração;
- Git e GitHub.

## Funcionalidades

- **M2:** inventário de metadados somente leitura, contagem de arquivos/bytes, interface gráfica e ocorrências de leitura;
- **M3 Parte 1:** indícios por tamanho zero e nomes de possíveis temporários/backups (prefixo ~$ ou terminações .tmp, .bak, .backup);
- **M3 Parte 2 (branch de testes):** sinal temporal opcional pelo instante UTC da última modificação, com limiar em dias escolhido pelo operador.

A classificação é multi-categoria. Os indícios não comprovam que um arquivo seja descartável. Todas as entradas efetivamente enumeradas são classificadas, mas só até 1.000 resultados são exibidos.

## Regra temporal

Desligada por padrão. A interface oferece entre 30 e 3650 dias (valor inicial sugerido: 365, sem representar política institucional). A data de referência é capturada uma única vez por execução, em UTC; a última modificação precisa ser estritamente anterior ao limite. O último acesso não é utilizado como prova de uso ou desuso.

## Limitações

Análise somente leitura; sem movimentar, renomear, editar, excluir nem abrir conteúdos. Links e junctions identificados são ignorados. Não há duplicatas exatas, SQLite, quarentena, rollback, avaliação específica de compatibilidade de destino ou migração. Windows 10 e Windows Server ainda aguardam validação. Teste apenas com pastas sintéticas.

## Compilar, testar e executar

    dotnet restore CLEAR.slnx
    dotnet build CLEAR.slnx -c Release --no-restore
    dotnet test CLEAR.slnx -c Release --no-build --no-restore
    dotnet run --project src/Clear.Desktop/Clear.Desktop.csproj

## Status

M0–M2 integrados à main. M3 Parte 1 validada e integrada à develop (45 testes). M3 Parte 2 desenvolvida para validação no notebook, ainda **sem aceite nem merge**. Consulte docs/architecture/Marco_M3_Parte2.md e docs/tests/Roteiro_Validacao_M3_Parte2.md.
