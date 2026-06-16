# The Last Arrow - Handoff Codex Broker Request Lifecycle

Data/hora local: 2026-06-15 08:48 -03:00

## Contexto

Continuidade do handoff `DocsDev/2026-06-15-0844-ai-arena-semantic-observation-builder-handoff.md`.

Fatia escolhida: com `AiArenaSnapshotBuilder` reduzido a orquestracao, iniciar a reducao de complexidade de `CodexBrokerCombatantInputSource` extraindo a mecanica pura de lifecycle de requests do broker.

## Alterado nesta continuacao

- Adicionado `Assets/ProjectPVP/Scripts/Runtime/Input/CodexBrokerRequestLifecycle.cs`.
- `CodexBrokerRequestLifecycle` centraliza:
  - inicio de request com versao incrementada;
  - completude apenas da versao corrente;
  - invalidacao com limpeza de estado e incremento de versao;
  - deteccao de request stale por janela em ms.
- Adicionado `CodexBrokerRequestLifecycleState` para armazenar:
  - `InFlight`;
  - `StartedTime`;
  - `Version`.
- `Assets/ProjectPVP/Scripts/Runtime/Input/CodexBrokerCombatantInputSource.cs` agora usa dois estados:
  - `_sessionStartRequest`;
  - `_strategyRequest`.
- Removidos os campos duplicados antigos de lifecycle:
  - `_sessionStartInFlight`;
  - `_strategyRequestInFlight`;
  - `_sessionStartRequestedTime`;
  - `_strategyRequestStartedTime`;
  - `_sessionStartRequestVersion`;
  - `_strategyRequestVersion`.
- Adicionado `Assets/ProjectPVP/Tests/Editor/CodexBrokerRequestLifecycleTests.cs` cobrindo:
  - `Begin` marcando request em voo e incrementando versao;
  - `TryComplete` limpando somente a versao corrente;
  - `Invalidate` impedindo completion de versao antiga;
  - `IsStale` exigindo request ativo e janela excedida.
- Criados `.meta` dos novos scripts Unity.

## Verificacoes

Passou:

- `git diff --check` nos arquivos tocados nesta fatia
- `rg -n "[ \t]+$"` nos arquivos tocados nesta fatia nao encontrou trailing whitespace
- `rg` confirmou ausencia dos campos antigos de lifecycle no broker
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

- files: `169`
- nodes: `4234`
- edges: `7570`
- pendingChanges: `added 0`, `modified 0`, `removed 0`

## Proximo passo recomendado

`CodexBrokerCombatantInputSource` ainda concentra prompt state, memoria/eventos, dangerous projectiles, parsing de envelope e fallback de sessao. Proxima fatia sugerida:

1. Extrair `BuildPromptState`, `BuildPromptCombatant`, `BuildPromptArena` e `EstimateProjectileEta` para um `CodexPromptStateBuilder`.
2. Preservar `AppendEvents`/`AddEvent` no broker por enquanto, ou passar eventos/memoria como argumentos para manter a fatia pequena.
3. Adicionar testes para:
   - combatant nulo gerando prompt combatant default;
   - arena nula/sem semantica gerando defaults;
   - dangerous projectiles filtrando por ETA ate `0.5s`.
4. Reexecutar Unity EditMode/PlayMode assim que a licenca local for resolvida.
