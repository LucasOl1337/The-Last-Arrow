using UnityEngine;

namespace ProjectPVP.Match
{
    public sealed class ProjectPvpMatchRoundHudOverlay : MonoBehaviour
    {
        private const int DotCount = 5;

        private MatchController _matchController;
        private GUIStyle _ruleStyle;
        private GUIStyle _dotActiveStyle;
        private GUIStyle _dotInactiveStyle;
        private GUIStyle _winnerBoxStyle;
        private GUIStyle _winnerTextStyle;
        private GUIStyle _finalKillStyle;
        private GUIStyle _finalKillMarkerStyle;

        public void SetMatchController(MatchController matchController)
        {
            _matchController = matchController;
        }

        private void OnGUI()
        {
            if (_matchController == null)
            {
                return;
            }

            EnsureStyles();
            DrawRuleLabel();
            DrawRoundDots(new Rect(24f, 12f, 180f, 28f), _matchController.PlayerOneWins, true);
            DrawRoundDots(new Rect(Screen.width - 204f, 12f, 180f, 28f), _matchController.PlayerTwoWins, false);
            DrawWinnerBanner();
            DrawFinalKillLabel();
            DrawFinalKillMarker();
        }

        private void EnsureStyles()
        {
            if (_ruleStyle != null)
            {
                return;
            }

            _ruleStyle = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 15,
                fontStyle = FontStyle.Bold,
                normal =
                {
                    textColor = new Color(0.96f, 0.96f, 0.96f, 1f),
                    background = Texture2D.whiteTexture,
                },
                padding = new RectOffset(10, 10, 4, 4),
            };

            _dotActiveStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 22,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.9f, 0.18f, 0.18f, 1f) },
            };

            _dotInactiveStyle = new GUIStyle(_dotActiveStyle)
            {
                normal = { textColor = new Color(0.78f, 0.78f, 0.78f, 0.95f) },
            };

            _winnerBoxStyle = new GUIStyle(GUI.skin.box)
            {
                normal = { background = Texture2D.whiteTexture },
            };

            _winnerTextStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 24,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(1f, 0.96f, 0.96f, 1f) },
            };

            _finalKillStyle = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                normal =
                {
                    textColor = new Color(0.98f, 0.95f, 0.84f, 1f),
                    background = Texture2D.whiteTexture,
                },
                padding = new RectOffset(10, 10, 4, 4),
            };

            _finalKillMarkerStyle = new GUIStyle(GUI.skin.box)
            {
                normal =
                {
                    background = Texture2D.whiteTexture,
                },
            };
        }

        private void DrawRuleLabel()
        {
            Rect rect = new Rect((Screen.width * 0.5f) - 130f, 10f, 260f, 26f);
            Color previous = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.55f);
            GUI.Box(rect, $"PRIMEIRO A {_matchController.RoundsToChampion} KILLS", _ruleStyle);
            GUI.color = previous;
        }

        private void DrawRoundDots(Rect rect, int wins, bool leftToRight)
        {
            float spacing = 26f;
            float totalWidth = (DotCount - 1) * spacing;
            float startX = leftToRight ? rect.x : rect.x + rect.width - totalWidth - 18f;

            for (int index = 0; index < DotCount; index += 1)
            {
                Rect dotRect = new Rect(startX + (index * spacing), rect.y, 24f, rect.height);
                GUIStyle style = index < wins ? _dotActiveStyle : _dotInactiveStyle;
                GUI.Label(dotRect, "\u25CF", style);
            }
        }

        private void DrawWinnerBanner()
        {
            if (_matchController.ChampionAnnouncementSlot == CombatantSlotId.None)
            {
                return;
            }

            Rect rect = new Rect((Screen.width * 0.5f) - 210f, 42f, 420f, 44f);
            Color previous = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.68f);
            GUI.Box(rect, GUIContent.none, _winnerBoxStyle);
            GUI.color = Color.white;
            GUI.Label(rect, "VENCEDOR = " + _matchController.ResolveSlotDisplayName(_matchController.ChampionAnnouncementSlot), _winnerTextStyle);
            GUI.color = previous;
        }

        private void DrawFinalKillLabel()
        {
            if (!ShouldShowFinalKillInfo())
            {
                return;
            }

            float width = Mathf.Clamp(Screen.width - 48f, 240f, 520f);
            Rect rect = new Rect((Screen.width - width) * 0.5f, 92f, width, 28f);
            Color previous = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.68f);
            GUI.Box(rect, "ABATE FINAL: " + _matchController.LastRoundDeathSummary, _finalKillStyle);
            GUI.color = previous;
        }

        private void DrawFinalKillMarker()
        {
            if (!ShouldShowFinalKillInfo())
            {
                return;
            }

            Camera targetCamera = Camera.main != null ? Camera.main : Object.FindFirstObjectByType<Camera>();
            if (targetCamera == null)
            {
                return;
            }

            Vector3 screenPoint = targetCamera.WorldToScreenPoint(_matchController.LastRoundDeathPosition);
            if (screenPoint.z <= 0f)
            {
                return;
            }

            float y = Screen.height - screenPoint.y;
            Rect markerRect = new Rect(screenPoint.x - 9f, y - 9f, 18f, 18f);
            Color previous = GUI.color;
            GUI.color = new Color(0.95f, 0.2f, 0.2f, 0.9f);
            GUI.Box(markerRect, GUIContent.none, _finalKillMarkerStyle);
            GUI.color = previous;
        }

        private bool ShouldShowFinalKillInfo()
        {
            return !string.IsNullOrWhiteSpace(_matchController.LastRoundDeathSummary)
                && (
                    _matchController.PendingRoundWinnerSlot != CombatantSlotId.None
                    || _matchController.PendingChampionSlot != CombatantSlotId.None
                    || _matchController.ChampionAnnouncementSlot != CombatantSlotId.None
                    || _matchController.IsRoundResetPending
                    || _matchController.IsRespawnFreezeActive);
        }
    }
}
