# CLEAR — TCC

Aplicativo desktop em C#/.NET 10 para avaliação e saneamento **assistido** de arquivos antes de migrações para a nuvem. O software **não** migra arquivos e **não** executa ações destrutivas automaticamente.

## Tecnologia e arquitetura

- .NET 10 LTS, SDK 10.0.402 fixado no global.json;
- Windows Forms para interface gráfica;
- Clean Architecture simplificada: Clear.Domain, Clear.Application, Clear.Infrastructure, Clear.Desktop;
- MSTest para testes unitários e de integração com arquivos sintéticos;
- Git/GitHub para versionamento.

## Marco atual: M2 — inventário de metadados

A interface permite escolher **manualmente** uma pasta, varrer seus arquivos e subpastas, visualizar até 1.000 entradas e acompanhar contagem total de arquivos/bytes. Problemas de leitura e links/junctions ignorados são relatados; a listagem de ocorrências é limitada a 100 detalhes para controlar uso de memória.

**Somente leitura:** o CLEAR consulta metadados, sem abrir conteúdo de documentos, editar, mover, copiar ou excluir. Não segue links/junctions/reparse points identificados. Não há SQLite, classificação automática, deduplicação nem quarentena neste marco.

A interface deve ser testada **somente com diretórios sintéticos**, não com dados de produção.

## Compilação e testes

Execute na raiz da solução:

~~~powershell
dotnet restore CLEAR.slnx
dotnet build CLEAR.slnx -c Release --no-restore
dotnet test CLEAR.slnx -c Release --no-build --no-restore
dotnet run --project src/Clear.Desktop/Clear.Desktop.csproj
~~~

Consulte docs/architecture/Marco_M2.md e docs/tests/Roteiro_Validacao_M2.md antes de testar.

## Estado da validação

Implementação publicada em branch de trabalho, **pendente de compilação e testes locais do autor**. Não confundir testes definidos no código com testes executados e aprovados.
