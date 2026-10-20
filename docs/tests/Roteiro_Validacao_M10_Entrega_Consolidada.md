# Roteiro único — M10 / Quarentena, filtros e ETA

**Estado:** Não executado no Windows. Expectativa de 237 testes = 224 anteriores + 3 hierarquia + 5 ETA + 5 filtros.

## Developer PowerShell

    cd C:\Users\Leucimar\Projetos\CLEAR
    git status --short
    git fetch origin
    git switch --track origin/feature/fluxo-quarentena-filtros-eta
    dotnet restore CLEAR.slnx
    dotnet build CLEAR.slnx -c Release --no-restore
    dotnet test CLEAR.slnx -c Release --no-build --no-restore
    dotnet list CLEAR.slnx package --vulnerable --include-transitive
    dotnet run --project src/Clear.Desktop/Clear.Desktop.csproj

Se a branch já existe, trocar e git pull --ff-only. Se git status não está vazio, não trocar até revisar alterações.

## Validação conjunta (somente arquivos sintéticos)

1. Criar acervo sintético em C:\Temp\CLEAR_M10_Origem\Financeiro\2025\Relatorios com arquivos vazios, .bak, .txt e tamanhos distintos. Calcular SHA dos originais.
2. Na tela principal, conferir sugestão automática de quarentena no mesmo volume; escolher manualmente C:\Temp\CLEAR_M10_Quarentena. Escolher C:\Temp\CLEAR_M10_Relatorios.
3. Ativar exportação M5 e histórico M6. Analisar. Conferir relatórios no local escolhido e que origem está intacta.
4. Abrir Histórico / Consultar detalhes / Filtrar e selecionar. Testar combinações de categoria, extensão, datas, tamanhos, duplicatas, incompatibilidade e busca por caminho.
5. Marcar arquivos individuais; trocar de página/filtro; verificar que apenas os IDs marcados continuam selecionados. Testar Limpar seleção. Marcar página não seleciona outras páginas.
6. Responder NÃO à confirmação de movimentação: arquivo e SQLite de auditoria não devem mudar.
7. Responder SIM somente para 1 a 3 arquivos sintéticos: confirmar destino com GUID / hierarquia relativa e metadados/hashes, além dos registros SQLite.
8. Testar arquivo modificado depois da análise: deve recusar especificamente aquele item sem interferir nos outros. Testar o cancelamento entre itens e o ETA exibido por progresso.
9. Restaurar com confirmação separada; testar conflito no caminho original, preservação de hash e retrocompatibilidade com operações legadas do M7.
10. Na sub-base experimental, conferir ETA por bytes e arquivos. No inventário, conferir ETA durante classificação e, com SHA ativo, previsão somente da fase de hashing.

Arquivos públicos em D: não devem ser enviados à quarentena nesta validação. Se houver erro, enviar saída completa do build/testes e prints. Não aprovar merge antes da avaliação conjunta.