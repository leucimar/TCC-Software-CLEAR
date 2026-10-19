# Marco M3 — Parte 1: classificação explicável

**Situação:** VALIDADA no notebook Windows 11 em 14/10/2026 e integrada à develop via PR #4 (merge 3494d99). Validação no desktop Windows 10 pendente.

## Escopo

- ArquivoVazio: tamanho igual a zero bytes;
- CopiaBackupOuTemporario: prefixo ~$ ou terminações .tmp, .bak e .backup (case-insensitive para terminações);
- regras independentes IRegraClassificacao, justificativas, múltiplas categorias por arquivo;
- contagens completas e amostra limitada a 1.000 linhas.

**Categorias são indícios, não decisões de descarte.**

## Evidências de aceite

- Build Release sem erros e sem avisos;
- 45/45 testes automatizados aprovados após correção de MSTEST0044;
- teste sintético da interface confirmado pelo autor: 6 arquivos, 10 bytes, 5 com indícios, 2 vazios, 4 temporários/backups e zero ocorrências;
- arquivo vazio.bak apresentou duas categorias simultaneamente.

Na Parte 2, acrescentamos sinais temporais configuráveis e desligados por padrão. As decisões sobre compatibilidade por destino, duplicatas e ações modificadoras exigem marcos específicos e autorização.
