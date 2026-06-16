using System.Collections.Generic;
using NUnit.Framework;
using ProjectPVP.Input;
using UnityEngine;

namespace ProjectPVP.Tests.Editor
{
    public sealed class AiArenaSemanticObservationBuilderTests
    {
        [Test]
        public void Build_ReturnsNoTargetSemanticsWhenTargetIsInvalid()
        {
            AiArenaControllerSnapshot self = BuildSelf(new Vector2(24f, 12f));
            self.facing = -1;
            self.arrows = 2;

            AiArenaSemanticObservation semantics = AiArenaSemanticObservationBuilder.Build(
                self,
                default,
                null,
                BuildArena(),
                desiredCombatDistance: 360f,
                closeRetreatDistance: 140f,
                meleeRange: 120f,
                ultimateRange: 180f,
                shootRange: 960f,
                verticalTolerance: 240f);

            Assert.That(semantics.hasTarget, Is.False);
            Assert.That(semantics.selfHasArrows, Is.True);
            Assert.That(semantics.predictedTargetDirection, Is.EqualTo(Vector2.left));
            Assert.That(semantics.incomingProjectileThreat, Is.False);
        }

        [Test]
        public void Build_ComputesTargetRangePressureAndPunishSemantics()
        {
            AiArenaControllerSnapshot self = BuildSelf(Vector2.zero);
            self.arrows = 3;

            AiArenaControllerSnapshot target = BuildTarget(new Vector2(100f, 40f));
            target.isHitStunned = true;

            AiArenaSemanticObservation semantics = AiArenaSemanticObservationBuilder.Build(
                self,
                target,
                null,
                BuildArena(),
                desiredCombatDistance: 360f,
                closeRetreatDistance: 80f,
                meleeRange: 120f,
                ultimateRange: 180f,
                shootRange: 960f,
                verticalTolerance: 240f);

            Assert.That(semantics.hasTarget, Is.True);
            Assert.That(semantics.targetSlotId, Is.EqualTo(2));
            Assert.That(semantics.horizontalDistance, Is.EqualTo(100f).Within(0.001f));
            Assert.That(semantics.verticalDistance, Is.EqualTo(40f).Within(0.001f));
            Assert.That(Vector2.Distance(semantics.targetDirection, new Vector2(100f, 40f).normalized), Is.LessThan(0.001f));
            Assert.That(semantics.targetAbove, Is.True);
            Assert.That(semantics.targetInMeleeRange, Is.True);
            Assert.That(semantics.targetInUltimateRange, Is.True);
            Assert.That(semantics.targetInShootRange, Is.True);
            Assert.That(semantics.shouldPressure, Is.True);
            Assert.That(semantics.shouldPunish, Is.True);
            Assert.That(semantics.shouldAntiAir, Is.True);
            Assert.That(semantics.targetVulnerable, Is.True);
            Assert.That(semantics.selfCornered, Is.False);
            Assert.That(semantics.targetCornered, Is.False);
        }

        [Test]
        public void Build_SelectsClosestIncomingProjectileThreat()
        {
            AiArenaControllerSnapshot self = BuildSelf(Vector2.zero);
            self.isGrounded = true;

            var projectiles = new List<AiArenaProjectileSnapshot>
            {
                new AiArenaProjectileSnapshot
                {
                    isValid = true,
                    sourceSlotId = 2,
                    position = new Vector2(-100f, 0f),
                    velocity = new Vector2(100f, 0f),
                    travelDirection = Vector2.left,
                },
                new AiArenaProjectileSnapshot
                {
                    isValid = true,
                    sourceSlotId = 2,
                    position = new Vector2(-5f, 0f),
                    velocity = new Vector2(-100f, 0f),
                    travelDirection = Vector2.left,
                },
                new AiArenaProjectileSnapshot
                {
                    isValid = true,
                    sourceSlotId = 2,
                    position = new Vector2(-10f, 0f),
                    velocity = new Vector2(100f, 0f),
                    travelDirection = Vector2.zero,
                },
            };

            AiArenaSemanticObservation semantics = AiArenaSemanticObservationBuilder.Build(
                self,
                default,
                projectiles,
                BuildArena(),
                desiredCombatDistance: 360f,
                closeRetreatDistance: 140f,
                meleeRange: 120f,
                ultimateRange: 180f,
                shootRange: 960f,
                verticalTolerance: 240f);

            Assert.That(semantics.hasTarget, Is.False);
            Assert.That(semantics.incomingProjectileThreat, Is.True);
            Assert.That(semantics.incomingProjectileTime, Is.EqualTo(0.1f).Within(0.001f));
            Assert.That(semantics.incomingProjectileDirection, Is.EqualTo(Vector2.right));
            Assert.That(semantics.shouldDashEvade, Is.True);
            Assert.That(semantics.shouldJumpEvade, Is.False);
        }

        private static AiArenaControllerSnapshot BuildSelf(Vector2 position)
        {
            return new AiArenaControllerSnapshot
            {
                isValid = true,
                slotId = 1,
                facing = 1,
                isGrounded = true,
                position = position,
            };
        }

        private static AiArenaControllerSnapshot BuildTarget(Vector2 position)
        {
            return new AiArenaControllerSnapshot
            {
                isValid = true,
                slotId = 2,
                position = position,
            };
        }

        private static AiArenaArenaSnapshot BuildArena()
        {
            return new AiArenaArenaSnapshot
            {
                wrapBounds = new Rect(-400f, -200f, 800f, 400f),
            };
        }
    }
}
