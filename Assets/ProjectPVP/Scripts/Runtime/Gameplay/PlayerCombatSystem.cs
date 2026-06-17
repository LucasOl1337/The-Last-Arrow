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
        private const float ContactArrowTransferLockDuration = 0.12f;

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
            if (_context.isDead || !frame.meleePressed || _context.meleeCooldownLeft > 0f || _context.meleeTimeLeft > 0f)
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
            if (_context.isDead || _context.meleeTimeLeft <= 0f)
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
                if (target == null || target == _context.Controller || target.IsDead || target.IsDodgeInvulnerable)
                {
                    continue;
                }

                int targetId = target.GetInstanceID();
                if (_context.meleeHitIds.Contains(targetId))
                {
                    continue;
                }

                _context.meleeHitIds.Add(targetId);
                target.Kill(_context.Controller, "Melee");
            }
        }

        public void HandleProjectileSeverDuringMelee()
        {
            if (_context.isDead)
            {
                return;
            }

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
            if (_context.isDead || !CanSeverProjectilesWithMelee() || hit == null)
            {
                return false;
            }

            ProjectileController projectile = hit.GetComponentInParent<ProjectileController>();
            if (projectile == null || projectile.IsStuck || projectile.IsDisarmed || IsOwnProjectileSource(projectile))
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
        /// Fires the held shot using the snapped 8-direction aim.
        /// The arrow leaves the bow in the chosen direction, then can receive a
        /// light in-flight seek if an opponent is already inside the aimed cone.
        /// </summary>
        public void FireHeldShot()
        {
            if (_context.isDead || _context.ProjectilePrefab == null || _context.shootCooldownLeft > 0f || _context.arrows <= 0)
                return;

            // ── 1. Resolve the snapped 8-directional aim ─────────────────────────────
            Vector2 aimDir8 = _context.aimHoldDirection.sqrMagnitude > 0.01f
                ? _context.aimHoldDirection.normalized
                : new Vector2(_context.facing >= 0 ? 1f : -1f, 0f);

            int shotFacing = ResolveShotFacing(aimDir8);
            if (shotFacing != _context.facing) _context.facing = shotFacing;

            Vector2 origin = GetProjectileSpawnPoint(aimDir8, shotFacing);
            Transform assistTarget = ResolveProjectileAssistTarget(origin, aimDir8);

            ProjectileController proj = ProjectileLauncher.Spawn(
                _context.ProjectilePrefab,
                _context.characterDefinition,
                _context.Controller.gameObject,
                origin,
                aimDir8,
                assistTarget,
                assistTarget != null && _statResolver.ResolveProjectileAssistEnabled(),
                _statResolver.ResolveProjectileAssistStrength(),
                _statResolver.ResolveProjectileAssistMaxTurnRateDeg(),
                Mathf.Clamp(_statResolver.ResolveProjectileAssistAcquireConeDeg(), 0f, 180f),
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
            if (candidate == null || candidate == _context.Controller || candidate.IsDead || candidate.IsDodgeInvulnerable)
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

            Vector2 inheritedVelocity = GetProjectileInheritedVelocity()
                * _statResolver.ResolveProjectileInheritVelocityFactor();
            if (!ProjectileTrajectoryMath.TryResolvePreferredTravelDirection(
                    origin,
                    candidateAimPoint,
                    arrowSpeed,
                    arrowGrav,
                    inheritedVelocity,
                    _context.Controller.groundMask,
                    out Vector2 preferredDirection))
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

        private static bool IsSameEightDirection(Vector2 a, Vector2 b)
        {
            if (a.sqrMagnitude <= 0.01f || b.sqrMagnitude <= 0.01f)
            {
                return false;
            }

            return Vector2.Dot(a.normalized, b.normalized) >= 0.999f;
        }

        public Vector2 GetProjectileInheritedVelocity()
        {
            return _context.body != null ? _context.body.linearVelocity : Vector2.zero;
        }

        public bool HandleArrowTransferOnContact()
        {
            if (_context.isDead || _context.contactArrowTransferLockTimeLeft > 0f || _context.bodyCollider == null || _context.Controller == null)
            {
                return false;
            }

            PlayerController.CopyActivePlayers(_playerQueryBuffer);
            PlayerController bestTarget = null;
            int bestTargetArrows = int.MaxValue;

            for (int index = 0; index < _playerQueryBuffer.Count; index += 1)
            {
                PlayerController candidate = _playerQueryBuffer[index];
                if (!CanReceiveArrowFromContact(candidate))
                {
                    continue;
                }

                if (!ArePlayersTouching(candidate))
                {
                    continue;
                }

                int candidateArrows = candidate.CurrentArrows;
                if (candidateArrows < bestTargetArrows)
                {
                    bestTargetArrows = candidateArrows;
                    bestTarget = candidate;
                }
            }

            _playerQueryBuffer.Clear();
            return bestTarget != null && TryTransferLastArrow(bestTarget);
        }

        private bool TryTransferLastArrow(PlayerController target)
        {
            if (target == null || target == _context.Controller || target.IsDead || target.bodyCollider == null)
            {
                return false;
            }

            // Any positive ammo lead can be converted into contact pressure.
            if (_context.arrows - target.CurrentArrows < 1)
            {
                return false;
            }

            if (!_context.bodyCollider.Distance(target.bodyCollider).isOverlapped)
            {
                return false;
            }

            _context.arrows = Mathf.Max(0, _context.arrows - 1);
            target.AddArrows(1);
            _context.contactArrowTransferLockTimeLeft = ContactArrowTransferLockDuration;
            target.LockContactArrowTransfer(ContactArrowTransferLockDuration);
            return true;
        }

        private bool CanReceiveArrowFromContact(PlayerController target)
        {
            return target != null
                && target != _context.Controller
                && !target.IsDead
                && !target.IsContactArrowTransferLocked
                && !target.HasShield
                && !target.IsDodgeInvulnerable
                && target.CurrentArrows < target.MaxArrows;
        }

        private bool ArePlayersTouching(PlayerController target)
        {
            if (target == null || target.bodyCollider == null || _context.bodyCollider == null)
            {
                return false;
            }

            return _context.bodyCollider.Distance(target.bodyCollider).isOverlapped;
        }

        public bool HandleIncomingProjectile(ProjectileController projectile, bool preserveParryEvent = false)
        {
            if (projectile == null || _context.isDead)
            {
                return false;
            }

            if (IsOwnProjectileSource(projectile))
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
                projectile.Stick(true);
                return true;
            }

            if (CanParryProjectile())
            {
                projectile.ReflectFromParry(_context.Controller != null ? _context.Controller.gameObject : null);
                if (!preserveParryEvent)
                {
                    projectile.ConsumeParryEvent();
                }

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

        private bool IsOwnProjectileSource(ProjectileController projectile)
        {
            if (_context.Controller == null || projectile == null || projectile.SourceObject == null)
            {
                return false;
            }

            PlayerController sourcePlayer = projectile.SourceObject.GetComponentInParent<PlayerController>();
            if (sourcePlayer != null)
            {
                return sourcePlayer == _context.Controller;
            }

            return projectile.SourceObject == _context.Controller.gameObject;
        }

        public bool TryCollectProjectile(ProjectileController projectile)
        {
            if (projectile == null || _context.isDead || !projectile.IsCollectible)
            {
                return false;
            }

            if (_context.arrows >= _statResolver.ResolveMaxArrows())
            {
                return false;
            }

            if (!projectile.TryConsumeCollectible())
            {
                return false;
            }

            AddArrows(1);
            return true;
        }

        public bool CanParryProjectile()
        {
            return !_context.isDead
                && (_context.dashParryTimer > 0f || _context.dashPressTimer > 0f)
                && (_context.Controller == null || !_context.Controller.HasShield);
        }

        private void ApplyProjectileHitReaction(ProjectileController projectile)
        {
            bool hadShield = _context.Controller != null && _context.Controller.HasShield;
            PlayerController sourcePlayer = projectile != null && projectile.SourceObject != null
                ? projectile.SourceObject.GetComponentInParent<PlayerController>()
                : null;
            bool killed = _context.Controller != null && _context.Controller.TryKill(sourcePlayer, "Projectile");
            if (projectile != null && (killed || hadShield))
            {
                projectile.Stick(true);
            }
        }

        public bool CanSeverIncomingProjectile(ProjectileController projectile)
        {
            if (!CanSeverProjectilesWithMelee()
                || !(_context.meleeTimeLeft > 0f)
                || projectile == null
                || projectile.IsStuck
                || projectile.IsDisarmed
                || IsOwnProjectileSource(projectile))
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
            if (_context.isDead || !frame.ultimatePressed || _context.ultimateCooldownLeft > 0f || _context.ultimateTimeLeft > 0f || !_statResolver.HasUltimateConfigured())
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
            if (_context.isDead || _context.ultimateTimeLeft <= 0f)
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
            if (_context.isDead)
            {
                return;
            }

            int hitCount = CollectCurrentUltimateHits(_context.overlapHits);
            ApplyUltimateDamageHits(_context.overlapHits, hitCount);
        }

        public Vector2 UpdateUltimateDashVelocity(float deltaTime)
        {
            if (_context.ultimateDashTimeLeft <= 0f)
            {
                return Vector2.zero;
            }

            float safeDeltaTime = Mathf.Max(0f, deltaTime);
            float appliedDashTime = safeDeltaTime > 0f
                ? Mathf.Min(_context.ultimateDashTimeLeft, safeDeltaTime)
                : 0f;
            Vector2 dashVelocity = safeDeltaTime > 0f
                ? _context.ultimateDashVelocity * (appliedDashTime / safeDeltaTime)
                : Vector2.zero;
            _context.ultimateDashTimeLeft = Mathf.Max(0f, _context.ultimateDashTimeLeft - safeDeltaTime);
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
            HashSet<int> appliedTargetIds = new HashSet<int>();
            for (int index = 0; index < hitCount; index += 1)
            {
                Collider2D hit = hits[index];
                if (hit == null)
                {
                    continue;
                }

                PlayerController target = hit.GetComponentInParent<PlayerController>();
                if (target == null || target == _context.Controller || target.IsDead || target.IsDodgeInvulnerable)
                {
                    continue;
                }

                int targetId = target.GetInstanceID();
                if (!appliedTargetIds.Add(targetId))
                {
                    continue;
                }

                target.Kill(_context.Controller, "Ultimate");
            }
        }

        public bool CanBlockProjectileWithUltimate()
        {
            return !_context.isDead && _context.ultimateProjectileBlockTimer > 0f;
        }

        public void ApplyHitstun(float duration)
        {
            if (_context.isDead || duration <= 0f)
            {
                return;
            }

            _context.hitStunTimeLeft = Mathf.Max(_context.hitStunTimeLeft, duration);
        }

        public void ApplyKnockback(Vector2 direction, float force, float duration)
        {
            if (_context.isDead || duration <= 0f || force <= 0f)
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
            _context.currentInputFrame = default;
            _context.aimHoldActive = false;
            _context.shootHeldLastFrame = false;
            _context.hitStunTimeLeft = 0f;
            _context.knockbackVelocity = Vector2.zero;
            _context.knockbackTimeLeft = 0f;
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
            _context.contactArrowTransferLockTimeLeft = 0f;
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
