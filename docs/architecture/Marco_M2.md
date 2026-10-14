# Marco M2 — Varredura de metadados (somente leitura)

**Situação:** desenvolvido em feature/inventario-arquivos. Pendente de compilação, testes e aceite local. Nenhum merge para develop/main.

## Funcionalidades

1. Seleção de diretório raiz por FolderBrowserDialog; nenhuma pasta é varrida automaticamente.
2. Enumeração iterativa de arquivos e subdiretórios, com leitura apenas de metadados (caminho, tamanho, última modificação e último acesso em UTC).
3. Soma de quantidade e bytes em streaming. Exibição limitada a 1.000 arquivos e 100 ocorrências; totais continuam contando além desses limites.
4. Diretórios inacessíveis e erros de leitura são registrados como ocorrências parciais (sem prometer cobertura completa).
5. Reparse points, symlinks e junctions identificados são ignorados e registrados; o diretório raiz não pode ser um reparse point.
6. Botão Cancelar e trabalho em thread separada da interface.
7. Suite MSTest com testes de aplicação e integração que criam somente arquivos sintéticos em diretórios temporários.

## Arquitetura

- Application: InventariarDiretorio, ResultadoInventario, OcorrenciaInventario, ProgressoInventario e IFonteInventarioComOcorrencias.
- Infrastructure: FonteInventarioSistemaArquivos implementa IFonteInventario, com estado próprio por execução.
- Desktop: Form1 monta controles em código e chama o caso de uso; não contém lógica de enumeração.
- Domain: reutiliza ArquivoInventariado do M1.

Não adicionamos NuGet novo, SQLite, hash, classificação, exportação, quarentena, rollback ou ações de modificação.

## Limitações de segurança e resultados

- «Somente leitura» significa que o código não invoca escrita, renomeação, exclusão ou movimentação; o acesso a metadados pode estar sujeito ao comportamento do filesystem e do SO.
- Entradas podem mudar durante o processamento; contagens e datas são uma fotografia aproximada, não uma imagem transacional. Metadados mutáveis não são comprovados após a leitura.
- Erros em entradas/diretórios geram resultado marcado incompleto. Uma falha na enumeração de uma pasta interrompe a exploração do restante dessa pasta, mas permite continuar outras pastas.
- Não seguimos reparse points **encontrados durante a travessia**. Junctions presentes em componentes ancestrais do caminho raiz ainda exigem política e testes adicionais; não afirmar isolamento contra toda forma de redirecionamento.
- O limite de exibição é deliberado: não representa que apenas 1.000 arquivos foram analisados.
- A ordenação da amostra é a produzida pelo sistema de arquivos, sem garantia de estabilidade.
- Cancelar lança exceção e descarta o inventário parcial na interface. Não há mecanismo de retomada.
- Diretórios de sistema, perfis reais, compartilhamentos corporativos e dados produtivos estão fora dos testes autorizados neste marco.
- O suporte para Windows Server, UNC, caminhos longos, ACLs e outros casos especiais permanece pendente de teste.
- Não interpretar arquivos listados como candidatos a exclusão: M2 não classifica arquivos.

## Portão de aceite

No notebook Windows 11, efetuar restore, build, test, teste manual com pasta sintética vazia, pasta aninhada, arquivos de tamanhos conhecidos e cancelamento. Confirmar ausência de alterações nos arquivos. Validação no desktop Windows 10 fica pendente conforme autorização anterior.

Nenhum relatório de sucesso será considerado evidência antes de receber a saída real do Visual Studio.
