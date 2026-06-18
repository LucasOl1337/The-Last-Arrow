using UnityEngine;

namespace ProjectPVP.Presentation
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class ProjectPvpCameraShake : MonoBehaviour
    {
        [SerializeField] private Vector3 _restLocalPosition;
        [SerializeField] private float _shakeElapsed;
        [SerializeField] private float _shakeDuration;
        [SerializeField] private float _shakeIntensity;
        [SerializeField] private bool _hasRestPose;

        public bool IsShaking => _shakeDuration > 0f && _shakeElapsed < _shakeDuration;
        public float ActiveIntensity => IsShaking ? _shakeIntensity : 0f;
        public float ActiveDuration => IsShaking ? _shakeDuration : 0f;

        private void Awake()
        {
            RefreshRestPose();
        }

        private void OnEnable()
        {
            RefreshRestPose();
        }

        public void Shake(float intensity, float duration)
        {
            if (intensity <= 0f || duration <= 0f)
            {
                return;
            }

            if (_shakeDuration > 0f && _shakeElapsed >= _shakeDuration)
            {
                RestoreRestPose();
            }

            if (!IsShaking)
            {
                RefreshRestPose();
            }

            _shakeElapsed = 0f;
            _shakeDuration = duration;
            _shakeIntensity = intensity;
            ApplyShakeOffset();
        }

        public void Tick(float deltaTime)
        {
            if (!_hasRestPose)
            {
                RefreshRestPose();
            }

            if (_shakeDuration <= 0f)
            {
                RefreshRestPose();
                RestoreRestPose();
                return;
            }

            if (deltaTime > 0f)
            {
                _shakeElapsed = Mathf.Min(_shakeDuration, _shakeElapsed + deltaTime);
            }

            if (_shakeElapsed >= _shakeDuration)
            {
                RestoreRestPose();
                return;
            }

            ApplyShakeOffset();
        }

        public static bool TryShakeDefault(float intensity, float duration)
        {
            ProjectPvpCameraShake authoredShake = Object.FindFirstObjectByType<ProjectPvpCameraShake>();
            if (authoredShake != null && authoredShake.GetComponent<Camera>() != null)
            {
                authoredShake.Shake(intensity, duration);
                return true;
            }

            Camera targetCamera = Camera.main;
            if (targetCamera == null)
            {
                targetCamera = Object.FindFirstObjectByType<Camera>();
            }

            if (targetCamera == null)
            {
                return false;
            }

            ProjectPvpCameraShake shake = targetCamera.GetComponent<ProjectPvpCameraShake>();
            if (shake == null)
            {
                shake = targetCamera.gameObject.AddComponent<ProjectPvpCameraShake>();
            }

            shake.Shake(intensity, duration);
            return true;
        }

        private void LateUpdate()
        {
            Tick(Time.deltaTime);
        }

        private void OnDisable()
        {
            RestoreRestPose();
        }

        private void OnDestroy()
        {
            RestoreRestPose();
        }

        private void RefreshRestPose()
        {
            _restLocalPosition = transform.localPosition;
            _hasRestPose = true;
        }

        private void RestoreRestPose()
        {
            if (!_hasRestPose)
            {
                return;
            }

            _shakeElapsed = 0f;
            _shakeDuration = 0f;
            _shakeIntensity = 0f;
            transform.localPosition = _restLocalPosition;
        }

        private void ApplyShakeOffset()
        {
            if (!_hasRestPose)
            {
                return;
            }

            float normalizedStrength = 1f - Mathf.Clamp01(_shakeElapsed / Mathf.Max(_shakeDuration, 0.0001f));
            float magnitude = _shakeIntensity * normalizedStrength;
            float x = (Mathf.Sin(0.37f + _shakeElapsed * 37f) * 0.65f + Mathf.Sin(1.21f + _shakeElapsed * 53f) * 0.35f) * magnitude;
            float y = (Mathf.Cos(0.83f + _shakeElapsed * 41f) * 0.6f + Mathf.Cos(1.97f + _shakeElapsed * 67f) * 0.4f) * magnitude;
            transform.localPosition = _restLocalPosition + new Vector3(x, y, 0f);
        }
    }
}
