using UnityEngine;

namespace ProjectPVP.Input
{
    internal static class AiArenaHeuristicPolicy
    {
        public static string DecideJson(string snapshotJson)
        {
            AiArenaSnapshotEnvelope snapshot = JsonUtility.FromJson<AiArenaSnapshotEnvelope>(snapshotJson);
            AiArenaDecisionEnvelope decision = Decide(snapshot);
            return JsonUtility.ToJson(decision);
        }

        public static AiArenaDecisionEnvelope Decide(AiArenaSnapshotEnvelope snapshot)
        {
            var decision = new AiArenaDecisionEnvelope();
            if (snapshot == null || snapshot.schemaVersion != AiArenaSnapshotEnvelope.CurrentSchemaVersion)
            {
                decision.status = "invalid_snapshot";
                decision.debugSummary = "AI invalid snapshot";
                return decision;
            }

            AiArenaSemanticObservation semantics = snapshot.semantics ?? new AiArenaSemanticObservation();
            AiArenaCombatantObservation self = snapshot.self ?? new AiArenaCombatantObservation();
            AiArenaCombatantObservation target = snapshot.opponents != null && snapshot.opponents.Count > 0 && snapshot.opponents[0] != null
                ? snapshot.opponents[0]
                : new AiArenaCombatantObservation();
            int selfArrows = Mathf.Max(0, self.arrows);
            int targetArrows = Mathf.Max(0, target.arrows);
            bool targetCanStopProjectile = !target.isDead && (target.canParryProjectile || target.canBlockProjectiles);

            if (!semantics.hasTarget || self.isDead)
            {
                decision.status = "idle";
                decision.debugSummary = "AI no target";
                return decision;
            }

            bool canShoot = semantics.selfHasArrows && self.shootCooldownLeft <= 0.01f && !targetCanStopProjectile;
            bool canMelee = self.meleeCooldownLeft <= 0.01f && !self.isMeleeActive;
            bool canDash = self.dashCooldownLeft <= 0.01f && !self.isDashing;
            bool canUltimate = self.ultimateCooldownLeft <= 0.01f && !self.isUltimateActive;

            float axis = ResolveNeutralAxis(snapshot.frame, semantics, self, target, selfArrows, targetArrows);
            Vector2 aim = semantics.predictedTargetDirection.sqrMagnitude > 0.001f
                ? semantics.predictedTargetDirection.normalized
                : semantics.targetDirection.normalized;
            bool prioritizeCollection = semantics.shouldCollectProjectile
                && (selfArrows <= 1 || targetArrows > selfArrows);

            bool useJump = false;
            bool useShoot = false;
            bool useMelee = false;
            bool useUltimate = false;
            bool useDash = false;
            bool holdProjectileDefense = false;
            bool holdUltimateDefense = false;

            if (semantics.incomingProjectileThreat)
            {
                if (self.canBlockProjectiles)
                {
                    axis = 0f;
                    holdProjectileDefense = true;
                    decision.debugSummary = "AI PROJECTILE BLOCK";
                }
                else if (self.canParryProjectile && semantics.incomingProjectileTime <= 0.18f)
                {
                    axis = 0f;
                    holdProjectileDefense = true;
                    decision.debugSummary = "AI PARRY HOLD";
                }
                else if (semantics.shouldDashEvade && canDash)
                {
                    axis = ResolveIncomingProjectileDashAxis(
                        semantics,
                        semantics.targetDirection.x >= 0f ? 1f : -1f);
                    useDash = true;
                    decision.debugSummary = "AI PARRY DASH";
                }
                else if (semantics.shouldJumpEvade || (semantics.shouldDashEvade && self.isGrounded))
                {
                    axis = semantics.targetDirection.x >= 0f ? -0.35f : 0.35f;
                    useJump = true;
                    decision.debugSummary = "AI JUMP EVADE";
                }
                else
                {
                    axis = ResolveIncomingProjectileDriftAxis(
                        semantics,
                        semantics.targetDirection.x >= 0f ? -1f : 1f) * 0.35f;
                    holdProjectileDefense = true;
                    decision.debugSummary = "AI PROJECTILE DRIFT";
                }
            }
            else if (prioritizeCollection && !semantics.targetUsingUltimate)
            {
                axis = ResolveCollectionMoveAxis(semantics.collectibleProjectileDirection);
                useJump = ShouldJumpForCollectible(semantics.collectibleProjectileDirection, self);
                decision.debugSummary = "AI COLLECT ARROW";
            }

            if (!useDash && !useJump && !holdProjectileDefense && semantics.targetUsingUltimate)
            {
                if (canDash)
                {
                    axis = semantics.targetDirection.x >= 0f ? -1f : 1f;
                    useDash = true;
                    decision.debugSummary = "AI DODGE ULT";
                }
                else
                {
                    axis = semantics.targetDirection.x >= 0f ? -1f : 1f;
                    holdUltimateDefense = true;
                    decision.debugSummary = "AI EVADE ULT";
                }
            }

            if (!prioritizeCollection && !useDash && !useJump && !holdProjectileDefense && !holdUltimateDefense && semantics.shouldPunish)
            {
                if (canUltimate && semantics.targetInUltimateRange && !semantics.incomingProjectileThreat)
                {
                    useUltimate = true;
                    axis = 0f;
                    decision.debugSummary = "AI PUNISH ULT";
                }
                else if (canMelee && semantics.targetInMeleeRange)
                {
                    useMelee = true;
                    axis = 0f;
                    decision.debugSummary = "AI PUNISH MELEE";
                }
                else if (canShoot && semantics.targetInShootRange)
                {
                    useShoot = true;
                    decision.debugSummary = "AI PUNISH SHOT";
                }
            }

            if (!prioritizeCollection && !useDash && !useJump && !useUltimate && !useMelee && !holdProjectileDefense && !holdUltimateDefense)
            {
                if (semantics.shouldAntiAir && canShoot)
                {
                    useShoot = true;
                    decision.debugSummary = "AI ANTI AIR";
                }
                else if (canMelee && semantics.targetInMeleeRange && !semantics.targetUsingUltimate && !semantics.targetUsingMelee)
                {
                    useMelee = true;
                    axis = 0f;
                    decision.debugSummary = "AI MELEE";
                }
                else if (canShoot && semantics.targetInShootRange && semantics.shouldZone && !semantics.targetUsingUltimate)
                {
                    useShoot = true;
                    decision.debugSummary = "AI ZONE SHOT";
                }
                else if (canUltimate && semantics.targetInUltimateRange && semantics.targetCornered && !semantics.incomingProjectileThreat)
                {
                    useUltimate = true;
                    axis = 0f;
                    decision.debugSummary = "AI CORNER ULT";
                }
                else if (canDash
                    && semantics.shouldPressure
                    && semantics.horizontalDistance > 520f
                    && !semantics.incomingProjectileThreat
                    && !semantics.targetUsingUltimate)
                {
                    axis = semantics.targetDirection.x >= 0f ? 1f : -1f;
                    useDash = true;
                    decision.debugSummary = "AI DASH IN";
                }
                else if (semantics.targetAbove && semantics.horizontalDistance < 640f && self.isGrounded)
                {
                    useJump = true;
                    decision.debugSummary = "AI CLIMB";
                }
            }

            if (string.IsNullOrWhiteSpace(decision.debugSummary))
            {
                decision.debugSummary = useUltimate
                    ? "AI ULT"
                    : useMelee
                        ? "AI MELEE"
                        : useShoot
                            ? "AI SHOOT"
                            : useDash
                                ? "AI DASH"
                                : Mathf.Abs(axis) > 0.1f
                                    ? "AI MOVE"
                                    : "AI HOLD";
            }

            decision.moveAxis = Mathf.Clamp(axis, -1f, 1f);
            decision.aimX = aim.x;
            decision.aimY = aim.y;
            decision.jumpPressed = useJump;
            decision.jumpHeld = useJump || (!holdProjectileDefense && !holdUltimateDefense && semantics.targetAbove && semantics.horizontalDistance < 700f);
            decision.shootPressed = useShoot;
            decision.shootHeld = useShoot;
            decision.meleePressed = useMelee;
            decision.ultimatePressed = useUltimate;
            decision.dashPrimaryPressed = useDash;
            return decision;
        }

        private static float ResolveNeutralAxis(
            int frame,
            AiArenaSemanticObservation semantics,
            AiArenaCombatantObservation self,
            AiArenaCombatantObservation target,
            int selfArrows,
            int targetArrows)
        {
            float towardTarget = semantics.targetDirection.x >= 0f ? 1f : -1f;
            float awayFromTarget = -towardTarget;

            if (semantics.selfCornered && semantics.horizontalDistance < 240f)
            {
                return towardTarget;
            }

            if (targetArrows <= 0 && selfArrows > 0)
            {
                return towardTarget;
            }

            if (selfArrows <= 0 && targetArrows > 0)
            {
                return awayFromTarget;
            }

            if (selfArrows > targetArrows && targetArrows <= 1)
            {
                return towardTarget;
            }

            if (semantics.targetUsingMelee || semantics.targetPressuring || semantics.shouldRetreat)
            {
                return awayFromTarget;
            }

            if (semantics.shouldPressure || semantics.shouldAdvance)
            {
                return towardTarget;
            }

            if (semantics.shouldZone && semantics.targetInShootRange)
            {
                float strafe = ((frame / 20) % 2 == 0) ? 0.35f : -0.35f;
                return target.position.y > self.position.y ? strafe : -strafe;
            }

            return 0f;
        }

        internal static float ResolveCollectionMoveAxis(Vector2 collectibleDirection)
        {
            if (collectibleDirection.x > 0.1f)
            {
                return 1f;
            }

            if (collectibleDirection.x < -0.1f)
            {
                return -1f;
            }

            return 0f;
        }

        internal static bool ShouldJumpForCollectible(
            Vector2 collectibleDirection,
            AiArenaCombatantObservation self)
        {
            return self != null
                && self.isGrounded
                && collectibleDirection.y > 0.35f;
        }

        internal static float ResolveIncomingProjectileDashAxis(
            AiArenaSemanticObservation semantics,
            float fallbackAxis)
        {
            if (semantics != null && Mathf.Abs(semantics.incomingProjectileDirection.x) > 0.1f)
            {
                return semantics.incomingProjectileDirection.x < 0f ? 1f : -1f;
            }

            if (Mathf.Abs(fallbackAxis) > 0.1f)
            {
                return fallbackAxis > 0f ? 1f : -1f;
            }

            return 0f;
        }

        internal static float ResolveIncomingProjectileDriftAxis(
            AiArenaSemanticObservation semantics,
            float fallbackAxis)
        {
            if (semantics != null && Mathf.Abs(semantics.incomingProjectileDirection.x) > 0.1f)
            {
                return semantics.incomingProjectileDirection.x > 0f ? 1f : -1f;
            }

            if (Mathf.Abs(fallbackAxis) > 0.1f)
            {
                return fallbackAxis > 0f ? 1f : -1f;
            }

            return 0f;
        }
    }
}
