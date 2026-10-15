# Validação — M3 Parte 3A (notebook Windows 11)

## 1. Sincronização e testes

No diretório do repositório, primeiro `git status --short` deve estar vazio.

```powershell
git fetch origin
git switch --track origin/feature/compatibilidade-base-m3-3a
dotnet --version
dotnet restore CLEAR.slnx
dotnet build CLEAR.slnx -c Release --no-restore
dotnet test CLEAR.slnx -c Release --no-build --no-restore
dotnet run --project src/Clear.Desktop/Clear.Desktop.csproj
```

Se a branch já existir, usar `git switch feature/compatibilidade-base-m3-3a` e `git pull --ff-only` em vez de `--track`.

Os 60 testes anteriores devem continuar passando. Os testes novos foram **somente escritos**, não executados pelo assistente. Registrar quantidade descoberta no notebook e eventuais avisos.

## 2. Criar pasta isolada no PowerShell

```powershell
$raiz = Join-Path $env:TEMP ("CLEAR_M3_3A_" + [guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Path (Join-Path $raiz "Equipe\2026") -Force | Out-Null
New-Item -ItemType Directory -Path (Join-Path $raiz "Vazia") | Out-Null
[IO.File]::WriteAllBytes((Join-Path $raiz "relatorio.txt"), [byte[]](1,2,3))
[IO.File]::WriteAllBytes((Join-Path $raiz "Equipe\2026\backup.bak"), [byte[]](1,2))
[IO.File]::WriteAllBytes((Join-Path $raiz "Vazia\zero.txt"), [byte[]]@())
Write-Host $raiz
```

Na interface, selecione a pasta principal exibida. Sem ativar regra temporal, espere:

- **3 arquivos**, **5 bytes**, **3 subpastas** (Equipe, 2026, Vazia), **2 arquivos com indícios** (backup.bak e zero.txt);
- contagens gerais: `CopiaBackupOuTemporario = 1`, `ArquivoVazio = 1`;
- **0 ocorrências**, em condições normais;
- tabela de arquivos semelhante à anterior; as pastas são contadas, ainda não classificadas por compatibilidade.

Não existe, na Parte 3A, seletor de destino nem filtro de tamanho personalizado na interface. Sua integração está programada para a Parte 3B.

## 3. Integridade e aceite

Confira no Explorador que nenhum arquivo foi alterado, removido ou renomeado. Não utilize documentos pessoais ou dados reais de trabalho. Envie saída do `dotnet build`, do `dotnet test` e captura da interface com os totais; reporte qualquer divergência.

**Nenhum merge à develop/main sem aprovação explícita.**
