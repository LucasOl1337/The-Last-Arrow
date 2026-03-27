# Physics & Mechanics Normalization Analysis
**Date**: 2026-03-26
**Scope**: Mizu, StormDragon, shared PlayerController systems
**Goal**: Identify inconsistencies before normalizing character physics & mechanics

---

## Executive Summary

There are **three layers of inconsistency** in the current setup:

1. **No neutral baseline** — the "default" values in `CharacterDefinition.cs` are identical to StormDragon's stats, meaning StormDragon isn't a designed character, it's a character that was never tuned away from class defaults.
2. **Two competing default systems** — `PlayerController.cs` has its own fallback constants (for when no `CharacterDefinition` is assigned) that are completely different from `CharacterDefinition`'s defaults. These two sets of values contradict each other.
3. **Several per-character values are inconsistent in ways that feel unintentional**, not design choices — Mizu's `ultimateWindupRatio: 11`, her larger collider, and StormDragon's stripped ultimate (all zeroed out) suggest unfinished or broken tuning rather than intentional differentiation.

---

## The Two Default Systems (Core Problem)

### PlayerController.cs Fallback Constants
Used when no `CharacterDefinition` asset is assigned at all:

| Parameter | PlayerController Default |
|---|---|
| moveSpeed | **605** |
| acceleration | **3200** |
| friction | **2600** |
| gravity | **1500** |
| maxFallSpeed | **1500** |
| jumpVelocity | **660** |

### CharacterDefinition.cs Class Field Defaults
Used when a `CharacterDefinition` asset IS assigned but a field wasn't set:

| Parameter | CharacterDefinition Default |
|---|---|
| moveSpeed | **240** |
| acceleration | **1600** |
| friction | **2000** |
| gravity | **1200** |
| maxFallSpeed | **2000** |
| jumpVelocity | **360** |

**These two defaults are irreconcilable.** A character on `PlayerController` fallbacks plays completely differently from a character on `CharacterDefinition` defaults. There is no single "neutral" feel to build from.

**Key finding**: `CharacterDefinition.cs` class defaults == StormDragon's exact values. StormDragon is not tuned — it's running on raw class defaults.

---

## Per-Character Comparison

### Movement

| Parameter | PC Fallback | CharDef Default (= StormDragon) | Mizu |
|---|---|---|---|
| moveSpeed | 605 | 240 (39.7%) | 415 (68.6%) |
| acceleration | 3200 | 1600 (50%) | 2200 (68.75%) |
| friction | 2600 | 2000 (76.9%) | 2400 (92.3%) |

**Note**: There's no character at or near the PlayerController fallback speed. Both defined characters are significantly slower, so the "unconfigured" speed is not a representative baseline for any real character.

### Jump & Gravity

| Parameter | PC Fallback | StormDragon | Mizu |
|---|---|---|---|
| jumpVelocity | 660 | **360 (54.5%)** | 660 |
| gravity | 1500 | **1200 (80%)** | 1500 |
| maxFallSpeed | 1500 | **2000 (+33%)** | 1500 |

**StormDragon problem**: Jumps only 54.5% as high as Mizu but falls 33% faster. A character that jumps half as high but falls extra fast will feel extremely grounded/sluggish — this may or may not be intentional design for "heavy dragon", but it's unverified tuning since StormDragon is running on class defaults.

### Melee Combat

| Parameter | Default | StormDragon | Mizu |
|---|---|---|---|
| meleeDuration | 0.12s | 0.12s | **0.3s (150% longer)** |
| meleeCooldown | 0.45s | 0.45s | 0.45s |
| meleeCanSeverProjectiles | false | false | **true** |
| meleeKnockbackForce | 400 | 400 | 400 |

Mizu's melee active window is 2.5x longer than StormDragon's. Both share the same cooldown, so Mizu has a significantly higher melee hit window as a percentage of cycle time.

### Dash

| Parameter | Default | StormDragon | Mizu |
|---|---|---|---|
| dashMultiplier | 1.8 | 1.8 | 1.8 |
| dashDuration | 0.12s | 0.12s | 0.12s |
| dashCooldown | 0.45s | 0.45s | 0.45s |
| dashDistance | 100 | 100 | 100 |

Dash is **fully consistent** across both characters — this is the most normalized system currently.

### Ultimate Ability

| Parameter | Default | StormDragon | Mizu |
|---|---|---|---|
| ultimateWindupRatio | 0.45 | 0.45 | **11** (bug — clamped to 1.0) |
| ultimateDashDistance | — | **0 (disabled)** | 120 |
| ultimateBlocksProjectiles | false | **false** | **true** |
| ultimateReplayDuration | — | **0 (disabled)** | 0.5s |
| ultimateReplayDashDistance | — | **0** | 220 |

**Mizu bug**: `ultimateWindupRatio: 11` is stored in the asset. The code does `Mathf.Clamp01()` so it becomes 1.0 at runtime — meaning Mizu's entire ultimate duration is windup with zero active window. This is almost certainly a bug. The intended value is probably `0.45` or similar.

**StormDragon ultimate**: All unique ultimate parameters are zeroed — no dash, no replay, no projectile blocking. Whether this is intentional (StormDragon has a purely stationary ultimate) or unfinished is unclear since no separate StormDragon ultimate mechanic module exists.

### Projectiles

| Parameter | Default | StormDragon | Mizu |
|---|---|---|---|
| projectileBaseSpeed | 1500 | 1500 | 1500 |
| projectileMinSpeed | 720 | 720 | 720 |
| projectileSpeedDecay | 360 | 360 | 360 |
| projectileGravity | 750 | 750 | 750 |
| projectileInheritVelocityFactor | 1.0 | **0.5** | 1.0 |
| maxArrows | 5 | 5 | **50** |

**StormDragon**: projectiles only inherit 50% of the player's movement velocity. This means a running StormDragon fires projectiles with half the trajectory boost a running Mizu would get.

**Mizu**: 50 arrow capacity vs. StormDragon's 5. This is a 10x difference in ammunition. Whether intentional (Mizu is an archer archetype?) or a leftover from testing is unclear.

**System-wide projectile issue**: Projectile gravity (750) is a completely different value from character gravity (1200–1500). Characters and their projectiles exist in different effective gravity worlds. A Mizu arrow falls much slower than Mizu herself.

### Collider Size

| Character | colliderSize |
|---|---|
| Default | (90, 210) |
| StormDragon | (90, 210) |
| Mizu | **(90, 240) — +30 height** |

Mizu is 14.3% taller as a hitbox target. This affects how likely she is to be hit by projectiles and melee, and how she fits through tight spaces in the level geometry.

---

## Shared Physics Constants (Not Overridable Per-Character)

These values in `PlayerController.cs` apply identically to all characters regardless of their `CharacterDefinition`. They cannot be tuned per-character currently:

| Constant | Value | What It Does |
|---|---|---|
| AirAccelerationMultiplier | 0.9x | All characters lose 10% acceleration in air |
| AirFrictionMultiplier | 0.22x | Air friction is 78% lower than ground friction |
| TurnAccelerationMultiplier | 1.3x | Direction-reversing gets 30% acceleration boost |
| JumpCutGravityMultiplier | 2.1x | Early jump release increases gravity 2.1x |
| FallGravityMultiplier | 1.2x | Falling applies extra 20% gravity |
| ApexGravityMultiplier | 0.82x | Gravity reduced 18% near jump apex |
| ApexVerticalSpeedThreshold | 120 units/s | Threshold for apex detection |
| JumpGraceTime (coyote time) | 0.16s | Window after leaving ground to still jump |
| WallJumpGraceTime | 0.12s | Window after leaving wall to still wall-jump |
| DashParryWindow | 0.2s | Window for parrying with dash |
| GroundSnapDistance | 240 units | Slope-following snap distance |

These are sensible shared values for a consistent-feeling physics world, but the jump curve multipliers (1.2x fall, 0.82x apex, 2.1x cut) interact with each character's base `gravity` and `jumpVelocity` differently — StormDragon's lower gravity means the `FallGravityMultiplier` has less absolute effect on it than on Mizu.

---

## Issues by Priority

### P0 — Bugs (Broken behavior)

**1. Mizu `ultimateWindupRatio: 11`**
Stored as 11, clamped to 1.0 at runtime. Mizu's ultimate has a 100% windup ratio — the entire duration is wind-up, leaving zero frames of active/payoff phase. The active window that should follow the windup never fires.
Fix: Change to `0.45` (matching the default) or whatever the intended ratio is.

---

### P1 — Structural Problems (Cause inconsistent feel)

**2. No single canonical baseline**
`PlayerController.cs` and `CharacterDefinition.cs` have two different sets of "defaults" that contradict each other. A new character's feel depends entirely on which system kicks in, not on a deliberate design choice.
Fix: Establish one canonical "reference character" set of values. Consider a `BaselineCharacterDefinition.asset` that new characters can reference or copy from.

**3. StormDragon = CharacterDefinition class defaults (never tuned)**
StormDragon's stats match the C# class field initializers exactly. It was never given deliberate stat values — it inherited whatever the developer typed as defaults when writing the class.
Fix: StormDragon needs deliberate stat tuning against a known baseline.

---

### P2 — Balance/Feel Inconsistencies (May be design intent, needs verification)

**4. StormDragon: low jump + high fall speed**
jumpVelocity = 360 (54.5% of Mizu) but maxFallSpeed = 2000 (33% higher than Mizu). If intended as a "heavy, grounded" character this needs to be a conscious decision, not an accidental combination of defaults.

**5. Mizu melee duration 2.5x longer**
0.3s active vs. 0.12s for StormDragon, same cooldown. Mizu's melee hit window is much more forgiving. Intentional?

**6. Mizu 50 arrows vs StormDragon 5**
10x the ammo. If Mizu is the archer archetype this makes sense, but it should be documented as intentional.

**7. StormDragon projectile velocity inheritance at 0.5x**
Arrow trajectory is dampened by 50% of player momentum for StormDragon. Mizu gets full momentum. This creates notably different aiming feel between characters.

**8. Mizu collider 30 units taller**
Larger hitbox makes Mizu easier to hit. May be intentional given her kit has stronger offensive tools.

---

### P3 — Architecture Improvements (For future proofing)

**9. Projectile gravity ≠ character gravity**
Projectiles use gravity 750, characters use 1200–1500. The worlds feel disconnected because they are. If you want the arrow arc to feel physically consistent with how the character moves, these should derive from a shared value (or at minimum be documented as intentional).

**10. Shared physics multipliers not per-character**
`AirAccelerationMultiplier`, `JumpCutGravityMultiplier`, etc. cannot be tuned per-character. If future characters need different air control feel, these would need to become `CharacterDefinition` fields.

---

## Normalization Applied — 2026-03-26

### ✅ Step 1 — Mizu ultimateWindupRatio bug fixed
`MizuDefinition.asset`: `ultimateWindupRatio: 11` → `0.45`
Note: this bug had zero gameplay effect on Mizu (her ultimate uses the dash-based code path, not the windup ratio path), but the stored value was semantically wrong.

### ✅ Step 2 — Canonical baseline established (Mizu's stats)
`CharacterDefinition.cs` class defaults updated to Mizu's values:
- moveSpeed: 240 → **415**
- acceleration: 1600 → **2200**
- friction: 2000 → **2400**
- jumpVelocity: 360 → **660**
- gravity: 1200 → **1500**
- maxFallSpeed: 2000 → **1500**

`PlayerController.cs` Default* constants updated to match:
- DefaultMoveSpeed: 605 → **415**
- DefaultAcceleration: 3200 → **2200**
- DefaultFriction: 2600 → **2400**
(gravity/maxFallSpeed/jump already matched Mizu)

Both default systems are now in sync. New characters start from Mizu's feel.

### ✅ Step 3 — StormDragon re-tuned deliberately
StormDragon was running on class defaults (never intentionally designed). Now tuned as a deliberately heavier character at ~80% of Mizu's mobility baseline:

| Stat | Old (class default) | New (intentional) | vs Mizu |
|---|---|---|---|
| moveSpeed | 240 | **330** | 79.5% |
| acceleration | 1600 | **1870** | 85% |
| friction | 2000 | **2160** | 90% |
| jumpVelocity | 360 | **530** | 80% |
| gravity | 1200 | **1650** | 110% |
| maxFallSpeed | 2000 | **1700** | 113% |

Feel: noticeably slower and lower jump than Mizu, slightly heavier gravity and faster fall — a grounded, deliberate archetype. No longer accidentally inheriting whatever the class defaults happened to be.

### ✅ Step 4 — StormDragon projectile velocity inheritance normalized
`projectileInheritVelocityFactor: 0.5` → `1.0`
Both characters now inherit full player momentum into projectile trajectory. The 0.5 was an unintentional leftover.

### Intentional divergences kept (documented)
- **Mizu melee duration 0.3s vs StormDragon 0.12s** — Mizu's melee window is 2.5x longer. Kept as intentional: Mizu is a hybrid melee/archer with forgiving hit windows; StormDragon's melee is a precise short-window attack.
- **Mizu 50 arrows vs StormDragon 5** — Kept as intentional: Mizu is the archer archetype with deep ammo reserves; StormDragon fires sparingly.
- **Mizu colliderSize (90, 240) vs StormDragon (90, 210)** — Kept: reflects character art height difference.
- **Mizu meleeCanSeverProjectiles: true, StormDragon: false** — Kept as intentional ability difference.
- **StormDragon ultimateDashDistance: 0** — StormDragon's ultimate is a stationary area attack vs Mizu's dash-based ability. Kept intentional.

### Step 5 — Projectile gravity (deferred)
Projectile gravity (750) remains separate from character gravity. Not changed — requires playtesting to decide if arc feel is an issue.

---

## File Reference

| File | Role |
|---|---|
| `Assets/ProjectPVP/Scripts/Runtime/Gameplay/PlayerController.cs` | Shared physics engine + fallback constants |
| `Assets/ProjectPVP/Scripts/Runtime/Data/CharacterDefinition.cs` | Per-character stat schema + class defaults |
| `Assets/ProjectPVP/Characters/Mizu/Data/MizuDefinition.asset` | Mizu's stats (has bug: ultimateWindupRatio: 11) |
| `Assets/ProjectPVP/Characters/StormDragon/Data/StormDragonDefinition.asset` | StormDragon stats (= class defaults, never tuned) |
| `Assets/ProjectPVP/Scripts/Runtime/Gameplay/ProjectileController.cs` | Projectile physics (separate gravity system) |
| `Assets/ProjectPVP/Characters/Mizu/Scripts/MizuUltimateReplayModule.cs` | Mizu-only ultimate replay mechanic |
