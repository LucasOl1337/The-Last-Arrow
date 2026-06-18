using System;
using System.Collections.Generic;
using ProjectPVP.Characters;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using RuntimeInput = UnityEngine.Input;

namespace ProjectPVP.Match
{
    internal sealed class ProjectPvpAscensionMenuOverlay : MonoBehaviour
    {
        private const float ReferenceWidth = 1920f;
        private const float ReferenceHeight = 1080f;

        private static readonly Color BackgroundColor = new Color(0.018f, 0.015f, 0.022f, 0.98f);
        private static readonly Color PanelColor = new Color(0.06f, 0.055f, 0.062f, 0.92f);
        private static readonly Color PanelDeepColor = new Color(0.025f, 0.022f, 0.03f, 0.95f);
        private static readonly Color GoldColor = new Color(0.96f, 0.76f, 0.32f, 1f);
        private static readonly Color WarmTextColor = new Color(1f, 0.94f, 0.82f, 1f);
        private static readonly Color MutedTextColor = new Color(0.78f, 0.78f, 0.78f, 1f);
        private static readonly Color SlotOneColor = new Color(0.20f, 0.74f, 0.92f, 1f);
        private static readonly Color SlotTwoColor = new Color(0.95f, 0.32f, 0.25f, 1f);
        private static readonly Color AiColor = new Color(0.86f, 0.48f, 0.94f, 1f);

        private readonly List<FocusRow> _focusRows = new List<FocusRow>(6);
        private readonly List<Image> _particles = new List<Image>(28);
        private readonly List<Vector2> _particleOrigins = new List<Vector2>(28);
        private readonly List<Sprite> _ownedSprites = new List<Sprite>(8);
        private readonly List<Texture2D> _ownedTextures = new List<Texture2D>(8);

        private MatchController _matchController;
        private ProjectPvpMenuSelection _selection;
        private IReadOnlyList<CharacterBootstrapProfile> _characters = Array.Empty<CharacterBootstrapProfile>();
        private Canvas _canvas;
        private CanvasGroup _canvasGroup;
        private RectTransform _modeFocus;
        private Text _modeText;
        private Text _modeFlavorText;
        private SlotView _slotOneView;
        private SlotView _slotTwoView;
        private RectTransform _startFocus;
        private Text _startText;
        private Sprite _panelSprite;
        private Sprite _softSprite;
        private Sprite _diamondSprite;
        private Sprite _vignetteSprite;
        private Sprite _fallbackPortraitSprite;
        private Font _font;
        private int _focusedRow;
        private float _age;
        private bool _visible;

        public bool IsVisible => _visible;

        public void Show(MatchController matchController, ProjectPvpMenuSelection selection)
        {
            _matchController = matchController;
            _selection = selection;
            _characters = matchController != null
                ? matchController.AvailableCharacters
                : Array.Empty<CharacterBootstrapProfile>();

            EnsureBuilt();
            RebuildFocusRows();
            _focusedRow = Mathf.Clamp(_focusedRow, 0, Mathf.Max(0, _focusRows.Count - 1));
            _age = 0f;
            _visible = true;
            _canvas.gameObject.SetActive(true);
            _canvasGroup.alpha = 0f;
            UpdateVisuals();
        }

        public void Hide()
        {
            _visible = false;
            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 0f;
            }

            if (_canvas != null)
            {
                _canvas.gameObject.SetActive(false);
            }
        }

        private void Update()
        {
            if (!_visible || _selection == null)
            {
                return;
            }

            _age += Time.unscaledDeltaTime;
            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = Mathf.MoveTowards(_canvasGroup.alpha, 1f, Time.unscaledDeltaTime * 4.5f);
            }

            HandleInput();
            AnimateParticles();
            AnimateFocus();
        }

        private void OnDestroy()
        {
            if (_canvas != null)
            {
                Destroy(_canvas.gameObject);
            }

            for (int index = 0; index < _ownedSprites.Count; index += 1)
            {
                if (_ownedSprites[index] != null)
                {
                    Destroy(_ownedSprites[index]);
                }
            }

            for (int index = 0; index < _ownedTextures.Count; index += 1)
            {
                if (_ownedTextures[index] != null)
                {
                    Destroy(_ownedTextures[index]);
                }
            }
        }

        private void EnsureBuilt()
        {
            if (_canvas != null)
            {
                return;
            }

            _font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            _panelSprite = CreatePanelSprite("MenuPanel", 64, 64, 8, new Color(1f, 1f, 1f, 1f), new Color(0.68f, 0.46f, 0.18f, 1f));
            _softSprite = CreateSoftSprite("SoftRect", 32, 32);
            _diamondSprite = CreateDiamondSprite();
            _vignetteSprite = CreateVignetteSprite();
            _fallbackPortraitSprite = CreateFallbackPortraitSprite();

            EnsureEventSystem();

            GameObject canvasObject = new GameObject("ProjectPvpAscensionMenu", typeof(RectTransform));
            canvasObject.transform.SetParent(transform, false);
            _canvas = canvasObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = short.MaxValue - 4;
            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(ReferenceWidth, ReferenceHeight);
            scaler.matchWidthOrHeight = 0.5f;
            canvasObject.AddComponent<GraphicRaycaster>();
            _canvasGroup = canvasObject.AddComponent<CanvasGroup>();

            RectTransform root = canvasObject.GetComponent<RectTransform>();
            Stretch(root);

            Image background = CreateImage(root, "Background", BackgroundColor, null);
            Stretch(background.rectTransform);

            Image backdrop = CreateImage(root, "ArenaBackdrop", new Color(0.12f, 0.11f, 0.12f, 0.45f), _softSprite);
            SetRect(backdrop.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1540f, 860f));
            backdrop.type = Image.Type.Sliced;

            BuildParticles(root);

            Image vignette = CreateImage(root, "Vignette", Color.white, _vignetteSprite);
            Stretch(vignette.rectTransform);
            vignette.raycastTarget = false;

            BuildTitle(root);
            BuildModeSelector(root);
            _slotOneView = BuildSlotView(root, CombatantSlotId.SlotOne, new Vector2(-430f, -65f), SlotOneColor);
            _slotTwoView = BuildSlotView(root, CombatantSlotId.SlotTwo, new Vector2(430f, -65f), SlotTwoColor);
            BuildStartButton(root);
            canvasObject.SetActive(false);
        }

        private void BuildTitle(RectTransform root)
        {
            Text title = CreateText(root, "Title", "THE LAST ARROW", 78, FontStyle.Bold, WarmTextColor, TextAnchor.MiddleCenter);
            SetRect(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -72f), new Vector2(980f, 92f));
            AddShadow(title.gameObject, new Color(0f, 0f, 0f, 0.75f), new Vector2(0f, -5f));

            Text subtitle = CreateText(root, "Subtitle", "ARROWFALL DUEL", 25, FontStyle.Bold, GoldColor, TextAnchor.MiddleCenter);
            SetRect(subtitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -138f), new Vector2(620f, 36f));
            subtitle.horizontalOverflow = HorizontalWrapMode.Overflow;
        }

        private void BuildModeSelector(RectTransform root)
        {
            RectTransform modeRoot = CreatePanel(root, "ModeSelector", new Vector2(0f, 218f), new Vector2(700f, 120f), PanelColor);
            _modeFocus = modeRoot;

            Text label = CreateText(modeRoot, "ModeLabel", "MODE", 17, FontStyle.Bold, MutedTextColor, TextAnchor.MiddleCenter);
            SetRect(label.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -18f), new Vector2(240f, 26f));

            Button leftButton = CreateIconButton(modeRoot, "ModeLeft", "<", new Vector2(-294f, -8f), () => CycleMode(-1));
            Button rightButton = CreateIconButton(modeRoot, "ModeRight", ">", new Vector2(294f, -8f), () => CycleMode(1));
            leftButton.navigation = new Navigation { mode = Navigation.Mode.None };
            rightButton.navigation = new Navigation { mode = Navigation.Mode.None };

            _modeText = CreateText(modeRoot, "ModeValue", string.Empty, 38, FontStyle.Bold, WarmTextColor, TextAnchor.MiddleCenter);
            SetRect(_modeText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 2f), new Vector2(480f, 52f));

            _modeFlavorText = CreateText(modeRoot, "ModeFlavor", string.Empty, 16, FontStyle.Bold, GoldColor, TextAnchor.MiddleCenter);
            SetRect(_modeFlavorText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 18f), new Vector2(560f, 28f));
        }

        private SlotView BuildSlotView(RectTransform root, CombatantSlotId slotId, Vector2 anchoredPosition, Color accent)
        {
            RectTransform slotRoot = CreatePanel(root, slotId + "Panel", anchoredPosition, new Vector2(580f, 590f), PanelDeepColor);
            Image accentStrip = CreateImage(slotRoot, "AccentStrip", accent, _softSprite);
            SetRect(accentStrip.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -8f), new Vector2(0f, 12f));
            accentStrip.type = Image.Type.Sliced;

            Text slotLabel = CreateText(slotRoot, "SlotLabel", slotId.ToDisplayName().ToUpperInvariant(), 24, FontStyle.Bold, WarmTextColor, TextAnchor.MiddleCenter);
            SetRect(slotLabel.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -48f), new Vector2(240f, 40f));

            RectTransform portraitFrame = CreatePanel(slotRoot, "PortraitFrame", new Vector2(0f, 80f), new Vector2(310f, 310f), new Color(0.015f, 0.015f, 0.018f, 0.95f));
            Image portraitGlow = CreateImage(portraitFrame, "PortraitGlow", new Color(accent.r, accent.g, accent.b, 0.18f), _softSprite);
            Stretch(portraitGlow.rectTransform);
            portraitGlow.type = Image.Type.Sliced;

            Image portrait = CreateImage(portraitFrame, "Portrait", Color.white, _fallbackPortraitSprite);
            SetRect(portrait.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(230f, 230f));
            portrait.preserveAspect = true;

            Button characterLeft = CreateIconButton(slotRoot, "CharacterLeft", "<", new Vector2(-224f, 82f), () => CycleCharacter(slotId, -1));
            Button characterRight = CreateIconButton(slotRoot, "CharacterRight", ">", new Vector2(224f, 82f), () => CycleCharacter(slotId, 1));
            characterLeft.navigation = new Navigation { mode = Navigation.Mode.None };
            characterRight.navigation = new Navigation { mode = Navigation.Mode.None };

            Text characterName = CreateText(slotRoot, "CharacterName", string.Empty, 33, FontStyle.Bold, WarmTextColor, TextAnchor.MiddleCenter);
            SetRect(characterName.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -122f), new Vector2(460f, 58f));
            AddShadow(characterName.gameObject, new Color(0f, 0f, 0f, 0.55f), new Vector2(0f, -3f));

            Text controlLabel = CreateText(slotRoot, "ControlLabel", "CONTROL", 16, FontStyle.Bold, MutedTextColor, TextAnchor.MiddleCenter);
            SetRect(controlLabel.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 126f), new Vector2(260f, 28f));

            Button controlButton = CreateTextButton(slotRoot, "ControlToggle", string.Empty, new Vector2(0f, 74f), new Vector2(260f, 60f), 24, () => ToggleSlotAi(slotId));
            controlButton.navigation = new Navigation { mode = Navigation.Mode.None };
            Text controlText = controlButton.GetComponentInChildren<Text>();

            return new SlotView
            {
                SlotId = slotId,
                AccentColor = accent,
                Root = slotRoot,
                CharacterFocus = portraitFrame,
                AiFocus = controlButton.GetComponent<RectTransform>(),
                Portrait = portrait,
                CharacterName = characterName,
                ControlButton = controlButton,
                ControlText = controlText,
            };
        }

        private void BuildStartButton(RectTransform root)
        {
            Button button = CreateTextButton(root, "StartButton", "START", new Vector2(0f, -438f), new Vector2(360f, 72f), 31, CommitStart);
            _startFocus = button.GetComponent<RectTransform>();
            _startText = button.GetComponentInChildren<Text>();
        }

        private void BuildParticles(RectTransform root)
        {
            _particles.Clear();
            _particleOrigins.Clear();
            for (int index = 0; index < 28; index += 1)
            {
                Image particle = CreateImage(root, "Spark" + index, new Color(0.96f, 0.72f, 0.28f, 0.18f), _diamondSprite);
                float x = -840f + ((index * 173) % 1680);
                float y = -430f + ((index * 97) % 860);
                float size = 5f + ((index * 7) % 16);
                SetRect(particle.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(x, y), new Vector2(size, size));
                particle.raycastTarget = false;
                _particles.Add(particle);
                _particleOrigins.Add(new Vector2(x, y));
            }
        }

        private void RebuildFocusRows()
        {
            _focusRows.Clear();
            _focusRows.Add(new FocusRow(_modeFocus, () => CycleMode(-1), () => CycleMode(1), () => CycleMode(1)));
            _focusRows.Add(new FocusRow(_slotOneView.CharacterFocus, () => CycleCharacter(CombatantSlotId.SlotOne, -1), () => CycleCharacter(CombatantSlotId.SlotOne, 1), () => CycleCharacter(CombatantSlotId.SlotOne, 1)));
            _focusRows.Add(new FocusRow(_slotOneView.AiFocus, () => ToggleSlotAi(CombatantSlotId.SlotOne), () => ToggleSlotAi(CombatantSlotId.SlotOne), () => ToggleSlotAi(CombatantSlotId.SlotOne)));
            _focusRows.Add(new FocusRow(_slotTwoView.CharacterFocus, () => CycleCharacter(CombatantSlotId.SlotTwo, -1), () => CycleCharacter(CombatantSlotId.SlotTwo, 1), () => CycleCharacter(CombatantSlotId.SlotTwo, 1)));
            _focusRows.Add(new FocusRow(_slotTwoView.AiFocus, () => ToggleSlotAi(CombatantSlotId.SlotTwo), () => ToggleSlotAi(CombatantSlotId.SlotTwo), () => ToggleSlotAi(CombatantSlotId.SlotTwo)));
            _focusRows.Add(new FocusRow(_startFocus, null, null, CommitStart));
        }

        private void HandleInput()
        {
            if (WasPressed(KeyCode.DownArrow) || WasPressed(KeyCode.S))
            {
                MoveFocus(1);
            }
            else if (WasPressed(KeyCode.UpArrow) || WasPressed(KeyCode.W))
            {
                MoveFocus(-1);
            }
            else if (WasPressed(KeyCode.LeftArrow) || WasPressed(KeyCode.A))
            {
                _focusRows[_focusedRow].Left?.Invoke();
            }
            else if (WasPressed(KeyCode.RightArrow) || WasPressed(KeyCode.D))
            {
                _focusRows[_focusedRow].Right?.Invoke();
            }
            else if (WasPressed(KeyCode.Return) || WasPressed(KeyCode.KeypadEnter) || WasPressed(KeyCode.Space) || WasPressed(KeyCode.JoystickButton0))
            {
                _focusRows[_focusedRow].Submit?.Invoke();
            }
        }

        private static bool WasPressed(KeyCode keyCode)
        {
            return keyCode != KeyCode.None && RuntimeInput.GetKeyDown(keyCode);
        }

        private void MoveFocus(int direction)
        {
            if (_focusRows.Count == 0)
            {
                return;
            }

            int next = (_focusedRow + direction) % _focusRows.Count;
            if (next < 0)
            {
                next += _focusRows.Count;
            }

            _focusedRow = next;
            AnimateFocus();
        }

        private void CycleMode(int direction)
        {
            ProjectPvpMenuSelectionService.ApplyGameMode(
                _selection,
                ProjectPvpMenuSelectionService.CycleMode(_selection.GameMode, direction));
            UpdateVisuals();
        }

        private void CycleCharacter(CombatantSlotId slotId, int direction)
        {
            ProjectPvpMenuSelectionService.CycleCharacter(_selection.GetSlot(slotId), _characters, direction);
            UpdateVisuals();
        }

        private void ToggleSlotAi(CombatantSlotId slotId)
        {
            ProjectPvpMenuSlotSelection slot = _selection.GetSlot(slotId);
            if (slot == null)
            {
                return;
            }

            slot.AiEnabled = !slot.AiEnabled;
            UpdateVisuals();
        }

        private void CommitStart()
        {
            if (_matchController == null || _selection == null)
            {
                return;
            }

            _matchController.BeginMatchFromMenu(_selection);
        }

        private void UpdateVisuals()
        {
            if (_selection == null)
            {
                return;
            }

            if (_modeText != null)
            {
                _modeText.text = ProjectPvpMenuSelectionService.ToDisplayName(_selection.GameMode);
            }

            if (_modeFlavorText != null)
            {
                _modeFlavorText.text = ResolveModeFlavor(_selection.GameMode);
            }

            UpdateSlotView(_slotOneView, _selection.GetSlot(CombatantSlotId.SlotOne));
            UpdateSlotView(_slotTwoView, _selection.GetSlot(CombatantSlotId.SlotTwo));
            AnimateFocus();
        }

        private void UpdateSlotView(SlotView view, ProjectPvpMenuSlotSelection slot)
        {
            if (view == null || slot == null)
            {
                return;
            }

            CharacterBootstrapProfile characterProfile = slot.CharacterProfile;
            string characterName = characterProfile != null ? characterProfile.ResolveDisplayName() : "NO CHARACTER";
            view.CharacterName.text = characterName.ToUpperInvariant();

            Sprite portrait = ResolvePortraitSprite(characterProfile);
            view.Portrait.sprite = portrait != null ? portrait : _fallbackPortraitSprite;
            view.Portrait.color = portrait != null ? Color.white : new Color(view.AccentColor.r, view.AccentColor.g, view.AccentColor.b, 0.72f);

            Text controlText = view.ControlText;
            if (controlText != null)
            {
                controlText.text = slot.AiEnabled ? "AI" : "HUMAN";
            }

            Image controlImage = view.ControlButton != null ? view.ControlButton.targetGraphic as Image : null;
            if (controlImage != null)
            {
                controlImage.color = slot.AiEnabled
                    ? new Color(AiColor.r, AiColor.g, AiColor.b, 0.86f)
                    : new Color(view.AccentColor.r, view.AccentColor.g, view.AccentColor.b, 0.72f);
            }
        }

        private void AnimateParticles()
        {
            for (int index = 0; index < _particles.Count; index += 1)
            {
                Image particle = _particles[index];
                if (particle == null)
                {
                    continue;
                }

                Vector2 origin = _particleOrigins[index];
                float phase = _age * (0.35f + (index % 5) * 0.08f) + index * 0.73f;
                particle.rectTransform.anchoredPosition = origin + new Vector2(Mathf.Sin(phase) * 18f, Mathf.Cos(phase * 0.7f) * 12f);
                Color color = particle.color;
                color.a = 0.10f + (Mathf.Sin(phase * 1.6f) + 1f) * 0.08f;
                particle.color = color;
            }
        }

        private void AnimateFocus()
        {
            for (int index = 0; index < _focusRows.Count; index += 1)
            {
                RectTransform target = _focusRows[index].Target;
                if (target == null)
                {
                    continue;
                }

                bool focused = index == _focusedRow;
                float pulse = focused ? 1f + (Mathf.Sin(_age * 7f) + 1f) * 0.012f : 1f;
                Vector3 targetScale = new Vector3(focused ? pulse : 1f, focused ? pulse : 1f, 1f);
                target.localScale = Vector3.Lerp(target.localScale, targetScale, Time.unscaledDeltaTime * 12f);

                Image image = target.GetComponent<Image>();
                if (image == null)
                {
                    continue;
                }

                if (focused)
                {
                    float pulseAmount = 0.18f + ((Mathf.Sin(_age * 7f) + 1f) * 0.06f);
                    image.color = Color.Lerp(_focusRows[index].BaseColor, new Color(GoldColor.r, GoldColor.g, GoldColor.b, _focusRows[index].BaseColor.a), pulseAmount);
                }
                else
                {
                    image.color = Color.Lerp(image.color, _focusRows[index].BaseColor, Time.unscaledDeltaTime * 12f);
                }
            }
        }

        private static string ResolveModeFlavor(ProjectPvpMenuGameMode mode)
        {
            return mode switch
            {
                ProjectPvpMenuGameMode.HumanVsAi => "ONE CHALLENGER, ONE HUNTER",
                ProjectPvpMenuGameMode.AiArena => "WATCH TWO BOTS TRADE ARROWS",
                _ => "LOCAL DUEL, TWO HUMAN PLAYERS",
            };
        }

        private static Sprite ResolvePortraitSprite(CharacterBootstrapProfile profile)
        {
            if (profile == null || profile.ResolveCharacterDefinition() == null)
            {
                return null;
            }

            return profile.ResolveCharacterDefinition().defaultSprite;
        }

        private RectTransform CreatePanel(RectTransform parent, string name, Vector2 anchoredPosition, Vector2 size, Color color)
        {
            Image panel = CreateImage(parent, name, color, _panelSprite);
            panel.type = Image.Type.Sliced;
            SetRect(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), anchoredPosition, size);
            return panel.rectTransform;
        }

        private Image CreateImage(Transform parent, string name, Color color, Sprite sprite)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform));
            obj.transform.SetParent(parent, false);
            Image image = obj.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private Text CreateText(Transform parent, string name, string text, int fontSize, FontStyle fontStyle, Color color, TextAnchor alignment)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform));
            obj.transform.SetParent(parent, false);
            Text label = obj.AddComponent<Text>();
            label.font = _font;
            label.text = text;
            label.fontSize = fontSize;
            label.fontStyle = fontStyle;
            label.color = color;
            label.alignment = alignment;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            label.raycastTarget = false;
            return label;
        }

        private Button CreateIconButton(RectTransform parent, string name, string glyph, Vector2 anchoredPosition, Action onClick)
        {
            Button button = CreateTextButton(parent, name, glyph, anchoredPosition, new Vector2(56f, 68f), 34, onClick);
            Text text = button.GetComponentInChildren<Text>();
            if (text != null)
            {
                text.color = GoldColor;
            }

            return button;
        }

        private Button CreateTextButton(RectTransform parent, string name, string label, Vector2 anchoredPosition, Vector2 size, int fontSize, Action onClick)
        {
            Image image = CreateImage(parent, name, new Color(0.15f, 0.11f, 0.08f, 0.86f), _panelSprite);
            image.type = Image.Type.Sliced;
            image.raycastTarget = true;
            SetRect(image.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), anchoredPosition, size);

            Button button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.ColorTint;
            button.colors = CreateButtonColors();
            if (onClick != null)
            {
                button.onClick.AddListener(() => onClick());
            }

            Text text = CreateText(image.rectTransform, "Label", label, fontSize, FontStyle.Bold, WarmTextColor, TextAnchor.MiddleCenter);
            Stretch(text.rectTransform);
            AddShadow(text.gameObject, new Color(0f, 0f, 0f, 0.6f), new Vector2(0f, -2f));
            return button;
        }

        private static ColorBlock CreateButtonColors()
        {
            ColorBlock colors = ColorBlock.defaultColorBlock;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 0.92f, 0.72f, 1f);
            colors.pressedColor = new Color(1f, 0.72f, 0.34f, 1f);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = new Color(0.35f, 0.35f, 0.35f, 0.45f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.08f;
            return colors;
        }

        private static void AddShadow(GameObject target, Color color, Vector2 distance)
        {
            Shadow shadow = target.AddComponent<Shadow>();
            shadow.effectColor = color;
            shadow.effectDistance = distance;
            shadow.useGraphicAlpha = true;
        }

        private static void Stretch(RectTransform rectTransform)
        {
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
        }

        private static void SetRect(
            RectTransform rectTransform,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 anchoredPosition,
            Vector2 sizeDelta)
        {
            rectTransform.anchorMin = anchorMin;
            rectTransform.anchorMax = anchorMax;
            rectTransform.pivot = pivot;
            rectTransform.anchoredPosition = anchoredPosition;
            rectTransform.sizeDelta = sizeDelta;
        }

        private Sprite CreatePanelSprite(string name, int width, int height, int border, Color center, Color edge)
        {
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Bilinear;
            for (int y = 0; y < height; y += 1)
            {
                for (int x = 0; x < width; x += 1)
                {
                    bool edgePixel = x < border || y < border || x >= width - border || y >= height - border;
                    bool innerLine = x == border || y == border || x == width - border - 1 || y == height - border - 1;
                    Color color = edgePixel ? edge : center;
                    if (innerLine)
                    {
                        color = Color.Lerp(center, GoldColor, 0.45f);
                    }

                    texture.SetPixel(x, y, color);
                }
            }

            texture.Apply();
            return RegisterSprite(Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), 32f, 0, SpriteMeshType.FullRect, new Vector4(border, border, border, border)), texture, name);
        }

        private Sprite CreateSoftSprite(string name, int width, int height)
        {
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Bilinear;
            for (int y = 0; y < height; y += 1)
            {
                for (int x = 0; x < width; x += 1)
                {
                    float edge = Mathf.Min(Mathf.Min(x, width - 1 - x), Mathf.Min(y, height - 1 - y));
                    float alpha = Mathf.Clamp01(edge / 5f);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            texture.Apply();
            return RegisterSprite(Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), 32f, 0, SpriteMeshType.FullRect, new Vector4(8f, 8f, 8f, 8f)), texture, name);
        }

        private Sprite CreateDiamondSprite()
        {
            int size = 32;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Bilinear;
            Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
            for (int y = 0; y < size; y += 1)
            {
                for (int x = 0; x < size; x += 1)
                {
                    float diamond = Mathf.Abs(x - center.x) + Mathf.Abs(y - center.y);
                    float alpha = Mathf.Clamp01(1f - ((diamond - 8f) / 6f));
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            texture.Apply();
            return RegisterSprite(Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 32f), texture, "Diamond");
        }

        private Sprite CreateVignetteSprite()
        {
            int width = 256;
            int height = 256;
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Bilinear;
            Vector2 center = new Vector2((width - 1) * 0.5f, (height - 1) * 0.5f);
            float maxDistance = Vector2.Distance(Vector2.zero, center);
            for (int y = 0; y < height; y += 1)
            {
                for (int x = 0; x < width; x += 1)
                {
                    float normalized = Vector2.Distance(new Vector2(x, y), center) / maxDistance;
                    float alpha = Mathf.SmoothStep(0f, 0.72f, normalized);
                    texture.SetPixel(x, y, new Color(0f, 0f, 0f, alpha));
                }
            }

            texture.Apply();
            return RegisterSprite(Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), 100f), texture, "Vignette");
        }

        private Sprite CreateFallbackPortraitSprite()
        {
            int size = 96;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Point;
            Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
            for (int y = 0; y < size; y += 1)
            {
                for (int x = 0; x < size; x += 1)
                {
                    float dx = Mathf.Abs(x - center.x);
                    float dy = Mathf.Abs(y - center.y);
                    bool hood = dy + dx * 0.7f < 34f && y > 22f;
                    bool body = y < 42f && dx < 18f + ((42f - y) * 0.45f);
                    Color color = hood || body ? new Color(0.95f, 0.82f, 0.42f, 1f) : Color.clear;
                    texture.SetPixel(x, y, color);
                }
            }

            texture.Apply();
            return RegisterSprite(Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 32f), texture, "FallbackPortrait");
        }

        private Sprite RegisterSprite(Sprite sprite, Texture2D texture, string name)
        {
            if (sprite != null)
            {
                sprite.name = name;
                _ownedSprites.Add(sprite);
            }

            if (texture != null)
            {
                texture.name = name + "Texture";
                _ownedTextures.Add(texture);
            }

            return sprite;
        }

        private static void EnsureEventSystem()
        {
            if (UnityEngine.Object.FindFirstObjectByType<EventSystem>() != null)
            {
                return;
            }

            GameObject eventSystemObject = new GameObject("EventSystem");
            eventSystemObject.AddComponent<EventSystem>();
            eventSystemObject.AddComponent<StandaloneInputModule>();
        }

        private sealed class FocusRow
        {
            public FocusRow(RectTransform target, Action left, Action right, Action submit)
            {
                Target = target;
                Left = left;
                Right = right;
                Submit = submit;
                Image image = target != null ? target.GetComponent<Image>() : null;
                BaseColor = image != null ? image.color : Color.white;
            }

            public RectTransform Target { get; }
            public Action Left { get; }
            public Action Right { get; }
            public Action Submit { get; }
            public Color BaseColor { get; }
        }

        private sealed class SlotView
        {
            public CombatantSlotId SlotId;
            public Color AccentColor;
            public RectTransform Root;
            public RectTransform CharacterFocus;
            public RectTransform AiFocus;
            public Image Portrait;
            public Text CharacterName;
            public Button ControlButton;
            public Text ControlText;
        }
    }
}
