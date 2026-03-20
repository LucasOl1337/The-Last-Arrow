using System.Collections.Generic;
using ProjectPVP.Data;
using ProjectPVP.Gameplay;
using UnityEngine;

namespace ProjectPVP.Presentation
{
    public sealed class CharacterSpriteAnimator : MonoBehaviour
    {
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
            int facingDirection = player.ResolveFacingDirection();
            AnimationSelection selection = FindBestAnimation(actionName, facingDirection);
            ActionSpriteAnimation animation = selection.animation;

            if (!HasUsableFrames(animation))
            {
                ApplyDirectionalFallbackSprite(actionName, facingDirection);
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
                ApplyDirectionalFallbackSprite(actionName, facingDirection);
                return;
            }

            ApplyResolvedSprite(resolvedFrame, selection.flipX);
        }

        private string ResolveActionName()
        {
            return string.IsNullOrWhiteSpace(player.CurrentVisualActionKey) ? "idle" : player.CurrentVisualActionKey;
        }

        private static string ResolveDirectionKey(int facingDirection)
        {
            return facingDirection < 0 ? "left" : "right";
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

        private AnimationSelection FindBestAnimation(string actionName, int facingDirection)
        {
            if (TryBuildSelection(actionName, facingDirection, out AnimationSelection selection))
            {
                return selection;
            }

            if (actionName == "running")
            {
                return FindBestAnimation("walk", facingDirection);
            }

            if (actionName != "idle")
            {
                return FindBestAnimation("idle", facingDirection);
            }

            return default;
        }

        private bool TryBuildSelection(string actionName, int facingDirection, out AnimationSelection selection)
        {
            if (!CharacterAnimationResolver.TryResolveActionAnimationSelection(
                    player.characterDefinition,
                    actionName,
                    facingDirection,
                    out ActionSpriteAnimation animation,
                    out bool flipX))
            {
                selection = default;
                return false;
            }

            string resolvedDirection = string.IsNullOrWhiteSpace(animation.directionKey)
                ? ResolveDirectionKey(facingDirection)
                : animation.directionKey.Trim().ToLowerInvariant();
            selection = new AnimationSelection
            {
                animation = animation,
                flipX = flipX,
                clipKey = actionName + ":" + resolvedDirection + ":" + (flipX ? "flip" : "no_flip"),
            };
            return true;
        }

        private void ApplyDirectionalFallbackSprite(string actionName, int facingDirection)
        {
            if (TryResolveDirectionalFallbackFrame(actionName, facingDirection, out Sprite directionalSprite, out bool resolvedFlip))
            {
                ApplyResolvedSprite(directionalSprite, resolvedFlip);
                return;
            }

            ApplyFallbackSprite(facingDirection < 0);
        }

        private bool TryResolveDirectionalFallbackFrame(string actionName, int facingDirection, out Sprite sprite, out bool flipX)
        {
            flipX = false;

            foreach (string actionCandidate in EnumerateFallbackActions(actionName))
            {
                if (TryBuildSelection(actionCandidate, facingDirection, out AnimationSelection selection))
                {
                    Sprite resolvedFrame = ResolveFrameSprite(selection.animation, 0);
                    if (resolvedFrame != null)
                    {
                        sprite = resolvedFrame;
                        flipX = selection.flipX;
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
