using UnityEngine;

namespace ProjectPVP.Gameplay
{
    public sealed class ProjectileController : MonoBehaviour
    {
        public float baseSpeed = 1500f;
        public float minSpeed = 720f;
        public float speedDecay = 360f;
        public float gravity = 750f;
        public float gravityDelayRatio = 0f;
        public float gravityRampRatio = 0.6f;
        public float gravityMinScale = 0.45f;
        public float gravityMaxScale = 1.2f;
        public float upwardGravityMultiplier = 3.2f;
        public float upwardSpeedDecayMultiplier = 2.2f;
        public float maxLifetime = 2.5f;
        public float maxRange = 1440f;
        public bool rotateWithVelocity = true;
        public bool collectableWhenStuck = true;
        public Rigidbody2D body;
        public BoxCollider2D hitCollider;
        public SpriteRenderer spriteRenderer;

        private GameObject _sourceObject;
        private Vector2 _velocity = Vector2.right;
        private Vector2 _forwardDirection = Vector2.right;
        private float _forwardSpeed;
        private float _lifetimeLeft;
        private float _distanceTravelled;
        private bool _launched;
        private bool _isStuck;
        private bool _isCollectible;
        private bool _isDisarmed;

        public GameObject SourceObject => _sourceObject;
        public bool IsStuck => _isStuck;
        public bool IsCollectible => _isCollectible;
        public bool IsDisarmed => _isDisarmed;
        public Vector2 CurrentVelocity => _velocity;
        public Vector2 TravelDirection => _velocity.sqrMagnitude > 0.01f
            ? _velocity.normalized
            : (_forwardDirection.sqrMagnitude > 0.01f ? _forwardDirection.normalized : Vector2.right);

        private void Reset()
        {
            body = GetComponent<Rigidbody2D>();
            hitCollider = GetComponent<BoxCollider2D>();
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        }

        private void FixedUpdate()
        {
            if (!_launched || _isStuck)
            {
                return;
            }

            float deltaTime = Time.fixedDeltaTime;
            Vector2 previousPosition = body != null ? body.position : (Vector2)transform.position;

            if (Mathf.Abs(_forwardSpeed) > minSpeed)
            {
                float decay = speedDecay;
                if (_velocity.y > 0f)
                {
                    decay *= upwardSpeedDecayMultiplier;
                }

                _forwardSpeed = Mathf.Sign(_forwardSpeed) * Mathf.Max(Mathf.Abs(_forwardSpeed) - (decay * deltaTime), minSpeed);
            }

            Vector2 forwardComponent = _forwardDirection * _forwardSpeed;
            float currentAlongForward = Vector2.Dot(_velocity, _forwardDirection);
            Vector2 sideComponent = _velocity - (_forwardDirection * currentAlongForward);
            _velocity = forwardComponent + sideComponent;

            if (_distanceTravelled >= maxRange * gravityDelayRatio)
            {
                float progress = maxRange > 0.01f ? Mathf.Clamp01(_distanceTravelled / maxRange) : 1f;
                float ramp = gravityRampRatio > 0f ? Mathf.Clamp01(progress / gravityRampRatio) : 1f;
                float gravityScale = Mathf.Lerp(gravityMinScale, gravityMaxScale, ramp);
                float gravityStrength = gravity * gravityScale;
                if (_velocity.y > 0f)
                {
                    gravityStrength *= upwardGravityMultiplier;
                }

                _velocity += Vector2.down * gravityStrength * deltaTime;
            }

            Vector2 nextPosition = previousPosition + (_velocity * deltaTime);

            if (body != null)
            {
                body.MovePosition(nextPosition);
            }
            else
            {
                transform.position = nextPosition;
            }

            if (rotateWithVelocity && _velocity.sqrMagnitude > 0.01f)
            {
                float angle = Mathf.Atan2(_velocity.y, _velocity.x) * Mathf.Rad2Deg;
                transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);
            }

            _distanceTravelled += Vector2.Distance(previousPosition, nextPosition);
            _lifetimeLeft -= deltaTime;

            if (_distanceTravelled >= maxRange || _lifetimeLeft <= 0f)
            {
                Stick(collectableWhenStuck);
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
                if (player.HandleIncomingProjectile(this))
                {
                    Destroy(gameObject);
                }

                return;
            }

            Stick(collectableWhenStuck);
        }

        public void Launch(GameObject sourceObject, Vector2 origin, Vector2 direction, Vector2 inheritedVelocity, float inheritFactor, Sprite overrideSprite)
        {
            _sourceObject = sourceObject;
            _forwardDirection = direction == Vector2.zero ? Vector2.right : direction.normalized;
            _forwardSpeed = Mathf.Max(minSpeed, baseSpeed + Mathf.Max(0f, Vector2.Dot(inheritedVelocity * inheritFactor, _forwardDirection)));
            _velocity = _forwardDirection * _forwardSpeed;
            _lifetimeLeft = maxLifetime;
            _distanceTravelled = 0f;
            _launched = true;
            _isStuck = false;
            _isCollectible = collectableWhenStuck;
            _isDisarmed = false;

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
                hitCollider.enabled = true;
            }
        }

        public void Stick(bool collectable)
        {
            if (_isStuck)
            {
                return;
            }

            _isStuck = true;
            _isCollectible = collectable;
            _velocity = Vector2.zero;
            _forwardSpeed = 0f;

            if (body != null)
            {
                body.linearVelocity = Vector2.zero;
            }
        }

        private bool ShouldIgnoreCollider(Collider2D other)
        {
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

        private void ResolveProjectileCollision(ProjectileController otherProjectile)
        {
            if (otherProjectile == null
                || otherProjectile == this
                || !_launched
                || _isStuck
                || _isDisarmed
                || !otherProjectile._launched
                || otherProjectile._isStuck
                || otherProjectile._isDisarmed)
            {
                return;
            }

            if (GetInstanceID() > otherProjectile.GetInstanceID())
            {
                return;
            }

            if (!IsOpposingProjectile(otherProjectile))
            {
                return;
            }

            DisarmIntoDrop();
            otherProjectile.DisarmIntoDrop();
        }

        private bool IsOpposingProjectile(ProjectileController otherProjectile)
        {
            float horizontal = ResolveTravelHorizontal();
            float otherHorizontal = otherProjectile.ResolveTravelHorizontal();
            return Mathf.Abs(horizontal) > 0.1f
                && Mathf.Abs(otherHorizontal) > 0.1f
                && Mathf.Sign(horizontal) != Mathf.Sign(otherHorizontal);
        }

        private float ResolveTravelHorizontal()
        {
            if (Mathf.Abs(_velocity.x) > 0.1f)
            {
                return _velocity.x;
            }

            return _forwardDirection.x;
        }

        private void DisarmIntoDrop()
        {
            _isDisarmed = true;
            _isCollectible = true;
            _forwardDirection = Vector2.zero;
            _forwardSpeed = 0f;

            if (_velocity.sqrMagnitude > 0.01f)
            {
                _velocity = new Vector2(_velocity.x * 0.15f, Mathf.Min(_velocity.y, -40f));
            }
            else
            {
                _velocity = new Vector2(0f, -40f);
            }

            if (body != null)
            {
                body.linearVelocity = Vector2.zero;
            }
        }
    }
}
