# ContextAndAiGuide

Este diretorio e a memoria operacional do projeto para IA e handoff tecnico.

Objetivos:
- registrar contexto real do projeto sem depender da conversa atual
- manter um ciclo padrao para qualquer IA atualizar status, decisoes e proximos passos
- guardar pacotes de integracao em arquivos datados para merge posterior

Fluxo minimo:
1. Ler `CURRENT_CONTEXT.md`.
2. Ler a entrada mais recente em `Daily/`.
3. Criar uma nova entrada datada para o trabalho do dia.
4. Atualizar `CURRENT_CONTEXT.md` com o estado novo.
5. Se houver pacote de integracao, registrar em `Packages/`.

Arquivos principais:
- `CURRENT_CONTEXT.md`: estado mais recente e pontos de continuidade
- `AI-Update-Cycle.md`: protocolo obrigatorio para qualquer IA
- `INDEX.md`: indice cronologico das entradas
- `Templates/Daily-Update-Template.md`: molde para novos registros
- `Daily/`: registros datados do trabalho
- `Packages/`: pacotes de integracao, manifests e handoffs

Tooling:
- `..\..\tools\context-ai-guide.ps1 new-entry -Slug <slug> -Title <titulo>`
- `..\..\tools\context-ai-guide.ps1 refresh-current -EntryRelativePath 'Docs/ContextAndAiGuide/Daily/YYYY-MM-DD-slug.md'`
