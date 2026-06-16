# The Last Arrow - Handoff Codex Broker Heuristic Fallback

Data/hora local: 2026-06-16 08:52 -03:00

## Contexto

Continuidade da correcao do sistema de bots. O fluxo do `CodexBrokerCombatantInputSource` ainda podia cair em um estado de espera quando nao havia intent live ou reutilizavel. Isso deixava o bot parado em vez de continuar jogando, o que quebra a experiencia quando o broker externo nao esta disponivel.

## Alterado nesta continuacao

- `Assets/ProjectPVP/Scripts/Runtime/Input/CodexBrokerCombatantInputSource.cs`
  - Adicionado `ResolveDecision(AiArenaSnapshotEnvelope snapshot)`.
  - Quando existe intent live, continua usando `AiArenaStrategicPolicy`.
  - Quando a intent ainda e reaproveitavel, continua usando `AiArenaStrategicPolicy`.
  - Quando nao ha intent util, agora cai para `AiArenaHeuristicPolicy.Decide(snapshot)` em vez de retornar um frame de espera.
  - `_lastExecutorSource` passa a registrar `heuristic_fallback` nessa situacao.
- `Assets/ProjectPVP/Tests/Editor/AiArenaInputSourceTests.cs`
  - Adicionado teste cobrindo o fallback heuristico quando o broker nao tem intent live disponivel.

## Verificacoes

Passou:

- `git diff --check`
- `python -m pytest tools\\tests -q` -> `17 passed`
- `codegraph sync .`

## Proximo passo recomendado

Com o bot agora degradando de forma jogavel, a proxima melhoria util e observar o comportamento de combate no runtime real quando a licenca local do Unity estiver ativa.

Proxima fatia sugerida:

1. Validar no editor se o slot 2 entra em AI local quando o broker esta ausente.
2. Medir se a heuristica local precisa de tuning de agressividade ou dash.
3. Se ainda houver dependencia quebradiça do broker em algum caminho visual ou de input, remover essa dependencia ou instalar fallback parecido.
