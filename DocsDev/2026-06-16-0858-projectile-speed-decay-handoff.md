# The Last Arrow - Handoff Projectile Speed Decay

Data/hora local: 2026-06-16 08:58 -03:00

## Contexto

Continuidade da tunagem de combate. Esta fatia removeu outro dado morto do projetil: `projectileSpeedDecay` e `projectileMinSpeed` agora entram no voo real da flecha, em vez de ficarem apenas serializados nos assets.

## Alterado nesta continuacao

- `Assets/ProjectPVP/Scripts/Runtime/Gameplay/ProjectileController.cs`
  - `baseSpeed` default alinhado para `1600f`.
  - `ApplyDefinition` agora copia:
    - `projectileMinSpeed`
    - `projectileSpeedDecay`
  - `FixedUpdate` agora chama `ApplySpeedDecay(dt)` apos a assist steer.
  - `ApplySpeedDecay()` reduz a velocidade do projetil em direcao ao piso minimo configurado.
  - `projectileUpwardSpeedDecayMultiplier` agora afeta o decay de velocidade ascendente, e nao a gravidade.
  - `ResolveGravityScale()` ficou restrito apenas ao peso/gravity.
- `Assets/ProjectPVP/Tests/Editor/ProjectileGravityTests.cs`
  - Adicionado teste cobrindo copia dos tunings de velocidade.
  - Adicionado teste cobrindo decay de velocidade, multiplicador de subida e piso minimo.
  - Atualizado o teste de gravidade para nao acoplar mais o multiplicador de speed decay ao calculo de gravidade.

## Verificacoes

Passou:

- `git diff --check`
- `python -m pytest tools\\tests -q` -> `17 passed`

## Proximo passo recomendado

Com a velocidade da flecha agora ativa, a proxima fatia util e playtest de feel:

1. Verificar se o decay de velocidade deixa os arcos mais legiveis sem quebrar tiros diagonais.
2. Ajustar `projectileSpeedDecay` e `projectileMinSpeed` nos dois personagens principais se a leitura ficar curta ou longa demais.
3. Depois disso, revisar se algum outro dado serializado em `CharacterDefinition` ainda nao tem efeito real no runtime.
