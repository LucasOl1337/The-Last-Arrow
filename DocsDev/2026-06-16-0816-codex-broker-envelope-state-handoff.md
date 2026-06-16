# The Last Arrow - Handoff Codex Broker Envelope State

Data/hora local: 2026-06-16 08:16 -03:00

## Contexto

Continuidade da limpeza de `CodexBrokerCombatantInputSource`, agora com a aplicacao do envelope do broker separada em um helper puro.

## Alterado nesta continuacao

- Adicionado `Assets/ProjectPVP/Scripts/Runtime/Input/CodexBrokerEnvelopeStateMapper.cs`.
- `CodexBrokerEnvelopeStateMapper` centraliza:
  - resolucao do `sessionId` final;
  - derivacao de `hasExecutableIntent`;
  - resolucao de `controllerOwner`;
  - preservacao do `intent` quando existe.
- `Assets/ProjectPVP/Scripts/Runtime/Input/CodexBrokerCombatantInputSource.cs` agora:
  - parseia o JSON do envelope localmente;
  - delega a aplicacao de estado ao novo mapper;
  - reduz a logica dentro de `ApplyBrokerEnvelope`.
- Adicionado `Assets/ProjectPVP/Tests/Editor/CodexBrokerEnvelopeStateMapperTests.cs` cobrindo:
  - uso do `sessionId` do envelope e do `intent` executavel;
  - preservacao do `sessionId` anterior quando o envelope nao traz um novo;
  - modo direto executavel mesmo com `hasAgentAction = false`.

## Verificacoes

Passou:

- `git diff --check` nos arquivos tocados nesta fatia
- `codegraph sync .`
- `codegraph status --json .` apos o sync -> `pendingChanges: added 0, modified 0, removed 0`
- `python -m pytest tools\\tests -q` -> `17 passed`

## Proximo passo recomendado

O broker ainda tem o parsing JSON inline dentro de `ApplyBrokerEnvelope`.

Proxima fatia sugerida:

1. Extrair um helper pequeno para parse seguro do envelope.
2. Cobrir o caso de JSON invalido e JSON vazio em teste unitario.
3. Depois disso, mudar o foco para um sistema de gameplay observavel, como combate, dash, salto ou projeteis, para aproximar o feel de Towerfall.
