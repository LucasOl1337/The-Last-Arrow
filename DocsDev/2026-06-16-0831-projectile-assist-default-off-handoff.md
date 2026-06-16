# The Last Arrow - Handoff Projectile Assist Default Off

Data/hora local: 2026-06-16 08:31 -03:00

## Contexto

Continuidade da melhoria de feel de combate. Esta fatia remove o assist padrao das flechas nos personagens jogaveis atuais, deixando o disparo mais proximo de um tiro puro de arena fighter.

## Alterado nesta continuacao

- `PlayerStatResolver.ResolveProjectileAssistEnabled()` agora retorna `false` quando nao ha `CharacterDefinition`.
- `DebugAimOverlay` agora usa `false` como fallback visual quando nao ha `CharacterDefinition`.
- `MizuDefinition.asset` e `StormDragonDefinition.asset` foram atualizados para:
  - `projectileAssistEnabled: 0`
- Adicionado `Assets/ProjectPVP/Tests/Editor/PlayerStatResolverTests.cs` cobrindo:
  - fallback padrao do resolver sem `CharacterDefinition`;
  - respeito ao valor explicitamente configurado no asset.

## Verificacoes

Passou:

- `git diff --check` nos arquivos tocados nesta fatia
- `python -m pytest tools\\tests -q` -> `17 passed`
- `codegraph sync .`

## Proximo passo recomendado

Com o assist desligado por padrao, o proximo ajuste de feel mais provavel e o arco da flecha.

Proxima fatia sugerida:

1. Subir a gravidade base do projétil ou tornar esse valor mais consistente com a escala dos personagens.
2. Revalidar a leitura do arco com um teste focado em `ProjectileController.ApplyDefinition`.
3. Depois disso, voltar a uma rodada de verificacao em Unity quando a licenca local estiver disponivel.
