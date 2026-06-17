using System.Collections.Generic;
using ProjectPVP.Gameplay;
using ProjectPVP.Input;
using UnityEngine;

namespace ProjectPVP.Match
{
    public sealed class ProjectPvpMatchRoundHudOverlay : MonoBehaviour
    {
        private const int DotCount = 5;
        private const int MaxBotCoachLines = 2;
        private const int MaxBotCoachFeedbackChars = 118;

        private MatchController _matchController;
        private GUIStyle _ruleStyle;
        private GUIStyle _dotActiveStyle;
        private GUIStyle _dotInactiveStyle;
        private GUIStyle _winnerBoxStyle;
        private GUIStyle _winnerTextStyle;
        private GUIStyle _finalKillStyle;
        private GUIStyle _finalKillMarkerStyle;
        private GUIStyle _botCoachBoxStyle;
        private GUIStyle _botCoachHeaderStyle;
        private GUIStyle _botCoachLineStyle;

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
            DrawBotCoachPanel();
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

            _botCoachBoxStyle = new GUIStyle(GUI.skin.box)
            {
                normal =
                {
                    background = Texture2D.whiteTexture,
                },
                padding = new RectOffset(12, 12, 8, 8),
            };

            _botCoachHeaderStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleLeft,
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.98f, 0.96f, 0.86f, 1f) },
            };

            _botCoachLineStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleLeft,
                fontSize = 12,
                wordWrap = false,
                clipping = TextClipping.Clip,
                normal = { textColor = new Color(0.9f, 0.94f, 1f, 1f) },
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

        private void DrawBotCoachPanel()
        {
            List<string> lines = BuildBotCoachLines();
            if (lines.Count == 0)
            {
                return;
            }

            float width = Mathf.Clamp(Screen.width - 48f, 280f, 760f);
            float height = 28f + (lines.Count * 22f);
            Rect panelRect = new Rect((Screen.width - width) * 0.5f, Screen.height - height - 18f, width, height);

            Color previous = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.68f);
            GUI.Box(panelRect, GUIContent.none, _botCoachBoxStyle);
            GUI.color = Color.white;
            GUI.Label(new Rect(panelRect.x + 12f, panelRect.y + 5f, panelRect.width - 24f, 18f), "BOT COACH", _botCoachHeaderStyle);

            for (int index = 0; index < lines.Count; index += 1)
            {
                Rect lineRect = new Rect(panelRect.x + 12f, panelRect.y + 24f + (index * 22f), panelRect.width - 24f, 20f);
                GUI.Label(lineRect, lines[index], _botCoachLineStyle);
            }

            GUI.color = previous;
        }

        private List<string> BuildBotCoachLines()
        {
            var lines = new List<string>(MaxBotCoachLines);
            IReadOnlyList<CombatantSlotConfig> slots = _matchController.Slots;
            for (int index = 0; index < slots.Count && lines.Count < MaxBotCoachLines; index += 1)
            {
                CombatantSlotConfig slot = slots[index];
                PlayerController player = slot != null ? slot.controller : null;
                ICombatantInputSource inputSource = player != null ? player.InputSource : null;
                string displayName = slot != null ? slot.ResolveBotDisplayName() : string.Empty;
                if (TryBuildBotCoachLine(displayName, inputSource, MaxBotCoachFeedbackChars, out string line))
                {
                    lines.Add(line);
                }
            }

            return lines;
        }

        private static bool TryBuildBotCoachLine(string displayName, ICombatantInputSource inputSource, int maxFeedbackChars, out string line)
        {
            line = string.Empty;
            if (inputSource is not IBotFeedbackInputSource feedbackSource)
            {
                return false;
            }

            string feedback = NormalizeBotFeedback(feedbackSource.BotFeedback);
            if (string.IsNullOrWhiteSpace(feedback))
            {
                return false;
            }

            int safeMax = Mathf.Max(3, maxFeedbackChars);
            if (feedback.Length > safeMax)
            {
                feedback = feedback.Substring(0, safeMax - 3).TrimEnd() + "...";
            }

            string label = string.IsNullOrWhiteSpace(displayName) ? "Bot" : displayName.Trim();
            line = label + ": " + feedback;
            return true;
        }

        private static string NormalizeBotFeedback(string feedback)
        {
            if (string.IsNullOrWhiteSpace(feedback))
            {
                return string.Empty;
            }

            return feedback
                .Replace('\r', ' ')
                .Replace('\n', ' ')
                .Replace('\t', ' ')
                .Trim();
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
