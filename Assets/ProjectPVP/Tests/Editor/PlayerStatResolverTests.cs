using NUnit.Framework;
using ProjectPVP.Data;
using ProjectPVP.Gameplay;
using UnityEditor;
using UnityEngine;

namespace ProjectPVP.Tests.Editor
{
    public sealed class PlayerStatResolverTests
    {
        [Test]
        public void ResolveMovementAndTempoStats_DifferBetweenMizuAndStormDragonAssets()
        {
            CharacterDefinition mizu = AssetDatabase.LoadAssetAtPath<CharacterDefinition>(
                "Assets/ProjectPVP/Characters/Mizu/Data/MizuDefinition.asset");
            CharacterDefinition stormDragon = AssetDatabase.LoadAssetAtPath<CharacterDefinition>(
                "Assets/ProjectPVP/Characters/StormDragon/Data/StormDragonDefinition.asset");

            Assert.That(mizu, Is.Not.Null);
            Assert.That(stormDragon, Is.Not.Null);

            PlayerStatResolver mizuResolver = new PlayerStatResolver(new PlayerContext
            {
                characterDefinition = mizu,
            });
            PlayerStatResolver stormDragonResolver = new PlayerStatResolver(new PlayerContext
            {
                characterDefinition = stormDragon,
            });

            Assert.That(mizuResolver.ResolveMoveSpeed(), Is.GreaterThan(stormDragonResolver.ResolveMoveSpeed() * 1.15f));
            Assert.That(mizuResolver.ResolveJumpVelocity(), Is.GreaterThan(stormDragonResolver.ResolveJumpVelocity() * 1.1f));
            Assert.That(mizuResolver.ResolveGravity(), Is.LessThan(stormDragonResolver.ResolveGravity()));
            Assert.That(mizuResolver.ResolveDashDistance(), Is.GreaterThan(stormDragonResolver.ResolveDashDistance()));
            Assert.That(mizuResolver.ResolveMeleeDuration(), Is.LessThan(stormDragonResolver.ResolveMeleeDuration() * 0.8f));
            Assert.That(mizuResolver.ResolveMeleeCooldown(), Is.LessThan(stormDragonResolver.ResolveMeleeCooldown() * 0.8f));
            Assert.That(
                mizuResolver.ResolveActionDuration("melee", 0f),
                Is.EqualTo(mizuResolver.ResolveMeleeDuration()).Within(0.001f));
            Assert.That(
                stormDragonResolver.ResolveActionDuration("melee", 0f),
                Is.EqualTo(stormDragonResolver.ResolveMeleeDuration()).Within(0.001f));
            Assert.That(mizuResolver.ResolveActionDuration("death", 0f), Is.EqualTo(0.33333334f).Within(0.001f));
            Assert.That(stormDragonResolver.ResolveActionDuration("death", 0f), Is.EqualTo(0.5f).Within(0.001f));
            Assert.That(mizuResolver.ResolveActionDuration("dash", 0f), Is.LessThan(stormDragonResolver.ResolveActionDuration("dash", 0f) * 0.75f));
            Assert.That(mizuResolver.ResolveActionDuration("shoot", 0f), Is.LessThan(stormDragonResolver.ResolveActionDuration("shoot", 0f) * 0.8f));
            Assert.That(mizuResolver.ResolveProjectileInheritVelocityFactor(), Is.EqualTo(1f).Within(0.001f));
            Assert.That(stormDragonResolver.ResolveProjectileInheritVelocityFactor(), Is.EqualTo(0.45f).Within(0.001f));
        }

        [Test]
        public void ResolveMaxArrows_ReturnsTowerFallLikeDefaultWhenCharacterDefinitionIsMissing()
        {
            PlayerContext context = new PlayerContext();
            PlayerStatResolver resolver = new PlayerStatResolver(context);

            Assert.That(resolver.ResolveMaxArrows(), Is.EqualTo(3));
        }

        [Test]
        public void ResolveMoveSpeed_UsesTowerFallStyleEncumbranceWhenHoldingMultipleArrows()
        {
            CharacterDefinition definition = ScriptableObject.CreateInstance<CharacterDefinition>();
            PlayerContext context = new PlayerContext
            {
                characterDefinition = definition,
            };
            PlayerStatResolver resolver = new PlayerStatResolver(context);

            try
            {
                definition.moveSpeed = 500f;

                context.arrows = 1;
                Assert.That(resolver.ResolveMoveSpeed(), Is.EqualTo(500f).Within(0.0001f));

                context.arrows = 2;
                Assert.That(resolver.ResolveMoveSpeed(), Is.EqualTo(425f).Within(0.0001f));

                context.arrows = 3;
                Assert.That(resolver.ResolveMoveSpeed(), Is.EqualTo(350f).Within(0.0001f));
            }
            finally
            {
                Object.DestroyImmediate(definition);
            }
        }

        [Test]
        public void ResolveShootCooldown_UsesFixedStepFriendlyDefaultWhenCharacterDefinitionIsMissing()
        {
            PlayerContext context = new PlayerContext();
            PlayerStatResolver resolver = new PlayerStatResolver(context);

            Assert.That(resolver.ResolveShootCooldown(), Is.EqualTo(0.02f).Within(0.0001f));
        }

        [Test]
        public void ResolveProjectileAssistEnabled_ReturnsFalseWhenCharacterDefinitionIsMissing()
        {
            PlayerContext context = new PlayerContext();
            PlayerStatResolver resolver = new PlayerStatResolver(context);

            Assert.That(resolver.ResolveProjectileAssistEnabled(), Is.False);
        }

        [Test]
        public void ResolveProjectileAssistStrength_ReturnsCanonicalDefaultWhenCharacterDefinitionIsMissing()
        {
            PlayerContext context = new PlayerContext();
            PlayerStatResolver resolver = new PlayerStatResolver(context);

            Assert.That(resolver.ResolveProjectileAssistStrength(), Is.EqualTo(0.2f).Within(0.0001f));
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

        [Test]
        public void ResolveProjectileInheritVelocityFactor_UsesCharacterDefinitionValue()
        {
            CharacterDefinition definition = ScriptableObject.CreateInstance<CharacterDefinition>();
            PlayerContext context = new PlayerContext
            {
                characterDefinition = definition,
            };
            PlayerStatResolver resolver = new PlayerStatResolver(context);

            try
            {
                definition.projectileInheritVelocityFactor = 0.25f;
                Assert.That(resolver.ResolveProjectileInheritVelocityFactor(), Is.EqualTo(0.25f).Within(0.0001f));
            }
            finally
            {
                Object.DestroyImmediate(definition);
            }
        }

        [Test]
        public void ResolveProjectileInheritVelocityFactor_ReturnsTowerFallLikeDefaultWhenCharacterDefinitionIsMissing()
        {
            PlayerContext context = new PlayerContext();
            PlayerStatResolver resolver = new PlayerStatResolver(context);

            Assert.That(resolver.ResolveProjectileInheritVelocityFactor(), Is.EqualTo(1f).Within(0.0001f));
        }
    }
}
