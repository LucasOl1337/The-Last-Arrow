using System.Reflection;
using NUnit.Framework;
using ProjectPVP.Data;
using ProjectPVP.Gameplay;
using ProjectPVP.Input;
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
        private static readonly FieldInfo DistanceTravelledField =
            typeof(ProjectileController).GetField("_distanceTravelled", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo VelocityField =
            typeof(ProjectileController).GetField("_velocity", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo LaunchDirectionField =
            typeof(ProjectileController).GetField("_launchDirection", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo LaunchedField =
            typeof(ProjectileController).GetField("_launched", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo SourceObjectField =
            typeof(ProjectileController).GetField("_sourceObject", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo IsStuckField =
            typeof(ProjectileController).GetField("_isStuck", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo HitColliderField =
            typeof(ProjectileController).GetField("hitCollider", BindingFlags.Instance | BindingFlags.Public);
        private static readonly MethodInfo ResolveGravityScaleMethod =
            typeof(ProjectileController).GetMethod("ResolveGravityScale", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly MethodInfo ApplySpeedDecayMethod =
            typeof(ProjectileController).GetMethod("ApplySpeedDecay", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly MethodInfo IsOpposingProjectileMethod =
            typeof(ProjectileController).GetMethod("IsOpposingProjectile", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly MethodInfo DisarmIntoDropMethod =
            typeof(ProjectileController).GetMethod("DisarmIntoDrop", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly MethodInfo OnDisableMethod =
            typeof(ProjectileController).GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic);

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

        [Test]
        public void Launch_InheritsFullVelocityVectorWhenFactorIsOne()
        {
            GameObject root = new GameObject("projectile_inherit_velocity");
            ProjectileController controller = root.AddComponent<ProjectileController>();

            try
            {
                controller.baseSpeed = 1600f;

                controller.Launch(
                    sourceObject: null,
                    origin: Vector2.zero,
                    direction: Vector2.right,
                    assistTarget: null,
                    launchAssistEnabled: false,
                    launchAssistStrength: 0f,
                    launchAssistMaxTurnRateDeg: 0f,
                    launchAssistAcquireConeDeg: 0f,
                    launchAssistMaxRange: 0f,
                    launchAssistMinDistance: 0f,
                    launchAssistDropoffStartRatio: 0f,
                    inheritedVelocity: new Vector2(220f, 80f),
                    inheritFactor: 1f,
                    overrideSprite: null);

                Assert.That(controller.CurrentVelocity.x, Is.EqualTo(1820f).Within(0.001f));
                Assert.That(controller.CurrentVelocity.y, Is.EqualTo(80f).Within(0.001f));
                Assert.That(controller.CurrentVelocity.magnitude, Is.EqualTo(new Vector2(1820f, 80f).magnitude).Within(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void Launch_InheritsPerpendicularMomentumWhenFactorIsOne()
        {
            GameObject root = new GameObject("projectile_inherit_perpendicular");
            ProjectileController controller = root.AddComponent<ProjectileController>();

            try
            {
                controller.baseSpeed = 1600f;

                controller.Launch(
                    sourceObject: null,
                    origin: Vector2.zero,
                    direction: Vector2.right,
                    assistTarget: null,
                    launchAssistEnabled: false,
                    launchAssistStrength: 0f,
                    launchAssistMaxTurnRateDeg: 0f,
                    launchAssistAcquireConeDeg: 0f,
                    launchAssistMaxRange: 0f,
                    launchAssistMinDistance: 0f,
                    launchAssistDropoffStartRatio: 0f,
                    inheritedVelocity: new Vector2(0f, 160f),
                    inheritFactor: 1f,
                    overrideSprite: null);

                Assert.That(controller.CurrentVelocity.x, Is.EqualTo(1600f).Within(0.001f));
                Assert.That(controller.CurrentVelocity.y, Is.EqualTo(160f).Within(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ReflectFromParry_RestartsLifetimeAndRangeBudget()
        {
            Assert.That(LifetimeLeftField, Is.Not.Null);
            Assert.That(DistanceTravelledField, Is.Not.Null);

            GameObject root = new GameObject("projectile_parry_budget");
            GameObject parrySource = new GameObject("projectile_parry_budget_source");
            ProjectileController controller = root.AddComponent<ProjectileController>();

            try
            {
                controller.baseSpeed = 1600f;
                controller.maxLifetime = 2f;
                controller.maxRange = 100f;
                controller.Launch(
                    sourceObject: null,
                    origin: Vector2.zero,
                    direction: Vector2.right,
                    assistTarget: null,
                    launchAssistEnabled: false,
                    launchAssistStrength: 0f,
                    launchAssistMaxTurnRateDeg: 0f,
                    launchAssistAcquireConeDeg: 0f,
                    launchAssistMaxRange: 0f,
                    launchAssistMinDistance: 0f,
                    launchAssistDropoffStartRatio: 0f,
                    inheritedVelocity: Vector2.zero,
                    inheritFactor: 0f,
                    overrideSprite: null);
                LifetimeLeftField.SetValue(controller, 0.03f);
                DistanceTravelledField.SetValue(controller, 99f);

                controller.ReflectFromParry(parrySource);

                Assert.That((float)LifetimeLeftField.GetValue(controller), Is.EqualTo(2f).Within(0.0001f));
                Assert.That((float)DistanceTravelledField.GetValue(controller), Is.Zero);
                Assert.That(controller.SourceObject, Is.SameAs(parrySource));
                Assert.That(controller.CurrentVelocity.x, Is.LessThan(0f));
            }
            finally
            {
                Object.DestroyImmediate(parrySource);
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void BuildAiArenaProjectileSnapshot_DoesNotMarkFlyingArrowAsCollectible()
        {
            GameObject root = new GameObject("projectile_collectible_state");
            ProjectileController controller = root.AddComponent<ProjectileController>();

            try
            {
                controller.Launch(
                    sourceObject: null,
                    origin: Vector2.zero,
                    direction: Vector2.right,
                    assistTarget: null,
                    launchAssistEnabled: false,
                    launchAssistStrength: 0f,
                    launchAssistMaxTurnRateDeg: 0f,
                    launchAssistAcquireConeDeg: 0f,
                    launchAssistMaxRange: 0f,
                    launchAssistMinDistance: 0f,
                    launchAssistDropoffStartRatio: 0f,
                    inheritedVelocity: Vector2.zero,
                    inheritFactor: 0f,
                    overrideSprite: null);

                AiArenaProjectileSnapshot snapshot = controller.BuildAiArenaProjectileSnapshot();

                Assert.That(snapshot.isValid, Is.True);
                Assert.That(snapshot.isStuck, Is.False);
                Assert.That(snapshot.isDisarmed, Is.False);
                Assert.That(snapshot.isCollectible, Is.False);
                Assert.That(controller.IsCollectible, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void SeverByMelee_MakesProjectileDisarmedAndCollectible()
        {
            GameObject root = new GameObject("projectile_sever_melee");
            ProjectileController controller = root.AddComponent<ProjectileController>();

            try
            {
                controller.Launch(
                    sourceObject: root,
                    origin: Vector2.zero,
                    direction: Vector2.right,
                    assistTarget: null,
                    launchAssistEnabled: false,
                    launchAssistStrength: 0f,
                    launchAssistMaxTurnRateDeg: 0f,
                    launchAssistAcquireConeDeg: 0f,
                    launchAssistMaxRange: 0f,
                    launchAssistMinDistance: 0f,
                    launchAssistDropoffStartRatio: 0f,
                    inheritedVelocity: Vector2.zero,
                    inheritFactor: 0f,
                    overrideSprite: null);

                controller.SeverByMelee();

                Assert.That(controller.IsDisarmed, Is.True);
                Assert.That(controller.IsStuck, Is.False);
                Assert.That(controller.IsCollectible, Is.True);
                Assert.That(controller.SourceObject, Is.Null);
                Assert.That(controller.CurrentVelocity.y, Is.LessThanOrEqualTo(-120f));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void DisarmIntoDrop_ClearsSourceObjectAndMarksProjectileCollectible()
        {
            Assert.That(DisarmIntoDropMethod, Is.Not.Null);
            Assert.That(SourceObjectField, Is.Not.Null);

            GameObject root = new GameObject("projectile_disarm_drop");
            ProjectileController controller = root.AddComponent<ProjectileController>();
            GameObject source = new GameObject("projectile_disarm_source");

            try
            {
                controller.Launch(
                    sourceObject: source,
                    origin: Vector2.zero,
                    direction: Vector2.right,
                    assistTarget: null,
                    launchAssistEnabled: false,
                    launchAssistStrength: 0f,
                    launchAssistMaxTurnRateDeg: 0f,
                    launchAssistAcquireConeDeg: 0f,
                    launchAssistMaxRange: 0f,
                    launchAssistMinDistance: 0f,
                    launchAssistDropoffStartRatio: 0f,
                    inheritedVelocity: Vector2.zero,
                    inheritFactor: 0f,
                    overrideSprite: null);

                DisarmIntoDropMethod.Invoke(controller, null);

                Assert.That((bool)IsStuckField.GetValue(controller), Is.False);
                Assert.That(controller.IsDisarmed, Is.True);
                Assert.That(controller.IsCollectible, Is.True);
                Assert.That(SourceObjectField.GetValue(controller), Is.Null);
                Assert.That(controller.CurrentVelocity.y, Is.EqualTo(-40f).Within(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(source);
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void OnDisable_ClearsRuntimeProjectileState()
        {
            Assert.That(LaunchedField, Is.Not.Null);
            Assert.That(SourceObjectField, Is.Not.Null);
            Assert.That(IsStuckField, Is.Not.Null);
            Assert.That(HitColliderField, Is.Not.Null);
            Assert.That(OnDisableMethod, Is.Not.Null);

            GameObject root = new GameObject("projectile_disable_state");
            ProjectileController controller = root.AddComponent<ProjectileController>();
            GameObject source = new GameObject("projectile_disable_state_source");

            try
            {
                controller.baseSpeed = 1600f;
                controller.Launch(
                    sourceObject: source,
                    origin: Vector2.zero,
                    direction: Vector2.right,
                    assistTarget: null,
                    launchAssistEnabled: true,
                    launchAssistStrength: 0.5f,
                    launchAssistMaxTurnRateDeg: 360f,
                    launchAssistAcquireConeDeg: 30f,
                    launchAssistMaxRange: 100f,
                    launchAssistMinDistance: 1f,
                    launchAssistDropoffStartRatio: 0.6f,
                    inheritedVelocity: new Vector2(40f, 20f),
                    inheritFactor: 1f,
                    overrideSprite: null);

                Assert.That((bool)LaunchedField.GetValue(controller), Is.True);
                Assert.That(SourceObjectField.GetValue(controller), Is.SameAs(source));
                Assert.That((bool)IsStuckField.GetValue(controller), Is.False);

                OnDisableMethod.Invoke(controller, null);

                Assert.That((bool)LaunchedField.GetValue(controller), Is.False);
                Assert.That(SourceObjectField.GetValue(controller), Is.Null);
                Assert.That((bool)IsStuckField.GetValue(controller), Is.False);
                Assert.That(((BoxCollider2D)HitColliderField.GetValue(controller)).enabled, Is.False);
                Assert.That(controller.CurrentVelocity, Is.EqualTo(Vector2.zero));
                Assert.That(controller.BuildAiArenaProjectileSnapshot().isValid, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(source);
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void IsOpposingProjectile_UsesFlightDirectionInsteadOfOnlyHorizontalSign()
        {
            Assert.That(LaunchDirectionField, Is.Not.Null);
            Assert.That(VelocityField, Is.Not.Null);
            Assert.That(IsOpposingProjectileMethod, Is.Not.Null);

            GameObject leftRoot = new GameObject("projectile_collision_left");
            GameObject rightRoot = new GameObject("projectile_collision_right");

            try
            {
                ProjectileController verticalDown = leftRoot.AddComponent<ProjectileController>();
                ProjectileController verticalUp = rightRoot.AddComponent<ProjectileController>();

                LaunchDirectionField.SetValue(verticalDown, Vector2.down);
                LaunchDirectionField.SetValue(verticalUp, Vector2.up);
                VelocityField.SetValue(verticalDown, new Vector2(0f, -120f));
                VelocityField.SetValue(verticalUp, new Vector2(0f, 120f));

                bool verticalCollision = (bool)IsOpposingProjectileMethod.Invoke(verticalDown, new object[] { verticalUp });
                Assert.That(verticalCollision, Is.True);

                ProjectileController parallelA = leftRoot.AddComponent<ProjectileController>();
                ProjectileController parallelB = rightRoot.AddComponent<ProjectileController>();

                LaunchDirectionField.SetValue(parallelA, Vector2.right);
                LaunchDirectionField.SetValue(parallelB, Vector2.right);
                VelocityField.SetValue(parallelA, new Vector2(150f, 20f));
                VelocityField.SetValue(parallelB, new Vector2(180f, 18f));

                bool parallelCollision = (bool)IsOpposingProjectileMethod.Invoke(parallelA, new object[] { parallelB });
                Assert.That(parallelCollision, Is.False);

                ProjectileController diagonalA = leftRoot.AddComponent<ProjectileController>();
                ProjectileController diagonalB = rightRoot.AddComponent<ProjectileController>();

                LaunchDirectionField.SetValue(diagonalA, new Vector2(1f, 1f));
                LaunchDirectionField.SetValue(diagonalB, new Vector2(1f, -1f));
                VelocityField.SetValue(diagonalA, new Vector2(140f, 140f));
                VelocityField.SetValue(diagonalB, new Vector2(140f, -140f));

                bool diagonalCollision = (bool)IsOpposingProjectileMethod.Invoke(diagonalA, new object[] { diagonalB });
                Assert.That(diagonalCollision, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(leftRoot);
                Object.DestroyImmediate(rightRoot);
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
