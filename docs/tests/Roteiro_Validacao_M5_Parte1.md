# Validação M5 — exportação de relatórios em CSV e JSON

## 1. Atualizar o código no notebook

No Developer PowerShell:

```powershell
cd C:\Users\Leucimar\Projetos\CLEAR
git status --short
git fetch origin
git switch --track origin/feature/relatorios-completos-m5

dotnet restore CLEAR.slnx
dotnet build CLEAR.slnx -c Release --no-restore
dotnet test CLEAR.slnx -c Release --no-build --no-restore
dotnet run --project src/Clear.Desktop/Clear.Desktop.csproj
```

Caso a feature já exista localmente, use `git switch feature/relatorios-completos-m5` e `git pull --ff-only`. Pare se `git status --short` não estiver vazio. O número esperado de testes é **156** (147 anteriores e 9 novos), sujeito ao resultado da descoberta real; não declarar aprovação sem a saída.

## 2. Preparar duas pastas sintéticas separadas

```powershell
$base = Join-Path $env:TEMP ("CLEAR_M5_Manual_" + [guid]::NewGuid().ToString("N"))
$raiz = Join-Path $base "Acervo"
$saida = Join-Path $base "Relatorios"
New-Item -ItemType Directory -Path (Join-Path $raiz "Copia") -Force | Out-Null
New-Item -ItemType Directory -Path (Join-Path $raiz "Vazia") -Force | Out-Null
New-Item -ItemType Directory -Path $saida -Force | Out-Null

[IO.File]::WriteAllBytes((Join-Path $raiz "relatorio.txt"), [byte[]](65,66,67,49,50,51))
[IO.File]::WriteAllBytes((Join-Path $raiz "Copia\relatorio-copia.dat"), [byte[]](65,66,67,49,50,51))
[IO.File]::WriteAllBytes((Join-Path $raiz "diferente-mesmo-tamanho.bin"), [byte[]](88,89,90,55,56,57))
[IO.File]::WriteAllBytes((Join-Path $raiz "vazio1.txt"), [byte[]]@())
[IO.File]::WriteAllBytes((Join-Path $raiz "Copia\vazio2.bak"), [byte[]]@())
[IO.File]::WriteAllBytes((Join-Path $raiz "curto.txt"), [byte[]](1,2))
[IO.File]::WriteAllBytes((Join-Path $raiz "Copia\curto.tmp"), [byte[]](1,2))
[IO.File]::WriteAllBytes((Join-Path $raiz "unico.txt"), [byte[]](9,9,9))
Write-Host "Acervo: $raiz"
Write-Host "Saída: $saida"
```

## 3. Teste A — relatório completo, com duplicatas

No CLEAR:
- Selecione como fonte a pasta `Acervo`, não a pasta `Relatorios`.
- Mantenha **Sem destino**. Arquivos vazios e backups/temporários ligados; temporal e limite pessoal desligados.
- Marque **Detectar duplicatas exatas**.
- Marque **Exportar relatório completo (CSV + JSON — M5)**. Em **Linhas CSV**, escolha **Todas as entradas**.
- Clique em **Analisar**. Na janela seguinte, escolha a pasta `Relatorios`, que está fora do `Acervo`.

Esperado: inventário com **8 arquivos, 2 pastas, 25 bytes**, 3 arquivos com indícios gerais e 0 ocorrências. Aba M4: 3 grupos, 6 membros, 7 candidatos/7 hashes, 8 bytes potencialmente redundantes.

Ao concluir, deverá aparecer uma janela com os caminhos:
- `CLEAR_M5_..._inventario.csv`: **11 linhas** (1 cabeçalho + 8 arquivos + 2 pastas);
- `CLEAR_M5_..._duplicatas.csv`: **7 linhas** (1 cabeçalho + 6 membros);
- `CLEAR_M5_..._resumo.json`: totais completos e `Duplicatas.Executada: true`.

Para conferir com PowerShell, caso os arquivos da pasta sejam somente os deste teste:

```powershell
$csv = Get-ChildItem $saida -Filter "*_inventario.csv" |
    Sort-Object LastWriteTime -Descending | Select-Object -First 1
$duplicatas = Get-ChildItem $saida -Filter "*_duplicatas.csv" |
    Sort-Object LastWriteTime -Descending | Select-Object -First 1
$resumo = Get-ChildItem $saida -Filter "*_resumo.json" |
    Sort-Object LastWriteTime -Descending | Select-Object -First 1

(Get-Content $csv.FullName).Count
(Get-Content $duplicatas.FullName).Count
Get-Content $resumo.FullName -Raw | ConvertFrom-Json |
    Select-Object Formato, LinhasExportadas, Inventario, Duplicatas
```

## 4. Teste B — só achados, sem SHA-256

Desmarque a deduplicação. Mantenha os dois filtros gerais de vazio/backup. Ative exportação com filtro **Somente achados (qualquer tipo)**. Selecione a mesma saída.

Esperado: **3 linhas de arquivo + 1 cabeçalho** no novo CSV; totais gerais permanecem 8 arquivos, 2 pastas, 25 bytes. JSON deve mostrar `Duplicatas.Executada: false`, e o novo conjunto não terá CSV de duplicatas.

A expressão *achados* nesse filtro abrange indícios M3, incompatibilidade M3 por destino e excesso de limite pessoal; os grupos M4 são exportados exclusivamente no CSV próprio quando habilitados.

## 5. Segurança e evidências

- Teste apontar `Relatorios` **dentro** do acervo (somente se desejar conferir a validação): o CLEAR deve recusar; nenhuma saída deve ser publicada ali.
- Cancele uma análise maior (opcional): relatórios `.part` não devem permanecer.
- Não use acervos reais; os arquivos produzidos contêm caminhos completos e podem conter informações sensíveis.
- Envie capturas do build, testes, janela final dos relatórios e, se possível, totais do PowerShell.

Não integrar à `develop` ou `main` antes do aceite explícito.
