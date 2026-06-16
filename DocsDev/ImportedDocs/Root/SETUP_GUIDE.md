# The Last Arrow - Setup & Tools Guide

**Status**: Repository installed and validated ✅
**Branch**: main (up to date with origin)
**Unity Version**: 6000.3.11f1
**Current Version**: v0.2.0 (2026-03-20)

---

## Repository Status

### Git Configuration
- **Remote**: `https://github.com/LucasOl1337/The-Last-Arrow.git`
- **Branch**: main (up to date)
- **Recent Commits**: 10 commits since v0.1.1

### Current State
- ✅ All core systems in place
- ✅ 2 playable characters (Mizu, Storm Dragon)
- ✅ Combat mechanics functional
- ✅ Input system centralized
- ✅ Character catalog system operational
- ✅ Editor tools integrated

### Uncommitted Changes
- Modified: `Assets/ProjectPVP/Scenes/Bootstrap.unity`
- Modified: `Assets/backg.png`
- Untracked: `Mizu.rar`, video background test files

---

## Editor Tools Available

All tools accessible via **ProjectPVP/** menu in Unity Editor.

### 📋 Scene & Project Management
```
ProjectPVP/Open Bootstrap Scene
ProjectPVP/Select MatchController
ProjectPVP/Validate Project Setup
ProjectPVP/Install Input Axes
```

### 🎨 Character Tools
```
ProjectPVP/Characters/Reserialize Character Assets
ProjectPVP/Characters/Rebuild Animation Clips From Folders
ProjectPVP/Characters/Rebuild All Character Clips From Folders
ProjectPVP/Characters/Audit Non Lateral Direction Folders
ProjectPVP/Characters/Optimize Character Sprite Imports
ProjectPVP/Characters/Bake Selected Character Sprites To Native Scale
ProjectPVP/Characters/Import PixelLab ZIP To Selected Character
ProjectPVP/Characters/Sync Selected Character From PixelLab
ProjectPVP/Characters/Sync All Configured Characters From PixelLab
```

### 🏟️ Environment Tools
```
ProjectPVP/Environment/Stamp Default Arena Collisions
ProjectPVP/Environment/Clear Auto Arena Collisions
```

### 🛠️ Special Tools
```
ProjectPVP/Characters/Configure Waifu2x (sprite upscaling)
```

---

## PixelLab MCP Integration

The project has **PixelLab MCP** configured for character generation and animation:

### Workflow
1. **Generate** character with PixelLab (8 directions recommended)
2. **Download** ZIP from PixelLab
3. **Import** via `ProjectPVP/Characters/Import PixelLab ZIP To Selected Character`
4. **Sync** animations to runtime via editor tools
5. **Rebuild** animation clips automatically

### Animation Actions Supported
- `idle` / `aim` / `walk` / `running`
- `shoot` / `dash` / `jump_start` / `jump_air`
- `melee` / `ult` (ultimate)

### Pipeline Structure
```
Assets/ProjectPVP/Characters/<CharacterName>/
├── Animations/          (organized by action/direction)
├── Rotations/          (static directional poses)
├── Data/               (ScriptableObjects: Definition, Audio, Bootstrap)
└── Scripts/            (character-specific code)
```

---

## Project Architecture at a Glance

### Core Layer
- `ProjectPvpRuntimeBootstrap` - Single initialization point
- Bootstrap scene wires all components

### Data Layer (ScriptableObjects)
- `CharacterDefinition` - Character config
- `CharacterBootstrapProfile` - Instantiation config
- `CharacterActionConfig` - Action mappings
- `ArenaDefinitionAsset` - Arena layout

### Gameplay Systems

#### Input
- `KeyboardPlayerInputSource` (Player 1: WASD, Player 2: Arrows)
- `InputSystemCombatantInputSource` (Gamepad support)
- Source of truth: `INPUT_SOURCE_OF_TRUTH.txt`

#### Combat
- `PlayerController` - Movement and state machine
- `PlayerCombatAnchor` - Hitbox and attack logic
- `ProjectileLauncher` + `ProjectileController` - Ranged attacks
- `CharacterMechanicsModule` - Per-character abilities

#### Presentation
- `CharacterSpriteAnimator` - Sprite animation
- `ProjectPvpVideoBackground` - Dynamic backgrounds
- `ParallaxController` - Scrolling effects
- Debug gizmos and HUD

---

## Ready to Work On

### Gameplay & Combat Focus (Current Priority)

**Areas to Improve:**
1. **Hitstun & Knockback** - Make hits feel more impactful
2. **Visual Feedback** - Screen shake, hit effects, impact indicators
3. **Combat Balance** - Mizu vs Storm Dragon comparison and tuning
4. **Special Mechanics** - Improve character-specific abilities
5. **Polish** - Responsiveness, animation flow, feel

**Key Files to Edit:**
```
Assets/ProjectPVP/Scripts/Runtime/Gameplay/
├── PlayerController.cs          (movement, state machine)
├── PlayerCombatAnchor.cs        (hitbox detection)
├── ProjectileLauncher.cs        (projectile creation)
├── ProjectileController.cs      (projectile behavior)
└── CharacterMechanicsModule.cs  (special character abilities)
```

**Character-Specific Mechanics:**
```
Assets/ProjectPVP/Characters/Mizu/Scripts/
└── MizuUltimateReplayModule.cs  (red afterimage ultimate)
```

---

## Quick Commands

### Validate Setup
```bash
# Check git status
git status

# View recent changes
git log --oneline -10

# Stage changes for commit
git add <file>
git commit -m "message"
```

### Editor Workflows
1. **Open scene**: ProjectPVP/Open Bootstrap Scene
2. **Validate setup**: ProjectPVP/Validate Project Setup
3. **Rebuild animations**: ProjectPVP/Characters/Rebuild All Character Clips From Folders
4. **Import new character**: Select CharacterDefinition → ProjectPVP/Characters/Import PixelLab ZIP To Selected Character

---

## Next Steps

1. ✅ Repository installed and tools documented
2. 🎯 **Focus on gameplay/combat improvements**
3. Profile current combat feel
4. Identify bottlenecks and improvement areas
5. Implement feedback systems (hitstun, knockback, effects)
6. Balance character matchup
7. Polish animation flow and responsiveness

---

**Last Updated**: 2026-03-25
**Repository Owner**: Lucas (@Donchitos)
**Ready to collaborate** ✨
