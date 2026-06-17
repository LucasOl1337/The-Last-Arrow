# Handoff - The Last Arrow rumo a TowerFall Ascension hitkill

Data e hora local: 2026-06-16 15:23:25 -03:00

## Tema principal

Continuidade do trabalho em The Last Arrow para aproximar o jogo de um clone/descendente direto de TowerFall Ascension, com foco em combate hitkill, sensacao de movimento, economia de flechas, comportamento de bots e testabilidade do loop de partida.

O usuario reforcou explicitamente que TowerFall Ascension e um jogo hitkill. Isso virou restricao central de design: qualquer ajuste de combate, feedback, bot ou balanceamento deve partir da ideia de que um acerto limpo mata, a rodada e curta, e a leitura do momento importa mais que dano acumulado.

## Objetivo central discutido

Levar The Last Arrow para mais perto de TowerFall Ascension em cinco frentes:

1. Combate hitkill claro, rapido e legivel.
2. Movimento com inercia e resposta suficientes para duelo tecnico.
3. Flechas como recurso limitado e decisivo, nao spam infinito.
4. Rodadas curtas, com morte, respawn/round flow e feedback fortes.
5. Bots capazes de jogar o proprio jogo, testar o loop e expor bugs.

Tambem houve permissao do usuario para testar o jogo "jogando" pelo sistema de bots. A fala "bogts" foi tratada como transcricao de voz para "bots". O usuario assumiu que esse sistema poderia estar quebrado e pediu para consertar quando necessario.

## Planejamento definido

O plano de evolucao para ficar mais TowerFall-like ficou nesta ordem:

1. Consolidar a regra hitkill e revisar qualquer sistema que ainda pareca HP/hitstun como regra principal.
2. Melhorar momentum de flechas, com heranca de velocidade do jogador.
3. Balancear personagens como arquipos bem diferentes, sem quebrar a leitura de duelo.
4. Validar economia de flechas: capacidade baixa, recuperacao relevante, sem assistencia excessiva.
5. Fortalecer bots para playtest automatico e tomada de decisao em vantagem/desvantagem de flechas.
6. Testar em engine, observar partidas reais ou simuladas e ajustar feel.
7. Polir feedback visual/sonoro de morte, impacto, camera shake e round transition.

## Decisoes de design registradas

- Hitkill e uma decisao de base, nao um detalhe: um acerto bem sucedido deve matar.
- O jogo deve punir desperdicio de flechas e recompensar posicionamento.
- Assistencia de projeteis deve ficar desligada por padrao para manter leitura competitiva.
- Flechas devem herdar momentum do jogador para dar profundidade a tiros em movimento.
- Mizu deve parecer mais rapido, tecnico e responsivo.
- StormDragon deve parecer mais pesado, lento e deliberado.
- Bots precisam ser bons o suficiente para servir de ferramenta de regressao/playtest, nao apenas placeholders.

## Alteracoes implementadas nesta linha de trabalho

### Balanceamento de personagens

Arquivo: `Assets/ProjectPVP/Characters/Mizu/Data/MizuDefinition.asset`

- `meleeCooldown: 0.4`
- `meleeDuration: 0.24`
- `runtimeMoveScale: 1.14`
- `runtimeJumpScale: 1.1`
- `runtimeGravityScale: 0.92`
- `runtimeDashScale: 1.1`
- `projectileAssistEnabled` permanece desligado.
- `projectileInheritVelocityFactor: 1`
- Duracoes de acoes ajustadas:
  - dash: `0.24`
  - shoot: `0.15`
  - melee: `0.24`
  - death: `0.33333334`

Arquivo: `Assets/ProjectPVP/Characters/StormDragon/Data/StormDragonDefinition.asset`

- `meleeCooldown: 0.5`
- `meleeDuration: 0.34`
- `runtimeMoveScale: 0.86`
- `runtimeJumpScale: 0.9`
- `runtimeGravityScale: 1.08`
- `runtimeDashScale: 0.9`
- `projectileAssistEnabled` permanece desligado.
- `projectileInheritVelocityFactor: 0.5`
- Duracoes de acoes ajustadas:
  - dash: `0.34`
  - shoot: `0.2`
  - melee: `0.34`
  - death principal: `0.5`

Resultado pretendido:

- Mizu deve vencer em cadencia, velocidade, salto, dash e heranca de momentum.
- StormDragon deve ser mais lento, com maior compromisso em melee/dash/tiro.

### Momentum de flechas

Arquivo: `Assets/ProjectPVP/Scripts/Runtime/Data/CharacterDefinition.cs`

- O default de `projectileInheritVelocityFactor` passou para `1f`.

Arquivo: `Assets/ProjectPVP/Scripts/Runtime/Gameplay/PlayerStatResolver.cs`

- `ResolveProjectileInheritVelocityFactor()` agora retorna `1f` quando nao ha `CharacterDefinition`.

Arquivo: `Assets/ProjectPVP/Scripts/Runtime/Gameplay/ProjectileController.cs`

- A logica de `Launch()` deixou de herdar apenas a projecao positiva da velocidade na direcao do tiro.
- Agora a velocidade final soma o vetor inteiro herdado:

```csharp
_velocity = _launchDirection * baseSpeed + inheritedVelocity * inheritFactor;
```

Isso permite que tiros carreguem velocidade horizontal e vertical do jogador, inclusive queda/subida, aproximando o feel de jogos de arena platformer com arco.

### Camera shake e bootstrap

Arquivo movido/criado:

- De: `Assets/ProjectPVP/Scripts/Runtime/Presentation/ProjectPvpCameraShake.cs`
- Para: `Assets/ProjectPVP/Scripts/Runtime/Core/ProjectPvpCameraShake.cs`

Observacoes:

- O namespace permaneceu `ProjectPVP.Presentation` para evitar churn desnecessario em chamadas existentes.
- `ProjectPvpCameraShake` e usado por `PlayerController.TryKill`.
- `PlayerController` dispara flash de morte e `ProjectPvpCameraShake.TryShakeDefault(0.08f, 0.12f)`.

Arquivos de assembly:

- `Assets/ProjectPVP/Scripts/Runtime/Gameplay/ProjectPVP.Gameplay.asmdef`
  - adicionada referencia a `ProjectPVP.Core`.
- `Assets/ProjectPVP/Scripts/Runtime/Core/ProjectPVP.Core.asmdef`
  - adicionada referencia a `UnityEngine.AudioModule`, necessaria por `AudioListener`.

Motivo:

- `PlayerController` depende de camera shake. Deixar a classe em Presentation criava acoplamento ruim para Gameplay. Mover para Core reduziu risco de assembly quebrado sem mexer em todas as chamadas.

### Testes adicionados ou ajustados

Arquivo: `Assets/ProjectPVP/Tests/Editor/ProjectPvpRuntimeBootstrapTests.cs`

- Novo teste `Awake_EnsuresAudioListenerAndCameraShakeOnAvailableCamera`.
- Garante que o bootstrap adiciona `AudioListener` e `ProjectPvpCameraShake` na camera disponivel.

Arquivo: `Assets/ProjectPVP/Tests/Editor/ProjectileGravityTests.cs`

- Novo teste `Launch_InheritsFullVelocityVectorWhenFactorIsOne`.
- Caso esperado: tiro para direita com `baseSpeed=1600`, velocidade herdada `(220,80)` e fator `1` vira `(1820,80)`.

Arquivo: `Assets/ProjectPVP/Tests/Editor/PlayerCombatSystemTests.cs`

- Em `FireHeldShot_UsesRawEightDirectionLaunchWithLightAssist`, `definition.projectileInheritVelocityFactor = 0f` foi fixado explicitamente para preservar o teste de direcao bruta depois do novo default global `1f`.

Arquivo: `Assets/ProjectPVP/Tests/Editor/PlayerStatResolverTests.cs`

- Teste de perfis Mizu/StormDragon foi expandido para garantir diferencas de movimento, melee, cooldown, duracoes de acao, morte e heranca de momentum.
- `ResolveProjectileInheritVelocityFactor_ReturnsTowerFallLikeDefaultWhenCharacterDefinitionIsMissing` agora espera `1f`.

Arquivo: `Assets/ProjectPVP/Tests/Editor/PlayerControllerCharacterDefinitionTests.cs`

- Teste de assets agora valida:
  - ambos com `maxArrows == 3`;
  - assistencia de projetil desligada;
  - Mizu com heranca `1f`;
  - StormDragon com heranca `0.5f`.
- Novas `CharacterDefinition` tambem devem defaultar para heranca `1f`.

### Sistema de bots e fallback local

Trabalho anterior relevante desta sessao:

Arquivo: `tools/codex_live_agent.py`

- Foi introduzido fallback heuristico quando `codex.exe` nao existe ou nao pode ser usado.
- `resolve_runtime_provider()` decide o provedor de runtime.
- Quando o provider configurado e Codex mas o executavel esta ausente, o agente cai para `heuristic`.
- Heartbeat passa a reportar `local-heuristic`.
- A heuristica gera intents localmente para manter o fluxo de bots funcionando.

Arquivo: `tools/tests/test_codex_live_agent.py`

- Testes cobrem resolucao de provider, nome de modelo, punish intent e evasao de projetil.

Verificacao anterior:

- Fluxo broker + live agent fallback + session start + state updates foi simulado.
- Foram recebidas actions com `model=local-heuristic`.
- Foi observado `agentActionCount=2`.

### Politica de IA

Arquivos envolvidos em alteracoes anteriores:

- `Assets/ProjectPVP/Scripts/Runtime/Input/AiArenaHeuristicPolicy.cs`
- `Assets/ProjectPVP/Scripts/Runtime/Input/AiArenaStrategicPolicy.cs`
- `tools/codex_live_agent.py`
- testes relacionados

Direcao:

- Considerar pressao de "last arrow".
- Distinguir vantagem/desvantagem de flechas.
- Priorizar punicao de alvo vulneravel antes de pressao generica.

## Verificacoes feitas

Verificacoes relatadas durante a sessao:

- Testes Python de ferramentas passaram anteriormente com `pytest` na pasta `tools/tests`.
- Em um ponto anterior foi registrado `31 passed`; no handoff de `DocsDev/2026-06-16-0918-codex-live-agent-heuristic-fallback-handoff.md`, o estado documentado naquele momento falava em `21 passed`. Nao assumir numero atual sem reexecutar.
- Unity batchmode foi tentado e bloqueado por licenca:

```text
No valid Unity Editor license found. Please activate your license.
```

- Compilacoes locais por subconjunto foram feitas e passaram:
  - Core subset com `ProjectPvpRuntimeBootstrap.cs` + `ProjectPvpCameraShake.cs`.
  - Runtime subset com Data + Core + Input + Gameplay.
  - Bootstrap/test subset com `ProjectPvpRuntimeBootstrapTests.cs`.
  - Projectile/test subset com `ProjectileGravityTests.cs`.
- `git diff --check` foi executado em arquivos editados e indicou apenas avisos de LF -> CRLF em `.asmdef`, sem erro de conteudo.

Nesta etapa final de handoff nao foi reexecutada a suite de testes. A prioridade foi registrar contexto completo antes do encerramento da sessao.

## Pendencias

1. Rodar testes Unity completos quando houver licenca valida do Editor.
2. Fazer playtest real em engine do novo momentum de flecha.
3. Jogar/testar por bots depois das mudancas de momentum, porque a IA pode precisar mirar considerando velocidade herdada total.
4. Validar o balanceamento real Mizu vs StormDragon:
   - Mizu pode ter ficado forte demais por combinar velocidade e heranca `1f`.
   - StormDragon pode ficar muito lento se o tiro `0.2` e dash/melee `0.34` criarem compromisso excessivo.
5. Confirmar se todas as referencias Unity ficam saudaveis apos mover `ProjectPvpCameraShake`.
6. Gerar/validar `.meta` de novos arquivos no Unity.
7. Garantir que o bot stack continua funcionando no fluxo real, nao apenas em simulacao.
8. Revisar se algum sistema de hitstun/ultimate ainda contradiz a promessa hitkill.

## Riscos e pontos de atencao

- Movimento completo herdado na flecha pode gerar tiros mais rapidos ou com trajetorias inesperadas quando o jogador esta caindo/subindo.
- Bots podem subestimar tiros herdados verticalmente, desviando tarde ou mirando errado.
- Como `ProjectPvpCameraShake.cs` foi movido para Core sem preservar necessariamente um `.meta` antigo, prefabs/scenes que dependessem diretamente do GUID antigo poderiam perder referencia. O risco parece baixo porque o componente e adicionado dinamicamente, mas precisa ser validado no Unity.
- `ProjectPVP.Core.asmdef` agora referencia `UnityEngine.AudioModule`; isso resolveu o compile local do bootstrap, mas deve ser confirmado no Editor.
- O worktree esta muito sujo e contem varias mudancas preexistentes. O proximo agente nao deve reverter arquivos sem entender origem e escopo.
- Existem arquivos untracked de testes e docs. Nao assumir que tudo esta staged ou commitado.
- Assistencia de projetil esta desligada, o que e desejado para TowerFall-like, mas pode tornar bots fracos se a mira heuristica ainda nao for boa.

## Estado atual da documentacao

`DocsDev` ja existia antes deste handoff e e a pasta correta para documentacao de passagem de bastao.

Foi encontrada tambem uma pasta `Docs` com documentacao forte. Ela ja estava importada em:

- `DocsDev/ImportedDocs/Docs`

Arquivos presentes nessa importacao:

- `AI-Arena-Agent-Request.md`
- `Combat-Playtest-Checklist.md`
- `Game-Studio-Unity-Translation.md`
- `Git-Worktree-Workflow.md`
- `PixelLab-MCP-Workflow.md`
- `ReleaseNotes-v0.1.1.md`

Tambem ja havia importacao de documentos importantes da raiz em:

- `DocsDev/ImportedDocs/Root`

Durante este handoff foram copiados para `DocsDev/ImportedDocs/Root`, sem apagar os originais:

- `changelog.md`
- `patchnotes.md`

Motivo: ambos sao grandes e relevantes para historico de desenvolvimento, mas ainda nao apareciam na importacao de raiz.

## Arquivos-chave tocados ou relevantes

Gameplay e runtime:

- `Assets/ProjectPVP/Scripts/Runtime/Data/CharacterDefinition.cs`
- `Assets/ProjectPVP/Scripts/Runtime/Gameplay/PlayerStatResolver.cs`
- `Assets/ProjectPVP/Scripts/Runtime/Gameplay/ProjectileController.cs`
- `Assets/ProjectPVP/Scripts/Runtime/Gameplay/PlayerController.cs`
- `Assets/ProjectPVP/Scripts/Runtime/Core/ProjectPvpRuntimeBootstrap.cs`
- `Assets/ProjectPVP/Scripts/Runtime/Core/ProjectPvpCameraShake.cs`
- `Assets/ProjectPVP/Scripts/Runtime/Core/ProjectPVP.Core.asmdef`
- `Assets/ProjectPVP/Scripts/Runtime/Gameplay/ProjectPVP.Gameplay.asmdef`

Assets de personagens:

- `Assets/ProjectPVP/Characters/Mizu/Data/MizuDefinition.asset`
- `Assets/ProjectPVP/Characters/StormDragon/Data/StormDragonDefinition.asset`

IA e bots:

- `Assets/ProjectPVP/Scripts/Runtime/Input/AiArenaHeuristicPolicy.cs`
- `Assets/ProjectPVP/Scripts/Runtime/Input/AiArenaStrategicPolicy.cs`
- `Assets/ProjectPVP/Scripts/Runtime/Input/AiArenaFrameExecutor.cs`
- `Assets/ProjectPVP/Scripts/Runtime/Input/CodexBrokerStateMapper.cs`
- `tools/codex_live_agent.py`
- `tools/codex_broker.py`

Testes:

- `Assets/ProjectPVP/Tests/Editor/ProjectPvpRuntimeBootstrapTests.cs`
- `Assets/ProjectPVP/Tests/Editor/ProjectileGravityTests.cs`
- `Assets/ProjectPVP/Tests/Editor/PlayerCombatSystemTests.cs`
- `Assets/ProjectPVP/Tests/Editor/PlayerStatResolverTests.cs`
- `Assets/ProjectPVP/Tests/Editor/PlayerControllerCharacterDefinitionTests.cs`
- `tools/tests/test_codex_live_agent.py`
- `tools/tests/test_codex_bot_stack.py`

Documentacao:

- `DocsDev/2026-06-16-0918-codex-live-agent-heuristic-fallback-handoff.md`
- `DocsDev/ImportedDocs/Root/changelog.md`
- `DocsDev/ImportedDocs/Root/patchnotes.md`
- este arquivo

## Proximos passos recomendados

1. Reexecutar testes Python:

```powershell
python -m pytest C:\Projetos\The-Last-Arrow\tools\tests -q
```

2. Assim que a licenca Unity estiver ativa, rodar testes Editor/PlayMode relevantes:

```powershell
Unity.exe -batchmode -projectPath C:\Projetos\The-Last-Arrow -runTests
```

3. Testar uma partida Mizu vs StormDragon com bots:
   - confirmar se ambos atiram;
   - confirmar se recuperam/gerenciam flechas;
   - confirmar se morrem em um acerto;
   - confirmar se o round avanca;
   - observar se bots entendem last-arrow pressure.

4. Ajustar IA para momentum total:
   - prever a velocidade final da flecha como `launchDirection * baseSpeed + inheritedVelocity * inheritFactor`;
   - usar essa previsao tanto para mira quanto para evasao;
   - criar teste cobrindo tiro com componente vertical herdado.

5. Fazer uma rodada de tuning fino:
   - Mizu: validar se `projectileInheritVelocityFactor=1` e escalas altas nao geram dominancia.
   - StormDragon: validar se `projectileInheritVelocityFactor=0.5` ainda permite competir.
   - Conferir se melee ainda faz sentido em hitkill sem virar resposta universal.

6. Revisar loop hitkill:
   - remover/limitar qualquer comportamento que pareca vida acumulada;
   - confirmar feedback instantaneo de morte;
   - confirmar camera shake e flash;
   - confirmar que o vencedor da troca fica claro.

7. Abrir o projeto no Unity para gerar `.meta` dos novos arquivos e verificar referencias.

## Observacao final para o proximo agente

Nao comece refatorando em larga escala. O caminho mais eficiente agora e validar o jogo funcionando: testes de ferramentas, testes Unity quando possivel, e partidas bot vs bot/humano vs bot para observar feel. O maior risco tecnico imediato e a IA nao acompanhar o novo modelo de momentum total das flechas; o maior risco de design e esquecer que o jogo-alvo e hitkill.
