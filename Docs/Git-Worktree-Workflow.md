# Git Worktree Workflow

## Objetivo

`git worktree` aqui serve para isolar trabalho por branch sem trocar o estado do diretorio principal. O repositorio continua sendo um monorepo Unity.

## Regras operacionais

- `main` fica no diretorio principal e deve permanecer limpa.
- Cada feature, fix ou spike ganha uma worktree propria.
- Branches novas devem usar prefixo `feature/`, `fix/` ou `spike/`.
- A criacao de worktree falha se a worktree atual estiver com mudancas pendentes.
- `Library/`, `Temp/`, `Logs/` e `UserSettings/` continuam isoladas por worktree.
- Evite misturar refactor de cena grande com mudancas extensas de gameplay na mesma branch.

## Script padrao

```powershell
.\tools\git-worktree.ps1 list
.\tools\git-worktree.ps1 create -Branch feature/player-hitstop
.\tools\git-worktree.ps1 open   -Branch feature/player-hitstop
.\tools\git-worktree.ps1 remove -Branch feature/player-hitstop
```

As worktrees sao criadas em uma pasta irma do repositorio:

```text
../The Last Arrow.worktrees/<branch-normalizada>
```

## Fluxo recomendado

1. Confirmar que `main` esta limpa com `git status --short`.
2. Criar a worktree da feature.
3. Abrir a nova pasta no editor ou IDE.
4. Trabalhar e commitar apenas naquela branch.
5. Atualizar a branch a partir de `main` quando necessario.
6. Remover a worktree ao finalizar.

## Estrutura de assemblies

- `ProjectPVP.Core`: bootstrap e infraestrutura minima.
- `ProjectPVP.Data`: ScriptableObjects e dados de runtime.
- `ProjectPVP.Input`: fontes de input e perfis/slots compartilhados.
- `ProjectPVP.Gameplay`: controle do jogador, combate, colisao, projetis e audio runtime.
- `ProjectPVP.Characters`: bootstrap de combatentes e catalogo de personagens.
- `ProjectPVP.Match`: orquestracao de partida e roster.
- `ProjectPVP.Presentation`: animacao, gizmos, HUD e video.

## Dependencias pretendidas

- `Data` nao depende de `Gameplay`.
- `Gameplay` depende de `Data` e `Input`.
- `Characters` depende de `Data`, `Gameplay`, `Input` e `Presentation`.
- `Match` depende de `Characters`, `Data`, `Gameplay` e `Input`.
- `Presentation` depende de `Data`, `Gameplay`, `Input` e `Match`.

## Ownership pratico

- `Gameplay`: regras de combate, movimento, colisao e projetis.
- `Input`: bindings, perfis e mapeamento por slot.
- `Presentation`: sprite animation, debug visual, video e gizmos.
- `Match`: spawn, roster, wrap e loop de rodada.
- `Characters` e `Assets/ProjectPVP/Characters`: conteudo e bootstrap de personagem.
