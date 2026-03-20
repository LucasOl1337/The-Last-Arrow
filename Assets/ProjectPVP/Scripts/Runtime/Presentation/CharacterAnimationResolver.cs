using System;
using System.Collections.Generic;
using ProjectPVP.Data;

namespace ProjectPVP.Presentation
{
    internal static class CharacterAnimationResolver
    {
        public static bool TryResolveActionAnimationSelection(
            CharacterDefinition definition,
            string actionName,
            int facingDirection,
            out ActionSpriteAnimation resolvedAnimation,
            out bool resolvedFlipX)
        {
            resolvedAnimation = null;
            resolvedFlipX = false;

            if (definition == null || string.IsNullOrWhiteSpace(actionName))
            {
                return false;
            }

            ActionCatalog catalog = definition.ResolveActionCatalog();
            string directionKey = facingDirection < 0 ? "left" : "right";
            string mirroredDirectionKey = catalog.ResolveMirroredDirectionKey(directionKey);

            foreach (string candidateActionName in catalog.EnumerateActionKeys(actionName))
            {
                resolvedAnimation = FindAnimation(definition, candidateActionName, directionKey);
                if (HasUsableAnimationFrames(resolvedAnimation))
                {
                    resolvedFlipX = false;
                    return true;
                }

                resolvedAnimation = FindAnimation(definition, candidateActionName, mirroredDirectionKey);
                if (HasUsableAnimationFrames(resolvedAnimation))
                {
                    resolvedFlipX = true;
                    return true;
                }

                resolvedAnimation = FindAnimation(definition, candidateActionName, "shared");
                if (HasUsableAnimationFrames(resolvedAnimation))
                {
                    resolvedFlipX = false;
                    return true;
                }
            }

            resolvedAnimation = null;
            resolvedFlipX = false;
            return false;
        }

        public static ActionSpriteAnimation FindAnimation(CharacterDefinition definition, string actionName, string directionKey)
        {
            if (definition == null)
            {
                return null;
            }

            IReadOnlyList<ActionSpriteAnimation> animations = definition.GetActionAnimations();
            if (animations == null || string.IsNullOrWhiteSpace(actionName) || string.IsNullOrWhiteSpace(directionKey))
            {
                return null;
            }

            for (int index = 0; index < animations.Count; index += 1)
            {
                ActionSpriteAnimation animation = animations[index];
                if (animation == null
                    || string.IsNullOrWhiteSpace(animation.actionName)
                    || string.IsNullOrWhiteSpace(animation.directionKey)
                    || !string.Equals(animation.actionName, actionName, StringComparison.OrdinalIgnoreCase)
                    || !string.Equals(animation.directionKey, directionKey, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                return animation;
            }

            return null;
        }

        public static bool HasUsableAnimationFrames(ActionSpriteAnimation animation)
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
