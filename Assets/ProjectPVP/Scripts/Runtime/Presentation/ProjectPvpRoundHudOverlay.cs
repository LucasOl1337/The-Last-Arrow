using ProjectPVP.Match;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectPVP.Presentation
{
    public sealed class ProjectPvpRoundHudOverlay : MonoBehaviour
    {
        private const int DotCount = 5;
        private static readonly Color InactiveDotColor = new Color(0.08f, 0.08f, 0.08f, 0.82f);

        private MatchController _matchController;
        private Canvas _canvas;
        private Image[] _leftDots;
        private Image[] _rightDots;
        private Text _winnerText;
        private Text _ruleText;
        private Image _winnerBackground;
        private Sprite _dotSprite;

        public void SetMatchController(MatchController matchController)
        {
            _matchController = matchController;
            EnsureBuilt();
            UpdateHud();
        }

        private void Awake()
        {
            EnsureBuilt();
        }

        private void LateUpdate()
        {
            UpdateHud();
        }

        private void OnDestroy()
        {
            if (_canvas != null)
            {
                Object.Destroy(_canvas.gameObject);
            }

            if (_dotSprite != null)
            {
                Object.Destroy(_dotSprite.texture);
            }
        }

        private void EnsureBuilt()
        {
            if (_canvas != null)
            {
                return;
            }

            _canvas = ProjectPvpRuntimeUiFactory.CreateOverlayCanvas("RoundHudOverlay");
            Transform canvasTransform = _canvas.transform;

            _dotSprite = ProjectPvpRuntimeUiFactory.CreateDotSprite();
            _leftDots = ProjectPvpRuntimeUiFactory.CreateDotRow(CreateRoot(canvasTransform, "LeftRoundDots", new Vector2(18f, -16f), TextAnchor.UpperLeft), _dotSprite, DotCount, true, InactiveDotColor);
            _rightDots = ProjectPvpRuntimeUiFactory.CreateDotRow(CreateRoot(canvasTransform, "RightRoundDots", new Vector2(-18f, -16f), TextAnchor.UpperRight), _dotSprite, DotCount, false, InactiveDotColor);

            GameObject bannerObject = new GameObject("WinnerBanner", typeof(RectTransform));
            bannerObject.transform.SetParent(canvasTransform, false);
            _winnerBackground = bannerObject.AddComponent<Image>();
            _winnerBackground.color = new Color(0f, 0f, 0f, 0f);
            RectTransform bannerRect = _winnerBackground.rectTransform;
            bannerRect.anchorMin = new Vector2(0.5f, 1f);
            bannerRect.anchorMax = new Vector2(0.5f, 1f);
            bannerRect.pivot = new Vector2(0.5f, 1f);
            bannerRect.anchoredPosition = new Vector2(0f, -14f);
            bannerRect.sizeDelta = new Vector2(380f, 44f);

            GameObject textObject = new GameObject("WinnerText", typeof(RectTransform));
            textObject.transform.SetParent(bannerObject.transform, false);
            _winnerText = textObject.AddComponent<Text>();
            _winnerText.font = ProjectPvpRuntimeUiFactory.ResolveRuntimeFont();
            _winnerText.fontSize = 24;
            _winnerText.fontStyle = FontStyle.Bold;
            _winnerText.alignment = TextAnchor.MiddleCenter;
            _winnerText.color = new Color(1f, 0.94f, 0.94f, 1f);
            RectTransform textRect = _winnerText.rectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            GameObject ruleObject = new GameObject("RuleText", typeof(RectTransform));
            ruleObject.transform.SetParent(canvasTransform, false);
            _ruleText = ruleObject.AddComponent<Text>();
            _ruleText.font = ProjectPvpRuntimeUiFactory.ResolveRuntimeFont();
            _ruleText.fontSize = 16;
            _ruleText.fontStyle = FontStyle.Bold;
            _ruleText.alignment = TextAnchor.UpperCenter;
            _ruleText.color = new Color(0.92f, 0.92f, 0.92f, 0.92f);
            RectTransform ruleRect = _ruleText.rectTransform;
            ruleRect.anchorMin = new Vector2(0.5f, 1f);
            ruleRect.anchorMax = new Vector2(0.5f, 1f);
            ruleRect.pivot = new Vector2(0.5f, 1f);
            ruleRect.anchoredPosition = new Vector2(0f, -54f);
            ruleRect.sizeDelta = new Vector2(320f, 22f);

            UpdateHud();
        }

        private RectTransform CreateRoot(Transform parent, string name, Vector2 anchoredPosition, TextAnchor anchor)
        {
            GameObject rootObject = new GameObject(name, typeof(RectTransform));
            rootObject.transform.SetParent(parent, false);
            RectTransform root = rootObject.GetComponent<RectTransform>();
            Vector2 anchorPoint = anchor == TextAnchor.UpperLeft ? new Vector2(0f, 1f) : new Vector2(1f, 1f);
            root.anchorMin = anchorPoint;
            root.anchorMax = anchorPoint;
            root.pivot = anchorPoint;
            root.anchoredPosition = anchoredPosition;
            root.sizeDelta = new Vector2(140f, 24f);
            return root;
        }

        private void UpdateHud()
        {
            if (_matchController == null)
            {
                return;
            }

            if (_leftDots != null)
            {
                UpdateDots(_leftDots, _matchController.PlayerOneWins);
            }

            if (_rightDots != null)
            {
                UpdateDots(_rightDots, _matchController.PlayerTwoWins);
            }

            bool showWinner = _matchController.ChampionAnnouncementSlot != CombatantSlotId.None;
            if (_winnerBackground != null)
            {
                _winnerBackground.enabled = showWinner;
                _winnerBackground.color = new Color(0f, 0f, 0f, showWinner ? 0.62f : 0f);
            }

            if (_winnerText != null)
            {
                _winnerText.enabled = showWinner;
                _winnerText.text = showWinner
                    ? "VENCEDOR = " + _matchController.ResolveSlotDisplayName(_matchController.ChampionAnnouncementSlot)
                    : string.Empty;
            }

            if (_ruleText != null)
            {
                _ruleText.text = $"PRIMEIRO A {_matchController.RoundsToChampion} KILLS";
            }
        }

        private static void UpdateDots(Image[] dots, int wins)
        {
            for (int index = 0; index < dots.Length; index += 1)
            {
                if (dots[index] == null)
                {
                    continue;
                }

                dots[index].color = index < wins
                    ? new Color(0.86f, 0.12f, 0.12f, 0.95f)
                    : InactiveDotColor;
            }
        }
    }
}
