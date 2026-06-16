# The Last Arrow - Handoff Ultimate Hitstun

Data/hora local: 2026-06-16 08:25 -03:00

## Contexto

Continuidade da tunagem de combate. Esta fatia separa o hitstun do ultimate do hitstun de melee, para que o impacto mais forte tenha leitura e janela de resposta proprias.

## Alterado nesta continuacao

- Adicionado `ultimateHitstunDuration` em `Assets/ProjectPVP/Scripts/Runtime/Data/CharacterDefinition.cs`.
- `PlayerCombatSystem.ApplyUltimateDamageHits` agora usa `ultimateHitstunDuration` em vez de derivar o stun do melee.
- `Assets/ProjectPVP/Tests/Editor/PlayerCombatSystemTests.cs` agora cobre:
  - ultimate com stun proprio configurado via reflection;
  - diferenciação entre melee e ultimate no mesmo personagem;
  - knockback e ausencia de kill direto continuam validados.
- Atualizei os assets de `Mizu` e `StormDragon` para serializar explicitamente:
  - `meleeHitstunDuration`
  - `projectileHitstunDuration`
  - `ultimateHitstunDuration`
  - knockback defaults do combate

## Verificacoes

Passou:

- `git diff --check` nos arquivos tocados nesta fatia
- `python -m pytest tools\\tests -q` -> `17 passed`
- `codegraph sync .`
- `codegraph status --json .` apos o sync -> `pendingChanges: added 0, modified 0, removed 0`

## Proximo passo recomendado

`ProjectileController` ainda tem gravidade base e assist que podem ser revisitadas para aproximar o arco das flechas de uma leitura mais consistente.

Proxima fatia sugerida:

1. Revisar se `projectileGravity` e `projectileAssist` precisam de ajuste fino ou de valores por personagem.
2. Se o feel estiver aceitavel, sair do tuning fino e entrar em verificacao de runtime no Unity assim que a licenca permitir.
