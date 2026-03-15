using System.Collections.Generic;
using ProjectPVP.Data;
using ProjectPVP.Gameplay;
using UnityEngine;

namespace ProjectPVP.Presentation
{
    public sealed class CharacterSpriteAnimator : MonoBehaviour
    {
        private static readonly string[] DirectionPriority =
        {
            "right",
            "left",
        };

        private struct AnimationSelection
        {
            public ActionSpriteAnimation animation;
            public bool flipX;
            public string clipKey;
        }

        public PlayerController player;
        public SpriteRenderer spriteRenderer;

        private string _currentClipKey = string.Empty;
        private int _currentFrameIndex;
        private float _frameTimer;

        private void Reset()
        {
            player = GetComponentInParent<PlayerController>();
            spriteRenderer = GetComponent<SpriteRenderer>();
        }

        private void LateUpdate()
        {
            if (player == null || spriteRenderer == null || player.characterDefinition == null)
            {
                return;
            }

            ApplyAnimationFrame();
        }

        private void ApplyAnimationFrame()
        {
            string actionName = ResolveActionName();
            string directionKey = ResolveDirectionKey();
            AnimationSelection selection = FindBestAnimation(actionName, directionKey);
            ActionSpriteAnimation animation = selection.animation;

            if (!HasUsableFrames(animation))
            {
                ApplyDirectionalFallbackSprite(actionName, directionKey);
                _currentClipKey = string.Empty;
                _currentFrameIndex = 0;
                _frameTimer = 0f;
                return;
            }

            if (_currentClipKey != selection.clipKey)
            {
                _currentClipKey = selection.clipKey;
                _currentFrameIndex = 0;
                _frameTimer = 0f;
            }

            float framesPerSecond = ResolvePlaybackFramesPerSecond(actionName, animation);
            _frameTimer += Time.deltaTime * framesPerSecond;

            if (_frameTimer >= 1f)
            {
                int stepCount = Mathf.FloorToInt(_frameTimer);
                _frameTimer -= stepCount;
                if (animation.loop)
                {
                    _currentFrameIndex = (_currentFrameIndex + stepCount) % animation.frames.Count;
                }
                else
                {
                    _currentFrameIndex = Mathf.Min(animation.frames.Count - 1, _currentFrameIndex + stepCount);
                }
            }

            _currentFrameIndex = Mathf.Clamp(_currentFrameIndex, 0, animation.frames.Count - 1);
            Sprite resolvedFrame = ResolveFrameSprite(animation, _currentFrameIndex);
            if (resolvedFrame == null)
            {
                ApplyDirectionalFallbackSprite(actionName, directionKey, selection.flipX);
                return;
            }

            ApplyResolvedSprite(resolvedFrame, selection.flipX);
        }

        private string ResolveActionName()
        {
            return string.IsNullOrWhiteSpace(player.CurrentVisualActionKey) ? "idle" : player.CurrentVisualActionKey;
        }

        private string ResolveDirectionKey()
        {
            return player.Facing < 0 ? "left" : "right";
        }

        private float ResolvePlaybackFramesPerSecond(string actionName, ActionSpriteAnimation animation)
        {
            float fallback = animation != null ? Mathf.Max(1f, animation.framesPerSecond > 0f ? animation.framesPerSecond : 12f) : 12f;
            if (player.characterDefinition == null || animation == null || animation.frames == null || animation.frames.Count == 0)
            {
                return fallback;
            }

            float defaultDuration = animation.frames.Count / fallback;
            float targetDuration = player.characterDefinition.ResolveActionDuration(actionName, defaultDuration);
            float durationDerivedSpeed = animation.frames.Count / Mathf.Max(0.01f, targetDuration);

            if (!animation.loop)
            {
                return Mathf.Max(1f, durationDerivedSpeed);
            }

            float configuredSpeed = Mathf.Max(0.01f, player.characterDefinition.ResolveActionSpeed(actionName, fallback));
            if (configuredSpeed < 1f || Mathf.Abs(targetDuration - defaultDuration) > 0.05f)
            {
                return Mathf.Max(1f, durationDerivedSpeed);
            }

            return Mathf.Max(1f, configuredSpeed);
        }

        private AnimationSelection FindBestAnimation(string actionName, string directionKey)
        {
            foreach (string actionCandidate in EnumerateActionCandidates(actionName))
            {
                AnimationSelection selection = PickAnimationWithFlip(actionCandidate, directionKey);
                if (selection.animation != null)
                {
                    return selection;
                }
            }

            if (actionName == "running")
            {
                return FindBestAnimation("walk", directionKey);
            }

            if (actionName != "idle")
            {
                return FindBestAnimation("idle", directionKey);
            }

            return default;
        }

        private AnimationSelection PickAnimationWithFlip(string actionName, string directionKey)
        {
            AnimationSelection exact = TryBuildSelection(actionName, directionKey, false);
            if (exact.animation != null)
            {
                return exact;
            }

            string oppositeDirection = directionKey == "left" ? "right" : "left";
            AnimationSelection mirrored = TryBuildSelection(actionName, oppositeDirection, true);
            if (mirrored.animation != null)
            {
                return mirrored;
            }

            foreach (string candidateDirection in BuildDirectionPriority(directionKey))
            {
                bool flip = candidateDirection == "right" && directionKey == "left";
                AnimationSelection directional = TryBuildSelection(actionName, candidateDirection, flip);
                if (directional.animation != null)
                {
                    return directional;
                }
            }

            return default;
        }

        private AnimationSelection TryBuildSelection(string actionName, string directionKey, bool flipX)
        {
            ActionSpriteAnimation animation = FindAnimation(actionName, directionKey);
            if (animation == null)
            {
                return default;
            }

            string resolvedDirection = string.IsNullOrWhiteSpace(directionKey) ? "raw" : directionKey;
            return new AnimationSelection
            {
                animation = animation,
                flipX = flipX,
                clipKey = actionName + ":" + resolvedDirection + ":" + (flipX ? "flip" : "no_flip"),
            };
        }

        private ActionSpriteAnimation FindAnimation(string actionName, string directionKey)
        {
            if (player.characterDefinition.actionSpriteAnimations == null || string.IsNullOrWhiteSpace(directionKey))
            {
                return null;
            }

            for (int index = 0; index < player.characterDefinition.actionSpriteAnimations.Count; index += 1)
            {
                ActionSpriteAnimation animation = player.characterDefinition.actionSpriteAnimations[index];
                if (animation == null
                    || string.IsNullOrWhiteSpace(animation.actionName)
                    || !string.Equals(animation.actionName, actionName, System.StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (string.Equals(animation.directionKey, directionKey, System.StringComparison.OrdinalIgnoreCase)
                    && HasUsableFrames(animation))
                {
                    return animation;
                }
            }

            return null;
        }

        private static IEnumerable<string> EnumerateActionCandidates(string actionName)
        {
            if (string.IsNullOrWhiteSpace(actionName))
            {
                yield break;
            }

            yield return actionName;

            if (actionName == "jump_start" || actionName == "jump_air")
            {
                yield return "jump";
            }

            if (actionName == "aim")
            {
                yield return "aiming";
            }
        }

        private static IEnumerable<string> BuildDirectionPriority(string requestedDirection)
        {
            yield return requestedDirection;

            for (int index = 0; index < DirectionPriority.Length; index += 1)
            {
                string candidate = DirectionPriority[index];
                if (candidate == requestedDirection)
                {
                    continue;
                }

                yield return candidate;
            }
        }

        private void ApplyDirectionalFallbackSprite(string actionName, string directionKey, bool fallbackFlip = false)
        {
            if (TryResolveDirectionalFallbackFrame(actionName, directionKey, out Sprite directionalSprite, out bool resolvedFlip))
            {
                ApplyResolvedSprite(directionalSprite, resolvedFlip);
                return;
            }

            ApplyFallbackSprite(fallbackFlip);
        }

        private bool TryResolveDirectionalFallbackFrame(string actionName, string directionKey, out Sprite sprite, out bool flipX)
        {
            flipX = false;
            string oppositeDirection = directionKey == "left" ? "right" : "left";

            foreach (string actionCandidate in EnumerateFallbackActions(actionName))
            {
                foreach (string alias in EnumerateActionCandidates(actionCandidate))
                {
                    ActionSpriteAnimation exact = FindAnimation(alias, directionKey);
                    Sprite exactFrame = ResolveFrameSprite(exact, 0);
                    if (exactFrame != null)
                    {
                        sprite = exactFrame;
                        return true;
                    }
                }
            }

            foreach (string actionCandidate in EnumerateFallbackActions(actionName))
            {
                foreach (string alias in EnumerateActionCandidates(actionCandidate))
                {
                    ActionSpriteAnimation mirrored = FindAnimation(alias, oppositeDirection);
                    Sprite mirroredFrame = ResolveFrameSprite(mirrored, 0);
                    if (mirroredFrame != null)
                    {
                        sprite = mirroredFrame;
                        flipX = true;
                        return true;
                    }
                }
            }

            sprite = null;
            return false;
        }

        private static IEnumerable<string> EnumerateFallbackActions(string actionName)
        {
            if (!string.IsNullOrWhiteSpace(actionName))
            {
                yield return actionName;
            }

            yield return "idle";
            yield return "walk";
            yield return "running";
            yield return "aim";
            yield return "shoot";
            yield return "dash";
            yield return "jump_air";
            yield return "jump_start";
            yield return "melee";
            yield return "ult";
        }

        private void ApplyFallbackSprite(bool flipX)
        {
            spriteRenderer.flipX = flipX;
            if (player.characterDefinition != null && player.characterDefinition.defaultSprite != null)
            {
                spriteRenderer.sprite = player.characterDefinition.defaultSprite;
            }

            ApplySpriteLayout();
        }

        private void ApplyResolvedSprite(Sprite sprite, bool flipX)
        {
            spriteRenderer.sprite = sprite;
            spriteRenderer.flipX = flipX;
            ApplySpriteLayout();
        }

        private void ApplySpriteLayout()
        {
            if (player == null || player.characterDefinition == null || spriteRenderer == null)
            {
                return;
            }

            Transform spriteTransform = spriteRenderer.transform;
            Vector2 baseScale = player.characterDefinition.spriteScale;
            Vector2 anchorOffset = player.characterDefinition.spriteAnchorOffset;
            float scaleFactor = ResolveScaleFactor(player.characterDefinition.defaultSprite, spriteRenderer.sprite);
            float scaleX = baseScale.x * scaleFactor;
            float scaleY = baseScale.y * scaleFactor;
            float positionY = anchorOffset.y + ResolveBottomAlignmentOffset(player.characterDefinition.defaultSprite, spriteRenderer.sprite, baseScale.y, scaleY);

            spriteTransform.localScale = new Vector3(scaleX, scaleY, 1f);
            spriteTransform.localPosition = new Vector3(anchorOffset.x, positionY, 0f);
        }

        private static float ResolveScaleFactor(Sprite referenceSprite, Sprite currentSprite)
        {
            if (referenceSprite == null || currentSprite == null)
            {
                return 1f;
            }

            float referenceHeight = referenceSprite.bounds.size.y;
            float currentHeight = currentSprite.bounds.size.y;
            if (referenceHeight <= 0.0001f || currentHeight <= 0.0001f)
            {
                return 1f;
            }

            return Mathf.Clamp(referenceHeight / currentHeight, 0.01f, 100f);
        }

        private static float ResolveBottomAlignmentOffset(Sprite referenceSprite, Sprite currentSprite, float referenceScaleY, float currentScaleY)
        {
            if (referenceSprite == null || currentSprite == null)
            {
                return 0f;
            }

            float referenceBottom = referenceSprite.bounds.min.y * referenceScaleY;
            float currentBottom = currentSprite.bounds.min.y * currentScaleY;
            return referenceBottom - currentBottom;
        }

        private static bool HasUsableFrames(ActionSpriteAnimation animation)
        {
            return FindFirstValidFrame(animation) != null;
        }

        private static Sprite ResolveFrameSprite(ActionSpriteAnimation animation, int frameIndex)
        {
            if (animation == null || animation.frames == null || animation.frames.Count == 0)
            {
                return null;
            }

            if (frameIndex >= 0 && frameIndex < animation.frames.Count && animation.frames[frameIndex] != null)
            {
                return animation.frames[frameIndex];
            }

            return FindFirstValidFrame(animation);
        }

        private static Sprite FindFirstValidFrame(ActionSpriteAnimation animation)
        {
            if (animation == null || animation.frames == null)
            {
                return null;
            }

            for (int index = 0; index < animation.frames.Count; index += 1)
            {
                if (animation.frames[index] != null)
                {
                    return animation.frames[index];
                }
            }

            return null;
        }
    }
}
