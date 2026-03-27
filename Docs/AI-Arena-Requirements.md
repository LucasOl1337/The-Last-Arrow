# AI Arena Requirements

## Objetivo

Criar um modulo de "ringue de AI" para que agentes diferentes consigam controlar os combatentes do jogo em tempo real e lutar entre si usando as mesmas regras de gameplay dos jogadores humanos.

O modulo deve nascer sem contaminar a base principal de input e sem criar excecoes por personagem.

## Estado atual do runtime

- O ponto oficial de entrada de input por combatente ja existe em `Assets/ProjectPVP/Scripts/Runtime/Input/ICombatantInputSource.cs`.
- O contrato de comandos ja existe em `Assets/ProjectPVP/Scripts/Runtime/Input/PlayerInputFrame.cs`.
- O `PlayerController` consome um frame de input por `FixedUpdate` e aplica movimento, dash, melee, tiro e ult no mesmo ciclo.
- O `MatchController` gerencia dois slots, spawn, respawn, rounds e vitorias.
- O `CharacterBootstrapFactory` cria combatentes runtime e hoje injeta `KeyboardPlayerInputSource` por padrao.
- O `INPUT_SOURCE_OF_TRUTH.txt` deixa explicito que o input e global, centralizado e nao pode virar uma regra por personagem.
- O `ProjectPvpDebugHud` ja expoe estado de input e combate, o que e util para telemetria do modulo.

## Requisitos de negocio

- O jogo deve suportar lutas AI vs AI no fluxo de partida atual.
- Cada slot do match deve poder ser controlado por uma origem diferente.
- O modulo deve aceitar pelo menos duas IAs distintas ao mesmo tempo, uma por slot.
- O comportamento da IA deve passar pelo mesmo contrato de input que o jogador humano usa hoje.
- O resultado da luta deve respeitar exatamente o gameplay atual do jogo.
- O modulo deve ser reutilizavel para bots locais, bots externos por processo e integracoes futuras.

## Requisitos funcionais obrigatorios

- Deve existir um novo tipo de `ICombatantInputSource` dedicado a AI.
- Cada slot deve ter um modo de controle explicito.
- Os modos minimos sao `Human`, `AI` e `Idle`.
- O modo de controle deve poder ser configurado por slot sem alterar `CharacterDefinition`.
- O slot controlado por AI deve continuar funcionando em respawn, reset de round e troca de personagem.
- O sistema deve permitir AI local embutida no Unity e AI externa conectada por ponte.
- O modulo deve receber o estado atual da luta e devolver uma acao por tick.
- O estado enviado para a AI deve ser o mesmo independentemente do personagem escolhido.
- A AI deve poder emitir todos os comandos do `PlayerInputFrame`.
- O sistema deve aceitar comandos de aim continuo, nao apenas oito direcoes fixas.
- O sistema deve tratar input `pressed` e input `held` como conceitos diferentes.
- O sistema deve definir comportamento quando a AI nao responder a tempo.
- O sistema deve definir comportamento quando a AI desconectar no meio da luta.
- O sistema deve permitir batalhas repetiveis para depuracao.
- O sistema deve expor logs suficientes para reproduzir uma luta e auditar decisoes.

## Contrato de simulacao

- O Unity continua sendo a autoridade do jogo.
- A simulacao de combate nao deve sair do `PlayerController`.
- A IA so pode sugerir input; ela nao pode alterar diretamente estado interno do personagem.
- A captura de input da AI deve acontecer no mesmo ponto em que o `PlayerController` hoje chama `CaptureFrame()`.
- O tick de AI deve ser sincronizado com `FixedUpdate`.
- O modulo deve usar um identificador monotonicamente crescente por frame de simulacao.
- O modulo deve diferenciar `match id`, `round id` e `simulation frame`.
- O sistema deve deixar claro se a AI esta respondendo para o frame atual ou para um frame atrasado.
- O jogo deve continuar rodando se uma AI ficar lenta; a luta nao pode travar a thread principal.

## Snapshot minimo obrigatorio para a AI

- `match_id`
- `round_id`
- `simulation_frame`
- `fixed_delta_time`
- `slot_id` da propria AI
- `facing`
- `root_position`
- `velocity`
- `is_grounded`
- `is_touching_wall`
- `is_dead`
- `is_dashing`
- `is_melee_active`
- `is_ultimate_active`
- `is_hit_stunned`
- `is_knocked_back`
- `current_arrows`
- `aim_hold_direction`
- `current_input_frame` do proprio slot
- estado resumido do oponente com os mesmos campos observaveis
- lista de projetis ativos com posicao, velocidade, dono, estado e direcao
- limites da arena ou wrap bounds efetivos
- placar atual do round
- informacao de reset de round pendente

## Contrato de acao minimo obrigatorio

- `frame` alvo da acao
- `axis`
- `aim`
- `left`
- `right`
- `up`
- `down`
- `jump_pressed`
- `jump_held`
- `shoot_pressed`
- `shoot_held`
- `melee_pressed`
- `ultimate_pressed`
- `dash_primary_pressed`
- `dash_secondary_pressed`

## Regras de protocolo

- O protocolo precisa ser versionado desde o inicio.
- O payload precisa ser estavel e documentado.
- O protocolo precisa suportar serializacao simples, preferencialmente JSON no MVP.
- A ponte precisa deixar claro qual agente esta ligado a qual slot.
- O handshake precisa informar capacidades minimas do agente.
- O protocolo precisa prever timeout e erro de parse.
- O protocolo precisa prever resposta invalida ou incompleta.
- O protocolo precisa prever desconexao graciosa e desconexao abrupta.
- O protocolo precisa permitir extensao futura sem quebrar bots antigos.

## Requisitos de arquitetura

- O modulo precisa ficar isolado em uma area propria de runtime, sem espalhar socket e serializacao pelo `PlayerController`.
- A construcao do snapshot precisa ficar fora do input source para manter responsabilidades claras.
- A traducao `snapshot -> transporte -> acao -> PlayerInputFrame` precisa ter componentes separados.
- O sistema precisa suportar troca de backend de transporte.
- O MVP deve priorizar um transporte simples e local.
- O backend de transporte nao pode bloquear `FixedUpdate`.
- O modulo deve funcionar mesmo com `InputSystemCombatantInputSource.IsNativeInputSystemAvailable == false`.
- O modulo nao deve quebrar teclado e gamepad existentes.
- O modulo nao deve depender de assets de personagem para funcionar.
- O modulo nao deve introduzir regras especiais por Mizu ou Storm Dragon.
- O modulo deve continuar compativel com combatentes instanciados por `CharacterBootstrapFactory`.
- O modulo deve continuar compativel com slots existentes do `MatchController`.

## Requisitos de fairness

- As duas IAs devem receber snapshots com a mesma granularidade temporal.
- Nenhuma AI pode receber acesso privilegiado a estado interno que a outra nao recebe.
- O sistema deve definir se o modelo de observacao sera estado completo ou observacao parcial.
- O sistema deve fixar essa decisao no contrato, nao por bot.
- A ordem de processamento das IAs nao pode favorecer sistematicamente um slot.
- O timeout deve ser igual para todos os agentes no mesmo modo de partida.
- O fallback de timeout deve ser igual para todos os agentes.

## Requisitos de resiliencia

- O jogo deve sobreviver a resposta atrasada.
- O jogo deve sobreviver a ausencia total de resposta.
- O jogo deve sobreviver a JSON invalido.
- O jogo deve sobreviver a valores fora de faixa.
- O jogo deve sobreviver a agente que envia mais de uma resposta para o mesmo frame.
- O jogo deve sobreviver a reconexao entre rounds.
- O sistema deve poder colocar o slot em `Idle` automaticamente se a AI falhar repetidamente.

## Requisitos de observabilidade

- O HUD de debug precisa mostrar a origem de controle de cada slot.
- O HUD de debug precisa mostrar status da conexao da AI.
- O HUD de debug precisa mostrar latencia por frame.
- O HUD de debug precisa mostrar quantidade de timeouts.
- O HUD de debug precisa mostrar ultimo comando recebido.
- O sistema precisa registrar snapshot enviado e acao aplicada por frame.
- O sistema precisa permitir salvar replay ou log de sessao para comparacao entre agentes.

## Requisitos de configuracao

- Deve existir configuracao por slot para escolher humano, AI ou idle.
- Deve existir configuracao para endpoint do agente quando o backend for externo.
- Deve existir configuracao para timeout maximo por frame.
- Deve existir configuracao para politica de fallback em timeout.
- Deve existir configuracao para habilitar ou desabilitar logs detalhados.
- Deve existir configuracao para escolher o backend da AI.

## Requisitos de UX e fluxo

- O usuario deve conseguir iniciar uma luta AI vs AI sem editar codigo.
- O fluxo minimo pode nascer como configuracao em inspector e debug scene.
- O sistema deve mostrar claramente quando um slot esta controlado por AI.
- O sistema deve deixar claro quando a luta esta em modo experimental ou debug.

## Requisitos de testes

- Teste de unidade para traducao de snapshot.
- Teste de unidade para parse de resposta da AI.
- Teste de unidade para clamp e saneamento de comandos.
- Teste de unidade para fallback em timeout.
- Teste de unidade para rebind da AI apos respawn e reset de round.
- Teste de unidade para compatibilidade com `MatchController.EnsureRuntimeCombatantsForConfiguredSlots()`.
- Teste para garantir que o contrato de input continua global e personagem-agnostico.
- Teste para garantir que teclado e gamepad continuam intactos quando o modulo de AI esta desligado.
- Teste para garantir que os dois slots podem operar com origens diferentes ao mesmo tempo.

## Requisitos de documentacao

- Documentar o contrato de snapshot.
- Documentar o contrato de acao.
- Documentar o ciclo de vida da conexao do agente.
- Documentar timeout, fallback e regras de fairness.
- Atualizar `INPUT_SOURCE_OF_TRUTH.txt` se o refactor alterar o caminho central de input.
- Documentar como rodar um bot de exemplo.

## Nao objetivos do MVP

- Nao e objetivo fazer netcode PvP completo.
- Nao e objetivo expor arena publica na internet no primeiro corte.
- Nao e objetivo depender de LLM remoto respondendo frame a frame.
- Nao e objetivo executar codigo arbitrario nao confiavel dentro do processo do jogo.
- Nao e objetivo criar balanceamento novo so para bots.
- Nao e objetivo criar excecoes por personagem.

## Decisoes de produto que ainda precisam ser fechadas

- O MVP sera apenas local no mesmo PC ou vai aceitar agentes em outra maquina na rede?
- O MVP precisa de Human vs AI, ou apenas AI vs AI?
- O modelo de observacao sera estado completo ou observacao parcial?
- O backend inicial sera `named pipe`, `TCP` ou `WebSocket`?
- O jogo deve esperar um frame pela resposta ou usar a ultima acao conhecida?
- O replay da luta entra no MVP ou fica para a fase seguinte?

## Recomendacao de corte para o MVP

- Suportar exatamente 2 slots.
- Manter Unity como simulador autoritativo.
- Criar `AI input source` por slot.
- Criar `snapshot builder` e `action parser` separados.
- Usar transporte local simples e assincrono.
- Expor configuracao por inspector no `MatchController` ou no slot.
- Adicionar um bot de exemplo deterministicamente simples para validar o loop.
- Adicionar HUD e logs minimos para depuracao.

## Riscos principais

- Bloqueio da thread principal por IO mal colocado.
- Latencia alta gerando luta inconsistente.
- Misturar regra de AI com regra de input humano e quebrar o contrato global.
- Acoplamento indevido ao personagem em vez de acoplamento ao slot.
- Falta de logging tornar impossivel depurar decisoes do agente.
- Definir snapshot grande demais e tornar o custo por frame desnecessario.
- Definir snapshot pequeno demais e impedir agentes competitivos de atuar.
