# M7 — Validação da quarentena sintética (Parte 1)

## 1. Atualizar branch

No Developer PowerShell do Visual Studio:

    cd C:\Users\Leucimar\Projetos\CLEAR
    git status --short
    git fetch origin
    git switch --track origin/feature/quarentena-auditavel-m7
    dotnet restore CLEAR.slnx
    dotnet build CLEAR.slnx -c Release --no-restore
    dotnet test CLEAR.slnx -c Release --no-build --no-restore
    dotnet list CLEAR.slnx package --vulnerable --include-transitive

Se git status --short não estiver vazio, pare antes de trocar de branch. Para branch local já existente, use git switch feature/quarentena-auditavel-m7 e git pull --ff-only. Esperados 182 testes (172 anteriores + 10 novos), sujeitos à execução real.

## 2. Criar cenário inédito, somente sintético

No PowerShell:

    $base = Join-Path "C:\Temp" ("CLEAR_M7_Manual_" + [guid]::NewGuid().ToString("N"))
    $raiz = Join-Path $base "Acervo"
    New-Item -ItemType Directory -Path $raiz -Force | Out-Null
    [IO.File]::WriteAllBytes((Join-Path $raiz "arquivo-teste.bak"), [byte[]](65,66,67,49,50,51))
    Write-Host "ACERVO: $raiz"

## 3. Fluxo M6 → M7

Execute:

    dotnet run --project src/Clear.Desktop/Clear.Desktop.csproj

1. Selecione a pasta Acervo; mantenha classificação de backups/temporários e habilite Salvar histórico SQLite (M6). Analise.
2. Na aba Histórico SQLite, Atualizar histórico; selecione a execução nova e abra Consultar detalhes da execução.
3. Selecione arquivo-teste.bak; clique Revisar envio à quarentena (M7).
4. Leia origem, destino, tamanho, SHA-256 e ID histórico. Primeiro clique NÃO: arquivo deve permanecer intacto, sem auditoria.
5. Repita e clique SIM. O arquivo deve sair de Acervo e aparecer em %LOCALAPPDATA%\CLEAR\Quarentena\<GUID>\arquivo-teste.bak, com os mesmos 6 bytes.
6. Clique na aba Auditoria da quarentena — M7 e em Atualizar auditoria; confira registro EmQuarentena e os caminhos.

Conferência simples:

    Test-Path (Join-Path $raiz "arquivo-teste.bak")
    Get-ChildItem (Join-Path $env:LOCALAPPDATA "CLEAR\Quarentena") -Recurse -Filter "arquivo-teste.bak" | Select-Object FullName, Length

O primeiro deve ser False; o segundo deve mostrar o arquivo movido e 6 bytes.

## 4. Segurança

- Não utilize dados reais até a implementação e validação do rollback M8.
- Não delete manualmente o arquivo movido ou o banco de auditoria após o teste; eles serão úteis no M8.
- Uma operação Pendente significa situação incerta. Não tente mover novamente; traga captura e ID para conciliação.
- O M7 não executa migração, exclusão ou recuperação e não faz nada sem confirmação por arquivo.

Não aprovar merge se houver aviso, falha ou movimento inesperado.
