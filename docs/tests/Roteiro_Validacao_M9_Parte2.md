# M9 Parte 2 — Roteiro de validação

## 1. Atualizar

No Developer PowerShell do Visual Studio:

    cd C:\Users\Leucimar\Projetos\CLEAR
    git status --short
    git fetch origin
    git switch --track origin/feature/checkpoint-reinicio-seguro-m9-parte2
    dotnet restore CLEAR.slnx
    dotnet build CLEAR.slnx -c Release --no-restore
    dotnet test CLEAR.slnx -c Release --no-build --no-restore
    dotnet list CLEAR.slnx package --vulnerable --include-transitive

Interrompa se git status mostrar alterações. Se branch já existir localmente, git switch feature/checkpoint-reinicio-seguro-m9-parte2 e git pull --ff-only. Esperados 210 testes; ainda não executados nesta revisão.

## 2. Checkpoint e reinício integral

- Inicie dotnet run --project src/Clear.Desktop/Clear.Desktop.csproj.
- Selecione acervo sintético de C:\Temp (de preferência o de 2.000 arquivos do benchmark M9). Ative Salvar progresso para reinício seguro; se necessário, execute um acervo maior para conseguir cancelar antes de finalizar.
- Clique Analisar e depois Cancelar durante a varredura. Checkpoint JSON deverá permanecer em %LOCALAPPDATA%\CLEAR\Checkpoints, marcando Cancelada. Em caso de encerramento abrupto, pode permanecer EmAndamento. Nenhuma classificação ou hash é reutilizada desse arquivo.
- Com MESMA raiz e opções, clique Reiniciar análise interrompida (do início). Clique NÃO na primeira confirmação: não deve haver novo inventário. Repita e clique SIM: nova análise começa desde zero; em sucesso o checkpoint é removido.
- Cancele uma nova análise; altere um dos filtros e tente reiniciar: deve recusar por assinatura divergente. Para outra configuração use Analisar normalmente.
- Confira que dados alterados no acervo entre as tentativas são reavaliados integralmente, não herdados do checkpoint.

## 3. Limite da indexação SHA

- No acervo de 500 arquivos, ative detectar duplicatas exatas.
- Ajuste Máximo de arquivos indexados em RAM para 100 (menos que 500), rode: deve falhar explicitamente no M4 antes do hash, sem resultado completo.
- Ajuste o limite para 500 ou mais: deve processar e produzir os mesmos 125 grupos previamente medidos.
- Desative deduplicação: controle de limite deve ficar desabilitado.

## 4. Evidências

Enviar print dos 210 testes, da janela de confirmação e da tela de limite. Checkpoint e histórico não devem ficar dentro do acervo; não usar arquivos reais de faculdade/trabalho. Nenhum merge sem aprovação.
