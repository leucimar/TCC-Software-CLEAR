# M7 — Parte 1: quarentena individual com revisão humana e auditoria

Estado: feature/quarentena-auditavel-m7, experimental e aguardando validação local. Prioridade ao núcleo; interface somente funcional.

## Escopo

- A revisão humana significa autorização explícita para toda alteração de estado do arquivo. O M7 oferece apenas movimentação individual à quarentena; não migra à nuvem nem exclui.
- O usuário escolhe uma execução concluída no Histórico M6, consulta os detalhes e seleciona um arquivo. A preparação somente leitura verifica que o arquivo pertence à raiz analisada, é regular, não é link/reparse point, tem tamanho/data compatíveis com a análise e calcula SHA-256 em fluxo.
- O CLEAR exibe origem, destino, tamanho, hash SHA-256 e ID da análise. Apenas a confirmação explícita SIM autoriza o movimento. NENHUM filtro ou classe gera uma ação automática.
- No momento da execução são revistos tamanho, data UTC e SHA-256. Alterações detectadas bloqueiam a operação.
- A movimentação exige mesmo volume, caminho seguro, destino único por GUID e ausência de sobrescrita. Não permite destino dentro da origem nem vice-versa.
- A auditoria escreve registro Pendente antes do movimento físico. Depois atualiza para EmQuarentena ou, se a origem permanecer intacta, Falha. O banco é persistente e consultável na nova aba de auditoria.

## Armazenamento

- Quarentena: %LOCALAPPDATA%\CLEAR\Quarentena\<GUID>\<nome original>
- Auditoria: %LOCALAPPDATA%\CLEAR\quarentena.sqlite3
- Histórico M6: %LOCALAPPDATA%\CLEAR\historico.sqlite3 (esquema inalterado)
- Dados de teste sintéticos: C:\Temp

A auditoria registra ID da operação, ID da execução M6 e da entrada, caminhos, tamanho, SHA-256 e estado. Não é criptografada; os caminhos podem ser sensíveis.

## Restrições relevantes

- M8 ainda precisa implementar recuperação/rollback; até lá, testar APENAS com dados sintéticos.
- O arquivo movido não é eliminado. Nenhum descarte é autorizado por indícios de vazio, temporários, tamanho ou duplicatas.
- A transferência do filesystem e o commit SQLite não são uma transação distribuída: falha de processo ou energia no instante crítico pode deixar registro Pendente e arquivo já movido. O M8 deverá reconciliar esses casos de maneira explícita.
- Existe janela de concorrência entre revalidação e movimentação. Não afirmar proteção absoluta contra ataques de troca de caminhos, links/hardlinks e manipulação hostil de metadados.
- O SQLite de auditoria fica fora do acervo e da quarentena; nunca sobrescrever arquivo existente. Não mover pastas nem executar em lotes.
- A restauração e confirmação para restaurar pertencem ao M8. Alterações cosméticas extensas serão feitas depois de completar o núcleo.

## Critérios de aceite

- Testes M0–M7, build Release e auditoria NuGet sem avisos.
- Preparar e negar deixam origem intacta; autorizar movimenta um arquivo sintético, preservando bytes e registrando a decisão.
- Recusar mudança de conteúdo ou metadados, histórico parcial, pasta, destinos inseguros e segunda tentativa sobre o mesmo plano.
- A aba de auditoria permite consultar os estados persistidos sem ferramentas externas.
- Não integrar na develop/main antes de validação e aprovação explícita.
