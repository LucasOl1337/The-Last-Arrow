# 2026-03-26 ai arena and worktree isolation

## Goal
- isolar em uma worktree limpa o pacote tecnico de `AI Arena + modularizacao + workflow de worktree`

## Clean Worktree
- `C:\Users\user\Desktop\The Last Arrow.worktrees\codex-ai-ringue-module`

## Source Snapshot
- `C:\Users\user\Desktop\The Last Arrow`
- estado observado: `main` suja e `ahead 1` de `origin/main`

## Already Materialized Here
- `Docs/Git-Worktree-Workflow.md`
- `Docs/AI-Arena-Agent-Request.md`
- `tools/git-worktree.ps1`
- `Docs/ContextAndAiGuide/*`

## Pending Technical Delta To Port
- modularizacao por asmdef do runtime:
  - `Assets/ProjectPVP/Scripts/Runtime/Core/ProjectPVP.Core.asmdef`
  - `Assets/ProjectPVP/Scripts/Runtime/Data/ProjectPVP.Data.asmdef`
  - `Assets/ProjectPVP/Scripts/Runtime/Input/ProjectPVP.Input.asmdef`
  - `Assets/ProjectPVP/Scripts/Runtime/Gameplay/ProjectPVP.Gameplay.asmdef`
  - `Assets/ProjectPVP/Scripts/Runtime/Characters/ProjectPVP.Characters.asmdef`
  - `Assets/ProjectPVP/Scripts/Runtime/Match/ProjectPVP.Match.asmdef`
  - `Assets/ProjectPVP/Scripts/Runtime/Presentation/ProjectPVP.Presentation.asmdef`
- AI Arena local:
  - `Assets/ProjectPVP/Scripts/Runtime/Input/CombatantControlMode.cs`
  - `Assets/ProjectPVP/Scripts/Runtime/Input/IdleCombatantInputSource.cs`
  - `Assets/ProjectPVP/Scripts/Runtime/Input/LocalAiCombatantInputSource.cs`
  - `Assets/ProjectPVP/Scripts/Runtime/Input/AiArenaProtocol.cs`
  - `Assets/ProjectPVP/Scripts/Runtime/Input/AiArenaLocalTransport.cs`
  - `Assets/ProjectPVP/Scripts/Runtime/Input/AiArenaHeuristicPolicy.cs`
  - `Assets/ProjectPVP/Scripts/Runtime/Input/AiArenaSnapshotBuilder.cs`
- wiring runtime/editor:
  - `Assets/ProjectPVP/Scripts/Runtime/Match/CombatantSlotConfig.cs`
  - `Assets/ProjectPVP/Scripts/Runtime/Match/CombatantSlotProfile.cs`
  - `Assets/ProjectPVP/Scripts/Runtime/Gameplay/PlayerController.cs`
  - `Assets/ProjectPVP/Scripts/Runtime/Presentation/ProjectPvpDebugHud.cs`
  - `Assets/ProjectPVP/Scripts/Runtime/Data/CharacterDefinition.cs`
  - `Assets/ProjectPVP/Scripts/Runtime/Gameplay/CharacterMechanicsModule.cs`
  - `Assets/ProjectPVP/Scripts/Runtime/Characters/CharacterBootstrapFactory.cs`
  - `Assets/ProjectPVP/Scripts/Editor/PlayerControllerEditor.cs`
  - `Assets/ProjectPVP/Scripts/Editor/ProjectPvpCharacterActionConfigSync.cs`
  - `Assets/ProjectPVP/Scripts/Editor/ProjectPVP.Editor.asmdef`
  - `Assets/ProjectPVP/Tests/Editor/ProjectPVP.Runtime.EditorTests.asmdef`
  - `Assets/ProjectPVP/Tests/Editor/AiArenaInputSourceTests.cs`

## Known Constraints
- nao misturar esse pacote com videos, imagens, cenas staged ou tuning paralelo da `main`
- nao puxar junto o commit local ja existente na `main`
- validar Unity nesta worktree antes de integrar

## Merge Rule
- quando o delta acima estiver realmente portado e validado, integrar por commit dedicado desta branch/worktree
