# M3 — Parte 3A: base do inventário de diretórios e perfis de compatibilidade

**Estado:** implementada em `feature/compatibilidade-base-m3-3a`, aguardando validação local do autor. Não integrada à `develop` nem à `main`.

## Decisões aprovadas pelo autor

- Um destino por execução, ou nenhum destino para filtros gerais;
- Primeiro escopo Azure Files clássico SMB/cliente Windows e SharePoint Online/OneDrive for Business; NFS e OneDrive pessoal fora;
- Sete verificações candidatas, cada uma habilitável individualmente;
- Filtro geral de tamanho personalizado independente dos limites oficiais e disponível sem destino; comparação estritamente maior que o limite;
- Não gastar com licenças; segurança do acervo, rastreabilidade e qualidade acima da velocidade.

Fonte de regras: relatório documental Work de 14/10/2026, com códigos estáveis `AZF-NOME-001`, `AZF-TAM-001`, `SPO-NOME-001` até `SPO-NOME-004` e `SPO-TAM-001`. Referências F01/F02/F14/F15/F19 constam no relatório, como documentação oficial do produto, não artigos científicos. **Nesta etapa o catálogo é somente metadados; não executa tais verificações nem certifica compatibilidade.**

## Alterações implementadas

### Inventário e segurança

- `EntradaInventariada` diferencia `Arquivo` e `Diretorio`, com tamanho apenas para arquivos.
- `IFonteInventarioComEntradas` amplia opcionalmente `IFonteInventario`. A fonte real percorre diretórios uma vez e emite ambos os tipos. A fonte antiga continua disponível para consumidores existentes.
- `InventariarDiretorio` usa a interface ampliada quando presente, sem refazer a varredura; em fontes legadas, a contagem de diretórios é zero (desconhecida para aquela fonte, não evidência de inexistência).
- `ResultadoInventario` acrescenta `QuantidadeDiretorios` e amostra limitada de subpastas, separadas da listagem de arquivos. A raiz selecionada não é contada.
- O fluxo WinForms mantém categorias gerais e só adiciona a quantidade de subpastas ao resumo. Nomes de pastas ainda não são avaliados por compatibilidade.
- Reparse points continuam não percorridos e registrados nas ocorrências; a ausência de falhas registradas não garante inventário transacional ou completo em todas as circunstâncias.

### Contratos futuros e resultados seguros

- `PerfilDestino`: único cenário Azure SMB ou subtipo explícito SPO/OneDrive for Business. `null` indica nenhum perfil.
- `CatalogoVerificacoes`: sete definições descritivas, ID estável e referência de documento. Não há algoritmo.
- `ConfiguracaoCompatibilidade`: congela seleção de checkboxes, bloqueia duplicatas, IDs desconhecidos, mistura de ambientes e regras selecionadas sem destino.
- `ResultadoVerificacao` / `ResumoVerificacoes`: estados explícitos. Nenhuma execução ou somente regras desativadas/inconclusivas **não** autoriza informar ausência de incompatibilidade.
- `IRegraCompatibilidade`: contrato para implementações futuras, ainda sem implementações.
- `FiltroTamanhoPersonalizado`: modelo puro independente, limite `long` positivo em bytes, sinaliza somente `tamanho > limite`. Não é limite oficial de serviço nem critério de remoção.

## Fora do escopo desta entrega

- Algoritmos das sete verificações, limites oficiais ou texto de incompatibilidade;
- Configurações de perfil e controle de tamanho na UI, relatório de compatibilidade, persistência e exportação;
- Projeção de caminhos remotos e regras Unicode não suficientemente fundamentadas;
- Hash, duplicatas, movimentação, exclusão, quarentena e rollback.

## Critérios de aceite

1. Build Release e suíte completa MSTest sem erros/avisos.
2. Pasta sintética: arquivo na raiz, arquivo em subpasta aninhada e subpasta vazia, com totais corretos.
3. Classificações gerais M3 mantêm comportamento aprovado.
4. Fonte legada que enumera somente arquivos continua compatível.
5. Testes unitários rejeitam mistura de destinos, ID inválido, ausência de perfil com regras ativas e valores inválidos de tamanho.
6. Testes verificam que nenhum sinal positivo é emitido por zero regras e por resultado inconclusivo.
7. Confirmar que os arquivos originais do cenário sintético continuam inalterados.

**Evidência de execução ainda pendente:** não há .NET SDK disponível no ambiente de autoria desta sessão. A execução real será feita pelo autor no notebook Windows 11, e no desktop quando possível.

## Decisão de arquitetura

Adicionamos apenas interfaces complementares à estrutura existente (sem novo pacote NuGet nem hierarquia Strategy artificial). Implementações das regras serão adicionadas no M3 Parte 3B e poderão reutilizar o contrato. Reversão em caso de falha: corrigir na mesma feature, sem merge até aceite humano.
