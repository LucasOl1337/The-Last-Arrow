using NUnit.Framework;
using ProjectPVP.Data;
using ProjectPVP.Gameplay;
using UnityEngine;

namespace ProjectPVP.Tests.Editor
{
    public sealed class PlayerStatResolverTests
    {
        [Test]
        public void ResolveProjectileAssistEnabled_ReturnsFalseWhenCharacterDefinitionIsMissing()
        {
            PlayerContext context = new PlayerContext();
            PlayerStatResolver resolver = new PlayerStatResolver(context);

            Assert.That(resolver.ResolveProjectileAssistEnabled(), Is.False);
        }

        [Test]
        public void ResolveProjectileAssistEnabled_UsesCharacterDefinitionValue()
        {
            CharacterDefinition definition = ScriptableObject.CreateInstance<CharacterDefinition>();
            PlayerContext context = new PlayerContext
            {
                characterDefinition = definition,
            };
            PlayerStatResolver resolver = new PlayerStatResolver(context);

            try
            {
                definition.projectileAssistEnabled = true;
                Assert.That(resolver.ResolveProjectileAssistEnabled(), Is.True);

                definition.projectileAssistEnabled = false;
                Assert.That(resolver.ResolveProjectileAssistEnabled(), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(definition);
            }
        }
    }
}
