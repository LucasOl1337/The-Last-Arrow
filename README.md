# The Last Arrow

<p align="center">
  <img src="Assets/backg.png" alt="The Last Arrow current arena preview" width="100%">
</p>

Um prototipo de combate 2D em Unity pensado para crescer com foco em partidas online e locais.

a base ja esta de pe, os personagens ja tem identidade visual forte, o combate ja comeca a mostrar personalidade e agora o projeto tambem possui uma stack real de bots externos para playtest, analise e showcase.

Patch notes do projeto: [PatchNotes.md](PatchNotes.md)

## Ultimo patch notes

`v0.3.0 - 2026-03-30`

Resumo dele:

- unificacao da stack principal de bots em torno de `mainbot.py`
- suporte a bots separados no `slot 1` e `slot 2`
- `Bot Manager v1` com perfis persistentes, memoria privada, relatorios e base para geracoes
- overlays compactos por bot e painel documental local em `localhost:8050`
- fluxo de rounds `first to 5 kills` com HUD de bolinhas no topo

## O que ja temos de mais legal

- 2 personagens jogaveis com vibes bem diferentes
- combate base ja funcionando e pronto para evoluir
- tiro, melee, dash e ult no ar
- arena com identidade visual propria e espaco claro para evoluir o estilo
- bots externos com Codex controlando a luta por intencao tatica
- memoria por bot, relatorios por round/serie e observabilidade do loop Unity -> broker -> Codex
- uma base solida para seguir polindo sensacao, impacto e estilo
- um caminho claro para levar o projeto bem no online e no local

## Estrutura pensada para crescer

- `Scripts/Runtime` separado por responsabilidade, com blocos como `Core`, `Gameplay`, `Input`, `Match` e `Presentation`
- personagens organizados em pastas proprias com animacoes, dados e rotacoes, sem misturar tudo num lugar so
- area `Shared` para o que e comum entre personagens e `Resources` para centralizar o que precisa ser carregado
- uma base modular e simples de manter, que facilita ajustar mecanicas, adicionar conteudo novo e seguir evoluindo o projeto sem virar bagunca
- `tools/` agora concentra a stack operacional de bots: broker, agentes, memoria, menu, overlays e painel documental
- fluxo de branch paralelo com `git worktree`, documentado em `Docs/Git-Worktree-Workflow.md`

## Stack atual de bots

Hoje o projeto nao esta mais em um estado de prova de conceito simples de IA local. A arquitetura atual e:

- `Unity` captura o estado da luta e traduz para `promptState`
- `codex_broker.py` funciona como ponte e cache de sessao
- `codex_live_agent.py` conversa com o `codex.exe`
- `mainbot.py` supervisiona broker, agentes e janelas auxiliares
- `bot_manager.py` guarda roster, atribuicoes, perfis e configuracao por bot
- `codex_memory.py` registra round review, series review, plans e conhecimento reaproveitavel

Fluxo resumido:

`Unity -> broker -> live agent -> codex.exe -> broker -> Unity`

O modelo nao aperta botoes frame a frame. Ele devolve uma intencao tatica de curto prazo, e o runtime local executa essa intencao continuamente entre uma resposta e outra.

## Ferramentas operacionais desta versao

- `python tools\\bot_menu.py`
  - gerencia bots, slots, provider, modelo e validacao
- `python mainbot.py`
  - sobe a stack principal
  - abre o HUD tecnico
  - abre overlays compactos por slot
  - sobe o painel documental local
- `http://127.0.0.1:8050`
  - mostra overview da luta e trace da comunicacao com o Codex

## Estado jogavel atual

- rounds funcionam como `1 kill = 1 round`
- a partida usa `first to 5 kills`
- o topo da tela mostra cinco indicadores por lado
- o HUD deixa explicita a regra com `PRIMEIRO A 5 KILLS`
- bots podem lutar entre si em `slot 1` vs `slot 2` usando o mesmo broker e agentes separados

## Onde estao os arquivos mais importantes agora

- `mainbot.py`
- `tools/bot_menu.py`
- `tools/bot_manager.py`
- `tools/codex_broker.py`
- `tools/codex_live_agent.py`
- `tools/codex_memory.py`
- `tools/codex_report_console.py`
- `tools/codex_slot_console.py`
- `tools/codex_documentary_server.py`
- `Assets/ProjectPVP/Scripts/Runtime/Match/MatchController.cs`
- `Assets/ProjectPVP/Scripts/Runtime/Presentation/ProjectPvpRoundHudOverlay.cs`

## Personagens

<table>
  <tr>
    <td align="center" width="50%">
      <img src="Assets/ProjectPVP/Characters/Mizu/Animations/ult/east/frame_004.png" alt="Mizu" width="220">
      <br>
      <strong>Mizu</strong>
      <br>
      Samurai veloz que abusa de sua destreza e cortes rapidos no combate
    </td>
    <td align="center" width="50%">
      <img src="Assets/ProjectPVP/Characters/StormDragon/Animations/ult/west/frame_003.png" alt="Storm Dragon" width="220">
      <br>
      <strong>Storm Dragon</strong>
      <br>
      Artista marcial com elementos eletricos 
    </td>
  </tr>
</table>

## Agora a ideia e evoluir isso aqui

- deixar o combate ainda mais gostoso de jogar
- melhorar feedback visual, clareza e impacto
- empurrar mais a identidade de cada personagem
- usar os bots para revelar problemas reais de gameplay e evoluir o jogo com base em playtest automatizado
- transformar esse processo em material forte para video e documentacao

## Video rapido

Tambem gravei um video mostrando a estrutura do projeto, as mecanicas basicas e o caminho que quero seguir daqui pra frente.
