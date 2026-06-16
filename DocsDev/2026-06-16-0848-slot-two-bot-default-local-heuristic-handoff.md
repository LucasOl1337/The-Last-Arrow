# The Last Arrow - Handoff Slot 2 Bot Default Local Heuristic

Data/hora local: 2026-06-16 08:48 -03:00

## Contexto

Continuidade da melhoria de jogabilidade com foco em bot de treino/partida. Esta fatia tira o fluxo de partida do caminho obrigatorio do CodexBroker e deixa o slot 2 jogavel por padrao com a IA local heuristica.

## Alterado nesta continuacao

- `Assets/ProjectPVP/Scripts/Runtime/Match/MatchController.cs`
  - `autoForceCodexBrokerForSlotTwoOnPlay` agora defaulta para `false`.
  - O fluxo de automacao de bot continua suportando `CodexBroker`, mas sem forcar esse caminho por padrao.
- `Assets/ProjectPVP/Scenes/Bootstrap.unity`
  - `slotTwoDebugAiBrain` foi ajustado para `LocalHeuristic` no bootstrap da cena.
- `Assets/ProjectPVP/Tests/Editor/MatchControllerRoundFlowTests.cs`
  - adicionado teste cobrindo o default do bot de slot 2:
    - nao forca `CodexBroker`;
    - usa `LocalHeuristic` por padrao;
    - a resolucao auto do brain continua consistente com o default.

## Verificacoes

Passou:

- `git diff --check`
- `python -m pytest tools\\tests -q` -> `17 passed`
- `codegraph sync .`
- `codegraph status --json .` apos o sync -> `pendingChanges: added 0, modified 0, removed 0`

Tentativa feita e bloqueada:

- `Unity.exe -batchmode -nographics -quit -projectPath C:\\Projetos\\The-Last-Arrow -runTests -testPlatform EditMode -testResults C:\\Projetos\\The-Last-Arrow\\Temp\\editmode-results.xml -logFile C:\\Projetos\\The-Last-Arrow\\Temp\\unity-editmode.log`
- Resultado: o editor iniciou, mas abortou com `No valid Unity Editor license found` e nao executou a suite.

## Proximo passo recomendado

Com o bot padrao destravado, a proxima verificacao util e um playtest real no Unity assim que a licenca local estiver disponivel.

Proxima fatia sugerida:

1. Confirmar no editor que o slot 2 entra em AI local sem depender de broker externo.
2. Observar se a heuristica local precisa de ajuste de agressividade, distancia ou prioridade de disparo.
3. Se ainda houver dependencia indesejada de broker em outros caminhos, remover o default ou adicionar fallback local nesses pontos tambem.
