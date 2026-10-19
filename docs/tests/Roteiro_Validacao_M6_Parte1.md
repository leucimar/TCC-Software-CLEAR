# Validação M6 — histórico SQLite, Parte 1

## 1. Atualizar a branch no Developer PowerShell

```powershell
cd C:\Users\Leucimar\Projetos\CLEAR
git status --short
git fetch origin
git switch --track origin/feature/historico-sqlite-m6
dotnet restore CLEAR.slnx
dotnet build CLEAR.slnx -c Release --no-restore
dotnet test CLEAR.slnx -c Release --no-build --no-restore
dotnet list CLEAR.slnx package --vulnerable --include-transitive
dotnet list CLEAR.slnx package --include-transitive
```

Pare antes do switch se `git status --short` indicar alterações. Se a branch já existir, usar `git switch feature/historico-sqlite-m6` e `git pull --ff-only`. Esperados **172 testes** (167 anteriores, já aprovados no Windows, + 5 novos de consulta). A revisão do SQLite não foi executada no ambiente do assistente.

## 2. Preparar acervo e saída de testes

```powershell
$base = Join-Path "C:\Temp" ("CLEAR_M6_Manual_" + [guid]::NewGuid().ToString("N"))
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
Write-Host "ACERVO: $raiz"
Write-Host "RELATORIOS: $saida"
```

## 3. Validar persistência

```powershell
dotnet run --project src/Clear.Desktop/Clear.Desktop.csproj
```

1. Selecione a pasta `Acervo` (não `$base`); destino **Sem destino**.
2. Marque **Detectar duplicatas exatas** e **Salvar histórico local SQLite (M6)**. Marque também **Exportar relatório completo (CSV + JSON)** para verificar compatibilidade M5+M6, em `Todas as entradas`.
3. Execute e salve o CSV/JSON em `Relatorios` quando solicitado.
4. A aba **Histórico SQLite — M6** deverá mostrar uma execução nova com **8 arquivos, 2 diretórios, 25 bytes, 10 entradas persistidas, 3 grupos de duplicatas e 0 ocorrências**.
5. Feche e reabra o CLEAR, clique **Atualizar histórico** e selecione a execução anterior. Clique **Consultar detalhes da execução** (ou dê duplo clique). Confira: 10 entradas históricas na primeira aba e suas evidências ao selecionar cada uma; 6 membros de 3 grupos de duplicatas na segunda; 0 ocorrências na terceira. O banco deve ser consultado sem editor externo.
6. Faça outra análise desmarcando **Detectar duplicatas exatas**, mas mantendo SQLite ativo. Histórico deverá apresentar uma segunda execução com 0 grupos, sem apagar a primeira.

O aplicativo utiliza `%LOCALAPPDATA%\CLEAR\historico.sqlite3`. O banco é propositalmente **separado** de `C:\Temp\CLEAR_M6_Manual_...\Acervo`. Na pasta de saída deve haver CSV(s) e resumo JSON se M5 estiver marcado.

## 4. Consultar o arquivo

```powershell
$banco = Join-Path $env:LOCALAPPDATA "CLEAR\historico.sqlite3"
Test-Path $banco
Get-Item $banco | Select-Object FullName, Length, LastWriteTime
```

Deverá existir um SQLite válido. **Não mova nem exclua o banco durante os testes; ele contém histórico.** Não precisa instalar programas adicionais para consultar: a aba do CLEAR apresenta as últimas execuções.

## 5. Verificação de segurança da dependência

A saída anterior do NuGet mostrou `NU1903` em `SQLitePCLRaw.lib.e_sqlite3 2.1.11`. A branch agora solicita `Microsoft.Data.Sqlite 10.0.13` e `SQLitePCLRaw.bundle_e_sqlite3 3.0.5`. **Não presumir que isso corrigiu o problema**: conferir o resultado de `dotnet restore`, `dotnet list ... --vulnerable --include-transitive`, build e 172 testes. Caso haja `NU1605`, `NU1903` ou erros de compatibilidade, não fazer merge; enviar o log.

## 6. Segurança

Não use dados reais de faculdade, trabalho ou clientes; histórico guarda nomes e caminhos potencialmente sensíveis. A implementação desta parte não reutiliza hashes nem classificações e não altera arquivos do acervo. Cancelamento durante o inventário antes do commit deve deixar somente as execuções concluídas anteriormente.

Envie capturas do build/testes, da aba de histórico depois de duas execuções e do resultado M4+M5. Não fazer merge sem aprovação.
