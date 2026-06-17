using System.Collections.Generic;
using UnityEngine;

namespace ProjectPVP.Input
{
    internal static class CodexPromptStateBuilder
    {
        private const float DangerousProjectileLateralTolerance = 140f;

        internal static CodexPromptState Build(
            AiArenaSnapshotEnvelope snapshot,
            AiArenaSnapshotEnvelope previousSnapshot,
            int fallbackFrame,
            IEnumerable<string> memoryHistory)
        {
            var promptState = new CodexPromptState
            {
                frame = snapshot != null ? snapshot.frame : fallbackFrame,
                botId = snapshot != null && snapshot.self != null ? snapshot.self.botId : string.Empty,
                botDisplayName = snapshot != null && snapshot.self != null ? snapshot.self.botDisplayName : string.Empty,
                self = BuildPromptCombatant(snapshot != null ? snapshot.self : null),
                target = BuildPromptCombatant(snapshot != null && snapshot.opponents != null && snapshot.opponents.Count > 0 ? snapshot.opponents[0] : null),
                arena = BuildPromptArena(snapshot),
            };

            AppendEvents(snapshot, previousSnapshot, promptState.events);

            if (memoryHistory != null)
            {
                foreach (string memory in memoryHistory)
                {
                    promptState.memory.Add(memory);
                }
            }

            if (snapshot != null && snapshot.projectiles != null)
            {
                for (int index = 0; index < snapshot.projectiles.Count; index += 1)
                {
                    AiArenaProjectileObservation projectile = snapshot.projectiles[index];
                    if (projectile == null)
                    {
                        continue;
                    }

                    if (projectile.isCollectible)
                    {
                        if (!IsInsidePlayableBounds(projectile.position, snapshot.arena))
                        {
                            continue;
                        }

                        float distance = EstimateProjectileDistance(snapshot.self, projectile);
                        if (distance >= 0f)
                        {
                            promptState.recoverableProjectiles.Add(new CodexPromptProjectileRecovery
                            {
                                sourceSlotId = projectile.sourceSlotId,
                                distanceToSelf = distance,
                                position = projectile.position,
                            });
                        }

                        continue;
                    }

                    if (projectile.sourceSlotId > 0
                        && snapshot.self != null
                        && projectile.sourceSlotId == snapshot.self.slotId)
                    {
                        continue;
                    }

                    if (projectile.isStuck || projectile.isDisarmed)
                    {
                        continue;
                    }

                    float etaSeconds = EstimateProjectileEta(snapshot.self, projectile);
                    if (etaSeconds < 0f || etaSeconds > 0.5f)
                    {
                        continue;
                    }

                    promptState.dangerousProjectiles.Add(new CodexPromptProjectileThreat
                    {
                        sourceSlotId = projectile.sourceSlotId,
                        etaSeconds = etaSeconds,
                        position = projectile.position,
                        travelDirection = projectile.travelDirection,
                    });
                }
            }

            return promptState;
        }

        private static void AppendEvents(
            AiArenaSnapshotEnvelope snapshot,
            AiArenaSnapshotEnvelope previousSnapshot,
            List<string> eventSink)
        {
            if (snapshot == null || snapshot.semantics == null || snapshot.arena == null)
            {
                return;
            }

            if (previousSnapshot == null || previousSnapshot.semantics == null || previousSnapshot.arena == null)
            {
                AddEvent(eventSink, "round_context_initialized");
                return;
            }

            AiArenaSemanticObservation previous = previousSnapshot.semantics;
            AiArenaSemanticObservation current = snapshot.semantics;
            if (previousSnapshot.arena.roundResetPending != snapshot.arena.roundResetPending && snapshot.arena.roundResetPending)
            {
                AddEvent(eventSink, "round_reset_started");
            }

            if (current.incomingProjectileThreat != previous.incomingProjectileThreat)
            {
                AddEvent(eventSink, current.incomingProjectileThreat ? "projectile_threat_spiked" : "projectile_threat_cleared");
            }

            if (current.targetUsingUltimate != previous.targetUsingUltimate)
            {
                AddEvent(eventSink, current.targetUsingUltimate ? "target_started_ultimate" : "target_stopped_ultimate");
            }

            if (current.targetUsingMelee != previous.targetUsingMelee)
            {
                AddEvent(eventSink, current.targetUsingMelee ? "target_started_melee" : "target_stopped_melee");
            }

            if (current.targetUsingRanged != previous.targetUsingRanged)
            {
                AddEvent(eventSink, current.targetUsingRanged ? "target_started_ranged" : "target_stopped_ranged");
            }

            if (current.selfCornered != previous.selfCornered)
            {
                AddEvent(eventSink, current.selfCornered ? "self_cornered" : "self_escaped_corner");
            }

            if (current.targetCornered != previous.targetCornered)
            {
                AddEvent(eventSink, current.targetCornered ? "target_cornered" : "target_left_corner");
            }

            if (current.targetVulnerable && !previous.targetVulnerable)
            {
                AddEvent(eventSink, "target_became_vulnerable");
            }

            if (!current.hasTarget && previous.hasTarget)
            {
                AddEvent(eventSink, "target_lost");
            }
        }

        private static void AddEvent(List<string> eventSink, string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            eventSink.Add(value);
        }

        private static CodexPromptCombatant BuildPromptCombatant(AiArenaCombatantObservation source)
        {
            if (source == null)
            {
                return new CodexPromptCombatant();
            }

            return new CodexPromptCombatant
            {
                slotId = source.slotId,
                botId = source.botId,
                botDisplayName = source.botDisplayName,
                displayName = source.displayName,
                actionKey = source.actionKey,
                isDead = source.isDead,
                isGrounded = source.isGrounded,
                isDashing = source.isDashing,
                isMeleeActive = source.isMeleeActive,
                isUltimateActive = source.isUltimateActive,
                isHitStunned = source.isHitStunned,
                canParryProjectile = source.canParryProjectile,
                canBlockProjectiles = source.canBlockProjectiles,
                arrows = source.arrows,
                facing = source.facing,
                projectileInheritVelocityFactor = source.projectileInheritVelocityFactor,
                projectileBaseSpeed = source.projectileBaseSpeed > 0f ? source.projectileBaseSpeed : 1600f,
                projectileGravity = source.projectileGravity > 0f ? source.projectileGravity : 1500f,
                shootCooldownLeft = source.shootCooldownLeft,
                meleeCooldownLeft = source.meleeCooldownLeft,
                dashCooldownLeft = source.dashCooldownLeft,
                ultimateCooldownLeft = source.ultimateCooldownLeft,
                hitStunTimeLeft = source.hitStunTimeLeft,
                position = source.position,
                velocity = source.velocity,
            };
        }

        private static CodexPromptArena BuildPromptArena(AiArenaSnapshotEnvelope snapshot)
        {
            AiArenaSemanticObservation semantics = snapshot != null ? snapshot.semantics : null;
            AiArenaArenaObservation arena = snapshot != null ? snapshot.arena : null;
            return new CodexPromptArena
            {
                roundResetPending = arena != null && arena.roundResetPending,
                roundsToChampion = arena != null ? arena.roundsToChampion : 1,
                playerOneWins = arena != null ? arena.playerOneWins : 0,
                playerTwoWins = arena != null ? arena.playerTwoWins : 0,
                currentRespawnSeedIndex = arena != null ? arena.currentRespawnSeedIndex : 0,
                currentRespawnSeedLabel = arena != null ? arena.currentRespawnSeedLabel : string.Empty,
                pendingRoundWinnerSlot = arena != null ? arena.pendingRoundWinnerSlot : 0,
                pendingChampionSlot = arena != null ? arena.pendingChampionSlot : 0,
                championAnnouncementSlot = arena != null ? arena.championAnnouncementSlot : 0,
                selfCornered = semantics != null && semantics.selfCornered,
                targetCornered = semantics != null && semantics.targetCornered,
                horizontalDistance = semantics != null ? semantics.horizontalDistance : 0f,
                verticalDistance = semantics != null ? semantics.verticalDistance : 0f,
                targetInMeleeRange = semantics != null && semantics.targetInMeleeRange,
                targetInUltimateRange = semantics != null && semantics.targetInUltimateRange,
                targetInShootRange = semantics != null && semantics.targetInShootRange,
                targetAbove = semantics != null && semantics.targetAbove,
                targetBelow = semantics != null && semantics.targetBelow,
            };
        }

        private static float EstimateProjectileEta(AiArenaCombatantObservation self, AiArenaProjectileObservation projectile)
        {
            if (self == null || projectile == null)
            {
                return -1f;
            }

            if (!AiArenaProjectileThreatMath.TryEstimateClosestApproach(
                self.position,
                self.velocity,
                projectile.position,
                projectile.velocity,
                out float etaSeconds,
                out Vector2 closestOffset))
            {
                return -1f;
            }

            return closestOffset.magnitude <= DangerousProjectileLateralTolerance ? etaSeconds : -1f;
        }

        private static bool IsInsidePlayableBounds(Vector2 position, AiArenaArenaObservation arena)
        {
            if (arena == null || arena.wrapXMax <= arena.wrapXMin || arena.wrapYMax <= arena.wrapYMin)
            {
                return true;
            }

            return position.x >= arena.wrapXMin
                && position.x <= arena.wrapXMax
                && position.y >= arena.wrapYMin
                && position.y <= arena.wrapYMax;
        }

        private static float EstimateProjectileDistance(AiArenaCombatantObservation self, AiArenaProjectileObservation projectile)
        {
            if (self == null || projectile == null)
            {
                return -1f;
            }

            return Vector2.Distance(self.position, projectile.position);
        }
    }
}
