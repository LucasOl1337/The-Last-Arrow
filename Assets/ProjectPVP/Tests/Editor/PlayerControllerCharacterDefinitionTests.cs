using System.Reflection;
using NUnit.Framework;
using ProjectPVP.Data;
using ProjectPVP.Gameplay;
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
