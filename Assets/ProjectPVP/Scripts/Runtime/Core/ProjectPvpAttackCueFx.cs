using UnityEngine;

namespace ProjectPVP.Presentation
{
    public enum ProjectPvpAttackCueKind
    {
        Melee = 0,
        Ultimate = 1,
    }

    [DisallowMultipleComponent]
    public sealed class ProjectPvpAttackCueFx : MonoBehaviour
    {
        private const float MinimumDuration = 0.04f;
        private const int SortingOrder = 38;

        private static readonly Color MeleeColor = new Color(1f, 0.82f, 0.24f, 0.42f);
        private static readonly Color UltimateColor = new Color(1f, 0.32f, 0.88f, 0.5f);
        private static Sprite s_boxSprite;
        private static Sprite s_ringSprite;
        private static Texture2D s_boxTexture;
        private static Texture2D s_ringTexture;

        [SerializeField] private SpriteRenderer _spriteRenderer;
        [SerializeField] private ProjectPvpAttackCueKind _kind;
        [SerializeField] private float _elapsed;
        [SerializeField] private float _duration;
        [SerializeField] private Color _baseColor;
        [SerializeField] private Vector3 _baseScale = Vector3.one;

        public SpriteRenderer SpriteRenderer => _spriteRenderer;
        public ProjectPvpAttackCueKind Kind => _kind;
        public float Duration => _duration;
        public Color BaseColor => _baseColor;
        public bool IsFinished => _duration <= 0f || _elapsed >= _duration;

        public static ProjectPvpAttackCueFx SpawnMelee(Vector2 center, Vector2 size, int facing, float duration)
        {
            Vector2 safeSize = new Vector2(Mathf.Max(1f, size.x), Mathf.Max(1f, size.y));
            ProjectPvpAttackCueFx fx = Spawn(
                "MeleeAttackCueFx",
                ProjectPvpAttackCueKind.Melee,
                ResolveBoxSprite(),
                center,
                safeSize,
                MeleeColor,
                duration);
            fx.transform.rotation = Quaternion.Euler(0f, 0f, facing < 0 ? -8f : 8f);
            return fx;
        }

        public static ProjectPvpAttackCueFx SpawnUltimate(Vector2 center, float radius, float duration)
        {
            float diameter = Mathf.Max(1f, radius * 2f);
            return Spawn(
                "UltimateAttackCueFx",
                ProjectPvpAttackCueKind.Ultimate,
                ResolveRingSprite(),
                center,
                new Vector2(diameter, diameter),
                UltimateColor,
                duration);
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
            float pulse = _kind == ProjectPvpAttackCueKind.Ultimate
                ? 1f + normalized * 0.08f
                : 1f + normalized * 0.04f;
            transform.localScale = _baseScale * pulse;

            Color color = _baseColor;
            color.a *= fade;
            _spriteRenderer.color = color;

            if (normalized >= 1f)
            {
                Finish();
            }
        }

        private static ProjectPvpAttackCueFx Spawn(
            string objectName,
            ProjectPvpAttackCueKind kind,
            Sprite sprite,
            Vector2 center,
            Vector2 scale,
            Color color,
            float duration)
        {
            GameObject root = new GameObject(objectName);
            root.transform.position = new Vector3(center.x, center.y, -0.07f);
            root.transform.localScale = new Vector3(scale.x, scale.y, 1f);

            SpriteRenderer spriteRenderer = root.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = sprite;
            spriteRenderer.color = color;
            spriteRenderer.sortingOrder = SortingOrder;

            ProjectPvpAttackCueFx fx = root.AddComponent<ProjectPvpAttackCueFx>();
            fx._spriteRenderer = spriteRenderer;
            fx._kind = kind;
            fx._duration = Mathf.Max(MinimumDuration, duration);
            fx._baseColor = color;
            fx._baseScale = root.transform.localScale;
            return fx;
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

        private static Sprite ResolveBoxSprite()
        {
            if (s_boxSprite != null)
            {
                return s_boxSprite;
            }

            s_boxTexture = new Texture2D(16, 16, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave,
            };

            for (int y = 0; y < s_boxTexture.height; y += 1)
            {
                for (int x = 0; x < s_boxTexture.width; x += 1)
                {
                    bool edge = x <= 1 || y <= 1 || x >= s_boxTexture.width - 2 || y >= s_boxTexture.height - 2;
                    s_boxTexture.SetPixel(x, y, edge ? Color.white : new Color(1f, 1f, 1f, 0.24f));
                }
            }

            s_boxTexture.Apply();
            s_boxSprite = Sprite.Create(s_boxTexture, new Rect(0f, 0f, 16f, 16f), new Vector2(0.5f, 0.5f), 16f);
            s_boxSprite.hideFlags = HideFlags.HideAndDontSave;
            return s_boxSprite;
        }

        private static Sprite ResolveRingSprite()
        {
            if (s_ringSprite != null)
            {
                return s_ringSprite;
            }

            s_ringTexture = new Texture2D(64, 64, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave,
            };

            Vector2 center = new Vector2(31.5f, 31.5f);
            for (int y = 0; y < s_ringTexture.height; y += 1)
            {
                for (int x = 0; x < s_ringTexture.width; x += 1)
                {
                    float distance = Vector2.Distance(new Vector2(x, y), center);
                    bool ring = distance >= 25f && distance <= 30f;
                    bool innerPulse = distance >= 17f && distance <= 18.5f;
                    float alpha = ring ? 1f : innerPulse ? 0.28f : 0f;
                    s_ringTexture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            s_ringTexture.Apply();
            s_ringSprite = Sprite.Create(s_ringTexture, new Rect(0f, 0f, 64f, 64f), new Vector2(0.5f, 0.5f), 64f);
            s_ringSprite.hideFlags = HideFlags.HideAndDontSave;
            return s_ringSprite;
        }
    }
}
