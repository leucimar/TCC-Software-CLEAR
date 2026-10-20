# M11 — Quarentena compartilhada, restauração em lote e classificação multirrótulo

**Branch:** feature/fluxo-quarentena-filtros-eta (continuação da entrega M10).
**Estado:** código de revisão, ainda sem build/testes Windows desta alteração e sem merge.

## Correção da hierarquia

**Antes:** Quarentena / GUID / caminho relativo (uma pasta GUID por arquivo).
**Agora, para novas operações:** Quarentena / NomeDaPastaAnalisada / caminho relativo completo, como Quarentena / 02_original / PDF / 001.pdf. Diretórios já existentes são reutilizados. Nenhum diretório exclusivo por arquivo é criado. A organização da raiz considera o nome da pasta selecionada; duas raízes diferentes com mesmo nome na mesma quarentena são recusadas quando detectadas na auditoria, evitando misturar origens. File.Move permanece mesmo volume e overwrite=false.

O GUID continua existindo como ID da movimentação, no SQLite, não como nome de diretório na nova árvore. Audit v1 recebe migração transacional para v2 com campos opcionais raiz_analisada e estrutura. Movimentações antigas não são alteradas por essa migração. Ao RESTAURAR registros antigos, o M8 ainda reconhece Quarentena/GUID/arquivo ou Quarentena/GUID/subpastas/arquivo, incluindo bancos originais v1. Não migrar automaticamente os ~400 arquivos já na quarentena: os caminhos no SQLite precisam permanecer exatos até a restauração.

## Restauração filtrada e em lote

Na consulta histórica > Auditoria, botão Filtrar / restaurar vários. O filtro consulta registros com estado EmQuarentena da execução ativa, por extensão, texto do caminho e intervalo de tamanho, com paginação (200 linhas). O usuário marca arquivos individualmente, seleciona a página ou escolhe explicitamente Selecionar TODOS filtrados (até 10.000 itens). O botão de seleção em si NÃO restaura. Uma nova confirmação mostra a contagem, bytes e amostra dos caminhos, e somente após SIM inicia a restauração. Cada item usa M8 PrepararAsync e RestaurarAsync, com SHA-256 e conferência de conflito, além da própria auditoria. Falhas ficam individualizadas; não há rollback automático dos arquivos já restaurados.

Ao concluir uma restauração com auditoria confirmada, o M8 remove apenas diretórios VAZIOS ao longo do caminho dentro da raiz da quarentena, nunca a pasta raiz e nunca arquivos. Essa limpeza é melhor esforço; se falhar, o arquivo restaura normalmente. Diretórios compartilhados que ainda contêm outro arquivo não são removidos.

Botão Limpar pastas VAZIAS auditadas, com confirmação separada, considera apenas ancestrais dos caminhos registrados no banco M7, inclusive GUIDs das restaurações anteriores. Não apaga arquivos nem pasta raiz. O escopo é o destino de quarentena configurado na interface; verificar cuidadosamente antes de autorizar.

## Categorias

SinalTemporal é um INDÍCIO de classificação geral e não impede que o mesmo arquivo pertença a um grupo de duplicatas. A participação em duplicatas exatas é armazenada separadamente em membros_duplicatas. O painel M10 passa a exibir o rótulo adicional DUPLICATA EXATA e o filtro Categoria aceita Duplicata exata ou Incompatibilidade documentada, mapeados para filtros próprios. Filtros combinados continuam AND; clicar Aplicar filtros é necessário para atualizar a lista. Uma regra temporal em todos os arquivos não significa que os demais sinais foram eliminados.

## Limitações e segurança

- Ainda não fazer merge. Requer compilar/testar e validar exclusivamente acervo sintético em C:\Temp.
- Não executar movimentações ou limpeza na base pública principal. Existem arquivos reais de teste na quarentena atual e todos os caminhos devem permanecer íntegros.
- Não substituir o banco de auditoria antigo; realizar cópia de segurança antes do primeiro uso da nova versão.
- Relatório de duplicatas no histórico é observação, não autorização automática para mover todos os arquivos duplicados; escolha humana permanece obrigatória.
- Duas pastas de origem com nomes idênticos devem usar quarantenas diferentes caso compartilhem volume, para não mesclar árvores.
