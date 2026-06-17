using UnityEngine;

namespace ProjectPVP.Presentation
{
    [DisallowMultipleComponent]
    public sealed class ProjectPvpParryCueFx : MonoBehaviour
    {
        private const float MinimumDuration = 0.04f;
        private const int SortingOrder = 44;

        private static readonly Color ParryColor = new Color(0.42f, 0.96f, 1f, 0.62f);
        private static Sprite s_ringSprite;
        private static Texture2D s_ringTexture;

        [SerializeField] private SpriteRenderer _spriteRenderer;
        [SerializeField] private float _elapsed;
        [SerializeField] private float _duration;
        [SerializeField] private Color _baseColor;
        [SerializeField] private Vector3 _baseScale = Vector3.one;

        public SpriteRenderer SpriteRenderer => _spriteRenderer;
        public float Duration => _duration;
        public Color BaseColor => _baseColor;
        public bool IsFinished => _duration <= 0f || _elapsed >= _duration;

        public static ProjectPvpParryCueFx Spawn(Vector2 center, float radius, float duration)
        {
            float diameter = Mathf.Max(1f, radius * 2f);
            GameObject root = new GameObject("ParryCueFx");
            root.transform.position = new Vector3(center.x, center.y, -0.09f);
            root.transform.localScale = new Vector3(diameter, diameter, 1f);

            SpriteRenderer spriteRenderer = root.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = ResolveRingSprite();
            spriteRenderer.color = ParryColor;
            spriteRenderer.sortingOrder = SortingOrder;

            ProjectPvpParryCueFx fx = root.AddComponent<ProjectPvpParryCueFx>();
            fx._spriteRenderer = spriteRenderer;
            fx._duration = Mathf.Max(MinimumDuration, duration);
            fx._baseColor = ParryColor;
            fx._baseScale = root.transform.localScale;
            return fx;
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
            float fade = 1f - normalized;
            float pulse = 1f + normalized * 0.16f;
            transform.localScale = _baseScale * pulse;

            Color color = _baseColor;
            color.a *= fade;
            _spriteRenderer.color = color;

            if (normalized >= 1f)
            {
                Finish();
            }
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

        private static Sprite ResolveRingSprite()
        {
            if (s_ringSprite != null)
            {
                return s_ringSprite;
            }

            s_ringTexture = new Texture2D(48, 48, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave,
            };

            Vector2 center = new Vector2(23.5f, 23.5f);
            for (int y = 0; y < s_ringTexture.height; y += 1)
            {
                for (int x = 0; x < s_ringTexture.width; x += 1)
                {
                    float distance = Vector2.Distance(new Vector2(x, y), center);
                    bool outerRing = distance >= 18f && distance <= 22f;
                    bool innerRing = distance >= 10.5f && distance <= 12.2f;
                    bool cross = (Mathf.Abs(x - center.x) <= 1.2f || Mathf.Abs(y - center.y) <= 1.2f) && distance <= 17f;
                    float alpha = outerRing ? 1f : innerRing ? 0.45f : cross ? 0.24f : 0f;
                    s_ringTexture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            s_ringTexture.Apply();
            s_ringSprite = Sprite.Create(s_ringTexture, new Rect(0f, 0f, 48f, 48f), new Vector2(0.5f, 0.5f), 48f);
            s_ringSprite.hideFlags = HideFlags.HideAndDontSave;
            return s_ringSprite;
        }
    }
}
