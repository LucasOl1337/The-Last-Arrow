using System;
using UnityEngine;

namespace ProjectPVP.Presentation
{
    [DisallowMultipleComponent]
    public sealed class ProjectPvpKillImpactFx : MonoBehaviour
    {
        private const float DefaultDuration = 0.22f;
        private const float DefaultStartScale = 0.42f;
        private const float DefaultEndScale = 1.35f;
        private const int SortingOrder = 42;
        private const float SpinDegreesPerSecond = 260f;

        private static readonly Color DefaultImpactColor = new Color(1f, 0.7f, 0.32f, 1f);
        private static Sprite s_impactSprite;
        private static Texture2D s_impactTexture;

        [SerializeField] private SpriteRenderer _spriteRenderer;
        [SerializeField] private float _elapsed;
        [SerializeField] private float _duration = DefaultDuration;
        [SerializeField] private float _startScale = DefaultStartScale;
        [SerializeField] private float _endScale = DefaultEndScale;
        [SerializeField] private Color _baseColor = DefaultImpactColor;

        public SpriteRenderer SpriteRenderer => _spriteRenderer;
        public float Elapsed => _elapsed;
        public float Duration => _duration;
        public float EndScale => _endScale;
        public Color BaseColor => _baseColor;
        public bool IsFinished => _duration <= 0f || _elapsed >= _duration;

        public static ProjectPvpKillImpactFx SpawnDefault(Vector2 position, string cause)
        {
            GameObject root = new GameObject("KillImpactFx");
            root.transform.position = new Vector3(position.x, position.y, -0.05f);
            root.transform.localScale = Vector3.one * DefaultStartScale;

            SpriteRenderer spriteRenderer = root.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = ResolveImpactSprite();
            spriteRenderer.sortingOrder = SortingOrder;

            ProjectPvpKillImpactFx fx = root.AddComponent<ProjectPvpKillImpactFx>();
            fx.Configure(spriteRenderer, cause);
            return fx;
        }

        public static Color ResolveImpactColor(string cause)
        {
            string normalizedCause = string.IsNullOrWhiteSpace(cause) ? string.Empty : cause.Trim();
            if (string.Equals(normalizedCause, "Projectile", StringComparison.OrdinalIgnoreCase))
            {
                return new Color(1f, 0.96f, 0.28f, 1f);
            }

            if (string.Equals(normalizedCause, "Head Stomp", StringComparison.OrdinalIgnoreCase))
            {
                return new Color(0.5f, 0.94f, 1f, 1f);
            }

            if (string.Equals(normalizedCause, "Ring Out", StringComparison.OrdinalIgnoreCase))
            {
                return new Color(0.9f, 0.95f, 1f, 1f);
            }

            if (string.Equals(normalizedCause, "Ultimate", StringComparison.OrdinalIgnoreCase))
            {
                return new Color(1f, 0.36f, 0.82f, 1f);
            }

            return DefaultImpactColor;
        }

        public static void ResolveImpactProfile(string cause, out Color color, out float duration, out float endScale)
        {
            color = ResolveImpactColor(cause);
            duration = DefaultDuration;
            endScale = DefaultEndScale;

            string normalizedCause = string.IsNullOrWhiteSpace(cause) ? string.Empty : cause.Trim();
            if (string.Equals(normalizedCause, "Head Stomp", StringComparison.OrdinalIgnoreCase))
            {
                duration = 0.24f;
                endScale = 1.48f;
                return;
            }

            if (string.Equals(normalizedCause, "Ring Out", StringComparison.OrdinalIgnoreCase))
            {
                duration = 0.26f;
                endScale = 1.58f;
                return;
            }

            if (string.Equals(normalizedCause, "Ultimate", StringComparison.OrdinalIgnoreCase))
            {
                duration = 0.28f;
                endScale = 1.72f;
            }
        }

        public void Tick(float deltaTime)
        {
            if (_spriteRenderer == null)
            {
                _spriteRenderer = GetComponent<SpriteRenderer>();
            }

            if (_spriteRenderer == null)
            {
                Finish();
                return;
            }

            if (deltaTime > 0f)
            {
                _elapsed = Mathf.Min(_duration, _elapsed + deltaTime);
            }

            float normalized = _duration > 0f ? Mathf.Clamp01(_elapsed / _duration) : 1f;
            float eased = 1f - (1f - normalized) * (1f - normalized);
            transform.localScale = Vector3.one * Mathf.Lerp(_startScale, _endScale, eased);
            transform.Rotate(0f, 0f, SpinDegreesPerSecond * Mathf.Max(0f, deltaTime));

            Color color = _baseColor;
            color.a *= 1f - normalized;
            _spriteRenderer.color = color;

            if (normalized >= 1f)
            {
                Finish();
            }
        }

        private void Configure(SpriteRenderer spriteRenderer, string cause)
        {
            ResolveImpactProfile(cause, out _baseColor, out _duration, out _endScale);
            _spriteRenderer = spriteRenderer;
            _elapsed = 0f;
            _startScale = DefaultStartScale;
            transform.localScale = Vector3.one * _startScale;
            _spriteRenderer.color = _baseColor;
        }

        private void Update()
        {
            Tick(Time.deltaTime);
        }

        private void Finish()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                DestroyImmediate(gameObject);
                return;
            }
#endif
            Destroy(gameObject);
        }

        private static Sprite ResolveImpactSprite()
        {
            if (s_impactSprite != null)
            {
                return s_impactSprite;
            }

            s_impactTexture = new Texture2D(32, 32, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave,
            };

            Vector2 center = new Vector2(15.5f, 15.5f);
            for (int y = 0; y < s_impactTexture.height; y += 1)
            {
                for (int x = 0; x < s_impactTexture.width; x += 1)
                {
                    float dx = Mathf.Abs(x - center.x);
                    float dy = Mathf.Abs(y - center.y);
                    float radius = Mathf.Sqrt(dx * dx + dy * dy);
                    bool core = radius <= 3.5f;
                    bool cross = (dx <= 1.8f || dy <= 1.8f) && radius <= 15f;
                    bool diagonal = Mathf.Abs(dx - dy) <= 1.15f && radius <= 13.5f;
                    bool ring = radius >= 10.4f && radius <= 13.2f;

                    float alpha = core ? 1f : cross ? 0.9f : diagonal ? 0.72f : ring ? 0.42f : 0f;
                    s_impactTexture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            s_impactTexture.Apply();
            s_impactSprite = Sprite.Create(
                s_impactTexture,
                new Rect(0f, 0f, s_impactTexture.width, s_impactTexture.height),
                new Vector2(0.5f, 0.5f),
                32f);
            s_impactSprite.hideFlags = HideFlags.HideAndDontSave;
            return s_impactSprite;
        }
    }
}
