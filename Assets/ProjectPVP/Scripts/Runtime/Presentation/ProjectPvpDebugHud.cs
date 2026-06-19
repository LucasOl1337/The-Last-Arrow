using System.Collections.Generic;
using ProjectPVP.Gameplay;
using ProjectPVP.Input;
using ProjectPVP.Match;
using UnityEngine;
using UnityEngine.Serialization;

namespace ProjectPVP.Presentation
{
    public sealed class ProjectPvpDebugHud : MonoBehaviour
    {
        public MatchController matchController;
        [FormerlySerializedAs("playerOne")]
        [SerializeField] private PlayerController legacySlotOneController;
        [FormerlySerializedAs("playerTwo")]
        [SerializeField] private PlayerController legacySlotTwoController;
        public bool showControls = true;
        public bool showProjectNotes = true;

        private GUIStyle _panelStyle;
        private GUIStyle _titleStyle;
        private GUIStyle _bodyStyle;
        private GUIStyle _roundDotStyle;
        private GUIStyle _roundDotInactiveStyle;
        private GUIStyle _winnerBannerStyle;
        private GUIStyle _winnerTextStyle;

        private void OnGUI()
        {
            EnsureStyles();

            DrawSummaryPanel(new Rect(18f, 18f, 360f, 396f));

            if (showControls)
            {
                DrawControlsPanel(new Rect(18f, 424f, 360f, 260f));
            }

            if (showProjectNotes)
            {
                DrawNotesPanel(new Rect(Screen.width - 348f, 18f, 330f, 120f));
            }

            DrawBotControlPanel(new Rect(Screen.width - 348f, showProjectNotes ? 148f : 18f, 330f, 220f));
        }

        private void Start()
        {
            // Legacy IMGUI HUD only. The visible round overlay now comes from MatchController.
        }

        private void EnsureStyles()
        {
            if (_panelStyle != null)
            {
                return;
            }

            _panelStyle = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.UpperLeft,
                fontSize = 14,
                padding = new RectOffset(14, 14, 12, 12),
            };

            _titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white },
            };

            _bodyStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                wordWrap = true,
                normal = { textColor = new Color(0.9f, 0.94f, 1f) },
            };

            _roundDotStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18,
                normal = { textColor = new Color(0.76f, 0.14f, 0.14f, 0.95f) },
            };

            _roundDotInactiveStyle = new GUIStyle(_roundDotStyle)
            {
                normal = { textColor = new Color(0.08f, 0.08f, 0.08f, 0.78f) },
            };

            _winnerBannerStyle = new GUIStyle(GUI.skin.box)
            {
                normal =
                {
                    background = Texture2D.whiteTexture,
                },
                padding = new RectOffset(18, 18, 8, 8),
            };

            _winnerTextStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 24,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(1f, 0.94f, 0.94f) },
            };
        }

        private void DrawSummaryPanel(Rect rect)
        {
            GUILayout.BeginArea(rect, _panelStyle);
            GUILayout.Label("Project PVP Unity Slice", _titleStyle);
            GUILayout.Space(4f);
            bool drewAnySlot = false;
            foreach (string summary in BuildSlotSummaries())
            {
                if (drewAnySlot)
                {
                    GUILayout.Space(6f);
                }

                GUILayout.Label(summary, _bodyStyle);
                drewAnySlot = true;
            }

            if (!drewAnySlot)
            {
                GUILayout.Label("Nenhum slot configurado.", _bodyStyle);
            }

            if (matchController != null && matchController.IsRoundResetPending)
            {
                GUILayout.Space(6f);
                GUILayout.Label("Round reset em andamento...", _bodyStyle);
            }

            if (matchController != null)
            {
                GUILayout.Space(8f);
                GUILayout.Label(
                    "First to " + matchController.RoundsToChampion + " rounds" + "\n" +
                    "Respawn seed " + (matchController.CurrentRespawnSeedIndex + 1) + ": " + matchController.CurrentRespawnSeedLabel,
                    _bodyStyle);

                if (matchController.PendingChampionSlot != CombatantSlotId.None)
                {
                    GUILayout.Space(4f);
                    GUILayout.Label(matchController.PendingChampionSlot.ToDisplayName() + " e o campeao da serie.", _bodyStyle);
                }
                else if (matchController.PendingRoundWinnerSlot != CombatantSlotId.None)
                {
                    GUILayout.Space(4f);
                    GUILayout.Label(matchController.PendingRoundWinnerSlot.ToDisplayName() + " venceu o round.", _bodyStyle);
                }

                if (!string.IsNullOrWhiteSpace(matchController.LastRoundDeathSummary)
                    && (matchController.IsRoundResetPending
                        || matchController.PendingRoundWinnerSlot != CombatantSlotId.None
                        || matchController.PendingChampionSlot != CombatantSlotId.None
                        || matchController.ChampionAnnouncementSlot != CombatantSlotId.None))
                {
                    GUILayout.Space(4f);
                    GUILayout.Label("Abate final: " + matchController.LastRoundDeathSummary, _bodyStyle);
                }
            }

            GUILayout.EndArea();
        }

        private void DrawRoundCounters()
        {
            if (matchController == null)
            {
                return;
            }

            DrawRoundDots(new Rect(18f, 8f, 160f, 28f), matchController.PlayerOneWins, leftAligned: true);
            DrawRoundDots(new Rect(Screen.width - 178f, 8f, 160f, 28f), matchController.PlayerTwoWins, leftAligned: false);
        }

        private void DrawRoundDots(Rect rect, int wins, bool leftAligned)
        {
            const int dotCount = 5;
            float dotSpacing = 18f;
            float totalWidth = (dotCount - 1) * dotSpacing;
            float startX = leftAligned ? rect.x : rect.x + rect.width - totalWidth - 10f;

            for (int index = 0; index < dotCount; index += 1)
            {
                Rect dotRect = new Rect(startX + (index * dotSpacing), rect.y, 18f, rect.height);
                GUIStyle style = index < wins ? _roundDotStyle : _roundDotInactiveStyle;
                GUI.Label(dotRect, "\u25CF", style);
            }
        }

        private void DrawWinnerBanner()
        {
            if (matchController == null || matchController.ChampionAnnouncementSlot == CombatantSlotId.None)
            {
                return;
            }

            Rect rect = new Rect((Screen.width * 0.5f) - 180f, 16f, 360f, 46f);
            Color previousColor = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.62f);
            GUI.Box(rect, GUIContent.none, _winnerBannerStyle);
            GUI.color = Color.white;
            GUI.Label(rect, "VENCEDOR = " + matchController.ResolveSlotDisplayName(matchController.ChampionAnnouncementSlot), _winnerTextStyle);
            GUI.color = previousColor;
        }

        private void DrawControlsPanel(Rect rect)
        {
            GUILayout.BeginArea(rect, _panelStyle);
            GUILayout.Label("Controles de Teste", _titleStyle);
            GUILayout.Space(4f);
            GUILayout.Label("Slot 1: A/D mover, W/S mirar, Space pular, Q atirar, E melee, Left Shift dash", _bodyStyle);
            GUILayout.Space(4f);
            GUILayout.Label("Slot 2 (60%): I/J/K/L mover+mirar, Enter pular, Right Ctrl atirar, Right Alt melee, P ultimate, Right Shift/M dash", _bodyStyle);
            GUILayout.Space(4f);
            GUILayout.Label("Gamepad por slot: D-Pad ou Left Stick mover/mirar, Triangle ult, Circle melee, X pular, Square atirar, L1/L2/R1/R2 dash", _bodyStyle);
            GUILayout.Space(4f);
            GUILayout.Label("F3: alterna hitboxes, hurtboxes e probes (com Gizmos ligado na Scene ou Game).", _bodyStyle);
            GUILayout.EndArea();
        }

        private void DrawNotesPanel(Rect rect)
        {
            GUILayout.BeginArea(rect, _panelStyle);
            GUILayout.Label("Notas do Slice", _titleStyle);
            GUILayout.Space(4f);
            GUILayout.Label("Cena gerada por tooling de editor a partir do snapshot do Godot.", _bodyStyle);
            GUILayout.Space(4f);
            GUILayout.Label("Ao apertar Play, o editor deve iniciar direto na cena jogavel principal e focar a aba Game.", _bodyStyle);
            GUILayout.Space(4f);
            GUILayout.Label("Se o teclado nao responder, clique uma vez dentro da janela Game.", _bodyStyle);
            GUILayout.EndArea();
        }

        private void DrawBotControlPanel(Rect rect)
        {
            List<CodexBrokerCombatantInputSource> inputs = CollectBrokerInputs();
            if (inputs.Count == 0)
            {
                return;
            }

            GUILayout.BeginArea(rect, _panelStyle);
            GUILayout.Label("Bot Coach", _titleStyle);
            GUILayout.Space(4f);

            for (int index = 0; index < inputs.Count; index += 1)
            {
                CodexBrokerCombatantInputSource input = inputs[index];
                if (index > 0)
                {
                    GUILayout.Space(6f);
                }

                string owner = string.IsNullOrWhiteSpace(input.ControllerOwner) ? "-" : input.ControllerOwner;
                string session = string.IsNullOrWhiteSpace(input.SessionId)
                    ? "-"
                    : input.SessionId.Length > 8 ? input.SessionId.Substring(0, 8) : input.SessionId;
                GUILayout.Label(
                    "Slot " + input.slotId +
                    " | " + (input.useAgentDrivenMode ? "Agent" : "Direct") +
                    " | Owner " + owner +
                    " | Session " + session +
                    (input.ManualForceRefreshPending ? " | Replan pending" : string.Empty),
                    _bodyStyle);

                GUILayout.BeginHorizontal();
                if (GUILayout.Button(input.useAgentDrivenMode ? "Agent On" : "Agent Off"))
                {
                    input.SetAgentDrivenMode(!input.useAgentDrivenMode);
                }

                if (GUILayout.Button("Replan"))
                {
                    input.RequestImmediateReplan("debug_hud");
                }

                if (GUILayout.Button("Restart"))
                {
                    input.RestartBrokerSession("debug_hud");
                }

                GUILayout.EndHorizontal();

                if (!string.IsNullOrWhiteSpace(input.BotFeedback))
                {
                    GUILayout.Label(input.BotFeedback, _bodyStyle);
                }
            }

            GUILayout.EndArea();
        }

        private List<CodexBrokerCombatantInputSource> CollectBrokerInputs()
        {
            var inputs = new List<CodexBrokerCombatantInputSource>();
            if (matchController != null && matchController.Slots.Count > 0)
            {
                for (int index = 0; index < matchController.Slots.Count; index += 1)
                {
                    CombatantSlotConfig slot = matchController.Slots[index];
                    AddBrokerInput(inputs, slot != null ? slot.controller : null);
                }
            }
            else
            {
                AddBrokerInput(inputs, legacySlotOneController);
                AddBrokerInput(inputs, legacySlotTwoController);
            }

            return inputs;
        }

        private static void AddBrokerInput(List<CodexBrokerCombatantInputSource> inputs, PlayerController player)
        {
            if (player == null)
            {
                return;
            }

            CodexBrokerCombatantInputSource input = player.InputSource as CodexBrokerCombatantInputSource;
            if (input == null)
            {
                input = player.GetComponent<CodexBrokerCombatantInputSource>();
            }

            if (input != null && !inputs.Contains(input))
            {
                inputs.Add(input);
            }
        }

        private IEnumerable<string> BuildSlotSummaries()
        {
            if (matchController != null && matchController.Slots.Count > 0)
            {
                for (int index = 0; index < matchController.Slots.Count; index += 1)
                {
                    CombatantSlotConfig slot = matchController.Slots[index];
                    if (slot == null)
                    {
                        continue;
                    }

                    yield return BuildPlayerSummary(slot, matchController.GetWins(slot.slotId));
                }

                yield break;
            }

            yield return BuildLegacyPlayerSummary("Slot 1", legacySlotOneController, matchController != null ? matchController.PlayerOneWins : 0);
            yield return BuildLegacyPlayerSummary("Slot 2", legacySlotTwoController, matchController != null ? matchController.PlayerTwoWins : 0);
        }

        private static string BuildPlayerSummary(CombatantSlotConfig slot, int wins)
        {
            string fallbackName = slot != null ? slot.ResolveDisplayName() : "Slot";
            PlayerController player = slot != null ? slot.controller : null;
            if (player == null)
            {
                string configuredCharacterName = slot != null && slot.ResolveCharacterDefinition() != null && !string.IsNullOrWhiteSpace(slot.ResolveCharacterDefinition().displayName)
                    ? slot.ResolveCharacterDefinition().displayName
                    : "Sem personagem";
                return fallbackName + ": aguardando spawn\n" +
                    "Rounds: " + wins + "\n" +
                    "Character: " + configuredCharacterName + "\n" +
                    "Control: " + slot.ResolveControlMode().ToDisplayName();
            }

            return BuildLegacyPlayerSummary(fallbackName, player, wins);
        }

        private static string BuildLegacyPlayerSummary(string fallbackName, PlayerController player, int wins)
        {
            if (player == null)
            {
                return fallbackName + ": nao configurado";
            }

            string characterName = player.characterDefinition != null && !string.IsNullOrWhiteSpace(player.characterDefinition.displayName)
                ? player.characterDefinition.displayName
                : "Sem personagem";
            string controlMode = player.SlotProfile != null
                ? player.SlotProfile.ResolveControlMode().ToDisplayName()
                : CombatantControlMode.Human.ToDisplayName();
            if (player.SlotProfile != null && player.SlotProfile.ResolveControlMode() == CombatantControlMode.AI)
            {
                controlMode += " (" + player.SlotProfile.ResolveAiBrain() + ")";
            }

            ICombatantInputSource inputSource = player.InputSource;
            PlayerInputFrame frame = inputSource != null ? inputSource.CurrentFrame : default;
            bool gamepadEnabled = inputSource != null && inputSource.ActiveGamepadSlot >= 0;
            int gamepadSlot = inputSource != null ? inputSource.ActiveGamepadSlot : -1;
            string gamepadStatus = !gamepadEnabled ? "Off" : gamepadSlot > 0 ? "On P" + gamepadSlot : "On?";
            Vector2 aimHoldDirection = player.AimHoldDirection;
            string faceButtonDebug = inputSource != null ? inputSource.FaceButtonDebug : "-";
            string botFeedback = inputSource is IBotFeedbackInputSource botFeedbackSource
                ? botFeedbackSource.BotFeedback
                : string.Empty;
            ProjectileController lastProjectile = player.LastLaunchedProjectile;
            string assistEnabled = lastProjectile != null && lastProjectile.AssistEnabledRuntime ? "ON" : "OFF";
            string assistLocked = lastProjectile != null && lastProjectile.AssistTargetLocked ? "Yes" : "No";
            string assistAngle = lastProjectile != null ? lastProjectile.AssistCurrentAngleDeg.ToString("0.0") : "-";
            string assistAppliedStrength = lastProjectile != null ? lastProjectile.AssistAppliedStrength.ToString("0.00") : "-";
            string codexStatus = string.Empty;
            if (inputSource is CodexBrokerCombatantInputSource codexInput)
            {
                string shortSessionId = string.IsNullOrWhiteSpace(codexInput.SessionId)
                    ? "-"
                    : codexInput.SessionId.Length > 8 ? codexInput.SessionId.Substring(0, 8) : codexInput.SessionId;
                string intentAge = codexInput.IntentAgeMs < 0f ? "-" : codexInput.IntentAgeMs.ToString("0");
                codexStatus =
                    "CodexSession: " + shortSessionId + " | Owner: " + (string.IsNullOrWhiteSpace(codexInput.ControllerOwner) ? "-" : codexInput.ControllerOwner) + " | Source: " + codexInput.LastExecutorSource + "\n" +
                    "CodexIntent: " + (string.IsNullOrWhiteSpace(codexInput.CurrentIntentMode) ? "-" : codexInput.CurrentIntentMode) +
                    " | AgeMs: " + intentAge +
                    " | AgentAction: " + (codexInput.HasAgentAction ? "Yes" : "No") +
                    " | Start: " + (codexInput.IsSessionStarting ? "Yes" : "No") +
                    " | Tick: " + (codexInput.IsStrategyRequestInFlight ? "Busy" : "Idle") +
                    " | AgentMode: " + (codexInput.useAgentDrivenMode ? "On" : "Off") + "\n" +
                    "CodexWhy: " + (string.IsNullOrWhiteSpace(codexInput.CurrentIntentReason) ? "-" : codexInput.CurrentIntentReason) + "\n";
            }

            return fallbackName + " -> " + characterName + "\n" +
                "Rounds: " + wins + "\n" +
                "Slot: " + player.SlotId.ToDisplayName() + "\n" +
                "Control: " + controlMode + "\n" +
                "Arrows: " + player.CurrentArrows + "\n" +
                "Shield: " + (player.HasShield ? "Yes" : "No") + "\n" +
                "Facing: " + (player.Facing < 0 ? "Left" : "Right") + "\n" +
                "Grounded: " + (player.IsGrounded ? "Yes" : "No") + " | Wall: " + (player.IsTouchingWall ? "Yes" : "No") + "\n" +
                "Action: " + player.CurrentVisualActionKey + " | Dash: " + (player.IsDashAnimationActive ? "Yes" : "No") + "\n" +
                "AimHold: " + (player.IsAimHoldActive ? "Yes" : "No") + " | Vel: (" + player.HorizontalVelocity.ToString("0.0") + ", " + player.VerticalVelocity.ToString("0.0") + ")" + "\n" +
                "Melee: " + (player.IsMeleeActive ? "Yes" : "No") + " | Ult: " + (player.IsUltimateActive ? "Yes" : "No") + "\n" +
                "Parry: " + player.DashParryTimeLeft.ToString("0.00") + " | Gamepad: " + gamepadStatus + "\n" +
                "Axis: " + frame.axis.ToString("0.00") + "\n" +
                "Aim: (" + frame.aim.x.ToString("0.00") + ", " + frame.aim.y.ToString("0.00") + ")" + "\n" +
                "AimHoldDir: (" + aimHoldDirection.x.ToString("0.00") + ", " + aimHoldDirection.y.ToString("0.00") + ")" + "\n" +
                "DeathFlash: " + player.DeathFlashTimeLeft.ToString("0.00") + " | ProjAssist: " + assistEnabled + " | Locked: " + assistLocked + " | Angle: " + assistAngle + " | Strength: " + assistAppliedStrength + "\n" +
                codexStatus +
                (!string.IsNullOrWhiteSpace(botFeedback) ? "BotFeedback: " + botFeedback + "\n" : string.Empty) +
                "FaceBtns: " + faceButtonDebug + "\n" +
                "Jump: " + (frame.jumpPressed ? "Pressed" : "-") + " | Shoot: " + (frame.shootHeld ? "Held" : "-") + " | UltBtn: " + (frame.ultimatePressed ? "Pressed" : "-") + " | DashBtn: " + ((frame.dashPrimaryPressed || frame.dashSecondaryPressed) ? "Pressed" : "-");
        }
    }
}
