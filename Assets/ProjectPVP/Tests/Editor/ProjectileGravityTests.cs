using System.Reflection;
using NUnit.Framework;
using ProjectPVP.Data;
using ProjectPVP.Gameplay;
using UnityEngine;

namespace ProjectPVP.Tests.Editor
{
    public sealed class ProjectileGravityTests
    {
        private static readonly FieldInfo GravityDelayRatioRuntimeField =
            typeof(ProjectileController).GetField("_gravityDelayRatioRuntime", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo GravityRampRatioRuntimeField =
            typeof(ProjectileController).GetField("_gravityRampRatioRuntime", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo GravityMinScaleRuntimeField =
            typeof(ProjectileController).GetField("_gravityMinScaleRuntime", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo GravityMaxScaleRuntimeField =
            typeof(ProjectileController).GetField("_gravityMaxScaleRuntime", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo UpwardGravityMultiplierRuntimeField =
            typeof(ProjectileController).GetField("_projectileUpwardGravityMultiplierRuntime", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo UpwardSpeedDecayMultiplierRuntimeField =
            typeof(ProjectileController).GetField("_projectileUpwardSpeedDecayMultiplierRuntime", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo ProjectileMinSpeedRuntimeField =
            typeof(ProjectileController).GetField("_projectileMinSpeedRuntime", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo ProjectileSpeedDecayRuntimeField =
            typeof(ProjectileController).GetField("_projectileSpeedDecayRuntime", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo LifetimeLeftField =
            typeof(ProjectileController).GetField("_lifetimeLeft", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo VelocityField =
            typeof(ProjectileController).GetField("_velocity", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly MethodInfo ResolveGravityScaleMethod =
            typeof(ProjectileController).GetMethod("ResolveGravityScale", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly MethodInfo ApplySpeedDecayMethod =
            typeof(ProjectileController).GetMethod("ApplySpeedDecay", BindingFlags.Instance | BindingFlags.NonPublic);

        [Test]
        public void ApplyDefinition_CopiesProjectileGravityTuning()
        {
            Assert.That(GravityDelayRatioRuntimeField, Is.Not.Null);
            Assert.That(GravityRampRatioRuntimeField, Is.Not.Null);
            Assert.That(GravityMinScaleRuntimeField, Is.Not.Null);
            Assert.That(GravityMaxScaleRuntimeField, Is.Not.Null);
            Assert.That(UpwardGravityMultiplierRuntimeField, Is.Not.Null);
            Assert.That(UpwardSpeedDecayMultiplierRuntimeField, Is.Not.Null);

            GameObject root = new GameObject("projectile_gravity_definition");
            ProjectileController controller = root.AddComponent<ProjectileController>();
            CharacterDefinition definition = ScriptableObject.CreateInstance<CharacterDefinition>();

            try
            {
                definition.projectileGravity = 1375f;
                definition.projectileGravityDelayRatio = 0.12f;
                definition.projectileGravityRampRatio = 0.33f;
                definition.projectileGravityMinScale = 0.55f;
                definition.projectileGravityMaxScale = 1.25f;
                definition.projectileUpwardGravityMultiplier = 1.45f;
                definition.projectileUpwardSpeedDecayMultiplier = 1.15f;

                controller.ApplyDefinition(definition);

                Assert.That(controller.gravity, Is.EqualTo(1375f));
                Assert.That((float)GravityDelayRatioRuntimeField.GetValue(controller), Is.EqualTo(0.12f).Within(0.0001f));
                Assert.That((float)GravityRampRatioRuntimeField.GetValue(controller), Is.EqualTo(0.33f).Within(0.0001f));
                Assert.That((float)GravityMinScaleRuntimeField.GetValue(controller), Is.EqualTo(0.55f).Within(0.0001f));
                Assert.That((float)GravityMaxScaleRuntimeField.GetValue(controller), Is.EqualTo(1.25f).Within(0.0001f));
                Assert.That((float)UpwardGravityMultiplierRuntimeField.GetValue(controller), Is.EqualTo(1.45f).Within(0.0001f));
                Assert.That((float)UpwardSpeedDecayMultiplierRuntimeField.GetValue(controller), Is.EqualTo(1.15f).Within(0.0001f));
            }
            finally
            {
                Object.DestroyImmediate(definition);
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ApplyDefinition_CopiesProjectileSpeedTuning()
        {
            Assert.That(ProjectileMinSpeedRuntimeField, Is.Not.Null);
            Assert.That(ProjectileSpeedDecayRuntimeField, Is.Not.Null);

            GameObject root = new GameObject("projectile_speed_definition");
            ProjectileController controller = root.AddComponent<ProjectileController>();
            CharacterDefinition definition = ScriptableObject.CreateInstance<CharacterDefinition>();

            try
            {
                definition.projectileMinSpeed = 615f;
                definition.projectileSpeedDecay = 245f;
                definition.projectileUpwardSpeedDecayMultiplier = 1.5f;

                controller.ApplyDefinition(definition);

                Assert.That((float)ProjectileMinSpeedRuntimeField.GetValue(controller), Is.EqualTo(615f).Within(0.0001f));
                Assert.That((float)ProjectileSpeedDecayRuntimeField.GetValue(controller), Is.EqualTo(245f).Within(0.0001f));
                Assert.That((float)UpwardSpeedDecayMultiplierRuntimeField.GetValue(controller), Is.EqualTo(1.5f).Within(0.0001f));
            }
            finally
            {
                Object.DestroyImmediate(definition);
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ResolveGravityScale_UsesDelayRampAndUpwardMultiplier()
        {
            Assert.That(ResolveGravityScaleMethod, Is.Not.Null);
            Assert.That(LifetimeLeftField, Is.Not.Null);
            Assert.That(VelocityField, Is.Not.Null);

            GameObject root = new GameObject("projectile_gravity_scale");
            ProjectileController controller = root.AddComponent<ProjectileController>();

            try
            {
                controller.maxLifetime = 10f;
                SetRuntimeGravity(controller, delayRatio: 0.25f, rampRatio: 0.5f, minScale: 0.4f, maxScale: 1f, upwardGravityMultiplier: 1.5f, upwardSpeedDecayMultiplier: 1.2f);

                LifetimeLeftField.SetValue(controller, 10f);
                VelocityField.SetValue(controller, new Vector2(0f, -40f));
                float delayedScale = (float)ResolveGravityScaleMethod.Invoke(controller, null);

                LifetimeLeftField.SetValue(controller, 5f);
                VelocityField.SetValue(controller, new Vector2(0f, -40f));
                float rampedScale = (float)ResolveGravityScaleMethod.Invoke(controller, null);

                VelocityField.SetValue(controller, new Vector2(0f, 40f));
                float upwardScale = (float)ResolveGravityScaleMethod.Invoke(controller, null);

                Assert.That(delayedScale, Is.EqualTo(0.4f).Within(0.0001f));
                Assert.That(rampedScale, Is.EqualTo(0.7f).Within(0.0001f));
                Assert.That(upwardScale, Is.EqualTo(0.7f * 1.5f).Within(0.0001f));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ApplySpeedDecay_UsesUpwardMultiplierAndMinSpeedFloor()
        {
            Assert.That(ApplySpeedDecayMethod, Is.Not.Null);
            Assert.That(ProjectileMinSpeedRuntimeField, Is.Not.Null);
            Assert.That(ProjectileSpeedDecayRuntimeField, Is.Not.Null);
            Assert.That(VelocityField, Is.Not.Null);

            GameObject root = new GameObject("projectile_speed_decay");
            ProjectileController controller = root.AddComponent<ProjectileController>();

            try
            {
                ProjectileMinSpeedRuntimeField.SetValue(controller, 50f);
                ProjectileSpeedDecayRuntimeField.SetValue(controller, 20f);
                UpwardSpeedDecayMultiplierRuntimeField.SetValue(controller, 1.5f);

                Vector2 downwardStart = new Vector2(100f, 0f);
                VelocityField.SetValue(controller, downwardStart);
                ApplySpeedDecayMethod.Invoke(controller, new object[] { 1f });
                Vector2 downwardVelocity = (Vector2)VelocityField.GetValue(controller);

                Vector2 upwardStart = new Vector2(100f, 1f);
                VelocityField.SetValue(controller, upwardStart);
                ApplySpeedDecayMethod.Invoke(controller, new object[] { 1f });
                Vector2 upwardVelocity = (Vector2)VelocityField.GetValue(controller);

                VelocityField.SetValue(controller, new Vector2(55f, 0f));
                ApplySpeedDecayMethod.Invoke(controller, new object[] { 1f });
                Vector2 flooredVelocity = (Vector2)VelocityField.GetValue(controller);

                float expectedDownwardSpeed = Mathf.Max(50f, downwardStart.magnitude - 20f);
                float expectedUpwardSpeed = Mathf.Max(50f, upwardStart.magnitude - (20f * 1.5f));

                Assert.That(downwardVelocity.magnitude, Is.EqualTo(expectedDownwardSpeed).Within(0.0001f));
                Assert.That(upwardVelocity.magnitude, Is.EqualTo(expectedUpwardSpeed).Within(0.0001f));
                Assert.That(flooredVelocity.magnitude, Is.EqualTo(50f).Within(0.0001f));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void SetRuntimeGravity(
            ProjectileController controller,
            float delayRatio,
            float rampRatio,
            float minScale,
            float maxScale,
            float upwardGravityMultiplier,
            float upwardSpeedDecayMultiplier)
        {
            GravityDelayRatioRuntimeField.SetValue(controller, delayRatio);
            GravityRampRatioRuntimeField.SetValue(controller, rampRatio);
            GravityMinScaleRuntimeField.SetValue(controller, minScale);
            GravityMaxScaleRuntimeField.SetValue(controller, maxScale);
            UpwardGravityMultiplierRuntimeField.SetValue(controller, upwardGravityMultiplier);
            UpwardSpeedDecayMultiplierRuntimeField.SetValue(controller, upwardSpeedDecayMultiplier);
        }
    }
}
