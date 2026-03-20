using NUnit.Framework;
using ProjectPVP.Data;
using UnityEngine;

namespace ProjectPVP.Tests.Editor
{
    public sealed class CharacterDefinitionActionConfigTests
    {
        [Test]
        public void ResolveActionValues_UsesActionBlockConfiguration()
        {
            CharacterDefinition definition = ScriptableObject.CreateInstance<CharacterDefinition>();
            Sprite frame = CreateSprite();

            try
            {
                definition.actions.Add(new CharacterActionConfig
                {
                    actionName = "shoot",
                    duration = 0.75f,
                    cancelable = true,
                    speed = 18f,
                    colliderOverride = new ActionColliderOverride
                    {
                        actionName = "shoot",
                        size = new Vector2(100f, 220f),
                        offset = new Vector2(4f, 8f),
                    },
                    animations =
                    {
                        new DirectionalSpriteAnimation
                        {
                            directionKey = "right",
                            framesPerSecond = 18f,
                            loop = false,
                            frames = { frame },
                        },
                    },
                });

                Assert.That(definition.ResolveActionDuration("shoot", 0.1f), Is.EqualTo(0.75f).Within(0.001f));
                Assert.That(definition.ResolveActionSpeed("shoot", 12f), Is.EqualTo(18f).Within(0.001f));
                Assert.That(definition.ResolveActionCancelable("shoot", false), Is.True);

                ActionColliderOverride colliderOverride = definition.FindActionColliderOverride("shoot");
                Assert.That(colliderOverride, Is.Not.Null);
                Assert.That(colliderOverride.size, Is.EqualTo(new Vector2(100f, 220f)));

                var resolvedAnimation = definition.GetActionAnimations();
                ActionSpriteAnimation shootRightAnimation = null;
                for (int index = 0; index < resolvedAnimation.Count; index += 1)
                {
                    ActionSpriteAnimation candidate = resolvedAnimation[index];
                    if (candidate == null)
                    {
                        continue;
                    }

                    if (candidate.actionName == "shoot" && candidate.directionKey == "right")
                    {
                        shootRightAnimation = candidate;
                        break;
                    }
                }
                Assert.That(shootRightAnimation, Is.Not.Null);
                Assert.That(shootRightAnimation.framesPerSecond, Is.EqualTo(18f).Within(0.001f));
                Assert.That(shootRightAnimation.frames.Count, Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(frame.texture);
                Object.DestroyImmediate(definition);
            }
        }

        [Test]
        public void TryResolveActionAudioCue_UsesSeparateAudioAsset()
        {
            CharacterDefinition definition = ScriptableObject.CreateInstance<CharacterDefinition>();
            CharacterAudioDefinition audioDefinition = ScriptableObject.CreateInstance<CharacterAudioDefinition>();

            try
            {
                definition.audioDefinition = audioDefinition;
                audioDefinition.actionAudioCues.Add(new ActionAudioCue
                {
                    actionName = "shoot",
                    resourcesPath = "ProjectPVP/Audio/Characters/StormDragon/shoot",
                    playbackSpeed = 2f,
                    stopAfterSeconds = 0.3f,
                });

                bool found = definition.TryResolveActionAudioCue("shoot", out ActionAudioCue cue);

                Assert.That(found, Is.True);
                Assert.That(cue, Is.Not.Null);
                Assert.That(cue.resourcesPath, Is.EqualTo("ProjectPVP/Audio/Characters/StormDragon/shoot"));
                Assert.That(cue.playbackSpeed, Is.EqualTo(2f).Within(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(audioDefinition);
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
