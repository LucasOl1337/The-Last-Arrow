using System;
using System.Collections.Generic;
using UnityEngine;
using ProjectPVP.Data;
using ProjectPVP.Input;

namespace ProjectPVP.Gameplay
{
    /// <summary>
    /// TowerFall-inspired arrow physics with limited ballistic assist.
    /// The player still chooses one of 8 snapped directions; when a target is
    /// inside that sector, the projectile can bias toward the best trajectory
    /// with a capped turn rate so it remains dodgeable.
    /// </summary>
    public sealed class ProjectileController : MonoBehaviour, IAiArenaProjectileSnapshotSource
    {
        [Header("Physics")]
        public float baseSpeed = 1600f;
        public float gravity = 1500f;
        public float maxLifetime = 2.0f;
        public float maxRange = 1440f;
        public bool rotateWithVelocity = true;
        public bool collectableWhenStuck = true;

        [Header("Hitbox")]
        public Vector2 flightHitboxSize = new Vector2(24f, 10f);
        public Vector2 flightHitboxOffset = new Vector2(32f, 0f);
        public Vector2 collectibleHitboxSize = new Vector2(96f, 24f);
        public Vector2 collectibleHitboxOffset = Vector2.zero;

        [Header("Components")]
        public Rigidbody2D body;
        public BoxCollider2D hitCollider;
        public SpriteRenderer spriteRenderer;
        public TrailRenderer trailRenderer;

        private GameObject _sourceObject;
        private Vector2 _velocity;
        private Vector2 _launchDirection;
        private float _lifetimeLeft;
        private float _distanceTravelled;
        private bool _launched;
        private bool _isStuck;
        private bool _isCollectible;
        private bool _isDisarmed;
        private bool _isParried;
        private Transform _assistTarget;
        private bool _assistEnabledRuntime;
        private bool _assistTargetLocked;
        private float _assistStrengthRuntime;
        private float _assistMaxTurnRateDegRuntime;
        private float _assistAcquireConeDegRuntime;
        private float _assistMaxRangeRuntime;
        private float _assistMinDistanceRuntime;
        private float _assistDropoffStartRatioRuntime;
        private float _assistCurrentAngleDeg;
        private float _assistAppliedStrength;
        private float _gravityDelayRatioRuntime = 0.05f;
        private float _gravityRampRatioRuntime = 0.2f;
        private float _gravityMinScaleRuntime = 0.9f;
        private float _gravityMaxScaleRuntime = 1f;
        private float _projectileUpwardGravityMultiplierRuntime = 1.6f;
        private float _projectileUpwardSpeedDecayMultiplierRuntime = 1.3f;
        private float _projectileMinSpeedRuntime = 720f;
        private float _projectileSpeedDecayRuntime = 360f;
        private static readonly List<ProjectileController> s_activeProjectiles = new();
        private static Material s_flightTrailMaterial;

        // ── Public state ──────────────────────────────────────────────────────────
        public GameObject SourceObject => _sourceObject;
        public bool IsStuck => _isStuck;
        public bool IsCollectible => (_isStuck || _isDisarmed) && _isCollectible;
        public bool IsDisarmed => _isDisarmed;
        public bool IsParried => _isParried;
        public Vector2 CurrentVelocity => _velocity;
        public Vector2 TravelDirection => _velocity.sqrMagnitude > 0.01f
            ? _velocity.normalized
            : (_launchDirection.sqrMagnitude > 0.01f ? _launchDirection.normalized : Vector2.right);

        // Legacy assist props — kept so existing code that reads them compiles without changes.
        public bool AssistEnabledRuntime => _assistEnabledRuntime;
        public bool AssistTargetLocked => _assistTargetLocked;
        public float AssistCurrentAngleDeg => _assistCurrentAngleDeg;
        public float AssistAppliedStrength => _assistAppliedStrength;
        public TrailRenderer TrailRenderer => trailRenderer;

        public static void CopyActiveProjectiles(List<ProjectileController> results)
        {
            if (results == null)
            {
                throw new ArgumentNullException(nameof(results));
            }

            results.Clear();
            PruneDestroyedActiveProjectiles();
            for (int index = 0; index < s_activeProjectiles.Count; index += 1)
            {
                results.Add(s_activeProjectiles[index]);
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ClearActiveProjectilesForRuntimeLoad()
        {
            s_activeProjectiles.Clear();
        }

        private static void RegisterActiveProjectile(ProjectileController projectile)
        {
            if (projectile == null || s_activeProjectiles.Contains(projectile))
            {
                return;
            }

            s_activeProjectiles.Add(projectile);
        }

        private static void UnregisterActiveProjectile(ProjectileController projectile)
        {
            if (projectile == null)
            {
                return;
            }

            s_activeProjectiles.Remove(projectile);
        }

        private static void PruneDestroyedActiveProjectiles()
        {
            for (int index = s_activeProjectiles.Count - 1; index >= 0; index -= 1)
            {
                if (s_activeProjectiles[index] == null)
                {
                    s_activeProjectiles.RemoveAt(index);
                }
            }
        }

        private static void ClearActiveProjectilesForTests()
        {
            s_activeProjectiles.Clear();
        }

        public static int DestroyActiveProjectilesForRoundReset()
        {
            PruneDestroyedActiveProjectiles();
            if (s_activeProjectiles.Count == 0)
            {
                return 0;
            }

            ProjectileController[] activeProjectiles = s_activeProjectiles.ToArray();
            s_activeProjectiles.Clear();

            int destroyedCount = 0;
            for (int index = 0; index < activeProjectiles.Length; index += 1)
            {
                ProjectileController projectile = activeProjectiles[index];
                if (projectile == null || !projectile.HasRoundProjectileState())
                {
                    continue;
                }

                destroyedCount += 1;
                if (Application.isPlaying)
                {
                    Destroy(projectile.gameObject);
                }
                else
                {
                    DestroyImmediate(projectile.gameObject);
                }
            }

            return destroyedCount;
        }

        public static ProjectileController SpawnDroppedArrow(
            ProjectileController projectilePrefab,
            CharacterDefinition definition,
            Vector2 origin)
        {
            ProjectileController projectile = ProjectileLauncher.Spawn(
                projectilePrefab,
                definition,
                null,
                origin,
                Vector2.right,
                null,
                false,
                0f,
                0f,
                0f,
                0f,
                0f,
                0f,
                Vector2.zero,
                0f,
                null,
                1f);
            if (projectile != null)
            {
                projectile.Stick(true);
            }

            return projectile;
        }

        private bool HasRoundProjectileState()
        {
            return _launched || _isStuck || _isDisarmed || _isCollectible;
        }

        public AiArenaProjectileSnapshot BuildAiArenaProjectileSnapshot()
        {
            int sourceSlotId = 0;
            if (_sourceObject != null)
            {
                PlayerController source = _sourceObject.GetComponentInParent<PlayerController>();
                if (source != null)
                {
                    sourceSlotId = source.slotId;
                }
            }

            Vector2 velocity = CurrentVelocity;
            Vector2 travelDirection = TravelDirection;
            return new AiArenaProjectileSnapshot
            {
                isValid = _launched,
                sourceSlotId = sourceSlotId,
                isStuck = _isStuck,
                isDisarmed = _isDisarmed,
                isCollectible = IsCollectible,
                position = transform.position,
                velocity = velocity,
                travelDirection = travelDirection.sqrMagnitude > 0.001f
                    ? travelDirection
                    : (velocity.sqrMagnitude > 0.001f ? velocity.normalized : Vector2.right),
            };
        }

        // ── Unity lifecycle ───────────────────────────────────────────────────────
        private void Reset()
        {
            body = GetComponent<Rigidbody2D>();
            hitCollider = GetComponent<BoxCollider2D>();
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            trailRenderer = GetComponent<TrailRenderer>();
            ApplyFlightHitbox();
        }

        private void OnValidate()
        {
            if (Application.isPlaying)
            {
                return;
            }

            if (hitCollider == null)
            {
                hitCollider = GetComponent<BoxCollider2D>();
            }

            if (trailRenderer == null)
            {
                trailRenderer = GetComponent<TrailRenderer>();
            }

            ApplyFlightHitbox();
        }

        private void OnEnable()
        {
            if (Application.isPlaying)
            {
                RegisterActiveProjectile(this);
                AiArenaSnapshotSourceRegistry.Register(this);
            }
        }

        private void OnDisable()
        {
            ResetRuntimeState();
            UnregisterActiveProjectile(this);
            AiArenaSnapshotSourceRegistry.Unregister(this);
        }

        private void FixedUpdate()
        {
            if (!_launched || _isStuck)
            {
                return;
            }

            float dt = Time.fixedDeltaTime;
            Vector2 prevPos = body != null ? body.position : (Vector2)transform.position;

            // Gravity starts gently, then ramps to the configured arc so arrows leave the bow cleanly.
            _velocity.y -= gravity * ResolveGravityScale() * dt;
            ApplyAssistSteering(prevPos, dt);
            ApplySpeedDecay(dt);

            Vector2 nextPos = prevPos + _velocity * dt;

            if (body != null)
            {
                body.MovePosition(nextPos);
            }
            else
            {
                transform.position = nextPos;
            }

            if (rotateWithVelocity && _velocity.sqrMagnitude > 0.01f)
            {
                float angle = Mathf.Atan2(_velocity.y, _velocity.x) * Mathf.Rad2Deg;
                transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);
            }

            _distanceTravelled += Vector2.Distance(prevPos, nextPos);
            _lifetimeLeft -= dt;

            if (_distanceTravelled >= maxRange || _lifetimeLeft <= 0f)
            {
                Stick(_isCollectible);
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!_launched || other == null)
            {
                return;
            }

            if (ShouldIgnoreCollider(other))
            {
                return;
            }

            ProjectileController otherProjectile = other.GetComponentInParent<ProjectileController>();
            if (otherProjectile != null && otherProjectile != this)
            {
                ResolveProjectileCollision(otherProjectile);
                return;
            }

            PlayerController player = other.GetComponentInParent<PlayerController>();

            if (_isStuck)
            {
                if (_isCollectible && player != null && player.TryCollectProjectile(this))
                {
                    Destroy(gameObject);
                }

                return;
            }

            if (_isDisarmed)
            {
                if (_isCollectible && player != null && player.TryCollectProjectile(this))
                {
                    Destroy(gameObject);
                }
                else if (player == null)
                {
                    Stick(true);
                }

                return;
            }

            if (player != null)
            {
                if (player.HandleIncomingProjectile(this, preserveParryEvent: true))
                {
                    if (ConsumeParryEvent())
                    {
                        return;
                    }

                    if (_isStuck)
                    {
                        return;
                    }

                    if (_isDisarmed)
                    {
                        Stick(_isCollectible);
                    }
                    else
                    {
                        Destroy(gameObject);
                    }
                }

                return;
            }

            Stick(_isCollectible);
        }

        // ── Launch ────────────────────────────────────────────────────────────────
        /// <summary>
        /// Fires the arrow with ballistic physics plus optional limited assist steering.
        /// Assist never teleports or fully homes the shot; it only biases the velocity
        /// toward a reachable arc within the configured turn-rate and range limits.
        /// </summary>
        public void Launch(
            GameObject sourceObject,
            Vector2 origin,
            Vector2 direction,
            Transform assistTarget,
            bool launchAssistEnabled,
            float launchAssistStrength,
            float launchAssistMaxTurnRateDeg,
            float launchAssistAcquireConeDeg,
            float launchAssistMaxRange,
            float launchAssistMinDistance,
            float launchAssistDropoffStartRatio,
            Vector2 inheritedVelocity,
            float inheritFactor,
            Sprite overrideSprite)
        {
            _sourceObject = sourceObject;
            _launchDirection = direction == Vector2.zero ? Vector2.right : direction.normalized;

            _velocity = _launchDirection * baseSpeed + inheritedVelocity * inheritFactor;

            _lifetimeLeft = maxLifetime;
            _distanceTravelled = 0f;
            _launched = true;
            RegisterActiveProjectile(this);
            _isStuck = false;
            _isCollectible = collectableWhenStuck;
            _isDisarmed = false;
            _isParried = false;
            _assistTarget = assistTarget;
            _assistEnabledRuntime = launchAssistEnabled && assistTarget != null;
            _assistTargetLocked = _assistEnabledRuntime;
            _assistStrengthRuntime = Mathf.Clamp01(launchAssistStrength);
            _assistMaxTurnRateDegRuntime = Mathf.Max(0f, launchAssistMaxTurnRateDeg);
            _assistAcquireConeDegRuntime = Mathf.Clamp(launchAssistAcquireConeDeg, 0f, 180f);
            _assistMaxRangeRuntime = Mathf.Max(0f, launchAssistMaxRange);
            _assistMinDistanceRuntime = Mathf.Max(0f, launchAssistMinDistance);
            _assistDropoffStartRatioRuntime = Mathf.Clamp01(launchAssistDropoffStartRatio);
            _assistCurrentAngleDeg = 0f;
            _assistAppliedStrength = 0f;

            if (spriteRenderer != null && overrideSprite != null)
            {
                spriteRenderer.sprite = overrideSprite;
            }

            if (body != null)
            {
                body.position = origin;
                body.linearVelocity = Vector2.zero;
            }
            else
            {
                transform.position = origin;
            }

            if (hitCollider != null)
            {
                ApplyFlightHitbox();
                hitCollider.enabled = true;
            }

            EnableFlightTrail();
        }

        // ── Definition ────────────────────────────────────────────────────────────
        public void ApplyDefinition(CharacterDefinition definition)
        {
            if (definition == null)
            {
                return;
            }

            baseSpeed = definition.projectileBaseSpeed;
            gravity = definition.projectileGravity;
            maxLifetime = definition.projectileMaxLifetime;
            maxRange = definition.projectileMaxRange;
            rotateWithVelocity = definition.projectileRotateWithVelocity;
            collectableWhenStuck = definition.projectileCollectableWhenStuck;
            _gravityDelayRatioRuntime = Mathf.Clamp01(definition.projectileGravityDelayRatio);
            _gravityRampRatioRuntime = Mathf.Clamp01(definition.projectileGravityRampRatio);
            _gravityMinScaleRuntime = Mathf.Max(0f, definition.projectileGravityMinScale);
            _gravityMaxScaleRuntime = Mathf.Max(0f, definition.projectileGravityMaxScale);
            _projectileUpwardGravityMultiplierRuntime = Mathf.Max(0f, definition.projectileUpwardGravityMultiplier);
            _projectileUpwardSpeedDecayMultiplierRuntime = Mathf.Max(0f, definition.projectileUpwardSpeedDecayMultiplier);
            _projectileMinSpeedRuntime = Mathf.Max(0f, definition.projectileMinSpeed);
            _projectileSpeedDecayRuntime = Mathf.Max(0f, definition.projectileSpeedDecay);
            flightHitboxSize = definition.projectileFlightHitboxSize;
            flightHitboxOffset = definition.projectileFlightHitboxOffset;
            collectibleHitboxSize = definition.projectileCollectibleHitboxSize;
            collectibleHitboxOffset = definition.projectileCollectibleHitboxOffset;
            ApplyFlightHitbox();
        }

        private void ResetRuntimeState()
        {
            _sourceObject = null;
            _velocity = Vector2.zero;
            _launchDirection = Vector2.right;
            _lifetimeLeft = 0f;
            _distanceTravelled = 0f;
            _launched = false;
            _isStuck = false;
            _isCollectible = false;
            _isDisarmed = false;
            _isParried = false;
            _assistTarget = null;
            _assistEnabledRuntime = false;
            _assistTargetLocked = false;
            _assistStrengthRuntime = 0f;
            _assistMaxTurnRateDegRuntime = 0f;
            _assistAcquireConeDegRuntime = 0f;
            _assistMaxRangeRuntime = 0f;
            _assistMinDistanceRuntime = 0f;
            _assistDropoffStartRatioRuntime = 0f;
            _assistCurrentAngleDeg = 0f;
            _assistAppliedStrength = 0f;

            if (hitCollider != null)
            {
                hitCollider.enabled = false;
            }

            DisableFlightTrail();
        }

        // ── State transitions ─────────────────────────────────────────────────────
        public void Stick(bool collectable)
        {
            if (_isStuck)
            {
                return;
            }

            if (collectable)
            {
                _sourceObject = null;
            }

            _isStuck = true;
            _isCollectible = collectable;
            _isParried = false;
            _velocity = Vector2.zero;

            if (body != null)
            {
                body.linearVelocity = Vector2.zero;
            }

            DisableFlightTrail();
            ApplyCollectibleHitbox();
        }

        public void SeverByMelee()
        {
            if (!_launched || _isStuck || _isDisarmed)
            {
                return;
            }

            _isDisarmed = true;
            _isCollectible = true;
            _sourceObject = null;
            _isParried = false;

            _velocity = _velocity.sqrMagnitude > 0.01f
                ? new Vector2(_velocity.x * 0.2f, Mathf.Min(_velocity.y, -120f))
                : new Vector2(0f, -120f);

            if (body != null)
            {
                body.linearVelocity = Vector2.zero;
            }

            DisableFlightTrail();
            ApplyCollectibleHitbox();
        }

        internal bool TryConsumeCollectible()
        {
            if (!IsCollectible)
            {
                return false;
            }

            _sourceObject = null;
            _velocity = Vector2.zero;
            _launched = false;
            _isStuck = false;
            _isCollectible = false;
            _isDisarmed = false;
            _isParried = false;
            _assistTarget = null;
            _assistEnabledRuntime = false;
            _assistTargetLocked = false;
            _assistCurrentAngleDeg = 0f;
            _assistAppliedStrength = 0f;

            if (body != null)
            {
                body.linearVelocity = Vector2.zero;
            }

            if (hitCollider != null)
            {
                hitCollider.enabled = false;
            }

            DisableFlightTrail();
            UnregisterActiveProjectile(this);
            return true;
        }

        // ── Collision helpers ─────────────────────────────────────────────────────
        private bool ShouldIgnoreCollider(Collider2D other)
        {
            if (other.GetComponentInParent<PlayerCombatAnchor>() != null)
            {
                return true;
            }

            if (_sourceObject == null)
            {
                return false;
            }

            if (other.gameObject == _sourceObject)
            {
                return true;
            }

            return other.transform.IsChildOf(_sourceObject.transform);
        }

        private void ResolveProjectileCollision(ProjectileController other)
        {
            if (other == null || other == this
                || !_launched || _isStuck || _isDisarmed
                || !other._launched || other._isStuck || other._isDisarmed)
            {
                return;
            }

            // Only the lower-ID instance runs the resolution to avoid double-processing.
            if (GetInstanceID() > other.GetInstanceID())
            {
                return;
            }

            if (!IsOpposingProjectile(other))
            {
                return;
            }

            DisarmIntoDrop();
            other.DisarmIntoDrop();
        }

        public void ReflectFromParry(GameObject newSourceObject)
        {
            if (!_launched || _isStuck || _isDisarmed)
            {
                return;
            }

            _sourceObject = newSourceObject;

            Vector2 reflectedDirection = _velocity.sqrMagnitude > 0.01f
                ? -_velocity.normalized
                : -_launchDirection;
            if (reflectedDirection.sqrMagnitude <= 0.01f)
            {
                reflectedDirection = Vector2.left;
            }

            float reflectedSpeed = Mathf.Max(baseSpeed * 0.95f, _velocity.magnitude);
            _launchDirection = reflectedDirection.normalized;
            _velocity = _launchDirection * reflectedSpeed;
            _lifetimeLeft = maxLifetime;
            _distanceTravelled = 0f;
            _assistTarget = null;
            _assistEnabledRuntime = false;
            _assistTargetLocked = false;
            _assistCurrentAngleDeg = 0f;
            _assistAppliedStrength = 0f;
            _isParried = true;

            if (hitCollider != null)
            {
                ApplyFlightHitbox();
                hitCollider.enabled = true;
            }

            EnableFlightTrail();
        }

        internal bool ConsumeParryEvent()
        {
            if (!_isParried)
            {
                return false;
            }

            _isParried = false;
            return true;
        }

        private bool IsOpposingProjectile(ProjectileController other)
        {
            Vector2 myDirection = ResolveTravelDirection();
            Vector2 theirDirection = other.ResolveTravelDirection();
            if (myDirection.sqrMagnitude <= 0.01f || theirDirection.sqrMagnitude <= 0.01f)
            {
                return false;
            }

            float directionalDot = Vector2.Dot(myDirection.normalized, theirDirection.normalized);
            return directionalDot <= -0.1f;
        }

        private Vector2 ResolveTravelDirection()
        {
            if (_velocity.sqrMagnitude > 0.01f)
            {
                return _velocity.normalized;
            }

            return _launchDirection.sqrMagnitude > 0.01f ? _launchDirection.normalized : Vector2.right;
        }

        private float ResolveGravityScale()
        {
            float lifetime = Mathf.Max(0.0001f, maxLifetime);
            float elapsedRatio = 1f - Mathf.Clamp01(_lifetimeLeft / lifetime);
            float delayRatio = Mathf.Clamp01(_gravityDelayRatioRuntime);
            float rampRatio = Mathf.Max(0.0001f, _gravityRampRatioRuntime);
            float minScale = Mathf.Min(_gravityMinScaleRuntime, _gravityMaxScaleRuntime);
            float maxScale = Mathf.Max(_gravityMinScaleRuntime, _gravityMaxScaleRuntime);

            float gravityScale = minScale;
            if (elapsedRatio > delayRatio)
            {
                float rampProgress = Mathf.Clamp01((elapsedRatio - delayRatio) / rampRatio);
                gravityScale = Mathf.Lerp(minScale, maxScale, rampProgress);
            }

            if (_velocity.y > 0f)
            {
                gravityScale *= _projectileUpwardGravityMultiplierRuntime;
            }

            return Mathf.Max(0f, gravityScale);
        }

        private void ApplySpeedDecay(float deltaTime)
        {
            if (deltaTime <= 0f || _velocity.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            float currentSpeed = _velocity.magnitude;
            float decayRate = Mathf.Max(0f, _projectileSpeedDecayRuntime);
            if (_velocity.y > 0f)
            {
                decayRate *= _projectileUpwardSpeedDecayMultiplierRuntime;
            }

            float nextSpeed = Mathf.Max(_projectileMinSpeedRuntime, currentSpeed - (decayRate * deltaTime));
            if (Mathf.Approximately(nextSpeed, currentSpeed))
            {
                return;
            }

            _velocity = _velocity.normalized * nextSpeed;
        }

        private void DisarmIntoDrop()
        {
            _isDisarmed = true;
            _isCollectible = true;
            _isParried = false;
            _sourceObject = null;

            _velocity = _velocity.sqrMagnitude > 0.01f
                ? new Vector2(_velocity.x * 0.15f, Mathf.Min(_velocity.y, -40f))
                : new Vector2(0f, -40f);

            if (body != null)
            {
                body.linearVelocity = Vector2.zero;
            }

            DisableFlightTrail();
            ApplyCollectibleHitbox();
        }

        private void EnableFlightTrail()
        {
            EnsureFlightTrail();
            if (trailRenderer == null)
            {
                return;
            }

            trailRenderer.enabled = true;
            trailRenderer.emitting = true;
            trailRenderer.Clear();
        }

        private void DisableFlightTrail()
        {
            if (trailRenderer == null)
            {
                return;
            }

            trailRenderer.emitting = false;
            trailRenderer.Clear();
            trailRenderer.enabled = false;
        }

        private void EnsureFlightTrail()
        {
            if (trailRenderer == null)
            {
                trailRenderer = GetComponent<TrailRenderer>();
            }

            if (trailRenderer == null)
            {
                trailRenderer = gameObject.AddComponent<TrailRenderer>();
            }

            trailRenderer.time = 0.1f;
            trailRenderer.minVertexDistance = 4f;
            trailRenderer.widthMultiplier = 8f;
            trailRenderer.widthCurve = new AnimationCurve(
                new Keyframe(0f, 1f),
                new Keyframe(1f, 0f));
            trailRenderer.colorGradient = BuildFlightTrailGradient();
            trailRenderer.alignment = LineAlignment.View;
            trailRenderer.textureMode = LineTextureMode.Stretch;
            trailRenderer.numCapVertices = 2;
            trailRenderer.numCornerVertices = 2;
            trailRenderer.autodestruct = false;
            trailRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            trailRenderer.receiveShadows = false;
            trailRenderer.sortingLayerID = spriteRenderer != null ? spriteRenderer.sortingLayerID : 0;
            trailRenderer.sortingOrder = spriteRenderer != null ? spriteRenderer.sortingOrder - 1 : 9;

            Material trailMaterial = ResolveFlightTrailMaterial();
            if (trailMaterial != null)
            {
                trailRenderer.sharedMaterial = trailMaterial;
            }
        }

        private static Gradient BuildFlightTrailGradient()
        {
            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(1f, 0.94f, 0.42f), 0f),
                    new GradientColorKey(new Color(1f, 0.42f, 0.16f), 1f),
                },
                new[]
                {
                    new GradientAlphaKey(0.62f, 0f),
                    new GradientAlphaKey(0f, 1f),
                });
            return gradient;
        }

        private static Material ResolveFlightTrailMaterial()
        {
            if (s_flightTrailMaterial != null)
            {
                return s_flightTrailMaterial;
            }

            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null)
            {
                shader = Shader.Find("Unlit/Color");
            }

            if (shader == null)
            {
                return null;
            }

            s_flightTrailMaterial = new Material(shader)
            {
                hideFlags = HideFlags.HideAndDontSave,
            };
            return s_flightTrailMaterial;
        }

        // ── Hitbox helpers ────────────────────────────────────────────────────────
        private void ApplyAssistSteering(Vector2 currentPosition, float deltaTime)
        {
            if (!_assistEnabledRuntime || _assistTarget == null || _isDisarmed)
            {
                _assistTargetLocked = false;
                _assistCurrentAngleDeg = 0f;
                _assistAppliedStrength = 0f;
                return;
            }

            Vector2 assistAimPoint = ResolveAssistAimPoint();
            Vector2 toTarget = assistAimPoint - currentPosition;
            float sqrDistance = toTarget.sqrMagnitude;
            if (sqrDistance <= 0.0001f)
            {
                _assistTargetLocked = false;
                _assistCurrentAngleDeg = 0f;
                _assistAppliedStrength = 0f;
                return;
            }

            float distance = Mathf.Sqrt(sqrDistance);
            if (distance < _assistMinDistanceRuntime || distance > _assistMaxRangeRuntime)
            {
                _assistTargetLocked = false;
                _assistCurrentAngleDeg = 0f;
                _assistAppliedStrength = 0f;
                return;
            }

            Vector2 currentDirection = TravelDirection;
            Vector2 desiredDirection = ResolveDesiredAssistDirection(currentPosition, assistAimPoint, currentDirection);
            _assistCurrentAngleDeg = Vector2.Angle(currentDirection, desiredDirection);

            if (!_assistTargetLocked && _assistCurrentAngleDeg > _assistAcquireConeDegRuntime)
            {
                _assistAppliedStrength = 0f;
                return;
            }

            _assistTargetLocked = true;
            float appliedStrength = ComputeAssistAppliedStrength(
                _assistStrengthRuntime,
                distance,
                _assistMaxRangeRuntime,
                _assistDropoffStartRatioRuntime);
            if (appliedStrength <= 0f)
            {
                _assistAppliedStrength = 0f;
                return;
            }

            float maxStepRadians = Mathf.Deg2Rad * _assistMaxTurnRateDegRuntime * appliedStrength * deltaTime;
            Vector2 steeredDirection = RotateDirectionTowardsTarget(currentDirection, desiredDirection, maxStepRadians);
            float speed = Mathf.Max(0.01f, _velocity.magnitude);
            _velocity = steeredDirection * speed;
            _assistAppliedStrength = appliedStrength;
        }

        private Vector2 ResolveAssistAimPoint()
        {
            if (_assistTarget == null)
            {
                return (Vector2)transform.position;
            }

            PlayerController targetPlayer = _assistTarget.GetComponent<PlayerController>();
            if (targetPlayer != null)
            {
                if (targetPlayer.IsDead)
                {
                    _assistEnabledRuntime = false;
                    _assistTargetLocked = false;
                    return (Vector2)_assistTarget.position;
                }

                return PlayerAnchorSystem.ResolveCombatantAimPoint(targetPlayer);
            }

            return (Vector2)_assistTarget.position;
        }

        private Vector2 ResolveDesiredAssistDirection(Vector2 origin, Vector2 target, Vector2 fallbackDirection)
        {
            float currentSpeed = Mathf.Max(0.01f, _velocity.magnitude);
            if (TrySolveBallisticArc(origin, target, currentSpeed, gravity, out Vector2 lowArc, out Vector2 highArc))
            {
                float lowAngle = Vector2.Angle(fallbackDirection, lowArc);
                float highAngle = Vector2.Angle(fallbackDirection, highArc);
                return lowAngle <= highAngle ? lowArc : highArc;
            }

            Vector2 direct = target - origin;
            return direct.sqrMagnitude > 0.0001f ? direct.normalized : fallbackDirection;
        }

        private static float ComputeAssistAppliedStrength(float baseStrength, float distance, float maxRange, float dropoffStartRatio)
        {
            float resolvedBaseStrength = Mathf.Clamp01(baseStrength);
            if (resolvedBaseStrength <= 0f || maxRange <= 0f)
            {
                return 0f;
            }

            float dropoffStartDistance = Mathf.Clamp01(dropoffStartRatio) * maxRange;
            if (distance <= dropoffStartDistance)
            {
                return resolvedBaseStrength;
            }

            if (distance >= maxRange)
            {
                return 0f;
            }

            float remainingRange = Mathf.Max(0.0001f, maxRange - dropoffStartDistance);
            float fade = 1f - ((distance - dropoffStartDistance) / remainingRange);
            return resolvedBaseStrength * Mathf.Clamp01(fade);
        }

        private static Vector2 RotateDirectionTowardsTarget(Vector2 currentDirection, Vector2 desiredDirection, float maxStepRadians)
        {
            Vector2 safeCurrent = currentDirection.sqrMagnitude > 0.0001f ? currentDirection.normalized : Vector2.right;
            Vector2 safeDesired = desiredDirection.sqrMagnitude > 0.0001f ? desiredDirection.normalized : safeCurrent;
            if (maxStepRadians <= 0f)
            {
                return safeCurrent;
            }

            float currentAngleDeg = Mathf.Atan2(safeCurrent.y, safeCurrent.x) * Mathf.Rad2Deg;
            float desiredAngleDeg = Mathf.Atan2(safeDesired.y, safeDesired.x) * Mathf.Rad2Deg;
            float steppedAngleDeg = Mathf.MoveTowardsAngle(currentAngleDeg, desiredAngleDeg, maxStepRadians * Mathf.Rad2Deg);
            float steppedAngleRad = steppedAngleDeg * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(steppedAngleRad), Mathf.Sin(steppedAngleRad)).normalized;
        }

        private static bool TrySolveBallisticArc(
            Vector2 origin,
            Vector2 target,
            float speed,
            float gravity,
            out Vector2 lowArcDir,
            out Vector2 highArcDir)
        {
            lowArcDir = highArcDir = Vector2.zero;

            if (speed < 0.1f)
            {
                Vector2 fallback = (target - origin).normalized;
                lowArcDir = fallback;
                highArcDir = fallback;
                return true;
            }

            float dx = target.x - origin.x;
            float dy = target.y - origin.y;
            if (Mathf.Abs(dx) < 1f)
            {
                Vector2 direct = (target - origin).normalized;
                lowArcDir = direct;
                highArcDir = direct;
                return true;
            }

            float speedSq = speed * speed;
            float a = gravity * dx * dx / (2f * speedSq);
            if (Mathf.Abs(a) < 0.0001f)
            {
                return false;
            }

            float b = -dx;
            float c = dy + a;
            float discriminant = b * b - (4f * a * c);
            if (discriminant < 0f)
            {
                return false;
            }

            float sqrtDiscriminant = Mathf.Sqrt(discriminant);
            float tanA = (-b + sqrtDiscriminant) / (2f * a);
            float tanB = (-b - sqrtDiscriminant) / (2f * a);

            Vector2 TanToDirection(float tanValue)
            {
                float cos = 1f / Mathf.Sqrt(1f + (tanValue * tanValue));
                float sin = tanValue * cos;
                return new Vector2(cos * (dx >= 0f ? 1f : -1f), sin).normalized;
            }

            Vector2 first = TanToDirection(tanA);
            Vector2 second = TanToDirection(tanB);
            if (Mathf.Abs(tanA) <= Mathf.Abs(tanB))
            {
                lowArcDir = first;
                highArcDir = second;
            }
            else
            {
                lowArcDir = second;
                highArcDir = first;
            }

            return true;
        }

        private void ApplyFlightHitbox()
        {
            ApplyHitboxProfile(flightHitboxSize, flightHitboxOffset);
        }

        private void ApplyCollectibleHitbox()
        {
            ApplyHitboxProfile(collectibleHitboxSize, collectibleHitboxOffset);
        }

        private void ApplyHitboxProfile(Vector2 size, Vector2 offset)
        {
            if (hitCollider == null)
            {
                return;
            }

            hitCollider.size = size;
            hitCollider.offset = offset;
        }
    }
}
