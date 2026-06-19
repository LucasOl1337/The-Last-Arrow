using UnityEngine;

namespace ProjectPVP.Presentation
{
    public sealed class ProjectPvpCameraPresentation
    {
        private static readonly Color CameraFallbackColor = new Color(0.23f, 0.34f, 0.23f, 1f);
        private const float BackdropViewportPadding = 1.04f;

        private readonly Transform _environmentRoot;
        private Camera _presentationCamera;
        private SpriteRenderer _fittedBackdrop;

        public ProjectPvpCameraPresentation(Transform environmentRoot)
        {
            _environmentRoot = environmentRoot;
        }

        public void EnsurePresented()
        {
            _presentationCamera = ResolvePresentationCamera(_presentationCamera);
            if (_presentationCamera == null)
            {
                return;
            }

            _presentationCamera.rect = new Rect(0f, 0f, 1f, 1f);
            _presentationCamera.backgroundColor = CameraFallbackColor;
            FitBackdropToCamera(_presentationCamera);
        }

        private void FitBackdropToCamera(Camera targetCamera)
        {
            if (targetCamera == null || !targetCamera.orthographic)
            {
                return;
            }

            _fittedBackdrop = ResolveBackdropSprite(_fittedBackdrop);
            if (_fittedBackdrop == null || _fittedBackdrop.sprite == null)
            {
                return;
            }

            Vector3 backdropPosition = _fittedBackdrop.transform.position;
            _fittedBackdrop.transform.position = new Vector3(
                targetCamera.transform.position.x,
                targetCamera.transform.position.y,
                backdropPosition.z);

            Bounds bounds = _fittedBackdrop.bounds;
            if (bounds.size.x <= 0.001f || bounds.size.y <= 0.001f)
            {
                return;
            }

            float targetHeight = targetCamera.orthographicSize * 2f * BackdropViewportPadding;
            float targetWidth = targetHeight * Mathf.Max(0.01f, targetCamera.aspect);
            float scaleMultiplier = Mathf.Max(targetWidth / bounds.size.x, targetHeight / bounds.size.y);
            if (scaleMultiplier <= 1.001f)
            {
                return;
            }

            Transform backdropTransform = _fittedBackdrop.transform;
            Vector3 localScale = backdropTransform.localScale;
            backdropTransform.localScale = new Vector3(
                localScale.x * scaleMultiplier,
                localScale.y * scaleMultiplier,
                localScale.z);
        }

        private SpriteRenderer ResolveBackdropSprite(SpriteRenderer current)
        {
            if (current != null)
            {
                return current;
            }

            Transform environment = _environmentRoot;
            if (environment == null)
            {
                GameObject environmentObject = GameObject.Find("Environment");
                environment = environmentObject != null ? environmentObject.transform : null;
            }

            if (environment == null)
            {
                return null;
            }

            SpriteRenderer[] renderers = environment.GetComponentsInChildren<SpriteRenderer>(true);
            SpriteRenderer best = null;
            float bestArea = 0f;
            for (int index = 0; index < renderers.Length; index += 1)
            {
                SpriteRenderer candidate = renderers[index];
                if (candidate == null || candidate.sprite == null)
                {
                    continue;
                }

                string objectName = candidate.gameObject.name.ToLowerInvariant();
                if (!objectName.Contains("backg") && !objectName.Contains("background"))
                {
                    continue;
                }

                Vector3 size = candidate.bounds.size;
                float area = Mathf.Abs(size.x * size.y);
                if (best == null || area > bestArea)
                {
                    best = candidate;
                    bestArea = area;
                }
            }

            return best;
        }

        private static Camera ResolvePresentationCamera(Camera current)
        {
            if (current != null)
            {
                return current;
            }

            Camera mainCamera = Camera.main;
            if (mainCamera != null)
            {
                return mainCamera;
            }

            Camera[] cameras = Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);
            return cameras.Length > 0 ? cameras[0] : null;
        }
    }
}
