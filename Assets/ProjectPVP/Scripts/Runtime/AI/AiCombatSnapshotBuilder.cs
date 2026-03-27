using System;
using System.Collections.Generic;
using ProjectPVP.Gameplay;
using ProjectPVP.Input;
using ProjectPVP.Match;
using UnityEngine;

namespace ProjectPVP.AI
{
    public static class AiCombatSnapshotBuilder
    {
        public static AiCombatSnapshot Build(MatchController matchController, PlayerController self, int simulationFrame)
        {
            PlayerController opponent = ResolveOpponent(matchController, self);
            AiCombatSnapshot snapshot = new AiCombatSnapshot
            {
                matchId = ResolveMatchId(matchController),
                roundId = ResolveRoundId(matchController),
                simulationFrame = simulationFrame,
                fixedDeltaTime = Time.fixedDeltaTime,
                roundResetPending = matchController != null && matchController.IsRoundResetPending,
                playerOneWins = matchController != null ? matchController.PlayerOneWins : 0,
                playerTwoWins = matchController != null ? matchController.PlayerTwoWins : 0,
                self = BuildCombatantSnapshot(self),
                opponent = BuildCombatantSnapshot(opponent),
                arena = BuildArena(matchController),
            };

            snapshot.projectiles = BuildProjectiles(self);
            snapshot.features = BuildFeatures(snapshot);
            snapshot.recentEvents = BuildRecentEvents(snapshot);
            return snapshot;
        }

        private static string ResolveMatchId(MatchController matchController)
        {
            return matchController != null
                ? "match-" + matchController.GetInstanceID().ToString()
                : "match-standalone";
        }

        private static int ResolveRoundId(MatchController matchController)
        {
            if (matchController == null)
            {
                return 1;
            }

            return matchController.PlayerOneWins + matchController.PlayerTwoWins + 1;
        }

        private static AiArenaStateSnapshot BuildArena(MatchController matchController)
        {
            Rect bounds = matchController != null ? matchController.ActiveWrapBounds : new Rect(-1280f, -720f, 2560f, 1440f);
            return new AiArenaStateSnapshot
            {
                minX = bounds.xMin,
                minY = bounds.yMin,
                width = bounds.width,
                height = bounds.height,
            };
        }

        private static AiCombatantSnapshot BuildCombatantSnapshot(PlayerController player)
        {
            if (player == null)
            {
                return new AiCombatantSnapshot();
            }

            string characterName = player.characterDefinition != null && !string.IsNullOrWhiteSpace(player.characterDefinition.displayName)
                ? player.characterDefinition.displayName
                : player.gameObject.name;
            PlayerInputFrame frame = player.CurrentInputFrame;
            Vector2 aimHold = player.AimHoldDirection;

            return new AiCombatantSnapshot
            {
                slotIndex = player.SlotId.ToIndex(),
                slotName = player.SlotId.ToDisplayName(),
                characterName = characterName,
                positionX = player.RootPosition.x,
                positionY = player.RootPosition.y,
                velocityX = player.CurrentVelocity.x,
                velocityY = player.CurrentVelocity.y,
                facing = player.Facing,
                isGrounded = player.IsGrounded,
                isTouchingWall = player.IsTouchingWall,
                isDead = player.IsDead,
                isDashing = player.IsDashing,
                isMeleeActive = player.IsMeleeActive,
                isUltimateActive = player.IsUltimateActive,
                isHitStunned = player.IsHitStunned,
                isKnockedBack = player.IsKnockedBack,
                isAimHoldActive = player.IsAimHoldActive,
                currentArrows = player.CurrentArrows,
                aimHoldX = aimHold.x,
                aimHoldY = aimHold.y,
                inputAxis = frame.axis,
                inputAimX = frame.aim.x,
                inputAimY = frame.aim.y,
                dashParryTimeLeft = player.DashParryTimeLeft,
                dashPressTimeLeft = player.DashPressTimeLeft,
                dashPrimaryCooldownLeft = player.DashPrimaryCooldownLeft,
                dashSecondaryCooldownLeft = player.DashSecondaryCooldownLeft,
                shootCooldownLeft = player.ShootCooldownLeft,
                meleeCooldownLeft = player.MeleeCooldownLeft,
                ultimateCooldownLeft = player.UltimateCooldownLeft,
                meleeTimeLeft = player.MeleeTimeLeft,
                ultimateTimeLeft = player.UltimateTimeLeft,
                hitStunTimeLeft = player.HitStunTimeLeft,
                knockbackTimeLeft = player.KnockbackTimeLeft,
                meleeRangeWidth = player.MeleeHitboxSize.x,
                meleeRangeHeight = player.MeleeHitboxSize.y,
                ultimateRadius = player.UltimateHitboxRadius,
                actionKey = player.CurrentVisualActionKey,
            };
        }

        private static AiProjectileSnapshot[] BuildProjectiles(PlayerController self)
        {
            ProjectileController[] projectiles = UnityEngine.Object.FindObjectsByType<ProjectileController>(FindObjectsSortMode.None);
            if (projectiles == null || projectiles.Length == 0)
            {
                return Array.Empty<AiProjectileSnapshot>();
            }

            List<AiProjectileSnapshot> results = new List<AiProjectileSnapshot>(projectiles.Length);
            Vector2 selfPosition = self != null ? self.RootPosition : Vector2.zero;

            for (int index = 0; index < projectiles.Length; index += 1)
            {
                ProjectileController projectile = projectiles[index];
                if (projectile == null)
                {
                    continue;
                }

                PlayerController owner = projectile.SourceObject != null
                    ? projectile.SourceObject.GetComponent<PlayerController>()
                    : null;
                int ownerSlotIndex = owner != null ? owner.SlotId.ToIndex() : -1;
                Vector2 delta = (Vector2)projectile.transform.position - selfPosition;

                results.Add(new AiProjectileSnapshot
                {
                    positionX = projectile.transform.position.x,
                    positionY = projectile.transform.position.y,
                    velocityX = projectile.CurrentVelocity.x,
                    velocityY = projectile.CurrentVelocity.y,
                    ownerSlotIndex = ownerSlotIndex,
                    isStuck = projectile.IsStuck,
                    isCollectible = projectile.IsCollectible,
                    isDisarmed = projectile.IsDisarmed,
                    distanceToSelf = self != null ? delta.magnitude : 0f,
                    horizontalDistanceToSelf = self != null ? Mathf.Abs(delta.x) : 0f,
                    verticalDistanceToSelf = self != null ? Mathf.Abs(delta.y) : 0f,
                });
            }

            return results.ToArray();
        }

        private static AiCombatFeatureSnapshot BuildFeatures(AiCombatSnapshot snapshot)
        {
            AiCombatFeatureSnapshot features = new AiCombatFeatureSnapshot();
            if (snapshot == null || snapshot.self == null || snapshot.opponent == null)
            {
                return features;
            }

            float horizontalDistance = snapshot.opponent.positionX - snapshot.self.positionX;
            float verticalDistance = snapshot.opponent.positionY - snapshot.self.positionY;
            features.horizontalDistance = horizontalDistance;
            features.verticalDistance = verticalDistance;
            features.euclideanDistance = Mathf.Sqrt((horizontalDistance * horizontalDistance) + (verticalDistance * verticalDistance));
            features.opponentAbove = verticalDistance > 40f;
            features.meleeRangeNow =
                Mathf.Abs(horizontalDistance) <= Mathf.Max(48f, snapshot.self.meleeRangeWidth * 0.6f) &&
                Mathf.Abs(verticalDistance) <= Mathf.Max(40f, snapshot.self.meleeRangeHeight * 0.85f);
            features.shootLaneOpen = Mathf.Abs(verticalDistance) <= 120f;
            features.nearestHostileProjectileDistance = float.MaxValue;

            for (int index = 0; index < snapshot.projectiles.Length; index += 1)
            {
                AiProjectileSnapshot projectile = snapshot.projectiles[index];
                if (projectile == null || projectile.ownerSlotIndex == snapshot.self.slotIndex)
                {
                    continue;
                }

                features.hostileProjectileCount += 1;
                features.nearestHostileProjectileDistance = Mathf.Min(features.nearestHostileProjectileDistance, projectile.distanceToSelf);
                if (projectile.horizontalDistanceToSelf <= 220f && projectile.verticalDistanceToSelf <= 90f && !projectile.isCollectible)
                {
                    features.hostileProjectileThreat = true;
                }
            }

            if (features.nearestHostileProjectileDistance == float.MaxValue)
            {
                features.nearestHostileProjectileDistance = 0f;
            }

            return features;
        }

        private static string[] BuildRecentEvents(AiCombatSnapshot snapshot)
        {
            List<string> events = new List<string>(4);
            if (snapshot == null)
            {
                return events.ToArray();
            }

            if (snapshot.roundResetPending)
            {
                events.Add("round_reset_pending");
            }

            if (snapshot.features != null && snapshot.features.hostileProjectileThreat)
            {
                events.Add("projectile_collision_risk");
            }

            if (snapshot.opponent != null && snapshot.opponent.isDashing)
            {
                events.Add("opponent_started_dash");
            }

            if (snapshot.opponent != null && snapshot.opponent.isUltimateActive)
            {
                events.Add("opponent_started_ultimate");
            }

            return events.ToArray();
        }

        private static PlayerController ResolveOpponent(MatchController matchController, PlayerController self)
        {
            if (self == null)
            {
                return null;
            }

            if (matchController != null)
            {
                foreach (PlayerController player in matchController.EnumerateControllers())
                {
                    if (player != null && player != self)
                    {
                        return player;
                    }
                }
            }

            PlayerController[] players = UnityEngine.Object.FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
            for (int index = 0; index < players.Length; index += 1)
            {
                if (players[index] != null && players[index] != self)
                {
                    return players[index];
                }
            }

            return null;
        }
    }
}
