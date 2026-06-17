using System.Collections.Generic;
using UnityEngine;

namespace ProjectPVP.Input
{
    internal static class AiArenaSemanticObservationBuilder
    {
        private const float EstimatedProjectileSpeed = 1600f;

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
            PopulateCollectibleProjectileSemantics(ref semantics, self, projectiles);

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
            Vector2 predictedDirection = ResolvePredictedTargetDirection(
                offset,
                target.velocity,
                self.velocity,
                ResolveProjectileInheritVelocityFactor(self),
                ResolveProjectileBaseSpeed(self),
                ResolveProjectileGravity(self),
                leadTime,
                direction);
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
            bool selfCornered = selfEdgeDistance < 180f;
            bool targetCornered = targetEdgeDistance < 180f;

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
                && (!semantics.selfHasArrows || targetVulnerable || targetEdgeDistance < 180f || horizontalDistance <= desiredCombatDistance * 1.15f);
            semantics.shouldZone = semantics.selfHasArrows
                && !targetVulnerable
                && !targetUsingUltimate
                && horizontalDistance >= meleeRange * 1.35f;
            semantics.shouldPunish = targetVulnerable
                && (semantics.targetInMeleeRange || semantics.targetInUltimateRange || semantics.targetInShootRange);
            semantics.shouldAntiAir = semantics.targetAbove
                && semantics.targetInShootRange
                && target.velocity.y <= 80f;
            semantics.shouldCollectProjectile = semantics.hasCollectibleProjectile
                && (self.arrows <= 1 || semantics.shouldRetreat || selfCornered)
                && semantics.collectibleProjectileDistance <= 720f;
            semantics.targetVulnerable = targetVulnerable;
            semantics.targetPressuring = targetUsingMelee || targetUsingUltimate || horizontalDistance < closeRetreatDistance;
            semantics.targetUsingRanged = targetUsingRanged;
            semantics.targetUsingMelee = targetUsingMelee;
            semantics.targetUsingUltimate = targetUsingUltimate;
            semantics.selfCornered = selfCornered;
            semantics.targetCornered = targetCornered;
            return semantics;
        }

        internal static Vector2 ResolvePredictedTargetDirection(
            Vector2 targetOffset,
            Vector2 targetVelocity,
            Vector2 selfVelocity,
            float projectileInheritVelocityFactor,
            float projectileBaseSpeed,
            float projectileGravity,
            float leadTime,
            Vector2 fallbackDirection)
        {
            Vector2 predictedOffset = targetOffset + targetVelocity * leadTime;
            if (predictedOffset.sqrMagnitude <= 0.0001f)
            {
                return fallbackDirection.sqrMagnitude > 0.0001f ? fallbackDirection.normalized : Vector2.right;
            }

            float safeBaseSpeed = Mathf.Max(1f, projectileBaseSpeed);
            float estimatedFlightTime = Mathf.Clamp(predictedOffset.magnitude / safeBaseSpeed, 0.05f, 1.5f);
            predictedOffset.y += 0.5f * Mathf.Max(0f, projectileGravity) * estimatedFlightTime * estimatedFlightTime;

            Vector2 compensatedTravel = (predictedOffset.normalized * safeBaseSpeed)
                - selfVelocity * Mathf.Max(0f, projectileInheritVelocityFactor);
            if (compensatedTravel.sqrMagnitude <= 0.0001f)
            {
                return predictedOffset.normalized;
            }

            return compensatedTravel.normalized;
        }

        private static float ResolveProjectileInheritVelocityFactor(AiArenaControllerSnapshot self)
        {
            return Mathf.Max(0f, self.projectileInheritVelocityFactor);
        }

        private static float ResolveProjectileBaseSpeed(AiArenaControllerSnapshot self)
        {
            return self.projectileBaseSpeed > 0f ? self.projectileBaseSpeed : EstimatedProjectileSpeed;
        }

        private static float ResolveProjectileGravity(AiArenaControllerSnapshot self)
        {
            return self.projectileGravity > 0f ? self.projectileGravity : 1500f;
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
                if (!projectile.isValid || projectile.isStuck || projectile.isDisarmed || projectile.isCollectible)
                {
                    continue;
                }

                if (projectile.sourceSlotId > 0 && projectile.sourceSlotId == self.slotId)
                {
                    continue;
                }

                if (!AiArenaProjectileThreatMath.TryEstimateClosestApproach(
                    self.position,
                    self.velocity,
                    projectile.position,
                    projectile.velocity,
                    out float timeToClosest,
                    out Vector2 closestOffset))
                {
                    continue;
                }

                float lateralDistance = closestOffset.magnitude;
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

        private static void PopulateCollectibleProjectileSemantics(
            ref AiArenaSemanticObservation semantics,
            AiArenaControllerSnapshot self,
            IReadOnlyList<AiArenaProjectileSnapshot> projectiles)
        {
            if (projectiles == null || !self.isValid)
            {
                return;
            }

            float bestDistance = float.MaxValue;
            Vector2 bestDirection = Vector2.zero;

            for (int index = 0; index < projectiles.Count; index += 1)
            {
                AiArenaProjectileSnapshot projectile = projectiles[index];
                if (!projectile.isValid || !projectile.isCollectible)
                {
                    continue;
                }

                Vector2 offset = projectile.position - self.position;
                float distance = offset.magnitude;
                if (distance <= 0.01f || distance >= bestDistance)
                {
                    continue;
                }

                bestDistance = distance;
                bestDirection = offset / distance;
            }

            if (bestDistance == float.MaxValue)
            {
                return;
            }

            semantics.hasCollectibleProjectile = true;
            semantics.collectibleProjectileDistance = bestDistance;
            semantics.collectibleProjectileDirection = bestDirection;
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
