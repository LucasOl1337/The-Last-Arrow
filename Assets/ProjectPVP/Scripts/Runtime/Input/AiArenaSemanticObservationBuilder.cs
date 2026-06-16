using System.Collections.Generic;
using UnityEngine;

namespace ProjectPVP.Input
{
    internal static class AiArenaSemanticObservationBuilder
    {
        internal static AiArenaSemanticObservation Build(
            AiArenaControllerSnapshot self,
            AiArenaControllerSnapshot target,
            IReadOnlyList<AiArenaProjectileSnapshot> projectiles,
            AiArenaArenaSnapshot arena,
            float desiredCombatDistance,
            float closeRetreatDistance,
            float meleeRange,
            float ultimateRange,
            float shootRange,
            float verticalTolerance)
        {
            AiArenaSemanticObservation semantics = new AiArenaSemanticObservation
            {
                selfHasArrows = self.arrows > 0,
            };

            PopulateProjectileThreatSemantics(ref semantics, self, projectiles, verticalTolerance);

            if (!self.isValid || !target.isValid)
            {
                semantics.hasTarget = false;
                semantics.predictedTargetDirection = new Vector2(self.facing == 0 ? 1f : self.facing, 0f);
                return semantics;
            }

            Vector2 offset = target.position - self.position;
            float horizontalDistance = Mathf.Abs(offset.x);
            float verticalDistance = Mathf.Abs(offset.y);
            Vector2 direction = offset.sqrMagnitude > 0.0001f ? offset.normalized : Vector2.right;
            float leadTime = Mathf.Clamp(horizontalDistance / Mathf.Max(320f, shootRange), 0.06f, 0.2f);
            Vector2 predictedOffset = offset + target.velocity * leadTime;
            Vector2 predictedDirection = predictedOffset.sqrMagnitude > 0.0001f ? predictedOffset.normalized : direction;
            float selfEdgeDistance = Mathf.Min(
                Mathf.Abs(self.position.x - arena.wrapBounds.xMin),
                Mathf.Abs(arena.wrapBounds.xMax - self.position.x));
            float targetEdgeDistance = Mathf.Min(
                Mathf.Abs(target.position.x - arena.wrapBounds.xMin),
                Mathf.Abs(arena.wrapBounds.xMax - target.position.x));
            bool targetUsingMelee = target.isMeleeActive && IsWithinBoxThreat(self.position, target.meleeHitboxCenter, target.meleeHitboxSize, 48f);
            bool targetUsingUltimate = target.isUltimateActive && IsWithinCircleThreat(self.position, target.ultimateHitboxCenter, target.ultimateHitboxRadius, 40f);
            bool targetUsingRanged = target.isShootAnimating;
            bool targetVulnerable = target.isHitStunned
                || (target.isShootAnimating && !targetUsingUltimate)
                || (target.isMeleeActive && !targetUsingMelee && verticalDistance <= verticalTolerance);

            semantics.hasTarget = true;
            semantics.targetSlotId = target.slotId;
            semantics.horizontalDistance = horizontalDistance;
            semantics.verticalDistance = verticalDistance;
            semantics.targetDirection = direction;
            semantics.predictedTargetDirection = predictedDirection;
            semantics.targetAbove = offset.y > 32f;
            semantics.targetBelow = offset.y < -32f;
            semantics.targetInMeleeRange = horizontalDistance <= meleeRange && verticalDistance <= Mathf.Min(verticalTolerance, 96f);
            semantics.targetInUltimateRange = horizontalDistance <= ultimateRange && verticalDistance <= Mathf.Min(verticalTolerance, 120f);
            semantics.targetInShootRange = horizontalDistance <= shootRange && verticalDistance <= verticalTolerance;
            semantics.shouldAdvance = horizontalDistance > desiredCombatDistance;
            semantics.shouldRetreat = horizontalDistance < closeRetreatDistance || targetUsingMelee || targetUsingUltimate;
            semantics.shouldPressure = !semantics.incomingProjectileThreat
                && (!semantics.selfHasArrows || targetVulnerable || targetEdgeDistance < 180f || horizontalDistance < desiredCombatDistance * 0.85f);
            semantics.shouldZone = semantics.selfHasArrows
                && !targetVulnerable
                && !targetUsingUltimate
                && horizontalDistance >= meleeRange * 1.35f;
            semantics.shouldPunish = targetVulnerable
                && (semantics.targetInMeleeRange || semantics.targetInUltimateRange || semantics.targetInShootRange);
            semantics.shouldAntiAir = semantics.targetAbove
                && horizontalDistance <= shootRange
                && target.velocity.y <= 80f;
            semantics.targetVulnerable = targetVulnerable;
            semantics.targetPressuring = targetUsingMelee || targetUsingUltimate || horizontalDistance < closeRetreatDistance;
            semantics.targetUsingRanged = targetUsingRanged;
            semantics.targetUsingMelee = targetUsingMelee;
            semantics.targetUsingUltimate = targetUsingUltimate;
            semantics.selfCornered = selfEdgeDistance < 180f;
            semantics.targetCornered = targetEdgeDistance < 180f;
            return semantics;
        }

        private static void PopulateProjectileThreatSemantics(
            ref AiArenaSemanticObservation semantics,
            AiArenaControllerSnapshot self,
            IReadOnlyList<AiArenaProjectileSnapshot> projectiles,
            float verticalTolerance)
        {
            if (projectiles == null || !self.isValid)
            {
                return;
            }

            float bestThreatTime = float.MaxValue;
            Vector2 bestThreatDirection = Vector2.zero;
            for (int index = 0; index < projectiles.Count; index += 1)
            {
                AiArenaProjectileSnapshot projectile = projectiles[index];
                if (!projectile.isValid || projectile.isStuck || projectile.isDisarmed)
                {
                    continue;
                }

                Vector2 toSelf = self.position - projectile.position;
                float speedSqr = projectile.velocity.sqrMagnitude;
                if (speedSqr <= 1f || Vector2.Dot(toSelf, projectile.velocity) <= 0f)
                {
                    continue;
                }

                float timeToClosest = Mathf.Clamp(Vector2.Dot(toSelf, projectile.velocity) / speedSqr, 0f, 1.5f);
                Vector2 closestOffset = toSelf - projectile.velocity * timeToClosest;
                float lateralDistance = Mathf.Abs(closestOffset.y);
                if (lateralDistance > Mathf.Min(verticalTolerance, 140f))
                {
                    continue;
                }

                if (timeToClosest >= bestThreatTime)
                {
                    continue;
                }

                bestThreatTime = timeToClosest;
                bestThreatDirection = projectile.travelDirection.sqrMagnitude > 0.001f
                    ? projectile.travelDirection.normalized
                    : projectile.velocity.normalized;
            }

            if (bestThreatTime == float.MaxValue)
            {
                return;
            }

            semantics.incomingProjectileThreat = true;
            semantics.incomingProjectileTime = bestThreatTime;
            semantics.incomingProjectileDirection = bestThreatDirection;
            semantics.shouldDashEvade = bestThreatTime <= 0.2f;
            semantics.shouldJumpEvade = bestThreatTime > 0.2f && bestThreatTime <= 0.35f && self.isGrounded;
        }

        private static bool IsWithinBoxThreat(Vector2 point, Vector2 center, Vector2 size, float padding)
        {
            if (size == Vector2.zero)
            {
                return false;
            }

            Vector2 halfSize = size * 0.5f + new Vector2(padding, padding);
            Vector2 delta = point - center;
            return Mathf.Abs(delta.x) <= halfSize.x && Mathf.Abs(delta.y) <= halfSize.y;
        }

        private static bool IsWithinCircleThreat(Vector2 point, Vector2 center, float radius, float padding)
        {
            if (radius <= 0.01f)
            {
                return false;
            }

            float radiusWithPadding = radius + padding;
            return (point - center).sqrMagnitude <= radiusWithPadding * radiusWithPadding;
        }
    }
}
