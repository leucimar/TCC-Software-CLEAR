# Roteiro de validação — M4 duplicatas exatas

## 1. Atualizar a branch sem perder alterações locais

No Developer PowerShell do Visual Studio:

```powershell
cd C:\Users\Leucimar\Projetos\CLEAR
git status --short
git fetch origin
git switch --track origin/feature/duplicatas-exatas-m4
dotnet restore CLEAR.slnx
dotnet build CLEAR.slnx -c Release --no-restore
dotnet test CLEAR.slnx -c Release --no-build --no-restore
dotnet run --project src/Clear.Desktop/Clear.Desktop.csproj
```

Pare se o `git status --short` não estiver vazio. Se a branch já existir localmente, substitua `switch --track` por `git switch feature/duplicatas-exatas-m4` seguido de `git pull --ff-only`. A quantidade real de testes será determinada pelo MSTest local; os 119 anteriores devem passar.

## 2. Criar dados sintéticos (pasta TEMP inédita)

```powershell
$raiz = Join-Path $env:TEMP ("CLEAR_M4_Manual_" + [guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Path (Join-Path $raiz "Copia") -Force | Out-Null
New-Item -ItemType Directory -Path (Join-Path $raiz "Vazia") -Force | Out-Null

[IO.File]::WriteAllBytes((Join-Path $raiz "relatorio.txt"), [byte[]](65,66,67,49,50,51))
[IO.File]::WriteAllBytes((Join-Path $raiz "Copia\relatorio-copia.dat"), [byte[]](65,66,67,49,50,51))
[IO.File]::WriteAllBytes((Join-Path $raiz "diferente-mesmo-tamanho.bin"), [byte[]](88,89,90,55,56,57))
[IO.File]::WriteAllBytes((Join-Path $raiz "vazio1.txt"), [byte[]]@())
[IO.File]::WriteAllBytes((Join-Path $raiz "Copia\vazio2.bak"), [byte[]]@())
[IO.File]::WriteAllBytes((Join-Path $raiz "curto.txt"), [byte[]](1,2))
[IO.File]::WriteAllBytes((Join-Path $raiz "Copia\curto.tmp"), [byte[]](1,2))
[IO.File]::WriteAllBytes((Join-Path $raiz "unico.txt"), [byte[]](9,9,9))
Write-Host $raiz
```

## 3. Cenários

Selecione a pasta indicada. Mantenha *Sem destino*, desligue o sinal temporal e o filtro personalizado, e **marque** `Detectar duplicatas exatas (SHA-256; leitura do conteúdo)`.

Inventário: **8 arquivos, 2 pastas, 25 bytes**, contagem de indícios gerais preservada, 0 ocorrências em condições normais.

Na aba `Duplicatas exatas — M4`:
- **3 grupos exatos**: (relatorio.txt + relatorio-copia.dat) com 6 B; (curto.txt + curto.tmp) com 2 B; (vazio1.txt + vazio2.bak) com 0 B;
- **6 arquivos em grupos**, 7 candidatos ao hash (o arquivo único de 3 B não é lido para hash);
- **8 bytes potencialmente redundantes** (estimativa teórica, sem indicação de descarte);
- `diferente-mesmo-tamanho.bin` tem 6 B mas não pertence a grupo, pois o conteúdo difere;
- **0 ocorrências** se não houver bloqueios.

Desmarque a checkbox de duplicatas e execute novamente: a busca não deve ser realizada e a aba deve informar que está desativada. Todos os demais filtros M3 devem continuar disponíveis.

## 4. Integridade e evidências

Confira se os arquivos e as pastas continuam no local e se os conteúdos não foram alterados. O CLEAR pode consultar o conteúdo somente para deduplicação, nunca escrever nele. Envie capturas de `dotnet build`, `dotnet test`, do resumo da aba inventário e do resumo da aba duplicatas.

Em caso de falha, aviso, incongruência ou cancelamento, reporte antes de aceitar a branch. Não migrar nem mover arquivos. Sem merge na `develop` ou `main` até aprovação.
