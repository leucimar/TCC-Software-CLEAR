# M8 — Roteiro de validação da restauração

## 1. Atualizar e testar

Execute no Developer PowerShell:

    cd C:\Users\Leucimar\Projetos\CLEAR
    git status --short
    git fetch origin
    git switch --track origin/feature/restauracao-quarentena-m8
    dotnet restore CLEAR.slnx
    dotnet build CLEAR.slnx -c Release --no-restore
    dotnet test CLEAR.slnx -c Release --no-build --no-restore
    dotnet list CLEAR.slnx package --vulnerable --include-transitive

Pare antes do switch se git status --short tiver alterações. Se a feature já existir localmente: git switch feature/restauracao-quarentena-m8 e git pull --ff-only.

Esperados 193 testes, sem falhas/avisos (172 M0–M6 + 10 M7 + 11 M8). Essa revisão não foi compilada pelo assistente.

## 2. Restaurar o arquivo sintético do teste M7

O usuário já moveu arquivo-teste.bak para %LOCALAPPDATA%\CLEAR\Quarentena\<GUID> com registro SQLite M7.

1. Execute dotnet run --project src/Clear.Desktop/Clear.Desktop.csproj.
2. Na aba Histórico SQLite M6 escolha a análise de teste M7, clique Consultar detalhes da execução.
3. Abra Auditoria da quarentena M7. Atualize, selecione o registro EmQuarentena, clique Revisar restauração (M8).
4. Primeiro clique NÃO. Confira que arquivo continua no diretório da quarentena e que o registro continua EmQuarentena.
5. Repita a revisão, clique SIM. O programa deve restaurar exatamente ao caminho original e atualizar o estado a Restaurado.
6. Confira que arquivo-teste.bak voltou à pasta C:\Temp\CLEAR_M7_Manual_...\Acervo e manteve os 6 bytes.
7. Feche e reabra o aplicativo e verifique que o estado Restaurado ainda consta na auditoria.

Se a pasta original foi excluída, o CLEAR deve recusar sem recriá-la. Recrie APENAS essa pasta sintética e tente novamente.

## 3. Conflito de destino (novo cenário)

No PowerShell:

    $base = Join-Path "C:\Temp" ("CLEAR_M8_Conflito_" + [guid]::NewGuid().ToString("N"))
    $origem = Join-Path $base "Acervo"
    New-Item -ItemType Directory -Path $origem -Force | Out-Null
    [IO.File]::WriteAllBytes((Join-Path $origem "teste-conflito.bak"), [byte[]](1,2,3,4))

- Analise Acervo e salve histórico SQLite M6; depois autorize o envio sintético M7 para a quarentena.
- Crie NOVO arquivo no caminho original:

    [IO.File]::WriteAllBytes((Join-Path $origem "teste-conflito.bak"), [byte[]](9,9))

- Tente restaurar: CLEAR deve recusar, preservar os 2 bytes novos no caminho original e os 4 bytes originais na quarentena.
- Remova SOMENTE o novo arquivo sintético de 2 bytes e restaure novamente com confirmação.

## 4. Evidências

- Envie build, resultados dos 193 testes e auditoria NuGet.
- Capture teste com resposta NÃO, restauração SIM, estado Restaurado e persistência após reiniciar.
- Estado RestauracaoPendente é situação incerta; preserve dados e não tente repetir automaticamente.
- Somente testar em C:\Temp; não apagar SQLite da auditoria nem mover arquivos reais sem novos testes.

Sem merge na develop/main até aprovação.
