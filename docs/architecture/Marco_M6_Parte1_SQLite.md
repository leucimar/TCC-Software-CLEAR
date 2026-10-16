# M6 — Parte 1: histórico persistente em SQLite

**Estado:** feature `feature/historico-sqlite-m6` ainda não compilada nem aprovada pelo autor. Banco separado dos diretórios analisados; `main` e `develop` preservadas.

## Escopo desta entrega

O CLEAR passa a disponibilizar **armazenamento histórico local opt-in** e uma aba para consultar as 30 execuções concluídas ou parciais mais recentes. O banco padrão da interface é `%LOCALAPPDATA%\CLEAR\historico.sqlite3`. `C:\Temp` permanece como local de *dados sintéticos de teste*, não como destino permanente do histórico pessoal.

A biblioteca `Microsoft.Data.Sqlite` (NuGet 10.0.0, pacote gratuito) é utilizada exclusivamente na camada Infrastructure. O esquema v1 armazena:
- `execucoes`: identificação, pasta raiz, início/fim UTC, estado `Concluida` ou `Parcial`, configuração JSON, resumo JSON, quantidades;
- `entradas`: cada arquivo/diretório enumerado, metadados, categorias, evidências, verificações de compatibilidade e indicador do filtro de tamanho;
- `grupos_duplicatas`: grupos confirmados pelo M4, com SHA-256, tamanho e estimativa teórica;
- `membros_duplicatas`: caminhos que pertencem aos grupos;
- `ocorrencias`: ocorrências de inventário e deduplicação (amostra de detalhes disponibilizada pelo caso de uso).

Todos os caminhos, metadados e regras são **dados de uma análise passada**; não representam o estado corrente dos arquivos. Grupos de SHA-256 não autorizam exclusão. O processo não move, renomeia, migra nem altera arquivos do acervo.

## Resiliência, limites e auditoria

- A escrita SQLite ocorre em **uma transação por análise**. Se houver exceção, cancelamento ou a sessão for descartada sem conclusão, ocorre rollback da execução inteira, sem a publicar como concluída.
- Se a enumeração registrar falhas mas concluir normalmente, a execução recebe estado `Parcial`, com total de ocorrências. Não é rotulada como totalmente confiável.
- A gravação das linhas acontece durante **a mesma enumeração** M3, via `IObservadorInventario`; um `ObservadorInventarioComposto` permite gravar histórico e relatório CSV simultaneamente, sem duplicar a enumeração de inventário.
- M4, quando habilitado, continua executando sua enumeração e leitura de conteúdo próprias. Seus grupos confirmados são armazenados ao concluir.
- O caminho do banco não pode ficar **dentro** da origem analisada e não pode utilizar diretórios com links/junctions detectáveis. O esquema usa chaves estrangeiras e consultas parametrizadas.
- O banco contém caminhos completos, nomes, hashes e justificativas potencialmente confidenciais; deve ter proteção local por conta do Windows. **O SQLite nesta fase não oferece criptografia própria.**
- A geração CSV/JSON M5 continua independente. Se o CSV for publicado e a etapa SQLite falhar depois, o CSV poderá permanecer gerado; não existe transação distribuída entre os formatos.
- A aba de histórico mostra **resumos**; consultas históricas detalhadas e paginação de itens poderão ser adicionadas em outra parte do M6, após a validação desta base. O banco já guarda os dados necessários.
- A versão de esquema é `PRAGMA user_version = 1`. Banco de versão desconhecida ou com tabelas preexistentes não reconhecidas é rejeitado, sem tentativa automática de migração.

## Reaproveitamento de classificações

**Não implementado nesta etapa.** O histórico é fonte auditável, não cache. Para eventual reaproveitamento futuro, precisaremos de invalidadores para:
1. mudança de arquivo/caminho/conteúdo e regras de metadados;
2. mudanças na versão das regras, perfil de destino e parâmetros temporais;
3. hashes/duplicatas, que exigem validação especialmente cuidadosa;
4. resultados parciais, falhas, permissões e períodos de medição.

O ganho de desempenho e o risco de resultados obsoletos serão investigados antes de habilitar cache (M6 posterior / M9).

## Critérios de aceite

1. `dotnet restore`, build Release e MSTest completos, sem erros nem avisos; a nova dependência deve restaurar corretamente no Windows 10/11.
2. Banco persistente após fechar e reabrir o aplicativo; duas execuções distintas preservadas no histórico.
3. Cenário sintético em `C:\Temp` com 8 arquivos, 2 subpastas e 3 grupos de duplicatas; a aba de histórico registra 10 entradas e 3 grupos.
4. Cenário com CSV e SQLite simultâneos; CSV M5 permanece correto, sem segunda varredura de metadados para persistência.
5. Cancelamento/falha elimina dados não consolidados da execução (transação rollback).
6. O banco não pode ser criado dentro da raiz analisada.
7. A ferramenta continua não destrutiva; nenhuma migração, quarentena ou rollback existe nesta entrega.

Não realizar merge sem validação local e aprovação explícita.
