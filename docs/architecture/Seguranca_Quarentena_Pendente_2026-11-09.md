> Atualização de fechamento CLEARv1.0: este é um registro histórico da preparação. Os quatro testes de permissões, a regressão de 283 testes e 63 verificações de integração foram posteriormente executados e aprovados localmente. Ver VALIDACAO_ENTREGA_CLEARv1.0.md; AD/SMB remoto continuam não validados.

# Segurança NTFS na quarentena — implementação pendente de validação

Data: 09/11/2026. Estado: código implementado, compilação aprovada, testes NÃO executados. Complemento posterior ao commit `89dd7ff`; não deve ser apresentado como parte da versão já validada apenas porque compila.

## Escopo autorizado

Somente a segurança das pastas/arquivos da quarentena e da restauração: DACL (permissões de permitir/negar), proprietário e grupo primário do descritor. Identidades permanecem referenciadas pelos mesmos SIDs, sem criar contas ou consultar/modificar o AD. Nenhuma configuração de compartilhamento SMB é criada, copiada ou alterada; compartilhamento continua manual, conforme pedido do usuário. A SACL de auditoria, rótulos de integridade e outras políticas avançadas não são certificados por esta alteração.

## Antes e depois

Antes, a hierarquia era recriada com `Directory.CreateDirectory`, herdando a segurança do destino. A movimentação de arquivo no mesmo volume dependia da preservação oferecida pelo Windows, sem comparação explícita de segurança. A preservação de último acesso implementada anteriormente trata de timestamps, não da DACL.

Agora `PreservacaoPermissoesQuarentena`:

1. Obtém a segurança efetiva de cada pasta de origem, incluindo regras herdadas e explícitas.
2. Recria a árvore abaixo da raiz de quarentena com DACL protegida já no ato de criação, preservando os SIDs, máscaras, regras, proprietário e grupo. Converte regras herdadas em explícitas na cópia para impedir que a segurança mais aberta da nova raiz substitua a original. Não escreve na DACL das pastas originais.
3. Exige que pastas de destino já existentes tenham segurança compatível e herança protegida. Não reconfigura permissões existentes silenciosamente: em caso de divergência, recusa o envio e orienta escolher quarentena vazia/compatível. Isso pode bloquear quarentenas criadas pela versão anterior, intencionalmente.
4. Confere novamente as pastas e o arquivo antes de mover. Depois da movimentação, compara a segurança do arquivo/diretórios antes de confirmar `EmQuarentena`. Registra a impressão SHA-256 do descritor normalizado na mensagem da auditoria.
5. Na restauração, confere a segurança do arquivo antes/depois sem recriar nem reconfigurar as pastas originais.

Normalização: a comparação converte herança em regras explícitas para comparar direitos/SIDs, proprietário e grupo, ignorando apenas os carimbos de auto-herança do Windows. A proteção contra herança das pastas de destino é exigida separadamente. Não afirma manter todos os indicadores binários da origem idênticos: o objetivo é conservar a política capturada sem receber concessões novas do destino.

Sem permissão para ler/criar/aplicar os descritores, a operação é interrompida. Não há fallback para criar diretórios com ACL padrão aberta, tomar posse, habilitar privilégios ou modificar usuários/grupos. Preservar proprietário/grupo de outra identidade pode exigir privilégios que uma simples execução elevada não garante.

## Falhas e limites

- Erro antes do move deixa o arquivo original no lugar e a auditoria como falha. Podem restar diretórios vazios já criados com ACL restritiva.
- Divergência descoberta depois do move deixa o registro em estado pendente, sem alegar sucesso. Não faz rollback automático, sobrescrita ou correção arbitrária de ACL. É necessária revisão da auditoria.
- ACLs obtidas por caminho são snapshots. Não há transação entre alterações administrativas externas de ACL, criação de diretórios e rename. As verificações reduzem o risco, mas não provam ausência de corrida nem exposição zero sob alterações concorrentes. Falha de leitura pós-move pode impedir determinar o estado do arquivo sem intervenção administrativa.
- A segurança da raiz externa de quarentena que já existe e de seus ancestrais não é alterada. A política da árvore espelhada é protegida; nomes de diretórios ancestrais continuam sujeitos às regras existentes do local escolhido.
- Não é uma validação de permissões efetivas de três logins de domínio, de grupos aninhados/trusts, privilégios de backup, acesso ao SQLite de auditoria ou aplicação de políticas do servidor. Nem é uma validação de links entre planilhas. O pedido posterior restringiu o trabalho atual à Segurança; vínculos de planilhas continuam não testados.
- Nenhuma VM foi iniciada, nenhum domínio foi modificado, nenhuma conta foi criada e nenhum teste de permissões foi executado no computador do usuário. Não foi usada base de 100 GB nem validação controlada nesta alteração.

## Verificação realizada

Somente `dotnet build` do projeto de testes Infrastructure, numa cópia isolada dos fontes. Resultado: zero erros e seis avisos MSTEST0037 já existentes. Compilar testes não significa executá-los.

Quatro testes foram escritos para a próxima etapa: descritores/SIDs preservados em pasta recriada; recusa de pasta existente com herança aberta sem alterar sua ACL; mudança de ACL durante preparação; restauração sem alterar segurança das pastas originais. Usam arquivos descartáveis e SID sintético, não autenticam usuários AD. Ainda NÃO EXECUTADOS, assim como a regressão da aplicação após esta alteração.

## Entrega local

Fontes: `src/Clear.Infrastructure/Quarentena/PreservacaoPermissoesQuarentena.cs`, integração em `ServicoQuarentenaM7.cs` e `ServicoRestauracaoM8.cs`; testes em `tests/Clear.Infrastructure.Tests/PreservacaoPermissoesQuarentenaTests.cs`.

O atalho e o executável da primeira versão já conferida permanecem na compilação `artifacts/conferencia_2026-11-09`. Não foram substituídos pela implementação de segurança ainda não testada. Nenhum novo commit/push foi realizado nesta etapa.
