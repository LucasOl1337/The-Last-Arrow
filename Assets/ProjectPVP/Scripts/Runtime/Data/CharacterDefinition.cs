using System;
using System.Collections.Generic;
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

    [CreateAssetMenu(fileName = "CharacterDefinition", menuName = "ProjectPVP/Character Definition")]
    public sealed class CharacterDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string id = string.Empty;
        public string displayName = string.Empty;

        [Header("Visual")]
        public Vector2 spriteScale = new Vector2(3.6f, 3.6f);
        public Vector2 spriteAnchorOffset = Vector2.zero;
        public float animationScaleMax = 1f;
        public float animationScaleMin;
        public float visualReferenceWidth;
        public float visualReferenceHeight;
        public float targetVisualHeight;
        [Range(0f, 1f)] public float groundAnchorRatio = 1f;
        public bool aimSkipLeft;
        public Sprite defaultSprite;

        [Header("Stats")]
        public bool overridesStats;
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

        [Header("Projectile")]
        public float projectileForward = 80f;
        public float projectileForwardFacing;
        public float projectileVerticalOffset;
        public float projectileInheritVelocityFactor = 1f;
        public float projectileScale = 1f;
        public ProjectileOriginMode projectileOriginMode = ProjectileOriginMode.BowNode;
        public Vector2 projectileOriginOffset = Vector2.zero;
        public bool projectileUseBowNode = true;
        public Sprite projectileSprite;

        [Header("Action Config")]
        public CharacterActionConfig actionConfig;

        [Header("Action Overrides")]
        public List<NamedBoolValue> actionSkipLeft = new List<NamedBoolValue>();
        public List<NamedFloatValue> actionTargetVisualHeight = new List<NamedFloatValue>();
        public List<NamedFloatValue> actionGroundAnchorRatio = new List<NamedFloatValue>();
        public List<NamedFloatValue> actionAnimationDurations = new List<NamedFloatValue>();
        public List<NamedBoolValue> actionAnimationCancelable = new List<NamedBoolValue>();
        public List<NamedFloatValue> actionAnimationSpeeds = new List<NamedFloatValue>();
        public List<NamedFloatValue> actionSpriteScale = new List<NamedFloatValue>();
        public List<NamedVector2Value> actionSpriteOffset = new List<NamedVector2Value>();
        public List<ActionColliderOverride> actionColliderOverrides = new List<ActionColliderOverride>();
        public List<ActionSpriteAnimation> actionSpriteAnimations = new List<ActionSpriteAnimation>();

        public float ResolveActionDuration(string actionName, float fallback)
        {
            if (actionConfig != null && actionConfig.TryResolveActionDuration(actionName, out float configDuration) && configDuration > 0.01f)
            {
                return configDuration;
            }

            float configuredDuration = ResolveNamedFloat(actionAnimationDurations, actionName, float.NaN);
            if (!float.IsNaN(configuredDuration) && configuredDuration > 0.01f)
            {
                return configuredDuration;
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
            if (actionConfig != null && actionConfig.TryResolveActionSpeed(actionName, out float configSpeed) && configSpeed > 0.01f)
            {
                return configSpeed;
            }

            float configuredSpeed = ResolveNamedFloat(actionAnimationSpeeds, actionName, float.NaN);
            if (!float.IsNaN(configuredSpeed) && configuredSpeed > 0.01f)
            {
                return configuredSpeed;
            }

            return fallback;
        }

        public bool ResolveActionCancelable(string actionName, bool fallback)
        {
            if (actionConfig != null && actionConfig.TryResolveActionCancelable(actionName, out bool configCancelable))
            {
                return configCancelable;
            }

            return ResolveNamedBool(actionAnimationCancelable, actionName, fallback);
        }

        public bool HasActionAnimation(string actionName)
        {
            return TryGetRepresentativeAnimation(actionName, out _);
        }

        public ActionColliderOverride FindActionColliderOverride(string actionName)
        {
            if (actionConfig != null && actionConfig.TryFindActionColliderOverride(actionName, out ActionColliderOverride configOverride))
            {
                return configOverride;
            }

            if (actionColliderOverrides == null || string.IsNullOrWhiteSpace(actionName))
            {
                return null;
            }

            foreach (string candidateKey in EnumerateActionKeys(actionName))
            {
                for (int index = 0; index < actionColliderOverrides.Count; index += 1)
                {
                    ActionColliderOverride entry = actionColliderOverrides[index];
                    if (entry == null || string.IsNullOrWhiteSpace(entry.actionName))
                    {
                        continue;
                    }

                    if (string.Equals(entry.actionName, candidateKey, StringComparison.OrdinalIgnoreCase))
                    {
                        return entry;
                    }
                }
            }

            return null;
        }

        private float ResolveNamedFloat(List<NamedFloatValue> values, string actionName, float fallback)
        {
            if (values == null || string.IsNullOrWhiteSpace(actionName))
            {
                return fallback;
            }

            foreach (string candidateKey in EnumerateActionKeys(actionName))
            {
                for (int index = 0; index < values.Count; index += 1)
                {
                    NamedFloatValue entry = values[index];
                    if (entry == null || string.IsNullOrWhiteSpace(entry.key))
                    {
                        continue;
                    }

                    if (string.Equals(entry.key, candidateKey, StringComparison.OrdinalIgnoreCase))
                    {
                        return entry.value;
                    }
                }
            }

            return fallback;
        }

        private bool ResolveNamedBool(List<NamedBoolValue> values, string actionName, bool fallback)
        {
            if (values == null || string.IsNullOrWhiteSpace(actionName))
            {
                return fallback;
            }

            foreach (string candidateKey in EnumerateActionKeys(actionName))
            {
                for (int index = 0; index < values.Count; index += 1)
                {
                    NamedBoolValue entry = values[index];
                    if (entry == null || string.IsNullOrWhiteSpace(entry.key))
                    {
                        continue;
                    }

                    if (string.Equals(entry.key, candidateKey, StringComparison.OrdinalIgnoreCase))
                    {
                        return entry.value;
                    }
                }
            }

            return fallback;
        }

        private bool TryGetRepresentativeAnimation(string actionName, out ActionSpriteAnimation animation)
        {
            animation = null;
            if (actionSpriteAnimations == null || string.IsNullOrWhiteSpace(actionName))
            {
                return false;
            }

            string[] preferredDirections =
            {
                "right",
                "left",
            };

            foreach (string candidateKey in EnumerateActionKeys(actionName))
            {
                for (int directionIndex = 0; directionIndex < preferredDirections.Length; directionIndex += 1)
                {
                    string directionKey = preferredDirections[directionIndex];
                    for (int animationIndex = 0; animationIndex < actionSpriteAnimations.Count; animationIndex += 1)
                    {
                        ActionSpriteAnimation candidate = actionSpriteAnimations[animationIndex];
                        if (candidate == null
                            || string.IsNullOrWhiteSpace(candidate.actionName)
                            || !string.Equals(candidate.actionName, candidateKey, StringComparison.OrdinalIgnoreCase)
                            || !string.Equals(candidate.directionKey, directionKey, StringComparison.OrdinalIgnoreCase)
                            || candidate.frames == null
                            || candidate.frames.Count <= 0)
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

        private static IEnumerable<string> EnumerateActionKeys(string actionName)
        {
            if (string.IsNullOrWhiteSpace(actionName))
            {
                yield break;
            }

            yield return actionName;

            if (string.Equals(actionName, "jump_start", StringComparison.OrdinalIgnoreCase)
                || string.Equals(actionName, "jump_air", StringComparison.OrdinalIgnoreCase))
            {
                yield return "jump";
            }

            if (string.Equals(actionName, "aim", StringComparison.OrdinalIgnoreCase))
            {
                yield return "aiming";
            }
        }
    }
}
