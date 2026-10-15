# Marco M4 — Detecção de duplicatas exatas

**Estado:** implementação em `feature/duplicatas-exatas-m4`; aguarda testes no notebook, revisão e aceite antes de merge. Alvo principal Windows 11; desktop Windows 10 pendente.

## Objetivo

Identificar grupos de arquivos com conteúdo idêntico por SHA-256 em fluxo e confirmação byte a byte, sem presumir que qualquer cópia deva ser removida. O CLEAR não migra arquivos nem executa ações modificadoras.

## Escopo e responsabilidades

- `Clear.Domain.Duplicatas`: grupo com dois ou mais arquivos, mesmo tamanho, SHA-256 de 64 dígitos hexadecimais, estimativa `(N - 1) × tamanho`.
- `Clear.Application.Duplicatas`: caso de uso `DetectarDuplicatasExatas`; contrato `ILeitorConteudoDuplicatas`; inventário prévio por metadados, hash restrito a grupos com tamanho repetido, comparação de bytes para eliminar colisões/alterações e resultado com ocorrências e progresso.
- `Clear.Infrastructure.Duplicatas`: SHA-256 streaming por `SHA256.HashDataAsync`, `FileStream` somente leitura, `FileShare.Read` e buffer de 64 KiB na comparação. Verificações antes/depois de tamanho, última modificação UTC e reparse points. Links ou caminhos por junction detectáveis são recusados. Erro individual deixa o diagnóstico parcial e não confirma arquivos sem comparação.
- `Clear.Desktop`: checkboxes **visíveis em três seções simultâneas** (gerais, Azure e SharePoint/OneDrive); apenas um destino permanece habilitado por vez. Ao escolher um destino, aplica-se o conjunto recomendado de verificações daquele ambiente; ajustes manuais posteriores são respeitados até uma troca de destino. Arquivos vazios e backups/temporários começam habilitados; temporal, limite pessoal e duplicatas permanecem desligados até marcação explícita. Os controles gerais nunca são alterados pelas sugestões de ambiente.
- `Clear.Domain.Analise`: fábrica `CriarComFiltrosSelecionados` monta apenas regras gerais marcadas sem modificar o comportamento dos construtores antigos. `Clear.Application.Compatibilidade`: `PerfisRecomendados` fornece IDs documentados, separados da UI.
- `Clear.Desktop`: checkbox M4 opt-in; após o inventário M3, realiza outra enumeração independente e usa aba separada para mostrar grupos. Renderiza até 200 linhas de membros, mantendo totais calculados de todos os grupos. Não foi implementado um relatório M5 nem modernização da tabela.

## Decisões de segurança e consistência

- **Somente leitura de conteúdo.** Não mover, copiar, renomear, excluir, registrar decisão de descarte nem sobrescrever nada.
- **SHA-256 é pré-agrupamento, não única prova de igualdade:** a comparação adicional de bytes tem o poder de separar hashes idênticos por colisão artificial/corrupção.
- **Estabilidade verificada:** comprimento e última modificação UTC após o inventário e após cada leitura; o handle permanece aberto durante a checagem após leitura. `FileShare.Read` limita escritas concorrentes no Windows; não elimina ataques de alteração de metadados com preservação artificial de timestamp ou substituição externa de caminho. Resultado não é snapshot transacional.
- **Arquivos inacessíveis/alterados:** ocorrências registradas (até 100 detalhes, total completo), item problemático não aparece como duplicata confirmada sem teste. Um erro não autoriza concluir que o arquivo é único.
- **Cancelamento:** o `CancellationToken` é passado à fonte de inventário, leitura SHA-256 e comparações; o resultado parcial não é publicado como conclusão.
- **Consumo:** somente arquivos com tamanho repetido são lidos; arquivos únicos por tamanho não têm hash calculado. Não há cache/persistência e não há paralelismo de hashing nesta versão (evita sobrecarga imprevisível de I/O). O índice por tamanho mantém os caminhos em memória, portanto volumes enormes exigirão ensaios M9 e possível tratamento em SQLite M6.
- **Arquivos vazios:** são incluídos se existem pelo menos dois, porém o potencial teórico de economia desse grupo é zero.
- **Hardlinks e links:** links simbólicos/junctions são ignorados; arquivos representando múltiplos nomes/hardlinks físicos ainda exigem tratamento explícito em futura evolução e não devem produzir projeção financeira automática.

## Limites e pendências para M5–M10

Não há decisão humana sobre cópia a preservar, SQLite, geração de relatórios, reutilização de hash, comparação distribuída, exclusão, quarentena ou rollback. O campo de potencial teórico não representa bytes recuperáveis com segurança. Falhas no inventário ou leitura geram análise parcial. Não fazer testes de desempenho sobre acervos corporativos; cenário sintético apenas.

## Critérios de aceite

1. Compilação Release sem erros ou avisos (incluindo a correção de MSTEST0037); testes antigos e novos aprovados no Windows 11.
2. Arquivos com mesmos bytes e nomes diferentes agrupados; arquivos de mesmo tamanho com bytes distintos excluídos.
3. Arquivos únicos por tamanho não abertos para hash; arquivos vazios tratados corretamente.
4. Alteração entre coleta de metadados e hash rejeitada; falha de leitura e cancelamento não geram grupos falsos.
5. Conteúdo e última modificação dos arquivos sintéticos permanecem iguais após a análise.
6. UI preserva M3, mostra aba específica, totais corretos, estado de falhas e não executa ações destrutivas.
7. Todos os filtros ficam visíveis; perfis oficiais são marcados automaticamente por destino e podem ser desmarcados depois. Sem destino, não se aplicam regras específicas. A seleção de destino não ativa SHA-256 nem modifica as opções gerais.

**Ainda não executado no ambiente de autoria:** não há .NET SDK instalado neste ambiente de edição remota. O autor executará `dotnet restore/build/test/run` no Visual Studio e trará evidências. Nenhum merge sem aprovação.
