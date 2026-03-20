using System;
using System.IO;
using UnityEngine;
using UnityEngine.Video;

namespace ProjectPVP.Presentation
{
    [RequireComponent(typeof(Camera))]
    public sealed class ProjectPvpVideoBackground : MonoBehaviour
    {
        public string streamingAssetRelativePath = "Backgrounds/grok-video-e0c7a690-83f6-451b-82f4-49cfeca00285.mp4";
        public bool controlVideoPlayerUrl = false;
        public string directVideoUrl = string.Empty;
        public bool playOnStart = true;
        public bool loop = true;
        public bool muteAudio = true;
        public float playbackSpeed = 1f;
        public VideoAspectRatio aspectRatio = VideoAspectRatio.FitOutside;
        public bool hideEnvironmentSpriteBackgrounds = true;

        private VideoPlayer _videoPlayer;
        private SpriteRenderer[] _disabledSpriteRenderers = Array.Empty<SpriteRenderer>();

        private void Awake()
        {
            EnsureVideoPlayer();
            ConfigureVideoPlayer();
            ToggleEnvironmentBackgroundSprites(false);
        }

        private void OnEnable()
        {
            EnsureVideoPlayer();
            ConfigureVideoPlayer();
            ToggleEnvironmentBackgroundSprites(false);

            if (Application.isPlaying && playOnStart)
            {
                _videoPlayer.Prepare();
            }
        }

        private void OnDisable()
        {
            ToggleEnvironmentBackgroundSprites(true);
        }

        private void OnValidate()
        {
            if (!Application.isPlaying)
            {
                EnsureVideoPlayer();
                ConfigureVideoPlayer();
            }
        }

        private void EnsureVideoPlayer()
        {
            if (_videoPlayer == null)
            {
                _videoPlayer = GetComponent<VideoPlayer>();
            }

            if (_videoPlayer == null)
            {
                _videoPlayer = gameObject.AddComponent<VideoPlayer>();
            }

            _videoPlayer.errorReceived -= HandleVideoError;
            _videoPlayer.prepareCompleted -= HandlePrepared;
            _videoPlayer.errorReceived += HandleVideoError;
            _videoPlayer.prepareCompleted += HandlePrepared;
        }

        private void ConfigureVideoPlayer()
        {
            Camera targetCamera = GetComponent<Camera>();
            if (targetCamera == null || _videoPlayer == null)
            {
                return;
            }

            _videoPlayer.playOnAwake = false;
            _videoPlayer.waitForFirstFrame = true;
            _videoPlayer.isLooping = loop;
            _videoPlayer.skipOnDrop = true;
            _videoPlayer.playbackSpeed = playbackSpeed;
            _videoPlayer.renderMode = VideoRenderMode.CameraFarPlane;
            _videoPlayer.targetCamera = targetCamera;
            _videoPlayer.aspectRatio = aspectRatio;
            _videoPlayer.audioOutputMode = muteAudio ? VideoAudioOutputMode.None : VideoAudioOutputMode.Direct;

            if (controlVideoPlayerUrl || string.IsNullOrWhiteSpace(_videoPlayer.url))
            {
                _videoPlayer.source = VideoSource.Url;
                _videoPlayer.url = ResolveVideoUrl();
            }
        }

        private string ResolveVideoUrl()
        {
            if (!string.IsNullOrWhiteSpace(directVideoUrl))
            {
                return directVideoUrl.Trim();
            }

            string fullPath = Path.Combine(Application.streamingAssetsPath, streamingAssetRelativePath);
            return new Uri(fullPath).AbsoluteUri;
        }

        private void HandlePrepared(VideoPlayer source)
        {
            if (Application.isPlaying && playOnStart && source != null)
            {
                source.Play();
            }
        }

        private void HandleVideoError(VideoPlayer source, string message)
        {
            Debug.LogWarning($"Video background failed to load: {message}", this);
        }

        private void ToggleEnvironmentBackgroundSprites(bool visible)
        {
            if (!hideEnvironmentSpriteBackgrounds)
            {
                return;
            }

            Transform environmentRoot = transform.parent != null ? transform.parent.Find("Environment") : null;
            if (environmentRoot == null)
            {
                return;
            }

            Transform gameplayGreybox = environmentRoot.Find("Gameplay_Greybox");

            if (!visible)
            {
                var candidates = environmentRoot.GetComponentsInChildren<SpriteRenderer>(true);
                var disabled = new System.Collections.Generic.List<SpriteRenderer>();
                for (int index = 0; index < candidates.Length; index += 1)
                {
                    SpriteRenderer spriteRenderer = candidates[index];
                    if (spriteRenderer == null || !spriteRenderer.enabled)
                    {
                        continue;
                    }

                    if (gameplayGreybox != null && spriteRenderer.transform.IsChildOf(gameplayGreybox))
                    {
                        continue;
                    }

                    disabled.Add(spriteRenderer);
                    spriteRenderer.enabled = false;
                }

                _disabledSpriteRenderers = disabled.ToArray();
                return;
            }

            for (int index = 0; index < _disabledSpriteRenderers.Length; index += 1)
            {
                if (_disabledSpriteRenderers[index] != null)
                {
                    _disabledSpriteRenderers[index].enabled = true;
                }
            }

            _disabledSpriteRenderers = Array.Empty<SpriteRenderer>();
        }
    }
}
