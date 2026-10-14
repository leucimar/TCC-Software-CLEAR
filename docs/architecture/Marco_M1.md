# Marco M1 — Modelos de domínio e contrato de inventário

**Situação:** implementação publicada, aguardando compilação e teste local pelo autor.

## Componentes

- `ArquivoInventariado`: metadados sem leitura física, datas opcionais em UTC e validação de tamanho.
- `CategoriaAnalise`, `EvidenciaAnalise` e `ResultadoAnaliseArquivo`: representação explicável de múltiplas categorias; nenhuma categoria autoriza descarte.
- `IFonteInventario`: contrato assíncrono somente leitura; implementação para filesystem fica para M2.
- `ResumirInventario`: totaliza quantidade e bytes de fonte injetada, com cancelamento e proteção contra overflow.
- Testes MSTest: **13 testes propostos** (7 de domínio e 6 de aplicação), usando somente objetos sintéticos em memória.

## Decisões

A injeção de dependência é feita pelo construtor; não foi adicionado contêiner ou NuGet novo. Os projetos mantêm as dependências existentes. Não há leitura real de diretórios, classificação automática, SQLite, quarentena nem rollback. O teste vazio do template foi substituído.

## Validação no notebook

```powershell
git status --short
git fetch origin
git switch feature/bootstrap-solution
git pull --ff-only
dotnet --version
dotnet restore CLEAR.slnx
dotnet build CLEAR.slnx -c Release --no-restore
dotnet test CLEAR.slnx -c Release --no-build --no-restore
```

Esperados: .NET SDK 10.0.402, build bem-sucedido e 13 testes aprovados. **Não são resultados observados**: dependem da execução local.

No próximo marco (M2) poderá ser adicionada a leitura somente leitura do filesystem, com política explícita para symlinks, permissões, arquivos mutáveis e falhas parciais. Os critérios de segurança deverão ser aprovados antes dessa implementação.
