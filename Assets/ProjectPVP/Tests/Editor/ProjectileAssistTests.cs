using System.Reflection;
using NUnit.Framework;
using ProjectPVP.Data;
using ProjectPVP.Gameplay;
using UnityEngine;

namespace ProjectPVP.Tests.Editor
{
    public sealed class ProjectileAssistTests
    {
        private static readonly MethodInfo ResolveProjectileAssistTargetMethod =
            typeof(PlayerController).GetMethod("ResolveProjectileAssistTarget", BindingFlags.Instance | BindingFlags.NonPublic);
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
            PlayerController shooter = shooterGo.AddComponent<PlayerController>();
            PlayerController validTarget = validTargetGo.AddComponent<PlayerController>();
            PlayerController outOfConeTarget = outOfConeTargetGo.AddComponent<PlayerController>();
            CharacterDefinition definition = ScriptableObject.CreateInstance<CharacterDefinition>();

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
    }
}
