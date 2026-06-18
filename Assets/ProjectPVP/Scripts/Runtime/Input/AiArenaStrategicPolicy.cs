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
            bool cornerEscapeIntent = IsCornerEscapeIntent(intent);
            bool antiAirIntent = IsAntiAirIntent(intent);
            bool antiAirOpportunity = antiAirIntent || semantics.shouldAntiAir;
            bool deferCollectionForCornerEscape = cornerEscapeIntent || AiArenaHeuristicPolicy.ShouldDeferCollectionForCornerEscape(semantics);
            bool prioritizeCollection = semantics.shouldCollectProjectile
                && (self.arrows <= 1 || targetArrows > self.arrows)
                && !deferCollectionForCornerEscape;

            float towardTarget = semantics.targetDirection.x >= 0f ? 1f : -1f;
            float awayFromTarget = -towardTarget;
            float preferredRange = Mathf.Max(80f, intent.preferredRange);
            float distanceError = semantics.horizontalDistance - preferredRange;
            bool defensiveRetreatIntent = IsDefensiveRetreatIntent(intent);
            bool recoverAfterResolvedProjectileThreat = defensiveRetreatIntent
                && IsProjectileThreatFeedbackIntent(intent)
                && !semantics.incomingProjectileThreat
                && !semantics.targetUsingRanged
                && !semantics.targetUsingMelee
                && !semantics.targetUsingUltimate
                && semantics.shouldCollectProjectile;
            bool attackAfterResolvedThreat = defensiveRetreatIntent
                && !semantics.incomingProjectileThreat
                && !semantics.targetUsingRanged
                && !semantics.targetUsingMelee
                && !semantics.targetUsingUltimate
                && semantics.shouldAntiAir
                && semantics.targetAbove
                && semantics.targetInShootRange
                && canShoot;
            bool pressureAfterResolvedThreat = defensiveRetreatIntent
                && !semantics.incomingProjectileThreat
                && !semantics.targetUsingRanged
                && !semantics.targetUsingMelee
                && !semantics.targetUsingUltimate
                && targetArrows <= 0
                && self.arrows > 0;
            bool effectiveDefensiveRetreatIntent = defensiveRetreatIntent
                && !recoverAfterResolvedProjectileThreat
                && !attackAfterResolvedThreat
                && !pressureAfterResolvedThreat;
            float cornerEscapeAxis = ResolveCornerEscapeAxis(snapshot, semantics, self, towardTarget);
            bool escapingCorner = false;

            if (!semantics.incomingProjectileThreat
                && !semantics.targetUsingUltimate
                && effectiveDefensiveRetreatIntent
                && !cornerEscapeIntent)
            {
                ClearCombatActions(decision);
                decision.moveAxis = awayFromTarget * Mathf.Lerp(0.65f, 1f, Mathf.Clamp01(intent.cornerEscapeBias));
                decision.dashPrimaryPressed = canDash && intent.dashBias >= 0.75f;
                decision.debugSummary = "AI DEFENSIVE RETREAT";
            }

            if (prioritizeCollection && !semantics.incomingProjectileThreat && !semantics.targetUsingUltimate && !semantics.targetUsingRanged && !effectiveDefensiveRetreatIntent)
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

            if (!semantics.incomingProjectileThreat && !semantics.targetUsingUltimate && !prioritizeCollection && !effectiveDefensiveRetreatIntent)
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
                        if (effectiveDefensiveRetreatIntent)
                        {
                            ClearCombatActions(decision);
                            decision.moveAxis = awayFromTarget * Mathf.Lerp(0.65f, 1f, Mathf.Clamp01(intent.cornerEscapeBias));
                            decision.dashPrimaryPressed = canDash && intent.dashBias >= 0.75f;
                            decision.debugSummary = "AI DEFENSIVE RETREAT";
                        }
                        else
                        {
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

            if (!semantics.incomingProjectileThreat
                && !semantics.targetUsingUltimate
                && !prioritizeCollection
                && (!effectiveDefensiveRetreatIntent || cornerEscapeIntent)
                && semantics.selfCornered
                && (cornerEscapeIntent || Mathf.Abs(cornerEscapeAxis) > 0.1f))
            {
                ClearCombatActions(decision);
                decision.moveAxis = cornerEscapeAxis * Mathf.Lerp(0.55f, 1f, Mathf.Clamp01(intent.cornerEscapeBias));
                decision.dashPrimaryPressed = canDash && (cornerEscapeIntent || intent.dashBias >= 0.5f || semantics.horizontalDistance < preferredRange * 0.65f);
                decision.debugSummary = "AI CORNER ESCAPE";
                escapingCorner = true;
            }

            if (antiAirOpportunity
                && semantics.targetAbove
                && !semantics.targetInShootRange
                && self.arrows > 0
                && !prioritizeCollection
                && !escapingCorner
                && !effectiveDefensiveRetreatIntent
                && !semantics.incomingProjectileThreat
                && !semantics.targetUsingRanged
                && !semantics.targetUsingUltimate)
            {
                ClearCombatActions(decision);
                decision.moveAxis = towardTarget * Mathf.Lerp(0.65f, 1f, Mathf.Clamp01(intent.advanceBias));
                if (self.isGrounded && intent.jumpBias >= 0.22f)
                {
                    decision.jumpPressed = true;
                }

                decision.jumpHeld = self.isGrounded || semantics.verticalDistance > 96f;
                decision.dashPrimaryPressed = canDash && semantics.horizontalDistance > preferredRange * 1.35f && intent.dashBias >= 0.5f;
                decision.debugSummary = "AI ANTI AIR CHASE";
            }

            if (!semantics.incomingProjectileThreat && !semantics.targetUsingUltimate && !prioritizeCollection && !escapingCorner && !effectiveDefensiveRetreatIntent)
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

            if (antiAirOpportunity
                && semantics.targetAbove
                && semantics.targetInShootRange
                && canShoot
                && !prioritizeCollection
                && !escapingCorner
                && !effectiveDefensiveRetreatIntent
                && !semantics.incomingProjectileThreat
                && !decision.meleePressed
                && !decision.ultimatePressed
                && !decision.dashPrimaryPressed
                && !decision.dashSecondaryPressed)
            {
                decision.shootPressed = true;
                decision.shootHeld = true;
            }

            if (!prioritizeCollection && !escapingCorner && !effectiveDefensiveRetreatIntent && !decision.shootPressed && !decision.meleePressed && !decision.ultimatePressed && !decision.dashPrimaryPressed)
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

            if (!semantics.incomingProjectileThreat && !semantics.targetUsingUltimate && !effectiveDefensiveRetreatIntent && semantics.targetUsingMelee)
            {
                ClearCombatActions(decision);
                decision.moveAxis = awayFromTarget;
                decision.dashPrimaryPressed = canDash;
                decision.debugSummary = "AI EVADE MELEE";
            }

            if (!semantics.incomingProjectileThreat && !semantics.targetUsingUltimate && !effectiveDefensiveRetreatIntent && semantics.targetUsingRanged && !semantics.targetVulnerable)
            {
                ClearCombatActions(decision);
                if (canShoot && semantics.targetInShootRange)
                {
                    decision.moveAxis = Mathf.Clamp(0.2f * towardTarget, -1f, 1f);
                    decision.shootPressed = true;
                    decision.shootHeld = true;
                    decision.debugSummary = "AI RANGED INTERRUPT";
                }
                else
                {
                    decision.moveAxis = awayFromTarget;
                    decision.dashPrimaryPressed = canDash;
                    decision.debugSummary = "AI EVADE RANGED";
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
                            ApplyProjectileDashPreferredFallback(decision, semantics, self, canDash, awayFromTarget);
                            break;
                        case "jump":
                            ApplyProjectileJumpPreferredFallback(decision, semantics, self, canDash, awayFromTarget, intent.dashBias);
                            break;
                        case "hold":
                            ApplyProjectileHoldPreferredFallback(decision, semantics, self, canDash, awayFromTarget, intent.dashBias);
                            break;
                        case "parry_prefer":
                            ApplyProjectileParryPreferredFallback(decision, semantics, self, canDash, awayFromTarget);
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

        private static bool IsDefensiveRetreatIntent(CodexStrategyIntent intent)
        {
            if (intent == null || string.IsNullOrWhiteSpace(intent.reason))
            {
                return false;
            }

            string normalized = intent.reason.Trim().ToLowerInvariant();
            return normalized.Contains("missed_ultimate_escape")
                || normalized.Contains("missed_melee_escape")
                || normalized.Contains("missed_ranged_response")
                || normalized.Contains("target_ultimate_threat")
                || normalized.Contains("target_melee_threat")
                || normalized.Contains("target_ranged_threat")
                || normalized.Contains("missed_projectile_defense")
                || normalized.Contains("projectile_threat_feedback");
        }

        private static bool IsCornerEscapeIntent(CodexStrategyIntent intent)
        {
            if (intent == null || string.IsNullOrWhiteSpace(intent.reason))
            {
                return false;
            }

            string normalized = intent.reason.Trim().ToLowerInvariant();
            return normalized.Contains("missed_corner_escape")
                || normalized.Contains("corner_escape")
                || normalized.Contains("self_cornered");
        }

        private static bool IsProjectileThreatFeedbackIntent(CodexStrategyIntent intent)
        {
            if (intent == null || string.IsNullOrWhiteSpace(intent.reason))
            {
                return false;
            }

            return intent.reason.Trim().ToLowerInvariant().Contains("projectile_threat_feedback");
        }

        private static bool IsAntiAirIntent(CodexStrategyIntent intent)
        {
            if (intent == null)
            {
                return false;
            }

            if (intent.antiAir)
            {
                return true;
            }

            return !string.IsNullOrWhiteSpace(intent.reason)
                && intent.reason.Trim().ToLowerInvariant().Contains("anti_air");
        }

        private static float ResolveCornerEscapeAxis(
            AiArenaSnapshotEnvelope snapshot,
            AiArenaSemanticObservation semantics,
            AiArenaCombatantObservation self,
            float fallbackAxis)
        {
            if (snapshot != null && snapshot.arena != null && self != null)
            {
                float width = snapshot.arena.wrapXMax - snapshot.arena.wrapXMin;
                if (width > 0.01f)
                {
                    float centerX = (snapshot.arena.wrapXMin + snapshot.arena.wrapXMax) * 0.5f;
                    float delta = centerX - self.position.x;
                    if (Mathf.Abs(delta) > 0.1f)
                    {
                        return delta > 0f ? 1f : -1f;
                    }
                }
            }

            if (semantics != null && Mathf.Abs(semantics.targetDirection.x) > 0.1f)
            {
                return semantics.targetDirection.x > 0f ? 1f : -1f;
            }

            return fallbackAxis >= 0f ? 1f : -1f;
        }

        private static void ApplyProjectileDashPreferredFallback(
            AiArenaDecisionEnvelope decision,
            AiArenaSemanticObservation semantics,
            AiArenaCombatantObservation self,
            bool canDash,
            float awayFromTarget)
        {
            if (TryApplyProjectileDash(decision, semantics, canDash, awayFromTarget))
            {
                return;
            }

            if (TryApplyProjectileJump(decision, self, awayFromTarget))
            {
                return;
            }

            if (TryApplyProjectileParryHold(decision, self, semantics, requireTightTiming: true))
            {
                return;
            }

            ApplyProjectileDrift(decision, semantics, awayFromTarget);
        }

        private static void ApplyProjectileJumpPreferredFallback(
            AiArenaDecisionEnvelope decision,
            AiArenaSemanticObservation semantics,
            AiArenaCombatantObservation self,
            bool canDash,
            float awayFromTarget,
            float dashBias)
        {
            if (TryApplyProjectileJump(decision, self, awayFromTarget))
            {
                return;
            }

            if (TryApplyProjectileDash(decision, semantics, canDash && (semantics.shouldDashEvade || dashBias >= 0.5f), awayFromTarget))
            {
                return;
            }

            if (TryApplyProjectileParryHold(decision, self, semantics, requireTightTiming: true))
            {
                return;
            }

            ApplyProjectileDrift(decision, semantics, awayFromTarget);
        }

        private static void ApplyProjectileHoldPreferredFallback(
            AiArenaDecisionEnvelope decision,
            AiArenaSemanticObservation semantics,
            AiArenaCombatantObservation self,
            bool canDash,
            float awayFromTarget,
            float dashBias)
        {
            if (TryApplyProjectileParryHold(decision, self, semantics, requireTightTiming: true))
            {
                return;
            }

            if (TryApplyProjectileDash(decision, semantics, canDash && (semantics.shouldDashEvade || dashBias >= 0.5f), awayFromTarget))
            {
                return;
            }

            if (TryApplyProjectileJump(decision, self, awayFromTarget))
            {
                return;
            }

            ApplyProjectileDrift(decision, semantics, awayFromTarget);
        }

        private static void ApplyProjectileParryPreferredFallback(
            AiArenaDecisionEnvelope decision,
            AiArenaSemanticObservation semantics,
            AiArenaCombatantObservation self,
            bool canDash,
            float awayFromTarget)
        {
            if (TryApplyProjectileParryHold(decision, self, semantics, requireTightTiming: false))
            {
                return;
            }

            if (TryApplyProjectileDash(decision, semantics, canDash, awayFromTarget))
            {
                return;
            }

            if (TryApplyProjectileJump(decision, self, awayFromTarget))
            {
                return;
            }

            ApplyProjectileDrift(decision, semantics, awayFromTarget);
        }

        private static bool TryApplyProjectileDash(
            AiArenaDecisionEnvelope decision,
            AiArenaSemanticObservation semantics,
            bool canDash,
            float awayFromTarget)
        {
            if (!canDash)
            {
                return false;
            }

            ClearCombatActions(decision);
            decision.moveAxis = AiArenaHeuristicPolicy.ResolveIncomingProjectileDashAxis(semantics, awayFromTarget);
            decision.dashPrimaryPressed = true;
            decision.debugSummary = "AI PROJECTILE DASH";
            return true;
        }

        private static bool TryApplyProjectileJump(
            AiArenaDecisionEnvelope decision,
            AiArenaCombatantObservation self,
            float awayFromTarget)
        {
            if (self == null || !self.isGrounded)
            {
                return false;
            }

            ClearCombatActions(decision);
            decision.moveAxis = awayFromTarget * 0.35f;
            decision.jumpPressed = true;
            decision.jumpHeld = true;
            decision.debugSummary = "AI PROJECTILE JUMP";
            return true;
        }

        private static bool TryApplyProjectileParryHold(
            AiArenaDecisionEnvelope decision,
            AiArenaCombatantObservation self,
            AiArenaSemanticObservation semantics,
            bool requireTightTiming)
        {
            if (self == null || !self.canParryProjectile)
            {
                return false;
            }

            if (requireTightTiming && semantics.incomingProjectileTime > 0.18f)
            {
                return false;
            }

            decision.moveAxis = 0f;
            ClearCombatActions(decision);
            decision.debugSummary = "AI PARRY HOLD";
            return true;
        }

        private static void ApplyProjectileDrift(
            AiArenaDecisionEnvelope decision,
            AiArenaSemanticObservation semantics,
            float awayFromTarget)
        {
            ClearCombatActions(decision);
            decision.moveAxis = AiArenaHeuristicPolicy.ResolveIncomingProjectileDriftAxis(semantics, awayFromTarget) * 0.35f;
            decision.debugSummary = "AI PROJECTILE DRIFT";
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
