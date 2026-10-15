# CLEAR — TCC

Aplicativo desktop gratuito em C#/.NET 10 para **avaliação e saneamento assistido** de arquivos antes de futuras migrações para a nuvem. O CLEAR **não realiza migração**, nem descarta ou move arquivos.

## Arquitetura e ferramentas

- C#/.NET 10 LTS (SDK 10.0.402 fixado em `global.json`);
- Windows Forms; projetos Domain, Application, Infrastructure e Desktop;
- MSTest e Git/GitHub, sem dependências pagas.

## Funcionalidades validadas anteriormente

- **M2:** inventário de arquivos e bytes, leitura apenas de metadados, cancelamento e registro de ocorrências;
- **M3 Parte 1:** indícios de arquivo vazio e nomes temporários/backup;
- **M3 Parte 2:** sinal temporal opt-in, com intervalo configurável e comparação de última modificação UTC.

Essas categorias gerais são indícios, não conclusões de descarte. O programa não altera arquivos.

## M3 — Parte 3A (branch de desenvolvimento)

**Base de compatibilidade, ainda sem validação local:**

- Enumeração de **subpastas**, inclusive vazias, durante a mesma travessia que inventaria os arquivos. A pasta raiz selecionada não é contada. A interface mostra a quantidade de subpastas, mantendo a tabela de arquivos como antes;
- Representação imutável de entrada de arquivo/diretório, sem atribuir tamanho lógico a diretórios;
- Modelo de destino exclusivo: Azure Files clássico SMB **ou** SharePoint Online/OneDrive for Business; nenhum perfil também é permitido para apenas análise geral;
- Catálogo **descritivo** das sete verificações previstas e seleção individual validada por destino;
- Contratos para resultados de compatibilidade que distinguem incompatibilidade, alerta, não avaliado, sem incompatibilidade detectada, desabilitado e não aplicável;
- Modelo independente de limite personalizado de tamanho: valor positivo em **bytes**, sinalização somente quando tamanho é **maior** que o limite.

**Não há, nesta Parte 3A, execução das sete regras, checkboxes de destino ou campo de tamanho na interface.** Essas integrações pertencem à Parte 3B. Nenhum perfil é selecionado automaticamente e nenhum resultado atual deve ser interpretado como certificação de compatibilidade.

## Segurança e limitações

- Apenas leitura de metadados, sem abrir conteúdo, renomear, editar, mover ou excluir;
- Links simbólicos, junctions e reparse points identificados são ignorados;
- Amostra de arquivos e de diretórios tem limite configurado para memória; contagens abrangem os itens enumerados;
- Erros de enumeração podem resultar em inventário parcial;
- Teste somente em pastas sintéticas. Windows 10/Server e datasets reais não estão validados nesta etapa.

## Compilar, testar e executar

Na raiz do repositório:

```powershell
dotnet restore CLEAR.slnx
dotnet build CLEAR.slnx -c Release --no-restore
dotnet test CLEAR.slnx -c Release --no-build --no-restore
dotnet run --project src/Clear.Desktop/Clear.Desktop.csproj
```

Consulte `docs/architecture/Marco_M3_Parte3A.md` e `docs/tests/Roteiro_Validacao_M3_Parte3A.md`.

## Status

- M0–M2: integrados à `main`;
- M3 Parte 1 e Parte 2: aprovadas no notebook e integradas à `develop` (60 testes ao final da Parte 2);
- **M3 Parte 3A: publicada somente na feature após commit; build, testes e aceite local pendentes.**
