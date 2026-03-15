using ProjectPVP.Data;
using ProjectPVP.Gameplay;
using ProjectPVP.Match;
using UnityEngine;

namespace ProjectPVP.Presentation
{
    [ExecuteAlways]
    public sealed class ProjectPvpCombatDebugGizmos : MonoBehaviour
    {
        private const float CollisionSkinWidth = 2f;
        private const float RayInsetPadding = 6f;
        private const float DefaultUltimateRadius = 180f;
        private const float DefaultColliderWidth = 90f;
        private const float DefaultColliderHeight = 210f;
        private const float ProbeMarkerRadius = 6f;
        private const float ProjectileDirectionLength = 44f;

        public MatchController matchController;

        [Header("Visibility")]
        public bool visibleInEditMode = true;
        public bool visibleInPlayMode = true;
        public bool allowToggleInPlayMode = true;
        public KeyCode toggleKey = KeyCode.F3;

        [Header("Overlays")]
        public bool showPlayerBodies = true;
        public bool showProjectileOrigins = true;
        public bool showGroundProbes = true;
        public bool showWallProbes = true;
        public bool showMeleeHitboxes = true;
        public bool showUltimateHitboxes = true;
        public bool showProjectileHitboxes = true;
        public bool showInactiveActionForecasts = true;
        public bool showProjectileDirection = true;

        [Header("Sizes")]
        public float originMarkerRadius = 12f;

        private bool _runtimeVisible = true;

        private void OnEnable()
        {
            _runtimeVisible = visibleInPlayMode;
        }

        private void Update()
        {
            if (!Application.isPlaying || !allowToggleInPlayMode)
            {
                return;
            }

            if (UnityEngine.Input.GetKeyDown(toggleKey))
            {
                _runtimeVisible = !_runtimeVisible;
            }
        }

        private void OnDrawGizmos()
        {
            if (!ShouldDraw())
            {
                return;
            }

            DrawPlayer(matchController != null ? matchController.playerOne : null);
            DrawPlayer(matchController != null ? matchController.playerTwo : null);

            if (showProjectileHitboxes || showProjectileDirection)
            {
                DrawProjectiles();
            }
        }

        private bool ShouldDraw()
        {
            return Application.isPlaying
                ? visibleInPlayMode && _runtimeVisible
                : visibleInEditMode;
        }

        private void DrawPlayer(PlayerController player)
        {
            if (player == null || !player.isActiveAndEnabled)
            {
                return;
            }

            Color tint = ResolvePlayerTint(player.playerId);

            if (showPlayerBodies && TryGetBodyCollider(player, out Vector2 bodyCenter, out Vector2 bodySize))
            {
                Gizmos.color = WithAlpha(tint, 0.95f);
                Gizmos.DrawWireCube(bodyCenter, bodySize);
            }

            if (showProjectileOrigins && TryGetProjectileOrigin(player, out Vector2 projectileOrigin))
            {
                Gizmos.color = WithAlpha(tint, 0.85f);
                Gizmos.DrawWireSphere(projectileOrigin, originMarkerRadius);
            }

            if (showGroundProbes)
            {
                DrawGroundProbes(player, tint);
            }

            if (showWallProbes)
            {
                DrawWallProbes(player, tint);
            }

            if (showMeleeHitboxes)
            {
                DrawMeleeHitbox(player, tint);
            }

            if (showUltimateHitboxes)
            {
                DrawUltimateHitbox(player, tint);
            }
        }

        private void DrawGroundProbes(PlayerController player, Color tint)
        {
            if (!TryGetColliderGeometry(player, out Vector2 center, out Vector2 extents))
            {
                return;
            }

            float inset = Mathf.Min(extents.x - CollisionSkinWidth, Mathf.Max(RayInsetPadding, extents.x * 0.6f));
            if (inset < CollisionSkinWidth)
            {
                inset = CollisionSkinWidth;
            }

            float rayDistance = Mathf.Max(CollisionSkinWidth + 1f, player.groundCheckDistance + CollisionSkinWidth);
            Vector2[] rayOrigins =
            {
                center + new Vector2(0f, -extents.y + CollisionSkinWidth),
                center + new Vector2(-extents.x + inset, -extents.y + CollisionSkinWidth),
                center + new Vector2(extents.x - inset, -extents.y + CollisionSkinWidth),
            };

            Gizmos.color = WithAlpha(tint, 0.72f);
            for (int index = 0; index < rayOrigins.Length; index += 1)
            {
                DrawProbe(rayOrigins[index], Vector2.down, rayDistance);
            }
        }

        private void DrawWallProbes(PlayerController player, Color tint)
        {
            if (!TryGetColliderGeometry(player, out Vector2 center, out Vector2 extents))
            {
                return;
            }

            float inset = Mathf.Min(extents.y - CollisionSkinWidth, Mathf.Max(RayInsetPadding, extents.y * 0.55f));
            if (inset < CollisionSkinWidth)
            {
                inset = CollisionSkinWidth;
            }

            float rayDistance = Mathf.Max(CollisionSkinWidth + 1f, player.wallCheckDistance + CollisionSkinWidth);
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

            Gizmos.color = WithAlpha(tint, 0.58f);
            for (int index = 0; index < leftOrigins.Length; index += 1)
            {
                DrawProbe(leftOrigins[index], Vector2.left, rayDistance);
                DrawProbe(rightOrigins[index], Vector2.right, rayDistance);
            }
        }

        private void DrawMeleeHitbox(PlayerController player, Color tint)
        {
            if (!showInactiveActionForecasts && !player.IsMeleeActive)
            {
                return;
            }

            Gizmos.color = player.IsMeleeActive
                ? new Color(1f, 0.28f, 0.28f, 0.92f)
                : WithAlpha(tint, 0.28f);
            if (!TryDrawAnchorCollider(player.meleeHitboxAnchor))
            {
                Gizmos.DrawWireCube(player.MeleeHitboxCenter, player.MeleeHitboxSize);
            }
        }

        private void DrawUltimateHitbox(PlayerController player, Color tint)
        {
            if (!showInactiveActionForecasts && !player.IsUltimateActive)
            {
                return;
            }

            Gizmos.color = player.IsUltimateActive
                ? new Color(1f, 0.2f, 0.9f, 0.92f)
                : WithAlpha(tint, 0.22f);
            if (!TryDrawAnchorCollider(player.ultimateHitboxAnchor))
            {
                Gizmos.DrawWireSphere(player.UltimateHitboxCenter, player.UltimateHitboxRadius);
            }
        }

        private void DrawProjectiles()
        {
            ProjectileController[] projectiles = FindObjectsByType<ProjectileController>(FindObjectsSortMode.None);
            for (int index = 0; index < projectiles.Length; index += 1)
            {
                ProjectileController projectile = projectiles[index];
                if (projectile == null || !projectile.isActiveAndEnabled)
                {
                    continue;
                }

                PlayerController owner = projectile.SourceObject != null
                    ? projectile.SourceObject.GetComponentInParent<PlayerController>()
                    : null;
                Color tint = owner != null ? ResolvePlayerTint(owner.playerId) : new Color(1f, 0.92f, 0.35f, 1f);
                if (projectile.IsCollectible)
                {
                    tint = Color.Lerp(tint, new Color(1f, 0.85f, 0.2f, 1f), 0.45f);
                }
                else if (projectile.IsStuck)
                {
                    tint = Color.Lerp(tint, new Color(1f, 0.32f, 0.22f, 1f), 0.45f);
                }

                if (showProjectileHitboxes)
                {
                    if (projectile.hitCollider != null)
                    {
                        Gizmos.color = WithAlpha(tint, 0.95f);
                        Matrix4x4 previousMatrix = Gizmos.matrix;
                        Gizmos.matrix = projectile.hitCollider.transform.localToWorldMatrix;
                        Gizmos.DrawWireCube(projectile.hitCollider.offset, projectile.hitCollider.size);
                        Gizmos.matrix = previousMatrix;
                    }
                    else
                    {
                        Gizmos.color = WithAlpha(tint, 0.85f);
                        Gizmos.DrawWireSphere(projectile.transform.position, 10f);
                    }
                }

                if (showProjectileDirection)
                {
                    Gizmos.color = WithAlpha(tint, 0.72f);
                    Vector3 origin = projectile.transform.position;
                    Vector3 direction = projectile.transform.right * ProjectileDirectionLength;
                    Gizmos.DrawLine(origin, origin + direction);
                }
            }
        }

        private static void DrawProbe(Vector2 origin, Vector2 direction, float distance)
        {
            Gizmos.DrawWireSphere(origin, ProbeMarkerRadius);
            Gizmos.DrawLine(origin, origin + (direction.normalized * distance));
        }

        private static bool TryGetBodyCollider(PlayerController player, out Vector2 center, out Vector2 size)
        {
            center = Vector2.zero;
            size = Vector2.zero;

            if (player.bodyCollider == null)
            {
                return false;
            }

            Bounds bounds = player.bodyCollider.bounds;
            center = bounds.center;
            size = bounds.size;
            return true;
        }

        private static bool TryGetProjectileOrigin(PlayerController player, out Vector2 origin)
        {
            if (player == null)
            {
                origin = Vector2.zero;
                return false;
            }

            origin = player.ProjectileOriginWorldPosition;
            return true;
        }

        private static bool TryDrawAnchorCollider(PlayerCombatAnchor anchor)
        {
            if (anchor == null || anchor.AttachedCollider == null)
            {
                return false;
            }

            Collider2D collider = anchor.AttachedCollider;
            Matrix4x4 previousMatrix = Gizmos.matrix;
            Gizmos.matrix = collider.transform.localToWorldMatrix;

            switch (collider)
            {
                case BoxCollider2D box:
                    Gizmos.DrawWireCube(box.offset, box.size);
                    break;
                case CircleCollider2D circle:
                    Gizmos.DrawWireSphere(circle.offset, circle.radius);
                    break;
                case CapsuleCollider2D capsule:
                    Gizmos.DrawWireCube(capsule.offset, capsule.size);
                    break;
                default:
                    Gizmos.matrix = previousMatrix;
                    return false;
            }

            Gizmos.matrix = previousMatrix;
            return true;
        }

        private static bool TryGetColliderGeometry(PlayerController player, out Vector2 center, out Vector2 extents)
        {
            center = Vector2.zero;
            extents = Vector2.zero;

            if (player == null || player.bodyCollider == null)
            {
                return false;
            }

            Vector2 colliderSize = ResolveColliderSize(player);
            Vector2 colliderOffset = ResolveColliderOffset(player);
            center = ResolvePlayerWorldPosition(player) + colliderOffset;
            extents = colliderSize * 0.5f;
            return true;
        }

        private static Vector2 ResolvePlayerWorldPosition(PlayerController player)
        {
            if (player == null)
            {
                return Vector2.zero;
            }

            return Application.isPlaying && player.body != null
                ? player.body.position
                : (Vector2)player.transform.position;
        }

        private static Vector2 ResolveColliderSize(PlayerController player)
        {
            if (player != null && player.characterDefinition != null)
            {
                return player.characterDefinition.colliderSize;
            }

            return new Vector2(DefaultColliderWidth, DefaultColliderHeight);
        }

        private static Vector2 ResolveColliderOffset(PlayerController player)
        {
            if (player != null && player.characterDefinition != null)
            {
                return player.characterDefinition.colliderOffset;
            }

            return Vector2.zero;
        }

        private static Color ResolvePlayerTint(int playerId)
        {
            return playerId == 2
                ? new Color(1f, 0.62f, 0.36f, 1f)
                : new Color(0.34f, 0.86f, 1f, 1f);
        }

        private static Color WithAlpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }
    }
}
