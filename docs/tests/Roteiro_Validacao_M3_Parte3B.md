# Validação manual M3 Parte 3B — notebook Windows 11

## 1. Atualizar e testar

No Developer PowerShell, dentro do repositório (o primeiro comando deve retornar vazio):

```powershell
cd C:\Users\Leucimar\Projetos\CLEAR
git status --short
git fetch origin
git switch --track origin/feature/compatibilidade-regras-m3-3b
dotnet restore CLEAR.slnx
dotnet build CLEAR.slnx -c Release --no-restore
dotnet test CLEAR.slnx -c Release --no-build --no-restore
dotnet run --project src/Clear.Desktop/Clear.Desktop.csproj
```

Se a branch já existir localmente, substituir `switch --track` por `git switch feature/compatibilidade-regras-m3-3b` e `git pull --ff-only`.

**Os testes ainda não foram executados pela assistência** (SDK .NET não disponível nesta sessão). A saída local é a fonte de validação. Os 86 testes anteriores devem continuar aprovados, além dos novos.

## 2. Pasta sintética

```powershell
$raiz = Join-Path $env:TEMP ("CLEAR_M3_3B_" + [guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Path (Join-Path $raiz "Projeto_vti_docs") -Force | Out-Null
New-Item -ItemType Directory -Path (Join-Path $raiz "PastaVazia") -Force | Out-Null
[IO.File]::WriteAllBytes((Join-Path $raiz "normal.txt"), [byte[]](1))
[IO.File]::WriteAllBytes((Join-Path $raiz "grande.txt"), [byte[]](1,2,3,4))
[IO.File]::WriteAllBytes((Join-Path $raiz "rascunho.tmp"), [byte[]](1,2))
[IO.File]::WriteAllBytes((Join-Path $raiz "Projeto_vti_docs\outro.docx"), [byte[]](1,2,3))
Write-Host $raiz
```

Esperado independente de destino: **4 arquivos, 2 subpastas, 10 bytes**, classificação geral `CopiaBackupOuTemporario: 1`, **zero ocorrências** em condições normais. Sinal temporal desativado. Todos os arquivos e pastas devem permanecer intocados.

## 3. Cenários da interface

- **Sem destino / filtro personalizado desativado:** 0 itens com incompatibilidade documentada; 0 acima do limite pessoal. Nenhuma mensagem de compatibilidade garantida.
- **Azure Files:** selecione o perfil com suas 2 verificações habilitadas. Com esses nomes/tamanhos pequenos, 0 incompatibilidades; o Azure não deve aplicar `SPO-NOME-004` à pasta `Projeto_vti_docs`.
- **SharePoint Online:** selecione o perfil com 5 verificações habilitadas. Esperado **1 item com incompatibilidade**, a pasta `Projeto_vti_docs`, pela regra `SPO-NOME-004`.
- **OneDrive for Business:** altere somente o subtipo empresarial; resultado esperado idêntico ao perfil SharePoint neste conjunto (1 pasta com `SPO-NOME-004`).
- **Checkbox individual:** ainda em SharePoint, desmarque apenas `SPO-NOME-004`. Reexecute: **0 incompatibilidades**. Reative: 1. Mantenha as demais ligadas.
- **Filtro personalizado sem destino:** escolha `Sem destino`, marque `Avisar se o arquivo for maior que`, valor **2**, unidade **B**. Esperado **2 arquivos acima do limite pessoal** (`grande.txt` com 4 bytes e `outro.docx` com 3 bytes). `rascunho.tmp` com exatamente 2 bytes não deve sinalizar.
- **Os dois filtros distintos:** selecione SharePoint e mantenha o filtro pessoal de 2 B. Esperado **1 incompatibilidade de destino** (pasta) e **2 arquivos acima do limite pessoal** (arquivos), categorias independentes.

Obs.: Os nomes inválidos para Windows e arquivos maiores que os limites oficiais são testados nos **testes automatizados com metadados simulados**, sem precisar criar arquivos gigantes ou ilegais no Windows.

## 4. Evidências

Enviar capturas da compilação, dos testes e da interface nos cenários SharePoint/OneDrive, Azure, checkbox desligada e filtro personalizado. Se houver avisos/erros, interromper o aceite e reportar. O CLEAR não migra, não renomeia e não exclui arquivos.
