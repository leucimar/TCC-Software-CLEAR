# Roteiro de validação — seleção global, listas operacionais e último acesso

Data de implementação: 20/10/2026.

## Escopo
- Branch: `feature/classificacao-ultimo-acesso-usabilidade-lotes` baseada em `develop`.
- A regra `SinalTemporal` considera **último acesso registrado** (`LastAccessTimeUtc`), não data de modificação. O inventário já capturava ambos os metadados **antes** de ler conteúdos para SHA-256.
- Arquivo sem `UltimoAcessoUtc` ou com sentinela (1601 ou anterior) não recebe sinal temporal por ausência de prova. O Windows pode atrasar, desabilitar ou não atualizar a data de acesso. Arquivos com acesso antigo não são automaticamente inúteis.
- `LastWriteTimeUtc` permanece armazenado, exibido e utilizado na proteção de integridade antes da movimentação.
- O SQLite M6 já tem `acessado_utc`: **não foi feita migração ou reescrita de dados históricos**. Execuções antigas mantêm as classificações antigas; execute uma nova análise para usar a regra atual.
- CSV M5 passa a expor data do último acesso e situação geral da pré-análise; resumo JSON identifica o critério temporal. "Sem alertas detectados" **não** equivale a aprovação para migração.
- A consulta de quarentena tem seleção explícita de **TODOS filtrados (até 10.000)**, independente das páginas. O comando seleciona IDs mas não movimenta arquivos; mover exige confirmação específica e revalidação individual.
- A consulta usa a auditoria SQLite somente leitura para não reapresentar arquivos cujo caminho esteja em estado Pendente, EmQuarentena ou RestauracaoPendente. Itens continuam no **histórico**, mas desaparecem da **lista operacional**.
- Na restauração, após um lote, a consulta volta à última página válida, evitando a exibição de linhas/seleções obsoletas.

## Validação no Visual Studio (Windows, pasta sintética descartável)
```powershell
cd C:\Users\Leucimar\Projetos\CLEAR
git status --short
git fetch origin
git switch feature/classificacao-ultimo-acesso-usabilidade-lotes
git pull --ff-only origin feature/classificacao-ultimo-acesso-usabilidade-lotes
dotnet restore CLEAR.slnx
dotnet build CLEAR.slnx -c Release
dotnet test CLEAR.slnx -c Release
dotnet run --project src/Clear.Desktop/Clear.Desktop.csproj -c Release --no-build
```

**Recomendações**
1. Com CLEAR fechado, use apenas acervo **sintético** e diretórios separados de relatórios/quarentena. Não reutilize bases reais ou a quarentena anterior para o primeiro teste.
2. Crie mais de 200 arquivos descartáveis em pelo menos duas pastas; misture extensões, arquivos vazios, backups, documentos normais e duplicatas controladas.
3. Faça uma análise nova com M6, M5 e a regra temporal ativados. Consulte a data de acesso exibida e o `acessado_utc` do histórico/CSV; confira se as justificativas citam **último acesso registrado**. Nunca inferir uso real com certeza.
4. Na seleção para quarentena, aplique filtro; use "Selecionar TODOS filtrados". Confira que o total marcado **excede 200**, que navegar não elimina IDs e que o botão não move nada antes da confirmação.
5. Confirme um lote sintético com >200 arquivos e verifique arquivos na hierarquia correta, estado individual na auditoria, ausência de movimentação de não selecionados e ausência de registros já ocupados na lista operacional (reabrir a tela também).
6. Restaure >200, confirme os arquivos originais, estados da auditoria, limpeza exclusiva de diretórios vazios e lista operacional atualizada, inclusive ao esvaziar a página 2.
7. Teste filtro "Sem alertas detectados" e "Com indícios ou alertas"; confirme que duplicatas, limite pessoal e incompatibilidade entram em "Com", mesmo se categorias gerais forem vazias.
8. Teste operações canceladas, falhas de permissão, alterações de arquivo entre prévia e execução, e tentativas de nova movimentação/restauração. Verifique que não há sobrescritas.
9. Verifique o comportamento de último acesso em NTFS considerando as limitações do Windows, sem alterar configurações do sistema para contornar testes. Documente hardware, sistema de arquivos, opções aplicadas e resultados.
10. Antes de qualquer teste em quarentena real: feche o CLEAR, faça backup da pasta de quarentena e de `%LOCALAPPDATA%\CLEAR\quarentena.sqlite3`. Somente avançar com autorização específica.

## Critério de aceite
Compilação Release sem erros; testes automatizados aprovados; seleção e movimentação >200 elegíveis; nenhuma movimentação sem confirmação; auditoria preservada; filtros coerentes; lista de restauração sem itens já restaurados e sem linhas obsoletas; confirmação de dados em disco/SQLite. **Não declarar validado antes dos testes no Windows.**

## Limitações conhecidas da apresentação
A paginação da seleção para quarentena ainda é a do **histórico filtrado** (200 registros por página). Itens indisponíveis são ocultados da grade, mas a indicação numérica global apresenta o total histórico, podendo haver páginas com menos de 200 itens disponíveis. Essa limitação de apresentação não reduz o alcance da seleção global, que consulta todos os registros filtrados e remove caminhos bloqueados antes de marcar.
