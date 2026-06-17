using ProjectPVP.Data;
using ProjectPVP.Input;
using UnityEngine;

namespace ProjectPVP.Gameplay
{
    /// <summary>
    /// Handles movement, gravity, and character positioning.
    /// </summary>
    public sealed class PlayerMovementSystem
    {
        private const float CollisionSkinWidth = 2f;
        private const float GroundSnapDistance = 240f;
        private const float GroundFollowExtraSnapDistance = 18f;
        private const float SpawnGroundPadding = 2f;
        private const float AirAccelerationMultiplier = 0.9f;
        private const float AirFrictionMultiplier = 0.30f;
        private const float TurnAccelerationMultiplier = 1.15f;
        // AimHoldSmoothing removed — aim direction is now applied instantly (TowerFall style).

        private readonly PlayerContext _context;
        private readonly PlayerStatResolver _statResolver;
        private readonly PlayerCollisionSystem _collisionSystem;

        public PlayerMovementSystem(PlayerContext context, PlayerStatResolver statResolver, PlayerCollisionSystem collisionSystem)
        {
            _context = context;
            _statResolver = statResolver;
            _collisionSystem = collisionSystem;
        }

        public void HandleMovement(PlayerInputFrame frame, float deltaTime, ref Vector2 velocity)
        {
            if (_context.isDead)
            {
                return;
            }

            float targetSpeed = frame.axis * _statResolver.ResolveMoveSpeed();
            bool hasDirectionalInput = Mathf.Abs(frame.axis) > 0.01f;
            float acceleration = _statResolver.ResolveAcceleration();
            float friction = _statResolver.ResolveFriction();
            bool effectivelyGrounded = IsEffectivelyGrounded();

            if (!effectivelyGrounded)
            {
                acceleration *= AirAccelerationMultiplier;
                friction *= AirFrictionMultiplier;
            }

            if (!effectivelyGrounded && _context.wallDetachIgnoreTimer > 0f)
            {
                velocity.x = Mathf.MoveTowards(velocity.x, 0f, friction * 1.5f * deltaTime);
                return;
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

        public void MoveCharacter(ref Vector2 velocity, float deltaTime)
        {
            if (_context.isDead)
            {
                velocity = Vector2.zero;
                return;
            }

            if (_context.body == null || _context.bodyCollider == null)
            {
                _context.transform.position += (Vector3)(velocity * deltaTime);
                return;
            }

            Vector2 position = _context.body.position;
            position = _collisionSystem.ResolveEnvironmentOverlaps(position);
            bool wasGrounded = IsEffectivelyGrounded();
            float horizontalTravelDistance = Mathf.Abs(velocity.x * deltaTime);
            position = MoveHorizontally(position, ref velocity.x, deltaTime);
            position = SnapToGroundWhileFollowingSlope(position, horizontalTravelDistance, wasGrounded, ref velocity.y);
            position = MoveVertically(position, ref velocity.y, deltaTime);
            position = _collisionSystem.ResolveEnvironmentOverlaps(position);
            _context.body.position = position;
            _context.transform.position = position;
        }

        public Vector2 MoveHorizontally(Vector2 position, ref float velocityX, float deltaTime)
        {
            if (Mathf.Abs(velocityX) <= 0.0001f || _context.bodyCollider == null)
            {
                return position;
            }

            float signedDistance = velocityX * deltaTime;
            float direction = Mathf.Sign(signedDistance);
            float distance = Mathf.Abs(signedDistance);
            _collisionSystem.GetColliderGeometry(position, out Vector2 center, out Vector2 extents);

            float verticalInset = Mathf.Min(extents.y - CollisionSkinWidth, Mathf.Max(6f, extents.y * 0.55f));
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

            float allowedDistance = _collisionSystem.ResolveTravelDistance(
                center,
                _collisionSystem.GetColliderCastSize(),
                origins,
                new Vector2(direction, 0f),
                distance,
                hit => PlayerCollisionSystem.IsBlockingWallSurface(hit.normal));

            if (allowedDistance + 0.001f < distance)
            {
                velocityX = 0f;
            }

            position.x += direction * allowedDistance;
            return position;
        }

        public Vector2 MoveVertically(Vector2 position, ref float velocityY, float deltaTime)
        {
            if (Mathf.Abs(velocityY) <= 0.0001f || _context.bodyCollider == null)
            {
                return position;
            }

            float signedDistance = velocityY * deltaTime;
            float direction = Mathf.Sign(signedDistance);
            float distance = Mathf.Abs(signedDistance);
            _collisionSystem.GetColliderGeometry(position, out Vector2 center, out Vector2 extents);

            float horizontalInset = Mathf.Min(extents.x - CollisionSkinWidth, Mathf.Max(6f, extents.x * 0.6f));
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

            float allowedDistance = _collisionSystem.ResolveTravelDistance(
                center,
                _collisionSystem.GetColliderCastSize(),
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

        public Vector2 SnapToGroundWhileFollowingSlope(Vector2 position, float horizontalTravelDistance, bool wasGrounded, ref float velocityY)
        {
            if (!wasGrounded || velocityY > 0.01f || _context.body == null || _context.bodyCollider == null)
            {
                return position;
            }

            Vector2 footWorldPosition = ResolveFootWorldPosition(position);
            float horizontalInset = Mathf.Max(6f, _statResolver.ResolveColliderSize().x * 0.25f);
            float maxSnapDistance = Mathf.Max(
                _context.groundCheckDistance + GroundFollowExtraSnapDistance,
                horizontalTravelDistance + GroundFollowExtraSnapDistance);
            float castHeight = maxSnapDistance;
            float castDistance = maxSnapDistance * 2f;
            Vector2[] origins =
            {
                footWorldPosition + new Vector2(0f, castHeight),
                footWorldPosition + new Vector2(-horizontalInset, castHeight),
                footWorldPosition + new Vector2(horizontalInset, castHeight),
            };

            bool foundGround = false;
            float closestGroundDistance = float.MaxValue;
            float resolvedGroundY = float.MinValue;
            Vector2 resolvedGroundNormal = Vector2.up;
            for (int originIndex = 0; originIndex < origins.Length; originIndex += 1)
            {
                int hitCount = Physics2D.Raycast(origins[originIndex], Vector2.down, _collisionSystem.GetDefaultContactFilter(), _context.castHits, castDistance);
                for (int hitIndex = 0; hitIndex < hitCount; hitIndex += 1)
                {
                    RaycastHit2D hit = _context.castHits[hitIndex];
                    if (_collisionSystem.ShouldIgnoreCastHit(hit) || !PlayerCollisionSystem.IsWalkableSurface(hit.normal))
                    {
                        continue;
                    }

                    if (hit.distance < closestGroundDistance)
                    {
                        closestGroundDistance = hit.distance;
                        resolvedGroundY = hit.point.y;
                        resolvedGroundNormal = hit.normal;
                        foundGround = true;
                    }
                }
            }

            if (!foundGround)
            {
                return position;
            }

            Vector2 snappedPosition = ResolveSpawnBodyPosition(new Vector2(footWorldPosition.x, resolvedGroundY));
            float snapDelta = snappedPosition.y - position.y;
            if (Mathf.Abs(snapDelta) > maxSnapDistance)
            {
                return position;
            }

            if (snapDelta > 0f || velocityY < 0f)
            {
                velocityY = 0f;
            }

            if (snapDelta > 0f && horizontalTravelDistance > 0.01f)
            {
                float uphillSnapBlend = Mathf.Lerp(0.6f, 1f, Mathf.Clamp01(resolvedGroundNormal.y));
                position.y += snapDelta * uphillSnapBlend;
            }
            else
            {
                position.y = snappedPosition.y;
            }

            return position;
        }

        public void UpdateFacing(PlayerInputFrame frame)
        {
            if (_context.isDead)
            {
                return;
            }

            if (Mathf.Abs(frame.axis) > 0.01f)
            {
                _context.facing = frame.axis > 0f ? 1 : -1;
            }

            if (_context.aimHoldActive && Mathf.Abs(_context.aimHoldDirection.x) > 0.01f)
            {
                _context.facing = _context.aimHoldDirection.x > 0f ? 1 : -1;
            }

            UpdateVisualFacing();
        }

        public void UpdateVisualFacing()
        {
            if (_context.spriteRenderer != null)
            {
                _context.spriteRenderer.flipX = _context.facing < 0;
            }

            UpdateProjectileOriginSocket();
        }

        /// <summary>
        /// TowerFall-style hold-to-aim / release-to-fire.
        /// Aim is always snapped to the nearest of 8 compass directions.
        /// Returns true on the frame the arrow should be fired.
        /// </summary>
        public bool UpdateAimHoldState(PlayerInputFrame frame)
        {
            if (_context.isDead)
            {
                _context.aimHoldActive = false;
                _context.shootHeldLastFrame = false;
                return false;
            }

            bool shootInputActive = frame.shootHeld || frame.shootPressed;
            bool shootJustPressed  = shootInputActive && !_context.shootHeldLastFrame;
            bool shootJustReleased = !shootInputActive && _context.shootHeldLastFrame;
            _context.shootHeldLastFrame = shootInputActive;

            // Cannot aim without arrows.
            if (_context.arrows <= 0)
            {
                _context.aimHoldActive = false;
                _context.shootHeldLastFrame = false;
                return false;
            }

            // Begin aiming on button press — default to horizontal facing direction.
            if (shootJustPressed)
            {
                _context.aimHoldActive    = true;
                _context.aimHoldDirection = ResolveAimDirection(frame);
            }

            // Keep the snapped aim direction fresh from any explicit aim input.
            // This also runs on the release frame so a last-moment diagonal still
            // updates before the arrow is fired.
            if (_context.aimHoldActive && frame.aim.sqrMagnitude > 0.01f)
            {
                _context.aimHoldDirection = Snap8Dir(frame.aim);
            }

            // Safety: aimHoldDirection must never be zero.
            if (_context.aimHoldActive && _context.aimHoldDirection.sqrMagnitude < 0.01f)
            {
                _context.aimHoldDirection = new Vector2(_context.facing >= 0 ? 1f : -1f, 0f);
            }

            // Fire on release.
            if (_context.aimHoldActive && shootJustReleased)
            {
                _context.aimHoldActive = false;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Resolves the initial aim direction when shoot is first pressed.
        /// Snaps to the nearest 8-direction; defaults to horizontal if no input.
        /// </summary>
        public Vector2 ResolveAimDirection(PlayerInputFrame frame)
        {
            if (frame.aim.sqrMagnitude > 0.01f)
                return Snap8Dir(frame.aim);

            // Default: horizontal in facing direction (the TowerFall "ready" pose).
            return new Vector2(_context.facing >= 0 ? 1f : -1f, 0f);
        }

        /// <summary>
        /// Snaps an arbitrary direction to the nearest of the 8 compass directions
        /// (N, NE, E, SE, S, SW, W, NW). Returns zero if input is nearly zero.
        /// </summary>
        public static Vector2 Snap8Dir(Vector2 raw)
        {
            if (raw.sqrMagnitude < 0.01f) return Vector2.zero;
            float angleDeg = Mathf.Atan2(raw.y, raw.x) * Mathf.Rad2Deg;
            float snapped  = Mathf.Round(angleDeg / 45f) * 45f * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(snapped), Mathf.Sin(snapped));
        }

        public Vector2 ResolveConfiguredSpawnWorldPosition()
        {
            _context.anchorRig ??= new CombatantAnchorRig();
            if (_context.anchorRig.spawnAnchor != null)
            {
                return _context.anchorRig.spawnAnchor.ResolveWorldPosition(_context.transform, 1);
            }

            if (_context.spawnAnchor != null)
            {
                return _context.spawnAnchor.ResolveWorldPosition(_context.transform, 1);
            }

            return _context.body != null ? _context.body.position : (Vector2)_context.transform.position;
        }

        public Vector2 ResolveSpawnBodyPosition(Vector2 footWorldPosition)
        {
            Vector2 colliderSize = _statResolver.ResolveColliderSize();
            Vector2 colliderOffset = _statResolver.ResolveColliderOffset();
            return footWorldPosition - colliderOffset + new Vector2(0f, (colliderSize.y * 0.5f) + SpawnGroundPadding);
        }

        public Vector2 ResolveFootWorldPosition(Vector2 bodyPosition)
        {
            Vector2 colliderSize = _statResolver.ResolveColliderSize();
            Vector2 colliderOffset = _statResolver.ResolveColliderOffset();
            return bodyPosition + colliderOffset - new Vector2(0f, colliderSize.y * 0.5f);
        }

        public void SnapToGroundAtSpawn(Vector2 footWorldPosition)
        {
            if (_context.body == null || _context.bodyCollider == null)
            {
                return;
            }

            Vector2 colliderSize = _statResolver.ResolveColliderSize();
            float horizontalInset = Mathf.Max(6f, colliderSize.x * 0.25f);
            float castHeight = Mathf.Max(_context.groundCheckDistance + 24f, GroundSnapDistance * 0.5f);
            float castDistance = Mathf.Max(_context.groundCheckDistance + 24f, GroundSnapDistance);
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
                int hitCount = Physics2D.Raycast(origins[originIndex], Vector2.down, _collisionSystem.GetDefaultContactFilter(), _context.castHits, castDistance);
                for (int hitIndex = 0; hitIndex < hitCount; hitIndex += 1)
                {
                    RaycastHit2D hit = _context.castHits[hitIndex];
                    if (_collisionSystem.ShouldIgnoreCastHit(hit) || hit.normal.y < 0.35f)
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
            _context.body.position = snappedPosition;
            _context.transform.position = snappedPosition;
        }

        public void SetSpawnPosition(Vector2 worldPosition)
        {
            Vector2 bodySpawnPosition = ResolveSpawnBodyPosition(worldPosition);
            if (_context.body != null)
            {
                _context.body.position = bodySpawnPosition;
                _context.body.linearVelocity = Vector2.zero;
            }
            else
            {
                _context.transform.position = bodySpawnPosition;
            }
        }

        public bool IsEffectivelyGrounded()
        {
            if (_context.isGrounded)
            {
                return true;
            }

            if (_context.coyoteTimeLeft <= 0f)
            {
                return false;
            }

            return _context.body == null || _context.body.linearVelocity.y <= 20f;
        }

        private void UpdateProjectileOriginSocket()
        {
            _context.anchorRig ??= new CombatantAnchorRig();
            Transform activeProjectileOrigin = _context.anchorRig.projectileOrigin != null ? _context.anchorRig.projectileOrigin : _context.projectileOrigin;
            if (_context.anchorRig.projectileOrigin == null && _context.projectileOrigin != null)
            {
                _context.anchorRig.projectileOrigin = _context.projectileOrigin;
            }

            if (activeProjectileOrigin == null)
            {
                return;
            }

            activeProjectileOrigin.localPosition = ResolveProjectileOriginLocalPosition(_context.facing);
            activeProjectileOrigin.localRotation = Quaternion.identity;
            activeProjectileOrigin.localScale = Vector3.one;
        }

        private Vector3 ResolveProjectileOriginLocalPosition(int facingDirection)
        {
            if (TryResolveProjectileOriginLocalAuthoring(facingDirection, out Vector3 authoredLocalPosition))
            {
                return authoredLocalPosition;
            }

            Vector2 localPosition = ResolveProjectileOriginBaseLocalPosition();
            Vector2 originOffset = _statResolver.ResolveProjectileOriginOffset();
            float horizontalDirection = facingDirection < 0 ? -1f : 1f;
            localPosition += new Vector2(Mathf.Abs(originOffset.x) * horizontalDirection, originOffset.y);
            return new Vector3(localPosition.x, localPosition.y, 0f);
        }

        private Vector2 ResolveProjectileOriginBaseLocalPosition()
        {
            Vector2 colliderSize = _statResolver.ResolveColliderSize();
            Vector2 colliderOffset = _statResolver.ResolveColliderOffset();

            switch (_statResolver.ResolveProjectileOriginMode())
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

        private bool TryResolveProjectileOriginLocalAuthoring(int facingDirection, out Vector3 localPosition)
        {
            Transform authoredProjectileOrigin = _context.anchorRig != null && _context.anchorRig.projectileOrigin != null
                ? _context.anchorRig.projectileOrigin
                : _context.projectileOrigin;

            if (authoredProjectileOrigin != null
                && authoredProjectileOrigin.parent == _context.transform
                && _statResolver.ResolveProjectileOriginMode() == ProjectileOriginMode.BowNode)
            {
                float horizontalDirection = facingDirection < 0 ? -1f : 1f;
                localPosition = authoredProjectileOrigin.localPosition;
                localPosition.x = Mathf.Abs(localPosition.x) * horizontalDirection;
                localPosition.z = 0f;
                return true;
            }

            localPosition = Vector3.zero;
            return false;
        }
    }
}
