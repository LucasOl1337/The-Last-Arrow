# Development Guide - The Last Arrow

**For**: Collaborative gameplay & combat improvement
**Reference**: Read GAMEPLAY_STRATEGY.md first for context

---

## How We'll Work Together

1. **Discuss** what to build (goals, mechanics, feel)
2. **Plan** the implementation (files, data structures, approach)
3. **Code** - I'll write code, you review and iterate
4. **Test** in Unity - You test in editor, report feedback
5. **Iterate** - Adjust parameters, refine, repeat
6. **Commit** - Save work to git with clear messages

---

## Critical Files to Know

### Core Gameplay (Must Modify)
```
Assets/ProjectPVP/Scripts/Runtime/Gameplay/PlayerController.cs
  → Main character controller
  → Handles input, movement, actions, state
  → ~3200 lines - We'll add hitstun/knockback here

Assets/ProjectPVP/Scripts/Runtime/Data/CharacterDefinition.cs
  → Character configuration (data-driven)
  → Stats, animations, mechanics
  → Where we'll add balance parameters
```

### Character Data (Will Adjust)
```
Assets/ProjectPVP/Characters/Mizu/Data/MizuDefinition.asset
  → Mizu-specific configuration
Assets/ProjectPVP/Characters/StormDragon/Data/StormDragonDefinition.asset
  → Storm Dragon-specific configuration
```

### Presentation Layer (May Enhance)
```
Assets/ProjectPVP/Scripts/Runtime/Presentation/CharacterSpriteAnimator.cs
  → Sprite animation playback
  → We can add flash/tint effects here

Assets/ProjectPVP/Scripts/Runtime/Presentation/ProjectPvpDebugHud.cs
  → Debug display
  → Show hitstun/knockback state during development
```

### Support Systems (Reference)
```
Assets/ProjectPVP/Scripts/Runtime/Gameplay/ProjectileController.cs
  → Projectile behavior (for knockback physics)

Assets/ProjectPVP/Scripts/Runtime/Audio/CharacterAudioController.cs
  → Audio playback (trigger hit sounds)

Assets/ProjectPVP/Scripts/Runtime/Match/MatchController.cs
  → Match orchestration (kill/reset logic)
```

---

## Code Modification Patterns

### Pattern 1: Adding a Property to PlayerController

**Location**: `Assets/ProjectPVP/Scripts/Runtime/Gameplay/PlayerController.cs`

```csharp
// ===== ADD THIS IN THE PRIVATE FIELDS SECTION (around line 140) =====
private float _hitStunTimeLeft = 0f;
private Vector2 _knockbackVelocity = Vector2.zero;
private float _knockbackTimeLeft = 0f;

// ===== ADD THIS IN THE PROPERTIES SECTION (around line 180) =====
public bool IsHitStunned => _hitStunTimeLeft > 0f;
public bool IsKnockedBack => _knockbackTimeLeft > 0f;

// ===== ADD THESE PUBLIC METHODS (after Awake/OnEnable section, ~line 260) =====
public void ApplyHitstun(float duration)
{
    _hitStunTimeLeft = Mathf.Max(_hitStunTimeLeft, duration);
}

public void ApplyKnockback(Vector2 direction, float force, float duration)
{
    if (duration <= 0f) return;
    _knockbackVelocity = direction.normalized * force;
    _knockbackTimeLeft = duration;
}
```

### Pattern 2: Modifying FixedUpdate Logic

**Location**: `PlayerController.cs` FixedUpdate method (~line 286)

**Current structure:**
```csharp
private void FixedUpdate()
{
    if (_isDead || body == null) { return; }

    // ... cooldowns, collision ...

    HandleMovement(_currentInputFrame, deltaTime, ref velocity);
    HandleJumpAndGravity(_currentInputFrame, deltaTime, ref velocity);
    // ... more logic ...

    TryUseMelee(_currentInputFrame);
    HandleActiveMelee();
}
```

**Where to add checks:**
```csharp
// EARLY IN FixedUpdate - block input if stunned
if (IsHitStunned)
{
    _hitStunTimeLeft -= deltaTime;
    _hitStunTimeLeft = Mathf.Max(0f, _hitStunTimeLeft);

    // Still apply physics (gravity, knockback)
    // But don't accept new input commands

    Vector2 velocity = body.linearVelocity;
    HandleJumpAndGravity(_currentInputFrame, deltaTime, ref velocity); // Still fall!

    // Apply knockback
    if (_knockbackTimeLeft > 0f)
    {
        velocity += _knockbackVelocity;
        _knockbackTimeLeft -= deltaTime;
    }

    body.linearVelocity = velocity;
    return; // Exit early - skip action/movement input
}

// IN MOVEMENT SECTION
Vector2 velocity = body.linearVelocity;
// ... existing dash/gravity code ...

// ADD AFTER EXISTING VELOCITY UPDATES
if (_knockbackTimeLeft > 0f)
{
    velocity += _knockbackVelocity * (deltaTime / 0.2f); // Decay over duration
    _knockbackTimeLeft -= deltaTime;
}
```

### Pattern 3: Adding Data Parameters

**Location**: `Assets/ProjectPVP/Scripts/Runtime/Data/CharacterDefinition.cs`

```csharp
[Header("Combat Feel")]
public float meleeHitstunDuration = 0.1f;
public float meleeKnockbackForce = 400f;
public float projectileKnockbackForce = 300f;
public float ultimateKnockbackForce = 600f;
```

### Pattern 4: Modifying Hit Detection

**Location**: `PlayerController.cs` HandleActiveMelee method (~line 1478)

**Current code:**
```csharp
private void HandleActiveMelee()
{
    // ... hit detection ...
    if (_meleeHitIds.Contains(targetId))
    {
        _meleeHitIds.Add(targetId);
        target.Kill(); // ← CHANGE THIS
    }
}
```

**New code:**
```csharp
private void HandleActiveMelee()
{
    // ... hit detection ...
    if (!_meleeHitIds.Contains(targetId))
    {
        _meleeHitIds.Add(targetId);

        // NEW: Apply hitstun + knockback instead of kill
        Vector2 hitDirection = (target.RootPosition - RootPosition).normalized;
        target.ApplyHitstun(characterDefinition.meleeHitstunDuration);
        target.ApplyKnockback(
            hitDirection,
            characterDefinition.meleeKnockbackForce,
            0.2f // knockback duration
        );

        // Trigger hit audio
        target._audioController?.PlayHitSFX();

        // TODO: Screen shake, VFX
    }
}
```

---

## Testing Workflow

### In Unity Editor

1. **Open Scene**: ProjectPVP → Open Bootstrap Scene
2. **Play Mode**: Press Play
3. **Test Feature**:
   - Mizu attacks Storm Dragon with melee
   - Look for hitstun (target freezes briefly)
   - Look for knockback (target moves away)
4. **Adjust Numbers**:
   - Select CharacterDefinition asset
   - Change `meleeHitstunDuration` or `meleeKnockbackForce`
   - Recompile and test
5. **Iterate**: Repeat until feels right

### Debug Display

In `ProjectPvpDebugHud.cs`, you can add:
```csharp
// In OnGUI or debug text rendering:
if (player1.IsHitStunned)
    GUI.Label(new Rect(50, 50, 200, 30), $"P1 HITSTUN: {player1._hitStunTimeLeft:0.00}s");
if (player1.IsKnockedBack)
    GUI.Label(new Rect(50, 80, 200, 30), $"P1 KNOCKBACK: {player1._knockbackTimeLeft:0.00}s");
```

---

## Git Workflow

### ContextAndAiGuide
```powershell
.\tools\context-ai-guide.ps1 new-entry -Slug minha-sessao -Title "titulo da sessao"
.\tools\context-ai-guide.ps1 refresh-current -EntryRelativePath "Docs/ContextAndAiGuide/Daily/YYYY-MM-DD-minha-sessao.md"
```

- toda IA deve ler `Docs/ContextAndAiGuide/CURRENT_CONTEXT.md` antes de continuar
- toda sessao deve gerar um arquivo datado em `Docs/ContextAndAiGuide/Daily/`
- handoffs, manifests e pacotes de integracao devem ir para `Docs/ContextAndAiGuide/Packages/`

### Before Starting Work
```bash
git status                    # Check current state
git log --oneline -5          # See recent commits
```

### During Development
```bash
git add Assets/ProjectPVP/Scripts/Runtime/Gameplay/PlayerController.cs
git status                    # Verify staged
```

### When Committing
```bash
git commit -m "feat: add hitstun + knockback system

- Implemented ApplyHitstun() for temporary input lockout
- Implemented ApplyKnockback() for impact physics
- Modified melee hit detection to use new system
- Updated CharacterDefinition with balance parameters"
```

### Pushing (if you have access)
```bash
git push origin main
```

---

## Debugging Tips

### Check if Property Exists
```csharp
// If compilation fails with "IsHitStunned does not exist"
// Make sure you added the property to PlayerController
public bool IsHitStunned => _hitStunTimeLeft > 0f;
```

### Verify Hit Detection
```csharp
// In HandleActiveMelee, add debug log:
Debug.Log($"Hit detected on target {target.slotId} at position {target.RootPosition}");
```

### Monitor Knockback
```csharp
// In FixedUpdate where knockback applies:
if (_knockbackTimeLeft > 0f)
{
    Debug.DrawLine(RootPosition, RootPosition + _knockbackVelocity, Color.red);
}
```

---

## Common Issues & Fixes

### Issue: "Player can't move after hitstun"
**Cause**: Hitstun check blocking all input, including during knockback
**Fix**: Keep gravity/knockback active even during hitstun stun, only block action input

### Issue: "Knockback feels too weak/strong"
**Cause**: Force value doesn't match game feel/scale
**Fix**: Adjust `meleeKnockbackForce` in 50-unit increments (e.g., 300 → 350 → 400)

### Issue: "Character gets stuck in wall after knockback"
**Cause**: Knockback velocity applied directly, no collision response
**Fix**: This is expected - knockback is raw velocity. Add collision damping if needed

### Issue: "Animation breaks during hitstun"
**Cause**: CharacterSpriteAnimator still running animation
**Fix**: Add check in animation update: `if (player.IsHitStunned) { freeze animation }`

---

## Resources & References

### Code References
- **Death animation**: `characterDefinition.HasActionAnimation("death")`
- **Action duration**: `ResolveActionDuration("actionName")`
- **Audio playback**: `_audioController.PlayActionAudio("actionName")`
- **Position math**: `RootPosition`, `ResolveWorldPosition()`

### Physics References
- Rigidbody2D: `body.linearVelocity`
- Colliders: `bodyCollider`, `meleeHitboxAnchor`
- Raycast: `_castHits`, `TryOverlapAuthoredHitbox()`

### Event System
- Death event: `Died?.Invoke(this)`
- Mechanics hooks: `_characterMechanicsRuntime?.OnHit()`

---

## Communication Format

When discussing changes, use this format:

### Proposal
**What**: Add hitstun to melee attacks
**Why**: Give opponent time to react, reduce instant-death feel
**Where**: `PlayerController.ApplyHitstun()`, `HandleActiveMelee()`
**Data**: `CharacterDefinition.meleeHitstunDuration` (0.1s default)

### Result
Code added in files:
- ✅ `PlayerController.cs` - Added hitstun system
- ✅ `CharacterDefinition.cs` - Added balance parameters
- 🔄 Testing in progress - feels good at 0.12s

---

## Ready?

When you're ready to start:

1. Tell me which phase/feature to implement first
2. I'll write the code
3. You test and give feedback
4. We iterate together!

Good luck! 🎮✨
