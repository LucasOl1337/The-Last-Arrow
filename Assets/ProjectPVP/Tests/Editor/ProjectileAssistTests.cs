using System.Reflection;
using NUnit.Framework;
using ProjectPVP.Data;
using ProjectPVP.Gameplay;
using UnityEngine;

namespace ProjectPVP.Tests.Editor
{
    public sealed class ProjectileAssistTests
    {
        private static readonly MethodInfo AwakeMethod =
            typeof(PlayerController).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly MethodInfo ResolveProjectileAssistTargetMethod =
            typeof(PlayerController).GetMethod("ResolveProjectileAssistTarget", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo ContextField =
            typeof(PlayerController).GetField("_context", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly MethodInfo ComputeAssistAppliedStrengthMethod =
            typeof(ProjectileController).GetMethod("ComputeAssistAppliedStrength", BindingFlags.Static | BindingFlags.NonPublic);
        private static readonly MethodInfo RotateDirectionTowardsTargetMethod =
            typeof(ProjectileController).GetMethod("RotateDirectionTowardsTarget", BindingFlags.Static | BindingFlags.NonPublic);

        [Test]
        public void ResolveProjectileAssistTarget_RequiresTargetInsideConeAndRange()
        {
            Assert.That(ResolveProjectileAssistTargetMethod, Is.Not.Null);

            GameObject shooterGo = new GameObject("shooter");
            GameObject validTargetGo = new GameObject("valid_target");
            GameObject outOfConeTargetGo = new GameObject("out_of_cone_target");
            CharacterDefinition definition = ScriptableObject.CreateInstance<CharacterDefinition>();
            PlayerController shooter = CreatePlayer(shooterGo, 1, definition);
            PlayerController validTarget = CreatePlayer(validTargetGo, 2, null);
            PlayerController outOfConeTarget = CreatePlayer(outOfConeTargetGo, 3, null);

            try
            {
                shooter.transform.position = Vector3.zero;
                validTarget.transform.position = new Vector3(8f, 1f, 0f);
                outOfConeTarget.transform.position = new Vector3(2f, 7f, 0f);

                definition.projectileAssistEnabled = true;
                definition.projectileAssistAcquireConeDeg = 15f;
                definition.projectileAssistMinDistance = 1f;
                definition.projectileAssistMaxRange = 20f;
                shooter.characterDefinition = definition;

                InvokeAwake(shooter);
                InvokeAwake(validTarget);
                InvokeAwake(outOfConeTarget);

                object resolved = ResolveProjectileAssistTargetMethod.Invoke(
                    shooter,
                    new object[] { Vector2.zero, Vector2.right });

                Assert.That(resolved, Is.EqualTo(validTarget.transform));
            }
            finally
            {
                Object.DestroyImmediate(definition);
                Object.DestroyImmediate(shooterGo);
                Object.DestroyImmediate(validTargetGo);
                Object.DestroyImmediate(outOfConeTargetGo);
            }
        }

        [Test]
        public void ResolveProjectileAssistTarget_IgnoresDodgingTargets()
        {
            Assert.That(ResolveProjectileAssistTargetMethod, Is.Not.Null);

            GameObject shooterGo = new GameObject("shooter_dodge_filter");
            GameObject dodgingTargetGo = new GameObject("dodging_target");
            GameObject validTargetGo = new GameObject("valid_target_farther");
            CharacterDefinition definition = ScriptableObject.CreateInstance<CharacterDefinition>();
            PlayerController shooter = CreatePlayer(shooterGo, 1, definition);
            PlayerController dodgingTarget = CreatePlayer(dodgingTargetGo, 2, null);
            PlayerController validTarget = CreatePlayer(validTargetGo, 3, null);

            try
            {
                shooter.transform.position = Vector3.zero;
                dodgingTarget.transform.position = new Vector3(6f, 0f, 0f);
                validTarget.transform.position = new Vector3(12f, 0f, 0f);

                definition.projectileAssistEnabled = true;
                definition.projectileAssistAcquireConeDeg = 30f;
                definition.projectileAssistMinDistance = 1f;
                definition.projectileAssistMaxRange = 40f;
                shooter.characterDefinition = definition;

                InvokeAwake(shooter);
                InvokeAwake(dodgingTarget);
                InvokeAwake(validTarget);

                PlayerContext dodgingContext = ContextField.GetValue(dodgingTarget) as PlayerContext;
                Assert.That(dodgingContext, Is.Not.Null);
                dodgingContext.dashParryTimer = 0.2f;

                object resolved = ResolveProjectileAssistTargetMethod.Invoke(
                    shooter,
                    new object[] { Vector2.zero, Vector2.right });

                Assert.That(resolved, Is.EqualTo(validTarget.transform));
            }
            finally
            {
                Object.DestroyImmediate(definition);
                Object.DestroyImmediate(shooterGo);
                Object.DestroyImmediate(dodgingTargetGo);
                Object.DestroyImmediate(validTargetGo);
            }
        }

        [Test]
        public void RotateDirectionTowardsTarget_RespectsMaxAngularStep()
        {
            Assert.That(RotateDirectionTowardsTargetMethod, Is.Not.Null);

            const float maxStepDeg = 10f;
            Vector2 currentDirection = Vector2.right;
            Vector2 desiredDirection = Vector2.up;
            float maxStepRadians = Mathf.Deg2Rad * maxStepDeg;

            Vector2 rotated = (Vector2)RotateDirectionTowardsTargetMethod.Invoke(
                null,
                new object[] { currentDirection, desiredDirection, maxStepRadians });

            float angleDelta = Vector2.Angle(currentDirection, rotated);
            Assert.That(angleDelta, Is.LessThanOrEqualTo(maxStepDeg + 0.001f));
        }

        [Test]
        public void ComputeAssistAppliedStrength_DropsToZeroByMaxRange()
        {
            Assert.That(ComputeAssistAppliedStrengthMethod, Is.Not.Null);

            float baseStrength = 0.18f;
            float maxRange = 1000f;
            float dropoffStartRatio = 0.6f;

            float beforeDropoff = (float)ComputeAssistAppliedStrengthMethod.Invoke(
                null,
                new object[] { baseStrength, 500f, maxRange, dropoffStartRatio });
            float inDropoff = (float)ComputeAssistAppliedStrengthMethod.Invoke(
                null,
                new object[] { baseStrength, 800f, maxRange, dropoffStartRatio });
            float atMaxRange = (float)ComputeAssistAppliedStrengthMethod.Invoke(
                null,
                new object[] { baseStrength, 1000f, maxRange, dropoffStartRatio });

            Assert.That(beforeDropoff, Is.EqualTo(baseStrength).Within(0.0001f));
            Assert.That(inDropoff, Is.GreaterThan(0f).And.LessThan(baseStrength));
            Assert.That(atMaxRange, Is.EqualTo(0f).Within(0.0001f));
        }

        [Test]
        public void ProjectileTrajectoryMath_UsesFullInheritedVelocityVector()
        {
            Vector2 origin = Vector2.zero;
            Vector2 target = new Vector2(400f, 120f);
            Vector2 launchVelocityWithLift = new Vector2(1600f, 300f);
            Vector2 launchVelocityFlat = new Vector2(1600f, 0f);

            float estimatedTime = ProjectileTrajectoryMath.ResolveEstimatedFlightTime(origin, target, launchVelocityWithLift);
            Vector2 evaluatedPositionWithLift = ProjectileTrajectoryMath.EvaluatePosition(origin, launchVelocityWithLift, 1500f, estimatedTime);
            Vector2 evaluatedPositionFlat = ProjectileTrajectoryMath.EvaluatePosition(origin, launchVelocityFlat, 1500f, estimatedTime);

            Assert.That(estimatedTime, Is.GreaterThan(0f));
            Assert.That(evaluatedPositionWithLift.x, Is.GreaterThan(0f));
            Assert.That(evaluatedPositionWithLift.y, Is.GreaterThan(evaluatedPositionFlat.y));
            Assert.That(evaluatedPositionWithLift.y, Is.Not.EqualTo(evaluatedPositionFlat.y));
        }

        [Test]
        public void TryResolvePreferredTravelDirection_AccountsForInheritedVelocity()
        {
            Vector2 origin = Vector2.zero;
            Vector2 target = new Vector2(320f, 12f);

            bool zeroResult = ProjectileTrajectoryMath.TryResolvePreferredTravelDirection(
                origin,
                target,
                baseSpeed: 1600f,
                gravity: 1500f,
                inheritedVelocity: Vector2.zero,
                groundMask: default,
                out Vector2 zeroLiftDirection);

            bool liftResult = ProjectileTrajectoryMath.TryResolvePreferredTravelDirection(
                origin,
                target,
                baseSpeed: 1600f,
                gravity: 1500f,
                inheritedVelocity: new Vector2(0f, 700f),
                groundMask: default,
                out Vector2 inheritedLiftDirection);

            Assert.That(zeroResult, Is.True);
            Assert.That(liftResult, Is.True);
            Assert.That(Vector2.Angle(zeroLiftDirection, inheritedLiftDirection), Is.GreaterThan(0.1f));
            Assert.That(inheritedLiftDirection.y, Is.GreaterThan(zeroLiftDirection.y));
        }

        private static PlayerController CreatePlayer(GameObject root, int slotId, CharacterDefinition definition)
        {
            Rigidbody2D body = root.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            BoxCollider2D collider = root.AddComponent<BoxCollider2D>();
            PlayerController controller = root.AddComponent<PlayerController>();
            controller.slotId = slotId;
            controller.body = body;
            controller.bodyCollider = collider;
            controller.characterDefinition = definition;
            return controller;
        }

        private static void InvokeAwake(PlayerController controller)
        {
            Assert.That(AwakeMethod, Is.Not.Null);
            AwakeMethod.Invoke(controller, null);
        }
    }
}
