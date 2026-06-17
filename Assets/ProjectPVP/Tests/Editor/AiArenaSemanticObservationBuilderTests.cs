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
        public void Build_DoesNotMarkCombatantsCorneredWhenArenaBoundsAreMissing()
        {
            AiArenaControllerSnapshot self = BuildSelf(Vector2.zero);
            self.arrows = 3;

            AiArenaControllerSnapshot target = BuildTarget(new Vector2(160f, 0f));

            var projectiles = new List<AiArenaProjectileSnapshot>
            {
                new AiArenaProjectileSnapshot
                {
                    isValid = true,
                    isCollectible = true,
                    position = new Vector2(-120f, 0f),
                },
            };

            AiArenaSemanticObservation semantics = AiArenaSemanticObservationBuilder.Build(
                self,
                target,
                projectiles,
                default,
                desiredCombatDistance: 360f,
                closeRetreatDistance: 80f,
                meleeRange: 120f,
                ultimateRange: 180f,
                shootRange: 960f,
                verticalTolerance: 240f);

            Assert.That(semantics.selfCornered, Is.False);
            Assert.That(semantics.targetCornered, Is.False);
            Assert.That(semantics.shouldCollectProjectile, Is.False);
        }

        [Test]
        public void Build_DoesNotMarkAntiAirWhenTargetIsAboveShootTolerance()
        {
            AiArenaControllerSnapshot self = BuildSelf(Vector2.zero);
            self.arrows = 3;

            AiArenaControllerSnapshot target = BuildTarget(new Vector2(100f, 420f));
            target.velocity = Vector2.zero;

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

            Assert.That(semantics.targetAbove, Is.True);
            Assert.That(semantics.targetInShootRange, Is.False);
            Assert.That(semantics.shouldAntiAir, Is.False);
        }

        [Test]
        public void Build_MarksShootAnimationInRangeAsRangedThreat()
        {
            AiArenaControllerSnapshot self = BuildSelf(Vector2.zero);
            self.arrows = 3;

            AiArenaControllerSnapshot target = BuildTarget(new Vector2(420f, 0f));
            target.isShootAnimating = true;

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

            Assert.That(semantics.targetInShootRange, Is.True);
            Assert.That(semantics.targetUsingRanged, Is.True);
            Assert.That(semantics.targetVulnerable, Is.True);
        }

        [Test]
        public void Build_DoesNotMarkShootAnimationOutsideShootRangeAsRangedThreat()
        {
            AiArenaControllerSnapshot self = BuildSelf(Vector2.zero);
            self.arrows = 3;

            AiArenaControllerSnapshot target = BuildTarget(new Vector2(1180f, 0f));
            target.isShootAnimating = true;

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

            Assert.That(semantics.targetInShootRange, Is.False);
            Assert.That(semantics.targetUsingRanged, Is.False);
            Assert.That(semantics.shouldPunish, Is.False);
        }

        [Test]
        public void Build_CompensatesPredictedTargetDirectionForSelfMomentum()
        {
            AiArenaControllerSnapshot selfStationary = BuildSelf(Vector2.zero);
            selfStationary.arrows = 3;

            AiArenaControllerSnapshot selfMovingRight = BuildSelf(Vector2.zero);
            selfMovingRight.arrows = 3;
            selfMovingRight.velocity = new Vector2(400f, 0f);

            AiArenaControllerSnapshot target = BuildTarget(new Vector2(200f, 100f));
            target.velocity = Vector2.zero;

            AiArenaSemanticObservation stationarySemantics = AiArenaSemanticObservationBuilder.Build(
                selfStationary,
                target,
                null,
                BuildArena(),
                desiredCombatDistance: 360f,
                closeRetreatDistance: 80f,
                meleeRange: 120f,
                ultimateRange: 180f,
                shootRange: 960f,
                verticalTolerance: 240f);

            AiArenaSemanticObservation movingSemantics = AiArenaSemanticObservationBuilder.Build(
                selfMovingRight,
                target,
                null,
                BuildArena(),
                desiredCombatDistance: 360f,
                closeRetreatDistance: 80f,
                meleeRange: 120f,
                ultimateRange: 180f,
                shootRange: 960f,
                verticalTolerance: 240f);

            Assert.That(movingSemantics.predictedTargetDirection.x, Is.LessThan(stationarySemantics.predictedTargetDirection.x));
            Assert.That(movingSemantics.predictedTargetDirection.y, Is.GreaterThan(stationarySemantics.predictedTargetDirection.y));
        }

        [Test]
        public void Build_UsesProjectileInheritVelocityFactorWhenLeadingShots()
        {
            AiArenaControllerSnapshot selfFullCarry = BuildSelf(Vector2.zero);
            selfFullCarry.arrows = 3;
            selfFullCarry.velocity = new Vector2(400f, 0f);
            selfFullCarry.projectileInheritVelocityFactor = 1f;

            AiArenaControllerSnapshot selfPartialCarry = BuildSelf(Vector2.zero);
            selfPartialCarry.arrows = 3;
            selfPartialCarry.velocity = new Vector2(400f, 0f);
            selfPartialCarry.projectileInheritVelocityFactor = 0.25f;

            AiArenaControllerSnapshot target = BuildTarget(new Vector2(200f, 100f));

            AiArenaSemanticObservation fullCarrySemantics = AiArenaSemanticObservationBuilder.Build(
                selfFullCarry,
                target,
                null,
                BuildArena(),
                desiredCombatDistance: 360f,
                closeRetreatDistance: 80f,
                meleeRange: 120f,
                ultimateRange: 180f,
                shootRange: 960f,
                verticalTolerance: 240f);

            AiArenaSemanticObservation partialCarrySemantics = AiArenaSemanticObservationBuilder.Build(
                selfPartialCarry,
                target,
                null,
                BuildArena(),
                desiredCombatDistance: 360f,
                closeRetreatDistance: 80f,
                meleeRange: 120f,
                ultimateRange: 180f,
                shootRange: 960f,
                verticalTolerance: 240f);

            Assert.That(fullCarrySemantics.predictedTargetDirection.x, Is.LessThan(partialCarrySemantics.predictedTargetDirection.x));
            Assert.That(fullCarrySemantics.predictedTargetDirection.y, Is.GreaterThan(partialCarrySemantics.predictedTargetDirection.y));
        }

        [Test]
        public void Build_UsesProjectileBaseSpeedWhenLeadingMomentumShots()
        {
            AiArenaControllerSnapshot fastShot = BuildSelf(Vector2.zero);
            fastShot.arrows = 3;
            fastShot.velocity = new Vector2(400f, 0f);
            fastShot.projectileInheritVelocityFactor = 1f;
            fastShot.projectileBaseSpeed = 1600f;

            AiArenaControllerSnapshot slowShot = BuildSelf(Vector2.zero);
            slowShot.arrows = 3;
            slowShot.velocity = new Vector2(400f, 0f);
            slowShot.projectileInheritVelocityFactor = 1f;
            slowShot.projectileBaseSpeed = 800f;

            AiArenaControllerSnapshot target = BuildTarget(new Vector2(200f, 100f));

            AiArenaSemanticObservation fastSemantics = AiArenaSemanticObservationBuilder.Build(
                fastShot,
                target,
                null,
                BuildArena(),
                desiredCombatDistance: 360f,
                closeRetreatDistance: 80f,
                meleeRange: 120f,
                ultimateRange: 180f,
                shootRange: 960f,
                verticalTolerance: 240f);

            AiArenaSemanticObservation slowSemantics = AiArenaSemanticObservationBuilder.Build(
                slowShot,
                target,
                null,
                BuildArena(),
                desiredCombatDistance: 360f,
                closeRetreatDistance: 80f,
                meleeRange: 120f,
                ultimateRange: 180f,
                shootRange: 960f,
                verticalTolerance: 240f);

            Assert.That(slowSemantics.predictedTargetDirection.x, Is.LessThan(fastSemantics.predictedTargetDirection.x));
            Assert.That(slowSemantics.predictedTargetDirection.y, Is.GreaterThan(fastSemantics.predictedTargetDirection.y));
        }

        [Test]
        public void Build_CompensatesPredictedTargetDirectionForProjectileGravity()
        {
            AiArenaControllerSnapshot flatShot = BuildSelf(Vector2.zero);
            flatShot.arrows = 3;
            flatShot.projectileBaseSpeed = 1600f;
            flatShot.projectileGravity = 1f;

            AiArenaControllerSnapshot arcingShot = BuildSelf(Vector2.zero);
            arcingShot.arrows = 3;
            arcingShot.projectileBaseSpeed = 1600f;
            arcingShot.projectileGravity = 1500f;

            AiArenaControllerSnapshot target = BuildTarget(new Vector2(420f, 0f));

            AiArenaSemanticObservation flatSemantics = AiArenaSemanticObservationBuilder.Build(
                flatShot,
                target,
                null,
                BuildArena(),
                desiredCombatDistance: 360f,
                closeRetreatDistance: 80f,
                meleeRange: 120f,
                ultimateRange: 180f,
                shootRange: 960f,
                verticalTolerance: 240f);

            AiArenaSemanticObservation arcSemantics = AiArenaSemanticObservationBuilder.Build(
                arcingShot,
                target,
                null,
                BuildArena(),
                desiredCombatDistance: 360f,
                closeRetreatDistance: 80f,
                meleeRange: 120f,
                ultimateRange: 180f,
                shootRange: 960f,
                verticalTolerance: 240f);

            Assert.That(flatSemantics.predictedTargetDirection.y, Is.EqualTo(0f).Within(0.01f));
            Assert.That(arcSemantics.predictedTargetDirection.y, Is.GreaterThan(flatSemantics.predictedTargetDirection.y + 0.04f));
            Assert.That(arcSemantics.predictedTargetDirection.x, Is.GreaterThan(0f));
        }

        [Test]
        public void Build_MarksVisibleMidRangeTargetsAsPressureInsteadOfParkingInZone()
        {
            AiArenaControllerSnapshot self = BuildSelf(Vector2.zero);
            self.arrows = 3;

            AiArenaControllerSnapshot target = BuildTarget(new Vector2(320f, 0f));

            AiArenaSemanticObservation semantics = AiArenaSemanticObservationBuilder.Build(
                self,
                target,
                null,
                new AiArenaArenaSnapshot
                {
                    wrapBounds = new Rect(-1000f, -200f, 2000f, 400f),
                },
                desiredCombatDistance: 360f,
                closeRetreatDistance: 80f,
                meleeRange: 120f,
                ultimateRange: 180f,
                shootRange: 960f,
                verticalTolerance: 240f);

            Assert.That(semantics.hasTarget, Is.True);
            Assert.That(semantics.targetInShootRange, Is.True);
            Assert.That(semantics.shouldPressure, Is.True);
            Assert.That(semantics.shouldZone, Is.True);
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

        [Test]
        public void Build_IgnoresOwnFlyingProjectileAsIncomingThreat()
        {
            AiArenaControllerSnapshot self = BuildSelf(Vector2.zero);
            self.isGrounded = true;
            self.slotId = 1;

            var projectiles = new List<AiArenaProjectileSnapshot>
            {
                new AiArenaProjectileSnapshot
                {
                    isValid = true,
                    sourceSlotId = 1,
                    position = new Vector2(-20f, 0f),
                    velocity = new Vector2(100f, 0f),
                    travelDirection = Vector2.right,
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
            Assert.That(semantics.incomingProjectileThreat, Is.False);
            Assert.That(semantics.shouldDashEvade, Is.False);
            Assert.That(semantics.shouldJumpEvade, Is.False);
        }

        [Test]
        public void Build_TreatsCollectibleProjectileAsRecoveryInsteadOfThreat()
        {
            AiArenaControllerSnapshot self = BuildSelf(Vector2.zero);
            self.isGrounded = true;
            self.arrows = 0;

            var projectiles = new List<AiArenaProjectileSnapshot>
            {
                new AiArenaProjectileSnapshot
                {
                    isValid = true,
                    sourceSlotId = 2,
                    isCollectible = true,
                    position = new Vector2(-20f, 0f),
                    velocity = new Vector2(100f, 0f),
                    travelDirection = Vector2.right,
                },
            };

            AiArenaSemanticObservation semantics = AiArenaSemanticObservationBuilder.Build(
                self,
                BuildTarget(new Vector2(220f, 0f)),
                projectiles,
                BuildArena(),
                desiredCombatDistance: 360f,
                closeRetreatDistance: 140f,
                meleeRange: 120f,
                ultimateRange: 180f,
                shootRange: 960f,
                verticalTolerance: 240f);

            Assert.That(semantics.incomingProjectileThreat, Is.False);
            Assert.That(semantics.shouldDashEvade, Is.False);
            Assert.That(semantics.shouldJumpEvade, Is.False);
            Assert.That(semantics.hasCollectibleProjectile, Is.True);
            Assert.That(semantics.shouldCollectProjectile, Is.True);
        }

        [Test]
        public void Build_AccountsForSelfVelocityWhenEstimatingProjectileThreat()
        {
            AiArenaControllerSnapshot selfStationary = BuildSelf(Vector2.zero);
            AiArenaControllerSnapshot selfMovingTowardProjectile = BuildSelf(Vector2.zero);
            selfMovingTowardProjectile.velocity = new Vector2(-50f, 0f);

            var projectiles = new List<AiArenaProjectileSnapshot>
            {
                new AiArenaProjectileSnapshot
                {
                    isValid = true,
                    sourceSlotId = 2,
                    position = new Vector2(-20f, 0f),
                    velocity = new Vector2(100f, 0f),
                    travelDirection = Vector2.right,
                },
            };

            AiArenaSemanticObservation stationarySemantics = AiArenaSemanticObservationBuilder.Build(
                selfStationary,
                default,
                projectiles,
                BuildArena(),
                desiredCombatDistance: 360f,
                closeRetreatDistance: 140f,
                meleeRange: 120f,
                ultimateRange: 180f,
                shootRange: 960f,
                verticalTolerance: 240f);

            AiArenaSemanticObservation movingSemantics = AiArenaSemanticObservationBuilder.Build(
                selfMovingTowardProjectile,
                default,
                projectiles,
                BuildArena(),
                desiredCombatDistance: 360f,
                closeRetreatDistance: 140f,
                meleeRange: 120f,
                ultimateRange: 180f,
                shootRange: 960f,
                verticalTolerance: 240f);

            Assert.That(movingSemantics.incomingProjectileThreat, Is.True);
            Assert.That(movingSemantics.incomingProjectileTime, Is.LessThan(stationarySemantics.incomingProjectileTime));
        }

        [Test]
        public void Build_RejectsVerticalProjectilePassingOutsideLateralTolerance()
        {
            AiArenaControllerSnapshot self = BuildSelf(Vector2.zero);
            self.isGrounded = true;

            var projectiles = new List<AiArenaProjectileSnapshot>
            {
                new AiArenaProjectileSnapshot
                {
                    isValid = true,
                    sourceSlotId = 2,
                    position = new Vector2(200f, -120f),
                    velocity = new Vector2(0f, 120f),
                    travelDirection = Vector2.up,
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

            Assert.That(semantics.incomingProjectileThreat, Is.False);
            Assert.That(semantics.shouldDashEvade, Is.False);
            Assert.That(semantics.shouldJumpEvade, Is.False);
        }

        [Test]
        public void Build_TracksNearestCollectibleProjectileForRecovery()
        {
            AiArenaControllerSnapshot self = BuildSelf(Vector2.zero);
            self.arrows = 0;

            var projectiles = new List<AiArenaProjectileSnapshot>
            {
                new AiArenaProjectileSnapshot
                {
                    isValid = true,
                    isCollectible = true,
                    position = new Vector2(140f, 0f),
                },
                new AiArenaProjectileSnapshot
                {
                    isValid = true,
                    isCollectible = true,
                    position = new Vector2(72f, 24f),
                },
                new AiArenaProjectileSnapshot
                {
                    isValid = true,
                    isCollectible = false,
                    position = new Vector2(30f, 0f),
                },
            };

            AiArenaSemanticObservation semantics = AiArenaSemanticObservationBuilder.Build(
                self,
                BuildTarget(new Vector2(200f, 0f)),
                projectiles,
                BuildArena(),
                desiredCombatDistance: 360f,
                closeRetreatDistance: 140f,
                meleeRange: 120f,
                ultimateRange: 180f,
                shootRange: 960f,
                verticalTolerance: 240f);

            Assert.That(semantics.hasCollectibleProjectile, Is.True);
            Assert.That(semantics.collectibleProjectileDistance, Is.EqualTo(Mathf.Sqrt((72f * 72f) + (24f * 24f))).Within(0.001f));
            Assert.That(Vector2.Distance(semantics.collectibleProjectileDirection, new Vector2(72f, 24f).normalized), Is.LessThan(0.001f));
            Assert.That(semantics.shouldCollectProjectile, Is.True);
        }

        [Test]
        public void Build_IgnoresOutOfBoundsCollectibleProjectilesForRecovery()
        {
            AiArenaControllerSnapshot self = BuildSelf(Vector2.zero);
            self.arrows = 0;

            var projectiles = new List<AiArenaProjectileSnapshot>
            {
                new AiArenaProjectileSnapshot
                {
                    isValid = true,
                    isCollectible = true,
                    position = new Vector2(-460f, 0f),
                },
                new AiArenaProjectileSnapshot
                {
                    isValid = true,
                    isCollectible = true,
                    position = new Vector2(180f, 0f),
                },
            };

            AiArenaSemanticObservation semantics = AiArenaSemanticObservationBuilder.Build(
                self,
                BuildTarget(new Vector2(240f, 0f)),
                projectiles,
                BuildArena(),
                desiredCombatDistance: 360f,
                closeRetreatDistance: 140f,
                meleeRange: 120f,
                ultimateRange: 180f,
                shootRange: 960f,
                verticalTolerance: 240f);

            Assert.That(semantics.hasCollectibleProjectile, Is.True);
            Assert.That(semantics.collectibleProjectileDistance, Is.EqualTo(180f).Within(0.001f));
            Assert.That(semantics.collectibleProjectileDirection, Is.EqualTo(Vector2.right));
            Assert.That(semantics.shouldCollectProjectile, Is.True);
        }

        [Test]
        public void Build_AllowsCollectibleProjectileOnArenaEdgeForRecovery()
        {
            AiArenaControllerSnapshot self = BuildSelf(new Vector2(320f, 0f));
            self.arrows = 0;

            var projectiles = new List<AiArenaProjectileSnapshot>
            {
                new AiArenaProjectileSnapshot
                {
                    isValid = true,
                    isCollectible = true,
                    position = new Vector2(400f, 0f),
                },
            };

            AiArenaSemanticObservation semantics = AiArenaSemanticObservationBuilder.Build(
                self,
                BuildTarget(new Vector2(240f, 0f)),
                projectiles,
                BuildArena(),
                desiredCombatDistance: 360f,
                closeRetreatDistance: 140f,
                meleeRange: 120f,
                ultimateRange: 180f,
                shootRange: 960f,
                verticalTolerance: 240f);

            Assert.That(semantics.hasCollectibleProjectile, Is.True);
            Assert.That(semantics.collectibleProjectileDistance, Is.EqualTo(80f).Within(0.001f));
            Assert.That(semantics.collectibleProjectileDirection, Is.EqualTo(Vector2.right));
            Assert.That(semantics.shouldCollectProjectile, Is.True);
        }

        [Test]
        public void EstimateTimeToClosestApproach_ReachesIncomingProjectileAndRejectsRetreatingOne()
        {
            float incoming = AiArenaProjectileThreatMath.EstimateTimeToClosestApproach(
                selfPosition: Vector2.zero,
                selfVelocity: Vector2.zero,
                projectilePosition: new Vector2(-120f, 0f),
                projectileVelocity: new Vector2(120f, 0f));

            float retreating = AiArenaProjectileThreatMath.EstimateTimeToClosestApproach(
                selfPosition: Vector2.zero,
                selfVelocity: Vector2.zero,
                projectilePosition: new Vector2(-120f, 0f),
                projectileVelocity: new Vector2(-120f, 0f));

            Assert.That(incoming, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(retreating, Is.EqualTo(-1f));
        }

        private static AiArenaControllerSnapshot BuildSelf(Vector2 position)
        {
            return new AiArenaControllerSnapshot
            {
                isValid = true,
                slotId = 1,
                facing = 1,
                isGrounded = true,
                projectileInheritVelocityFactor = 1f,
                projectileBaseSpeed = 1600f,
                projectileGravity = 1500f,
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
