using UnityEngine;
using UnityEngine.UI;

namespace ProjectPVP.Presentation
{
    internal static class ProjectPvpRuntimeUiFactory
    {
        public static Canvas CreateOverlayCanvas(string name, Transform parent = null, int sortingOrder = short.MaxValue)
        {
            GameObject canvasObject = new GameObject(name, typeof(RectTransform));
            if (parent != null)
            {
                canvasObject.transform.SetParent(parent, false);
            }

            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            canvasObject.AddComponent<CanvasScaler>();
            canvasObject.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        public static Font ResolveRuntimeFont()
        {
            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        public static RectTransform CreateAnchoredRoot(string name, Transform parent, Vector2 anchoredPosition, Vector2 anchorPoint, Vector2 sizeDelta)
        {
            GameObject rootObject = new GameObject(name, typeof(RectTransform));
            rootObject.transform.SetParent(parent, false);
            RectTransform root = rootObject.GetComponent<RectTransform>();
            root.anchorMin = anchorPoint;
            root.anchorMax = anchorPoint;
            root.pivot = anchorPoint;
            root.anchoredPosition = anchoredPosition;
            root.sizeDelta = sizeDelta;
            return root;
        }

        public static Image[] CreateDotRow(RectTransform root, Sprite dotSprite, int dotCount, bool leftToRight, Color inactiveColor)
        {
            Image[] dots = new Image[dotCount];
            for (int index = 0; index < dotCount; index += 1)
            {
                GameObject dotObject = new GameObject("Dot" + index, typeof(RectTransform));
                dotObject.transform.SetParent(root, false);
                Image image = dotObject.AddComponent<Image>();
                image.sprite = dotSprite;
                image.type = Image.Type.Simple;
                image.color = inactiveColor;

                RectTransform rect = image.rectTransform;
                rect.anchorMin = new Vector2(0f, 0.5f);
                rect.anchorMax = new Vector2(0f, 0.5f);
                rect.sizeDelta = new Vector2(10f, 10f);
                rect.anchoredPosition = new Vector2((leftToRight ? index : (dotCount - 1 - index)) * 18f, 0f);
                dots[index] = image;
            }

            return dots;
        }

        public static Sprite CreateDotSprite()
        {
            Texture2D texture = new Texture2D(16, 16, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Bilinear;
            Vector2 center = new Vector2(7.5f, 7.5f);
            const float radius = 7f;

            for (int y = 0; y < texture.height; y += 1)
            {
                for (int x = 0; x < texture.width; x += 1)
                {
                    float distance = Vector2.Distance(new Vector2(x, y), center);
                    texture.SetPixel(x, y, distance <= radius ? new Color(1f, 1f, 1f, 1f) : Color.clear);
                }
            }

            texture.Apply();
            return Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 16f);
        }
    }
}
