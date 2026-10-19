# CLEAR — Roteiro de validação da sub-base por CÓPIA

Status: ainda não executado no Windows. Esperados 224 testes automatizados (210 anteriores + 14 novos).

## Comandos no Developer PowerShell

    cd C:\Users\Leucimar\Projetos\CLEAR
    git status --short
    git fetch origin
    git switch --track origin/feature/subbase-experimental-estratificada
    dotnet restore CLEAR.slnx
    dotnet build CLEAR.slnx -c Release --no-restore
    dotnet test CLEAR.slnx -c Release --no-build --no-restore
    dotnet list CLEAR.slnx package --vulnerable --include-transitive

Se houver alterações locais, interromper antes de trocar a branch. Se ela já existir, usar git switch feature/subbase-experimental-estratificada e git pull --ff-only.

## Teste pequeno antes do acervo real

1. Criar um pequeno dataset SINTÉTICO em C:\Temp\CLEAR_SubBase_Origem, com pastas e arquivos em vários formatos/tamanhos, inclusive vazios. Registrar quantidade e hashes SHA-256 de TODOS os originais.
2. Abrir CLEAR pelo comando dotnet run --project src/Clear.Desktop/Clear.Desktop.csproj.
3. No novo botão Criar sub-base experimental (SOMENTE CÓPIA), informar origem sintética e destino novo C:\Temp\CLEAR_SubBase_Saida\CLEAR_30pct. Calcular prévia para 30%, semente 5721.
4. Verificar contagens e taxas REALMENTE atingidas. Negar a confirmação de cópia primeiro (responder NÃO): nada deve ser criado.
5. Confirmar SIM, aguardar conclusão, conferir que todos os originais continuam no lugar e com mesmo conteúdo; todos os selecionados existem como cópias na estrutura Acervo e com SHA-256 idêntico.
6. Conferir manifesto_resumo.json e manifesto_arquivos.jsonl, seus hashes e estratos.
7. Repetir em outro destino com 100% para demonstrar cópia integral sem modificar a origem.

## Casos negativos

- Destino existente, dentro da origem ou ancestral da origem: rejeitar sem sobrescrita.
- Origem alterada entre prévia e cópia: recusar antes de iniciar.
- Cancelamento durante cópia: pasta final não aparece. Pode existir estágio incompleto identificado, não utilizável como dataset; a limpeza é manual.
- Origem com link/junction: não atravessar silenciosamente.
- Mostrar mensagens de erros claros. Confirmar interface responsiva.

## Posteriormente — somente após testes sintéticos aprovados

Origem D:\Base_Dados_TCC\02_original; destino novo sugerido D:\Base_Dados_TCC\03_amostras\CLEAR_30pct. Para 311.525 arquivos, a meta de 30% é aproximadamente 93.458 arquivos e 25.815.337.371 bytes (volume real aproximado e não garantido). Reservar espaço físico e conferir licenças dos datasets. Não executar arquivos potencialmente perigosos nem usar dados originais em quarentena. Registrar diferenças entre sub-base e acervo completo por formato, bytes, pacote, faixa de tamanho e profundidade.

Não aprovar merge sem validação explícita do autor; continuar registrando aviso MSTEST0037 existente do M9 como pendência.
