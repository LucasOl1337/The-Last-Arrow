# The Last Arrow - Handoff Codex Prompt State Builder

Data/hora local: 2026-06-16 08:07 -03:00

## Contexto

Continuidade do handoff `DocsDev/2026-06-15-0848-codex-broker-request-lifecycle-handoff.md`.

Fatia escolhida: extrair a montagem de prompt do broker para um builder puro, reduzindo o peso de `CodexBrokerCombatantInputSource` sem alterar o comportamento observavel.

## Alterado nesta continuacao

- Adicionado `Assets/ProjectPVP/Scripts/Runtime/Input/CodexPromptStateBuilder.cs`.
- `CodexPromptStateBuilder` centraliza:
  - construcao de `CodexPromptState`;
  - mapeamento de `AiArenaCombatantObservation` para `CodexPromptCombatant`;
  - mapeamento de `AiArenaArenaObservation` para `CodexPromptArena`;
  - calculo de `CodexPromptProjectileThreat` com ETA limite;
  - geracao de eventos de contexto da rodada a partir de `snapshot` e `previousSnapshot`.
- `Assets/ProjectPVP/Scripts/Runtime/Input/CodexBrokerCombatantInputSource.cs` agora:
  - delega `BuildPromptState` ao novo builder;
  - reaproveita a fila `_eventMemory` apenas para registrar eventos retornados pelo builder.
- Adicionado `Assets/ProjectPVP/Tests/Editor/CodexPromptStateBuilderTests.cs` cobrindo:
  - defaults quando `snapshot`/campos estao ausentes;
  - copia de memoria e evento `round_context_initialized`;
  - filtragem de projeteis perigosos por ETA e distancia lateral.
- Criados `.meta` dos novos scripts Unity.

## Verificacoes

Passou:

- `git diff --check` nos arquivos tocados nesta fatia
- `rg -n "[ \t]+$"` nos arquivos tocados nesta fatia nao encontrou trailing whitespace
- `rg` confirmou que os metodos antigos de prompt (`AppendEvents`, `BuildPromptCombatant`, `BuildPromptArena`, `EstimateProjectileEta`) nao ficaram no broker
- `python -m pytest tools\tests -q` -> `17 passed`
- `python -m compileall -q mainbot.py tools`
- `codegraph sync .`
- `codegraph status --json .` apos sync -> `pendingChanges: added 0, modified 0, removed 0`
- `python tools\codegraphy_report.py --limit 8`

Bloqueado:

- Unity EditMode batchmode foi tentado com Unity `6000.3.11f1`.
- Nao gerou `Logs/codex-editmode-results.xml`.
- `Logs/codex-editmode.log` termina com `No valid Unity Editor license found. Please activate your license.`
- O processo retornou Unity exit code `198`.

## Estado CodeGraphy apos sync

- files: `171`
- nodes: `4249`
- edges: `7606`
- pendingChanges: `added 0`, `modified 0`, `removed 0`

## Proximo passo recomendado

`CodexBrokerCombatantInputSource` ainda concentra:

1. `BuildExecutorFeedback`
2. `ResolveControllerOwner`
3. `ApplyBrokerEnvelope`
4. `HandleBrokerRequestFailure`

Proxima fatia sugerida:

1. Extrair `BuildExecutorFeedback` e `ResolveControllerOwner` para um helper puro de envelope/feedback.
2. Se sobrar contexto, isolar `HandleBrokerRequestFailure` em um pequeno controlador de estado do broker.
3. Reexecutar Unity EditMode/PlayMode assim que a licenca local for resolvida.
