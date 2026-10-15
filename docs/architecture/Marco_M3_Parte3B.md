# M3 — Parte 3B: avaliação local por destino e filtro de tamanho

**Estado:** feature `feature/compatibilidade-regras-m3-3b`, aguardando validação no notebook. Não integrar a `develop` ou `main` sem aprovação explícita.

## Decisões aprovadas

- Destino único por execução: Azure Files clássico (Microsoft.Storage/SMB/Windows) **ou** SharePoint Online/OneDrive for Business. Também é possível escolher **nenhum**, mantendo apenas os filtros gerais.
- Sete verificações oficiais podem ser ligadas ou desligadas individualmente. A interface usa opções exclusivas para perfis e checkboxes para verificações.
- Filtro **operacional** de tamanho personalizado, independente da seleção de destino ou do limite oficial do serviço. Nenhum achado autoriza descarte, movimentação ou migração.
- A data temporal opcional, arquivos vazios e possíveis temporários/backups permanecem independentes.

## Catálogo M3-3B-1 e fontes documentais

O catálogo traz códigos estáveis, texto explicativo e referência ao relatório Work de compatibilidade de 14/10/2026. As fontes são **documentação de produto da Microsoft, não artigos científicos**. Estas regras se aplicam ao recorte específico acima, não a NFS, OneDrive pessoal nem a quaisquer políticas empresariais remotas.

| Código | Regra | Origem documental | Critério |
|---|---|---|---|
| AZF-NOME-001 | Pontuação proibida no componente | F01 | Presença literal de um dos nove caracteres documentados |
| AZF-TAM-001 | Limite de arquivo Azure Files | F02 | Tamanho lógico **maior que** 4 TiB = 4.398.046.511.104 bytes |
| SPO-NOME-001 | Pontuação proibida no componente | F14 | Os nove caracteres documentados |
| SPO-NOME-002 | Espaço ASCII inicial | F14 | U+0020 no primeiro caractere |
| SPO-NOME-003 | Espaço ASCII final | F14 | U+0020 no último caractere |
| SPO-NOME-004 | Sequência reservada `_vti_` | F14/F19 | Sequência literal, comparada sem distinção de maiúsculas/minúsculas (hipótese de caixa explicitada no relatório) |
| SPO-TAM-001 | Limite de arquivo empresarial | F15 | Tamanho lógico **maior que** 250 × 2³⁰ = 268.435.456.000 bytes |

Fontes:
- F01: https://learn.microsoft.com/en-us/rest/api/storageservices/naming-and-referencing-shares--directories--files--and-metadata
- F02: https://learn.microsoft.com/en-us/azure/storage/files/storage-files-scale-targets
- F14: https://support.microsoft.com/en-us/onedrive/restrictions-and-limitations-in-onedrive-and-sharepoint
- F15: https://learn.microsoft.com/en-us/office365/servicedescriptions/sharepoint-online-service-description/sharepoint-online-limits
- F19: https://learn.microsoft.com/en-us/onedrive/developer/rest-api/concepts/case-sensitivity?view=odsp-graph-online

**Importante:** os limites são do serviço no escopo documental, não de cliente Windows, sincronização, API ou ferramenta de transferência. Não aplicamos blacklist geral Unicode e não rejeitamos genericamente acentos, emojis, `#` ou `%`. A implementação não interpreta nomes locais como URLs.

## Arquitetura de execução

- **Domain:** `AvaliadorCompatibilidade` avalia regras sem acessar disco, por entrada de arquivo ou pasta. Uma regra desmarcada resulta `Desabilitada`; tamanho em pasta, `NaoAplicavel`. `NaoAvaliado` permanece no contrato para dados incertos de futuras regras.
- **Application:** `InventariarDiretorio` faz uma só travessia, classifica todas as entradas e agrega contagens por regra e por estado; conserva somente amostra de até 1.000 entradas para a tabela. Em paralelo, mantém as classificações gerais anteriores e conta arquivos acima do limite personalizado separadamente.
- **Infrastructure:** fonte existente de arquivos e subpastas, preservada; sem leitura de conteúdo nem autenticação.
- **Desktop:** opções exclusivas de perfil; subtipo empresarial; checkboxes por regra; filtro geral de tamanho com B, KiB, MiB, GiB, TiB; feedback de motivo e estado. Quando nenhum destino é selecionado, compatibilidade não é avaliada.
- O tamanho personalizado usa `decimal.Ceiling(valor × unidade)` e converte o resultado para bytes inteiros antes de aplicar comparação **estritamente maior**. Isso evita arredondar o limiar para baixo.

## Interpretação, privacidade e limitações

- `IncompatibilidadeDocumentada`: há violação identificada para uma regra habilitada no cenário. Não implica que uma ferramenta de migração não tenha tratamento próprio.
- `SemIncompatibilidadeDetectada`: apenas aquela regra local não identificou a condição, não é certificação de migração.
- `Desabilitada`, `NaoAplicavel`, `NaoAvaliado` não são sucessos. Zero regras habilitadas nunca vira indicação favorável.
- A visão geral distingue indícios de saneamento, itens com incompatibilidade documentada e arquivos acima do limite pessoal. O mesmo arquivo pode participar de várias contagens.
- A amostra é limitada; os totais são calculados em toda a enumeração bem-sucedida. Erros, cancelamento e mudanças concorrentes podem impedir conclusão integral.
- O nome de cada subpasta descoberta é avaliado, mas **ancestrais não enumerados acima da raiz não são projetados e não são avaliados**, pois limites/composição de caminho ficam para etapa posterior.
- Sem investigação de itens já existentes no destino, permissões, quota, versões de API, sincronização, composição remota de caminho, Unicode além das condições literais ou limites de comprimento/profundidade. A consulta atual não afirma que o acervo é migrável.
- Sem CSV, SQLite, telemetria, coleta remota, licenças pagas ou operação destrutiva.

## Portão de aceite

- Build Release sem avisos nem erros e suíte MSTest completa.
- Testes sintéticos das sete regras (inclusive tamanho exato e limite+1 sem arquivos gigantes), cancelamento, classificação múltipla, perfil sem regras, perfil ausente, pastas e contagem após amostra.
- Testes manuais com pasta `_vti_`, arquivo de nome normal e filtro pessoal em bytes.
- Revisar estados e nomes truncados na interface; as justificativas completas estão disponíveis na tabela/tooltip.
- Testar somente em pasta sintética, sem documentos reais de faculdade/trabalho e sem qualquer tentativa de migrar.
- Não fazer merge antes do aceite explícito.
