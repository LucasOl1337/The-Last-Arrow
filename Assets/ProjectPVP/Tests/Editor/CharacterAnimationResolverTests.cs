using NUnit.Framework;
using ProjectPVP.Data;
using ProjectPVP.Presentation;
using UnityEngine;

namespace ProjectPVP.Tests.Editor
{
    public sealed class CharacterAnimationResolverTests
    {
        [Test]
        public void TryResolveActionAnimationSelection_UsesMirroredAnimation_WhenRequestedDirectionIsMissing()
        {
            CharacterDefinition definition = ScriptableObject.CreateInstance<CharacterDefinition>();
            Sprite frame = CreateSprite();

            try
            {
                definition.actions.Add(new CharacterActionConfig
                {
                    actionName = "idle",
                    animations =
                    {
                        new DirectionalSpriteAnimation
                        {
                            directionKey = "right",
                            loop = true,
                            frames = { frame },
                        },
                    },
                });

                bool found = CharacterAnimationResolver.TryResolveActionAnimationSelection(
                    definition,
                    "idle",
                    -1,
                    out ActionSpriteAnimation animation,
                    out bool flipX);

                Assert.That(found, Is.True);
                Assert.That(animation, Is.Not.Null);
                Assert.That(animation.directionKey, Is.EqualTo("right"));
                Assert.That(flipX, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(frame.texture);
                Object.DestroyImmediate(definition);
            }
        }

        [Test]
        public void TryResolveActionAnimationSelection_UsesSharedAnimation_AsDirectionalFallback()
        {
            CharacterDefinition definition = ScriptableObject.CreateInstance<CharacterDefinition>();
            Sprite frame = CreateSprite();

            try
            {
                definition.actions.Add(new CharacterActionConfig
                {
                    actionName = "ult",
                    animations =
                    {
                        new DirectionalSpriteAnimation
                        {
                            directionKey = "shared",
                            loop = false,
                            frames = { frame },
                        },
                    },
                });

                bool found = CharacterAnimationResolver.TryResolveActionAnimationSelection(
                    definition,
                    "ult",
                    1,
                    out ActionSpriteAnimation animation,
                    out bool flipX);

                Assert.That(found, Is.True);
                Assert.That(animation, Is.Not.Null);
                Assert.That(animation.directionKey, Is.EqualTo("shared"));
                Assert.That(flipX, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(frame.texture);
                Object.DestroyImmediate(definition);
            }
        }

        private static Sprite CreateSprite()
        {
            Texture2D texture = new Texture2D(4, 4);
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 1f);
        }
    }
}
