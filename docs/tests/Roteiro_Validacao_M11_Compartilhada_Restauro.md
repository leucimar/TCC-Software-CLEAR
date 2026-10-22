# Roteiro M11 — Teste conjunto sem risco de dados

**Estado:** pendente no Windows. Contagem estimada de testes: 243 (237 anteriores + cinco novos de fluxo/consulta/limpeza + um de duplicatas). Esperar confirmação real.

## Comandos

    cd C:\Users\Leucimar\Projetos\CLEAR
    git status --short
    git pull --ff-only
    dotnet build CLEAR.slnx -c Release
    dotnet test CLEAR.slnx -c Release
    dotnet list CLEAR.slnx package --vulnerable --include-transitive

A branch local precisa ser feature/fluxo-quarentena-filtros-eta. Se git status listar alterações locais, não atualizar sem revisão.

## Segurança antes de abrir a nova versão

- Os cerca de 400 arquivos que já foram enviados para D:\Quarentena_SubBase_Dados_TCC permanecem fisicamente no formato antigo (GUID). NÃO movê-los manualmente.
- Faça uma cópia de segurança da pasta de quarentena e de %LOCALAPPDATA%\CLEAR\quarentena.sqlite3 com o CLEAR FECHADO, para permitir recuperação em caso de problema na migração do SQLite.
- Não testar quarentena, restauração ou limpeza em acervo público/empresarial importante sem aprovação de um roteiro específico. Primeiro utilizar C:\Temp\CLEAR_M11_Origem e C:\Temp\CLEAR_M11_Quarentena.

## Validação funcional

1. Montar cópias sintéticas em C:\Temp\CLEAR_M11_Origem\pasta01\pastaX01\RelatoriosPDF contendo pelo menos 3 PDFs, e uma pastaX04\PlanilhasFiscais com planilhas. Registrar hashes.
2. Analisar com histórico SQLite M6 habilitado, usar origem e quarentena no mesmo volume C:.
3. Selecionar 2 PDFs e 2 planilhas, confirmar sim. Verificar que os quatro arquivos estão em uma ÚNICA árvore C:\Temp\CLEAR_M11_Quarentena\CLEAR_M11_Origem\pasta01\... sem diretórios GUID, e que cada ID de operação permanece separado na auditoria.
4. Analisar/classificar no histórico e aplicar filtro Duplicata exata; confirmar que arquivos podem aparecer com SinalTemporal + DUPLICATA EXATA simultaneamente. O filtro de extensão pode ser combinado com duplicatas.
5. Na auditoria, abrir Filtrar / restaurar vários. Testar seleções individuais, paginação, Limpar seleção, Selecionar TODOS filtrados e NEGAR a confirmação (nenhum arquivo deve ser restaurado).
6. Autorizar 1–3 arquivos sintéticos. Confirmar conteúdo/hash, estado Restaurado, sem sobrescrita; pastas compartilhadas COM arquivos continuam existindo. Após restaurar último arquivo daquela pasta, verificar exclusão SOMENTE das pastas vazias.
7. Na aba Auditoria, confirmar botão Limpar pastas VAZIAS auditadas com quarentena sintética; não remover pasta raiz nem arquivos de outras pastas. Confirmar que negar a operação mantém as pastas.
8. Testar banco legado v1 e pastas GUID somente em ambiente sintético. O M8 deve restaurar e remover GUID vazio. Nenhum arquivo legado do usuário deve ser migrado automaticamente.
9. Registrar resultado do build, 243 testes, erros, auditoria NuGet, comparação de hashes e capturas de tela.

## Observação

O procedimento de limpeza sobre a quarentena já existente do usuário só deve ser considerado depois de todos os testes sintéticos e de backup da pasta/SQLite. Essa etapa não tem autorização automática.
