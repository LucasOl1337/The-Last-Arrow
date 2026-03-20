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

        private void OnGUI()
        {
            EnsureStyles();

            DrawSummaryPanel(new Rect(18f, 18f, 360f, 336f));

            if (showControls)
            {
                DrawControlsPanel(new Rect(18f, 364f, 360f, 260f));
            }

            if (showProjectNotes)
            {
                DrawNotesPanel(new Rect(Screen.width - 348f, 18f, 330f, 120f));
            }
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

            GUILayout.EndArea();
        }

        private void DrawControlsPanel(Rect rect)
        {
            GUILayout.BeginArea(rect, _panelStyle);
            GUILayout.Label("Controles de Teste", _titleStyle);
            GUILayout.Space(4f);
            GUILayout.Label("Slot 1: A/D mover, W/S mirar, Space pular, Q atirar, E melee, Left Shift dash", _bodyStyle);
            GUILayout.Space(4f);
            GUILayout.Label("Slot 2: Setas mover/mirar, Enter pular, Right Ctrl atirar, Right Shift melee, Keypad 0 dash", _bodyStyle);
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
                    "Wins: " + wins + "\n" +
                    "Character: " + configuredCharacterName;
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

            ICombatantInputSource inputSource = player.InputSource;
            PlayerInputFrame frame = inputSource != null ? inputSource.CurrentFrame : default;
            bool gamepadEnabled = inputSource != null && inputSource.ActiveGamepadSlot >= 0;
            int gamepadSlot = inputSource != null ? inputSource.ActiveGamepadSlot : -1;
            string gamepadStatus = !gamepadEnabled ? "Off" : gamepadSlot > 0 ? "On P" + gamepadSlot : "On?";
            Vector2 aimHoldDirection = player.AimHoldDirection;
            string faceButtonDebug = inputSource != null ? inputSource.FaceButtonDebug : "-";

            return fallbackName + " -> " + characterName + "\n" +
                "Wins: " + wins + "\n" +
                "Slot: " + player.SlotId.ToDisplayName() + "\n" +
                "Arrows: " + player.CurrentArrows + "\n" +
                "Facing: " + (player.Facing < 0 ? "Left" : "Right") + "\n" +
                "Grounded: " + (player.IsGrounded ? "Yes" : "No") + " | Wall: " + (player.IsTouchingWall ? "Yes" : "No") + "\n" +
                "Action: " + player.CurrentVisualActionKey + " | Dash: " + (player.IsDashAnimationActive ? "Yes" : "No") + "\n" +
                "AimHold: " + (player.IsAimHoldActive ? "Yes" : "No") + " | Vel: (" + player.HorizontalVelocity.ToString("0.0") + ", " + player.VerticalVelocity.ToString("0.0") + ")" + "\n" +
                "Melee: " + (player.IsMeleeActive ? "Yes" : "No") + " | Ult: " + (player.IsUltimateActive ? "Yes" : "No") + "\n" +
                "Parry: " + player.DashParryTimeLeft.ToString("0.00") + " | Gamepad: " + gamepadStatus + "\n" +
                "Axis: " + frame.axis.ToString("0.00") + "\n" +
                "Aim: (" + frame.aim.x.ToString("0.00") + ", " + frame.aim.y.ToString("0.00") + ")" + "\n" +
                "AimHoldDir: (" + aimHoldDirection.x.ToString("0.00") + ", " + aimHoldDirection.y.ToString("0.00") + ")" + "\n" +
                "FaceBtns: " + faceButtonDebug + "\n" +
                "Jump: " + (frame.jumpPressed ? "Pressed" : "-") + " | Shoot: " + (frame.shootHeld ? "Held" : "-") + " | UltBtn: " + (frame.ultimatePressed ? "Pressed" : "-") + " | DashBtn: " + ((frame.dashPrimaryPressed || frame.dashSecondaryPressed) ? "Pressed" : "-");
        }
    }
}
