using System.Collections.Generic;
using ProjectPVP.Data;
using UnityEngine;

namespace ProjectPVP.Gameplay
{
    /// <summary>
    /// Handles combat anchor synchronization and positioning.
    /// </summary>
    public sealed class PlayerAnchorSystem
    {
        private readonly PlayerContext _context;
        private readonly PlayerStatResolver _statResolver;

        public PlayerAnchorSystem(PlayerContext context, PlayerStatResolver statResolver)
        {
            _context = context;
            _statResolver = statResolver;
        }

        public void SyncCombatAnchors()
        {
            _context.anchorRig ??= new CombatantAnchorRig();
            _context.anchorRig.SyncLegacy(ref _context.spawnAnchor, ref _context.projectileOrigin, ref _context.meleeHitboxAnchor, ref _context.ultimateHitboxAnchor);

            HashSet<PlayerCombatAnchor> visitedAnchors = null;
            if (_context.anchorRig.spawnAnchor != null || _context.anchorRig.meleeHitboxAnchor != null || _context.anchorRig.ultimateHitboxAnchor != null)
            {
                visitedAnchors = new HashSet<PlayerCombatAnchor>();
                ApplyRegisteredAnchor(_context.anchorRig.spawnAnchor, visitedAnchors);
                ApplyRegisteredAnchor(_context.anchorRig.meleeHitboxAnchor, visitedAnchors);
                ApplyRegisteredAnchor(_context.anchorRig.ultimateHitboxAnchor, visitedAnchors);
            }

            for (int childIndex = 0; childIndex < _context.transform.childCount; childIndex += 1)
            {
                Transform child = _context.transform.GetChild(childIndex);
                if (child == null)
                {
                    continue;
                }

                PlayerCombatAnchor anchor = child.GetComponent<PlayerCombatAnchor>();
                if (anchor != null && (visitedAnchors == null || !visitedAnchors.Contains(anchor)))
                {
                    ApplyAnchorRuntimePose(anchor);
                }
            }
        }

        public void ApplyRegisteredAnchor(PlayerCombatAnchor anchor, HashSet<PlayerCombatAnchor> visitedAnchors)
        {
            if (anchor == null)
            {
                return;
            }

            ApplyAnchorRuntimePose(anchor);
            visitedAnchors?.Add(anchor);
        }

        public Vector2 ResolveProjectileOriginWorldPosition(int facingDirection)
        {
            return _context.transform.TransformPoint(ResolveProjectileOriginLocalPosition(facingDirection));
        }

        public Vector3 ResolveProjectileOriginLocalPosition(int facingDirection)
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

        public Vector2 ResolveProjectileOriginBaseLocalPosition()
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

        public ProjectileOriginMode ResolveEffectiveProjectileOriginMode()
        {
            if (_context.characterDefinition != null && _context.characterDefinition.projectileUseBowNode)
            {
                return ProjectileOriginMode.BowNode;
            }

            return _statResolver.ResolveProjectileOriginMode();
        }

        public void ApplyAnchorRuntimePose(PlayerCombatAnchor anchor)
        {
            if (anchor == null)
            {
                return;
            }

            anchor.ApplyRuntimePose(_context.facing);
        }

        public bool TryOverlapAuthoredHitbox(
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

        public bool TryResolveAnchorWorldPosition(PlayerCombatAnchor anchor, out Vector2 worldPosition)
        {
            if (anchor != null)
            {
                worldPosition = anchor.ResolveWorldPosition(_context.transform, _context.facing);
                return true;
            }

            worldPosition = Vector2.zero;
            return false;
        }

        public bool TryResolveProjectileOriginLocalAuthoring(int facingDirection, out Vector3 localPosition)
        {
            Transform authoredProjectileOrigin = _context.anchorRig != null && _context.anchorRig.projectileOrigin != null
                ? _context.anchorRig.projectileOrigin
                : _context.projectileOrigin;

            if (authoredProjectileOrigin != null
                && authoredProjectileOrigin.parent == _context.transform
                && ResolveEffectiveProjectileOriginMode() == ProjectileOriginMode.BowNode)
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

        public PlayerCombatAnchor FindChildAnchor(string childName, PlayerCombatAnchorKind expectedKind)
        {
            Transform child = _context.transform.Find(childName);
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

        public Vector2 GetMeleeHitboxCenter()
        {
            if (TryResolveAnchorWorldPosition(_context.meleeHitboxAnchor, out Vector2 authoredCenter))
            {
                return authoredCenter;
            }

            Vector2 colliderSize = _statResolver.ResolveColliderSize();
            Vector2 colliderOffset = _statResolver.ResolveColliderOffset();
            Vector2 chestOffset = new Vector2(colliderSize.x * 0.15f * _context.facing, colliderSize.y * 0.15f);
            Vector2 anchorOffset = new Vector2((colliderSize.x * 0.5f + 12f) * _context.facing, 0f);
            return (Vector2)_context.transform.position + colliderOffset + chestOffset + anchorOffset;
        }

        public Vector2 GetMeleeHitboxSize()
        {
            if (_context.meleeHitboxAnchor != null && _context.meleeHitboxAnchor.boxSize.sqrMagnitude > 0.001f)
            {
                return _context.meleeHitboxAnchor.boxSize;
            }

            var overrideData = FindActionColliderOverride("melee");
            if (overrideData != null)
            {
                return overrideData.size;
            }

            Vector2 colliderSize = _statResolver.ResolveColliderSize();
            return new Vector2(
                Mathf.Max(72f, colliderSize.x * 0.85f),
                Mathf.Max(64f, colliderSize.y * 0.45f));
        }

        public Vector2 GetUltimateHitboxCenter()
        {
            if (TryResolveAnchorWorldPosition(_context.ultimateHitboxAnchor, out Vector2 authoredCenter))
            {
                return authoredCenter;
            }

            Vector2 colliderSize = _statResolver.ResolveColliderSize();
            Vector2 colliderOffset = _statResolver.ResolveColliderOffset();
            Vector2 chestOffset = new Vector2(colliderSize.x * 0.2f * _context.facing, colliderSize.y * 0.1f);
            Vector2 forwardOffset = new Vector2((colliderSize.x * 0.4f + _statResolver.ResolveUltimateRadius() * 0.4f) * _context.facing, 0f);
            return (Vector2)_context.transform.position + colliderOffset + chestOffset + forwardOffset;
        }

        public bool TryCaptureUltimateHitShapeSnapshot(out CombatShapeSnapshot snapshot)
        {
            Collider2D authoredCollider = _context.ultimateHitboxAnchor != null ? _context.ultimateHitboxAnchor.AttachedCollider : null;
            if (authoredCollider is BoxCollider2D box)
            {
                snapshot = new CombatShapeSnapshot
                {
                    shapeKind = CombatShapeKind.Box,
                    center = box.transform.TransformPoint(box.offset),
                    size = PlayerCollisionSystem.ScaleAbsolute(box.size, box.transform.lossyScale),
                    angle = box.transform.eulerAngles.z,
                    capsuleDirection = CapsuleDirection2D.Horizontal,
                };
                return snapshot.size.sqrMagnitude > 0.001f;
            }

            if (authoredCollider is CapsuleCollider2D capsule)
            {
                snapshot = new CombatShapeSnapshot
                {
                    shapeKind = CombatShapeKind.Capsule,
                    center = capsule.transform.TransformPoint(capsule.offset),
                    size = PlayerCollisionSystem.ScaleAbsolute(capsule.size, capsule.transform.lossyScale),
                    angle = capsule.transform.eulerAngles.z,
                    capsuleDirection = capsule.direction,
                };
                return snapshot.size.sqrMagnitude > 0.001f;
            }

            if (authoredCollider is CircleCollider2D circle)
            {
                snapshot = new CombatShapeSnapshot
                {
                    shapeKind = CombatShapeKind.Circle,
                    center = circle.transform.TransformPoint(circle.offset),
                    radius = circle.radius * Mathf.Max(Mathf.Abs(circle.transform.lossyScale.x), Mathf.Abs(circle.transform.lossyScale.y)),
                    capsuleDirection = CapsuleDirection2D.Horizontal,
                };
                return snapshot.radius > 0.01f;
            }

            snapshot = new CombatShapeSnapshot
            {
                shapeKind = CombatShapeKind.Circle,
                center = GetUltimateHitboxCenter(),
                radius = _statResolver.ResolveUltimateRadius(),
                capsuleDirection = CapsuleDirection2D.Horizontal,
            };
            return snapshot.radius > 0.01f;
        }

        public int CollectHitsForShape(CombatShapeSnapshot shape, Collider2D[] results)
        {
            ContactFilter2D filter = _context.Controller != null ? GetMeleeContactFilter() : new ContactFilter2D();
            switch (shape.shapeKind)
            {
                case CombatShapeKind.Box:
                    return Physics2D.OverlapBox(shape.center, shape.size, shape.angle, filter, results);
                case CombatShapeKind.Capsule:
                    return Physics2D.OverlapCapsule(shape.center, shape.size, shape.capsuleDirection, shape.angle, filter, results);
                default:
                    return Physics2D.OverlapCircle(shape.center, shape.radius, filter, results);
            }
        }

        public static Vector2 ResolveCombatantAimPoint(PlayerController combatant)
        {
            if (combatant != null && !Application.isPlaying)
            {
                return combatant.transform.position;
            }

            if (combatant != null && combatant.bodyCollider != null)
            {
                return combatant.bodyCollider.bounds.center;
            }

            return combatant != null ? (Vector2)combatant.transform.position : Vector2.zero;
        }

        private ContactFilter2D GetMeleeContactFilter()
        {
            return new ContactFilter2D
            {
                useTriggers = false,
                useLayerMask = false
            };
        }

        private ActionColliderOverride FindActionColliderOverride(string actionName)
        {
            return _context.characterDefinition != null ? _context.characterDefinition.FindActionColliderOverride(actionName) : null;
        }
    }
}
