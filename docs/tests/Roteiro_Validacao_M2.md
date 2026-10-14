# Roteiro de validação local — M2 (notebook)

## 1. Atualizar a feature e testar

Confirme primeiro git status --short sem alterações não commitadas. Depois:

~~~powershell
git fetch origin
git switch --track origin/feature/inventario-arquivos
dotnet --version
dotnet restore CLEAR.slnx
dotnet build CLEAR.slnx -c Release --no-restore
dotnet test CLEAR.slnx -c Release --no-build --no-restore
dotnet run --project src/Clear.Desktop/Clear.Desktop.csproj
~~~

Se a branch local já existir, use git switch feature/inventario-arquivos e git pull --ff-only, em vez de --track.

## 2. Dados sintéticos de teste

Execute no PowerShell para criar uma pasta isolada. Não use documentos reais:

~~~powershell
$raiz = Join-Path $env:TEMP ("CLEAR_M2_Manual_" + [guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Path $raiz | Out-Null
New-Item -ItemType Directory -Path (Join-Path $raiz "Subpasta") | Out-Null
[System.IO.File]::WriteAllBytes((Join-Path $raiz "a.txt"), [byte[]](1,2,3))
[System.IO.File]::WriteAllBytes((Join-Path $raiz "Subpasta\b.txt"), [byte[]](1,2,3,4,5))
[System.IO.File]::WriteAllBytes((Join-Path $raiz "vazio.txt"), [byte[]]@())
Write-Host $raiz
~~~

No CLEAR, selecione o caminho exibido. Esperado: **3 arquivos e 8 bytes**, zero ocorrências em condições normais. Verifique o mesmo resultado no Explorador do Windows.

## 3. Outros casos

- Selecione uma pasta temporária vazia: esperado 0 arquivos e 0 bytes.
- Use Cancelar durante uma varredura de pasta de teste maior: o CLEAR deve anunciar cancelamento e descartar o resultado incompleto.
- Nunca use dados corporativos/reais neste teste.
- Confira que os arquivos e seus conteúdos continuam intactos após o inventário.

## 4. Evidências de aceite

Envie as saídas de dotnet build, dotnet test, a tela de inventário com os arquivos sintéticos e eventuais avisos/erros. Não integre a develop antes de seu aceite explícito.
