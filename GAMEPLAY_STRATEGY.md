# Gameplay & Combat Strategy

**Analysis Date**: 2026-03-25
**Current Version**: v0.2.0
**Focus**: Improve combat feel, feedback, and balance

---

## Current Combat System Analysis

### Core Mechanics (As Implemented)

#### Damage & Death
- **Current State**: Instant-kill on any hit (melee, projectile, ultimate)
- **No System For**:
  - Health/HP tracking
  - Hitstun (stagger after being hit)
  - Knockback (physics response to impact)
  - Multiple lives or damage progression
  - Knockdown or knockup states

#### What Works
```
Input → Action (Melee/Shoot/Dash/Ultimate)
      → Hitbox Detection
      → Target.Kill()
      → Death Animation plays
      → Match resets
```

#### Movement
- Ground movement: WASD/Arrows (acceleration/friction model)
- Air control: Reduced acceleration (0.9x) and friction (0.22x)
- Wall sliding: Velocity capped, gravity reduced (0.2x)
- Dashing: High-speed burst with directional control
- Jumping: With grace windows (coyote time, buffer)

#### Combat Actions
1. **Melee**: Active for 0.12s, detects hits once per target
2. **Shoot**: Launches projectile, cooldown 0.001s (unrestricted)
3. **Dash**: 0.12s duration, 0.45s cooldown, can be double-tapped
4. **Ultimate**: 0.28s total (0.45s windup ratio), 1.25s cooldown, can launch/dash mid-animation

### Areas for Enhancement

#### 1. **Hitstun System** (HIGH IMPACT)
- Currently: Hit registers → instant death
- Proposed: Hit registers → brief stun/animation lock
- Benefits:
  - Adds reaction time for counterplay
  - Makes hitting feel more satisfying
  - Enables combo windows
  - Reduces "instant death" feel

**Implementation Path**:
```
PlayerController:
  - Add `_hitStunTimeLeft` variable
  - Add `IsHitStunned` property
  - Add `ApplyHitstun(float duration)` method
  - In movement/action handlers, check `IsHitStunned` before accepting input
  - On melee/projectile hit: Call ApplyHitstun(0.1f) instead of Kill()
```

#### 2. **Knockback System** (HIGH IMPACT)
- Currently: No physics response to hits
- Proposed: Apply impulse velocity on impact
- Benefits:
  - Visual feedback of impact force
  - Creates spacing/zoning gameplay
  - Pushes toward walls/hazards (environmental gameplay)
  - Adds weight to attacks

**Implementation Path**:
```
PlayerController:
  - Add `_knockbackVelocity` Vector2
  - Add `_knockbackTimeLeft` for duration
  - Add `ApplyKnockback(Vector2 direction, float force, float duration)` method
  - In movement: Add knockback velocity to total velocity
  - In CharacterDefinition: Add knockback params per action
```

#### 3. **Visual Feedback** (MEDIUM IMPACT)
- Currently: Sprite change + death animation
- Proposed additions:
  - Screen shake on major hits (melee, ultimate)
  - Hit flash/color tint on character
  - Impact effect sprites (slash effect, spark, dust)
  - Hit landing sound (separate from swing sound)

**Implementation Path**:
```
Add to existing presentation layer:
  - CameraScreenShake component or method
  - CharacterSpriteAnimator: Add flash/tint coroutine
  - Instantiate impact VFX on hit detection
  - Play impact SFX (separate from animation SFX)
```

#### 4. **Attack Feedback** (MEDIUM IMPACT)
- Currently: Just animation and hit detection
- Proposed additions:
  - **Melee**:
    - Projectile cutting (already in code)
    - Crit-style enhanced damage on full charge
  - **Projectile**:
    - Arc/gravity on arrows
    - Trail VFX
  - **Ultimate**:
    - Improved visual clarity of hitbox
    - Warning indicator before activation
    - Power-up visual during windup

**Implementation Path**:
- Add `ProjectileController` parameter: `_trajectoryType` (linear/arc)
- Add visual feedback system: Gizmos show hitbox during impact
- Add animation events for impact timing

#### 5. **Character Balance** (MEDIUM IMPACT)
- Currently: Mizu vs Storm Dragon have different animation counts but same action timings
- Proposed improvements:
  - **Mizu**: Faster attacks, lower knockback, higher mobility
  - **Storm Dragon**: Stronger attacks, more knockback, electric effects
  - Matchup variety (not rock-paper-scissors, but distinct playstyles)

**Data Adjustments in CharacterDefinition**:
```
Mizu (Fast/Technical):
  - meleeAttackSpeed: 1.1x (faster recovery)
  - knockbackForce: 0.8x (less pushback)
  - dashDistance: 120 (more range)

StormDragon (Strong/Tanky):
  - meleeAttackSpeed: 0.9x (slower but stronger)
  - knockbackForce: 1.2x (more pushback)
  - dashDistance: 90 (less range)
```

#### 6. **Visual Polish** (LOW IMPACT, HIGH FEEL)
- Smooth animation transitions
- Particle effects on dash start
- Trail effect on melee swings
- Dust clouds on landing
- Water droplet effects in arena background

---

## Recommended Implementation Order

### Phase 1: Core Feel (Hitstun + Knockback)
**Goal**: Make hits feel impactful and allow counterplay
**Time**: 2-3 hours
**Files**:
- `PlayerController.cs` - Add hitstun/knockback system
- `CharacterDefinition.cs` - Add knockback parameters
- Hit detection methods - Use new system instead of instant kill

**Deliverable**: Hits apply stun + knockback, characters can react

### Phase 2: Visual Feedback
**Goal**: Make impacts visually satisfying
**Time**: 2-3 hours
**Files**:
- New: `ScreenShakeController.cs` or similar
- `CharacterSpriteAnimator.cs` - Add flash/tint
- `ProjectPvpDebugHud.cs` - Show hitstun/knockback state
- Audio: Trigger hit SFX

**Deliverable**: Screen shake, character flash, impact sounds

### Phase 3: Character Balance & Tuning
**Goal**: Make Mizu vs Storm Dragon feel different
**Time**: 1-2 hours
**Files**:
- `MizuDefinition.asset` - Adjust stats
- `StormDragonDefinition.asset` - Adjust stats
- Playtesting and iteration

**Deliverable**: Each character has distinct playstyle

### Phase 4: Polish & Special Effects
**Goal**: Add juice and visual flair
**Time**: 2-4 hours
**Files**:
- Particle system setup
- Animation refinement
- Sound design
- Camera/screen effects

**Deliverable**: Professional-feeling combat with VFX

---

## Code Architecture for New Systems

### Hitstun System
```csharp
// In PlayerController.cs
private float _hitStunTimeLeft = 0f;
public bool IsHitStunned => _hitStunTimeLeft > 0f;

public void ApplyHitstun(float duration)
{
    _hitStunTimeLeft = Mathf.Max(_hitStunTimeLeft, duration);
}

// In FixedUpdate/HandleActiveMelee section:
if (IsHitStunned)
{
    TickCooldowns(deltaTime);
    // Block all movement/action input
    return;
}
```

### Knockback System
```csharp
// In PlayerController.cs
private Vector2 _knockbackVelocity = Vector2.zero;
private float _knockbackTimeLeft = 0f;

public void ApplyKnockback(Vector2 direction, float force, float duration)
{
    _knockbackVelocity = direction.normalized * force;
    _knockbackTimeLeft = duration;
}

// In movement calculation:
if (_knockbackTimeLeft > 0f)
{
    velocity += _knockbackVelocity * (deltaTime / _knockbackTimeLeft);
    _knockbackTimeLeft -= deltaTime;
}
```

### Hit Detection Refactor
```csharp
// Current: target.Kill()
// Proposed: target.ReceiveHit(hitForce, hitDirection, knockbackPower)

private void HandleActiveMelee()
{
    // ... existing detection code ...
    if (TryOverlapAuthoredHitbox(...))
    {
        target.ReceiveHit(
            hitForce: 1f,
            hitDirection: (target.RootPosition - RootPosition).normalized,
            knockbackPower: characterDefinition.meleeKnockbackForce
        );
    }
}

// In target.ReceiveHit():
public void ReceiveHit(float hitForce, Vector2 hitDirection, float knockbackPower)
{
    ApplyHitstun(0.15f);
    ApplyKnockback(hitDirection, knockbackPower * 300f, 0.2f);
    _audioController.PlayHitSFX();
}
```

---

## Testing & Validation Checklist

### Hitstun
- [ ] Melee hit applies 0.1-0.15s stun
- [ ] Player cannot move during stun
- [ ] Player cannot attack during stun
- [ ] Dash can break out of stun? (TBD)
- [ ] Stun cancels on landing/wall contact

### Knockback
- [ ] Melee applies directional knockback
- [ ] Knockback is strongest for melee, medium for projectile, high for ultimate
- [ ] Knockback can push toward walls
- [ ] Character can still move/input during knockback
- [ ] Knockback respects physics (gravity, collisions)

### Balance
- [ ] Mizu feels faster/more agile
- [ ] Storm Dragon feels stronger/tankier
- [ ] No matchup is 100-0 (unbeatable)
- [ ] Both characters can win neutral exchanges

### Polish
- [ ] Screen shake on melee hit feels weighty (not jarring)
- [ ] Character flash on hit is visible but not distracting
- [ ] Impact sounds are crisp and satisfying
- [ ] VFX don't obscure hitboxes/gameplay

---

## Next Steps

1. ✅ Repository analyzed and documented
2. 🎯 **Start Phase 1: Implement hitstun + knockback**
3. Test and iterate
4. Move to Phase 2: Visual feedback
5. Phase 3: Character balance
6. Phase 4: Polish

**Ready to code?** Let me know which file you want to start with!
