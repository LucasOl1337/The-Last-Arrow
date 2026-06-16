using System.Collections.Generic;
using ProjectPVP.Data;
using ProjectPVP.Input;
using UnityEngine;

namespace ProjectPVP.Gameplay
{
    /// <summary>
    /// Handles melee, projectiles, ultimates, and combat mechanics.
    /// </summary>
    public sealed class PlayerCombatSystem
    {
        private const float InitialAssistAimNudge = 0.25f;
        private const float AssistSectorHalfAngleDeg = 22f;
        private const float ElevatedTargetBiasHeight = 24f;

        private readonly PlayerContext _context;
        private readonly PlayerStatResolver _statResolver;
        private readonly PlayerAnchorSystem _anchorSystem;
        private readonly PlayerActionLockSystem _actionLockSystem;
        private readonly List<PlayerController> _playerQueryBuffer = new();

        public PlayerCombatSystem(PlayerContext context, PlayerStatResolver statResolver, PlayerAnchorSystem anchorSystem, PlayerActionLockSystem actionLockSystem)
        {
            _context = context;
            _statResolver = statResolver;
            _anchorSystem = anchorSystem;
            _actionLockSystem = actionLockSystem;
        }

        public void TryUseMelee(PlayerInputFrame frame)
        {
            if (!frame.meleePressed || _context.meleeCooldownLeft > 0f || _context.meleeTimeLeft > 0f)
            {
                return;
            }

            _context.meleeCooldownLeft = _statResolver.ResolveMeleeCooldown();
            _context.meleeTimeLeft = _statResolver.ResolveMeleeDuration();
            _context.meleeHitIds.Clear();
            TriggerMeleeAnimation(_statResolver.ResolveActionDuration("melee", _statResolver.ResolveMeleeDuration()));
            PlayActionSfx("melee");
        }

        public void HandleActiveMelee()
        {
            if (_context.meleeTimeLeft <= 0f)
            {
                return;
            }

            HandleProjectileSeverDuringMelee();

            int hitCount = _anchorSystem.TryOverlapAuthoredHitbox(_context.meleeHitboxAnchor, GetMeleeContactFilter(), _context.overlapHits, out int authoredHitCount)
                ? authoredHitCount
                : Physics2D.OverlapBox(
                    _anchorSystem.GetMeleeHitboxCenter(),
                    _anchorSystem.GetMeleeHitboxSize(),
                    0f,
                    GetMeleeContactFilter(),
                    _context.overlapHits);

            for (int index = 0; index < hitCount; index += 1)
            {
                Collider2D hit = _context.overlapHits[index];
                if (hit == null)
                {
                    continue;
                }

                if (TrySeverProjectileWithMelee(hit))
                {
                    continue;
                }

                PlayerController target = hit.GetComponentInParent<PlayerController>();
                if (target == null || target == _context.Controller || target.IsDead)
                {
                    continue;
                }

                int targetId = target.GetInstanceID();
                if (_context.meleeHitIds.Contains(targetId))
                {
                    continue;
                }

                _context.meleeHitIds.Add(targetId);

                Vector2 hitDirection = (target.RootPosition - _context.Controller.RootPosition).normalized;
                float hitstunDuration = _context.characterDefinition != null
                    ? _context.characterDefinition.meleeHitstunDuration
                    : 0.1f;
                float knockbackForce = _context.characterDefinition != null
                    ? _context.characterDefinition.meleeKnockbackForce
                    : 400f;

                target.ApplyHitstun(hitstunDuration);
                target.ApplyKnockback(hitDirection, knockbackForce, 0.2f);
            }
        }

        public void HandleProjectileSeverDuringMelee()
        {
            int hitCount = _anchorSystem.TryOverlapAuthoredHitbox(_context.meleeHitboxAnchor, GetProjectileSeverContactFilter(), _context.overlapHits, out int authoredHitCount)
                ? authoredHitCount
                : Physics2D.OverlapBox(
                    _anchorSystem.GetMeleeHitboxCenter(),
                    _anchorSystem.GetMeleeHitboxSize(),
                    0f,
                    GetProjectileSeverContactFilter(),
                    _context.overlapHits);

            for (int index = 0; index < hitCount; index += 1)
            {
                Collider2D hit = _context.overlapHits[index];
                if (hit == null)
                {
                    continue;
                }

                TrySeverProjectileWithMelee(hit);
            }
        }

        public void TriggerMeleeAnimation(float duration)
        {
            _context.meleeAnimationTimeLeft = Mathf.Max(duration, 0f);
            _actionLockSystem.LockActionForDuration("melee", duration, Mathf.Min(duration, 0.08f), false);
        }

        public bool TrySeverProjectileWithMelee(Collider2D hit)
        {
            if (!CanSeverProjectilesWithMelee() || hit == null)
            {
                return false;
            }

            ProjectileController projectile = hit.GetComponentInParent<ProjectileController>();
            if (projectile == null || projectile.IsStuck || projectile.IsDisarmed)
            {
                return false;
            }

            int projectileId = projectile.GetInstanceID();
            if (_context.meleeHitIds.Contains(projectileId))
            {
                return true;
            }

            _context.meleeHitIds.Add(projectileId);
            projectile.SeverByMelee();
            return true;
        }

        public bool CanSeverProjectilesWithMelee()
        {
            return _context.characterDefinition != null && _context.characterDefinition.meleeCanSeverProjectiles;
        }

        /// <summary>
        /// Fires the held shot using the Last Arrow ballistic system:
        ///   1. Aim is snapped to one of 8 compass directions.
        ///   2. If an enemy is found inside the aimed cone, compute the exact ballistic
        ///      arc needed to reach their current position at the arrow's launch speed.
        ///   3. The arrow then flies with pure physics — gravity only, no homing —
        ///      so the target can still dodge by moving.
        /// </summary>
        public void FireHeldShot()
        {
            if (_context.ProjectilePrefab == null || _context.shootCooldownLeft > 0f || _context.arrows <= 0)
                return;

            // ── 1. Resolve the snapped 8-directional aim ─────────────────────────────
            Vector2 aimDir8 = _context.aimHoldDirection.sqrMagnitude > 0.01f
                ? _context.aimHoldDirection.normalized
                : new Vector2(_context.facing >= 0 ? 1f : -1f, 0f);

            int shotFacing = ResolveShotFacing(aimDir8);
            if (shotFacing != _context.facing) _context.facing = shotFacing;

            Vector2 origin = GetProjectileSpawnPoint(aimDir8, shotFacing);

            float arrowSpeed = _context.characterDefinition != null
                ? _context.characterDefinition.projectileBaseSpeed : 1600f;
            float arrowGrav = _context.characterDefinition != null
                ? _context.characterDefinition.projectileGravity : 1500f;

            // ── 2. Find the best enemy inside the aimed cone ──────────────────────────
            // Cone is ±22° — exactly half the gap between the 8 directions (45°/2).
            // This means ballistic assist only activates when the player genuinely
            // aimed at the correct one of the 8 directions; picking the wrong direction
            // fires a raw shot with no lock-on.
            Transform assistTarget = ResolveProjectileAssistTarget(origin, aimDir8);
            PlayerController targetPlayer = assistTarget != null ? assistTarget.GetComponent<PlayerController>() : null;

            // ── 3. Compute optimal ballistic arc ──────────────────────────────────────
            // If a target is found and the arc is solvable, override the raw aim direction.
            // Otherwise fall back to the raw 8-directional shot so the player is never stuck.
            Vector2 launchDir = aimDir8;
            if (targetPlayer != null)
            {
                Vector2 hitPoint = PlayerAnchorSystem.ResolveCombatantAimPoint(targetPlayer);
                if (TryBallisticSolve(origin, hitPoint, arrowSpeed, arrowGrav,
                                      out Vector2 lowArc, out Vector2 highArc))
                {
                    // Accept the low (fast/direct) arc only if it stays inside the aimed sector.
                    // Allow up to +5° extra for the arc math rounding — but not more.
                    if (!TrySelectBestBallisticDirection(origin, hitPoint, aimDir8, arrowSpeed, arrowGrav, out launchDir))
                    {
                        launchDir = SelectBestBallisticDirection(aimDir8, lowArc, highArc);
                    }
                }
                else
                {
                    launchDir = ApplyInitialAssistNudge(aimDir8, origin, assistTarget);
                }
            }

            // ── 4. Spawn the arrow ────────────────────────────────────────────────────
            ProjectileController proj = ProjectileLauncher.Spawn(
                _context.ProjectilePrefab,
                _context.characterDefinition,
                _context.Controller.gameObject,
                origin,
                launchDir,
                assistTarget,
                assistTarget != null && _statResolver.ResolveProjectileAssistEnabled(),
                _statResolver.ResolveProjectileAssistStrength(),
                _statResolver.ResolveProjectileAssistMaxTurnRateDeg(),
                Mathf.Min(AssistSectorHalfAngleDeg, Mathf.Clamp(_statResolver.ResolveProjectileAssistAcquireConeDeg(), 0f, 180f)),
                _statResolver.ResolveProjectileAssistMaxRange(),
                _statResolver.ResolveProjectileAssistMinDistance(),
                _statResolver.ResolveProjectileAssistDropoffStartRatio(),
                GetProjectileInheritedVelocity(),
                _statResolver.ResolveProjectileInheritVelocityFactor(),
                _statResolver.ResolveProjectileSprite(),
                _statResolver.ResolveProjectileScale());

            if (proj != null) _context.lastLaunchedProjectile = proj;

            _context.arrows -= 1;
            _context.shootCooldownLeft = _statResolver.ResolveShootCooldown();
            TriggerShootAnimation(_statResolver.ResolveActionDuration("shoot", 0.18f));
            PlayActionSfx("shoot");
        }

        /// <summary>
        /// Solves the two ballistic launch directions that will carry an arrow from
        /// <paramref name="origin"/> to <paramref name="target"/> at exactly
        /// <paramref name="speed"/> under constant downward <paramref name="gravity"/>.
        ///
        /// Uses the standard quadratic-in-tan(θ) method:
        ///   A·u² + B·u + C = 0   where  u = tan(launch angle)
        ///   A =  g·dx² / (2·s²)
        ///   B = −dx
        ///   C =  dy + A
        ///
        /// Returns false when the target is out of range (negative discriminant).
        /// <paramref name="lowArcDir"/>  = shallower, faster trajectory (preferred).
        /// <paramref name="highArcDir"/> = steeper,   slower trajectory.
        /// </summary>
        private static bool TryBallisticSolve(
            Vector2 origin, Vector2 target,
            float speed, float gravity,
            out Vector2 lowArcDir, out Vector2 highArcDir)
        {
            lowArcDir = highArcDir = Vector2.zero;

            if (speed < 0.1f)
            {
                Vector2 fallback = (target - origin).normalized;
                lowArcDir = highArcDir = fallback;
                return true;
            }

            float dx = target.x - origin.x;
            float dy = target.y - origin.y;

            // Near-vertical: target almost directly above/below — aim straight at it.
            if (Mathf.Abs(dx) < 1f)
            {
                Vector2 d = (target - origin).normalized;
                lowArcDir = highArcDir = d;
                return true;
            }

            float s2 = speed * speed;
            float A  =  gravity * dx * dx / (2f * s2);
            float B  = -dx;
            float C  =  dy + A;

            float disc = B * B - 4f * A * C;
            if (disc < 0f) return false;   // target unreachable at this speed

            float sqrtD = Mathf.Sqrt(disc);
            float u1    = (-B + sqrtD) / (2f * A);
            float u2    = (-B - sqrtD) / (2f * A);

            // Convert tan(θ) → normalised launch direction.
            // The horizontal component sign is determined by the direction to target.
            Vector2 TanToDir(float u)
            {
                float cosA = 1f / Mathf.Sqrt(1f + u * u);
                float sinA = u * cosA;
                return new Vector2(cosA * (dx >= 0f ? 1f : -1f), sinA);
            }

            Vector2 d1 = TanToDir(u1);
            Vector2 d2 = TanToDir(u2);

            // Low arc = shallower angle = smaller absolute tan value.
            if (Mathf.Abs(u1) <= Mathf.Abs(u2)) { lowArcDir = d1; highArcDir = d2; }
            else                                  { lowArcDir = d2; highArcDir = d1; }
            return true;
        }

        public Vector2 GetProjectileSpawnPoint(Vector2 aimDirection, int facingDirection)
        {
            Vector2 basePosition = _anchorSystem.ResolveProjectileOriginWorldPosition(facingDirection);
            basePosition += aimDirection * _statResolver.ResolveProjectileForward();
            basePosition += new Vector2(facingDirection * _statResolver.ResolveProjectileForwardFacing(), _statResolver.ResolveProjectileVerticalOffset());
            return basePosition;
        }

        public int ResolveShotFacing(Vector2 aimDirection)
        {
            if (Mathf.Abs(aimDirection.x) > 0.01f)
            {
                return aimDirection.x > 0f ? 1 : -1;
            }

            return _context.facing == 0 ? 1 : (_context.facing > 0 ? 1 : -1);
        }

        public Transform ResolveProjectileAssistTarget(Vector2 origin, Vector2 initialDirection)
        {
            if (!_statResolver.ResolveProjectileAssistEnabled())
            {
                return null;
            }

            float maxRange = Mathf.Max(0f, _statResolver.ResolveProjectileAssistMaxRange());
            float minDistance = Mathf.Max(0f, _statResolver.ResolveProjectileAssistMinDistance());
            Vector2 selectedSector = PlayerMovementSystem.Snap8Dir(initialDirection);
            if (selectedSector.sqrMagnitude <= 0.01f)
            {
                selectedSector = new Vector2(_context.facing >= 0 ? 1f : -1f, 0f);
            }

            float bestSqrDistance = float.MaxValue;
            PlayerController bestTarget = null;

            PlayerController.CopyActivePlayers(_playerQueryBuffer);
            if (_playerQueryBuffer.Count > 0)
            {
                for (int index = 0; index < _playerQueryBuffer.Count; index += 1)
                {
                    EvaluateProjectileAssistCandidate(
                        _playerQueryBuffer[index],
                        origin,
                        selectedSector,
                        maxRange,
                        minDistance,
                        ref bestSqrDistance,
                        ref bestTarget);
                }
            }
            else
            {
                PlayerController[] players = Object.FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
                for (int index = 0; index < players.Length; index += 1)
                {
                    EvaluateProjectileAssistCandidate(
                        players[index],
                        origin,
                        selectedSector,
                        maxRange,
                        minDistance,
                        ref bestSqrDistance,
                        ref bestTarget);
                }
            }

            _playerQueryBuffer.Clear();
            return bestTarget != null ? bestTarget.transform : null;
        }

        private void EvaluateProjectileAssistCandidate(
            PlayerController candidate,
            Vector2 origin,
            Vector2 selectedSector,
            float maxRange,
            float minDistance,
            ref float bestSqrDistance,
            ref PlayerController bestTarget)
        {
            if (candidate == null || candidate == _context.Controller || candidate.IsDead)
            {
                return;
            }

            if (!TryResolveRequiredAssistSector(origin, candidate, out Vector2 requiredSector, out Vector2 toCandidate))
            {
                return;
            }

            float sqrDistance = toCandidate.sqrMagnitude;
            if (sqrDistance <= 0.0001f)
            {
                return;
            }

            float distance = Mathf.Sqrt(sqrDistance);
            if (distance > maxRange || distance < minDistance)
            {
                return;
            }

            if (!IsSameEightDirection(selectedSector, requiredSector))
            {
                return;
            }

            if (sqrDistance >= bestSqrDistance)
            {
                return;
            }

            bestSqrDistance = sqrDistance;
            bestTarget = candidate;
        }

        private bool TryResolveRequiredAssistSector(
            Vector2 origin,
            PlayerController candidate,
            out Vector2 requiredSector,
            out Vector2 toCandidate)
        {
            requiredSector = Vector2.zero;
            toCandidate = Vector2.zero;

            if (candidate == null)
            {
                return false;
            }

            Vector2 candidateAimPoint = PlayerAnchorSystem.ResolveCombatantAimPoint(candidate);
            toCandidate = candidateAimPoint - origin;
            if (toCandidate.sqrMagnitude <= 0.0001f)
            {
                return false;
            }

            float arrowSpeed = _context.characterDefinition != null
                ? _context.characterDefinition.projectileBaseSpeed
                : 1600f;
            float arrowGrav = _context.characterDefinition != null
                ? _context.characterDefinition.projectileGravity
                : 1500f;

            if (!TryResolvePreferredBallisticDirection(origin, candidateAimPoint, arrowSpeed, arrowGrav, out Vector2 preferredDirection))
            {
                preferredDirection = toCandidate.normalized;
            }

            requiredSector = PlayerMovementSystem.Snap8Dir(preferredDirection);
            if (requiredSector.sqrMagnitude <= 0.01f)
            {
                requiredSector = PlayerMovementSystem.Snap8Dir(toCandidate);
            }

            return requiredSector.sqrMagnitude > 0.01f;
        }

        private static Vector2 SelectBestBallisticDirection(Vector2 preferredSector, Vector2 firstArc, Vector2 secondArc)
        {
            float firstAngle = Vector2.Angle(preferredSector, firstArc);
            float secondAngle = Vector2.Angle(preferredSector, secondArc);
            return firstAngle <= secondAngle ? firstArc : secondArc;
        }

        private bool TryResolvePreferredBallisticDirection(
            Vector2 origin,
            Vector2 target,
            float baseSpeed,
            float gravity,
            out Vector2 preferredDirection)
        {
            preferredDirection = Vector2.zero;
            if (!TryBallisticSolve(origin, target, baseSpeed, gravity, out Vector2 lowArc, out Vector2 highArc))
            {
                return false;
            }

            Vector2 inheritedVelocity = GetProjectileInheritedVelocity() * _statResolver.ResolveProjectileInheritVelocityFactor();
            bool lowClear = IsBallisticPathClear(origin, lowArc, baseSpeed, gravity, inheritedVelocity, target);
            bool highClear = IsBallisticPathClear(origin, highArc, baseSpeed, gravity, inheritedVelocity, target);
            bool favorHighArc = target.y - origin.y > ElevatedTargetBiasHeight;

            if (lowClear && highClear)
            {
                preferredDirection = favorHighArc ? highArc : lowArc;
                return true;
            }

            if (highClear)
            {
                preferredDirection = highArc;
                return true;
            }

            if (lowClear)
            {
                preferredDirection = lowArc;
                return true;
            }

            preferredDirection = favorHighArc ? highArc : lowArc;
            return true;
        }

        private bool TrySelectBestBallisticDirection(
            Vector2 origin,
            Vector2 target,
            Vector2 preferredSector,
            float baseSpeed,
            float gravity,
            out Vector2 selectedDirection)
        {
            selectedDirection = preferredSector;
            if (!TryBallisticSolve(origin, target, baseSpeed, gravity, out Vector2 lowArc, out Vector2 highArc))
            {
                return false;
            }

            Vector2 inheritedVelocity = GetProjectileInheritedVelocity() * _statResolver.ResolveProjectileInheritVelocityFactor();
            bool lowClear = IsBallisticPathClear(origin, lowArc, baseSpeed, gravity, inheritedVelocity, target);
            bool highClear = IsBallisticPathClear(origin, highArc, baseSpeed, gravity, inheritedVelocity, target);

            if (lowClear && highClear)
            {
                selectedDirection = SelectBestBallisticDirection(preferredSector, lowArc, highArc);
                return true;
            }

            if (lowClear)
            {
                selectedDirection = lowArc;
                return true;
            }

            if (highClear)
            {
                selectedDirection = highArc;
                return true;
            }

            return false;
        }

        private bool IsBallisticPathClear(
            Vector2 origin,
            Vector2 launchDirection,
            float baseSpeed,
            float gravity,
            Vector2 inheritedVelocity,
            Vector2 target)
        {
            float initialSpeed = baseSpeed + Mathf.Max(0f, Vector2.Dot(inheritedVelocity, launchDirection.normalized));
            if (initialSpeed <= 0.01f)
            {
                return false;
            }

            const int sampleCount = 24;
            const float targetRadius = 24f;
            float estimatedFlightTime = ResolveEstimatedFlightTime(origin, target, launchDirection.normalized, initialSpeed);
            Vector2 previous = origin;

            for (int step = 1; step <= sampleCount; step += 1)
            {
                float t = estimatedFlightTime * (step / (float)sampleCount);
                Vector2 current = origin
                    + (launchDirection.normalized * initialSpeed * t)
                    + (Vector2.down * (0.5f * gravity * t * t));

                if (Physics2D.Linecast(previous, current, _context.Controller.groundMask))
                {
                    return false;
                }

                if ((current - target).sqrMagnitude <= targetRadius * targetRadius)
                {
                    return true;
                }

                previous = current;
            }

            return (previous - target).sqrMagnitude <= targetRadius * targetRadius;
        }

        private static float ResolveEstimatedFlightTime(Vector2 origin, Vector2 target, Vector2 direction, float speed)
        {
            float horizontalSpeed = direction.x * speed;
            float dx = target.x - origin.x;
            if (Mathf.Abs(horizontalSpeed) > 0.01f)
            {
                float time = dx / horizontalSpeed;
                if (time > 0f)
                {
                    return Mathf.Clamp(time, 0.05f, 2.5f);
                }
            }

            return Mathf.Clamp(Vector2.Distance(origin, target) / Mathf.Max(speed, 0.01f), 0.05f, 2.5f);
        }

        private static bool IsSameEightDirection(Vector2 a, Vector2 b)
        {
            if (a.sqrMagnitude <= 0.01f || b.sqrMagnitude <= 0.01f)
            {
                return false;
            }

            return Vector2.Dot(a.normalized, b.normalized) >= 0.999f;
        }

        public Vector2 ApplyInitialAssistNudge(Vector2 shotDirection, Vector2 origin, Transform assistTarget)
        {
            if (assistTarget == null || !_statResolver.ResolveProjectileAssistEnabled())
            {
                return shotDirection;
            }

            Vector2 toTarget = (Vector2)assistTarget.position - origin;
            if (toTarget.sqrMagnitude <= 0.0001f)
            {
                return shotDirection;
            }

            float weight = Mathf.Clamp01(_statResolver.ResolveProjectileAssistStrength() * InitialAssistAimNudge);
            return Vector2.Lerp(shotDirection, toTarget.normalized, weight).normalized;
        }

        public Vector2 GetProjectileInheritedVelocity()
        {
            return _context.body != null ? _context.body.linearVelocity : Vector2.zero;
        }

        public bool HandleIncomingProjectile(ProjectileController projectile)
        {
            if (projectile == null || _context.isDead)
            {
                return false;
            }

            if (projectile.SourceObject == _context.Controller.gameObject)
            {
                return false;
            }

            if (CanSeverIncomingProjectile(projectile))
            {
                projectile.SeverByMelee();
                return true;
            }

            if (CanBlockProjectileWithUltimate())
            {
                return true;
            }

            if (CanParryProjectile())
            {
                AddArrows(1);
                _context.dashParryTimer = 0f;
                _context.dashPressTimer = 0f;
                return true;
            }

            ApplyProjectileHitReaction(projectile);

            return true;
        }

        public void ReceiveProjectile(ProjectileController projectile)
        {
            HandleIncomingProjectile(projectile);
        }

        public bool TryCollectProjectile(ProjectileController projectile)
        {
            if (projectile == null || _context.isDead)
            {
                return false;
            }

            AddArrows(1);
            return true;
        }

        public bool CanParryProjectile()
        {
            return _context.dashParryTimer > 0f || _context.dashPressTimer > 0f;
        }

        private void ApplyProjectileHitReaction(ProjectileController projectile)
        {
            CharacterDefinition sourceDefinition = ResolveProjectileSourceDefinition(projectile);
            Vector2 hitDirection = ResolveProjectileHitDirection(projectile);
            float hitstunDuration = sourceDefinition != null
                ? sourceDefinition.projectileHitstunDuration
                : 0.08f;
            float knockbackForce = sourceDefinition != null
                ? sourceDefinition.projectileKnockbackForce
                : 300f;

            ApplyHitstun(hitstunDuration);
            ApplyKnockback(hitDirection, knockbackForce, 0.2f);
        }

        private CharacterDefinition ResolveProjectileSourceDefinition(ProjectileController projectile)
        {
            if (projectile == null || projectile.SourceObject == null)
            {
                return null;
            }

            PlayerController source = projectile.SourceObject.GetComponentInParent<PlayerController>();
            return source != null ? source.characterDefinition : null;
        }

        private Vector2 ResolveProjectileHitDirection(ProjectileController projectile)
        {
            Vector2 hitDirection = projectile != null ? projectile.TravelDirection : Vector2.zero;
            if (hitDirection.sqrMagnitude > 0.01f)
            {
                return hitDirection.normalized;
            }

            if (projectile != null && projectile.SourceObject != null)
            {
                PlayerController source = projectile.SourceObject.GetComponentInParent<PlayerController>();
                if (source != null)
                {
                    hitDirection = _context.Controller.RootPosition - source.RootPosition;
                    if (hitDirection.sqrMagnitude > 0.01f)
                    {
                        return hitDirection.normalized;
                    }
                }
            }

            return Vector2.right;
        }

        public bool CanSeverIncomingProjectile(ProjectileController projectile)
        {
            if (!CanSeverProjectilesWithMelee() || !(_context.meleeTimeLeft > 0f) || projectile == null || projectile.IsStuck || projectile.IsDisarmed)
            {
                return false;
            }

            Collider2D projectileCollider = projectile.hitCollider;
            if (projectileCollider == null)
            {
                return false;
            }

            Collider2D meleeCollider = _context.meleeHitboxAnchor != null ? _context.meleeHitboxAnchor.AttachedCollider : null;
            if (meleeCollider != null)
            {
                return meleeCollider.bounds.Intersects(projectileCollider.bounds);
            }

            Bounds projectileBounds = projectileCollider.bounds;
            Bounds meleeBounds = new Bounds(_anchorSystem.GetMeleeHitboxCenter(), _anchorSystem.GetMeleeHitboxSize());
            return meleeBounds.Intersects(projectileBounds);
        }

        public void TryUseUltimate(PlayerInputFrame frame)
        {
            if (!frame.ultimatePressed || _context.ultimateCooldownLeft > 0f || _context.ultimateTimeLeft > 0f || !_statResolver.HasUltimateConfigured())
            {
                return;
            }

            _context.ultimateCooldownLeft = _statResolver.ResolveUltimateCooldown();
            _context.ultimateTotalDuration = _statResolver.ResolveActionDuration("ult", 0.28f);
            _context.ultimateTimeLeft = _context.ultimateTotalDuration;
            _context.ultimateAnimationTimeLeft = _context.ultimateTotalDuration;
            _context.ultimateImpactApplied = false;
            BeginUltimateDash();
            _actionLockSystem.LockActionForDuration("ult", _context.ultimateTotalDuration, Mathf.Min(_context.ultimateTotalDuration, 0.2f), false);
            PlayActionSfx("ult");
        }

        public void HandleActiveUltimate(float deltaTime)
        {
            if (_context.ultimateTimeLeft <= 0f)
            {
                return;
            }

            _context.ultimateTimeLeft = Mathf.Max(0f, _context.ultimateTimeLeft - deltaTime);
            if (_context.ultimateImpactApplied)
            {
                return;
            }

            if (_statResolver.ResolveUltimateDashDistance() > 0.01f)
            {
                if (_context.ultimateDashTimeLeft <= 0f)
                {
                    ApplyUltimateImpact();
                    _context.ultimateImpactApplied = true;
                }

                return;
            }

            float elapsed = _context.ultimateTotalDuration - _context.ultimateTimeLeft;
            float activeTime = _context.ultimateTotalDuration * _statResolver.ResolveUltimateWindupRatio();
            if (elapsed >= activeTime)
            {
                ApplyUltimateImpact();
                _context.ultimateImpactApplied = true;
            }
        }

        public void ApplyUltimateImpact()
        {
            int hitCount = CollectCurrentUltimateHits(_context.overlapHits);
            ApplyUltimateDamageHits(_context.overlapHits, hitCount);
        }

        public Vector2 UpdateUltimateDashVelocity(float deltaTime)
        {
            if (_context.ultimateDashTimeLeft <= 0f)
            {
                return Vector2.zero;
            }

            Vector2 dashVelocity = _context.ultimateDashVelocity;
            _context.ultimateDashTimeLeft = Mathf.Max(0f, _context.ultimateDashTimeLeft - deltaTime);
            if (_context.ultimateDashTimeLeft <= 0f)
            {
                _context.ultimateDashVelocity = Vector2.zero;
            }

            return dashVelocity;
        }

        public void BeginUltimateDash()
        {
            _context.ultimateDashTimeLeft = 0f;
            _context.ultimateDashVelocity = Vector2.zero;
            _context.lastUltimateDashVelocity = Vector2.zero;
            _context.ultimateProjectileBlockTimer = 0f;

            float dashDistance = _statResolver.ResolveUltimateDashDistance();
            float dashDuration = _statResolver.ResolveUltimateDashDuration();
            if (dashDistance <= 0.01f || dashDuration <= 0.01f)
            {
                return;
            }

            Vector2 dashDirection = new Vector2(_context.facing == 0 ? 1 : (_context.facing > 0 ? 1 : -1), 0f);
            _context.ultimateDashVelocity = dashDirection * (dashDistance / dashDuration);
            _context.ultimateDashTimeLeft = dashDuration;
            if (_statResolver.ResolveUltimateBlocksProjectiles())
            {
                _context.ultimateProjectileBlockTimer = _statResolver.ResolveUltimateProjectileBlockDuration();
            }
        }

        public int CollectCurrentUltimateHits(Collider2D[] results)
        {
            return _anchorSystem.TryOverlapAuthoredHitbox(_context.ultimateHitboxAnchor, GetMeleeContactFilter(), results, out int authoredHitCount)
                ? authoredHitCount
                : Physics2D.OverlapCircle(
                    _anchorSystem.GetUltimateHitboxCenter(),
                    _statResolver.ResolveUltimateRadius(),
                    GetMeleeContactFilter(),
                    results);
        }

        public void ApplyUltimateDamageHits(Collider2D[] hits, int hitCount)
        {
            for (int index = 0; index < hitCount; index += 1)
            {
                Collider2D hit = hits[index];
                if (hit == null)
                {
                    continue;
                }

                PlayerController target = hit.GetComponentInParent<PlayerController>();
                if (target == null || target == _context.Controller || target.IsDead)
                {
                    continue;
                }

                Vector2 hitDirection = (target.RootPosition - _context.Controller.RootPosition).normalized;
                float hitstunDuration = _context.characterDefinition != null
                    ? _context.characterDefinition.ultimateHitstunDuration
                    : 0.15f;
                float knockbackForce = _context.characterDefinition != null
                    ? _context.characterDefinition.ultimateKnockbackForce
                    : 600f;

                target.ApplyHitstun(hitstunDuration);
                target.ApplyKnockback(hitDirection, knockbackForce, 0.25f);
            }
        }

        public bool CanBlockProjectileWithUltimate()
        {
            return _context.ultimateProjectileBlockTimer > 0f;
        }

        public void ApplyHitstun(float duration)
        {
            if (duration <= 0f)
            {
                return;
            }

            _context.hitStunTimeLeft = Mathf.Max(_context.hitStunTimeLeft, duration);
        }

        public void ApplyKnockback(Vector2 direction, float force, float duration)
        {
            if (duration <= 0f || force <= 0f)
            {
                return;
            }

            _context.knockbackVelocity = direction.normalized * force;
            _context.knockbackTimeLeft = duration;
        }

        public bool Kill()
        {
            if (_context.isDead)
            {
                return false;
            }

            _context.isDead = true;
            _context.aimHoldActive = false;
            _context.shootHeldLastFrame = false;
            _context.dashTimeLeft = 0f;
            _context.dashVelocity = Vector2.zero;
            _context.lastDashVelocity = Vector2.zero;
            _context.meleeTimeLeft = 0f;
            _context.ultimateTimeLeft = 0f;
            _context.ultimateTotalDuration = 0f;
            _context.meleeAnimationTimeLeft = 0f;
            _context.shootAnimationTimeLeft = 0f;
            _context.jumpStartTimeLeft = 0f;
            _context.dashAnimationHoldTimeLeft = 0f;
            _context.ultimateAnimationTimeLeft = 0f;
            _context.jumpBufferLeft = 0f;
            _context.coyoteTimeLeft = 0f;
            _context.dashParryTimer = 0f;
            _context.dashPressTimer = 0f;
            _context.dashComboWindowLeft = 0f;
            _context.currentOverrideLockLeft = 0f;
            _context.pendingDashPrimary = false;
            _context.pendingDashSecondary = false;
            _context.ultimateCooldownLeft = 0f;
            _context.ultimateImpactApplied = false;
            _context.ultimateDashTimeLeft = 0f;
            _context.ultimateDashVelocity = Vector2.zero;
            _context.lastUltimateDashVelocity = Vector2.zero;
            _context.ultimateProjectileBlockTimer = 0f;
            _context.currentOverrideAction = string.Empty;
            _context.pendingOverrideAction = string.Empty;
            _context.activeColliderAction = string.Empty;
            _context.currentOverridePriority = -99999;
            _context.pendingOverridePriority = -99999;
            _context.pendingOverrideLockLeft = 0f;
            _context.actionLockEntries.Clear();

            if (_context.body != null)
            {
                _context.body.linearVelocity = Vector2.zero;
            }

            return true;
        }

        public void AddArrows(int amount)
        {
            _context.arrows = Mathf.Clamp(_context.arrows + amount, 0, _statResolver.ResolveMaxArrows());
        }

        public void ApplyEliminationHits(Collider2D[] hits, int hitCount)
        {
            ApplyUltimateDamageHits(hits, hitCount);
        }

        public void PlayActionSfx(string actionName)
        {
            if (string.IsNullOrWhiteSpace(actionName))
            {
                return;
            }

            if (_context.AudioController != null)
            {
                _context.AudioController.PlayAction(actionName);
            }
        }

        private void TriggerShootAnimation(float duration)
        {
            _context.shootAnimationTimeLeft = Mathf.Max(duration, 0f);
            _actionLockSystem.LockActionForDuration("shoot", duration, Mathf.Min(duration, 0.10f), true);
        }

        private ContactFilter2D GetMeleeContactFilter()
        {
            return new ContactFilter2D
            {
                useTriggers = false,
                useLayerMask = false
            };
        }

        private ContactFilter2D GetProjectileSeverContactFilter()
        {
            return new ContactFilter2D
            {
                useTriggers = true,
                useLayerMask = false
            };
        }
    }
}
