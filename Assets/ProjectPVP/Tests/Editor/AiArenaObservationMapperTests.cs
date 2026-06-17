using NUnit.Framework;
using ProjectPVP.Input;
using UnityEngine;

namespace ProjectPVP.Tests.Editor
{
    public sealed class AiArenaObservationMapperTests
    {
        [Test]
        public void ToObservation_CopiesControllerSnapshotFields()
        {
            var snapshot = new AiArenaControllerSnapshot
            {
                isValid = true,
                slotId = 2,
                botId = "bot-rouge",
                botDisplayName = "Rouge",
                characterId = "archer",
                displayName = "Player Two",
                actionKey = "shoot",
                isDead = true,
                isGrounded = true,
                isTouchingWall = true,
                isDashing = true,
                isMeleeActive = true,
                isShootAnimating = true,
                isUltimateActive = true,
                isHitStunned = true,
                canParryProjectile = true,
                canBlockProjectiles = true,
                arrows = 3,
                facing = -1,
                projectileInheritVelocityFactor = 0.45f,
                projectileBaseSpeed = 1234f,
                shootCooldownLeft = 0.1f,
                meleeCooldownLeft = 0.2f,
                dashCooldownLeft = 0.3f,
                ultimateCooldownLeft = 0.4f,
                hitStunTimeLeft = 0.5f,
                position = new Vector2(10f, 20f),
                velocity = new Vector2(-30f, 40f),
                meleeHitboxCenter = new Vector2(1f, 2f),
                meleeHitboxSize = new Vector2(3f, 4f),
                ultimateHitboxCenter = new Vector2(5f, 6f),
                ultimateHitboxRadius = 7f,
            };

            AiArenaCombatantObservation observation = AiArenaObservationMapper.ToObservation(snapshot);

            Assert.That(observation.slotId, Is.EqualTo(2));
            Assert.That(observation.botId, Is.EqualTo("bot-rouge"));
            Assert.That(observation.botDisplayName, Is.EqualTo("Rouge"));
            Assert.That(observation.characterId, Is.EqualTo("archer"));
            Assert.That(observation.displayName, Is.EqualTo("Player Two"));
            Assert.That(observation.actionKey, Is.EqualTo("shoot"));
            Assert.That(observation.isDead, Is.True);
            Assert.That(observation.isGrounded, Is.True);
            Assert.That(observation.isTouchingWall, Is.True);
            Assert.That(observation.isDashing, Is.True);
            Assert.That(observation.isMeleeActive, Is.True);
            Assert.That(observation.isShootAnimating, Is.True);
            Assert.That(observation.isUltimateActive, Is.True);
            Assert.That(observation.isHitStunned, Is.True);
            Assert.That(observation.canParryProjectile, Is.True);
            Assert.That(observation.canBlockProjectiles, Is.True);
            Assert.That(observation.arrows, Is.EqualTo(3));
            Assert.That(observation.facing, Is.EqualTo(-1));
            Assert.That(observation.projectileInheritVelocityFactor, Is.EqualTo(0.45f).Within(0.001f));
            Assert.That(observation.projectileBaseSpeed, Is.EqualTo(1234f).Within(0.001f));
            Assert.That(observation.shootCooldownLeft, Is.EqualTo(0.1f).Within(0.001f));
            Assert.That(observation.meleeCooldownLeft, Is.EqualTo(0.2f).Within(0.001f));
            Assert.That(observation.dashCooldownLeft, Is.EqualTo(0.3f).Within(0.001f));
            Assert.That(observation.ultimateCooldownLeft, Is.EqualTo(0.4f).Within(0.001f));
            Assert.That(observation.hitStunTimeLeft, Is.EqualTo(0.5f).Within(0.001f));
            Assert.That(observation.position, Is.EqualTo(new Vector2(10f, 20f)));
            Assert.That(observation.velocity, Is.EqualTo(new Vector2(-30f, 40f)));
            Assert.That(observation.meleeHitboxCenter, Is.EqualTo(new Vector2(1f, 2f)));
            Assert.That(observation.meleeHitboxSize, Is.EqualTo(new Vector2(3f, 4f)));
            Assert.That(observation.ultimateHitboxCenter, Is.EqualTo(new Vector2(5f, 6f)));
            Assert.That(observation.ultimateHitboxRadius, Is.EqualTo(7f).Within(0.001f));
        }

        [Test]
        public void ToObservation_CopiesProjectileSnapshotFields()
        {
            var snapshot = new AiArenaProjectileSnapshot
            {
                isValid = true,
                sourceSlotId = 1,
                isStuck = true,
                isDisarmed = true,
                isCollectible = true,
                position = new Vector2(12f, 34f),
                velocity = new Vector2(-56f, 78f),
                travelDirection = new Vector2(-1f, 0.25f),
            };

            AiArenaProjectileObservation observation = AiArenaObservationMapper.ToObservation(snapshot);

            Assert.That(observation.sourceSlotId, Is.EqualTo(1));
            Assert.That(observation.isStuck, Is.True);
            Assert.That(observation.isDisarmed, Is.True);
            Assert.That(observation.isCollectible, Is.True);
            Assert.That(observation.position, Is.EqualTo(new Vector2(12f, 34f)));
            Assert.That(observation.velocity, Is.EqualTo(new Vector2(-56f, 78f)));
            Assert.That(observation.travelDirection, Is.EqualTo(new Vector2(-1f, 0.25f)));
        }

        [Test]
        public void ToObservation_CopiesArenaSnapshotFields()
        {
            var snapshot = new AiArenaArenaSnapshot
            {
                wrapBounds = new Rect(-320f, -180f, 640f, 360f),
                roundResetPending = true,
                roundsToChampion = 3,
                playerOneWins = 1,
                playerTwoWins = 2,
                currentRespawnSeedIndex = 4,
                currentRespawnSeedLabel = "center_split",
                pendingRoundWinnerSlot = 1,
                pendingChampionSlot = 2,
                championAnnouncementSlot = 2,
            };

            AiArenaArenaObservation observation = AiArenaObservationMapper.ToObservation(snapshot);

            Assert.That(observation.roundResetPending, Is.True);
            Assert.That(observation.roundsToChampion, Is.EqualTo(3));
            Assert.That(observation.playerOneWins, Is.EqualTo(1));
            Assert.That(observation.playerTwoWins, Is.EqualTo(2));
            Assert.That(observation.currentRespawnSeedIndex, Is.EqualTo(4));
            Assert.That(observation.currentRespawnSeedLabel, Is.EqualTo("center_split"));
            Assert.That(observation.pendingRoundWinnerSlot, Is.EqualTo(1));
            Assert.That(observation.pendingChampionSlot, Is.EqualTo(2));
            Assert.That(observation.championAnnouncementSlot, Is.EqualTo(2));
            Assert.That(observation.wrapXMin, Is.EqualTo(-320f).Within(0.001f));
            Assert.That(observation.wrapXMax, Is.EqualTo(320f).Within(0.001f));
            Assert.That(observation.wrapYMin, Is.EqualTo(-180f).Within(0.001f));
            Assert.That(observation.wrapYMax, Is.EqualTo(180f).Within(0.001f));
        }

        [Test]
        public void ToObservation_UsesEmptyRespawnSeedLabelWhenArenaLabelIsMissing()
        {
            var snapshot = new AiArenaArenaSnapshot
            {
                currentRespawnSeedLabel = null,
            };

            AiArenaArenaObservation observation = AiArenaObservationMapper.ToObservation(snapshot);

            Assert.That(observation.currentRespawnSeedLabel, Is.EqualTo(string.Empty));
        }
    }
}
