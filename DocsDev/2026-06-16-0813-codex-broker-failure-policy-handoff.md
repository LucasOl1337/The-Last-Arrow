# The Last Arrow - Handoff Codex Broker Failure Policy

Data/hora local: 2026-06-16 08:13 -03:00

## Contexto

Continuidade da limpeza do broker em `CodexBrokerCombatantInputSource`, agora com a politica de retry/falha separada em helper puro.

## Alterado nesta continuacao

- Adicionado `Assets/ProjectPVP/Scripts/Runtime/Input/CodexBrokerFailurePolicy.cs`.
- `CodexBrokerFailurePolicy` concentra a decisao de invalidação da sessao quando o broker falha.
- `Assets/ProjectPVP/Scripts/Runtime/Input/CodexBrokerCombatantInputSource.cs` agora:
  - incrementa o contador de falhas localmente;
  - delega a decisao de invalidar sessao ao novo helper.
- Adicionado `Assets/ProjectPVP/Tests/Editor/CodexBrokerFailurePolicyTests.cs` cobrindo:
  - ausencia de `sessionId`;
  - limite minimo de falhas;
  - janela de grace period apos sucesso recente;
  - invalidação quando o limite e a janela permitem.

## Verificacoes

Passou:

- `git diff --check` nos arquivos tocados nesta fatia
- `codegraph sync .`
- `codegraph status --json .` apos o sync -> `pendingChanges: added 0, modified 0, removed 0`
- `python -m pytest tools\\tests -q` -> `17 passed`

## Proximo passo recomendado

`CodexBrokerCombatantInputSource` ainda concentra a aplicacao do envelope do broker em `ApplyBrokerEnvelope`.

Proxima fatia sugerida:

1. Separar parsing do envelope e aplicacao de estado em um helper pequeno.
2. Cobrir os ramos de `sessionId`, `hasAgentAction` e `controllerOwner` com teste unitario.
3. Reavaliar se ja vale entrar em ajustes de jogabilidade propriamente dita no combate/janela de input.
