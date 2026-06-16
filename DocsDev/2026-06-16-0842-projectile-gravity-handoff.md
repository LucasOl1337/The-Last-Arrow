# The Last Arrow - Handoff Projectile Gravity

Data/hora local: 2026-06-16 08:42 -03:00

## Contexto

Continuidade da tunagem de combate. Esta fatia aproxima o arco das flechas da escala dos personagens, tira o projétil do valor antigo de gravidade e aplica atraso/rampa de gravidade para deixar a saida do disparo mais limpa.

## Alterado nesta continuacao

- `Assets/ProjectPVP/Scripts/Runtime/Data/CharacterDefinition.cs`
  - `projectileGravity` agora tem default `1500f`.
- `Assets/ProjectPVP/Scripts/Runtime/Gameplay/ProjectileController.cs`
  - `gravity` default ajustado para `1500f`.
  - `ApplyDefinition` agora copia os tunings de gravidade do projétil.
  - `FixedUpdate` passa a usar `ResolveGravityScale()`.
  - `ResolveGravityScale()` aplica:
    - atraso inicial por `projectileGravityDelayRatio`;
    - rampa ate `projectileGravityMaxScale`;
    - multiplicadores de subida quando o projétil ainda esta ascendendo.
- `Assets/ProjectPVP/Scripts/Runtime/Gameplay/PlayerCombatSystem.cs`
  - fallbacks de gravidade do arco alinhados para `1500f`.
- `Assets/ProjectPVP/Scripts/Runtime/Gameplay/DebugAimOverlay.cs`
  - fallback de gravidade do arco alinhado para `1500f`.
- Assets atualizados:
  - `Assets/ProjectPVP/Characters/Mizu/Data/MizuDefinition.asset`
  - `Assets/ProjectPVP/Characters/StormDragon/Data/StormDragonDefinition.asset`
  - ambos agora serializam `projectileGravity: 1500`.
- Adicionado `Assets/ProjectPVP/Tests/Editor/ProjectileGravityTests.cs` cobrindo:
  - copia dos tunings de gravidade em `ApplyDefinition`;
  - leitura de atraso, rampa e multiplicadores em `ResolveGravityScale()`.

## Verificacoes

Passou:

- `git diff --check`
- `python -m pytest tools\\tests -q` -> `17 passed`
- `codegraph sync .`
- `codegraph status --json .` apos o sync -> `pendingChanges: added 0, modified 0, removed 0`

## Proximo passo recomendado

Fazer uma passada de playtest focada em arco e leitura visual do disparo.

Proxima fatia sugerida:

1. Verificar no Unity se a gravidade `1500` nao deixou o disparo curto demais em diagonais altas.
2. Se necessario, ajustar apenas os multiplicadores `projectileGravityDelayRatio`, `projectileGravityRampRatio` e `projectileUpwardGravityMultiplier`.
3. Depois disso, revisar se `projectileSpeedDecay` / `projectileMinSpeed` merecem implementacao propria ou se devem ser removidos da definicao para evitar ruido futuro.
