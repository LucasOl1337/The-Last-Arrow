# AI Arena Agent Request

Implemente a integracao do modulo de AI Arena no jogo atual, preservando o fluxo humano e o contrato de input existente.

Leia nesta ordem:

1. `INPUT_SOURCE_OF_TRUTH.txt`
2. `Docs/AI-Arena-Agent-Request.md`
3. `Docs/Git-Worktree-Workflow.md`

Objetivo:

- manter Unity como simulador autoritativo
- suportar por slot os modos `Human`, `AI` e `Idle`
- manter o `PlayerController` consumindo `PlayerInputFrame`
- permitir AI vs AI sem quebrar teclado ou gamepad
- manter a integracao desacoplada do `PlayerController`

Diretrizes obrigatorias:

- a IA deve entrar por `ICombatantInputSource`
- nao colocar logica de ponte, transporte ou parsing direto no `PlayerController`
- criar snapshot canonico e versionado
- criar camada semantica voltada para combate, nao apenas estado cru
- tratar timeout, parse invalido, desconexao e fallback seguro
- adicionar HUD/debug minimo e logs uteis para diagnostico
- adicionar pelo menos um bot simples para validar o loop completo
- adicionar testes de timeout, reset/respawn, parse invalido e regressao de input humano

Criterios de aceite:

- dois slots lutam em AI vs AI no fluxo atual de partida
- respawn, round reset e wrap continuam funcionando
- o jogo nao trava nem perde controle humano quando a IA falha
- o sistema continua personagem-agnostico
- o modulo pode ser desligado sem impacto no jogo humano

Se precisar cortar escopo para um MVP:

- 2 slots
- transporte local
- mensagens em JSON
- input final em `PlayerInputFrame`
- observabilidade minima ja no primeiro merge
