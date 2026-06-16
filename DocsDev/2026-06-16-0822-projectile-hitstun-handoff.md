# The Last Arrow - Handoff Projectile Hitstun

Data/hora local: 2026-06-16 08:22 -03:00

## Contexto

Continuidade da limpeza e tuning de combate. Esta fatia separa o hitstun de projectile do hitstun de melee, para que impactos a distancia tenham feel proprio e mais próximo de um arena fighter como Towerfall.

## Alterado nesta continuacao

- Adicionado `projectileHitstunDuration` em `Assets/ProjectPVP/Scripts/Runtime/Data/CharacterDefinition.cs`.
- `PlayerCombatSystem.ApplyProjectileHitReaction` agora usa `projectileHitstunDuration` em vez de reaproveitar `meleeHitstunDuration`.
- `Assets/ProjectPVP/Tests/Editor/PlayerCombatSystemTests.cs` agora cobre:
  - projectile hitstun e knockback sem kill direto;
  - projectile hitstun configurado explicitamente de forma diferente do melee;
  - default do novo campo via reflection para evitar regressao de schema.

## Verificacoes

Passou:

- `git diff --check` nos arquivos tocados nesta fatia
- `codegraph sync .`
- `codegraph status --json .` apos o sync -> `pendingChanges: added 0, modified 0, removed 0`
- `python -m pytest tools\\tests -q` -> `17 passed`

## Proximo passo recomendado

O combate continua com um stun unico para melee/ultimate via `meleeHitstunDuration`.

Proxima fatia sugerida:

1. Separar `ultimateHitstunDuration` se o feel precisar de mais leitura no impacto mais forte.
2. Revisar se `ProjectileController` merece um ajuste fino de gravidade/assist para combinar melhor com esse novo stun.
3. Depois disso, validar no Unity assim que a licenca local permitir.
