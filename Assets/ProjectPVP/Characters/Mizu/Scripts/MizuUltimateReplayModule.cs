using System;
using System.Collections.Generic;
using ProjectPVP.Data;
using ProjectPVP.Gameplay;
using UnityEngine;

namespace ProjectPVP.Characters.Mizu
{
    [CreateAssetMenu(fileName = "MizuUltimateReplayModule", menuName = "ProjectPVP/Characters/Mizu/Ultimate Replay Module")]
    public sealed class MizuUltimateReplayModule : CharacterMechanicsModule
    {
        private const string ReplaySceneAnchorName = "UltimateReplayHitbox";

        [Serializable]
        public sealed class ReplayAnimationSettings
        {
            public string actionName = "ult_replay";
            public string fallbackActionName = "ult";
            [Min(0.01f)] public float fallbackFramesPerSecond = 24f;
            public bool loop;
        }

        [Serializable]
        public sealed class ReplayMovementSettings
        {
            public bool mirrorX = true;
            public Vector2 localEndpoint = new Vector2(120f, 0f);
            [Min(0f)] public float startDelay = 0.1f;
            [Min(0f)] public float travelDuration = 0.5f;
        }

        [Serializable]
        public sealed class ReplayHitboxSettings
        {
            public bool enabled = true;
            public CombatShapeKind shapeKind = CombatShapeKind.Circle;
            public bool mirrorX = true;
            public Vector2 localOffset = new Vector2(104f, 21f);
            public Vector2 size = new Vector2(220f, 96f);
            [Min(0f)] public float radius = 126f;
            public float angle;
            public CapsuleDirection2D capsuleDirection = CapsuleDirection2D.Horizontal;

            public bool TryResolve(Vector2 rootPosition, int facingDirection, out CombatShapeSnapshot snapshot)
            {
                snapshot = default;
                if (!enabled)
                {
                    return false;
                }

                Vector2 resolvedOffset = localOffset;
                float resolvedAngle = angle;
                if (mirrorX && facingDirection < 0)
                {
                    resolvedOffset.x = -resolvedOffset.x;
                    resolvedAngle = -resolvedAngle;
                }

                snapshot = new CombatShapeSnapshot
                {
                    shapeKind = shapeKind,
                    center = rootPosition + resolvedOffset,
                    size = new Vector2(Mathf.Abs(size.x), Mathf.Abs(size.y)),
                    radius = Mathf.Max(0f, radius),
                    angle = resolvedAngle,
                    capsuleDirection = capsuleDirection,
                };

                if (shapeKind == CombatShapeKind.Circle)
                {
                    return snapshot.radius > 0.01f;
                }

                return snapshot.size.sqrMagnitude > 0.001f;
            }
        }

        [Header("Replay Animation")]
        public ReplayAnimationSettings replayAnimation = new ReplayAnimationSettings();

        [Header("Replay Movement")]
        public ReplayMovementSettings replayMovement = new ReplayMovementSettings();

        [Header("Replay Hitbox")]
        public ReplayHitboxSettings replayHitbox = new ReplayHitboxSettings();

        [Header("Shadow")]
        public Color shadowColor = new Color(0.92f, 1f, 1f, 1f);
        public Color glowColor = new Color(0.08f, 0.98f, 1f, 0.82f);
        public Vector3 glowScale = new Vector3(1.14f, 1.14f, 1f);
        public int shadowSortingOffset = 8;
        [Range(0f, 1f)] public float shadowStartAlpha = 1f;
        [Range(0f, 1f)] public float shadowEndAlpha = 0.78f;
        [Range(0f, 1f)] public float glowStartAlpha = 0.82f;
        [Range(0f, 1f)] public float glowEndAlpha = 0.34f;

        public override IEnumerable<string> GetAdditionalActionKeys(CharacterDefinition definition)
        {
            if (replayAnimation == null)
            {
                yield break;
            }

            if (!string.IsNullOrWhiteSpace(replayAnimation.actionName))
            {
                yield return replayAnimation.actionName.Trim();
            }

            if (!string.IsNullOrWhiteSpace(replayAnimation.fallbackActionName))
            {
                yield return replayAnimation.fallbackActionName.Trim();
            }
        }

        public override IEnumerable<CharacterMechanicsSceneAnchorDefinition> GetAdditionalSceneAnchors(PlayerController player, CharacterDefinition definition)
        {
            Vector2 anchorLocalPosition = ResolveReplayEndpointLocalPosition(definition);

            Vector2 colliderOffset = replayHitbox != null
                ? replayHitbox.localOffset - anchorLocalPosition
                : Vector2.zero;

            yield return new CharacterMechanicsSceneAnchorDefinition
            {
                childName = ReplaySceneAnchorName,
                anchorKind = PlayerCombatAnchorKind.UltimateReplayHitbox,
                shapeKind = replayHitbox != null ? replayHitbox.shapeKind : CombatShapeKind.Circle,
                localPosition = new Vector3(anchorLocalPosition.x, anchorLocalPosition.y, 0f),
                localEulerAngles = new Vector3(0f, 0f, replayHitbox != null ? replayHitbox.angle : 0f),
                colliderOffset = colliderOffset,
                boxSize = replayHitbox != null ? replayHitbox.size : new Vector2(220f, 96f),
                radius = replayHitbox != null ? replayHitbox.radius : 126f,
                capsuleDirection = replayHitbox != null ? replayHitbox.capsuleDirection : CapsuleDirection2D.Horizontal,
                mirrorX = replayMovement == null || replayMovement.mirrorX,
            };
        }

        public Vector2 ResolveReplayEndpointLocalPosition(CharacterDefinition definition)
        {
            if (definition != null && definition.ultimateReplayDashDistance > 0.01f)
            {
                return new Vector2(definition.ultimateReplayDashDistance, 0f);
            }

            Vector2 endpoint = replayMovement != null ? replayMovement.localEndpoint : Vector2.zero;
            if (endpoint.sqrMagnitude <= 0.001f)
            {
                if (definition != null && definition.ultimateDashDistance > 0.01f)
                {
                    endpoint = new Vector2(definition.ultimateDashDistance, 0f);
                }
                else if (replayHitbox != null)
                {
                    endpoint = new Vector2(replayHitbox.localOffset.x, 0f);
                }
            }

            return endpoint;
        }

        public Vector2 ResolveReplayRootEnd(Vector2 rootStart, int facingDirection, CharacterDefinition definition)
        {
            Vector2 endpoint = ResolveReplayEndpointLocalPosition(definition);
            bool mirrorX = replayMovement == null || replayMovement.mirrorX;
            if (mirrorX)
            {
                endpoint.x = Mathf.Abs(endpoint.x) * (facingDirection < 0 ? -1f : 1f);
            }

            return rootStart + endpoint;
        }

        public float ResolveReplayStartDelay(CharacterDefinition definition)
        {
            if (replayMovement != null)
            {
                return Mathf.Max(0f, replayMovement.startDelay);
            }

            return definition != null ? Mathf.Max(0f, definition.ultimateReplayDelay) : 0f;
        }

        public float ResolveReplayTravelDuration(CharacterDefinition definition)
        {
            if (definition != null && definition.ultimateReplayDashDuration > 0.01f)
            {
                return definition.ultimateReplayDashDuration;
            }

            if (replayMovement != null)
            {
                return Mathf.Max(0f, replayMovement.travelDuration);
            }

            return definition != null ? Mathf.Max(0f, definition.ultimateReplayDuration) : 0f;
        }

        public bool TryBuildReplayHitbox(Vector2 rootPosition, int facingDirection, out CombatShapeSnapshot snapshot)
        {
            snapshot = default;
            return replayHitbox != null && replayHitbox.TryResolve(rootPosition, facingDirection, out snapshot);
        }

        public bool CopyReplayHitboxFromAnchor(PlayerCombatAnchor anchor)
        {
            if (anchor == null)
            {
                return false;
            }

            Collider2D collider = anchor.AttachedCollider;
            if (collider == null)
            {
                return false;
            }

            if (replayHitbox == null)
            {
                replayHitbox = new ReplayHitboxSettings();
            }

            replayHitbox.enabled = true;
            replayHitbox.mirrorX = anchor.mirrorX;

            Vector2 anchorLocalPosition = anchor.transform.localPosition;
            Vector2 anchorScale = ResolveAbsoluteScale(anchor.transform.localScale);
            float resolvedAngle = NormalizeSignedAngle(anchor.transform.localEulerAngles.z);

            switch (collider)
            {
                case BoxCollider2D box:
                    replayHitbox.shapeKind = CombatShapeKind.Box;
                    replayHitbox.localOffset = anchorLocalPosition + Rotate(Vector2.Scale(box.offset, anchorScale), resolvedAngle);
                    replayHitbox.size = Vector2.Scale(box.size, anchorScale);
                    replayHitbox.radius = 0f;
                    replayHitbox.angle = resolvedAngle;
                    replayHitbox.capsuleDirection = CapsuleDirection2D.Horizontal;
                    return true;

                case CircleCollider2D circle:
                    replayHitbox.shapeKind = CombatShapeKind.Circle;
                    replayHitbox.localOffset = anchorLocalPosition + Rotate(Vector2.Scale(circle.offset, anchorScale), resolvedAngle);
                    replayHitbox.radius = circle.radius * Mathf.Max(anchorScale.x, anchorScale.y);
                    replayHitbox.size = Vector2.zero;
                    replayHitbox.angle = 0f;
                    replayHitbox.capsuleDirection = CapsuleDirection2D.Horizontal;
                    return true;

                case CapsuleCollider2D capsule:
                    replayHitbox.shapeKind = CombatShapeKind.Capsule;
                    replayHitbox.localOffset = anchorLocalPosition + Rotate(Vector2.Scale(capsule.offset, anchorScale), resolvedAngle);
                    replayHitbox.size = Vector2.Scale(capsule.size, anchorScale);
                    replayHitbox.radius = 0f;
                    replayHitbox.angle = resolvedAngle;
                    replayHitbox.capsuleDirection = capsule.direction;
                    return true;
            }

            return false;
        }

        public override CharacterMechanicsRuntime CreateRuntime(PlayerController player, CharacterDefinition definition)
        {
            return new Runtime(player, definition, this);
        }

        private static Vector2 ResolveAbsoluteScale(Vector3 scale)
        {
            return new Vector2(Mathf.Abs(scale.x), Mathf.Abs(scale.y));
        }

        private static float NormalizeSignedAngle(float angle)
        {
            return Mathf.DeltaAngle(0f, angle);
        }

        private static Vector2 Rotate(Vector2 value, float angleDegrees)
        {
            if (Mathf.Abs(angleDegrees) <= 0.001f)
            {
                return value;
            }

            float radians = angleDegrees * Mathf.Deg2Rad;
            float sin = Mathf.Sin(radians);
            float cos = Mathf.Cos(radians);
            return new Vector2(
                (value.x * cos) - (value.y * sin),
                (value.x * sin) + (value.y * cos));
        }

        private sealed class Runtime : CharacterMechanicsRuntime
        {
            private readonly MizuUltimateReplayModule _module;
            private readonly Collider2D[] _hits = new Collider2D[16];

            private Vector2 _replayRootStart;
            private Vector2 _replayRootEnd;
            private CombatShapeSnapshot _replayShape;
            private PlayerCombatAnchor _replaySceneAnchor;
            private Vector2 _ghostSpriteOffset;
            private Vector3 _ghostScale = Vector3.one;
            private Vector3 _ghostEulerAngles = Vector3.zero;
            private float _replayDelayLeft;
            private float _replayTimeLeft;
            private float _ghostAnimationFramesPerSecond = 12f;
            private int _replayFacing = 1;
            private bool _replayQueued;
            private bool _replayStarted;
            private bool _replayImpactPending;
            private bool _sceneReplayCaptured;
            private bool _ghostFlipX;
            private bool _ghostAnimationLoop;
            private ActionSpriteAnimation _ghostAnimation;
            private Sprite _ghostFallbackSprite;
            private int _ghostSortingLayerId;
            private int _ghostSortingOrder;
            private GameObject _ghostObject;
            private SpriteRenderer _ghostRenderer;
            private SpriteRenderer _ghostGlowRenderer;

            public Runtime(PlayerController player, CharacterDefinition definition, MizuUltimateReplayModule module)
                : base(player, definition)
            {
                _module = module;
            }

            public override void OnSpawned()
            {
                ResetReplayState();
            }

            public override void OnResetState()
            {
                ResetReplayState();
            }

            public override void OnKilled()
            {
                ResetReplayState();
            }

            public override void OnUltimateStarted()
            {
                ResetReplayState();
                _replayFacing = Player.ResolveFacingDirection();
                _replayRootStart = Player.RootPosition;
                _sceneReplayCaptured = TryCaptureReplaySceneShape(out _replayRootEnd, out _replayShape);
                if (!_sceneReplayCaptured)
                {
                    _replayRootEnd = _module.ResolveReplayRootEnd(_replayRootStart, _replayFacing, Definition);
                }
            }

            public override void OnUltimateImpactApplied()
            {
                if (!_sceneReplayCaptured && (_replayRootEnd - _replayRootStart).sqrMagnitude <= 0.001f)
                {
                    return;
                }

                if (!_sceneReplayCaptured)
                {
                    _replayRootEnd = _module.ResolveReplayRootEnd(_replayRootStart, _replayFacing, Definition);
                    if (!_module.TryBuildReplayHitbox(_replayRootEnd, _replayFacing, out _replayShape)
                        && !Player.TryCaptureUltimateHitShapeSnapshot(out _replayShape))
                    {
                        return;
                    }
                }

                CaptureGhostVisual();
                _replayDelayLeft = _module.ResolveReplayStartDelay(Definition);
                _replayTimeLeft = 0f;
                _replayQueued = true;
                _replayStarted = false;
                _replayImpactPending = true;
            }

            public override void OnTick(float deltaTime)
            {
                if (!_replayQueued)
                {
                    return;
                }

                if (!_replayStarted)
                {
                    if (_replayDelayLeft > 0f)
                    {
                        _replayDelayLeft = Mathf.Max(0f, _replayDelayLeft - deltaTime);
                        if (_replayDelayLeft > 0f)
                        {
                            return;
                        }
                    }

                    _replayStarted = true;
                    _replayTimeLeft = Mathf.Max(0f, _module.ResolveReplayTravelDuration(Definition));
                    StartGhost();
                }

                if (_replayTimeLeft <= 0f)
                {
                    FinishReplayImpact();
                    return;
                }

                _replayTimeLeft = Mathf.Max(0f, _replayTimeLeft - deltaTime);
                float replayDuration = Mathf.Max(0.01f, _module.ResolveReplayTravelDuration(Definition));
                float progress = 1f - (_replayTimeLeft / replayDuration);
                float elapsedTime = replayDuration - _replayTimeLeft;
                UpdateGhost(progress, elapsedTime);

                if (_replayImpactPending && progress >= 1f)
                {
                    ApplyReplayImpact();
                    _replayImpactPending = false;
                }

                if (_replayTimeLeft <= 0f)
                {
                    _replayQueued = false;
                    CleanupGhost();
                }
            }

            public override void DrawGizmos(bool selected)
            {
                bool activeReplay = _replayQueued || _replayDelayLeft > 0f || _replayTimeLeft > 0f || _replayImpactPending;
                if (!selected && (!Application.isPlaying || !activeReplay))
                {
                    return;
                }

                if (!TryResolvePreview(out Vector2 rootStart, out Vector2 rootEnd, out CombatShapeSnapshot finalShape))
                {
                    return;
                }

                Gizmos.color = new Color(1f, 0.72f, 0.18f, 0.9f);
                Gizmos.DrawLine(rootStart, rootEnd);
                Gizmos.DrawWireSphere(rootStart, 10f);
                Gizmos.DrawWireSphere(rootEnd, 10f);

                PlayerController.DrawShapeSnapshotGizmo(finalShape, new Color(0.2f, 0.95f, 1f, 0.98f));
                DrawImpactMarker(finalShape.center);

                if (TryResolveCurrentReplayProgress(out float replayProgress))
                {
                    Vector2 currentRoot = Vector2.Lerp(rootStart, rootEnd, replayProgress);
                    CombatShapeSnapshot currentShape = finalShape.Translate(currentRoot - rootEnd);
                    PlayerController.DrawShapeSnapshotGizmo(currentShape, new Color(0.94f, 1f, 1f, 1f));
                    Gizmos.color = new Color(0.94f, 1f, 1f, 1f);
                    Gizmos.DrawLine(currentRoot, currentShape.center);
                    DrawImpactMarker(currentShape.center);
                }
            }

            private void ResetReplayState()
            {
                _replayDelayLeft = 0f;
                _replayTimeLeft = 0f;
                _replayQueued = false;
                _replayStarted = false;
                _replayImpactPending = false;
                _ghostAnimation = null;
                _ghostFallbackSprite = null;
                _ghostAnimationFramesPerSecond = 12f;
                _ghostAnimationLoop = false;
                _sceneReplayCaptured = false;
                _replayFacing = Player.ResolveFacingDirection();
                CleanupGhost();
            }

            private void FinishReplayImpact()
            {
                if (_replayImpactPending)
                {
                    ApplyReplayImpact();
                    _replayImpactPending = false;
                }

                _replayQueued = false;
                CleanupGhost();
            }

            private void ApplyReplayImpact()
            {
                int hitCount = Player.CollectHitsForShape(_replayShape, _hits);
                Player.ApplyEliminationHits(_hits, hitCount);
            }

            private void CaptureGhostVisual()
            {
                SpriteRenderer visual = Player.VisualSpriteRenderer;
                if (visual == null)
                {
                    _ghostAnimation = null;
                    _ghostFallbackSprite = null;
                    return;
                }

                if (TryResolveReplayAnimation(out ActionSpriteAnimation ghostAnimation, out bool ghostFlipX, out float framesPerSecond, out bool loop))
                {
                    _ghostAnimation = ghostAnimation;
                    _ghostAnimationFramesPerSecond = framesPerSecond;
                    _ghostAnimationLoop = loop;
                    _ghostFallbackSprite = ResolveAnimationFrameByElapsed(ghostAnimation, 0f, framesPerSecond, loop, visual.sprite);
                    _ghostFlipX = ghostFlipX;
                }
                else
                {
                    _ghostAnimation = null;
                    _ghostFallbackSprite = visual.sprite;
                    _ghostFlipX = visual.flipX;
                    _ghostAnimationFramesPerSecond = 12f;
                    _ghostAnimationLoop = false;
                }

                if (_ghostFallbackSprite == null)
                {
                    return;
                }

                _ghostSortingLayerId = visual.sortingLayerID;
                _ghostSortingOrder = visual.sortingOrder + _module.shadowSortingOffset;
                _ghostScale = visual.transform.lossyScale;
                _ghostEulerAngles = visual.transform.eulerAngles;
                _ghostSpriteOffset = (Vector2)visual.transform.position - _replayRootEnd;
            }

            private bool TryResolveReplayAnimation(
                out ActionSpriteAnimation ghostAnimation,
                out bool ghostFlipX,
                out float framesPerSecond,
                out bool loop)
            {
                ghostAnimation = null;
                ghostFlipX = false;
                framesPerSecond = _module.replayAnimation != null && _module.replayAnimation.fallbackFramesPerSecond > 0.01f
                    ? _module.replayAnimation.fallbackFramesPerSecond
                    : 12f;
                loop = _module.replayAnimation != null && _module.replayAnimation.loop;

                string preferredActionName = _module.replayAnimation != null ? _module.replayAnimation.actionName : string.Empty;
                if (TryResolveReplayAnimationForAction(preferredActionName, preferredActionName, out ghostAnimation, out ghostFlipX, out framesPerSecond))
                {
                    return true;
                }

                string fallbackActionName = _module.replayAnimation != null ? _module.replayAnimation.fallbackActionName : string.Empty;
                return TryResolveReplayAnimationForAction(preferredActionName, fallbackActionName, out ghostAnimation, out ghostFlipX, out framesPerSecond);
            }

            private bool TryResolveReplayAnimationForAction(
                string preferredSpeedAction,
                string animationActionName,
                out ActionSpriteAnimation ghostAnimation,
                out bool ghostFlipX,
                out float framesPerSecond)
            {
                ghostAnimation = null;
                ghostFlipX = false;
                framesPerSecond = 12f;

                if (string.IsNullOrWhiteSpace(animationActionName)
                    || !Player.TryResolveActionAnimationSelection(animationActionName, _replayFacing, out ghostAnimation, out ghostFlipX))
                {
                    return false;
                }

                float clipFallbackFramesPerSecond = ghostAnimation.framesPerSecond > 0.01f
                    ? ghostAnimation.framesPerSecond
                    : (_module.replayAnimation != null && _module.replayAnimation.fallbackFramesPerSecond > 0.01f
                        ? _module.replayAnimation.fallbackFramesPerSecond
                        : 12f);

                framesPerSecond = ResolveConfiguredReplayAnimationSpeed(preferredSpeedAction, animationActionName, clipFallbackFramesPerSecond);
                return true;
            }

            private float ResolveConfiguredReplayAnimationSpeed(string preferredSpeedAction, string fallbackAction, float fallbackFramesPerSecond)
            {
                float configuredSpeed = ResolveActionSpeed(preferredSpeedAction);
                if (!float.IsNaN(configuredSpeed))
                {
                    return configuredSpeed;
                }

                configuredSpeed = ResolveActionSpeed(fallbackAction);
                if (!float.IsNaN(configuredSpeed))
                {
                    return configuredSpeed;
                }

                if (_module.replayAnimation != null && _module.replayAnimation.fallbackFramesPerSecond > 0.01f)
                {
                    return _module.replayAnimation.fallbackFramesPerSecond;
                }

                return fallbackFramesPerSecond > 0.01f ? fallbackFramesPerSecond : 12f;
            }

            private float ResolveActionSpeed(string actionName)
            {
                if (Definition == null || string.IsNullOrWhiteSpace(actionName))
                {
                    return float.NaN;
                }

                float resolvedValue = Definition.ResolveActionSpeed(actionName, float.NaN);
                return !float.IsNaN(resolvedValue) && resolvedValue > 0.01f
                    ? resolvedValue
                    : float.NaN;
            }

            private void StartGhost()
            {
                if (_ghostFallbackSprite == null)
                {
                    return;
                }

                EnsureGhostRenderers();
                _ghostObject.SetActive(true);
                _ghostRenderer.sprite = _ghostFallbackSprite;
                _ghostRenderer.flipX = _ghostFlipX;
                _ghostRenderer.sortingLayerID = _ghostSortingLayerId;
                _ghostRenderer.sortingOrder = _ghostSortingOrder;
                _ghostObject.transform.position = _replayRootStart + _ghostSpriteOffset;
                _ghostObject.transform.eulerAngles = _ghostEulerAngles;
                _ghostObject.transform.localScale = _ghostScale;
                _ghostRenderer.color = ApplyAlpha(_module.shadowColor, _module.shadowStartAlpha);

                Sprite initialFrame = ResolveAnimationFrameByElapsed(_ghostAnimation, 0f, _ghostAnimationFramesPerSecond, _ghostAnimationLoop, _ghostFallbackSprite);
                if (initialFrame != null)
                {
                    _ghostRenderer.sprite = initialFrame;
                }

                if (_ghostGlowRenderer != null)
                {
                    _ghostGlowRenderer.sprite = _ghostRenderer.sprite;
                    _ghostGlowRenderer.flipX = _ghostFlipX;
                    _ghostGlowRenderer.sortingLayerID = _ghostSortingLayerId;
                    _ghostGlowRenderer.sortingOrder = _ghostSortingOrder - 1;
                    _ghostGlowRenderer.color = ApplyAlpha(_module.glowColor, _module.glowStartAlpha);

                    Transform glowTransform = _ghostGlowRenderer.transform;
                    glowTransform.localPosition = Vector3.zero;
                    glowTransform.localEulerAngles = Vector3.zero;
                    glowTransform.localScale = _module.glowScale;
                    _ghostGlowRenderer.gameObject.SetActive(true);
                }
            }

            private void UpdateGhost(float progress, float elapsedTime)
            {
                if (_ghostObject == null || _ghostRenderer == null)
                {
                    return;
                }

                Vector2 rootPosition = Vector2.Lerp(_replayRootStart, _replayRootEnd, Mathf.Clamp01(progress));
                _ghostObject.transform.position = rootPosition + _ghostSpriteOffset;

                Sprite replayFrame = ResolveAnimationFrameByElapsed(_ghostAnimation, elapsedTime, _ghostAnimationFramesPerSecond, _ghostAnimationLoop, _ghostFallbackSprite);
                if (replayFrame != null)
                {
                    _ghostRenderer.sprite = replayFrame;
                    if (_ghostGlowRenderer != null)
                    {
                        _ghostGlowRenderer.sprite = replayFrame;
                    }
                }

                _ghostRenderer.color = ApplyAlpha(_module.shadowColor, Mathf.Lerp(_module.shadowStartAlpha, _module.shadowEndAlpha, Mathf.Clamp01(progress)));
                if (_ghostGlowRenderer != null)
                {
                    _ghostGlowRenderer.flipX = _ghostFlipX;
                    _ghostGlowRenderer.color = ApplyAlpha(_module.glowColor, Mathf.Lerp(_module.glowStartAlpha, _module.glowEndAlpha, Mathf.Clamp01(progress)));
                }
            }

            private void EnsureGhostRenderers()
            {
                if (_ghostObject == null)
                {
                    _ghostObject = new GameObject(Player.name + "_MizuUltimateReplayGhost");
                    _ghostRenderer = _ghostObject.AddComponent<SpriteRenderer>();
                }
                else if (_ghostRenderer == null)
                {
                    _ghostRenderer = _ghostObject.GetComponent<SpriteRenderer>();
                }

                if (_ghostGlowRenderer == null && _ghostObject != null)
                {
                    Transform glowTransform = _ghostObject.transform.Find("Glow");
                    if (glowTransform == null)
                    {
                        GameObject glowObject = new GameObject("Glow");
                        glowObject.transform.SetParent(_ghostObject.transform, false);
                        _ghostGlowRenderer = glowObject.AddComponent<SpriteRenderer>();
                    }
                    else
                    {
                        _ghostGlowRenderer = glowTransform.GetComponent<SpriteRenderer>();
                        if (_ghostGlowRenderer == null)
                        {
                            _ghostGlowRenderer = glowTransform.gameObject.AddComponent<SpriteRenderer>();
                        }
                    }
                }
            }

            private void CleanupGhost()
            {
                if (_ghostObject != null)
                {
                    _ghostObject.SetActive(false);
                }
            }

            private bool TryResolvePreview(out Vector2 rootStart, out Vector2 rootEnd, out CombatShapeSnapshot shape)
            {
                bool hasCapturedReplay = Application.isPlaying && (_replayQueued || _replayDelayLeft > 0f || _replayTimeLeft > 0f || _replayImpactPending);
                if (hasCapturedReplay)
                {
                    rootStart = _replayRootStart;
                    rootEnd = _replayRootEnd;
                    shape = _replayShape;
                    return IsValid(shape);
                }

                if (TryCaptureReplaySceneShape(out rootEnd, out shape))
                {
                    rootStart = Player.RootPosition;
                    return IsValid(shape);
                }

                Vector2 endpointOffset = _module.ResolveReplayEndpointLocalPosition(Definition);
                if (endpointOffset.sqrMagnitude <= 0.001f)
                {
                    rootStart = Player.RootPosition;
                    rootEnd = rootStart;
                    shape = default;
                    return false;
                }

                rootStart = Player.RootPosition;
                rootEnd = _module.ResolveReplayRootEnd(rootStart, Player.ResolveFacingDirection(), Definition);
                int previewFacing = Player.ResolveFacingDirection();

                if (_module.TryBuildReplayHitbox(rootEnd, previewFacing, out shape))
                {
                    return IsValid(shape);
                }

                if (!Player.TryCaptureUltimateHitShapeSnapshot(out CombatShapeSnapshot liveShape))
                {
                    shape = default;
                    return false;
                }

                shape = liveShape.Translate(rootEnd - rootStart);
                return IsValid(shape);
            }

            private bool TryResolveCurrentReplayProgress(out float replayProgress)
            {
                replayProgress = 0f;
                if (!Application.isPlaying || !_replayQueued)
                {
                    return false;
                }

                if (_replayDelayLeft > 0f)
                {
                    replayProgress = 0f;
                    return true;
                }

                if (!_replayStarted)
                {
                    replayProgress = 0f;
                    return true;
                }

                float replayDuration = Mathf.Max(0.01f, _module.ResolveReplayTravelDuration(Definition));
                replayProgress = 1f - (_replayTimeLeft / replayDuration);
                replayProgress = Mathf.Clamp01(replayProgress);
                return true;
            }

            private static bool IsValid(CombatShapeSnapshot shape)
            {
                if (shape.shapeKind == CombatShapeKind.Circle)
                {
                    return shape.radius > 0.01f;
                }

                return shape.size.sqrMagnitude > 0.001f;
            }

            private bool TryCaptureReplaySceneShape(out Vector2 rootEnd, out CombatShapeSnapshot snapshot)
            {
                rootEnd = Player.RootPosition;
                snapshot = default;

                PlayerCombatAnchor replayAnchor = ResolveReplaySceneAnchor();
                if (replayAnchor == null || replayAnchor.AttachedCollider == null)
                {
                    return false;
                }

                rootEnd = replayAnchor.transform.position;
                return TryCaptureShapeSnapshotFromCollider(replayAnchor.AttachedCollider, out snapshot);
            }

            private PlayerCombatAnchor ResolveReplaySceneAnchor()
            {
                if (_replaySceneAnchor != null)
                {
                    return _replaySceneAnchor;
                }

                Transform child = Player.transform.Find(ReplaySceneAnchorName);
                if (child == null)
                {
                    return null;
                }

                PlayerCombatAnchor anchor = child.GetComponent<PlayerCombatAnchor>();
                if (anchor == null || anchor.anchorKind != PlayerCombatAnchorKind.UltimateReplayHitbox)
                {
                    return null;
                }

                _replaySceneAnchor = anchor;
                return _replaySceneAnchor;
            }

            private static bool TryCaptureShapeSnapshotFromCollider(Collider2D collider, out CombatShapeSnapshot snapshot)
            {
                snapshot = default;
                if (collider == null)
                {
                    return false;
                }

                switch (collider)
                {
                    case BoxCollider2D box:
                        snapshot = new CombatShapeSnapshot
                        {
                            shapeKind = CombatShapeKind.Box,
                            center = box.transform.TransformPoint(box.offset),
                            size = ScaleAbsolute(box.size, box.transform.lossyScale),
                            angle = box.transform.eulerAngles.z,
                            capsuleDirection = CapsuleDirection2D.Horizontal,
                        };
                        return snapshot.size.sqrMagnitude > 0.001f;

                    case CircleCollider2D circle:
                        snapshot = new CombatShapeSnapshot
                        {
                            shapeKind = CombatShapeKind.Circle,
                            center = circle.transform.TransformPoint(circle.offset),
                            radius = circle.radius * Mathf.Max(Mathf.Abs(circle.transform.lossyScale.x), Mathf.Abs(circle.transform.lossyScale.y)),
                            capsuleDirection = CapsuleDirection2D.Horizontal,
                        };
                        return snapshot.radius > 0.01f;

                    case CapsuleCollider2D capsule:
                        snapshot = new CombatShapeSnapshot
                        {
                            shapeKind = CombatShapeKind.Capsule,
                            center = capsule.transform.TransformPoint(capsule.offset),
                            size = ScaleAbsolute(capsule.size, capsule.transform.lossyScale),
                            angle = capsule.transform.eulerAngles.z,
                            capsuleDirection = capsule.direction,
                        };
                        return snapshot.size.sqrMagnitude > 0.001f;

                    default:
                        return false;
                }
            }

            private static Vector2 ScaleAbsolute(Vector2 value, Vector3 lossyScale)
            {
                return new Vector2(
                    Mathf.Abs(value.x * lossyScale.x),
                    Mathf.Abs(value.y * lossyScale.y));
            }

            private static Color ApplyAlpha(Color color, float alpha)
            {
                color.a = Mathf.Clamp01(alpha);
                return color;
            }

            private static Sprite ResolveAnimationFrameByElapsed(
                ActionSpriteAnimation animation,
                float elapsedTime,
                float framesPerSecond,
                bool loop,
                Sprite fallbackSprite)
            {
                if (animation == null || animation.frames == null || animation.frames.Count == 0)
                {
                    return fallbackSprite;
                }

                float resolvedFramesPerSecond = framesPerSecond > 0.01f
                    ? framesPerSecond
                    : (animation.framesPerSecond > 0.01f ? animation.framesPerSecond : 12f);
                int frameCount = animation.frames.Count;
                int frameIndex = Mathf.FloorToInt(Mathf.Max(0f, elapsedTime) * resolvedFramesPerSecond);
                if (loop && frameCount > 0)
                {
                    frameIndex %= frameCount;
                }
                else
                {
                    frameIndex = Mathf.Clamp(frameIndex, 0, frameCount - 1);
                }

                Sprite resolvedFrame = animation.frames[frameIndex];
                if (resolvedFrame != null)
                {
                    return resolvedFrame;
                }

                for (int index = 0; index < frameCount; index += 1)
                {
                    if (animation.frames[index] != null)
                    {
                        return animation.frames[index];
                    }
                }

                return fallbackSprite;
            }

            private static void DrawImpactMarker(Vector2 center)
            {
                const float markerSize = 14f;
                Gizmos.DrawWireSphere(center, markerSize * 0.35f);
                Gizmos.DrawLine(center + Vector2.left * markerSize, center + Vector2.right * markerSize);
                Gizmos.DrawLine(center + Vector2.up * markerSize, center + Vector2.down * markerSize);
            }
        }
    }
}
