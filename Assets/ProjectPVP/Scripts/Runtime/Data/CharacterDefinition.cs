using System;
using System.Collections.Generic;
using ProjectPVP.Gameplay;
using UnityEngine;

namespace ProjectPVP.Data
{
    public enum ProjectileOriginMode
    {
        BowNode = 0,
        Chest = 1,
        ColliderCenter = 2,
        ColliderTop = 3,
    }

    [Serializable]
    public sealed class NamedBoolValue
    {
        public string key = string.Empty;
        public bool value;
    }

    [Serializable]
    public sealed class NamedFloatValue
    {
        public string key = string.Empty;
        public float value;
    }

    [Serializable]
    public sealed class NamedVector2Value
    {
        public string key = string.Empty;
        public Vector2 value = Vector2.zero;
    }

    [Serializable]
    public sealed class ActionColliderOverride
    {
        public string actionName = string.Empty;
        public Vector2 size = new Vector2(90f, 210f);
        public Vector2 offset = Vector2.zero;
    }

    [Serializable]
    public sealed class ActionSpriteAnimation
    {
        public string actionName = string.Empty;
        public string directionKey = "right";
        public float framesPerSecond = 12f;
        public bool loop = true;
        public List<Sprite> frames = new List<Sprite>();
    }

    [Serializable]
    public sealed class ActionAudioCue
    {
        public string actionName = string.Empty;
        public AudioClip clip;
        public string resourcesPath = string.Empty;
        public float playbackSpeed = 1f;
        public float volumeDb;
        public float stopAfterSeconds;
    }

    [Serializable]
    public sealed class PixelLabActionAlias
    {
        public string pattern = string.Empty;
        public string actionName = string.Empty;
    }

    [CreateAssetMenu(fileName = "CharacterDefinition", menuName = "ProjectPVP/Character Definition")]
    public sealed class CharacterDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string id = string.Empty;
        public string displayName = string.Empty;

        [Header("PixelLab Sync")]
        public string pixelLabCharacterId = string.Empty;
        public List<PixelLabActionAlias> pixelLabActionAliases = new List<PixelLabActionAlias>();

        [Header("Visual")]
        public ActionCatalog actionCatalog;
        public Vector2 spriteScale = new Vector2(3.6f, 3.6f);
        public Vector2 spriteAnchorOffset = Vector2.zero;
        [Min(1)] public int nativeSpriteBakeScale = 1;
        public float animationScaleMax = 1f;
        public float animationScaleMin;
        public float visualReferenceWidth;
        public float visualReferenceHeight;
        public float targetVisualHeight;
        [Range(0f, 1f)] public float groundAnchorRatio = 1f;
        public bool aimSkipLeft;
        public Sprite defaultSprite;

        [Header("Stats")]
        [HideInInspector] public bool overridesStats;
        public float moveSpeed = 240f;
        public float acceleration = 1600f;
        public float friction = 2000f;
        public float jumpVelocity = 360f;
        public float gravity = 1200f;
        public float maxFallSpeed = 2000f;
        public float shootCooldown = 0.001f;
        public int maxArrows = 5;
        public float meleeCooldown = 0.45f;
        public float meleeDuration = 0.12f;
        public bool meleeCanSeverProjectiles;

        [Header("Collider")]
        public Vector2 colliderSize = new Vector2(90f, 210f);
        public Vector2 colliderOffset = Vector2.zero;

        [Header("Movement Defaults")]
        public float wallJumpHorizontalForce = 500f;
        public float wallJumpVerticalForce = 720f;
        public float wallSlideSpeed = 60f;
        public float wallGravityScale = 0.2f;

        [Header("Unity Runtime Tuning")]
        public float runtimeMoveScale = 1f;
        public float runtimeJumpScale = 1f;
        public float runtimeGravityScale = 1f;
        public float runtimeDashScale = 1f;

        [Header("Dash")]
        public float dashMultiplier = 1.8f;
        public float dashDuration = 0.12f;
        public float dashCooldown = 0.45f;
        public float dashDistance = 100f;
        public float dashUpwardMultiplier = 0.5f;

        [Header("Ultimate")]
        [Range(0f, 1f)] public float ultimateWindupRatio = 0.45f;
        public float ultimateDashDistance;
        public float ultimateDashDuration = 0.1f;
        public bool ultimateBlocksProjectiles;
        public float ultimateProjectileBlockDuration = 0.12f;
        public float ultimateReplayDelay;
        public float ultimateReplayDuration;
        public float ultimateReplayDashDistance;
        public float ultimateReplayDashDuration;

        [Header("Projectile Spawn")]
        public float projectileForward = 80f;
        public float projectileForwardFacing;
        public float projectileVerticalOffset;
        public float projectileInheritVelocityFactor = 1f;
        public float projectileScale = 1f;
        public ProjectileOriginMode projectileOriginMode = ProjectileOriginMode.BowNode;
        public Vector2 projectileOriginOffset = Vector2.zero;
        public bool projectileUseBowNode = true;
        public Sprite projectileSprite;

        [Header("Projectile Motion")]
        public float projectileBaseSpeed = 1500f;
        public float projectileMinSpeed = 720f;
        public float projectileSpeedDecay = 360f;
        public float projectileGravity = 750f;
        public float projectileGravityDelayRatio;
        public float projectileGravityRampRatio = 0.6f;
        public float projectileGravityMinScale = 0.45f;
        public float projectileGravityMaxScale = 1.2f;
        public float projectileUpwardGravityMultiplier = 3.2f;
        public float projectileUpwardSpeedDecayMultiplier = 2.2f;
        public float projectileMaxLifetime = 2.5f;
        public float projectileMaxRange = 1440f;
        public bool projectileRotateWithVelocity = true;
        public bool projectileCollectableWhenStuck = true;

        [Header("Projectile Hitbox")]
        public Vector2 projectileFlightHitboxSize = new Vector2(24f, 10f);
        public Vector2 projectileFlightHitboxOffset = new Vector2(32f, 0f);
        public Vector2 projectileCollectibleHitboxSize = new Vector2(96f, 24f);
        public Vector2 projectileCollectibleHitboxOffset = Vector2.zero;

        [Header("Action Config")]
        public CharacterAnimationCatalog animationCatalog;
        public CharacterAudioDefinition audioDefinition;
        public CharacterMechanicsModule mechanicsModule;

        [Header("Actions")]
        public List<CharacterActionConfig> actions = new List<CharacterActionConfig>();

        public ActionCatalog ResolveActionCatalog()
        {
            if (animationCatalog != null && animationCatalog.actionCatalog != null)
            {
                return animationCatalog.actionCatalog;
            }

            return actionCatalog != null ? actionCatalog : ActionCatalog.LoadDefault();
        }

        public IReadOnlyList<ActionSpriteAnimation> GetActionAnimations()
        {
            var flattenedAnimations = new List<ActionSpriteAnimation>();
            if (actions == null)
            {
                if (animationCatalog != null && animationCatalog.actionSpriteAnimations != null && animationCatalog.actionSpriteAnimations.Count > 0)
                {
                    return animationCatalog.actionSpriteAnimations;
                }

                return flattenedAnimations;
            }

            for (int actionIndex = 0; actionIndex < actions.Count; actionIndex += 1)
            {
                CharacterActionConfig action = actions[actionIndex];
                if (action == null || string.IsNullOrWhiteSpace(action.actionName) || action.animations == null)
                {
                    continue;
                }

                for (int animationIndex = 0; animationIndex < action.animations.Count; animationIndex += 1)
                {
                    DirectionalSpriteAnimation animation = action.animations[animationIndex];
                    if (animation == null)
                    {
                        continue;
                    }

                    flattenedAnimations.Add(new ActionSpriteAnimation
                    {
                        actionName = action.actionName,
                        directionKey = animation.directionKey,
                        framesPerSecond = animation.framesPerSecond,
                        loop = animation.loop,
                        frames = animation.frames != null ? new List<Sprite>(animation.frames) : new List<Sprite>(),
                    });
                }
            }

            if (flattenedAnimations.Count > 0)
            {
                return flattenedAnimations;
            }

            if (animationCatalog != null && animationCatalog.actionSpriteAnimations != null && animationCatalog.actionSpriteAnimations.Count > 0)
            {
                return animationCatalog.actionSpriteAnimations;
            }

            return flattenedAnimations;
        }

        public IEnumerable<string> EnumerateActionKeys(string actionName)
        {
            return ResolveActionCatalog().EnumerateActionKeys(actionName);
        }

        public float ResolveActionDuration(string actionName, float fallback)
        {
            CharacterActionConfig action = FindActionConfig(actionName);
            if (action != null && action.duration > 0.01f)
            {
                return action.duration;
            }

            if (TryGetRepresentativeAnimation(actionName, out ActionSpriteAnimation animation))
            {
                float framesPerSecond = ResolveActionSpeed(actionName, animation.framesPerSecond > 0f ? animation.framesPerSecond : 12f);
                if (framesPerSecond > 0.01f && animation.frames != null && animation.frames.Count > 0)
                {
                    return Mathf.Max(0.01f, animation.frames.Count / framesPerSecond);
                }
            }

            return fallback;
        }

        public float ResolveActionSpeed(string actionName, float fallback)
        {
            CharacterActionConfig action = FindActionConfig(actionName);
            if (action != null && action.speed > 0.01f)
            {
                return action.speed;
            }

            return fallback;
        }

        public bool ResolveActionCancelable(string actionName, bool fallback)
        {
            CharacterActionConfig action = FindActionConfig(actionName);
            return action != null ? action.cancelable : fallback;
        }

        public bool HasActionAnimation(string actionName)
        {
            return TryGetRepresentativeAnimation(actionName, out _);
        }

        public ActionColliderOverride FindActionColliderOverride(string actionName)
        {
            CharacterActionConfig action = FindActionConfig(actionName);
            return action != null ? action.colliderOverride : null;
        }

        public bool TryResolveActionAudioCue(string actionName, out ActionAudioCue resolvedCue)
        {
            if (audioDefinition != null)
            {
                return audioDefinition.TryResolveActionAudioCue(ResolveActionCatalog(), actionName, out resolvedCue);
            }

            resolvedCue = null;
            return false;
        }

        private bool TryGetRepresentativeAnimation(string actionName, out ActionSpriteAnimation animation)
        {
            animation = null;
            IReadOnlyList<ActionSpriteAnimation> animations = GetActionAnimations();
            if (animations == null || string.IsNullOrWhiteSpace(actionName))
            {
                return false;
            }

            string[] preferredDirections =
            {
                "right",
                "left",
            };

            foreach (string candidateKey in ResolveActionCatalog().EnumerateActionKeys(actionName))
            {
                for (int directionIndex = 0; directionIndex < preferredDirections.Length; directionIndex += 1)
                {
                    string directionKey = preferredDirections[directionIndex];
                    for (int animationIndex = 0; animationIndex < animations.Count; animationIndex += 1)
                    {
                        ActionSpriteAnimation candidate = animations[animationIndex];
                        if (candidate == null
                            || string.IsNullOrWhiteSpace(candidate.actionName)
                            || !string.Equals(candidate.actionName, candidateKey, StringComparison.OrdinalIgnoreCase)
                            || !string.Equals(candidate.directionKey, directionKey, StringComparison.OrdinalIgnoreCase)
                            || !HasUsableFrames(candidate))
                        {
                            continue;
                        }

                        animation = candidate;
                        return true;
                    }
                }
            }

            return false;
        }

        private CharacterActionConfig FindActionConfig(string actionName)
        {
            if (actions == null || string.IsNullOrWhiteSpace(actionName))
            {
                return null;
            }

            foreach (string candidateKey in ResolveActionCatalog().EnumerateActionKeys(actionName))
            {
                for (int index = 0; index < actions.Count; index += 1)
                {
                    CharacterActionConfig action = actions[index];
                    if (action == null || string.IsNullOrWhiteSpace(action.actionName))
                    {
                        continue;
                    }

                    if (string.Equals(action.actionName, candidateKey, StringComparison.OrdinalIgnoreCase))
                    {
                        return action;
                    }
                }
            }

            return null;
        }

        private static bool HasUsableFrames(ActionSpriteAnimation animation)
        {
            if (animation == null || animation.frames == null || animation.frames.Count == 0)
            {
                return false;
            }

            for (int index = 0; index < animation.frames.Count; index += 1)
            {
                if (animation.frames[index] != null)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
