using System;
using System.Collections;
using System.Collections.Generic;
using ProjectPVP.Audio;
using ProjectPVP.Data;
using ProjectPVP.Input;
using UnityEngine;

namespace ProjectPVP.Gameplay
{
    public sealed class PlayerController : MonoBehaviour
    {
        private const int PriorityNegativeInfinity = -99999;
        private const float DefaultMoveSpeed = 605f;
        private const float DefaultAcceleration = 3200f;
        private const float DefaultFriction = 2600f;
        private const float DefaultGravity = 1500f;
        private const float DefaultMaxFallSpeed = 1500f;
        private const float DefaultJumpVelocity = 660f;
        private const float DefaultShootCooldown = 0.001f;
        private const int DefaultMaxArrows = 5;
        private const float DefaultMeleeCooldown = 0.45f;
        private const float DefaultMeleeDuration = 0.12f;
        private const float DefaultUltimateCooldown = 1.25f;
        private const float DefaultUltimateDuration = 0.28f;
        private const float DefaultUltimateRadius = 180f;
        private const float DefaultUltimateWindupRatio = 0.45f;
        private const float DefaultUltimateDashDuration = 0.1f;
        private const float DefaultUltimateProjectileBlockDuration = 0.12f;
        private const float DefaultWallJumpHorizontalForce = 500f;
        private const float DefaultWallJumpVerticalForce = 720f;
        private const float DefaultWallSlideSpeed = 60f;
        private const float DefaultWallGravityScale = 0.2f;
        private const float DefaultDashMultiplier = 1.8f;
        private const float DefaultDashDuration = 0.12f;
        private const float DefaultDashCooldown = 0.45f;
        private const float DefaultDashDistance = 100f;
        private const float DefaultDashUpwardMultiplier = 0.5f;
        private const float DashParryWindow = 0.2f;
        private const float DashPressParryWindow = 0.2f;
        private const float DashComboWindow = 0.2f;
        private const float JumpGraceTime = 0.16f;
        private const float ShootAnimationDuration = 0.18f;
        private const float JumpStartAnimationDuration = 0.12f;
        private const float AirAccelerationMultiplier = 0.78f;
        private const float AirFrictionMultiplier = 0.3f;
        private const float TurnAccelerationMultiplier = 1.2f;
        private const float JumpCutGravityMultiplier = 2.1f;
        private const float FallGravityMultiplier = 1.2f;
        private const float ApexGravityMultiplier = 0.82f;
        private const float ApexVerticalSpeedThreshold = 120f;
        private const float GroundSnapDistance = 240f;
        private const float SpawnGroundPadding = 2f;
        private const float CollisionSkinWidth = 2f;
        private const float RayInsetPadding = 6f;
        private const int MaxCastHits = 8;
        private const int MaxOverlapHits = 16;

        [Header("Identity")]
        [Min(1)] public int playerId = 1;
        public CharacterDefinition characterDefinition;

        [Header("References")]
        public KeyboardPlayerInputSource inputSource;
        public Rigidbody2D body;
        public BoxCollider2D bodyCollider;
        public SpriteRenderer spriteRenderer;
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

        private readonly HashSet<int> _meleeHitIds = new HashSet<int>();
        private readonly RaycastHit2D[] _castHits = new RaycastHit2D[MaxCastHits];
        private readonly Collider2D[] _overlapHits = new Collider2D[MaxOverlapHits];
        private readonly List<ActionLockEntry> _actionLockEntries = new List<ActionLockEntry>(6);

        private struct ActionLockEntry
        {
            public string action;
            public float remaining;
            public bool cancelable;
        }

        private enum UltimateReplayShapeKind
        {
            Circle = 0,
            Box = 1,
            Capsule = 2,
        }

        private static PhysicsMaterial2D s_runtimeNoFrictionMaterial;

        private CharacterAudioController _audioController;
        private PlayerInputFrame _currentInputFrame;
        private Vector2 _aimHoldDirection = Vector2.right;
        private Vector2 _dashVelocity = Vector2.zero;
        private Vector2 _lastDashVelocity = Vector2.zero;
        private int _facing = 1;
        private int _arrows = DefaultMaxArrows;
        private bool _isDead;
        private bool _aimHoldActive;
        private bool _shootHeldLastFrame;
        private bool _needsGroundReset;
        private bool _dashJumpUsed;
        private bool _pendingDashPrimary;
        private bool _pendingDashSecondary;
        private bool _isGrounded;
        private bool _isTouchingWall;
        private float _dashTimeLeft;
        private float _dashPrimaryCooldownLeft;
        private float _dashSecondaryCooldownLeft;
        private float _dashComboWindowLeft;
        private float _shootCooldownLeft;
        private float _meleeCooldownLeft;
        private float _meleeTimeLeft;
        private float _ultimateCooldownLeft;
        private float _ultimateTimeLeft;
        private float _ultimateTotalDuration;
        private float _meleeAnimationTimeLeft;
        private float _shootAnimationTimeLeft;
        private float _jumpStartTimeLeft;
        private float _dashAnimationHoldTimeLeft;
        private float _ultimateAnimationTimeLeft;
        private float _jumpBufferLeft;
        private float _coyoteTimeLeft;
        private float _dashParryTimer;
        private float _dashPressTimer;
        private float _currentOverrideLockLeft;
        private Vector2 _wallNormal = Vector2.zero;
        private string _currentOverrideAction = string.Empty;
        private string _pendingOverrideAction = string.Empty;
        private string _activeColliderAction = string.Empty;
        private int _currentOverridePriority = PriorityNegativeInfinity;
        private int _pendingOverridePriority = PriorityNegativeInfinity;
        private float _pendingOverrideLockLeft;
        private bool _ultimateImpactApplied;
        private Vector2 _ultimateDashVelocity = Vector2.zero;
        private Vector2 _lastUltimateDashVelocity = Vector2.zero;
        private Vector2 _ultimateReplayRootStart = Vector2.zero;
        private Vector2 _ultimateReplayRootEnd = Vector2.zero;
        private Vector2 _ultimateReplayImpactCenter = Vector2.zero;
        private Vector2 _ultimateReplayShapeSize = Vector2.zero;
        private Vector2 _ultimateReplayGhostSpriteOffset = Vector2.zero;
        private Vector3 _ultimateReplayGhostScale = Vector3.one;
        private Vector3 _ultimateReplayGhostEulerAngles = Vector3.zero;
        private float _ultimateDashTimeLeft;
        private float _ultimateProjectileBlockTimer;
        private float _ultimateReplayDelayLeft;
        private float _ultimateReplayTimeLeft;
        private float _ultimateReplayShapeRadius;
        private float _ultimateReplayShapeAngle;
        private bool _ultimateReplayQueued;
        private bool _ultimateReplayImpactPending;
        private bool _ultimateReplayGhostFlipX;
        private UltimateReplayShapeKind _ultimateReplayShapeKind;
        private CapsuleDirection2D _ultimateReplayCapsuleDirection = CapsuleDirection2D.Horizontal;
        private GameObject _ultimateReplayGhostObject;
        private SpriteRenderer _ultimateReplayGhostRenderer;
        private Sprite _ultimateReplayGhostSprite;
        private int _ultimateReplayGhostSortingLayerId;
        private int _ultimateReplayGhostSortingOrder;
        public int Facing => _facing;
        public int CurrentArrows => _arrows;
        public bool IsDead => _isDead;
        public bool IsGrounded => _isGrounded;
        public bool IsTouchingWall => _isTouchingWall;
        public bool IsDashing => _dashTimeLeft > 0f;
        public bool IsDashAnimationActive => IsDashing || _dashAnimationHoldTimeLeft > 0f;
        public bool IsAimHoldActive => _aimHoldActive;
        public bool IsMeleeActive => _meleeTimeLeft > 0f;
        public bool IsShootAnimating => _shootAnimationTimeLeft > 0f;
        public bool IsJumpStartActive => _jumpStartTimeLeft > 0f;
        public bool IsUltimateActive => _ultimateTimeLeft > 0f;
        public Vector2 AimHoldDirection => _aimHoldDirection;
        public Vector2 CurrentVelocity => body != null ? body.linearVelocity : Vector2.zero;
        public float HorizontalVelocity => body != null ? body.linearVelocity.x : 0f;
        public float VerticalVelocity => body != null ? body.linearVelocity.y : 0f;
        public Vector2 ConfiguredSpawnWorldPosition => ResolveConfiguredSpawnWorldPosition();
        public Vector2 ProjectileOriginWorldPosition => ResolveProjectileOriginWorldPosition(_facing);
        public Vector2 MeleeHitboxCenter => GetMeleeHitboxCenter();
        public Vector2 MeleeHitboxSize => GetMeleeHitboxSize();
        public Vector2 UltimateHitboxCenter => GetUltimateHitboxCenter();
        public float UltimateHitboxRadius => ResolveUltimateRadius();
        public float DashParryTimeLeft => _dashParryTimer;
        public float DashPressTimeLeft => _dashPressTimer;
        public PlayerInputFrame CurrentInputFrame => _currentInputFrame;
        public string CurrentVisualActionKey => ResolveVisualActionKey();

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
            ApplyDefinitionToCollider();
            ApplyCharacterVisuals();
        }

        private void Awake()
        {
            CacheReferences();
            EnsureAudioController();
            ConfigureInput();
            ConfigureRuntimeBody();
            EnsureFrictionlessColliderMaterial();
            ApplyDefinitionToCollider();
            ApplyCharacterVisuals();
            SyncCombatAnchors();
            ResetRuntimeState();
            RefreshCollisionState();
        }

        private void OnEnable()
        {
            CacheReferences();
            EnsureAudioController();

            if (!Application.isPlaying)
            {
                ApplyDefinitionToCollider();
                ApplyCharacterVisuals();
                SyncCombatAnchors();
                return;
            }

            ConfigureInput();
            ConfigureRuntimeBody();
            SyncCombatAnchors();
        }

        private void FixedUpdate()
        {
            if (_isDead || body == null)
            {
                return;
            }

            CaptureInputFrame();

            float deltaTime = Time.fixedDeltaTime;
            TickCooldowns(deltaTime);
            RefreshCollisionState();

            if (_isGrounded)
            {
                _needsGroundReset = false;
                _dashJumpUsed = false;
                _coyoteTimeLeft = JumpGraceTime;
            }

            if (_currentInputFrame.jumpPressed)
            {
                _jumpBufferLeft = JumpGraceTime;
            }

            bool shootReleasedThisFrame = UpdateAimHoldState(_currentInputFrame);
            UpdateFacing(_currentInputFrame);
            SyncCombatAnchors();

            Vector2 velocity = body.linearVelocity;
            Vector2 previousDashVelocity = _lastDashVelocity;
            Vector2 previousUltimateDashVelocity = _lastUltimateDashVelocity;
            velocity -= previousDashVelocity;
            velocity -= previousUltimateDashVelocity;

            HandleMovement(_currentInputFrame, deltaTime, ref velocity);
            HandleJumpAndGravity(_currentInputFrame, deltaTime, ref velocity);
            TryStartDash(_currentInputFrame);

            Vector2 dashVelocity = UpdateDashVelocity(deltaTime, ref velocity);
            ApplyTransientVelocity(ref velocity, previousDashVelocity, dashVelocity, ref _lastDashVelocity);

            Vector2 ultimateDashVelocity = UpdateUltimateDashVelocity(deltaTime);
            ApplyTransientVelocity(ref velocity, previousUltimateDashVelocity, ultimateDashVelocity, ref _lastUltimateDashVelocity);

            MoveCharacter(ref velocity, deltaTime);
            body.linearVelocity = velocity;
            RefreshCollisionState();

            if (shootReleasedThisFrame)
            {
                FireHeldShot();
            }

            TryUseMelee(_currentInputFrame);
            HandleActiveMelee();
            TryUseUltimate(_currentInputFrame);
            HandleActiveUltimate(deltaTime);
            UpdateUltimateReplay(deltaTime);
            TryCheckHeadStomp();
            ApplyRuntimeColliderOverride(CurrentVisualActionKey);
            UpdatePresentationState();
        }

        public void SetSpawnPosition(Vector2 worldPosition)
        {
            Vector2 bodySpawnPosition = ResolveSpawnBodyPosition(worldPosition);
            if (body != null)
            {
                body.position = bodySpawnPosition;
                body.linearVelocity = Vector2.zero;
            }
            else
            {
                transform.position = bodySpawnPosition;
            }

            _isDead = false;
            ApplyCharacterVisuals();
            SyncCombatAnchors();
            ResetRuntimeState();
            RefreshCollisionState();
            SnapToGroundAtSpawn(worldPosition);
            RefreshCollisionState();
        }

        public void AddArrows(int amount)
        {
            _arrows = Mathf.Clamp(_arrows + amount, 0, ResolveMaxArrows());
        }

        public void Kill()
        {
            if (_isDead)
            {
                return;
            }

            _isDead = true;
            _aimHoldActive = false;
            _shootHeldLastFrame = false;
            _dashTimeLeft = 0f;
            _dashVelocity = Vector2.zero;
            _lastDashVelocity = Vector2.zero;
            _meleeTimeLeft = 0f;
            _ultimateTimeLeft = 0f;
            _ultimateTotalDuration = 0f;
            _meleeAnimationTimeLeft = 0f;
            _shootAnimationTimeLeft = 0f;
            _jumpStartTimeLeft = 0f;
            _dashAnimationHoldTimeLeft = 0f;
            _ultimateAnimationTimeLeft = 0f;
            _jumpBufferLeft = 0f;
            _coyoteTimeLeft = 0f;
            _dashParryTimer = 0f;
            _dashPressTimer = 0f;
            _dashComboWindowLeft = 0f;
            _currentOverrideLockLeft = 0f;
            _pendingDashPrimary = false;
            _pendingDashSecondary = false;
            _ultimateCooldownLeft = 0f;
            _ultimateImpactApplied = false;
            _ultimateDashTimeLeft = 0f;
            _ultimateDashVelocity = Vector2.zero;
            _lastUltimateDashVelocity = Vector2.zero;
            _ultimateProjectileBlockTimer = 0f;
            _ultimateReplayDelayLeft = 0f;
            _ultimateReplayTimeLeft = 0f;
            _ultimateReplayQueued = false;
            _ultimateReplayImpactPending = false;
            CleanupUltimateReplayGhost();
            _currentOverrideAction = string.Empty;
            _pendingOverrideAction = string.Empty;
            _activeColliderAction = string.Empty;
            _currentOverridePriority = PriorityNegativeInfinity;
            _pendingOverridePriority = PriorityNegativeInfinity;
            _pendingOverrideLockLeft = 0f;
            _actionLockEntries.Clear();

            if (body != null)
            {
                body.linearVelocity = Vector2.zero;
            }

            ApplyDefinitionToCollider();
            UpdatePresentationState();
            float deathEventDelay = characterDefinition != null && characterDefinition.HasActionAnimation("death")
                ? Mathf.Clamp(ResolveActionDuration("death", 0.35f), 0.2f, 0.6f)
                : 0f;
            StartCoroutine(NotifyDeathAfterDelay(deathEventDelay));
        }

        public bool HandleIncomingProjectile(ProjectileController projectile)
        {
            if (projectile == null || _isDead)
            {
                return false;
            }

            if (projectile.SourceObject == gameObject)
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
                _dashParryTimer = 0f;
                _dashPressTimer = 0f;
                UpdatePresentationState();
                return true;
            }

            Kill();
            return true;
        }

        public void ReceiveProjectile(ProjectileController projectile)
        {
            HandleIncomingProjectile(projectile);
        }

        public bool TryCollectProjectile(ProjectileController projectile)
        {
            if (projectile == null || _isDead)
            {
                return false;
            }

            AddArrows(1);
            UpdatePresentationState();
            return true;
        }

        public Vector2 GetProjectileInheritedVelocity()
        {
            return body != null ? body.linearVelocity : Vector2.zero;
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

            if (inputSource == null)
            {
                inputSource = GetComponent<KeyboardPlayerInputSource>();
            }

            if (spawnAnchor == null)
            {
                spawnAnchor = FindChildAnchor("SpawnAnchor", PlayerCombatAnchorKind.Spawn);
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
                meleeHitboxAnchor = FindChildAnchor("MeleeHitbox", PlayerCombatAnchorKind.MeleeHitbox);
            }

            if (ultimateHitboxAnchor == null)
            {
                ultimateHitboxAnchor = FindChildAnchor("UltimateHitbox", PlayerCombatAnchorKind.UltimateHitbox);
            }
        }

        private void EnsureAudioController()
        {
            if (_audioController == null)
            {
                _audioController = GetComponent<CharacterAudioController>();
            }

            if (_audioController == null && Application.isPlaying)
            {
                _audioController = gameObject.AddComponent<CharacterAudioController>();
            }

            if (_audioController != null)
            {
                _audioController.player = this;
            }
        }

        private void ConfigureInput()
        {
            if (inputSource != null)
            {
                inputSource.ConfigureForPlayer(playerId);
            }
        }

        private void CaptureInputFrame()
        {
            if (inputSource == null)
            {
                _currentInputFrame = default;
                return;
            }

            inputSource.CaptureFrame();
            _currentInputFrame = inputSource.CurrentFrame;
        }

        private void ApplyDefinitionToCollider()
        {
            if (bodyCollider == null)
            {
                return;
            }

            bodyCollider.size = ResolveColliderSize();
            bodyCollider.offset = ResolveColliderOffset();
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
            UpdateVisualFacing();
        }

        private IEnumerator NotifyDeathAfterDelay(float delay)
        {
            if (delay > 0f)
            {
                yield return new WaitForSeconds(delay);
            }

            Died?.Invoke(this);
        }

        private void ResetRuntimeState()
        {
            _currentInputFrame = default;
            _arrows = ResolveMaxArrows();
            _aimHoldActive = false;
            _aimHoldDirection = new Vector2(_facing, 0f);
            _shootHeldLastFrame = false;
            _dashTimeLeft = 0f;
            _dashVelocity = Vector2.zero;
            _lastDashVelocity = Vector2.zero;
            _dashPrimaryCooldownLeft = 0f;
            _dashSecondaryCooldownLeft = 0f;
            _dashComboWindowLeft = 0f;
            _shootCooldownLeft = 0f;
            _meleeCooldownLeft = 0f;
            _meleeTimeLeft = 0f;
            _ultimateCooldownLeft = 0f;
            _ultimateTimeLeft = 0f;
            _ultimateTotalDuration = 0f;
            _meleeAnimationTimeLeft = 0f;
            _shootAnimationTimeLeft = 0f;
            _jumpStartTimeLeft = 0f;
            _dashAnimationHoldTimeLeft = 0f;
            _ultimateAnimationTimeLeft = 0f;
            _jumpBufferLeft = 0f;
            _coyoteTimeLeft = 0f;
            _dashParryTimer = 0f;
            _dashPressTimer = 0f;
            _ultimateProjectileBlockTimer = 0f;
            _currentOverrideLockLeft = 0f;
            _needsGroundReset = false;
            _dashJumpUsed = false;
            _pendingDashPrimary = false;
            _pendingDashSecondary = false;
            _ultimateImpactApplied = false;
            _ultimateDashTimeLeft = 0f;
            _ultimateDashVelocity = Vector2.zero;
            _lastUltimateDashVelocity = Vector2.zero;
            _ultimateReplayDelayLeft = 0f;
            _ultimateReplayTimeLeft = 0f;
            _ultimateReplayQueued = false;
            _ultimateReplayImpactPending = false;
            _meleeHitIds.Clear();
            CleanupUltimateReplayGhost();
            _wallNormal = Vector2.zero;
            _currentOverrideAction = string.Empty;
            _pendingOverrideAction = string.Empty;
            _activeColliderAction = string.Empty;
            _currentOverridePriority = PriorityNegativeInfinity;
            _pendingOverridePriority = PriorityNegativeInfinity;
            _pendingOverrideLockLeft = 0f;
            _actionLockEntries.Clear();
            ApplyDefinitionToCollider();
        }

        private void TickCooldowns(float deltaTime)
        {
            _dashPrimaryCooldownLeft = Mathf.Max(0f, _dashPrimaryCooldownLeft - deltaTime);
            _dashSecondaryCooldownLeft = Mathf.Max(0f, _dashSecondaryCooldownLeft - deltaTime);
            _dashComboWindowLeft = Mathf.Max(0f, _dashComboWindowLeft - deltaTime);
            _shootCooldownLeft = Mathf.Max(0f, _shootCooldownLeft - deltaTime);
            _meleeCooldownLeft = Mathf.Max(0f, _meleeCooldownLeft - deltaTime);
            _ultimateCooldownLeft = Mathf.Max(0f, _ultimateCooldownLeft - deltaTime);
            _meleeTimeLeft = Mathf.Max(0f, _meleeTimeLeft - deltaTime);
            _meleeAnimationTimeLeft = Mathf.Max(0f, _meleeAnimationTimeLeft - deltaTime);
            _shootAnimationTimeLeft = Mathf.Max(0f, _shootAnimationTimeLeft - deltaTime);
            _jumpStartTimeLeft = Mathf.Max(0f, _jumpStartTimeLeft - deltaTime);
            _dashAnimationHoldTimeLeft = Mathf.Max(0f, _dashAnimationHoldTimeLeft - deltaTime);
            _ultimateAnimationTimeLeft = Mathf.Max(0f, _ultimateAnimationTimeLeft - deltaTime);
            _jumpBufferLeft = Mathf.Max(0f, _jumpBufferLeft - deltaTime);
            _coyoteTimeLeft = Mathf.Max(0f, _coyoteTimeLeft - deltaTime);
            _dashParryTimer = Mathf.Max(0f, _dashParryTimer - deltaTime);
            _dashPressTimer = Mathf.Max(0f, _dashPressTimer - deltaTime);
            _ultimateProjectileBlockTimer = Mathf.Max(0f, _ultimateProjectileBlockTimer - deltaTime);
            _currentOverrideLockLeft = Mathf.Max(0f, _currentOverrideLockLeft - deltaTime);
            UpdateActionLockTimers(deltaTime);
            UpdateActionOverrideState();
        }

        private void RefreshCollisionState()
        {
            _isGrounded = QueryGround(out _);
            _isTouchingWall = TryGetWallNormal(out _wallNormal);
        }

        private bool UpdateAimHoldState(PlayerInputFrame frame)
        {
            bool shootJustPressed = frame.shootHeld && !_shootHeldLastFrame;
            bool shootJustReleased = !frame.shootHeld && _shootHeldLastFrame;
            _shootHeldLastFrame = frame.shootHeld;

            if (shootJustPressed && _arrows > 0)
            {
                _aimHoldActive = true;
                _aimHoldDirection = ResolveAimDirection(frame);
            }

            // Preserve the last valid hold direction until release so the shot
            // is fired with the direction the player was actually holding.
            if (_aimHoldActive && frame.shootHeld && frame.aim.sqrMagnitude > 0.01f)
            {
                _aimHoldDirection = frame.aim.normalized;
            }

            if (_arrows <= 0)
            {
                _aimHoldActive = false;
                return false;
            }

            if (_aimHoldActive && shootJustReleased)
            {
                _aimHoldActive = false;
                return true;
            }

            return false;
        }

        private void UpdateFacing(PlayerInputFrame frame)
        {
            if (Mathf.Abs(frame.axis) > 0.01f)
            {
                _facing = frame.axis > 0f ? 1 : -1;
            }

            if (_aimHoldActive && Mathf.Abs(_aimHoldDirection.x) > 0.01f)
            {
                _facing = _aimHoldDirection.x > 0f ? 1 : -1;
            }

            UpdateVisualFacing();
        }

        private void UpdateVisualFacing()
        {
            if (spriteRenderer != null)
            {
                spriteRenderer.flipX = _facing < 0;
            }

            UpdateProjectileOriginSocket();
        }

        private void MoveCharacter(ref Vector2 velocity, float deltaTime)
        {
            if (body == null || bodyCollider == null)
            {
                transform.position += (Vector3)(velocity * deltaTime);
                return;
            }

            Vector2 position = body.position;
            position = MoveHorizontally(position, ref velocity.x, deltaTime);
            position = MoveVertically(position, ref velocity.y, deltaTime);
            body.position = position;
            transform.position = position;
        }

        private Vector2 MoveHorizontally(Vector2 position, ref float velocityX, float deltaTime)
        {
            if (Mathf.Abs(velocityX) <= 0.0001f || bodyCollider == null)
            {
                return position;
            }

            float signedDistance = velocityX * deltaTime;
            float direction = Mathf.Sign(signedDistance);
            float distance = Mathf.Abs(signedDistance);
            GetColliderGeometry(position, out Vector2 center, out Vector2 extents);

            float verticalInset = Mathf.Min(extents.y - CollisionSkinWidth, Mathf.Max(RayInsetPadding, extents.y * 0.55f));
            if (verticalInset < CollisionSkinWidth)
            {
                verticalInset = CollisionSkinWidth;
            }

            Vector2[] origins =
            {
                center + new Vector2(direction * (extents.x - CollisionSkinWidth), 0f),
                center + new Vector2(direction * (extents.x - CollisionSkinWidth), extents.y - verticalInset),
                center + new Vector2(direction * (extents.x - CollisionSkinWidth), -extents.y + verticalInset),
            };

            float allowedDistance = ResolveTravelDistance(
                origins,
                new Vector2(direction, 0f),
                distance,
                hit => Mathf.Abs(hit.normal.x) >= 0.35f);

            if (allowedDistance + 0.001f < distance)
            {
                velocityX = 0f;
            }

            position.x += direction * allowedDistance;
            return position;
        }

        private Vector2 MoveVertically(Vector2 position, ref float velocityY, float deltaTime)
        {
            if (Mathf.Abs(velocityY) <= 0.0001f || bodyCollider == null)
            {
                return position;
            }

            float signedDistance = velocityY * deltaTime;
            float direction = Mathf.Sign(signedDistance);
            float distance = Mathf.Abs(signedDistance);
            GetColliderGeometry(position, out Vector2 center, out Vector2 extents);

            float horizontalInset = Mathf.Min(extents.x - CollisionSkinWidth, Mathf.Max(RayInsetPadding, extents.x * 0.6f));
            if (horizontalInset < CollisionSkinWidth)
            {
                horizontalInset = CollisionSkinWidth;
            }

            Vector2[] origins =
            {
                center + new Vector2(0f, direction * (extents.y - CollisionSkinWidth)),
                center + new Vector2(-extents.x + horizontalInset, direction * (extents.y - CollisionSkinWidth)),
                center + new Vector2(extents.x - horizontalInset, direction * (extents.y - CollisionSkinWidth)),
            };

            float allowedDistance = ResolveTravelDistance(
                origins,
                new Vector2(0f, direction),
                distance,
                hit => Mathf.Abs(hit.normal.y) >= 0.35f);

            if (allowedDistance + 0.001f < distance)
            {
                velocityY = 0f;
            }

            position.y += direction * allowedDistance;
            return position;
        }

        private float ResolveTravelDistance(Vector2[] origins, Vector2 direction, float distance, Func<RaycastHit2D, bool> acceptsHit)
        {
            float allowedDistance = distance;
            float castDistance = distance + CollisionSkinWidth;

            for (int originIndex = 0; originIndex < origins.Length; originIndex += 1)
            {
                int hitCount = Physics2D.Raycast(origins[originIndex], direction, GetDefaultContactFilter(), _castHits, castDistance);
                for (int hitIndex = 0; hitIndex < hitCount; hitIndex += 1)
                {
                    RaycastHit2D hit = _castHits[hitIndex];
                    if (ShouldIgnoreCastHit(hit) || !acceptsHit(hit))
                    {
                        continue;
                    }

                    allowedDistance = Mathf.Min(allowedDistance, Mathf.Max(0f, hit.distance - CollisionSkinWidth));
                }
            }

            return allowedDistance;
        }

        private void GetColliderGeometry(Vector2 position, out Vector2 center, out Vector2 extents)
        {
            center = position + bodyCollider.offset;
            extents = bodyCollider.size * 0.5f;
        }

        private Vector2 UpdateDashVelocity(float deltaTime, ref Vector2 velocity)
        {
            if (_dashTimeLeft <= 0f)
            {
                return Vector2.zero;
            }

            if (HasBufferedJump() && !_dashJumpUsed)
            {
                velocity.y = Mathf.Max(velocity.y, ResolveJumpVelocity());
                _dashJumpUsed = true;
                ConsumeBufferedJump();
                TriggerJumpStartAnimation();
            }

            Vector2 dashVelocity = _dashVelocity;
            _dashTimeLeft -= deltaTime;

            if (_dashTimeLeft <= 0f)
            {
                _dashVelocity = Vector2.zero;
            }

            return dashVelocity;
        }

        private void HandleMovement(PlayerInputFrame frame, float deltaTime, ref Vector2 velocity)
        {
            float targetSpeed = frame.axis * ResolveMoveSpeed();
            bool hasDirectionalInput = Mathf.Abs(frame.axis) > 0.01f;
            float acceleration = ResolveAcceleration();
            float friction = ResolveFriction();

            if (!_isGrounded)
            {
                acceleration *= AirAccelerationMultiplier;
                friction *= AirFrictionMultiplier;
            }

            if (hasDirectionalInput)
            {
                if (Mathf.Abs(velocity.x) > 0.01f && Mathf.Sign(velocity.x) != Mathf.Sign(frame.axis))
                {
                    acceleration *= TurnAccelerationMultiplier;
                }

                velocity.x = Mathf.MoveTowards(velocity.x, targetSpeed, acceleration * deltaTime);
                return;
            }

            velocity.x = Mathf.MoveTowards(velocity.x, 0f, friction * deltaTime);
        }

        private void HandleJumpAndGravity(PlayerInputFrame frame, float deltaTime, ref Vector2 velocity)
        {
            if (TryConsumeJump(ref velocity))
            {
                return;
            }

            if (_isGrounded)
            {
                if (velocity.y < 0f)
                {
                    velocity.y = 0f;
                }

                return;
            }

            bool pushingIntoWall = _isTouchingWall && Mathf.Abs(frame.axis) > 0.01f && Mathf.Sign(frame.axis) == -Mathf.Sign(_wallNormal.x);
            if (pushingIntoWall)
            {
                if (velocity.y < 0f)
                {
                    velocity.y = Mathf.Max(velocity.y, -ResolveWallSlideSpeed());
                }

                velocity.y -= ResolveGravity() * ResolveWallGravityScale() * deltaTime;
                velocity.y = Mathf.Max(velocity.y, -ResolveMaxFallSpeed());
                return;
            }

            float gravityMultiplier = ResolveAirGravityMultiplier(frame, velocity.y);
            velocity.y -= ResolveGravity() * gravityMultiplier * deltaTime;
            velocity.y = Mathf.Max(velocity.y, -ResolveMaxFallSpeed());
        }

        private float ResolveAirGravityMultiplier(PlayerInputFrame frame, float verticalVelocity)
        {
            if (verticalVelocity > 0f && !frame.jumpHeld)
            {
                return JumpCutGravityMultiplier;
            }

            if (verticalVelocity < 0f)
            {
                return FallGravityMultiplier;
            }

            if (Mathf.Abs(verticalVelocity) <= ApexVerticalSpeedThreshold)
            {
                return ApexGravityMultiplier;
            }

            return 1f;
        }

        private void TryStartDash(PlayerInputFrame frame)
        {
            bool primaryPressed = frame.dashPrimaryPressed;
            bool secondaryPressed = frame.dashSecondaryPressed;
            if (primaryPressed || secondaryPressed)
            {
                _dashPressTimer = DashPressParryWindow;
                _pendingDashPrimary |= primaryPressed;
                _pendingDashSecondary |= secondaryPressed;
                _dashComboWindowLeft = Mathf.Max(_dashComboWindowLeft, DashComboWindow);
            }

            if (!_pendingDashPrimary && !_pendingDashSecondary)
            {
                return;
            }

            if (IsDashing)
            {
                return;
            }

            if (_dashComboWindowLeft <= 0f && !primaryPressed && !secondaryPressed)
            {
                _pendingDashPrimary = false;
                _pendingDashSecondary = false;
                return;
            }

            int usedCount = 0;
            bool usePrimary = _pendingDashPrimary && _dashPrimaryCooldownLeft <= 0f;
            bool useSecondary = _pendingDashSecondary && _dashSecondaryCooldownLeft <= 0f;

            if (usePrimary)
            {
                _dashPrimaryCooldownLeft = ResolveDashCooldown();
                _pendingDashPrimary = false;
                usedCount += 1;
            }

            if (useSecondary)
            {
                _dashSecondaryCooldownLeft = ResolveDashCooldown();
                _pendingDashSecondary = false;
                usedCount += 1;
            }

            if (usedCount <= 0)
            {
                if (_dashComboWindowLeft <= 0f)
                {
                    _pendingDashPrimary = false;
                    _pendingDashSecondary = false;
                }

                return;
            }

            _dashComboWindowLeft = 0f;

            Vector2 direction = ResolveDashDirection();
            float dashSpeed = ResolveDashDistance() > 0f && ResolveDashDuration() > 0f
                ? (ResolveDashDistance() * usedCount) / ResolveDashDuration()
                : ResolveMoveSpeed() * ResolveDashMultiplier() * usedCount;

            _dashVelocity = direction * dashSpeed;
            _dashTimeLeft = ResolveDashDuration();
            _dashParryTimer = DashParryWindow;
            _dashJumpUsed = false;
            _needsGroundReset = true;
            TriggerDashAnimation(ResolveActionDuration("dash", 0.3f));
            PlayActionSfx("dash");
        }

        private void FireHeldShot()
        {
            if (projectilePrefab == null || _shootCooldownLeft > 0f || _arrows <= 0)
            {
                return;
            }

            Vector2 aimDirection = _aimHoldDirection.sqrMagnitude > 0.01f
                ? _aimHoldDirection.normalized
                : new Vector2(_facing, 0f);
            int shotFacing = ResolveShotFacing(aimDirection);
            if (shotFacing != _facing)
            {
                _facing = shotFacing;
                UpdateVisualFacing();
            }

            Vector2 origin = GetProjectileSpawnPoint(aimDirection, shotFacing);

            ProjectileController projectile = Instantiate(projectilePrefab, origin, Quaternion.identity);
            projectile.baseSpeed = 1500f;
            projectile.minSpeed = 720f;
            projectile.speedDecay = 360f;
            projectile.gravity = 750f;
            projectile.gravityDelayRatio = 0f;
            projectile.gravityRampRatio = 0.6f;
            projectile.gravityMinScale = 0.45f;
            projectile.gravityMaxScale = 1.2f;
            projectile.upwardGravityMultiplier = 3.2f;
            projectile.upwardSpeedDecayMultiplier = 2.2f;
            projectile.maxLifetime = 2.5f;
            projectile.maxRange = 1600f * 0.9f;
            projectile.collectableWhenStuck = true;
            projectile.Launch(
                gameObject,
                origin,
                aimDirection,
                GetProjectileInheritedVelocity(),
                ResolveProjectileInheritVelocityFactor(),
                ResolveProjectileSprite());
            projectile.transform.localScale = Vector3.one * ResolveProjectileScale();

            _arrows -= 1;
            _shootCooldownLeft = ResolveShootCooldown();
            TriggerShootAnimation(ResolveActionDuration("shoot", ShootAnimationDuration));
            PlayActionSfx("shoot");
        }

        private void TryUseMelee(PlayerInputFrame frame)
        {
            if (!frame.meleePressed || _meleeCooldownLeft > 0f || _meleeTimeLeft > 0f)
            {
                return;
            }

            _meleeCooldownLeft = ResolveMeleeCooldown();
            _meleeTimeLeft = ResolveMeleeDuration();
            _meleeHitIds.Clear();
            TriggerMeleeAnimation(ResolveActionDuration("melee", ResolveMeleeDuration()));
            PlayActionSfx("melee");
        }

        private void HandleActiveMelee()
        {
            if (_meleeTimeLeft <= 0f)
            {
                return;
            }

            HandleProjectileSeverDuringMelee();

            int hitCount = TryOverlapAuthoredHitbox(meleeHitboxAnchor, GetMeleeContactFilter(), _overlapHits, out int authoredHitCount)
                ? authoredHitCount
                : Physics2D.OverlapBox(
                    GetMeleeHitboxCenter(),
                    GetMeleeHitboxSize(),
                    0f,
                    GetMeleeContactFilter(),
                    _overlapHits);

            for (int index = 0; index < hitCount; index += 1)
            {
                Collider2D hit = _overlapHits[index];
                if (hit == null)
                {
                    continue;
                }

                if (TrySeverProjectileWithMelee(hit))
                {
                    continue;
                }

                PlayerController target = hit.GetComponentInParent<PlayerController>();
                if (target == null || target == this || target.IsDead)
                {
                    continue;
                }

                int targetId = target.GetInstanceID();
                if (_meleeHitIds.Contains(targetId))
                {
                    continue;
                }

                _meleeHitIds.Add(targetId);
                target.Kill();
            }
        }

        private void HandleProjectileSeverDuringMelee()
        {
            int hitCount = TryOverlapAuthoredHitbox(meleeHitboxAnchor, GetProjectileSeverContactFilter(), _overlapHits, out int authoredHitCount)
                ? authoredHitCount
                : Physics2D.OverlapBox(
                    GetMeleeHitboxCenter(),
                    GetMeleeHitboxSize(),
                    0f,
                    GetProjectileSeverContactFilter(),
                    _overlapHits);

            for (int index = 0; index < hitCount; index += 1)
            {
                Collider2D hit = _overlapHits[index];
                if (hit == null)
                {
                    continue;
                }

                TrySeverProjectileWithMelee(hit);
            }
        }

        private void TryUseUltimate(PlayerInputFrame frame)
        {
            if (!frame.ultimatePressed || _ultimateCooldownLeft > 0f || _ultimateTimeLeft > 0f || !HasUltimateConfigured())
            {
                return;
            }

            _ultimateCooldownLeft = ResolveUltimateCooldown();
            _ultimateTotalDuration = ResolveActionDuration("ult", DefaultUltimateDuration);
            _ultimateTimeLeft = _ultimateTotalDuration;
            _ultimateAnimationTimeLeft = _ultimateTotalDuration;
            _ultimateImpactApplied = false;
            BeginUltimateDash();
            LockActionForDuration("ult", _ultimateTotalDuration, Mathf.Min(_ultimateTotalDuration, 0.2f), false);
            PlayActionSfx("ult");
        }

        private void HandleActiveUltimate(float deltaTime)
        {
            if (_ultimateTimeLeft <= 0f)
            {
                return;
            }

            _ultimateTimeLeft = Mathf.Max(0f, _ultimateTimeLeft - deltaTime);
            if (_ultimateImpactApplied)
            {
                return;
            }

            if (ResolveUltimateDashDistance() > 0.01f)
            {
                if (_ultimateDashTimeLeft <= 0f)
                {
                    ApplyUltimateImpact();
                    _ultimateImpactApplied = true;
                }

                return;
            }

            float elapsed = _ultimateTotalDuration - _ultimateTimeLeft;
            float activeTime = _ultimateTotalDuration * ResolveUltimateWindupRatio();
            if (elapsed >= activeTime)
            {
                ApplyUltimateImpact();
                _ultimateImpactApplied = true;
            }
        }

        private void ApplyUltimateImpact()
        {
            CaptureUltimateReplayData();
            int hitCount = CollectCurrentUltimateHits(_overlapHits);
            ApplyUltimateDamageHits(_overlapHits, hitCount);
        }

        private Vector2 UpdateUltimateDashVelocity(float deltaTime)
        {
            if (_ultimateDashTimeLeft <= 0f)
            {
                return Vector2.zero;
            }

            Vector2 dashVelocity = _ultimateDashVelocity;
            _ultimateDashTimeLeft = Mathf.Max(0f, _ultimateDashTimeLeft - deltaTime);
            if (_ultimateDashTimeLeft <= 0f)
            {
                _ultimateDashVelocity = Vector2.zero;
            }

            return dashVelocity;
        }

        private void BeginUltimateDash()
        {
            _ultimateDashTimeLeft = 0f;
            _ultimateDashVelocity = Vector2.zero;
            _lastUltimateDashVelocity = Vector2.zero;
            _ultimateProjectileBlockTimer = 0f;
            _ultimateReplayDelayLeft = 0f;
            _ultimateReplayTimeLeft = 0f;
            _ultimateReplayQueued = false;
            _ultimateReplayImpactPending = false;
            CleanupUltimateReplayGhost();

            float dashDistance = ResolveUltimateDashDistance();
            float dashDuration = ResolveUltimateDashDuration();
            if (dashDistance <= 0.01f || dashDuration <= 0.01f)
            {
                return;
            }

            Vector2 dashDirection = ResolveDashDirection();
            _ultimateDashVelocity = dashDirection * (dashDistance / dashDuration);
            _ultimateDashTimeLeft = dashDuration;
            _ultimateReplayRootStart = body != null ? body.position : (Vector2)transform.position;
            if (ResolveUltimateBlocksProjectiles())
            {
                _ultimateProjectileBlockTimer = ResolveUltimateProjectileBlockDuration();
            }
        }

        private void CaptureUltimateReplayData()
        {
            if (ResolveUltimateReplayDelay() <= 0f || ResolveUltimateDashDistance() <= 0.01f)
            {
                return;
            }

            _ultimateReplayRootEnd = body != null ? body.position : (Vector2)transform.position;
            CaptureUltimateReplayShape();
            CaptureUltimateReplayGhostVisual();
            _ultimateReplayDelayLeft = ResolveUltimateReplayDelay();
            _ultimateReplayTimeLeft = 0f;
            _ultimateReplayQueued = true;
            _ultimateReplayImpactPending = true;
        }

        private void UpdateUltimateReplay(float deltaTime)
        {
            if (!_ultimateReplayQueued)
            {
                return;
            }

            if (_ultimateReplayDelayLeft > 0f)
            {
                _ultimateReplayDelayLeft = Mathf.Max(0f, _ultimateReplayDelayLeft - deltaTime);
                if (_ultimateReplayDelayLeft > 0f)
                {
                    return;
                }

                _ultimateReplayTimeLeft = ResolveUltimateDashDuration();
                StartUltimateReplayGhost();
            }

            if (_ultimateReplayTimeLeft <= 0f)
            {
                if (_ultimateReplayImpactPending)
                {
                    ApplyUltimateReplayImpact();
                    _ultimateReplayImpactPending = false;
                }

                _ultimateReplayQueued = false;
                CleanupUltimateReplayGhost();
                return;
            }

            _ultimateReplayTimeLeft = Mathf.Max(0f, _ultimateReplayTimeLeft - deltaTime);
            float progress = 1f - (_ultimateReplayTimeLeft / Mathf.Max(0.01f, ResolveUltimateDashDuration()));
            UpdateUltimateReplayGhost(progress);

            if (_ultimateReplayImpactPending && progress >= 1f)
            {
                ApplyUltimateReplayImpact();
                _ultimateReplayImpactPending = false;
            }

            if (_ultimateReplayTimeLeft <= 0f)
            {
                _ultimateReplayQueued = false;
                CleanupUltimateReplayGhost();
            }
        }

        private void StartUltimateReplayGhost()
        {
            if (_ultimateReplayGhostSprite == null)
            {
                return;
            }

            if (_ultimateReplayGhostObject == null)
            {
                _ultimateReplayGhostObject = new GameObject(name + "_UltimateReplayGhost");
                _ultimateReplayGhostRenderer = _ultimateReplayGhostObject.AddComponent<SpriteRenderer>();
            }

            _ultimateReplayGhostObject.SetActive(true);
            _ultimateReplayGhostRenderer.sprite = _ultimateReplayGhostSprite;
            _ultimateReplayGhostRenderer.flipX = _ultimateReplayGhostFlipX;
            _ultimateReplayGhostRenderer.sortingLayerID = _ultimateReplayGhostSortingLayerId;
            _ultimateReplayGhostRenderer.sortingOrder = _ultimateReplayGhostSortingOrder;
            _ultimateReplayGhostObject.transform.position = _ultimateReplayRootStart + _ultimateReplayGhostSpriteOffset;
            _ultimateReplayGhostObject.transform.eulerAngles = _ultimateReplayGhostEulerAngles;
            _ultimateReplayGhostObject.transform.localScale = _ultimateReplayGhostScale;
            _ultimateReplayGhostRenderer.color = new Color(0.75f, 0.08f, 0.12f, 0.55f);
        }

        private void UpdateUltimateReplayGhost(float progress)
        {
            if (_ultimateReplayGhostObject == null || _ultimateReplayGhostRenderer == null)
            {
                return;
            }

            Vector2 rootPosition = Vector2.Lerp(_ultimateReplayRootStart, _ultimateReplayRootEnd, Mathf.Clamp01(progress));
            _ultimateReplayGhostObject.transform.position = rootPosition + _ultimateReplayGhostSpriteOffset;
            Color ghostColor = _ultimateReplayGhostRenderer.color;
            ghostColor.a = Mathf.Lerp(0.55f, 0.18f, Mathf.Clamp01(progress));
            _ultimateReplayGhostRenderer.color = ghostColor;
        }

        private void CaptureUltimateReplayGhostVisual()
        {
            if (spriteRenderer == null || spriteRenderer.sprite == null)
            {
                _ultimateReplayGhostSprite = null;
                return;
            }

            Vector2 rootEnd = body != null ? body.position : (Vector2)transform.position;
            _ultimateReplayGhostSprite = spriteRenderer.sprite;
            _ultimateReplayGhostFlipX = spriteRenderer.flipX;
            _ultimateReplayGhostSortingLayerId = spriteRenderer.sortingLayerID;
            _ultimateReplayGhostSortingOrder = spriteRenderer.sortingOrder - 1;
            _ultimateReplayGhostScale = spriteRenderer.transform.lossyScale;
            _ultimateReplayGhostEulerAngles = spriteRenderer.transform.eulerAngles;
            _ultimateReplayGhostSpriteOffset = (Vector2)spriteRenderer.transform.position - rootEnd;
        }

        private void CaptureUltimateReplayShape()
        {
            Collider2D authoredCollider = ultimateHitboxAnchor != null ? ultimateHitboxAnchor.AttachedCollider : null;
            if (authoredCollider is BoxCollider2D box)
            {
                _ultimateReplayShapeKind = UltimateReplayShapeKind.Box;
                _ultimateReplayImpactCenter = box.transform.TransformPoint(box.offset);
                _ultimateReplayShapeSize = ScaleAbsolute(box.size, box.transform.lossyScale);
                _ultimateReplayShapeAngle = box.transform.eulerAngles.z;
                return;
            }

            if (authoredCollider is CapsuleCollider2D capsule)
            {
                _ultimateReplayShapeKind = UltimateReplayShapeKind.Capsule;
                _ultimateReplayImpactCenter = capsule.transform.TransformPoint(capsule.offset);
                _ultimateReplayShapeSize = ScaleAbsolute(capsule.size, capsule.transform.lossyScale);
                _ultimateReplayShapeAngle = capsule.transform.eulerAngles.z;
                _ultimateReplayCapsuleDirection = capsule.direction;
                return;
            }

            if (authoredCollider is CircleCollider2D circle)
            {
                _ultimateReplayShapeKind = UltimateReplayShapeKind.Circle;
                _ultimateReplayImpactCenter = circle.transform.TransformPoint(circle.offset);
                _ultimateReplayShapeRadius = circle.radius * Mathf.Max(Mathf.Abs(circle.transform.lossyScale.x), Mathf.Abs(circle.transform.lossyScale.y));
                return;
            }

            _ultimateReplayShapeKind = UltimateReplayShapeKind.Circle;
            _ultimateReplayImpactCenter = GetUltimateHitboxCenter();
            _ultimateReplayShapeRadius = ResolveUltimateRadius();
        }

        private int CollectCurrentUltimateHits(Collider2D[] results)
        {
            return TryOverlapAuthoredHitbox(ultimateHitboxAnchor, GetMeleeContactFilter(), results, out int authoredHitCount)
                ? authoredHitCount
                : Physics2D.OverlapCircle(
                    GetUltimateHitboxCenter(),
                    ResolveUltimateRadius(),
                    GetMeleeContactFilter(),
                    results);
        }

        private void ApplyUltimateReplayImpact()
        {
            int hitCount = CollectUltimateReplayHits(_overlapHits);
            ApplyUltimateDamageHits(_overlapHits, hitCount);
        }

        private int CollectUltimateReplayHits(Collider2D[] results)
        {
            ContactFilter2D filter = GetMeleeContactFilter();
            switch (_ultimateReplayShapeKind)
            {
                case UltimateReplayShapeKind.Box:
                    return Physics2D.OverlapBox(_ultimateReplayImpactCenter, _ultimateReplayShapeSize, _ultimateReplayShapeAngle, filter, results);
                case UltimateReplayShapeKind.Capsule:
                    return Physics2D.OverlapCapsule(_ultimateReplayImpactCenter, _ultimateReplayShapeSize, _ultimateReplayCapsuleDirection, _ultimateReplayShapeAngle, filter, results);
                default:
                    return Physics2D.OverlapCircle(_ultimateReplayImpactCenter, _ultimateReplayShapeRadius, filter, results);
            }
        }

        private void ApplyUltimateDamageHits(Collider2D[] hits, int hitCount)
        {
            for (int index = 0; index < hitCount; index += 1)
            {
                Collider2D hit = hits[index];
                if (hit == null)
                {
                    continue;
                }

                PlayerController target = hit.GetComponentInParent<PlayerController>();
                if (target == null || target == this || target.IsDead)
                {
                    continue;
                }

                target.Kill();
            }
        }

        private void ApplyTransientVelocity(ref Vector2 velocity, Vector2 previousVelocity, Vector2 currentVelocity, ref Vector2 lastVelocity)
        {
            if (previousVelocity != Vector2.zero && currentVelocity == Vector2.zero)
            {
                velocity += previousVelocity;
                lastVelocity = Vector2.zero;
                return;
            }

            velocity += currentVelocity;
            lastVelocity = currentVelocity;
        }

        private bool CanBlockProjectileWithUltimate()
        {
            return _ultimateProjectileBlockTimer > 0f;
        }

        private void CleanupUltimateReplayGhost()
        {
            if (_ultimateReplayGhostObject != null)
            {
                _ultimateReplayGhostObject.SetActive(false);
            }
        }

        private static Vector2 ScaleAbsolute(Vector2 value, Vector3 scale)
        {
            return new Vector2(Mathf.Abs(value.x * scale.x), Mathf.Abs(value.y * scale.y));
        }

        private void TryCheckHeadStomp()
        {
            if (_isDead || body == null || bodyCollider == null || body.linearVelocity.y >= 0f)
            {
                return;
            }

            Bounds selfBounds = bodyCollider.bounds;
            Rect selfFeetRect = BuildRect(
                new Vector2(selfBounds.min.x, selfBounds.min.y),
                new Vector2(selfBounds.size.x, Mathf.Max(10f, selfBounds.size.y * 0.2f)));

            PlayerController[] players = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
            foreach (PlayerController other in players)
            {
                if (other == null || other == this || other.IsDead || other.bodyCollider == null)
                {
                    continue;
                }

                Bounds otherBounds = other.bodyCollider.bounds;
                float headHeight = Mathf.Max(12f, otherBounds.size.y * 0.25f);
                Rect otherHeadRect = BuildRect(
                    new Vector2(otherBounds.min.x, otherBounds.max.y - headHeight),
                    new Vector2(otherBounds.size.x, headHeight));

                if (!selfFeetRect.Overlaps(otherHeadRect))
                {
                    continue;
                }

                other.Kill();
                body.linearVelocity = new Vector2(body.linearVelocity.x, ResolveJumpVelocity() * 0.8f);
                TriggerJumpStartAnimation();
                break;
            }
        }

        private void UpdatePresentationState()
        {
            if (spriteRenderer == null)
            {
                return;
            }

            if (_isDead)
            {
                spriteRenderer.color = Color.white;
                return;
            }

            if (IsDashAnimationActive)
            {
                spriteRenderer.color = new Color(0.75f, 0.95f, 1f, 1f);
                return;
            }

            if (_meleeAnimationTimeLeft > 0f)
            {
                spriteRenderer.color = new Color(1f, 0.78f, 0.78f, 1f);
                return;
            }

            if (_aimHoldActive)
            {
                spriteRenderer.color = new Color(1f, 0.96f, 0.72f, 1f);
                return;
            }

            spriteRenderer.color = Color.white;
        }

        private Vector2 GetProjectileSpawnPoint(Vector2 aimDirection, int facingDirection)
        {
            Vector2 basePosition = ResolveProjectileOriginWorldPosition(facingDirection);
            basePosition += aimDirection * ResolveProjectileForward();
            basePosition += new Vector2(facingDirection * ResolveProjectileForwardFacing(), ResolveProjectileVerticalOffset());
            return basePosition;
        }

        private Vector2 GetMeleeHitboxCenter()
        {
            if (TryResolveAnchorWorldPosition(meleeHitboxAnchor, out Vector2 authoredCenter))
            {
                return authoredCenter;
            }

            Vector2 colliderSize = ResolveColliderSize();
            Vector2 colliderOffset = ResolveColliderOffset();
            Vector2 chestOffset = new Vector2(colliderSize.x * 0.15f * _facing, colliderSize.y * 0.15f);
            Vector2 anchorOffset = new Vector2((colliderSize.x * 0.5f + 12f) * _facing, 0f);
            return (Vector2)transform.position + colliderOffset + chestOffset + anchorOffset;
        }

        private Vector2 GetMeleeHitboxSize()
        {
            if (meleeHitboxAnchor != null && meleeHitboxAnchor.boxSize.sqrMagnitude > 0.001f)
            {
                return meleeHitboxAnchor.boxSize;
            }

            ActionColliderOverride overrideData = FindActionColliderOverride("melee");
            if (overrideData != null)
            {
                return overrideData.size;
            }

            Vector2 colliderSize = ResolveColliderSize();
            return new Vector2(
                Mathf.Max(72f, colliderSize.x * 0.85f),
                Mathf.Max(64f, colliderSize.y * 0.45f));
        }

        private Vector2 GetUltimateHitboxCenter()
        {
            if (TryResolveAnchorWorldPosition(ultimateHitboxAnchor, out Vector2 authoredCenter))
            {
                return authoredCenter;
            }

            Vector2 colliderSize = ResolveColliderSize();
            Vector2 colliderOffset = ResolveColliderOffset();
            Vector2 chestOffset = new Vector2(colliderSize.x * 0.2f * _facing, colliderSize.y * 0.1f);
            Vector2 forwardOffset = new Vector2((colliderSize.x * 0.4f + ResolveUltimateRadius() * 0.4f) * _facing, 0f);
            return (Vector2)transform.position + colliderOffset + chestOffset + forwardOffset;
        }

        private Vector2 ResolveAimDirection(PlayerInputFrame frame)
        {
            if (frame.aim.sqrMagnitude > 0.01f)
            {
                return frame.aim.normalized;
            }

            return new Vector2(_facing, 0f);
        }

        private int ResolveShotFacing(Vector2 aimDirection)
        {
            if (Mathf.Abs(aimDirection.x) > 0.01f)
            {
                return aimDirection.x > 0f ? 1 : -1;
            }

            return _facing == 0 ? 1 : (_facing > 0 ? 1 : -1);
        }

        private Vector2 ResolveDashDirection()
        {
            int facingDirection = _facing == 0 ? 1 : (_facing > 0 ? 1 : -1);
            return new Vector2(facingDirection, 0f);
        }

        private void PlayActionSfx(string actionName)
        {
            if (string.IsNullOrWhiteSpace(actionName))
            {
                return;
            }

            EnsureAudioController();
            if (_audioController != null)
            {
                _audioController.PlayAction(actionName);
            }
        }

        private bool CanParryProjectile()
        {
            return _dashParryTimer > 0f || _dashPressTimer > 0f;
        }

        private bool CanSeverIncomingProjectile(ProjectileController projectile)
        {
            if (!CanSeverProjectilesWithMelee() || !IsMeleeActive || projectile == null || projectile.IsStuck || projectile.IsDisarmed)
            {
                return false;
            }

            Collider2D projectileCollider = projectile.hitCollider;
            if (projectileCollider == null)
            {
                return false;
            }

            Collider2D meleeCollider = meleeHitboxAnchor != null ? meleeHitboxAnchor.AttachedCollider : null;
            if (meleeCollider != null)
            {
                return meleeCollider.bounds.Intersects(projectileCollider.bounds);
            }

            Bounds projectileBounds = projectileCollider.bounds;
            Bounds meleeBounds = new Bounds(GetMeleeHitboxCenter(), GetMeleeHitboxSize());
            return meleeBounds.Intersects(projectileBounds);
        }

        private bool TrySeverProjectileWithMelee(Collider2D hit)
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
            if (_meleeHitIds.Contains(projectileId))
            {
                return true;
            }

            _meleeHitIds.Add(projectileId);
            projectile.SeverByMelee();
            return true;
        }

        private bool CanSeverProjectilesWithMelee()
        {
            return characterDefinition != null && characterDefinition.meleeCanSeverProjectiles;
        }

        private bool QueryGround(out Vector2 hitNormal)
        {
            hitNormal = Vector2.zero;

            if (bodyCollider == null)
            {
                return false;
            }

            Vector2 position = body != null ? body.position : (Vector2)transform.position;
            GetColliderGeometry(position, out Vector2 center, out Vector2 extents);

            float inset = Mathf.Min(extents.x - CollisionSkinWidth, Mathf.Max(RayInsetPadding, extents.x * 0.6f));
            if (inset < CollisionSkinWidth)
            {
                inset = CollisionSkinWidth;
            }

            float rayDistance = Mathf.Max(CollisionSkinWidth + 1f, groundCheckDistance + CollisionSkinWidth);
            Vector2[] rayOrigins =
            {
                center + new Vector2(0f, -extents.y + CollisionSkinWidth),
                center + new Vector2(-extents.x + inset, -extents.y + CollisionSkinWidth),
                center + new Vector2(extents.x - inset, -extents.y + CollisionSkinWidth),
            };

            float closestDistance = float.MaxValue;
            for (int originIndex = 0; originIndex < rayOrigins.Length; originIndex += 1)
            {
                int hitCount = Physics2D.Raycast(rayOrigins[originIndex], Vector2.down, GetDefaultContactFilter(), _castHits, rayDistance);
                for (int hitIndex = 0; hitIndex < hitCount; hitIndex += 1)
                {
                    RaycastHit2D hit = _castHits[hitIndex];
                    if (ShouldIgnoreCastHit(hit) || hit.normal.y < 0.35f)
                    {
                        continue;
                    }

                    if (hit.distance < closestDistance)
                    {
                        closestDistance = hit.distance;
                        hitNormal = hit.normal;
                    }
                }
            }

            return closestDistance != float.MaxValue;
        }

        private bool TryGetWallNormal(out Vector2 wallNormal)
        {
            if (bodyCollider == null)
            {
                wallNormal = Vector2.zero;
                return false;
            }

            Vector2 position = body != null ? body.position : (Vector2)transform.position;
            GetColliderGeometry(position, out Vector2 center, out Vector2 extents);

            float inset = Mathf.Min(extents.y - CollisionSkinWidth, Mathf.Max(RayInsetPadding, extents.y * 0.55f));
            if (inset < CollisionSkinWidth)
            {
                inset = CollisionSkinWidth;
            }

            float rayDistance = Mathf.Max(CollisionSkinWidth + 1f, wallCheckDistance + CollisionSkinWidth);
            Vector2[] leftOrigins =
            {
                center + new Vector2(-extents.x + CollisionSkinWidth, 0f),
                center + new Vector2(-extents.x + CollisionSkinWidth, extents.y - inset),
                center + new Vector2(-extents.x + CollisionSkinWidth, -extents.y + inset),
            };

            Vector2[] rightOrigins =
            {
                center + new Vector2(extents.x - CollisionSkinWidth, 0f),
                center + new Vector2(extents.x - CollisionSkinWidth, extents.y - inset),
                center + new Vector2(extents.x - CollisionSkinWidth, -extents.y + inset),
            };

            if (TryRaycastNormals(leftOrigins, Vector2.left, rayDistance, out Vector2 leftNormal))
            {
                wallNormal = leftNormal;
                return true;
            }

            if (TryRaycastNormals(rightOrigins, Vector2.right, rayDistance, out Vector2 rightNormal))
            {
                wallNormal = rightNormal;
                return true;
            }

            wallNormal = Vector2.zero;
            return false;
        }

        private bool TryRaycastNormals(Vector2[] origins, Vector2 direction, float rayDistance, out Vector2 resolvedNormal)
        {
            resolvedNormal = Vector2.zero;
            float closestDistance = float.MaxValue;

            for (int originIndex = 0; originIndex < origins.Length; originIndex += 1)
            {
                int hitCount = Physics2D.Raycast(origins[originIndex], direction, GetDefaultContactFilter(), _castHits, rayDistance);
                for (int hitIndex = 0; hitIndex < hitCount; hitIndex += 1)
                {
                    RaycastHit2D hit = _castHits[hitIndex];
                    if (ShouldIgnoreCastHit(hit))
                    {
                        continue;
                    }

                    if (Mathf.Abs(hit.normal.x) < 0.35f || hit.distance >= closestDistance)
                    {
                        continue;
                    }

                    closestDistance = hit.distance;
                    resolvedNormal = hit.normal;
                }
            }

            return closestDistance != float.MaxValue;
        }

        private ContactFilter2D GetDefaultContactFilter()
        {
            var filter = new ContactFilter2D
            {
                useTriggers = false,
                useLayerMask = groundMask.value != 0,
                layerMask = groundMask
            };
            return filter;
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

        private bool TryConsumeJump(ref Vector2 velocity)
        {
            if (!HasBufferedJump())
            {
                return false;
            }

            if (_isGrounded || _coyoteTimeLeft > 0f)
            {
                velocity.y = ResolveJumpVelocity();
                ConsumeBufferedJump();
                _coyoteTimeLeft = 0f;
                TriggerJumpStartAnimation();
                return true;
            }

            if (_isTouchingWall)
            {
                velocity.y = ResolveWallJumpVerticalForce();
                velocity.x = _wallNormal.x * ResolveWallJumpHorizontalForce();
                ConsumeBufferedJump();
                TriggerJumpStartAnimation();
                return true;
            }

            return false;
        }

        private bool HasBufferedJump()
        {
            return _jumpBufferLeft > 0f;
        }

        private void ConsumeBufferedJump()
        {
            _jumpBufferLeft = 0f;
        }

        private void TriggerJumpStartAnimation()
        {
            float duration = ResolveActionDuration("jump_start", JumpStartAnimationDuration);
            _jumpStartTimeLeft = duration;
            LockActionForDuration("jump_start", duration, Mathf.Min(duration, 0.05f), false);
        }

        private void TriggerDashAnimation(float duration)
        {
            _dashAnimationHoldTimeLeft = Mathf.Max(duration, 0f);
            LockActionForDuration("dash", duration, Mathf.Min(duration, 0.12f), false);
        }

        private void TriggerShootAnimation(float duration)
        {
            _shootAnimationTimeLeft = Mathf.Max(duration, 0f);
            LockActionForDuration("shoot", duration, Mathf.Min(duration, 0.05f), true);
        }

        private void TriggerMeleeAnimation(float duration)
        {
            _meleeAnimationTimeLeft = Mathf.Max(duration, 0f);
            LockActionForDuration("melee", duration, Mathf.Min(duration, 0.08f), false);
        }

        private void UpdateActionLockTimers(float deltaTime)
        {
            for (int index = _actionLockEntries.Count - 1; index >= 0; index -= 1)
            {
                ActionLockEntry entry = _actionLockEntries[index];
                entry.remaining -= deltaTime;
                if (entry.remaining <= 0f)
                {
                    _actionLockEntries.RemoveAt(index);
                    ReleaseActionOverride(entry.action);
                    continue;
                }

                _actionLockEntries[index] = entry;
            }
        }

        private void UpdateActionOverrideState()
        {
            if (!string.IsNullOrEmpty(_pendingOverrideAction) && CanApplyOverride(_pendingOverridePriority))
            {
                ApplyPendingOverride();
                return;
            }

            if (ShouldReleaseCancelableOverride())
            {
                ClearCurrentOverride();
            }

            if (string.IsNullOrEmpty(_currentOverrideAction) && !string.IsNullOrEmpty(_pendingOverrideAction))
            {
                ApplyPendingOverride();
            }
        }

        private void LockActionForDuration(string actionName, float duration, float lockDuration, bool defaultCancelable)
        {
            if (string.IsNullOrWhiteSpace(actionName) || duration <= 0f)
            {
                return;
            }

            bool cancelable = ResolveActionCancelable(actionName, defaultCancelable);
            if (!cancelable)
            {
                lockDuration = Mathf.Max(lockDuration, duration);
            }

            bool updatedExistingEntry = false;
            for (int index = 0; index < _actionLockEntries.Count; index += 1)
            {
                ActionLockEntry entry = _actionLockEntries[index];
                if (!string.Equals(entry.action, actionName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                entry.remaining = Mathf.Max(entry.remaining, duration);
                entry.cancelable = cancelable;
                _actionLockEntries[index] = entry;
                updatedExistingEntry = true;
                break;
            }

            if (!updatedExistingEntry)
            {
                _actionLockEntries.Add(new ActionLockEntry
                {
                    action = actionName,
                    remaining = duration,
                    cancelable = cancelable,
                });
            }

            RequestActionOverride(actionName, GetActionPriority(actionName), lockDuration);
        }

        private void RequestActionOverride(string actionName, int priority, float lockDuration)
        {
            if (string.IsNullOrWhiteSpace(actionName))
            {
                return;
            }

            if (string.Equals(_currentOverrideAction, actionName, StringComparison.OrdinalIgnoreCase))
            {
                _currentOverridePriority = priority;
                _currentOverrideLockLeft = Mathf.Max(_currentOverrideLockLeft, lockDuration);
                if (string.Equals(_pendingOverrideAction, actionName, StringComparison.OrdinalIgnoreCase))
                {
                    _pendingOverrideAction = string.Empty;
                    _pendingOverridePriority = PriorityNegativeInfinity;
                    _pendingOverrideLockLeft = 0f;
                }

                return;
            }

            if (CanApplyOverride(priority))
            {
                ApplyOverrideAction(actionName, priority, lockDuration);
                return;
            }

            if (string.IsNullOrEmpty(_pendingOverrideAction) || priority >= _pendingOverridePriority)
            {
                _pendingOverrideAction = actionName;
                _pendingOverridePriority = priority;
                _pendingOverrideLockLeft = lockDuration;
            }
        }

        private void ReleaseActionOverride(string actionName)
        {
            if (string.IsNullOrWhiteSpace(actionName))
            {
                return;
            }

            if (string.Equals(actionName, _currentOverrideAction, StringComparison.OrdinalIgnoreCase))
            {
                ClearCurrentOverride();
                ApplyPendingOverride();
                return;
            }

            if (string.Equals(actionName, _pendingOverrideAction, StringComparison.OrdinalIgnoreCase))
            {
                _pendingOverrideAction = string.Empty;
                _pendingOverridePriority = PriorityNegativeInfinity;
                _pendingOverrideLockLeft = 0f;
            }
        }

        private bool CanApplyOverride(int priority)
        {
            if (string.IsNullOrEmpty(_currentOverrideAction))
            {
                return true;
            }

            if (TryGetActionLockEntry(_currentOverrideAction, out ActionLockEntry currentEntry)
                && currentEntry.remaining > 0f
                && !currentEntry.cancelable)
            {
                return false;
            }

            if (priority > _currentOverridePriority)
            {
                return true;
            }

            return priority == _currentOverridePriority && _currentOverrideLockLeft <= 0f;
        }

        private bool ShouldReleaseCancelableOverride()
        {
            if (string.IsNullOrEmpty(_currentOverrideAction) || _currentOverrideLockLeft > 0f)
            {
                return false;
            }

            if (!TryGetActionLockEntry(_currentOverrideAction, out ActionLockEntry currentEntry) || !currentEntry.cancelable)
            {
                return false;
            }

            string nextAction = ResolveBaseVisualActionKey();
            return !string.IsNullOrWhiteSpace(nextAction)
                && !string.Equals(nextAction, _currentOverrideAction, StringComparison.OrdinalIgnoreCase);
        }

        private bool TryGetActionLockEntry(string actionName, out ActionLockEntry resolvedEntry)
        {
            for (int index = 0; index < _actionLockEntries.Count; index += 1)
            {
                ActionLockEntry entry = _actionLockEntries[index];
                if (!string.Equals(entry.action, actionName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                resolvedEntry = entry;
                return true;
            }

            resolvedEntry = default;
            return false;
        }

        private void ApplyOverrideAction(string actionName, int priority, float lockDuration)
        {
            _currentOverrideAction = actionName;
            _currentOverridePriority = priority;
            _currentOverrideLockLeft = Mathf.Max(lockDuration, 0f);

            if (string.Equals(actionName, _pendingOverrideAction, StringComparison.OrdinalIgnoreCase))
            {
                _pendingOverrideAction = string.Empty;
                _pendingOverridePriority = PriorityNegativeInfinity;
                _pendingOverrideLockLeft = 0f;
            }
        }

        private void ApplyPendingOverride()
        {
            if (string.IsNullOrEmpty(_pendingOverrideAction))
            {
                return;
            }

            ApplyOverrideAction(_pendingOverrideAction, _pendingOverridePriority, _pendingOverrideLockLeft);
        }

        private string ResolveVisualActionKey()
        {
            if (!string.IsNullOrEmpty(_currentOverrideAction))
            {
                return _currentOverrideAction;
            }

            return ResolveBaseVisualActionKey();
        }

        private string ResolveBaseVisualActionKey()
        {
            if (_isDead)
            {
                return "death";
            }

            if (IsDashAnimationActive)
            {
                return "dash";
            }

            if (_aimHoldActive && _shootAnimationTimeLeft <= 0f)
            {
                return "aim";
            }

            if (_shootAnimationTimeLeft > 0f)
            {
                return "shoot";
            }

            if (_ultimateAnimationTimeLeft > 0f)
            {
                return "ult";
            }

            if (_meleeAnimationTimeLeft > 0f)
            {
                return "melee";
            }

            if (_jumpStartTimeLeft > 0f)
            {
                return "jump_start";
            }

            if (!_isGrounded)
            {
                return "jump_air";
            }

            if (Mathf.Abs(HorizontalVelocity) > 10f || Mathf.Abs(_currentInputFrame.axis) > 0.1f)
            {
                if (characterDefinition != null && characterDefinition.HasActionAnimation("running"))
                {
                    return "running";
                }

                return "walk";
            }

            return "idle";
        }

        private void ClearCurrentOverride()
        {
            _currentOverrideAction = string.Empty;
            _currentOverridePriority = PriorityNegativeInfinity;
            _currentOverrideLockLeft = 0f;
        }

        private Vector2 ResolveSpawnBodyPosition(Vector2 footWorldPosition)
        {
            Vector2 colliderSize = ResolveColliderSize();
            Vector2 colliderOffset = ResolveColliderOffset();
            return footWorldPosition - colliderOffset + new Vector2(0f, (colliderSize.y * 0.5f) + SpawnGroundPadding);
        }

        private void SnapToGroundAtSpawn(Vector2 footWorldPosition)
        {
            if (body == null || bodyCollider == null)
            {
                return;
            }

            Vector2 colliderSize = ResolveColliderSize();
            float horizontalInset = Mathf.Max(6f, colliderSize.x * 0.25f);
            float castHeight = Mathf.Max(groundCheckDistance + 24f, GroundSnapDistance * 0.5f);
            float castDistance = Mathf.Max(groundCheckDistance + 24f, GroundSnapDistance);
            Vector2[] origins =
            {
                footWorldPosition + new Vector2(0f, castHeight),
                footWorldPosition + new Vector2(-horizontalInset, castHeight),
                footWorldPosition + new Vector2(horizontalInset, castHeight),
            };

            bool foundGround = false;
            float highestGroundY = float.MinValue;
            for (int originIndex = 0; originIndex < origins.Length; originIndex += 1)
            {
                int hitCount = Physics2D.Raycast(origins[originIndex], Vector2.down, GetDefaultContactFilter(), _castHits, castDistance);
                for (int hitIndex = 0; hitIndex < hitCount; hitIndex += 1)
                {
                    RaycastHit2D hit = _castHits[hitIndex];
                    if (ShouldIgnoreCastHit(hit) || hit.normal.y < 0.35f)
                    {
                        continue;
                    }

                    highestGroundY = Mathf.Max(highestGroundY, hit.point.y);
                    foundGround = true;
                }
            }

            if (!foundGround)
            {
                return;
            }

            Vector2 snappedPosition = ResolveSpawnBodyPosition(new Vector2(footWorldPosition.x, highestGroundY));
            body.position = snappedPosition;
            transform.position = snappedPosition;
        }

        private bool ShouldIgnoreCastHit(RaycastHit2D hit)
        {
            if (hit.collider == null || hit.collider == bodyCollider)
            {
                return true;
            }

            return hit.collider.GetComponentInParent<PlayerController>() != null;
        }

        private ActionColliderOverride FindActionColliderOverride(string actionName)
        {
            return characterDefinition != null ? characterDefinition.FindActionColliderOverride(actionName) : null;
        }

        private void ApplyRuntimeColliderOverride(string actionName)
        {
            if (bodyCollider == null)
            {
                return;
            }

            ActionColliderOverride overrideData = FindActionColliderOverride(actionName);
            Vector2 targetSize = overrideData != null ? overrideData.size : ResolveColliderSize();
            Vector2 targetOffset = overrideData != null ? overrideData.offset : ResolveColliderOffset();
            string resolvedAction = overrideData != null ? actionName : string.Empty;

            if (_activeColliderAction == resolvedAction
                && bodyCollider.size == targetSize
                && bodyCollider.offset == targetOffset)
            {
                return;
            }

            bodyCollider.size = targetSize;
            bodyCollider.offset = targetOffset;
            _activeColliderAction = resolvedAction;
        }

        private static Rect BuildRect(Vector2 position, Vector2 size)
        {
            return new Rect(position, size);
        }

        private static int GetActionPriority(string actionName)
        {
            switch (actionName)
            {
                case "death":
                    return 120;
                case "ult":
                    return 100;
                case "melee":
                    return 90;
                case "dash":
                    return 80;
                case "shoot":
                    return 70;
                case "aim":
                    return 60;
                case "jump_start":
                    return 55;
                case "jump_air":
                    return 50;
                case "running":
                    return 40;
                case "walk":
                    return 30;
                case "crouch":
                    return 25;
                case "idle":
                    return 10;
                default:
                    return 0;
            }
        }

        private float ResolveAcceleration() => (HasStatOverrides() ? characterDefinition.acceleration : DefaultAcceleration) * ResolveMoveScale();
        private float ResolveFriction() => (HasStatOverrides() ? characterDefinition.friction : DefaultFriction) * ResolveMoveScale();
        private float ResolveGravity() => (HasStatOverrides() ? characterDefinition.gravity : DefaultGravity) * ResolveGravityScale();
        private float ResolveMaxFallSpeed() => (HasStatOverrides() ? characterDefinition.maxFallSpeed : DefaultMaxFallSpeed) * ResolveGravityScale();
        private float ResolveJumpVelocity() => (HasStatOverrides() ? characterDefinition.jumpVelocity : DefaultJumpVelocity) * ResolveJumpScale();
        private float ResolveShootCooldown() => HasStatOverrides() ? characterDefinition.shootCooldown : DefaultShootCooldown;
        private int ResolveMaxArrows() => HasStatOverrides() ? characterDefinition.maxArrows : DefaultMaxArrows;
        private float ResolveMeleeCooldown() => HasStatOverrides() ? characterDefinition.meleeCooldown : DefaultMeleeCooldown;
        private float ResolveMeleeDuration() => HasStatOverrides() ? characterDefinition.meleeDuration : DefaultMeleeDuration;
        private float ResolveWallJumpHorizontalForce() => (characterDefinition != null ? characterDefinition.wallJumpHorizontalForce : DefaultWallJumpHorizontalForce) * ResolveJumpScale();
        private float ResolveWallJumpVerticalForce() => (characterDefinition != null ? characterDefinition.wallJumpVerticalForce : DefaultWallJumpVerticalForce) * ResolveJumpScale();
        private float ResolveWallSlideSpeed() => (characterDefinition != null ? characterDefinition.wallSlideSpeed : DefaultWallSlideSpeed) * ResolveGravityScale();
        private float ResolveWallGravityScale() => characterDefinition != null ? characterDefinition.wallGravityScale : DefaultWallGravityScale;
        private float ResolveDashMultiplier() => characterDefinition != null ? characterDefinition.dashMultiplier : DefaultDashMultiplier;
        private float ResolveDashDuration() => characterDefinition != null ? characterDefinition.dashDuration : DefaultDashDuration;
        private float ResolveDashCooldown() => characterDefinition != null ? characterDefinition.dashCooldown : DefaultDashCooldown;
        private float ResolveDashDistance() => (characterDefinition != null ? characterDefinition.dashDistance : DefaultDashDistance) * ResolveDashScale();
        private float ResolveDashUpwardMultiplier() => characterDefinition != null ? characterDefinition.dashUpwardMultiplier : DefaultDashUpwardMultiplier;
        private Vector2 ResolveColliderSize() => characterDefinition != null ? characterDefinition.colliderSize : new Vector2(90f, 210f);
        private Vector2 ResolveColliderOffset() => characterDefinition != null ? characterDefinition.colliderOffset : Vector2.zero;
        private float ResolveProjectileForward() => characterDefinition != null ? characterDefinition.projectileForward : 80f;
        private float ResolveProjectileForwardFacing() => characterDefinition != null ? characterDefinition.projectileForwardFacing : 0f;
        private float ResolveProjectileVerticalOffset() => characterDefinition != null ? characterDefinition.projectileVerticalOffset : 0f;
        private float ResolveProjectileInheritVelocityFactor() => characterDefinition != null ? characterDefinition.projectileInheritVelocityFactor : 1f;
        private float ResolveProjectileScale() => characterDefinition != null ? characterDefinition.projectileScale : 1f;
        private Vector2 ResolveProjectileOriginOffset() => characterDefinition != null ? characterDefinition.projectileOriginOffset : Vector2.zero;
        private ProjectileOriginMode ResolveProjectileOriginMode() => characterDefinition != null ? characterDefinition.projectileOriginMode : ProjectileOriginMode.BowNode;
        private Sprite ResolveProjectileSprite() => characterDefinition != null ? characterDefinition.projectileSprite : null;

        private Vector2 ResolveConfiguredSpawnWorldPosition()
        {
            if (spawnAnchor != null)
            {
                return spawnAnchor.ResolveWorldPosition(transform, 1);
            }

            return body != null ? body.position : (Vector2)transform.position;
        }

        private void UpdateProjectileOriginSocket()
        {
            if (projectileOrigin == null)
            {
                return;
            }

            projectileOrigin.localPosition = ResolveProjectileOriginLocalPosition(_facing);
            projectileOrigin.localRotation = Quaternion.identity;
            projectileOrigin.localScale = Vector3.one;
        }

        private void SyncCombatAnchors()
        {
            ApplyAnchorRuntimePose(meleeHitboxAnchor);
            ApplyAnchorRuntimePose(ultimateHitboxAnchor);
            ApplyAnchorRuntimePose(spawnAnchor);
        }

        private Vector2 ResolveProjectileOriginWorldPosition(int facingDirection)
        {
            return transform.TransformPoint(ResolveProjectileOriginLocalPosition(facingDirection));
        }

        private Vector3 ResolveProjectileOriginLocalPosition(int facingDirection)
        {
            if (TryResolveProjectileOriginLocalAuthoring(facingDirection, out Vector3 authoredLocalPosition))
            {
                return authoredLocalPosition;
            }

            Vector2 localPosition = ResolveProjectileOriginBaseLocalPosition();
            Vector2 originOffset = ResolveProjectileOriginOffset();
            float horizontalDirection = facingDirection < 0 ? -1f : 1f;
            localPosition += new Vector2(Mathf.Abs(originOffset.x) * horizontalDirection, originOffset.y);
            return new Vector3(localPosition.x, localPosition.y, 0f);
        }

        private Vector2 ResolveProjectileOriginBaseLocalPosition()
        {
            Vector2 colliderSize = ResolveColliderSize();
            Vector2 colliderOffset = ResolveColliderOffset();

            switch (ResolveEffectiveProjectileOriginMode())
            {
                case ProjectileOriginMode.ColliderCenter:
                    return colliderOffset;
                case ProjectileOriginMode.ColliderTop:
                    return colliderOffset + new Vector2(0f, colliderSize.y * 0.5f);
                case ProjectileOriginMode.Chest:
                    return colliderOffset + new Vector2(0f, colliderSize.y * 0.15f);
                default:
                    return Vector2.zero;
            }
        }

        private ProjectileOriginMode ResolveEffectiveProjectileOriginMode()
        {
            if (characterDefinition != null && characterDefinition.projectileUseBowNode)
            {
                return ProjectileOriginMode.BowNode;
            }

            return ResolveProjectileOriginMode();
        }

        private bool HasStatOverrides()
        {
            return characterDefinition != null && characterDefinition.overridesStats;
        }

                private bool HasUltimateConfigured()
        {
            if (characterDefinition == null)
            {
                return false;
            }

            return characterDefinition.HasActionAnimation("ult")
                || ResolveUltimateDashDistance() > 0.01f
                || ResolveUltimateReplayDelay() > 0.01f;
        }

        private float ResolveMoveSpeed()
        {
            float baseValue = HasStatOverrides() ? characterDefinition.moveSpeed : DefaultMoveSpeed;
            return baseValue * ResolveMoveScale();
        }

        private float ResolveMoveScale()
        {
            return characterDefinition != null ? Mathf.Max(0.1f, characterDefinition.runtimeMoveScale) : 1f;
        }

        private float ResolveJumpScale()
        {
            return characterDefinition != null ? Mathf.Max(0.1f, characterDefinition.runtimeJumpScale) : 1f;
        }

        private float ResolveGravityScale()
        {
            return characterDefinition != null ? Mathf.Max(0.1f, characterDefinition.runtimeGravityScale) : 1f;
        }

        private float ResolveDashScale()
        {
            return characterDefinition != null ? Mathf.Max(0.1f, characterDefinition.runtimeDashScale) : 1f;
        }

        private float ResolveUltimateCooldown()
        {
            return Mathf.Max(DefaultUltimateCooldown, ResolveActionDuration("ult", DefaultUltimateDuration) * 2.5f);
        }

        private float ResolveUltimateRadius()
        {
            if (ultimateHitboxAnchor != null && ultimateHitboxAnchor.radius > 0.01f)
            {
                return ultimateHitboxAnchor.radius;
            }

            return characterDefinition != null
                ? Mathf.Max(DefaultUltimateRadius * 0.7f, ResolveColliderSize().x * 1.4f)
                : DefaultUltimateRadius;
        }

        private float ResolveUltimateWindupRatio()
        {
            return characterDefinition != null && characterDefinition.ultimateWindupRatio > 0f
                ? Mathf.Clamp01(characterDefinition.ultimateWindupRatio)
                : DefaultUltimateWindupRatio;
        }

        private float ResolveUltimateDashDistance()
        {
            return characterDefinition != null && characterDefinition.ultimateDashDistance > 0.01f
                ? characterDefinition.ultimateDashDistance * ResolveDashScale()
                : 0f;
        }

        private float ResolveUltimateDashDuration()
        {
            return characterDefinition != null && characterDefinition.ultimateDashDuration > 0.01f
                ? characterDefinition.ultimateDashDuration
                : DefaultUltimateDashDuration;
        }

        private bool ResolveUltimateBlocksProjectiles()
        {
            return characterDefinition != null && characterDefinition.ultimateBlocksProjectiles;
        }

        private float ResolveUltimateProjectileBlockDuration()
        {
            if (!ResolveUltimateBlocksProjectiles())
            {
                return 0f;
            }

            float configured = characterDefinition != null && characterDefinition.ultimateProjectileBlockDuration > 0.01f
                ? characterDefinition.ultimateProjectileBlockDuration
                : DefaultUltimateProjectileBlockDuration;
            return Mathf.Max(configured, ResolveUltimateDashDuration());
        }

        private float ResolveUltimateReplayDelay()
        {
            return characterDefinition != null && characterDefinition.ultimateReplayDelay > 0.01f
                ? characterDefinition.ultimateReplayDelay
                : 0f;
        }

        private float ResolveActionDuration(string actionName, float fallback)
        {
            return characterDefinition != null
                ? characterDefinition.ResolveActionDuration(actionName, fallback)
                : fallback;
        }

        private bool TryResolveAnchorWorldPosition(PlayerCombatAnchor anchor, out Vector2 worldPosition)
        {
            if (anchor != null)
            {
                worldPosition = anchor.ResolveWorldPosition(transform, _facing);
                return true;
            }

            worldPosition = Vector2.zero;
            return false;
        }

        private bool TryResolveProjectileOriginLocalAuthoring(int facingDirection, out Vector3 localPosition)
        {
            if (projectileOrigin != null
                && projectileOrigin.parent == transform
                && ResolveEffectiveProjectileOriginMode() == ProjectileOriginMode.BowNode)
            {
                float horizontalDirection = facingDirection < 0 ? -1f : 1f;
                localPosition = projectileOrigin.localPosition;
                localPosition.x = Mathf.Abs(localPosition.x) * horizontalDirection;
                localPosition.z = 0f;
                return true;
            }

            localPosition = Vector3.zero;
            return false;
        }

        private PlayerCombatAnchor FindChildAnchor(string childName, PlayerCombatAnchorKind expectedKind)
        {
            Transform child = transform.Find(childName);
            if (child == null)
            {
                return null;
            }

            PlayerCombatAnchor anchor = child.GetComponent<PlayerCombatAnchor>();
            if (anchor != null && anchor.anchorKind == expectedKind)
            {
                return anchor;
            }

            return anchor;
        }

        private bool ResolveActionCancelable(string actionName, bool fallback)
        {
            return characterDefinition != null
                ? characterDefinition.ResolveActionCancelable(actionName, fallback)
                : fallback;
        }

        private void ApplyAnchorRuntimePose(PlayerCombatAnchor anchor)
        {
            if (anchor == null)
            {
                return;
            }

            anchor.ApplyRuntimePose(_facing);
        }

        private bool TryOverlapAuthoredHitbox(
            PlayerCombatAnchor anchor,
            ContactFilter2D filter,
            Collider2D[] results,
            out int hitCount)
        {
            hitCount = 0;
            if (anchor == null)
            {
                return false;
            }

            Collider2D authoredCollider = anchor.AttachedCollider;
            if (authoredCollider == null)
            {
                return false;
            }

            hitCount = authoredCollider.Overlap(filter, results);
            return true;
        }

        private void OnDrawGizmosSelected()
        {
            if (!Application.isPlaying || !IsMeleeActive)
            {
                return;
            }

            Gizmos.color = new Color(1f, 0.25f, 0.25f, 0.85f);
            Gizmos.DrawWireCube(GetMeleeHitboxCenter(), GetMeleeHitboxSize());
        }
    }
}
