# M9 Parte 1 — Roteiro de validação no Visual Studio

## Atualização e testes

Execute no Developer PowerShell:

    cd C:\Users\Leucimar\Projetos\CLEAR
    git status --short
    git fetch origin
    git switch --track origin/feature/robustez-medicoes-m9
    dotnet restore CLEAR.slnx
    dotnet build CLEAR.slnx -c Release --no-restore
    dotnet test CLEAR.slnx -c Release --no-build --no-restore
    dotnet list CLEAR.slnx package --vulnerable --include-transitive

Pare se git status mostrar alterações antes do switch. Se a branch já existir localmente, git switch feature/robustez-medicoes-m9 e git pull --ff-only. Total esperado: 198 testes, a confirmar.

## Teste funcional

Execute:

    dotnet run --project src/Clear.Desktop/Clear.Desktop.csproj

Use somente acervo sintético em C:\Temp. Marque Detectar duplicatas exatas e selecione Leituras SHA-256 simultâneas M9 = 1. Analise; confira grupos M4 e aba Métricas M9. Repita com concorrência 4. Os mesmos grupos e hashes devem aparecer. Os tempos podem melhorar ou piorar conforme hardware e cache do SO. Desmarcar duplicatas precisa desabilitar o controle de concorrência. O padrão ao abrir o programa deve ser 1.

## Benchmark sintético reproduzível

Execute:

    dotnet run --project benchmarks/Clear.Benchmarks.M9/Clear.Benchmarks.M9.csproj -c Release -- --arquivos 500 --bytes 4096 --repeticoes 3

Deve ser criada C:\Temp\CLEAR_M9_Benchmark_<GUID>\Acervo com 500 arquivos sintéticos e um JSON na pasta pai, chamado medicoes_m9.json. Caso C:\Temp não exista, o caminho é %TEMP%. São esperadas 9 amostras (3 concorrências x 3 repetições), grupos confirmados consistentes, sem ocorrências. Registre hardware, SO, versão .NET, tempos e CPU antes de tirar conclusões. A memória observada é do processo e não necessariamente de cada rodada isolada.

Limites do console: 8..10000 arquivos, 0..65536 bytes por arquivo, 1..10 repetições. O benchmark NÃO move ou exclui arquivos. Não use arquivos reais do trabalho/faculdade.

## Evidências

Envie resultados restore/build/test, auditoria, capturas de M4/M9 para 1 e 4 e caminho/resultado do JSON gerado (revisando dados de ambiente antes de compartilhar). Não realizar merge M9 antes de autorização.
