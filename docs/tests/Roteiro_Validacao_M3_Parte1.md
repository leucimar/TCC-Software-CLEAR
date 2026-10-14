# Validação manual do M3 — Parte 1

## 1. Sincronize o notebook

Certifique-se de que `git status --short` esteja vazio antes da troca de branch.

```powershell
git fetch origin
git switch --track origin/feature/classificacao-inicial-m3
dotnet restore CLEAR.slnx
dotnet build CLEAR.slnx -c Release --no-restore
dotnet test CLEAR.slnx -c Release --no-build --no-restore
dotnet run --project src/Clear.Desktop/Clear.Desktop.csproj
```

Se a branch já existir localmente, use `git switch feature/classificacao-inicial-m3` e `git pull --ff-only`, em vez de `--track`.

## 2. Cenário sintético isolado

Crie uma pasta **nova** sem utilizar documentos pessoais ou corporativos:

```powershell
$raiz = Join-Path $env:TEMP ("CLEAR_M3_Manual_" + [guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Path (Join-Path $raiz "Subpasta") -Force | Out-Null
[IO.File]::WriteAllBytes((Join-Path $raiz "vazio.txt"), [byte[]]@())
[IO.File]::WriteAllBytes((Join-Path $raiz "rascunho.tmp"), [byte[]](1,2))
[IO.File]::WriteAllBytes((Join-Path $raiz "backup.bak"), [byte[]](1,2,3))
[IO.File]::WriteAllBytes((Join-Path $raiz '~$planilha.xlsx'), [byte[]](1))
[IO.File]::WriteAllBytes((Join-Path $raiz "relatorio.docx"), [byte[]](1,2,3,4))
[IO.File]::WriteAllBytes((Join-Path $raiz "Subpasta\vazio.bak"), [byte[]]@())
Write-Host $raiz
```

Selecione a **pasta principal** exibida pelo PowerShell, e não a subpasta.

## 3. Resultados esperados

- Total: **6 arquivos**, **10 bytes**, **0 ocorrências** em condições normais.
- Arquivos com indícios: **5**.
- Categoria `ArquivoVazio`: **2** arquivos.
- Categoria `CopiaBackupOuTemporario`: **4** arquivos.
- `vazio.bak`: duas categorias com justificativas independentes.
- `relatorio.docx`: nenhuma categoria.
- A interface deve mostrar todos os seis arquivos e suas evidências; nenhum deles pode ser alterado.

**Importante:** as contagens das categorias somam 6, mas só 5 arquivos têm indícios, pois `vazio.bak` aparece em duas categorias. Isso é esperado, não é erro.

Envie as saídas de compilação e testes e uma captura da interface. Se surgir erro, não faça alterações locais não combinadas; reporte para correção na mesma branch.
