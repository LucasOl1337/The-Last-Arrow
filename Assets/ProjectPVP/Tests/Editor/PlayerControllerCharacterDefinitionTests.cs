using System.Reflection;
using NUnit.Framework;
using ProjectPVP.Data;
using ProjectPVP.Gameplay;
using UnityEditor;
using UnityEngine;

namespace ProjectPVP.Tests.Editor
{
    public sealed class PlayerControllerCharacterDefinitionTests
    {
        private static readonly MethodInfo ResolveMoveSpeedMethod = ResolvePrivateMethod("ResolveMoveSpeed");
        private static readonly MethodInfo ResolveMaxArrowsMethod = ResolvePrivateMethod("ResolveMaxArrows");
        private static readonly MethodInfo ResolveMeleeDurationMethod = ResolvePrivateMethod("ResolveMeleeDuration");

        [Test]
        public void ResolveMoveSpeed_UsesCharacterDefinitionValue_EvenWhenLegacyOverrideFlagIsDisabled()
        {
            PlayerController player = CreatePlayer(out GameObject gameObject);
            CharacterDefinition definition = ScriptableObject.CreateInstance<CharacterDefinition>();

            try
            {
                definition.overridesStats = false;
                definition.moveSpeed = 321f;
                player.characterDefinition = definition;

                float resolvedMoveSpeed = InvokePrivate<float>(ResolveMoveSpeedMethod, player);

                Assert.That(resolvedMoveSpeed, Is.EqualTo(321f).Within(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(definition);
                Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void ResolveMaxArrows_UsesCharacterDefinitionValue_EvenWhenLegacyOverrideFlagIsDisabled()
        {
            PlayerController player = CreatePlayer(out GameObject gameObject);
            CharacterDefinition definition = ScriptableObject.CreateInstance<CharacterDefinition>();

            try
            {
                definition.overridesStats = false;
                definition.maxArrows = 17;
                player.characterDefinition = definition;

                int resolvedMaxArrows = InvokePrivate<int>(ResolveMaxArrowsMethod, player);

                Assert.That(resolvedMaxArrows, Is.EqualTo(17));
            }
            finally
            {
                Object.DestroyImmediate(definition);
                Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void ResolveMeleeDuration_UsesCharacterDefinitionValue_EvenWhenLegacyOverrideFlagIsDisabled()
        {
            PlayerController player = CreatePlayer(out GameObject gameObject);
            CharacterDefinition definition = ScriptableObject.CreateInstance<CharacterDefinition>();

            try
            {
                definition.overridesStats = false;
                definition.meleeDuration = 0.37f;
                player.characterDefinition = definition;

                float resolvedMeleeDuration = InvokePrivate<float>(ResolveMeleeDurationMethod, player);

                Assert.That(resolvedMeleeDuration, Is.EqualTo(0.37f).Within(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(definition);
                Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void CharacterDefinitionAssets_KeepTowerFallLikeArrowCapacityAndMomentumProfiles()
        {
            CharacterDefinition mizu = AssetDatabase.LoadAssetAtPath<CharacterDefinition>(
                "Assets/ProjectPVP/Characters/Mizu/Data/MizuDefinition.asset");
            CharacterDefinition stormDragon = AssetDatabase.LoadAssetAtPath<CharacterDefinition>(
                "Assets/ProjectPVP/Characters/StormDragon/Data/StormDragonDefinition.asset");

            Assert.That(mizu, Is.Not.Null);
            Assert.That(stormDragon, Is.Not.Null);
            Assert.That(mizu.maxArrows, Is.EqualTo(3));
            Assert.That(stormDragon.maxArrows, Is.EqualTo(3));
            Assert.That(mizu.projectileAssistEnabled, Is.False);
            Assert.That(stormDragon.projectileAssistEnabled, Is.False);
            Assert.That(mizu.projectileInheritVelocityFactor, Is.EqualTo(1f).Within(0.001f));
            Assert.That(stormDragon.projectileInheritVelocityFactor, Is.EqualTo(0.45f).Within(0.001f));
        }

        [Test]
        public void CharacterDefinitionAssets_KeepDistinctAggressionAndMobilityProfiles()
        {
            CharacterDefinition mizu = AssetDatabase.LoadAssetAtPath<CharacterDefinition>(
                "Assets/ProjectPVP/Characters/Mizu/Data/MizuDefinition.asset");
            CharacterDefinition stormDragon = AssetDatabase.LoadAssetAtPath<CharacterDefinition>(
                "Assets/ProjectPVP/Characters/StormDragon/Data/StormDragonDefinition.asset");

            Assert.That(mizu, Is.Not.Null);
            Assert.That(stormDragon, Is.Not.Null);
            Assert.That(mizu.meleeCooldown, Is.LessThan(stormDragon.meleeCooldown));
            Assert.That(mizu.meleeDuration, Is.LessThan(stormDragon.meleeDuration));
            Assert.That(mizu.runtimeMoveScale, Is.GreaterThan(stormDragon.runtimeMoveScale));
            Assert.That(mizu.runtimeDashScale, Is.GreaterThan(stormDragon.runtimeDashScale));
            Assert.That(mizu.dashDistance, Is.GreaterThan(stormDragon.dashDistance));
            Assert.That(mizu.runtimeJumpScale, Is.GreaterThan(stormDragon.runtimeJumpScale));
        }

        [Test]
        public void NewCharacterDefinitions_DefaultToTowerFallLikeArrowCapacityAndMomentum()
        {
            CharacterDefinition definition = ScriptableObject.CreateInstance<CharacterDefinition>();

            try
            {
                Assert.That(definition.maxArrows, Is.EqualTo(3));
                Assert.That(definition.projectileAssistEnabled, Is.False);
                Assert.That(definition.projectileInheritVelocityFactor, Is.EqualTo(1f).Within(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(definition);
            }
        }

        private static PlayerController CreatePlayer(out GameObject gameObject)
        {
            gameObject = new GameObject("PlayerControllerCharacterDefinitionTests");
            return gameObject.AddComponent<PlayerController>();
        }

        private static MethodInfo ResolvePrivateMethod(string methodName)
        {
            MethodInfo method = typeof(PlayerController).GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, "Expected PlayerController to define private method '{0}'.", methodName);
            return method;
        }

        private static T InvokePrivate<T>(MethodInfo method, object target)
        {
            return (T)method.Invoke(target, null);
        }
    }
}
