using System;
using System.Collections;
using System.Collections.Generic;
using ProjectPVP.Audio;
using ProjectPVP.Data;
using ProjectPVP.Input;
using ProjectPVP.Match;
using ProjectPVP.Presentation;
using UnityEngine;
using UnityEngine.Serialization;

namespace ProjectPVP.Gameplay
{
    public sealed class PlayerController : MonoBehaviour, IAiArenaControllerSnapshotSource
    {
        private const float DeathFlashDuration = 0.08f;
        private const float DefaultFatalHitShakeIntensity = 0.08f;
        private const float DefaultFatalHitShakeDuration = 0.12f;
        private const float ProjectileFatalHitShakeIntensity = 0.09f;
        private const float ProjectileFatalHitShakeDuration = 0.13f;
        private const float HeadStompFatalHitShakeIntensity = 0.11f;
        private const float HeadStompFatalHitShakeDuration = 0.14f;
        private const float RingOutFatalHitShakeIntensity = 0.1f;
        private const float RingOutFatalHitShakeDuration = 0.16f;
        private const float UltimateFatalHitShakeIntensity = 0.13f;
        private const float UltimateFatalHitShakeDuration = 0.16f;
        private static readonly Color DeathFlashColor = new Color(1f, 0.58f, 0.26f, 1f);

        [Header("Identity")]
        [FormerlySerializedAs("playerId")]
        [Min(1)] public int slotId = 1;
        public CombatantSlotProfile slotProfile;
        public CharacterDefinition characterDefinition;

        [Header("References")]
        public MonoBehaviour inputSource;
        public Rigidbody2D body;
        public BoxCollider2D bodyCollider;
        public SpriteRenderer spriteRenderer;
        public CombatantAnchorRig anchorRig = new CombatantAnchorRig();
        public PlayerCombatAnchor spawnAnchor;
        public Transform projectileOrigin;
        public PlayerCombatAnchor meleeHitboxAnchor;
        public PlayerCombatAnchor ultimateHitboxAnchor;
        public ProjectileController projectilePrefab;

        [Header("Collision")]
        public LayerMask groundMask;
        public float groundCheckDistance = 8f;
        public float wallCheckDistance = 6f;

        public event Action<PlayerController> Died;

        private static PhysicsMaterial2D s_runtimeNoFrictionMaterial;
        private static readonly List<PlayerController> s_activePlayers = new();

        private PlayerContext _context;
        private PlayerStatResolver _statResolver;
        private PlayerCollisionSystem _collisionSystem;
        private PlayerMovementSystem _movementSystem;
        private PlayerJumpSystem _jumpSystem;
        private PlayerDashSystem _dashSystem;
        private PlayerActionLockSystem _actionLockSystem;
        private PlayerAnchorSystem _anchorSystem;
        private PlayerCombatSystem _combatSystem;
        private Coroutine _deathFlashRoutine;
        private Coroutine _deathNotifyRoutine;
        private bool _externalControlLock;
        private bool _inputSourceEnabledBeforeExternalLock = true;

        public int playerId
        {
            get => slotId;
            set => slotId = value;
        }

        public CombatantSlotId SlotId => CombatantSlotIdUtility.FromInt(slotId);
        public CombatantSlotProfile SlotProfile => slotProfile;
        public string BotId => slotProfile != null ? slotProfile.ResolveBotId(SlotId) : string.Empty;
        public string BotDisplayName => slotProfile != null ? slotProfile.ResolveBotDisplayName(SlotId) : SlotId.ToDisplayName();
        public int Facing => _context != null && _context.facing != 0 ? _context.facing : 1;
        public int CurrentArrows => _context != null ? _context.arrows : 0;
        public int MaxArrows => _statResolver != null ? _statResolver.ResolveMaxArrows() : 0;
        public bool IsDead => _context != null && _context.isDead;
        public bool IsGrounded => _context != null && _context.isGrounded;
        public bool IsTouchingWall => _context != null && _context.isTouchingWall;
        public bool IsDashing => _context != null && !_context.isDead && _context.dashTimeLeft > 0f;
        public bool IsDashAnimationActive => IsDashing || (_context != null && !_context.isDead && _context.dashAnimationHoldTimeLeft > 0f);
        public bool IsAimHoldActive => _context != null && !_context.isDead && _context.aimHoldActive;
        public bool IsMeleeActive => _context != null && !_context.isDead && _context.meleeTimeLeft > 0f;
        public bool IsShootAnimating => _context != null && !_context.isDead && _context.shootAnimationTimeLeft > 0f;
        public bool IsJumpStartActive => _context != null && !_context.isDead && _context.jumpStartTimeLeft > 0f;
        public bool IsUltimateActive => _context != null && !_context.isDead && _context.ultimateTimeLeft > 0f;
        public bool HasShield => _context != null && _context.hasShield;
        public bool IsHitStunned => _context != null && !_context.isDead && _context.hitStunTimeLeft > 0f;
        public bool IsKnockedBack => _context != null && !_context.isDead && _context.knockbackTimeLeft > 0f;
        public bool IsExternallyControlLocked => _externalControlLock;
        internal bool IsContactArrowTransferLocked => _context != null && _context.contactArrowTransferLockTimeLeft > 0f;
        public float ShootCooldownLeft => _context != null ? _context.shootCooldownLeft : 0f;
        public float MeleeCooldownLeft => _context != null ? _context.meleeCooldownLeft : 0f;
        public float DashPrimaryCooldownLeft => _context != null ? _context.dashPrimaryCooldownLeft : 0f;
        public float DashSecondaryCooldownLeft => _context != null ? _context.dashSecondaryCooldownLeft : 0f;
        public float DashCooldownLeft => _context != null ? Mathf.Min(_context.dashPrimaryCooldownLeft, _context.dashSecondaryCooldownLeft) : 0f;
        public float UltimateCooldownLeft => _context != null ? _context.ultimateCooldownLeft : 0f;
        public float HitStunTimeLeft => _context != null ? _context.hitStunTimeLeft : 0f;
        public float DeathFlashTimeLeft => _context != null ? _context.deathFlashTimeLeft : 0f;
        public float UltimateProjectileBlockTimeLeft => _context != null && !_context.isDead ? _context.ultimateProjectileBlockTimer : 0f;
        public Vector2 AimHoldDirection => _context != null ? _context.aimHoldDirection : Vector2.zero;
        public bool IsDodgeInvulnerable => _context != null && !_context.isDead && (_context.dashParryTimer > 0f || _context.dashPressTimer > 0f);
        public Vector2 CurrentVelocity => body != null ? body.linearVelocity : Vector2.zero;
        public float HorizontalVelocity => body != null ? body.linearVelocity.x : 0f;
        public float VerticalVelocity => body != null ? body.linearVelocity.y : 0f;
        public Vector2 RootPosition => body != null ? body.position : (Vector2)transform.position;
        public SpriteRenderer VisualSpriteRenderer => spriteRenderer;
        public Vector2 ConfiguredSpawnWorldPosition => _movementSystem != null ? _movementSystem.ResolveConfiguredSpawnWorldPosition() : (Vector2)transform.position;
        public Vector2 ProjectileOriginWorldPosition => _anchorSystem != null ? _anchorSystem.ResolveProjectileOriginWorldPosition(Facing) : (Vector2)transform.position;
        public Vector2 MeleeHitboxCenter => _anchorSystem != null ? _anchorSystem.GetMeleeHitboxCenter() : Vector2.zero;
        public Vector2 MeleeHitboxSize => _anchorSystem != null ? _anchorSystem.GetMeleeHitboxSize() : Vector2.zero;
        public Vector2 UltimateHitboxCenter => _anchorSystem != null ? _anchorSystem.GetUltimateHitboxCenter() : Vector2.zero;
        public float UltimateHitboxRadius => _statResolver != null ? _statResolver.ResolveUltimateRadius() : 0f;
        public float ResolvedUltimateDashDistance => _statResolver != null ? _statResolver.ResolveUltimateDashDistance() : 0f;
        public float ResolvedUltimateDashDuration => _statResolver != null ? _statResolver.ResolveUltimateDashDuration() : 0f;
        public float ResolvedUltimateReplayDelay => _statResolver != null ? _statResolver.ResolveUltimateReplayDelay() : 0f;
        public float ResolvedUltimateReplayDuration => _statResolver != null ? _statResolver.ResolveUltimateReplayDuration() : 0f;
        public float DashParryTimeLeft => _context != null && !_context.isDead ? _context.dashParryTimer : 0f;
        public float DashPressTimeLeft => _context != null && !_context.isDead ? _context.dashPressTimer : 0f;
        public PlayerInputFrame CurrentInputFrame => _context != null ? _context.currentInputFrame : default;
        public string CurrentVisualActionKey => _actionLockSystem != null ? _actionLockSystem.CurrentVisualActionKey : string.Empty;
        public ICombatantInputSource InputSource => _context != null ? _context.RuntimeInputSource : null;
        public ProjectileController LastLaunchedProjectile => _context != null ? _context.lastLaunchedProjectile : null;
        public PlayerController LastFatalHitSource => _context != null ? _context.lastFatalHitSource : null;
        public string LastFatalHitCause => _context != null ? _context.lastFatalHitCause : string.Empty;
        public Vector2 LastFatalHitPosition => _context != null ? _context.lastFatalHitPosition : RootPosition;
        public string LastFatalHitSummary => ResolveLastFatalHitSummary();
        public bool CanParryProjectile => _combatSystem != null && _combatSystem.CanParryProjectile();
        public bool CanBlockProjectileWithUltimate => _combatSystem != null && _combatSystem.CanBlockProjectileWithUltimate();
        public float ProjectileInheritVelocityFactor => _statResolver != null
            ? _statResolver.ResolveProjectileInheritVelocityFactor()
            : characterDefinition != null
                ? Mathf.Max(0f, characterDefinition.projectileInheritVelocityFactor)
                : 1f;
        public CombatantRuntimeContext RuntimeContext => new CombatantRuntimeContext(SlotId, this, characterDefinition, anchorRig, _context != null ? _context.RuntimeInputSource : null);

        public static void CopyActivePlayers(List<PlayerController> results)
        {
            if (results == null)
            {
                throw new ArgumentNullException(nameof(results));
            }

            results.Clear();
            PruneDestroyedActivePlayers();
            for (int index = 0; index < s_activePlayers.Count; index += 1)
            {
                results.Add(s_activePlayers[index]);
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ClearActivePlayersForRuntimeLoad()
        {
            s_activePlayers.Clear();
        }

        private static void RegisterActivePlayer(PlayerController player)
        {
            if (player == null || s_activePlayers.Contains(player))
            {
                return;
            }

            s_activePlayers.Add(player);
        }

        private static void UnregisterActivePlayer(PlayerController player)
        {
            if (player == null)
            {
                return;
            }

            s_activePlayers.Remove(player);
        }

        private static void PruneDestroyedActivePlayers()
        {
            for (int index = s_activePlayers.Count - 1; index >= 0; index -= 1)
            {
                if (s_activePlayers[index] == null)
                {
                    s_activePlayers.RemoveAt(index);
                }
            }
        }

        private static void ClearActivePlayersForTests()
        {
            s_activePlayers.Clear();
        }

        public AiArenaControllerSnapshot BuildAiArenaControllerSnapshot(int fallbackSlotId, Vector2 fallbackPosition)
        {
            Vector2 position = body != null ? body.position : (Vector2)transform.position;
            int resolvedSlotId = slotId > 0 ? slotId : fallbackSlotId;
            int resolvedFacing = _context != null && _context.facing != 0
                ? _context.facing
                : (position.x >= fallbackPosition.x ? 1 : -1);
            bool isDead = _context != null && _context.isDead;

            return new AiArenaControllerSnapshot
            {
                isValid = true,
                slotId = resolvedSlotId,
                botId = BotId,
                botDisplayName = BotDisplayName,
                characterId = characterDefinition != null ? characterDefinition.id : string.Empty,
                displayName = name,
                actionKey = _actionLockSystem != null ? _actionLockSystem.CurrentVisualActionKey : string.Empty,
                isDead = isDead,
                isGrounded = _context == null || _context.isGrounded,
                isTouchingWall = _context != null && _context.isTouchingWall,
                isDashing = !isDead && _context != null && _context.dashTimeLeft > 0f,
                isMeleeActive = !isDead && _context != null && _context.meleeTimeLeft > 0f,
                isShootAnimating = !isDead && _context != null && _context.shootAnimationTimeLeft > 0f,
                isUltimateActive = !isDead && _context != null && _context.ultimateTimeLeft > 0f,
                isHitStunned = !isDead && _context != null && _context.hitStunTimeLeft > 0f,
                canParryProjectile = _combatSystem != null && _combatSystem.CanParryProjectile(),
                canBlockProjectiles = _combatSystem != null && _combatSystem.CanBlockProjectileWithUltimate(),
                arrows = _context != null ? _context.arrows : 0,
                facing = resolvedFacing,
                projectileInheritVelocityFactor = ProjectileInheritVelocityFactor,
                shootCooldownLeft = _context != null ? _context.shootCooldownLeft : 0f,
                meleeCooldownLeft = _context != null ? _context.meleeCooldownLeft : 0f,
                dashCooldownLeft = _context != null ? Mathf.Min(_context.dashPrimaryCooldownLeft, _context.dashSecondaryCooldownLeft) : 0f,
                ultimateCooldownLeft = _context != null ? _context.ultimateCooldownLeft : 0f,
                hitStunTimeLeft = !isDead && _context != null ? _context.hitStunTimeLeft : 0f,
                position = position,
                velocity = body != null ? body.linearVelocity : Vector2.zero,
                meleeHitboxCenter = _anchorSystem != null ? _anchorSystem.GetMeleeHitboxCenter() : position,
                meleeHitboxSize = _anchorSystem != null ? _anchorSystem.GetMeleeHitboxSize() : Vector2.zero,
                ultimateHitboxCenter = _anchorSystem != null ? _anchorSystem.GetUltimateHitboxCenter() : position,
                ultimateHitboxRadius = _statResolver != null ? _statResolver.ResolveUltimateRadius() : 0f,
            };
        }

        private void Reset()
        {
            CacheReferences();
        }

        private void OnValidate()
        {
            if (Application.isPlaying)
            {
                return;
            }

            CacheReferences();
            RebuildCharacterMechanicsRuntime();
            ApplyDefinitionToCollider();
            ApplyCharacterVisuals();
        }

        private void Awake()
        {
            InitializeContext();
            CacheReferences();
            EnsureAudioController();
            RebuildCharacterMechanicsRuntime();
            ConfigureInput();
            ConfigureRuntimeBody();
            EnsureFrictionlessColliderMaterial();
            ApplyDefinitionToCollider();
            ApplyCharacterVisuals();
            _anchorSystem.SyncCombatAnchors();
            ResetRuntimeState();
            _collisionSystem.RefreshCollisionState();
        }

        private void OnEnable()
        {
            if (_context == null)
            {
                InitializeContext();
            }

            CacheReferences();
            EnsureAudioController();
            RebuildCharacterMechanicsRuntime();

            if (!Application.isPlaying)
            {
                ApplyDefinitionToCollider();
                ApplyCharacterVisuals();
                _anchorSystem.SyncCombatAnchors();
                return;
            }

            ConfigureInput();
            ConfigureRuntimeBody();
            _anchorSystem.SyncCombatAnchors();
            if (_externalControlLock)
            {
                ApplyExternalControlLockState();
            }
            RegisterActivePlayer(this);
            AiArenaSnapshotSourceRegistry.Register(this);
        }

        private void OnDisable()
        {
            _context?.RuntimeInputSource?.ResetInputState();
            UnregisterActivePlayer(this);
            AiArenaSnapshotSourceRegistry.Unregister(this);
            StopDeathNotifyRoutine();
        }

        private void InitializeContext()
        {
            _context = new PlayerContext
            {
                transform = transform,
                Controller = this,
            };

            _statResolver = new PlayerStatResolver(_context);
            _collisionSystem = new PlayerCollisionSystem(_context);
            _movementSystem = new PlayerMovementSystem(_context, _statResolver, _collisionSystem);
            _jumpSystem = new PlayerJumpSystem(_context, _statResolver, _collisionSystem, _movementSystem);
            _dashSystem = new PlayerDashSystem(_context, _statResolver);
            _actionLockSystem = new PlayerActionLockSystem(_context, _statResolver);
            _anchorSystem = new PlayerAnchorSystem(_context, _statResolver);
            _combatSystem = new PlayerCombatSystem(_context, _statResolver, _anchorSystem, _actionLockSystem);
        }

        private void FixedUpdate()
        {
            if (_context.isDead || body == null)
            {
                return;
            }

            if (_externalControlLock)
            {
                ApplyExternalControlLockState();
                return;
            }

            EnsureCharacterMechanicsRuntime();
            CaptureInputFrame();

            float deltaTime = Time.fixedDeltaTime;
            TickCooldowns(deltaTime);
            _collisionSystem.RefreshCollisionState();

            if (_context.hitStunTimeLeft > 0f)
            {
                _context.hitStunTimeLeft -= deltaTime;
            }

            if (_context.knockbackTimeLeft > 0f)
            {
                _context.knockbackTimeLeft -= deltaTime;
            }

            if (IsHitStunned)
            {
                Vector2 stunVelocity = body.linearVelocity;

                _jumpSystem.HandleJumpAndGravity(_context.currentInputFrame, deltaTime, ref stunVelocity);

                if (IsKnockedBack)
                {
                    stunVelocity += _context.knockbackVelocity * (deltaTime / 0.2f);
                }

                _movementSystem.MoveCharacter(ref stunVelocity, deltaTime);
                body.linearVelocity = stunVelocity;
                _collisionSystem.RefreshCollisionState();
                UpdatePresentationState();
                return;
            }

            if (_context.isGrounded)
            {
                _context.dashJumpUsed = false;
                _context.coyoteTimeLeft = 0.16f;
            }

            if (_context.currentInputFrame.jumpPressed)
            {
                _context.jumpBufferLeft = 0.16f;
            }

            bool shootReleasedThisFrame = _movementSystem.UpdateAimHoldState(_context.currentInputFrame);
            _movementSystem.UpdateFacing(_context.currentInputFrame);
            _anchorSystem.SyncCombatAnchors();

            Vector2 velocity = body.linearVelocity;
            Vector2 previousDashVelocity = _context.lastDashVelocity;
            Vector2 previousUltimateDashVelocity = _context.lastUltimateDashVelocity;
            velocity -= previousDashVelocity;
            velocity -= previousUltimateDashVelocity;

            _movementSystem.HandleMovement(_context.currentInputFrame, deltaTime, ref velocity);
            _jumpSystem.HandleJumpAndGravity(_context.currentInputFrame, deltaTime, ref velocity);
            _dashSystem.TryStartDash(_context.currentInputFrame);

            Vector2 dashVelocity = _dashSystem.UpdateDashVelocity(deltaTime, ref velocity);
            _dashSystem.ApplyTransientVelocity(ref velocity, previousDashVelocity, dashVelocity, ref _context.lastDashVelocity);

            Vector2 ultimateDashVelocity = _combatSystem.UpdateUltimateDashVelocity(deltaTime);
            _dashSystem.ApplyTransientVelocity(ref velocity, previousUltimateDashVelocity, ultimateDashVelocity, ref _context.lastUltimateDashVelocity);

            if (IsKnockedBack)
            {
                velocity += _context.knockbackVelocity * (deltaTime / 0.2f);
            }

            _movementSystem.MoveCharacter(ref velocity, deltaTime);
            body.linearVelocity = velocity;
            _collisionSystem.RefreshCollisionState();
            _combatSystem.HandleArrowTransferOnContact();

            if (shootReleasedThisFrame)
            {
                _combatSystem.FireHeldShot();
            }

            _combatSystem.TryUseMelee(_context.currentInputFrame);
            _combatSystem.HandleActiveMelee();
            _combatSystem.TryUseUltimate(_context.currentInputFrame);
            _combatSystem.HandleActiveUltimate(deltaTime);
            _context.CharacterMechanicsRuntime?.OnTick(deltaTime);
            _jumpSystem.TryCheckHeadStomp();
            _actionLockSystem.ApplyRuntimeColliderOverride(CurrentVisualActionKey);
            UpdatePresentationState();
        }

        private void Update()
        {
            if (_context == null || _context.RuntimeInputSource == null || _externalControlLock)
            {
                return;
            }

            PlayerInputFrame liveFrame = _context.RuntimeInputSource.CurrentFrame;
            if (_context.aimHoldActive && liveFrame.aim.sqrMagnitude > 0.01f)
            {
                _context.aimHoldDirection = PlayerMovementSystem.Snap8Dir(liveFrame.aim);
            }
        }

        public void SetSpawnPosition(Vector2 worldPosition)
        {
            EnsureCharacterMechanicsRuntime();
            _movementSystem.SetSpawnPosition(worldPosition);
            _context.isDead = false;
            ApplyCharacterVisuals();
            _anchorSystem.SyncCombatAnchors();
            ResetRuntimeState();
            _collisionSystem.RefreshCollisionState();
            _movementSystem.SnapToGroundAtSpawn(worldPosition);
            _collisionSystem.RefreshCollisionState();
            _context.CharacterMechanicsRuntime?.OnSpawned();
        }

        public void SetExternalControlLock(bool locked)
        {
            if (_externalControlLock == locked)
            {
                if (locked && _context != null)
                {
                    _context.RuntimeInputSource?.ResetInputState();
                    if (inputSource != null)
                    {
                        inputSource.enabled = false;
                    }

                    ApplyExternalControlLockState();
                }

                return;
            }

            _externalControlLock = locked;
            if (_context == null)
            {
                return;
            }

            if (locked)
            {
                _context.RuntimeInputSource?.ResetInputState();
                if (inputSource != null)
                {
                    _inputSourceEnabledBeforeExternalLock = inputSource.enabled;
                    inputSource.enabled = false;
                }

                ApplyExternalControlLockState();
                return;
            }

            if (inputSource != null)
            {
                inputSource.enabled = _inputSourceEnabledBeforeExternalLock;
            }
        }

        public void SetRoundShield(bool enabled)
        {
            if (_context == null)
            {
                return;
            }

            _context.hasShield = enabled;
            UpdatePresentationState();
        }

        public void AddArrows(int amount)
        {
            _combatSystem.AddArrows(amount);
        }

        public void SetRoundArrowCount(int amount)
        {
            EnsureCharacterMechanicsRuntime();
            if (_context == null)
            {
                return;
            }

            _context.arrows = Mathf.Clamp(amount, 0, _statResolver.ResolveMaxArrows());
        }

        internal void LockContactArrowTransfer(float duration)
        {
            EnsureCharacterMechanicsRuntime();
            if (_context == null || duration <= 0f)
            {
                return;
            }

            _context.contactArrowTransferLockTimeLeft = Mathf.Max(_context.contactArrowTransferLockTimeLeft, duration);
        }

        public void Kill()
        {
            TryKill();
        }

        public void Kill(PlayerController source, string cause = "")
        {
            TryKill(source, cause);
        }

        public bool TryKill()
        {
            return TryKill(null, string.Empty);
        }

        public bool TryKill(PlayerController source, string cause = "")
        {
            EnsureCharacterMechanicsRuntime();
            if (CanShieldAbsorbFatalHit(cause))
            {
                _context.hasShield = false;
                UpdatePresentationState();
                return false;
            }

            if (!_combatSystem.Kill())
            {
                return false;
            }

            RecordFatalHit(source, cause);
            ApplyDefinitionToCollider();
            BeginDeathFlash();
            ResolveFatalHitCameraShake(cause, out float shakeIntensity, out float shakeDuration);
            ProjectPvpCameraShake.TryShakeDefault(shakeIntensity, shakeDuration);
            UpdatePresentationState();
            _combatSystem.PlayActionSfx("death");
            float deathEventDelay = characterDefinition != null && characterDefinition.HasActionAnimation("death")
                ? Mathf.Clamp(_statResolver.ResolveActionDuration("death", 0.35f), 0.2f, 0.6f)
                : 0f;
            StopDeathNotifyRoutine();
            _deathNotifyRoutine = StartCoroutine(NotifyDeathAfterDelay(deathEventDelay));
            return true;
        }

        internal static void ResolveFatalHitCameraShake(string cause, out float intensity, out float duration)
        {
            intensity = DefaultFatalHitShakeIntensity;
            duration = DefaultFatalHitShakeDuration;

            if (string.IsNullOrWhiteSpace(cause))
            {
                return;
            }

            string normalizedCause = cause.Trim();
            if (string.Equals(normalizedCause, "Projectile", StringComparison.OrdinalIgnoreCase))
            {
                intensity = ProjectileFatalHitShakeIntensity;
                duration = ProjectileFatalHitShakeDuration;
                return;
            }

            if (string.Equals(normalizedCause, "Head Stomp", StringComparison.OrdinalIgnoreCase))
            {
                intensity = HeadStompFatalHitShakeIntensity;
                duration = HeadStompFatalHitShakeDuration;
                return;
            }

            if (string.Equals(normalizedCause, "Ring Out", StringComparison.OrdinalIgnoreCase))
            {
                intensity = RingOutFatalHitShakeIntensity;
                duration = RingOutFatalHitShakeDuration;
                return;
            }

            if (string.Equals(normalizedCause, "Ultimate", StringComparison.OrdinalIgnoreCase))
            {
                intensity = UltimateFatalHitShakeIntensity;
                duration = UltimateFatalHitShakeDuration;
            }
        }

        public void ApplyHitstun(float duration)
        {
            _combatSystem.ApplyHitstun(duration);
        }

        public void ApplyKnockback(Vector2 direction, float force, float duration)
        {
            _combatSystem.ApplyKnockback(direction, force, duration);
        }

        public bool HandleIncomingProjectile(ProjectileController projectile, bool preserveParryEvent = false)
        {
            return _combatSystem.HandleIncomingProjectile(projectile, preserveParryEvent);
        }

        public void ReceiveProjectile(ProjectileController projectile)
        {
            _combatSystem.ReceiveProjectile(projectile);
        }

        public bool TryCollectProjectile(ProjectileController projectile)
        {
            return _combatSystem.TryCollectProjectile(projectile);
        }

        public Vector2 GetProjectileInheritedVelocity()
        {
            return _combatSystem.GetProjectileInheritedVelocity();
        }

        public void AssignCharacterDefinition(CharacterDefinition definition)
        {
            if (definition == null || definition == characterDefinition)
            {
                return;
            }

            characterDefinition = definition;
            RebuildCharacterMechanicsRuntime();
            ApplyDefinitionToCollider();
            ApplyCharacterVisuals();
            _anchorSystem.SyncCombatAnchors();
        }

        public void AssignSlotProfile(CombatantSlotProfile profile)
        {
            if (slotProfile == profile)
            {
                return;
            }

            slotProfile = profile;
            ConfigureInput();
        }

        public int ResolveFacingDirection()
        {
            return _context.facing == 0 ? 1 : (_context.facing > 0 ? 1 : -1);
        }

        public Vector2 ResolveCurrentDashDirection()
        {
            return _dashSystem.ResolveDashDirection();
        }

        public bool TryCaptureUltimateHitShapeSnapshot(out CombatShapeSnapshot snapshot)
        {
            return _anchorSystem.TryCaptureUltimateHitShapeSnapshot(out snapshot);
        }

        public int CollectHitsForShape(CombatShapeSnapshot shape, Collider2D[] results)
        {
            return _anchorSystem.CollectHitsForShape(shape, results);
        }

        public void ApplyEliminationHits(Collider2D[] hits, int hitCount)
        {
            _combatSystem.ApplyEliminationHits(hits, hitCount);
        }

        public bool TryResolveActionAnimationSelection(string actionName, int facingDirection, out ActionSpriteAnimation resolvedAnimation, out bool resolvedFlipX)
        {
            return CharacterAnimationResolver.TryResolveActionAnimationSelection(characterDefinition, actionName, facingDirection, out resolvedAnimation, out resolvedFlipX);
        }

        private float ResolveMoveSpeed()
        {
            return _statResolver.ResolveMoveSpeed();
        }

        private int ResolveMaxArrows()
        {
            return _statResolver.ResolveMaxArrows();
        }

        private float ResolveMeleeDuration()
        {
            return _statResolver.ResolveMeleeDuration();
        }

        private Transform ResolveProjectileAssistTarget(Vector2 origin, Vector2 initialDirection)
        {
            return _combatSystem.ResolveProjectileAssistTarget(origin, initialDirection);
        }

        public static Sprite ResolveAnimationFrame(ActionSpriteAnimation animation, float progress, Sprite fallbackSprite = null)
        {
            if (animation == null || animation.frames == null || animation.frames.Count == 0)
            {
                return fallbackSprite;
            }

            int frameCount = animation.frames.Count;
            int frameIndex = Mathf.Clamp(Mathf.FloorToInt(Mathf.Clamp01(progress) * frameCount), 0, frameCount - 1);
            Sprite resolvedFrame = animation.frames[frameIndex];
            if (resolvedFrame != null)
            {
                return resolvedFrame;
            }

            for (int index = 0; index < frameCount; index += 1)
            {
                if (animation.frames[index] != null)
                {
                    return animation.frames[index];
                }
            }

            return fallbackSprite;
        }

        public static void DrawShapeSnapshotGizmo(CombatShapeSnapshot shape, Color color)
        {
            PlayerGizmosSystem.DrawShapeSnapshotGizmo(shape, color);
        }

        private void RebuildCharacterMechanicsRuntime()
        {
            if (_context == null)
            {
                return;
            }
            _context.CharacterMechanicsRuntime = null;
            _context.CharacterMechanicsSource = null;
            EnsureCharacterMechanicsRuntime();
        }

        private void EnsureCharacterMechanicsRuntime()
        {
            CharacterMechanicsModule mechanicsModule = characterDefinition.ResolveMechanicsModule();

            if (mechanicsModule == null)
            {
                _context.CharacterMechanicsRuntime = null;
                _context.CharacterMechanicsSource = null;
                return;
            }

            if (_context.CharacterMechanicsRuntime != null && _context.CharacterMechanicsSource == mechanicsModule)
            {
                return;
            }

            _context.CharacterMechanicsSource = mechanicsModule;
            _context.CharacterMechanicsRuntime = mechanicsModule.CreateRuntime(this, characterDefinition);
        }

        private void ConfigureRuntimeBody()
        {
            if (body == null)
            {
                return;
            }

            body.bodyType = RigidbodyType2D.Kinematic;
            body.useFullKinematicContacts = true;
            body.gravityScale = 0f;
            body.freezeRotation = true;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        }

        private void CacheReferences()
        {
            if (body == null)
            {
                body = GetComponent<Rigidbody2D>();
            }

            if (bodyCollider == null)
            {
                bodyCollider = GetComponent<BoxCollider2D>();
            }

            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            }

            ResolveInputSourceComponent();

            if (spawnAnchor == null)
            {
                spawnAnchor = _anchorSystem?.FindChildAnchor("SpawnAnchor", PlayerCombatAnchorKind.Spawn);
            }

            if (projectileOrigin == null)
            {
                Transform existingProjectileOrigin = transform.Find("ProjectileOrigin");
                if (existingProjectileOrigin != null)
                {
                    projectileOrigin = existingProjectileOrigin;
                }
            }

            if (meleeHitboxAnchor == null)
            {
                meleeHitboxAnchor = _anchorSystem?.FindChildAnchor("MeleeHitbox", PlayerCombatAnchorKind.MeleeHitbox);
            }

            if (ultimateHitboxAnchor == null)
            {
                ultimateHitboxAnchor = _anchorSystem?.FindChildAnchor("UltimateHitbox", PlayerCombatAnchorKind.UltimateHitbox);
            }

            anchorRig ??= new CombatantAnchorRig();
            anchorRig.SyncFromLegacy(spawnAnchor, projectileOrigin, meleeHitboxAnchor, ultimateHitboxAnchor);
            anchorRig.SyncLegacy(ref spawnAnchor, ref projectileOrigin, ref meleeHitboxAnchor, ref ultimateHitboxAnchor);

            if (_context != null)
            {
                _context.body = body;
                _context.bodyCollider = bodyCollider;
                _context.spriteRenderer = spriteRenderer;
                _context.spawnAnchor = spawnAnchor;
                _context.projectileOrigin = projectileOrigin;
                _context.meleeHitboxAnchor = meleeHitboxAnchor;
                _context.ultimateHitboxAnchor = ultimateHitboxAnchor;
                _context.ProjectilePrefab = projectilePrefab;
                _context.anchorRig = anchorRig;
                _context.characterDefinition = characterDefinition;
                _context.slotProfile = slotProfile;
                _context.slotId = slotId;
                _context.groundMask = groundMask;
                _context.groundCheckDistance = groundCheckDistance;
                _context.wallCheckDistance = wallCheckDistance;
            }
        }

        private void ResolveInputSourceComponent()
        {
            if (inputSource is ICombatantInputSource configuredSource)
            {
                if (_context != null)
                {
                    _context.RuntimeInputSource = configuredSource;
                }
                return;
            }

            MonoBehaviour preferredSource = null;
            MonoBehaviour[] behaviours = GetComponents<MonoBehaviour>();
            for (int index = 0; index < behaviours.Length; index += 1)
            {
                MonoBehaviour behaviour = behaviours[index];
                if (behaviour == null || behaviour is not ICombatantInputSource)
                {
                    continue;
                }

                preferredSource = behaviour;
                if (behaviour is InputSystemCombatantInputSource)
                {
                    break;
                }
            }

            if (preferredSource != null)
            {
                inputSource = preferredSource;
            }

            if (_context != null)
            {
                _context.RuntimeInputSource = inputSource as ICombatantInputSource;
            }
        }

        private void EnsurePreferredInputSource()
        {
            CombatantControlMode controlMode = slotProfile != null
                ? slotProfile.ResolveControlMode()
                : CombatantControlMode.Human;

            if (Application.isPlaying)
            {
                Debug.Log($"[CodexBot] PlayerController slot {slotId} resolving input source mode={controlMode} brain={(slotProfile != null ? slotProfile.ResolveAiBrain().ToString() : "<none>")}");
            }

            switch (controlMode)
            {
                case CombatantControlMode.AI:
                    SetActiveInputSource(ResolveAiInputSource());
                    return;
                case CombatantControlMode.Idle:
                    SetActiveInputSource(EnsureIdleInputSource());
                    return;
            }

            ResolveInputSourceComponent();

            if (!Application.isPlaying)
            {
                KeyboardPlayerInputSource editorKeyboardInput = GetComponent<KeyboardPlayerInputSource>();
                if (editorKeyboardInput != null)
                {
                    SetActiveInputSource(editorKeyboardInput);
                }

                return;
            }

            if (inputSource is InputSystemCombatantInputSource systemSource)
            {
                SetActiveInputSource(systemSource);
                return;
            }

            if (!InputSystemCombatantInputSource.IsNativeInputSystemAvailable)
            {
                KeyboardPlayerInputSource legacyInput = GetComponent<KeyboardPlayerInputSource>();
                if (legacyInput != null)
                {
                    SetActiveInputSource(legacyInput);
                    return;
                }

                legacyInput = gameObject.AddComponent<KeyboardPlayerInputSource>();
                SetActiveInputSource(legacyInput);
                return;
            }

            InputSystemCombatantInputSource systemInput = GetComponent<InputSystemCombatantInputSource>();
            if (systemInput == null)
            {
                systemInput = gameObject.AddComponent<InputSystemCombatantInputSource>();
                systemInput.useSlotDefaults = true;
            }

            SetActiveInputSource(systemInput);
        }

        private void EnsureAudioController()
        {
            if (_context.AudioController == null)
            {
                _context.AudioController = GetComponent<CharacterAudioController>();
            }

            if (_context.AudioController == null && Application.isPlaying)
            {
                _context.AudioController = gameObject.AddComponent<CharacterAudioController>();
            }

            if (_context.AudioController != null)
            {
                _context.AudioController.player = this;
            }
        }

        private void ConfigureInput()
        {
            EnsurePreferredInputSource();
            ApplySlotProfileToInputSource();

            if (_context.RuntimeInputSource != null)
            {
                _context.RuntimeInputSource.ConfigureForSlot(SlotId);
            }
        }

        private void ApplySlotProfileToInputSource()
        {
            if (inputSource is KeyboardPlayerInputSource configurableInput
                && (slotProfile == null || slotProfile.ResolveControlMode() == CombatantControlMode.Human))
            {
                configurableInput.ApplySlotProfile(slotProfile, SlotId);
                _context.RuntimeInputSource = configurableInput;
            }
        }

        private LocalAiCombatantInputSource EnsureLocalAiInputSource()
        {
            LocalAiCombatantInputSource aiInput = GetComponent<LocalAiCombatantInputSource>();
            if (aiInput == null && Application.isPlaying)
            {
                aiInput = gameObject.AddComponent<LocalAiCombatantInputSource>();
            }

            if (aiInput != null)
            {
                aiInput.ConfigureForSlot(SlotId);
            }

            return aiInput;
        }

        private MonoBehaviour ResolveAiInputSource()
        {
            AiBrainKind aiBrain = slotProfile != null
                ? slotProfile.ResolveAiBrain()
                : AiBrainKind.LocalHeuristic;

            if (Application.isPlaying)
            {
                Debug.Log($"[CodexBot] PlayerController slot {slotId} selecting AI source brain={aiBrain}");
            }

            return aiBrain == AiBrainKind.CodexBroker
                ? (MonoBehaviour)EnsureCodexBrokerInputSource()
                : EnsureLocalAiInputSource();
        }

        private CodexBrokerCombatantInputSource EnsureCodexBrokerInputSource()
        {
            CodexBrokerCombatantInputSource brokerInput = GetComponent<CodexBrokerCombatantInputSource>();
            if (brokerInput == null && Application.isPlaying)
            {
                brokerInput = gameObject.AddComponent<CodexBrokerCombatantInputSource>();
                Debug.Log($"[CodexBot] PlayerController slot {slotId} added CodexBrokerCombatantInputSource.");
            }

            if (brokerInput != null)
            {
                brokerInput.ConfigureForSlot(SlotId);
                Debug.Log($"[CodexBot] PlayerController slot {slotId} configured CodexBrokerCombatantInputSource.");
            }

            return brokerInput;
        }

        private IdleCombatantInputSource EnsureIdleInputSource()
        {
            IdleCombatantInputSource idleInput = GetComponent<IdleCombatantInputSource>();
            if (idleInput == null && Application.isPlaying)
            {
                idleInput = gameObject.AddComponent<IdleCombatantInputSource>();
            }

            if (idleInput != null)
            {
                idleInput.ConfigureForSlot(SlotId);
            }

            return idleInput;
        }

        private void SetActiveInputSource(MonoBehaviour preferredInput)
        {
            ResolveInputSourceComponent();

            MonoBehaviour[] behaviours = GetComponents<MonoBehaviour>();
            for (int index = 0; index < behaviours.Length; index += 1)
            {
                MonoBehaviour behaviour = behaviours[index];
                if (behaviour == null || behaviour is not ICombatantInputSource)
                {
                    continue;
                }

                behaviour.enabled = !_externalControlLock
                    && preferredInput != null
                    && ReferenceEquals(behaviour, preferredInput);
            }

            inputSource = preferredInput;
            _context.RuntimeInputSource = preferredInput as ICombatantInputSource;
        }

        private void CaptureInputFrame()
        {
            if (_context.RuntimeInputSource == null)
            {
                _context.currentInputFrame = default;
                return;
            }

            _context.RuntimeInputSource.CaptureFrame();
            _context.currentInputFrame = _context.RuntimeInputSource.CurrentFrame;
        }

        private void ApplyExternalControlLockState()
        {
            if (_context == null)
            {
                return;
            }

            _context.currentInputFrame = default;
            _context.shootHeldLastFrame = false;
            _context.aimHoldActive = false;
            _context.dashTimeLeft = 0f;
            _context.dashVelocity = Vector2.zero;
            _context.lastDashVelocity = Vector2.zero;
            _context.dashComboWindowLeft = 0f;
            _context.dashParryTimer = 0f;
            _context.dashPressTimer = 0f;
            _context.dashJumpUsed = false;
            _context.pendingDashPrimary = false;
            _context.pendingDashSecondary = false;
            _context.meleeTimeLeft = 0f;
            _context.ultimateTimeLeft = 0f;
            _context.ultimateTotalDuration = 0f;
            _context.ultimateImpactApplied = false;
            _context.ultimateDashTimeLeft = 0f;
            _context.ultimateDashVelocity = Vector2.zero;
            _context.lastUltimateDashVelocity = Vector2.zero;
            _context.ultimateProjectileBlockTimer = 0f;
            _context.meleeAnimationTimeLeft = 0f;
            _context.shootAnimationTimeLeft = 0f;
            _context.jumpStartTimeLeft = 0f;
            _context.dashAnimationHoldTimeLeft = 0f;
            _context.ultimateAnimationTimeLeft = 0f;
            _context.jumpBufferLeft = 0f;
            _context.coyoteTimeLeft = 0f;
            _context.wallJumpGraceTimer = 0f;
            _context.wallDetachIgnoreTimer = 0f;
            _context.contactArrowTransferLockTimeLeft = 0f;
            _context.isTouchingWall = false;
            _context.wallNormal = Vector2.zero;
            _context.currentOverrideLockLeft = 0f;
            _context.currentOverrideAction = string.Empty;
            _context.pendingOverrideAction = string.Empty;
            _context.activeColliderAction = string.Empty;
            _context.currentOverridePriority = -99999;
            _context.pendingOverridePriority = -99999;
            _context.pendingOverrideLockLeft = 0f;
            _context.meleeHitIds.Clear();
            _context.actionLockEntries.Clear();

            if (body != null)
            {
                body.linearVelocity = Vector2.zero;
            }

            _collisionSystem.RefreshCollisionState();
            UpdatePresentationState();
        }

        private void ApplyDefinitionToCollider()
        {
            if (bodyCollider == null || _statResolver == null)
            {
                return;
            }

            bodyCollider.size = _statResolver.ResolveColliderSize();
            bodyCollider.offset = _statResolver.ResolveColliderOffset();
        }

        private void EnsureFrictionlessColliderMaterial()
        {
            if (bodyCollider == null)
            {
                return;
            }

            if (s_runtimeNoFrictionMaterial == null)
            {
                s_runtimeNoFrictionMaterial = new PhysicsMaterial2D("ProjectPVPRuntimeNoFriction")
                {
                    friction = 0f,
                    bounciness = 0f,
                };
                s_runtimeNoFrictionMaterial.hideFlags = HideFlags.HideAndDontSave;
            }

            bodyCollider.sharedMaterial = s_runtimeNoFrictionMaterial;
        }

        private void ApplyCharacterVisuals()
        {
            if (spriteRenderer == null)
            {
                return;
            }

            if (characterDefinition != null)
            {
                if (characterDefinition.defaultSprite != null)
                {
                    spriteRenderer.sprite = characterDefinition.defaultSprite;
                }

                Transform spriteTransform = spriteRenderer.transform;
                Vector2 anchorOffset = characterDefinition.spriteAnchorOffset;
                spriteTransform.localPosition = new Vector3(anchorOffset.x, anchorOffset.y, 0f);
                spriteTransform.localScale = new Vector3(characterDefinition.spriteScale.x, characterDefinition.spriteScale.y, 1f);
            }

            spriteRenderer.color = Color.white;
            spriteRenderer.sortingOrder = 10;
            if (_movementSystem != null)
            {
                _movementSystem.UpdateVisualFacing();
            }
        }

        private void TickCooldowns(float deltaTime)
        {
            _context.dashPrimaryCooldownLeft = Mathf.Max(0f, _context.dashPrimaryCooldownLeft - deltaTime);
            _context.dashSecondaryCooldownLeft = Mathf.Max(0f, _context.dashSecondaryCooldownLeft - deltaTime);
            _context.dashComboWindowLeft = Mathf.Max(0f, _context.dashComboWindowLeft - deltaTime);
            _context.shootCooldownLeft = Mathf.Max(0f, _context.shootCooldownLeft - deltaTime);
            _context.meleeCooldownLeft = Mathf.Max(0f, _context.meleeCooldownLeft - deltaTime);
            _context.ultimateCooldownLeft = Mathf.Max(0f, _context.ultimateCooldownLeft - deltaTime);
            _context.meleeTimeLeft = Mathf.Max(0f, _context.meleeTimeLeft - deltaTime);
            _context.meleeAnimationTimeLeft = Mathf.Max(0f, _context.meleeAnimationTimeLeft - deltaTime);
            _context.shootAnimationTimeLeft = Mathf.Max(0f, _context.shootAnimationTimeLeft - deltaTime);
            _context.jumpStartTimeLeft = Mathf.Max(0f, _context.jumpStartTimeLeft - deltaTime);
            _context.dashAnimationHoldTimeLeft = Mathf.Max(0f, _context.dashAnimationHoldTimeLeft - deltaTime);
            _context.ultimateAnimationTimeLeft = Mathf.Max(0f, _context.ultimateAnimationTimeLeft - deltaTime);
            _context.jumpBufferLeft = Mathf.Max(0f, _context.jumpBufferLeft - deltaTime);
            _context.coyoteTimeLeft = Mathf.Max(0f, _context.coyoteTimeLeft - deltaTime);
            _context.dashParryTimer = Mathf.Max(0f, _context.dashParryTimer - deltaTime);
            _context.dashPressTimer = Mathf.Max(0f, _context.dashPressTimer - deltaTime);
            _context.wallJumpGraceTimer = Mathf.Max(0f, _context.wallJumpGraceTimer - deltaTime);
            _context.wallDetachIgnoreTimer = Mathf.Max(0f, _context.wallDetachIgnoreTimer - deltaTime);
            _context.contactArrowTransferLockTimeLeft = Mathf.Max(0f, _context.contactArrowTransferLockTimeLeft - deltaTime);
            _context.ultimateProjectileBlockTimer = Mathf.Max(0f, _context.ultimateProjectileBlockTimer - deltaTime);
            _context.currentOverrideLockLeft = Mathf.Max(0f, _context.currentOverrideLockLeft - deltaTime);
            _actionLockSystem.UpdateActionLockTimers(deltaTime);
            _actionLockSystem.UpdateActionOverrideState();
        }

        private void ResetRuntimeState()
        {
            EnsureCharacterMechanicsRuntime();
            StopDeathFlashRoutine();
            StopDeathNotifyRoutine();
            _context.currentInputFrame = default;
            _context.lastLaunchedProjectile = null;
            _context.lastFatalHitSource = null;
            _context.lastFatalHitCause = string.Empty;
            _context.lastFatalHitPosition = Vector2.zero;
            _context.arrows = _statResolver.ResolveMaxArrows();
            _context.aimHoldActive = false;
            _context.aimHoldDirection = new Vector2(_context.facing, 0f);
            _context.shootHeldLastFrame = false;
            _context.hitStunTimeLeft = 0f;
            _context.knockbackVelocity = Vector2.zero;
            _context.knockbackTimeLeft = 0f;
            _context.deathFlashTimeLeft = 0f;
            _context.hasShield = false;
            _context.dashTimeLeft = 0f;
            _context.dashVelocity = Vector2.zero;
            _context.lastDashVelocity = Vector2.zero;
            _context.dashPrimaryCooldownLeft = 0f;
            _context.dashSecondaryCooldownLeft = 0f;
            _context.dashComboWindowLeft = 0f;
            _context.shootCooldownLeft = 0f;
            _context.meleeCooldownLeft = 0f;
            _context.meleeTimeLeft = 0f;
            _context.ultimateCooldownLeft = 0f;
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
            _context.wallJumpGraceTimer = 0f;
            _context.wallDetachIgnoreTimer = 0f;
            _context.contactArrowTransferLockTimeLeft = 0f;
            _context.ultimateProjectileBlockTimer = 0f;
            _context.currentOverrideLockLeft = 0f;
            _context.dashJumpUsed = false;
            _context.pendingDashPrimary = false;
            _context.pendingDashSecondary = false;
            _context.ultimateImpactApplied = false;
            _context.ultimateDashTimeLeft = 0f;
            _context.ultimateDashVelocity = Vector2.zero;
            _context.lastUltimateDashVelocity = Vector2.zero;
            _context.meleeHitIds.Clear();
            _context.wallNormal = Vector2.zero;
            _context.currentOverrideAction = string.Empty;
            _context.pendingOverrideAction = string.Empty;
            _context.activeColliderAction = string.Empty;
            _context.currentOverridePriority = -99999;
            _context.pendingOverridePriority = -99999;
            _context.pendingOverrideLockLeft = 0f;
            _context.actionLockEntries.Clear();
            ApplyDefinitionToCollider();
            _context.CharacterMechanicsRuntime?.OnResetState();
        }

        private void UpdatePresentationState()
        {
            if (spriteRenderer == null)
            {
                return;
            }

            if (_context.isDead)
            {
                spriteRenderer.color = _context.deathFlashTimeLeft > 0f ? DeathFlashColor : Color.white;
                return;
            }

            if (IsDashAnimationActive)
            {
                spriteRenderer.color = new Color(0.75f, 0.95f, 1f, 1f);
                return;
            }

            if (_context.meleeAnimationTimeLeft > 0f)
            {
                spriteRenderer.color = new Color(1f, 0.78f, 0.78f, 1f);
                return;
            }

            if (_context.aimHoldActive)
            {
                spriteRenderer.color = new Color(1f, 0.96f, 0.72f, 1f);
                return;
            }

            if (_context.hasShield)
            {
                spriteRenderer.color = new Color(0.74f, 0.92f, 1f, 1f);
                return;
            }

            spriteRenderer.color = Color.white;
        }

        private void BeginDeathFlash()
        {
            _context.deathFlashTimeLeft = DeathFlashDuration;
            StopDeathFlashRoutine();
            UpdatePresentationState();
            _deathFlashRoutine = StartCoroutine(ClearDeathFlashAfterDelay(DeathFlashDuration));
        }

        private void StopDeathFlashRoutine()
        {
            if (_deathFlashRoutine == null)
            {
                return;
            }

            StopCoroutine(_deathFlashRoutine);
            _deathFlashRoutine = null;
        }

        private IEnumerator ClearDeathFlashAfterDelay(float delay)
        {
            if (delay > 0f)
            {
                yield return new WaitForSeconds(delay);
            }

            if (_context == null)
            {
                _deathFlashRoutine = null;
                yield break;
            }

            _context.deathFlashTimeLeft = 0f;
            _deathFlashRoutine = null;
            if (_context.isDead)
            {
                UpdatePresentationState();
            }
        }

        private IEnumerator NotifyDeathAfterDelay(float delay)
        {
            if (delay > 0f)
            {
                yield return new WaitForSeconds(delay);
            }

            if (_context != null && _context.isDead)
            {
                Died?.Invoke(this);
            }

            _deathNotifyRoutine = null;
        }

        private void StopDeathNotifyRoutine()
        {
            if (_deathNotifyRoutine == null)
            {
                return;
            }

            StopCoroutine(_deathNotifyRoutine);
            _deathNotifyRoutine = null;
        }

        private bool CanShieldAbsorbFatalHit(string cause)
        {
            if (_context == null || !_context.hasShield)
            {
                return false;
            }

            string normalizedCause = string.IsNullOrWhiteSpace(cause) ? string.Empty : cause.Trim();
            return !string.Equals(normalizedCause, "Ring Out", StringComparison.OrdinalIgnoreCase);
        }

        private void RecordFatalHit(PlayerController source, string cause)
        {
            if (_context == null)
            {
                return;
            }

            _context.lastFatalHitSource = source;
            _context.lastFatalHitCause = string.IsNullOrWhiteSpace(cause) ? "Unknown" : cause.Trim();
            _context.lastFatalHitPosition = RootPosition;
        }

        private string ResolveLastFatalHitSummary()
        {
            if (_context == null || string.IsNullOrWhiteSpace(_context.lastFatalHitCause))
            {
                return string.Empty;
            }

            string sourceLabel = _context.lastFatalHitSource != null
                ? _context.lastFatalHitSource.BotDisplayName
                : "Environment";
            return sourceLabel + " via " + _context.lastFatalHitCause;
        }

    }
}
