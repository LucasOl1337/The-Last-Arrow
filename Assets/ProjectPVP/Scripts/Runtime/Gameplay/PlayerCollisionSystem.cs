using UnityEngine;

namespace ProjectPVP.Gameplay
{
    /// <summary>
    /// Handles collision detection and ground/wall checks.
    /// </summary>
    public sealed class PlayerCollisionSystem
    {
        private const float CollisionSkinWidth = 2f;
        private const float RayInsetPadding = 6f;
        private const float WalkableSurfaceNormalY = 0.35f;

        private readonly PlayerContext _context;

        public PlayerCollisionSystem(PlayerContext context)
        {
            _context = context;
        }

        public bool QueryGround(out Vector2 hitNormal)
        {
            hitNormal = Vector2.zero;

            if (_context.bodyCollider == null)
            {
                return false;
            }

            Vector2 position = _context.body != null ? _context.body.position : (Vector2)_context.transform.position;
            GetColliderGeometry(position, out Vector2 center, out Vector2 extents);

            float inset = Mathf.Min(extents.x - CollisionSkinWidth, Mathf.Max(RayInsetPadding, extents.x * 0.6f));
            if (inset < CollisionSkinWidth)
            {
                inset = CollisionSkinWidth;
            }

            float rayDistance = Mathf.Max(CollisionSkinWidth + 1f, _context.groundCheckDistance + CollisionSkinWidth);
            Vector2[] rayOrigins =
            {
                center + new Vector2(0f, -extents.y + CollisionSkinWidth),
                center + new Vector2(-extents.x + inset, -extents.y + CollisionSkinWidth),
                center + new Vector2(extents.x - inset, -extents.y + CollisionSkinWidth),
            };

            float closestDistance = float.MaxValue;
            for (int originIndex = 0; originIndex < rayOrigins.Length; originIndex += 1)
            {
                int hitCount = Physics2D.Raycast(rayOrigins[originIndex], Vector2.down, GetDefaultContactFilter(), _context.castHits, rayDistance);
                for (int hitIndex = 0; hitIndex < hitCount; hitIndex += 1)
                {
                    RaycastHit2D hit = _context.castHits[hitIndex];
                    if (ShouldIgnoreCastHit(hit) || !IsWalkableSurface(hit.normal))
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

        public bool TryGetWallNormal(out Vector2 wallNormal)
        {
            if (_context.bodyCollider == null)
            {
                wallNormal = Vector2.zero;
                return false;
            }

            Vector2 position = _context.body != null ? _context.body.position : (Vector2)_context.transform.position;
            GetColliderGeometry(position, out Vector2 center, out Vector2 extents);

            float inset = Mathf.Min(extents.y - CollisionSkinWidth, Mathf.Max(RayInsetPadding, extents.y * 0.55f));
            if (inset < CollisionSkinWidth)
            {
                inset = CollisionSkinWidth;
            }

            float rayDistance = Mathf.Max(CollisionSkinWidth + 1f, _context.wallCheckDistance + CollisionSkinWidth);
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

        public bool TryRaycastNormals(Vector2[] origins, Vector2 direction, float rayDistance, out Vector2 resolvedNormal)
        {
            resolvedNormal = Vector2.zero;
            float closestDistance = float.MaxValue;

            for (int originIndex = 0; originIndex < origins.Length; originIndex += 1)
            {
                int hitCount = Physics2D.Raycast(origins[originIndex], direction, GetDefaultContactFilter(), _context.castHits, rayDistance);
                for (int hitIndex = 0; hitIndex < hitCount; hitIndex += 1)
                {
                    RaycastHit2D hit = _context.castHits[hitIndex];
                    if (ShouldIgnoreCastHit(hit))
                    {
                        continue;
                    }

                    if (!IsBlockingWallSurface(hit.normal) || hit.distance >= closestDistance)
                    {
                        continue;
                    }

                    closestDistance = hit.distance;
                    resolvedNormal = hit.normal;
                }
            }

            return closestDistance != float.MaxValue;
        }

        public ContactFilter2D GetDefaultContactFilter()
        {
            var filter = new ContactFilter2D
            {
                useTriggers = false,
                useLayerMask = _context.groundMask.value != 0,
                layerMask = _context.groundMask
            };
            return filter;
        }

        public static bool IsWalkableSurface(Vector2 normal)
        {
            return normal.y >= WalkableSurfaceNormalY;
        }

        public static bool IsBlockingWallSurface(Vector2 normal)
        {
            return Mathf.Abs(normal.x) >= 0.35f && !IsWalkableSurface(normal);
        }

        public ContactFilter2D GetMeleeContactFilter()
        {
            return new ContactFilter2D
            {
                useTriggers = false,
                useLayerMask = false
            };
        }

        public ContactFilter2D GetProjectileSeverContactFilter()
        {
            return new ContactFilter2D
            {
                useTriggers = true,
                useLayerMask = false
            };
        }

        public bool ShouldIgnoreCastHit(RaycastHit2D hit)
        {
            if (hit.collider == null || hit.collider == _context.bodyCollider)
            {
                return true;
            }

            return hit.collider.GetComponentInParent<PlayerController>() != null;
        }

        public void RefreshCollisionState()
        {
            bool wasTouchingWall = _context.isTouchingWall;
            _context.isGrounded = QueryGround(out _);
            _context.isTouchingWall = TryGetWallNormal(out _context.wallNormal);

            if (_context.isGrounded)
            {
                _context.wallJumpGraceTimer = 0f;
                _context.wallDetachIgnoreTimer = 0f;
                return;
            }

            if (_context.wallDetachIgnoreTimer > 0f)
            {
                _context.isTouchingWall = false;
                _context.wallNormal = Vector2.zero;
                return;
            }

            if (_context.isTouchingWall && !wasTouchingWall)
            {
                _context.wallJumpGraceTimer = 0.12f;
            }
        }

        public void GetColliderGeometry(Vector2 position, out Vector2 center, out Vector2 extents)
        {
            center = position + _context.bodyCollider.offset;
            extents = _context.bodyCollider.size * 0.5f;
        }

        public Vector2 GetColliderCastSize()
        {
            if (_context.bodyCollider == null)
            {
                return Vector2.zero;
            }

            Vector3 scale = _context.transform.lossyScale;
            return new Vector2(
                Mathf.Abs(_context.bodyCollider.size.x * scale.x),
                Mathf.Abs(_context.bodyCollider.size.y * scale.y));
        }

        public Vector2 ResolveEnvironmentOverlaps(Vector2 position)
        {
            if (_context.bodyCollider == null)
            {
                return position;
            }

            Vector2 resolvedPosition = position;
            for (int iteration = 0; iteration < 4; iteration += 1)
            {
                _context.bodyCollider.transform.position = resolvedPosition;
                int hitCount = _context.bodyCollider.Overlap(GetDefaultContactFilter(), _context.overlapHits);
                bool moved = false;

                for (int hitIndex = 0; hitIndex < hitCount; hitIndex += 1)
                {
                    Collider2D other = _context.overlapHits[hitIndex];
                    if (other == null || other == _context.bodyCollider || other.isTrigger || other.GetComponentInParent<PlayerController>() != null)
                    {
                        continue;
                    }

                    ColliderDistance2D distance = _context.bodyCollider.Distance(other);
                    if (!distance.isOverlapped)
                    {
                        continue;
                    }

                    Vector2 separation = distance.normal * distance.distance;
                    if (separation.sqrMagnitude <= 0.000001f)
                    {
                        continue;
                    }

                    resolvedPosition += separation.normalized * (separation.magnitude + CollisionSkinWidth);
                    moved = true;
                }

                if (!moved)
                {
                    break;
                }
            }

            return resolvedPosition;
        }

        public float ResolveTravelDistance(
            Vector2 center,
            Vector2 colliderSize,
            Vector2[] origins,
            Vector2 direction,
            float distance,
            System.Func<RaycastHit2D, bool> acceptsHit)
        {
            float allowedDistance = distance;
            float castDistance = distance + CollisionSkinWidth;
            Vector2 castSize = new Vector2(
                Mathf.Max(0.01f, colliderSize.x - (CollisionSkinWidth * 2f)),
                Mathf.Max(0.01f, colliderSize.y - (CollisionSkinWidth * 2f)));

            for (int originIndex = 0; originIndex < origins.Length; originIndex += 1)
            {
                int hitCount = Physics2D.Raycast(origins[originIndex], direction, GetDefaultContactFilter(), _context.castHits, castDistance);
                for (int hitIndex = 0; hitIndex < hitCount; hitIndex += 1)
                {
                    RaycastHit2D hit = _context.castHits[hitIndex];
                    if (ShouldIgnoreCastHit(hit) || !acceptsHit(hit))
                    {
                        continue;
                    }

                    allowedDistance = Mathf.Min(allowedDistance, Mathf.Max(0f, hit.distance - CollisionSkinWidth));
                }
            }

            int boxHitCount = Physics2D.BoxCast(center, castSize, 0f, direction, GetDefaultContactFilter(), _context.castHits, castDistance);
            for (int hitIndex = 0; hitIndex < boxHitCount; hitIndex += 1)
            {
                RaycastHit2D hit = _context.castHits[hitIndex];
                if (ShouldIgnoreCastHit(hit) || !acceptsHit(hit))
                {
                    continue;
                }

                allowedDistance = Mathf.Min(allowedDistance, Mathf.Max(0f, hit.distance - CollisionSkinWidth));
            }

            return allowedDistance;
        }

        public static Rect BuildRect(Vector2 position, Vector2 size)
        {
            return new Rect(position, size);
        }

        public static Vector2 ScaleAbsolute(Vector2 value, Vector3 scale)
        {
            return new Vector2(Mathf.Abs(value.x * scale.x), Mathf.Abs(value.y * scale.y));
        }
    }
}
