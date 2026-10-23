# Avaliação geral do CLEAR — 22/10/2026

## 1. Resultado da avaliação

O núcleo do CLEAR apresentou resultados corretos no laboratório para inventário, categorias gerais, identificação das duplicatas, verificações SharePoint, consultas filtradas, movimentação e restauração integral. **Os 467 arquivos foram restaurados nos caminhos originais, com todos os hashes SHA-256 corretos e as 43 pastas preservadas.**

Foram encontrados problemas de consistência na apresentação dos resultados e limitações importantes de interface. A classificação consolidada do CSV não considera as duplicatas; a consulta histórica simples apresenta o indicador de duplicata sempre falso; a auditoria individual expõe somente 100 registros; e o tempo total incorpora a espera de uma confirmação de interface.

**Conclusão:** o software tem um núcleo aproveitável e funcionalidades operacionais demonstradas. A prioridade deve ser corrigir a coerência dos resultados apresentados e a usabilidade, antes de usar essas telas e medições como evidência final do TCC. A avaliação não exigiu alteração no código do CLEAR.

## 2. Ambiente, versão e método

| Item | Registro |
|---|---|
| Data | 22/10/2026; horários técnicos dos arquivos em UTC |
| Repositório local | `C:\Users\Leucimar\Projetos\CLEAR` |
| Branch | `develop` |
| Commit | `581d965f26494f35b2c30710377c26a85e9c9092` |
| Executável utilizado | `src\Clear.Desktop\bin\Release\net10.0-windows\Clear.Desktop.exe` |
| Plataforma | C#/.NET 10, WinForms e SQLite |
| Sistema | Windows 11 Home Single Language, build 26200 |
| Computador | Acer Aspire A514-54; Intel Core i5-1135G7; 4 núcleos/8 processadores lógicos; aproximadamente 8 GB de RAM |
| Laboratório | `C:\Temp\CLEAR_Laboratorio_Sintetico_TCC` |
| Origem efetivamente analisada | `C:\Temp\CLEAR_Laboratorio_Sintetico_TCC\Acervo` |
| Gabarito | `Gabarito\rotulos_esperados.csv` e `resumo_esperado.json` |
| Referência automatizada anterior | 251/251 testes aprovados; build Release com 0 erros e 4 avisos MSTEST0037. A suíte do CLEAR não foi reexecutada nesta avaliação. |

A avaliação combinou operação real da interface, comparação arquivo a arquivo com o gabarito, conferência dos arquivos em disco, consulta SQLite em modo somente leitura e inspeção dos trechos de código responsáveis pelos comportamentos observados. Foram executadas **26 verificações complementares aprovadas**, utilizando as bibliotecas já compiladas, sobre os dados reais do laboratório. Essas verificações complementam a suíte anterior; não substituem os testes da interface.

O acervo contém **467 arquivos, 43 subpastas e 25.723.860 bytes**. Há arquivos regulares, vazios, temporários/backups, combinações de indícios, datas de acesso e modificação deliberadamente diferentes, arquivos acima/abaixo de 1 MiB, nomes especiais para SharePoint e grupos de PNG, ZIP, WAV e outros arquivos duplicados.

Antes da movimentação foi criada uma cópia de segurança em `avaliacao_CLEAR_2026-10-22\backup_acervo`, fora do repositório do software. Os testes operaram apenas sobre o laboratório indicado.

### Critérios de resultado

- **Aprovado:** o cenário executado produziu o resultado esperado.
- **Parcial:** há partes corretas e uma falha ou limitação identificada.
- **Reprovado:** o comportamento observado divergiu do esperado.
- **Não validado nesta rodada:** houve apenas inspeção, referência à suíte anterior ou ausência de cenário suficiente. Não significa falha demonstrada.

## 3. Funcionalidades e resultados

| Funcionalidade | Resultado | Evidência / limite da conclusão | Cabe melhoria? |
|---|---|---|---|
| Inicialização do aplicativo | Aprovado | Executável Release abriu e permitiu realizar os fluxos avaliados. | Sim: título e identificação das funções precisam ser atualizados. |
| Escolha da pasta de origem | Aprovado | Selecionada a subpasta `Acervo`, mantendo gabarito e saídas fora da análise. | Sim: acesso e apresentação dos caminhos completos. |
| Inventário de arquivos | Aprovado | 467 arquivos, sem ausências; tamanhos individuais e total coincidem com o gabarito. | Sim: resumo e grade mais legíveis. |
| Inventário de diretórios/pastas vazias | Aprovado | 43 subpastas; diretórios aparecem no histórico; todas permaneceram após o ciclo de movimentação/restauração. | Sim: CSV deve distinguir claramente situação de pasta e situação de arquivo. |
| Arquivos vazios | Aprovado | 48 identificados; comparação individual sem divergências. | Sem correção funcional indicada pelo cenário. |
| Nomes de backup/temporário | Aprovado | 64 identificados; comparação individual sem divergências. | Sim: descrições amigáveis para categorias técnicas. |
| Último acesso acima de 365 dias | Aprovado na primeira execução | 76 identificados, conforme datas do gabarito. Filtro histórico por acesso também retornou 76. | Sim: tornar a dependência de metadados do Windows mais visível. |
| Múltiplos indícios no mesmo arquivo | Aprovado | Categorias sobrepostas correspondem ao gabarito, incluindo os casos da pasta `07_Multiplos`. | Sim: substituir JSON bruto por apresentação estruturada. |
| Limite de tamanho personalizado | Aprovado | Limite de 1 MiB; 12 arquivos acima dele; comparação individual sem divergências. | Sim: unidades e limiar devem aparecer em um resumo único. |
| Perfil de análise geral | Aprovado | Não aplicou regras de destino; preservou os filtros gerais. | Sim: deixar explícito o escopo das conclusões. |
| Perfil SharePoint Online | Aprovado no laboratório | 24 incompatibilidades: 12 `SPO-NOME-002` e 12 `SPO-NOME-004`; códigos por arquivo coincidem com o gabarito. | Sim: explicar códigos e limitações sem exigir leitura de nomes internos. |
| Perfil Azure Files | Aprovado para os casos disponíveis | 467 arquivos, 0 incompatibilidades e 0 ocorrências; apenas regras Azure habilitadas. | Sim: ampliar casos específicos; o acervo não demonstra limites de vários TiB nem todas as restrições possíveis. |
| Subtipo OneDrive empresarial | Não validado nesta rodada | Controle observado; execução específica não realizada. | Sim: validar separadamente o subtipo e suas recomendações. |
| Detecção de duplicatas exatas | Aprovado | 16 grupos e 111 membros; a composição de todos os grupos coincide com o gabarito, inclusive vazios. | Sim: integrar o resultado à situação consolidada de cada arquivo. |
| Relatório separado de duplicatas | Aprovado | 111 linhas de membros, grupos e SHA-256 consistentes. | Sim: associar claramente ao inventário exportado. |
| CSV completo de inventário | Parcial | 510 registros, incluindo 467 arquivos e 43 pastas; dados e categorias corretos, mas 63 duplicatas recebem situação consolidada incorreta. | **Sim, prioridade alta.** |
| Resumo JSON | Aprovado para os cenários executados | Totais, opções utilizadas, regras de destino, duplicatas e sinalização de conclusão presentes. | Sim: incluir tempos de processamento e definições padronizadas das contagens. |
| Filtros de linhas na exportação M5 | Não validados integralmente nesta rodada | Executado o modo “Todas as entradas”. Inspeção aponta que “Somente achados (qualquer tipo)” também não incorpora duplicatas na decisão de inclusão. | Sim: validar os cinco modos, sobretudo “qualquer tipo”. |
| Histórico SQLite | Aprovado | Nova execução geral persistiu 510 entradas e 16 grupos; consultas posteriores recuperaram os dados; integridade SQLite `ok`. | Sim: apresentação e navegação do histórico. |
| Paginação histórica | Aprovado | Interface avançou para a página 2. Consulta filtrada: páginas de 200, 200 e 67 arquivos. | Sim: melhorar navegação e informar claramente arquivos versus diretórios. |
| Indicador de duplicata no histórico simples | **Reprovado** | Consulta simples marcou 0 duplicatas; consulta filtrada marcou corretamente 111, na mesma execução. | **Sim, prioridade alta.** |
| Filtros operacionais por categoria, extensão, tamanho, data e nome | Aprovado | Verificadas categorias, `.PNG`, tamanho zero, data de acesso e filtro combinado; busca com texto SQL foi tratada como texto literal. | Sim: organização, nomes e aplicação explícita dos filtros. |
| Filtros “Sem alertas” / “Com alertas” | Aprovado na consulta operacional | 236 sem alertas e 231 com alertas; duplicatas consideradas corretamente aqui. | Sim: usar a mesma definição em todas as telas e saídas. |
| Seleção global e preservação entre páginas | Aprovado | 467 marcados; troca da página 1 para a 2 manteve o total; consulta resolveu todos os IDs. | Sim: tornar o alcance da seleção evidente e reduzir a sobrecarga visual. |
| Validação de seleção | Aprovado | IDs duplicados, inexistentes e intervalo de tamanho invertido foram rejeitados; seleção excluiu 10 caminhos indisponíveis informados ao serviço. | Sem falha funcional nesses cenários. |
| Quarentena individual: negação | Aprovado | Serviço recusou confirmação negativa, mantendo o arquivo e sem criar banco de auditoria de teste. | Sim: manter mensagens concisas e específicas. |
| Quarentena em lote | Aprovado | 467 arquivos movidos, 0 falhas. Negar previamente a confirmação manteve os 467 na origem. | Sim: grade de revisão e resumo de conclusão. |
| Hierarquia compartilhada da quarentena | Aprovado | Caminhos relativos preservados sob `CLEAR_Quarentena\Acervo\...`. | Sim: mostrar uma prévia visual de origem e destino. |
| Ocultação de caminhos indisponíveis | Aprovado | Depois do lote, os arquivos desapareceram da lista operacional; histórico permaneceu disponível. | Sim: corrigir total/paginação para refletir os itens disponíveis. |
| Auditoria das movimentações/restaurações | Parcial | Banco contém 467 operações `Restaurado`, íntegro; consulta individual da interface retorna somente 100. | **Sim: consulta completa paginada e exportável.** |
| Restauração em lote maior que 200 arquivos | Aprovado | 467 selecionados na página 1; seleção preservada na página 2; 467 restaurados com 0 falhas. | Sim: confirmação menor e resultado resumido persistente. |
| Atualização da lista após restauração | Aprovado | De página 2 retornou à página 1, com 0 elegíveis e 0 marcados. | Sem falha funcional nesse cenário. |
| Integridade após restauração | Aprovado | 467 caminhos originais; 43 pastas; 0 hashes divergentes; 0 alterações em `LastWriteTimeUtc`; quarentena do teste sem arquivos restantes. | Manter essa conferência em futuras regressões. |
| Repetição de restauração já concluída | Aprovado | Preparação de operação `Restaurado` foi rejeitada. | Sem correção indicada nesse cenário. |
| Limpeza de pastas vazias | Parcialmente validado | Limpeza associada à restauração ocorreu sem perda do acervo. O botão independente “Limpar pastas VAZIAS auditadas” não foi acionado. | Sim: demonstrar escopo e resultados sem ambiguidade. |
| Criação de sub-base experimental | Aprovado | 140 arquivos, 8.240.522 bytes; cópias com hashes corretos; 467 originais mantidos; manifestos publicados. | Sim: apresentação do desvio entre percentual de arquivos e de bytes. |
| Reprodutibilidade da sub-base | Aprovado | Mesma origem, percentual e semente produziram os mesmos 140 caminhos e os mesmos bytes. | Sem falha funcional no cenário. |
| Destino da sub-base dentro da origem | Aprovado | Prévia recusada: origem e destino não podem conter um ao outro. | Manter proteção. |
| Sobrescrita de sub-base existente | Aprovado | Segunda tentativa de cópia no mesmo destino foi bloqueada; arquivos existentes preservados. | Sim: detectar o destino ocupado já na prévia, antes da confirmação. |
| Progresso e ETA | Parcial | Progresso observado em análise, quarentena, restauração e cópia. No começo aparece “calculando”. Não medido o erro estatístico da estimativa. | Sim: apresentação unificada por fase e estimativa explicitamente aproximada. |
| Métricas de duração | Parcial | Tempos de inventário e duplicatas exibidos; tempo total inclui espera humana no diálogo do relatório. | **Sim: separar processamento, interação e tempo de ponta a ponta.** |
| Limite defensivo de indexação M9 | Aprovado | Limite 100 interrompeu o acervo de 467 com mensagem explícita e sem resultado conclusivo de duplicatas. | Sim: explicar custo aproximado e orientar a decisão sobre o limite. |
| Rollback e descarte após falha | Aprovado no limite defensivo | Histórico continuou com 6 execuções; nenhuma nova execução concluída; nenhum `.part` restante. | Manter conferência em outros tipos de falha. |
| Checkpoint após falha | Aprovado | JSON registrou `Falha`, 467 arquivos, 25.723.860 bytes e possibilidade de reinício. | Sim: acesso mais visível ao estado da execução. |
| Reinício com opções diferentes | Aprovado | Ao mudar o limite para 250.000, a interface bloqueou reinício do checkpoint criado com limite 100. | Sim: mostrar quais opções diferem e orientar uma nova execução. |
| Reinício completo confirmado e cancelamento manual | Não validados de ponta a ponta nesta rodada | Não foi executado ciclo visual completo cancelar → reiniciar → concluir. A suíte anterior possui cobertura automatizada de cenários relacionados. | Sim: cenário manual dedicado e evidência específica. |
| Concorrência SHA-256 e benchmark M9 | Não repetidos | Execuções visuais usaram 1 leitor. Não houve comparação de 1/2/4/8 nem benchmark controlado nesta rodada. | Sim: rodada específica para desempenho e consumo de memória. |

## 4. Conferência com o gabarito

Na primeira análise geral foram ativados arquivos vazios, backup/temporários, último acesso acima de 365 dias, tamanho acima de 1 MiB, duplicatas, exportação completa, histórico e checkpoint.

| Verificação | Esperado / referência | Obtido |
|---|---:|---:|
| Arquivos | 467 | 467 |
| Subpastas abaixo da raiz | 43 | 43 |
| Bytes | 25.723.860 | 25.723.860 |
| Arquivos vazios | 48 | 48 |
| Backup/temporário | 64 | 64 |
| Sinal temporal inicial | 76 | 76 |
| Arquivos acima de 1 MiB | 12 | 12 |
| Grupos de duplicatas | 16 | 16 |
| Membros de grupos | 111 | 111 |
| Arquivos com indícios gerais, considerando sobreposição | 156 | 156 |
| Arquivos com algum alerta, incluindo duplicatas e tamanho | 231 | 231 na consulta operacional |
| Arquivos sem alertas | 236 | 236 na consulta operacional |
| Ocorrências na análise geral | 0 | 0 |

As contagens de categorias não devem ser somadas para obter o total de arquivos: um mesmo arquivo pode pertencer a várias categorias.

A comparação individual encontrou **0 divergências em categorias, tamanhos, bytes e códigos de incompatibilidade SharePoint**. Os 16 conjuntos de duplicatas coincidiram integralmente com o gabarito, não apenas na quantidade de grupos.

### Divergência de situação consolidada

O CSV geral apresenta **299 arquivos sem alertas**; a consulta operacional apresenta corretamente **236**. Os **63 excedentes** são arquivos cuja duplicidade é o alerta relevante. O relatório separado de duplicatas os identifica, mas a coluna `SituacaoPreAnalise` do inventário não é atualizada com esse resultado.

O CSV também atribui a expressão “Sem alertas detectados” às 43 pastas. A tela principal distingue pastas como “não aplicável”; convém padronizar essa distinção na exportação.

## 5. Problemas e melhorias prioritários

### P1 — Coerência dos resultados por arquivo — prioridade alta

**Problema comprovado:** duplicatas podem aparecer como sem alertas no CSV. A tela principal calcula sua situação antes da etapa de duplicatas; a inspeção do fluxo aponta a mesma omissão. A consulta histórica simples preenche `EhDuplicataExata` como falso, enquanto a consulta filtrada encontra 111 membros corretamente.

**Impacto:** o usuário recebe respostas diferentes para o mesmo arquivo conforme a tela/relatório consultado. A frase “não significa aprovado” não elimina essa contradição.

**Melhoria proposta:** definir uma situação consolidada única para arquivo, com categorias, compatibilidade, tamanho e duplicidade. Usar esse resultado em inventário, histórico, filtros e exportação. No modo em fluxo, publicar explicitamente a informação ainda não calculada e consolidar após a deduplicação. Não alterar o gabarito para acomodar a divergência.

**Referências locais:** `src/Clear.Desktop/Form1.cs`, `src/Clear.Infrastructure/Relatorios/ExportadorRelatoriosM5.cs`, `src/Clear.Infrastructure/Historico/ConsultaHistoricoSqliteM6.cs` e `ConsultaEntradasFiltradasM10.cs`.

### P2 — Tempo total inclui interação humana — prioridade média/alta para o TCC

Na primeira execução, a interface mostrou inventário de **863,4 ms**, duplicatas de **5.233,9 ms** e total de **38.544,4 ms**. O diálogo de relatório é modal e aparece antes de confirmar o histórico e parar o cronômetro total. Portanto, o tempo aguardando o fechamento do diálogo faz parte da medida.

**Melhoria proposta:** concluir persistência e medição antes da mensagem final; exibir separadamente tempo de processamento, espera do operador e duração de ponta a ponta. Os números desta rodada são observações locais, com cache e outras cargas não controlados; não representam um benchmark científico do CLEAR.

### P3 — Auditoria individual incompleta na interface — prioridade média

O serviço que abastece a grade individual usa `LIMIT 100`. Há 467 operações restauradas no banco, mas a consulta retorna 100. A restauração global usa outra consulta e conseguiu alcançar o lote completo.

**Melhoria proposta:** paginação e busca da auditoria, contador total e exportação completa. Mostrar explicitamente qualquer limite visual. Referências: `ServicoQuarentenaM7.ConsultarAsync` e `FormDetalhesHistoricoM6.AtualizarAuditoriaAsync`.

### P4 — Layout esconde operações — prioridade média

Foram observados controles com textos cortados, rolagem horizontal e vertical dentro de painéis pequenos, poucas linhas úteis na grade principal e comandos importantes fora da área visível. A janela histórica padrão oculta o botão de seleção filtrada quando os controles da barra quebram para outra linha; maximizar a janela tornou o comando visível nesta sessão.

**Melhoria proposta:** reorganizar a interface por tarefa, com área de resultados predominante; opções avançadas recolhíveis; barras de ações com altura adaptável; larguras mínimas apropriadas; uma área de detalhes do arquivo; e revisão em diferentes escalas de DPI e dimensões de janela.

### P5 — Confirmações grandes e informação técnica — prioridade média

A confirmação de restauração com caminhos longos ficou maior que a área útil da janela capturada, dificultando alcançar visualmente os botões. O histórico mostra nomes de propriedades e JSON bruto. A indicação “somente leitura” no título histórico também pode confundir, pois essa janela oferece operações que movimentam arquivos.

**Melhoria proposta:** substituir mensagens muito grandes por janela de revisão com lista rolável, resumo do lote, destino, riscos concretos e botões sempre visíveis. Mostrar nomes amigáveis e evidências formatadas; manter JSON como detalhe técnico opcional. Atualizar títulos que ainda citam apenas M6, embora o programa inclua módulos posteriores.

### P6 — Totais e estado das listas — prioridade média

Depois da quarentena, a lista mostrou histórico filtrado de 467, disponíveis na página 0 e página 2. O comportamento é compatível com a implementação atual, mas exige que o usuário compreenda dois totais e uma página sem itens operacionais.

**Melhoria proposta:** contar e paginar os elegíveis após aplicar indisponibilidade, informar os totais histórico/disponível em locais separados e apresentar estado vazio com explicação e próximo passo. Na restauração, o retorno à página 1 vazia já funcionou corretamente.

## 6. Metadados e reprodutibilidade experimental

Os hashes e as datas de última modificação dos 467 arquivos permaneceram iguais ao gabarito no fim dos testes. Contudo, **419 datas de último acesso diferem dos valores registrados na geração do laboratório**. As leituras do CLEAR e as conferências de integridade realizadas na avaliação podem participar dessa atualização; não é possível atribuir cada mudança a uma leitura específica.

Na análise geral inicial houve 76 sinais temporais. Em uma execução posterior houve 8. Isso não prova mudança na regra: o Windows pode atualizar `LastAccessTimeUtc` após abrir conteúdos, incluindo operações SHA-256. Os resultados históricos da primeira execução continuam preservados.

Para a metodologia do TCC, registrar a configuração do sistema de arquivos, capturar os metadados antes da primeira leitura de conteúdo e tratar cada execução como uma observação com referência temporal própria. Não interpretar acesso antigo como prova de inutilidade. Uma base reproduzida para novo experimento precisa ter suas datas planejadas e observadas verificadas novamente.

## 7. Avaliação da interface

| Aspecto | Avaliação | Ajuste sugerido |
|---|---|---|
| Organização inicial | Muitos parâmetros expostos simultaneamente; início pouco guiado. | Sequência “Origem → Opções → Analisar → Resultados → Ações”. |
| Hierarquia visual | Área de configuração ocupa espaço desproporcional; resumo pouco destacado. | Indicadores de totais e área central dedicada aos resultados. |
| Legibilidade | Caminhos, códigos e títulos de colunas frequentemente truncados. | Nome do arquivo separado do caminho; detalhes laterais; colunas ajustáveis. |
| Navegação | Operações relevantes dependem de rolagem interna ou maximização. | Menu de tarefas e barras com adaptação ao espaço disponível. |
| Estados e feedback | Mensagens úteis de erro, mas conclusão, fase e histórico disputam o rodapé. | Estado persistente por execução, com resultado e falhas consultáveis depois. |
| Seleção em lote | Seleção global e contador funcionaram, inclusive entre páginas. | Diferenciar visualmente “página”, “todos filtrados” e “selecionados”. |
| Segurança das decisões | Confirmação negativa preservou o acervo; limites e destinos inválidos foram bloqueados. | Preservar essas garantias durante qualquer mudança visual. |
| Evidências por arquivo | Disponíveis, mas frequentemente em JSON técnico. | Linguagem do usuário, categorias visuais e motivo da classificação. |
| Teclado | Tab e Espaço permitiram alcançar e marcar controles escondidos. | Revisar ordem de foco, atalhos e indicação de foco; leitor de tela não validado. |
| Estética | Controles nativos sem um sistema visual consistente. | Tipografia, espaçamento, cores, ícones e componentes padronizados. |

O problema observado não decorre de uma impossibilidade do Visual Studio de produzir uma interface melhor. A implementação atual pode melhorar bastante com outro layout e organização de tarefas, mesmo mantendo WinForms. Trocar o framework também não resolve automaticamente esses problemas: o desenho da experiência precisa ser revisto.

## 8. Alternativas de interface preservando o núcleo

### O que o projeto atual permite reaproveitar

O CLEAR possui `Clear.Domain`, `Clear.Application`, `Clear.Infrastructure` e `Clear.Desktop`. As três primeiras bibliotecas usam `net10.0`; o projeto Desktop usa `net10.0-windows` com WinForms. A inspeção não encontrou referência a `System.Windows.Forms` ou `Clear.Desktop` nas três bibliotecas do núcleo.

Isso favorece criar outra apresentação que referencie as bibliotecas C# existentes. **Não é necessário traduzir regras de negócio, SHA-256 ou comunicação SQLite para outra linguagem apenas para trocar a interface.**

Há, porém, coordenação de operações dentro dos formulários, especialmente `Form1`. Uma futura mudança de apresentação precisará adaptar essa coordenação a serviços de aplicação ou modelos de apresentação. Isso é diferente de reescrever o domínio ou substituir o acesso ao banco. A compatibilidade entre sistemas operacionais exige avaliação própria das operações de arquivos e das regras Windows.

| Alternativa | Linguagens da interface | Reaproveitamento do núcleo | Adequação e custo relativo |
|---|---|---|---|
| Reorganizar WinForms | C# | Direto, mantendo a aplicação atual. | Menor transição; resolve layout e fluxo sem trocar a tecnologia. |
| WPF | XAML e C# | Direto por referência às bibliotecas .NET. | Boa opção para um aplicativo Windows com estilos, templates e separação por MVVM; reconstrução das telas. |
| Blazor Hybrid | Razor/C#, HTML e CSS | Código .NET executa localmente; pode usar as bibliotecas existentes. | Boa opção para aparência próxima à web; demanda integração com WebView e gestão do estado da interface. Pode ser hospedado em WinForms ou WPF. |
| Avalonia | AXAML e C# | Bibliotecas C# reaproveitáveis, sujeitas à verificação de dependências da plataforma. | Considerar se houver objetivo real de Windows/Linux/macOS; envolve reconstruir as telas. |
| React/TypeScript + serviço ASP.NET Core | TypeScript/JavaScript, HTML e CSS | Núcleo continua em C#, chamado por uma API; SQLite permanece no serviço .NET. | Possível e flexível, mas adiciona comunicação, execução e empacotamento de componentes. Maior complexidade para o escopo atual. |

WPF oferece recursos de layout, estilos, templates e ligação de dados no ecossistema .NET, com execução em Windows. [Documentação WPF](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/overview/).

Blazor Hybrid executa componentes .NET no dispositivo e os apresenta em uma WebView; há integração com WinForms e WPF, permitindo acrescentar uma interface baseada em HTML/CSS preservando código local. [Documentação Blazor Hybrid](https://learn.microsoft.com/en-us/aspnet/core/blazor/hybrid/).

Avalonia permite compartilhar apresentação e lógica C# entre plataformas, mas recursos dependentes do sistema precisam ser tratados separadamente. [Arquitetura multiplataforma do Avalonia](https://docs.avaloniaui.net/docs/fundamentals/cross-platform-architecture).

Separar React na apresentação e ASP.NET Core no backend é um modelo documentado pela Microsoft. Para o CLEAR, a API seria uma camada adicional sobre as bibliotecas existentes; o navegador não deveria assumir a comunicação direta com os arquivos e o SQLite. [React com ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/client-side/spa/react).

**Recomendação para decisão posterior:** comparar um redesenho de uma tela em WinForms com uma pequena prova de conceito em WPF. Se a preferência for um visual de aplicação web, avaliar Blazor Hybrid. Não há justificativa demonstrada nesta rodada para reescrever o núcleo em outra linguagem. WPF e WinForms continuam suportados no .NET moderno; migrar para WinUI 3 não é uma obrigação. [Orientação Microsoft sobre modernização](https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/migrate-to-windows-app-sdk/migration-decision-guide).

## 9. Cobertura restante

Esta avaliação cobre os principais fluxos no laboratório de 467 arquivos, sem certificar todas as combinações de entradas e falhas. Permanecem como cenários específicos para outra rodada:

- Cancelamento visual durante inventário, hashing, cópia, quarentena e restauração; reinício confirmado até a conclusão.
- Queda do processo/energia durante operações auditadas e reconciliação de estados pendentes.
- Conflito no caminho original durante restauração, adulteração de conteúdo entre prévia e execução e perda de permissões durante o lote, por interação visual.
- Destinos em outro volume, unidade de rede, disco cheio e remoção de dispositivo.
- Acervos acima de 10.000 itens selecionados e do limite padrão de indexação; consumo de RAM e comportamento prolongado.
- Links/junctions e pastas inacessíveis como experimentos manuais dedicados, sem alterar políticas de segurança do Windows.
- Todos os modos de exportação; subtipo OneDrive; todos os limites e regras específicas dos perfis.
- Leitor de tela, alto contraste, múltiplos monitores e diferentes escalas de DPI.
- Instalação/distribuição em máquina sem o ambiente de desenvolvimento e teste do executável Debug pelo Visual Studio.

Esses itens devem permanecer como “não validados manualmente nesta rodada”, mesmo quando existirem testes automatizados relacionados aprovados anteriormente.

## 10. Estado final e evidências

- Repositório do CLEAR sem alterações rastreadas ou pendentes ao término da inspeção; nenhuma correção ou refatoração implementada e nenhum commit criado nesta etapa.
- Acervo original: 467 arquivos, 43 subpastas, hashes corretos e nenhuma alteração nas datas de última modificação.
- Quarentena usada no teste: `C:\Temp\CLEAR_Laboratorio_Sintetico_TCC\CLEAR_Quarentena`; nenhum arquivo restante após restauração. A pasta `Quarentena` que já existia no laboratório não foi usada como destino deste lote.
- Sub-base de teste preservada em `C:\Temp\CLEAR_Laboratorio_Sintetico_TCC\03_amostras\Avaliacao_20261022_30pct`.
- Foram acrescentadas três execuções concluídas ao histórico local: geral, SharePoint e Azure. A execução de limite defensivo não foi publicada como concluída.
- Auditoria local contém 467 operações do teste em estado `Restaurado`; registros anteriores permaneceram preservados.
- O checkpoint de falha controlada foi preservado como evidência; o limite da interface foi devolvido a 250.000. O reinício com configuração diferente foi bloqueado corretamente.
- Relatórios gerados pelo aplicativo ficaram em `C:\Users\Leucimar\AppData\Local\CLEAR\Relatorios`; cópias foram reunidas na pasta de evidências abaixo.

Pasta de evidências: `C:\Users\Leucimar\Documents\ChatGPT\TCC_Leucimar\avaliacao_CLEAR_2026-10-22`.

| Arquivo / conjunto | Conteúdo |
|---|---|
| `comparacao_final.json` | Comparação com gabarito, integridade final, grupos de duplicatas e estados da auditoria. |
| `divergencias_detalhadas.json` e `divergencias_gabarito.csv` | Identificação dos 63 arquivos com situação consolidada divergente. |
| `verificacoes_complementares.json` | 26 verificações aprovadas e observações dos indicadores históricos e do limite de auditoria. |
| `integridade_antes_movimentacao.json` / `integridade_apos_restauracao.json` | Conferências antes e depois do lote. |
| `metadados_iniciais.csv` / `metadados_finais_resumo.json` | Captura inicial e diferenças finais dos metadados. |
| `verificacao_amostra.json` / `manifesto_resumo.json` / `manifesto_arquivos.jsonl` | Verificação e manifestos da sub-base de 140 arquivos. |
| `CLEAR_M5_20261022_204216_*` | Análise geral inicial, usada para comparação temporal e de categorias com o gabarito. |
| `CLEAR_M5_20261022_205021_*` | Análise SharePoint. |
| `CLEAR_M5_20261022_205058_*` | Análise Azure Files. |
| JSON de checkpoint | Registro da falha controlada de indexação. |
| `ambiente.json` | Sistema e características do computador. |
| `backup_acervo` | Cópia de segurança preparada antes da movimentação em lote. |

A decisão sobre correções, refatoração e eventual troca de apresentação fica para a próxima etapa, conforme solicitado.
