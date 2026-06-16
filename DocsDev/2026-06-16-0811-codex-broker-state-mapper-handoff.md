# The Last Arrow - Handoff Codex Broker State Mapper

Data/hora local: 2026-06-16 08:11 -03:00

## Contexto

Continuidade do trabalho em `CodexBrokerCombatantInputSource`, seguindo a linha de extração de helpers puros para reduzir risco de regressao no fluxo do broker.

## Alterado nesta continuacao

- Adicionado `Assets/ProjectPVP/Scripts/Runtime/Input/CodexBrokerStateMapper.cs`.
- `CodexBrokerStateMapper` centraliza:
  - montagem de `CodexExecutorFeedback`;
  - resolucao de `controllerOwner` a partir do envelope e do modo de execucao.
- `Assets/ProjectPVP/Scripts/Runtime/Input/CodexBrokerCombatantInputSource.cs` agora:
  - delega `BuildExecutorFeedback` para o novo mapper;
  - delega `ResolveControllerOwner` para o novo mapper;
  - manteve `BuildReportedInput` local porque ele ainda e um helper pequeno e puro.
- Adicionado `Assets/ProjectPVP/Tests/Editor/CodexBrokerStateMapperTests.cs` cobrindo:
  - mapeamento completo de `CodexExecutorFeedback`;
  - defaults seguros quando `snapshot`/`intent`/`reportedInput` estao ausentes;
  - resolucao de `controllerOwner` em todos os ramos observaveis.

## Verificacoes

Passou:

- `git diff --check` nos arquivos tocados nesta fatia
- `codegraph sync .`
- `codegraph status --json .` apos o sync -> `pendingChanges: added 0, modified 0, removed 0`
- `python -m pytest tools\\tests -q` -> `17 passed`

## Proximo passo recomendado

`CodexBrokerCombatantInputSource` ainda concentra a politica de falha do broker em `HandleBrokerRequestFailure`.

Proxima fatia sugerida:

1. Extrair a politica de falha do broker para um helper pequeno e testavel.
2. Validar a transicao entre falhas consecutivas, janela de sucesso e invalidação de sessao.
3. Reexecutar Unity EditMode assim que a licenca local estiver disponivel.
