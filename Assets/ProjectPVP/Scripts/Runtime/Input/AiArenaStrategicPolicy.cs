using UnityEngine;

namespace ProjectPVP.Input
{
    public static class AiArenaStrategicPolicy
    {
        public static AiArenaDecisionEnvelope Decide(AiArenaSnapshotEnvelope snapshot, CodexStrategyIntent intent)
        {
            AiArenaDecisionEnvelope baseline = AiArenaHeuristicPolicy.Decide(snapshot);
            if (snapshot == null || snapshot.semantics == null || intent == null || !snapshot.semantics.hasTarget)
            {
                return baseline;
            }

            AiArenaSemanticObservation semantics = snapshot.semantics;
            AiArenaCombatantObservation self = snapshot.self ?? new AiArenaCombatantObservation();
            AiArenaDecisionEnvelope decision = baseline;
            AiArenaCombatantObservation target = snapshot.opponents != null && snapshot.opponents.Count > 0 && snapshot.opponents[0] != null
                ? snapshot.opponents[0]
                : new AiArenaCombatantObservation();
            bool targetCanStopProjectile = !target.isDead && (target.canParryProjectile || target.canBlockProjectiles);
            bool canShoot = self.arrows > 0 && self.shootCooldownLeft <= 0.01f && !targetCanStopProjectile;
            bool canMelee = self.meleeCooldownLeft <= 0.01f && !self.isMeleeActive;
            bool canUltimate = self.ultimateCooldownLeft <= 0.01f && !self.isUltimateActive;
            bool canDash = self.dashCooldownLeft <= 0.01f && !self.isDashing;
            int targetArrows = Mathf.Max(0, target.arrows);
            int arrowLead = self.arrows - targetArrows;
            bool prioritizeCollection = semantics.shouldCollectProjectile
                && (self.arrows <= 1 || targetArrows > self.arrows);

            float towardTarget = semantics.targetDirection.x >= 0f ? 1f : -1f;
            float awayFromTarget = -towardTarget;
            float preferredRange = Mathf.Max(80f, intent.preferredRange);
            float distanceError = semantics.horizontalDistance - preferredRange;

            if (prioritizeCollection && !semantics.incomingProjectileThreat && !semantics.targetUsingUltimate)
            {
                decision.shootPressed = false;
                decision.shootHeld = false;
                decision.meleePressed = false;
                decision.ultimatePressed = false;
                decision.dashPrimaryPressed = false;
                decision.dashSecondaryPressed = false;
                decision.jumpPressed = AiArenaHeuristicPolicy.ShouldJumpForCollectible(semantics.collectibleProjectileDirection, self);
                decision.jumpHeld = decision.jumpPressed;
                decision.moveAxis = AiArenaHeuristicPolicy.ResolveCollectionMoveAxis(semantics.collectibleProjectileDirection);
                decision.debugSummary = "AI COLLECT ARROW";
            }

            if (!semantics.incomingProjectileThreat && !semantics.targetUsingUltimate && !prioritizeCollection)
            {
                switch (NormalizeMode(intent.mode))
                {
                    case "pressure":
                        decision.moveAxis = Mathf.Clamp(
                            towardTarget * Mathf.Lerp(0.55f, 1f, Mathf.Clamp01(intent.advanceBias)) - distanceError / preferredRange * 0.3f,
                            -1f,
                            1f);
                        if (canMelee && semantics.targetInMeleeRange && (intent.meleeBias >= 0.3f || semantics.targetVulnerable || semantics.shouldPunish))
                        {
                            decision.meleePressed = true;
                        }

                        if (!decision.meleePressed && canShoot && semantics.targetInShootRange && (intent.shootBias >= 0.25f || semantics.targetAbove))
                        {
                            decision.shootPressed = true;
                            decision.shootHeld = true;
                        }

                        if (semantics.horizontalDistance > preferredRange * 1.05f && canDash && intent.dashBias >= 0.5f)
                        {
                            decision.dashPrimaryPressed = true;
                        }

                        if (!decision.dashPrimaryPressed && self.isGrounded && semantics.targetAbove && intent.jumpBias >= 0.22f)
                        {
                            decision.jumpPressed = true;
                            decision.jumpHeld = true;
                        }
                        break;
                    case "zone":
                        decision.moveAxis = semantics.horizontalDistance < preferredRange * 0.8f
                            ? awayFromTarget * Mathf.Lerp(0.45f, 0.95f, Mathf.Clamp01(intent.cornerEscapeBias))
                            : Mathf.Abs(distanceError) > 42f ? Mathf.Sign(distanceError) * towardTarget * 0.3f : towardTarget * 0.12f;
                        decision.shootPressed = canShoot && semantics.targetInShootRange && (intent.shootBias >= 0.22f || semantics.targetCornered || semantics.targetAbove);
                        decision.shootHeld = decision.shootPressed;
                        decision.meleePressed = false;
                        if (!decision.shootPressed && canMelee && semantics.targetInMeleeRange && semantics.targetVulnerable)
                        {
                            decision.meleePressed = true;
                        }
                        break;
                    case "retreat":
                    case "stabilize":
                        decision.moveAxis = semantics.horizontalDistance < preferredRange * 0.75f
                            ? awayFromTarget * Mathf.Lerp(0.45f, 1f, Mathf.Clamp01(intent.cornerEscapeBias))
                            : semantics.horizontalDistance > preferredRange * 1.15f
                                ? towardTarget * Mathf.Lerp(0.2f, 0.55f, Mathf.Clamp01(intent.advanceBias))
                                : 0f;
                        decision.meleePressed = canMelee && semantics.targetInMeleeRange && (semantics.targetVulnerable || semantics.shouldPunish) && intent.meleeBias >= 0.22f;
                        decision.ultimatePressed = false;
                        if (!decision.meleePressed && canShoot && semantics.targetInShootRange && (intent.shootBias >= 0.2f || semantics.horizontalDistance > preferredRange * 0.6f))
                        {
                            decision.shootPressed = true;
                            decision.shootHeld = true;
                        }
                        if (semantics.selfCornered && canDash && semantics.horizontalDistance < preferredRange * 0.65f)
                        {
                            decision.dashPrimaryPressed = true;
                        }
                        break;
                    case "punish":
                        if (semantics.shouldPunish || intent.punishRecovery)
                        {
                            if (canUltimate && semantics.targetInUltimateRange && (semantics.targetCornered || semantics.targetVulnerable) && intent.dashBias < 0.9f)
                            {
                                decision.ultimatePressed = true;
                                decision.moveAxis = 0f;
                            }
                            else if (canMelee && semantics.targetInMeleeRange)
                            {
                                decision.meleePressed = true;
                                decision.moveAxis = 0f;
                            }
                            else
                            {
                                decision.shootPressed = canShoot && semantics.targetInShootRange;
                                decision.shootHeld = decision.shootPressed;
                                decision.moveAxis = semantics.targetInShootRange ? 0.25f * towardTarget : towardTarget;
                                if (!decision.shootPressed && canDash && semantics.horizontalDistance > preferredRange * 0.8f)
                                {
                                    decision.dashPrimaryPressed = true;
                                }
                            }
                        }
                        break;
                }
            }

            if (!semantics.incomingProjectileThreat && !semantics.targetUsingUltimate && !prioritizeCollection)
            {
                if (targetArrows <= 0 && self.arrows > 0)
                {
                    decision.moveAxis = towardTarget * Mathf.Max(0.35f, Mathf.Abs(decision.moveAxis));
                    if (!decision.meleePressed && canMelee && semantics.targetInMeleeRange)
                    {
                        decision.meleePressed = true;
                    }
                    else if (!decision.shootPressed && canShoot && semantics.targetInShootRange)
                    {
                        decision.shootPressed = true;
                        decision.shootHeld = true;
                    }
                    else if (!decision.dashPrimaryPressed && canDash && semantics.horizontalDistance > 180f)
                    {
                        decision.dashPrimaryPressed = true;
                    }

                    decision.debugSummary = "AI LAST ARROW PRESSURE";
                }
                else if (self.arrows <= 0 && targetArrows > 0)
                {
                    decision.moveAxis = awayFromTarget * Mathf.Max(0.25f, Mathf.Abs(decision.moveAxis));
                    decision.debugSummary = "AI ARROW DISADVANTAGE";
                }
                else if (arrowLead > 0 && semantics.targetInShootRange && canShoot)
                {
                    decision.moveAxis = towardTarget * Mathf.Max(0.2f, Mathf.Abs(decision.moveAxis));
                    if (!decision.meleePressed && canMelee && semantics.targetInMeleeRange && (semantics.targetCornered || semantics.targetVulnerable))
                    {
                        decision.meleePressed = true;
                    }
                    else if (!decision.shootPressed && canShoot && semantics.targetInShootRange && (semantics.targetCornered || semantics.targetVulnerable || semantics.shouldPunish || semantics.horizontalDistance <= preferredRange * 1.1f))
                    {
                        decision.shootPressed = true;
                        decision.shootHeld = true;
                    }
                    else if (!decision.dashPrimaryPressed && canDash && semantics.horizontalDistance > preferredRange * 0.85f)
                    {
                        decision.dashPrimaryPressed = true;
                    }

                    decision.debugSummary = "AI ARROW LEAD PRESSURE";
                }
            }

            if (intent.antiAir && semantics.targetAbove && semantics.targetInShootRange && self.arrows > 0 && !prioritizeCollection)
            {
                decision.shootPressed = true;
                decision.shootHeld = true;
            }

            if (!prioritizeCollection && !decision.shootPressed && !decision.meleePressed && !decision.ultimatePressed && !decision.dashPrimaryPressed)
            {
                if (baseline.ultimatePressed && canUltimate && semantics.targetInUltimateRange)
                {
                    decision.ultimatePressed = true;
                }
                else if (baseline.meleePressed && canMelee && semantics.targetInMeleeRange)
                {
                    decision.meleePressed = true;
                }
                else if (baseline.shootPressed && canShoot && semantics.targetInShootRange)
                {
                    decision.shootPressed = true;
                    decision.shootHeld = true;
                }
                else if (baseline.dashPrimaryPressed && canDash && semantics.horizontalDistance > preferredRange * 0.9f)
                {
                    decision.dashPrimaryPressed = true;
                }
            }

            if (semantics.incomingProjectileThreat)
            {
                if (self.canBlockProjectiles)
                {
                    decision.moveAxis = 0f;
                    ClearCombatActions(decision);
                    decision.debugSummary = "AI PROJECTILE BLOCK";
                }
                else
                {
                    switch (NormalizeAntiProjectile(intent.antiProjectile))
                    {
                        case "dash":
                            if (self.dashCooldownLeft <= 0.01f && !self.isDashing)
                            {
                                ClearCombatActions(decision);
                                decision.moveAxis = AiArenaHeuristicPolicy.ResolveIncomingProjectileDashAxis(semantics, awayFromTarget);
                                decision.dashPrimaryPressed = true;
                                decision.debugSummary = "AI PROJECTILE DASH";
                            }
                            break;
                        case "jump":
                            if (self.isGrounded)
                            {
                                ClearCombatActions(decision);
                                decision.jumpPressed = true;
                                decision.jumpHeld = true;
                                decision.debugSummary = "AI PROJECTILE JUMP";
                            }
                            break;
                        case "parry_prefer":
                            decision.moveAxis = 0f;
                            ClearCombatActions(decision);
                            decision.debugSummary = "AI PARRY HOLD";
                            break;
                    }
                }
            }

            Vector2 aim = semantics.predictedTargetDirection.sqrMagnitude > 0.001f
                ? semantics.predictedTargetDirection.normalized
                : semantics.targetDirection.normalized;
            decision.aimX = aim.x;
            decision.aimY = aim.y;
            if (string.IsNullOrWhiteSpace(decision.debugSummary))
            {
                decision.debugSummary = "AI | Codex:" + NormalizeMode(intent.mode) + " | " + intent.reason;
            }
            return decision;
        }

        private static string NormalizeMode(string mode)
        {
            if (string.IsNullOrWhiteSpace(mode))
            {
                return "stabilize";
            }

            string normalized = mode.Trim().ToLowerInvariant();
            return normalized switch
            {
                "pressure" => "pressure",
                "zone" => "zone",
                "retreat" => "retreat",
                "punish" => "punish",
                _ => "stabilize",
            };
        }

        private static string NormalizeAntiProjectile(string antiProjectile)
        {
            if (string.IsNullOrWhiteSpace(antiProjectile))
            {
                return "hold";
            }

            string normalized = antiProjectile.Trim().ToLowerInvariant();
            return normalized switch
            {
                "jump" => "jump",
                "dash" => "dash",
                "parry_prefer" => "parry_prefer",
                _ => "hold",
            };
        }

        private static void ClearCombatActions(AiArenaDecisionEnvelope decision)
        {
            if (decision == null)
            {
                return;
            }

            decision.shootPressed = false;
            decision.shootHeld = false;
            decision.meleePressed = false;
            decision.ultimatePressed = false;
            decision.dashPrimaryPressed = false;
            decision.dashSecondaryPressed = false;
            decision.jumpPressed = false;
            decision.jumpHeld = false;
        }
    }
}
