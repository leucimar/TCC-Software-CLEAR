# Validação controlada — CLEARv1.0

Instrumento de integração para Windows. Usa os projetos reais Domain/Application/Infrastructure; não automatiza a interface visual. Não usa dados sintéticos para os totais físicos: confere os 558 arquivos do acervo controlado e seu oráculo. Os casos de limites impossíveis de materializar, falhas e concorrência são explicitamente virtuais e separados.

Requer o acervo `D:\TCC_Leucimar\Base_Dados\Validacao_Controlada_CLEAR`, com `02_original` e os dois manifestos em `04_manifestos`. A raiz aceita é restrita a esse caminho para evitar uso acidental do acervo principal. Antes de reutilizar em outra máquina, revisar explicitamente essa restrição e disponibilizar o mesmo corpus/oráculo.

Na raiz do repositório:

```powershell
dotnet run --project benchmarks/Clear.ValidacaoControlada/Clear.ValidacaoControlada.csproj -c Release -- "D:\TCC_Leucimar\Base_Dados\Validacao_Controlada_CLEAR"
```

Cria uma nova cópia em `03_experimental` e uma nova saída em `08_relatorios/CLEARv1_0_<UTC>`. Quarentena/restauração afetam somente a cópia; não sobrescreve o acervo original. Conteúdos são lidos com solicitação de preservação de acesso, sem fallback. As datas controladas são aplicadas somente à cópia, sem restauração forçada de datas dos originais.

Grava verificações, conclusão, desempenho, relatórios CSV/JSON, SQLite de teste, auditoria e métricas. Falhas geram saída diferente de zero. A comparação de 1/8 leitores é observacional e não controla cache/carga. Não executa envio real para Azure/SharePoint. Não cria contas, domínio AD nem compartilhamentos SMB.

A execução de fechamento passou 63 verificações. Os dados, logs, binários e bancos não integram o Git; o instrumento versionado permite repetir a execução quando o corpus estiver disponível.
