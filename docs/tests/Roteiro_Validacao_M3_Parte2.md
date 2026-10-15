# Roteiro de validação — M3 Parte 2 no notebook

## 1. Atualizar, compilar, testar

Confirme que git status --short não apresenta modificações; caso apresente, interrompa.

    git fetch origin
    git switch --track origin/feature/classificacao-temporal-m3
    dotnet restore CLEAR.slnx
    dotnet build CLEAR.slnx -c Release --no-restore
    dotnet test CLEAR.slnx -c Release --no-build --no-restore
    dotnet run --project src/Clear.Desktop/Clear.Desktop.csproj

Se a branch já existir, use git switch feature/classificacao-temporal-m3 seguido de git pull --ff-only no lugar de switch --track.

## 2. Criar pasta sintética isolada no PowerShell

Execute apenas em uma pasta TEMP nova, sem dados reais:

    $raiz = Join-Path $env:TEMP ("CLEAR_M3_Temporal_" + [guid]::NewGuid().ToString("N"))
    New-Item -ItemType Directory -Path $raiz | Out-Null
    [IO.File]::WriteAllBytes((Join-Path $raiz "antigo.txt"), [byte[]](1,2,3))
    [IO.File]::WriteAllBytes((Join-Path $raiz "antigo.tmp"), [byte[]](1,2))
    [IO.File]::WriteAllBytes((Join-Path $raiz "atual.docx"), [byte[]](1,2,3,4))
    [IO.File]::WriteAllBytes((Join-Path $raiz "vazio.txt"), [byte[]]@())
    [IO.File]::WriteAllBytes((Join-Path $raiz "backup.bak"), [byte[]](1,2,3))
    $antigo = [DateTime]::UtcNow.AddDays(-500)
    [IO.File]::SetLastWriteTimeUtc((Join-Path $raiz "antigo.txt"), $antigo)
    [IO.File]::SetLastWriteTimeUtc((Join-Path $raiz "antigo.tmp"), $antigo)
    Write-Host $raiz

## 3. Verificações da interface

Selecione a pasta principal exibida.

**Cenário A — sinal temporal DESLIGADO:** 5 arquivos, 12 bytes, 3 arquivos com indícios, ArquivoVazio: 1, CopiaBackupOuTemporario: 2, nenhum SinalTemporal, zero ocorrências em condições normais.

**Cenário B — sinal temporal LIGADO com 365 dias:** 5 arquivos, 12 bytes, 4 arquivos com indícios, ArquivoVazio: 1, CopiaBackupOuTemporario: 2, SinalTemporal: 2, zero ocorrências em condições normais. Antigo.txt e antigo.tmp possuem justificativas temporais; antigo.tmp tem duas categorias; atual.docx nenhuma.

As somas por categoria podem superar o total com indícios por haver sobreposição. Não há exclusão/movimentação. Alteração da data dos arquivos sintéticos foi feita pelo PowerShell preparatório, não pelo CLEAR.

## 4. Evidências

Enviar captura do build, testes e dois cenários da interface. Não aprovar automaticamente se houver advertências, diferenças ou falhas. Nada é integrado à develop/main nesta fase.
