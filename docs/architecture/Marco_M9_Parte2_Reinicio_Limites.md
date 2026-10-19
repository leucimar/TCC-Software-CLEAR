# M9 Parte 2 — Checkpoint de progresso e limite de indexação

Status: feature/checkpoint-reinicio-seguro-m9-parte2; implementação experimental sem compilação ou testes no Windows. UI somente funcional.

## Decisão arquitetural

O inventário atual M3 não oferece ordenação estável da enumeração e o M6 reverte a transação de histórico quando a análise é cancelada. Continuar a partir do último caminho lido ou reutilizar classificações/hashes seria inseguro, pois o acervo pode mudar. Por isso, esta primeira entrega salva um marcador de progresso, mas só oferece **reinício integral explicitamente confirmado**, sem reaproveitar dados antigos. Isto NÃO é retomada incremental.

## Checkpoint opcional

- Controle: Salvar progresso para reinício seguro (M9; sem cache).
- Arquivo: %LOCALAPPDATA%\CLEAR\Checkpoints\<SHA-256 da raiz normalizada>.json. O SHA da raiz é uma chave do nome; não é hash do conteúdo analisado.
- Registra versão, ID, raiz, assinatura SHA-256 das configurações, estado, arquivos/bytes observados e datas UTC. Nunca salva classificações, hashes de conteúdo nem resultados parciais.
- Publicação via arquivo temporário e File.Move no mesmo diretório; trava interprocesso .lock FileShare.None. Impede duas sessões simultâneas usando checkpoint na mesma raiz.
- Periodicidade: a cada 1000 arquivos ou 5 segundos, com persistência forçada ao encerrar por cancelamento ou falha. Um encerramento abrupto pode deixar o último registro EmAndamento, sem prova de conclusão.
- Estados válidos: EmAndamento, Cancelada, Falha, Interrompida. Sucesso integral remove o marcador.
- Checkpoint deve ficar fora da raiz e não conter a raiz; não aceitar ancestral reparse point detectável, links no arquivo ou na trava.
- Assinatura inclui filtros gerais, temporal/dias, limite personalizado, perfil e regras, SHA concorrente, limite RAM, exportação e histórico. Alteração de configuração recusa reinício. A data de referência temporal é recalculada porque todos os arquivos serão analisados NOVAMENTE.

## Reinício assistido

- Usuário seleciona a mesma raiz e configura as mesmas opções.
- Botão: Reiniciar análise interrompida (do início). Valida checkpoint, versão e assinatura.
- Antes da operação, uma confirmação informa último progresso e que nada será reutilizado. Resposta NÃO preserva o estado.
- Resposta SIM inicia nova análise do começo pelos mesmos caminhos de execução M3/M4/M5/M6, sem confiar em conteúdo nem metadados anteriores. Se houver novos arquivos, são inventariados; removidos não são herdados.
- Essa retomada do trabalho é apenas operacional. Checkpoint não é um snapshot nem progresso reutilizável; o benefício é auditabilidade de interrupção, não economia de CPU/IO.

## Limite defensivo da memória M4

- DetectarDuplicatasExatas ganhou maximoArquivosIndexados de 1 a 1.000.000, padrão 250.000, parametrizável na UI quando SHA-256 estiver ativo.
- Ultrapassado o limite, lança InvalidOperationException com diagnóstico antes de iniciar os hashes; não publica resultado falso e o histórico M6 permanece sujeito ao rollback usual.
- O inventário M3 permanece com amostra visual de até mil itens. O limite de índice não corresponde a bytes de memória exatos nem elimina a pressão de RAM dos primeiros N arquivos.

## Limites e segurança

- Nenhum arquivo original é alterado pelo checkpoint; M7 e M8 continuam exigindo confirmação humana independente.
- Caminhos em JSON e dados de progresso podem ser sensíveis. Não compartilhe sem revisão.
- Falha de disco, power loss, mudanças hostis de caminho e concorrência externa podem deixar marcadores antigos. Não alegar recuperação transacional total.
- Cache ou verdadeira retomada incremental com validação de conteúdos, parâmetros, versões de regras e evolução de estrutura ficam para incrementos futuros. O TCC não deve afirmar que tais recursos já existem.

## Aceite

- Esperados 210 testes: 198 anteriores + 9 de checkpoint + 3 de limite.
- Build Release sem avisos, auditoria NuGet sem vulnerabilidades detectadas nas fontes consultadas.
- Cancelar e reiniciar com confirmação, negar confirmação, recusar configuração modificada e refazer a enumeração integral.
- Ultrapassar limite de indexação interrompe M4 com mensagem e sem concluir resultados; repetir dentro do limite mantém resultado.
- Dados sintéticos em C:\Temp apenas. Nenhum merge para develop/main antes da aprovação.
