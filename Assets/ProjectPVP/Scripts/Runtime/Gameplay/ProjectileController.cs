using UnityEngine;
using ProjectPVP.Data;

namespace ProjectPVP.Gameplay
{
    /// <summary>
    /// TowerFall-inspired arrow physics with limited ballistic assist.
    /// The player still chooses one of 8 snapped directions; when a target is
    /// inside that sector, the projectile can bias toward the best trajectory
    /// with a capped turn rate so it remains dodgeable.
    /// </summary>
    public sealed class ProjectileController : MonoBehaviour
    {
        [Header("Physics")]
        public float baseSpeed = 2800f;
        public float gravity = 2200f;
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

        private GameObject _sourceObject;
        private Vector2 _velocity;
        private Vector2 _launchDirection;
        private float _lifetimeLeft;
        private float _distanceTravelled;
        private bool _launched;
        private bool _isStuck;
        private bool _isCollectible;
        private bool _isDisarmed;
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

        // ── Public state ──────────────────────────────────────────────────────────
        public GameObject SourceObject => _sourceObject;
        public bool IsStuck => _isStuck;
        public bool IsCollectible => _isCollectible;
        public bool IsDisarmed => _isDisarmed;
        public Vector2 CurrentVelocity => _velocity;
        public Vector2 TravelDirection => _velocity.sqrMagnitude > 0.01f
            ? _velocity.normalized
            : (_launchDirection.sqrMagnitude > 0.01f ? _launchDirection.normalized : Vector2.right);

        // Legacy assist props — kept so existing code that reads them compiles without changes.
        public bool AssistEnabledRuntime => _assistEnabledRuntime;
        public bool AssistTargetLocked => _assistTargetLocked;
        public float AssistCurrentAngleDeg => _assistCurrentAngleDeg;
        public float AssistAppliedStrength => _assistAppliedStrength;

        // ── Unity lifecycle ───────────────────────────────────────────────────────
        private void Reset()
        {
            body = GetComponent<Rigidbody2D>();
            hitCollider = GetComponent<BoxCollider2D>();
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
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

            ApplyFlightHitbox();
        }

        private void FixedUpdate()
        {
            if (!_launched || _isStuck)
            {
                return;
            }

            float dt = Time.fixedDeltaTime;
            Vector2 prevPos = body != null ? body.position : (Vector2)transform.position;

            // Gravity applied each frame from the moment of launch — no delay, no ramp.
            _velocity.y -= gravity * dt;
            ApplyAssistSteering(prevPos, dt);

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
                    Stick(_isCollectible);
                }

                return;
            }

            if (player != null)
            {
                if (player.HandleIncomingProjectile(this))
                {
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

            // Add player's own velocity component along the shot direction.
            float inheritedBoost = Mathf.Max(0f, Vector2.Dot(inheritedVelocity * inheritFactor, _launchDirection));
            _velocity = _launchDirection * (baseSpeed + inheritedBoost);

            _lifetimeLeft = maxLifetime;
            _distanceTravelled = 0f;
            _launched = true;
            _isStuck = false;
            _isCollectible = collectableWhenStuck;
            _isDisarmed = false;
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
            flightHitboxSize = definition.projectileFlightHitboxSize;
            flightHitboxOffset = definition.projectileFlightHitboxOffset;
            collectibleHitboxSize = definition.projectileCollectibleHitboxSize;
            collectibleHitboxOffset = definition.projectileCollectibleHitboxOffset;
            ApplyFlightHitbox();
        }

        // ── State transitions ─────────────────────────────────────────────────────
        public void Stick(bool collectable)
        {
            if (_isStuck)
            {
                return;
            }

            _isStuck = true;
            _isCollectible = collectable;
            _velocity = Vector2.zero;

            if (body != null)
            {
                body.linearVelocity = Vector2.zero;
            }

            ApplyCollectibleHitbox();
        }

        public void SeverByMelee()
        {
            if (!_launched || _isStuck || _isDisarmed)
            {
                return;
            }

            _isDisarmed = true;
            _isCollectible = false;

            _velocity = _velocity.sqrMagnitude > 0.01f
                ? new Vector2(_velocity.x * 0.2f, Mathf.Min(_velocity.y, -120f))
                : new Vector2(0f, -120f);

            if (body != null)
            {
                body.linearVelocity = Vector2.zero;
            }

            ApplyCollectibleHitbox();
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

        private bool IsOpposingProjectile(ProjectileController other)
        {
            float myH = ResolveTravelHorizontal();
            float theirH = other.ResolveTravelHorizontal();
            return Mathf.Abs(myH) > 0.1f
                && Mathf.Abs(theirH) > 0.1f
                && Mathf.Sign(myH) != Mathf.Sign(theirH);
        }

        private float ResolveTravelHorizontal()
        {
            return Mathf.Abs(_velocity.x) > 0.1f ? _velocity.x : _launchDirection.x;
        }

        private void DisarmIntoDrop()
        {
            _isDisarmed = true;
            _isCollectible = true;

            _velocity = _velocity.sqrMagnitude > 0.01f
                ? new Vector2(_velocity.x * 0.15f, Mathf.Min(_velocity.y, -40f))
                : new Vector2(0f, -40f);

            if (body != null)
            {
                body.linearVelocity = Vector2.zero;
            }

            ApplyCollectibleHitbox();
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
