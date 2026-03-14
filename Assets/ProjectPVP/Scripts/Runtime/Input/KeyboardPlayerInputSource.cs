using System;
using LegacyInput = UnityEngine.Input;
using UnityEngine;

namespace ProjectPVP.Input
{
    public sealed class KeyboardPlayerInputSource : MonoBehaviour
    {
        private const int MaxSupportedGamepads = 4;
        private const string DefaultGamepadProfileResourcePath = "ProjectPVP/Input/DefaultGamepadControlProfile";
        private static readonly GamepadBindingLabel[] AllBoundGamepadLabels =
        {
            GamepadBindingLabel.X,
            GamepadBindingLabel.Square,
            GamepadBindingLabel.Circle,
            GamepadBindingLabel.Triangle,
            GamepadBindingLabel.L1,
            GamepadBindingLabel.R1,
            GamepadBindingLabel.L2,
            GamepadBindingLabel.R2,
        };

        private static readonly GamepadBindingLabel[] DashPrimaryLabels =
        {
            GamepadBindingLabel.L1,
            GamepadBindingLabel.R1,
        };

        private static readonly GamepadBindingLabel[] DashSecondaryLabels =
        {
            GamepadBindingLabel.L2,
            GamepadBindingLabel.R2,
        };

        [Min(1)] public int playerId = 1;
        public bool usePlayerDefaults = true;
        public PlayerActionMap actionMap = PlayerActionMap.CreateDefaultPlayerOne();
        public GamepadControlProfileAsset gamepadProfile;
        public bool enableGamepad;
        [Min(0)] public int preferredGamepadIndex;
        public GamepadActionMap gamepadActionMap = new GamepadActionMap();
        [Range(0.01f, 0.25f)] public float buttonBufferSeconds = 0.1f;

        private int _frameIndex;
        private PlayerInputFrame _currentFrame;
        private float _jumpBufferLeft;
        private float _shootBufferLeft;
        private float _meleeBufferLeft;
        private float _ultimateBufferLeft;
        private float _dashPrimaryBufferLeft;
        private float _dashSecondaryBufferLeft;
        private int _activeGamepadSlot = -1;
        private EditableGamepadBindings _editableGamepadBindings;
        private readonly int[] _connectedGamepadSlots = new int[MaxSupportedGamepads];
        private static GamepadControlProfileAsset s_defaultGamepadProfile;

        public PlayerInputFrame CurrentFrame => _currentFrame;
        public int ActiveGamepadSlot => _activeGamepadSlot;
        public string FaceButtonDebug => BuildFaceButtonDebugSummary();

        private void Awake()
        {
            ApplyDefaultsIfNeeded();
        }

        private void OnEnable()
        {
            ApplyDefaultsIfNeeded();
        }

        private void Reset()
        {
            ApplyDefaultsIfNeeded();
        }

        private void OnValidate()
        {
            ApplyDefaultsIfNeeded();
        }

        private void Update()
        {
            TickBuffers(Time.unscaledDeltaTime);
            PollBufferedButtons();
        }

        public void CaptureFrame()
        {
            _activeGamepadSlot = ResolveActiveGamepadSlot();
            Vector2 keyboardMove = ReadKeyboardMove();
            Vector2 keyboardAim = ReadKeyboardAim();
            Vector2 gamepadMove = enableGamepad ? ReadGamepadMove() : Vector2.zero;
            bool allowMovementAimFallback = IsShootHeld() || _shootBufferLeft > 0f;
            Vector2 gamepadAim = enableGamepad ? ReadGamepadAim(gamepadMove, allowMovementAimFallback) : Vector2.zero;
            Vector2 combinedMove = CombineAxis(keyboardMove, gamepadMove);
            Vector2 combinedAim = CombineAim(keyboardAim, gamepadAim, allowMovementAimFallback ? combinedMove : Vector2.zero);

            _currentFrame = new PlayerInputFrame
            {
                frame = _frameIndex,
                axis = combinedMove.x,
                aim = combinedAim,
                left = combinedMove.x < -0.1f,
                right = combinedMove.x > 0.1f,
                up = combinedMove.y > 0.1f,
                down = combinedMove.y < -0.1f,
                jumpPressed = ConsumeBufferedPress(ref _jumpBufferLeft),
                jumpHeld = IsJumpHeld(),
                shootPressed = ConsumeBufferedPress(ref _shootBufferLeft),
                shootHeld = IsShootHeld(),
                meleePressed = ConsumeBufferedPress(ref _meleeBufferLeft),
                ultimatePressed = ConsumeBufferedPress(ref _ultimateBufferLeft),
                dashPrimaryPressed = ConsumeBufferedPress(ref _dashPrimaryBufferLeft),
                dashSecondaryPressed = ConsumeBufferedPress(ref _dashSecondaryBufferLeft),
            };

            _frameIndex += 1;
        }

        public void ConfigureForPlayer(int configuredPlayerId)
        {
            playerId = Mathf.Max(1, configuredPlayerId);
            preferredGamepadIndex = Mathf.Max(0, playerId - 1);
            _activeGamepadSlot = -1;
            ApplyDefaultsIfNeeded();
        }

        private void PollBufferedButtons()
        {
            if (ReadJumpPressed())
            {
                _jumpBufferLeft = buttonBufferSeconds;
            }

            if (ReadShootPressed())
            {
                _shootBufferLeft = buttonBufferSeconds;
            }

            if (ReadMeleePressed())
            {
                _meleeBufferLeft = buttonBufferSeconds;
            }

            if (ReadUltimatePressed())
            {
                _ultimateBufferLeft = buttonBufferSeconds;
            }

            if (ReadDashPrimaryPressed())
            {
                _dashPrimaryBufferLeft = buttonBufferSeconds;
            }

            if (ReadDashSecondaryPressed())
            {
                _dashSecondaryBufferLeft = buttonBufferSeconds;
            }
        }

        private void TickBuffers(float deltaTime)
        {
            _jumpBufferLeft = Mathf.Max(0f, _jumpBufferLeft - deltaTime);
            _shootBufferLeft = Mathf.Max(0f, _shootBufferLeft - deltaTime);
            _meleeBufferLeft = Mathf.Max(0f, _meleeBufferLeft - deltaTime);
            _ultimateBufferLeft = Mathf.Max(0f, _ultimateBufferLeft - deltaTime);
            _dashPrimaryBufferLeft = Mathf.Max(0f, _dashPrimaryBufferLeft - deltaTime);
            _dashSecondaryBufferLeft = Mathf.Max(0f, _dashSecondaryBufferLeft - deltaTime);
        }

        private Vector2 ReadKeyboardMove()
        {
            float horizontal = 0f;
            if (LegacyInput.GetKey(actionMap.left))
            {
                horizontal -= 1f;
            }

            if (LegacyInput.GetKey(actionMap.right))
            {
                horizontal += 1f;
            }

            float vertical = 0f;
            if (LegacyInput.GetKey(actionMap.up))
            {
                vertical += 1f;
            }

            if (LegacyInput.GetKey(actionMap.down))
            {
                vertical -= 1f;
            }

            return ClampVector(new Vector2(horizontal, vertical));
        }

        private Vector2 ReadKeyboardAim()
        {
            return ReadKeyboardMove();
        }

        private Vector2 ReadGamepadMove()
        {
            int slot = _activeGamepadSlot > 0 ? _activeGamepadSlot : ResolveActiveGamepadSlot();
            if (slot <= 0)
            {
                return Vector2.zero;
            }

            Vector2 legacyMove = new Vector2(
                ReadAxisForSlot(gamepadActionMap.moveHorizontalAxis, slot),
                ReadAxisForSlot(gamepadActionMap.moveVerticalAxis, slot));
            legacyMove += ReadGamepadDpadVector(slot);
            return legacyMove.magnitude < gamepadActionMap.deadzone ? Vector2.zero : ClampVector(legacyMove);
        }

        private Vector2 ReadGamepadAim(Vector2 gamepadMove, bool allowMovementAimFallback)
        {
            int slot = _activeGamepadSlot > 0 ? _activeGamepadSlot : ResolveActiveGamepadSlot();
            if (slot <= 0)
            {
                return Vector2.zero;
            }

            Vector2 dpadAim = ReadGamepadDpadVector(slot);
            if (dpadAim.sqrMagnitude > 0.01f)
            {
                return dpadAim;
            }

            // Match the documented controls: hold shoot and aim with D-pad/left stick.
            bool movementCanDriveAim = gamepadActionMap.useMoveStickAsAimFallback && allowMovementAimFallback;
            if (movementCanDriveAim && gamepadMove.magnitude >= gamepadActionMap.deadzone)
            {
                return ClampVector(gamepadMove);
            }

            if (allowMovementAimFallback)
            {
                return Vector2.zero;
            }

            float lookX = ReadStrongestAxisForSlot(slot, gamepadActionMap.lookHorizontalAxis, gamepadActionMap.lookHorizontalAxisAlt);
            float lookY = ReadStrongestAxisForSlot(slot, gamepadActionMap.lookVerticalAxis, gamepadActionMap.lookVerticalAxisAlt);
            Vector2 legacyAim = new Vector2(lookX, lookY);
            if (legacyAim.magnitude >= gamepadActionMap.aimDeadzone)
            {
                return ClampVector(legacyAim);
            }

            return Vector2.zero;
        }

        private float ReadAxisForSlot(string axisName, int slot)
        {
            if (string.IsNullOrWhiteSpace(axisName) || slot <= 0)
            {
                return 0f;
            }

            string resolvedAxisName = ResolveAxisNameForSlot(axisName, slot);
            if (!string.IsNullOrWhiteSpace(resolvedAxisName))
            {
                return ReadAxisRaw(resolvedAxisName);
            }

            return 0f;
        }

        private float ReadStrongestAxisForSlot(int slot, params string[] axisNames)
        {
            float strongestValue = 0f;
            for (int index = 0; index < axisNames.Length; index += 1)
            {
                float candidate = ReadAxisForSlot(axisNames[index], slot);
                if (Mathf.Abs(candidate) > Mathf.Abs(strongestValue))
                {
                    strongestValue = candidate;
                }
            }

            return strongestValue;
        }

        private float ReadAxisRaw(string axisName)
        {
            try
            {
                return LegacyInput.GetAxisRaw(axisName);
            }
            catch
            {
                return 0f;
            }
        }

        private bool ReadJumpPressed()
        {
            return LegacyInput.GetKeyDown(actionMap.jump)
                || ReadGamepadActionDown(GamepadBindingAction.Jump);
        }

        private bool IsJumpHeld()
        {
            return LegacyInput.GetKey(actionMap.jump)
                || ReadGamepadActionHeld(GamepadBindingAction.Jump);
        }

        private bool ReadShootPressed()
        {
            return LegacyInput.GetKeyDown(actionMap.shoot)
                || ReadGamepadActionDown(GamepadBindingAction.ShootArrow);
        }

        private bool IsShootHeld()
        {
            return LegacyInput.GetKey(actionMap.shoot)
                || ReadGamepadActionHeld(GamepadBindingAction.ShootArrow);
        }

        private bool ReadMeleePressed()
        {
            return LegacyInput.GetKeyDown(actionMap.melee)
                || ReadGamepadActionDown(GamepadBindingAction.MeleeAttack);
        }

        private bool ReadUltimatePressed()
        {
            return LegacyInput.GetKeyDown(actionMap.ultimate)
                || ReadGamepadActionDown(GamepadBindingAction.Ult);
        }

        private bool ReadDashPrimaryPressed()
        {
            return LegacyInput.GetKeyDown(actionMap.dashPrimary)
                || ReadGamepadActionDown(GamepadBindingAction.Dash, DashPrimaryLabels);
        }

        private bool ReadDashSecondaryPressed()
        {
            return LegacyInput.GetKeyDown(actionMap.dashSecondary)
                || ReadGamepadActionDown(GamepadBindingAction.Dash, DashSecondaryLabels);
        }

        private bool ReadGamepadActionDown(GamepadBindingAction action, GamepadBindingLabel[] constrainedLabels = null)
        {
            return ReadGamepadAction(action, true, constrainedLabels);
        }

        private bool ReadGamepadActionHeld(GamepadBindingAction action, GamepadBindingLabel[] constrainedLabels = null)
        {
            return ReadGamepadAction(action, false, constrainedLabels);
        }

        private bool ReadGamepadAction(GamepadBindingAction action, bool justPressed, GamepadBindingLabel[] constrainedLabels)
        {
            if (!enableGamepad)
            {
                return false;
            }

            EditableGamepadBindings bindings = ResolveEditableBindings();
            GamepadBindingLabel[] labels = constrainedLabels ?? AllBoundGamepadLabels;
            for (int index = 0; index < labels.Length; index += 1)
            {
                GamepadBindingLabel label = labels[index];
                if (!bindings.MatchesAction(label, action))
                {
                    continue;
                }

                if (ReadGamepadLabel(label, bindings, justPressed))
                {
                    return true;
                }
            }

            return false;
        }

        private bool ReadGamepadLabel(GamepadBindingLabel label, EditableGamepadBindings bindings, bool justPressed)
        {
            int slot = _activeGamepadSlot > 0 ? _activeGamepadSlot : ResolveActiveGamepadSlot();
            string joystickName = GetJoystickName(slot);
            var rawBindings = bindings.GetRawBindings(joystickName, label);
            for (int index = 0; index < rawBindings.Count; index += 1)
            {
                int buttonIndex = rawBindings[index].ButtonIndex;
                if (justPressed ? ReadGamepadButtonDown(buttonIndex) : ReadGamepadButton(buttonIndex))
                {
                    return true;
                }
            }

            return false;
        }

        private bool ReadGamepadButtonDown(int buttonIndex)
        {
            if (!enableGamepad)
            {
                return false;
            }

            int slot = _activeGamepadSlot > 0 ? _activeGamepadSlot : ResolveActiveGamepadSlot();
            if (slot <= 0)
            {
                return false;
            }

            KeyCode preferredKey = ResolveJoystickButton(slot, buttonIndex);
            if (preferredKey != KeyCode.None && LegacyInput.GetKeyDown(preferredKey))
            {
                return true;
            }

            if (ShouldUseGenericJoystickFallback(slot))
            {
                return LegacyInput.GetKeyDown(ResolveGenericJoystickButton(buttonIndex));
            }

            return false;
        }

        private bool ReadGamepadButton(int buttonIndex)
        {
            if (!enableGamepad)
            {
                return false;
            }

            int slot = _activeGamepadSlot > 0 ? _activeGamepadSlot : ResolveActiveGamepadSlot();
            if (slot <= 0)
            {
                return false;
            }

            KeyCode preferredKey = ResolveJoystickButton(slot, buttonIndex);
            if (preferredKey != KeyCode.None && LegacyInput.GetKey(preferredKey))
            {
                return true;
            }

            if (ShouldUseGenericJoystickFallback(slot))
            {
                return LegacyInput.GetKey(ResolveGenericJoystickButton(buttonIndex));
            }

            return false;
        }

        private KeyCode ResolveGenericJoystickButton(int buttonIndex)
        {
            switch (buttonIndex)
            {
                case 0: return KeyCode.JoystickButton0;
                case 1: return KeyCode.JoystickButton1;
                case 2: return KeyCode.JoystickButton2;
                case 3: return KeyCode.JoystickButton3;
                case 4: return KeyCode.JoystickButton4;
                case 5: return KeyCode.JoystickButton5;
                case 6: return KeyCode.JoystickButton6;
                case 7: return KeyCode.JoystickButton7;
                case 8: return KeyCode.JoystickButton8;
                case 9: return KeyCode.JoystickButton9;
                case 10: return KeyCode.JoystickButton10;
                case 11: return KeyCode.JoystickButton11;
                case 12: return KeyCode.JoystickButton12;
                case 13: return KeyCode.JoystickButton13;
                case 14: return KeyCode.JoystickButton14;
                case 15: return KeyCode.JoystickButton15;
                case 16: return KeyCode.JoystickButton16;
                case 17: return KeyCode.JoystickButton17;
                case 18: return KeyCode.JoystickButton18;
                case 19: return KeyCode.JoystickButton19;
                default: return KeyCode.None;
            }
        }

        private KeyCode ResolveJoystickButton(int joystickSlot, int buttonIndex)
        {
            if (buttonIndex < 0 || joystickSlot <= 0)
            {
                return KeyCode.None;
            }

            int joystickNumber = Mathf.Clamp(joystickSlot, 1, 8);
            if (Enum.TryParse("Joystick" + joystickNumber + "Button" + buttonIndex, out KeyCode keyCode))
            {
                return keyCode;
            }

            return KeyCode.None;
        }

        private static Vector2 CombineAxis(Vector2 primary, Vector2 secondary)
        {
            return secondary.sqrMagnitude > primary.sqrMagnitude ? secondary : primary;
        }

        private static Vector2 CombineAim(Vector2 keyboardAim, Vector2 gamepadAim, Vector2 movementFallback)
        {
            if (keyboardAim.sqrMagnitude > 0.01f)
            {
                return keyboardAim;
            }

            if (gamepadAim.sqrMagnitude > 0.01f)
            {
                return gamepadAim;
            }

            return movementFallback.sqrMagnitude > 0.01f ? movementFallback : Vector2.zero;
        }

        private static Vector2 ClampVector(Vector2 value)
        {
            return value.sqrMagnitude > 1f ? value.normalized : value;
        }

        private static bool ConsumeBufferedPress(ref float timer)
        {
            if (timer <= 0f)
            {
                return false;
            }

            timer = 0f;
            return true;
        }

        private int ResolveActiveGamepadSlot()
        {
            if (!enableGamepad)
            {
                return -1;
            }

            int connectedCount = GetConnectedGamepadSlots(_connectedGamepadSlots);
            if (connectedCount <= 0)
            {
                return -1;
            }

            if (connectedCount == 1)
            {
                return playerId == 1 ? _connectedGamepadSlots[0] : -1;
            }

            int preferredConnectedIndex = Mathf.Clamp(preferredGamepadIndex, 0, connectedCount - 1);
            return _connectedGamepadSlots[preferredConnectedIndex];
        }

        private bool IsGamepadSlotActive(int slot)
        {
            if (!IsJoystickConnected(slot))
            {
                return false;
            }

            float moveX = ReadAxisForSlot(gamepadActionMap.moveHorizontalAxis, slot);
            float moveY = ReadAxisForSlot(gamepadActionMap.moveVerticalAxis, slot);
            float lookX = ReadStrongestAxisForSlot(slot, gamepadActionMap.lookHorizontalAxis, gamepadActionMap.lookHorizontalAxisAlt);
            float lookY = ReadStrongestAxisForSlot(slot, gamepadActionMap.lookVerticalAxis, gamepadActionMap.lookVerticalAxisAlt);
            float trigger = ReadStrongestAxisForSlot(slot, gamepadActionMap.dashSecondaryAxis, gamepadActionMap.dashSecondaryAxisAlt, gamepadActionMap.dashSecondaryAxisThird);

            if (Mathf.Abs(moveX) > gamepadActionMap.deadzone
                || Mathf.Abs(moveY) > gamepadActionMap.deadzone
                || Mathf.Abs(lookX) > gamepadActionMap.aimDeadzone
                || Mathf.Abs(lookY) > gamepadActionMap.aimDeadzone
                || trigger >= gamepadActionMap.triggerPressThreshold)
            {
                return true;
            }

            EditableGamepadBindings bindings = ResolveEditableBindings();
            string joystickName = GetJoystickName(slot);
            for (int index = 0; index < AllBoundGamepadLabels.Length; index += 1)
            {
                var rawBindings = bindings.GetRawBindings(joystickName, AllBoundGamepadLabels[index]);
                for (int rawIndex = 0; rawIndex < rawBindings.Count; rawIndex += 1)
                {
                    if (IsJoystickButtonHeld(slot, rawBindings[rawIndex].ButtonIndex))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private bool IsJoystickConnected(int slot)
        {
            if (slot <= 0)
            {
                return false;
            }

            return !string.IsNullOrWhiteSpace(GetJoystickName(slot));
        }

        private string GetJoystickName(int slot)
        {
            if (slot <= 0)
            {
                return string.Empty;
            }

            string[] joystickNames = LegacyInput.GetJoystickNames();
            if (joystickNames == null || slot - 1 >= joystickNames.Length)
            {
                return string.Empty;
            }

            return joystickNames[slot - 1] ?? string.Empty;
        }

        private string BuildFaceButtonDebugSummary()
        {
            if (!enableGamepad)
            {
                return "Off";
            }

            int slot = _activeGamepadSlot > 0 ? _activeGamepadSlot : ResolveActiveGamepadSlot();
            if (slot <= 0)
            {
                return "NoPad";
            }

            EditableGamepadBindings bindings = ResolveEditableBindings();
            string joystickName = GetJoystickName(slot);
            return ResolveShortJoystickName(joystickName) + "/" + bindings.BuildDebugSummary(joystickName) +
                " | Btn0:" + (IsJoystickButtonHeld(slot, 0) ? "1" : "0") +
                " Btn1:" + (IsJoystickButtonHeld(slot, 1) ? "1" : "0") +
                " Btn2:" + (IsJoystickButtonHeld(slot, 2) ? "1" : "0") +
                " Btn3:" + (IsJoystickButtonHeld(slot, 3) ? "1" : "0") +
                " Btn4:" + (IsJoystickButtonHeld(slot, 4) ? "1" : "0") +
                " Btn5:" + (IsJoystickButtonHeld(slot, 5) ? "1" : "0") +
                " Btn6:" + (IsJoystickButtonHeld(slot, 6) ? "1" : "0") +
                " Btn7:" + (IsJoystickButtonHeld(slot, 7) ? "1" : "0");
        }

        private static string ResolveShortJoystickName(string joystickName)
        {
            if (string.IsNullOrWhiteSpace(joystickName))
            {
                return "UnknownPad";
            }

            string trimmed = joystickName.Trim();
            return trimmed.Length <= 24 ? trimmed : trimmed.Substring(0, 24);
        }

        private int GetConnectedGamepadSlots(int[] buffer)
        {
            if (buffer == null || buffer.Length == 0)
            {
                return 0;
            }

            int count = 0;
            for (int slot = 1; slot <= MaxSupportedGamepads && count < buffer.Length; slot += 1)
            {
                if (!IsJoystickConnected(slot))
                {
                    continue;
                }

                buffer[count] = slot;
                count += 1;
            }

            return count;
        }

        private bool ShouldUseGenericJoystickFallback(int slot)
        {
            if (slot <= 0)
            {
                return false;
            }

            int connectedCount = GetConnectedGamepadSlots(_connectedGamepadSlots);
            return connectedCount == 1 && _connectedGamepadSlots[0] == slot;
        }

        private bool IsJoystickButtonHeld(int slot, int buttonIndex)
        {
            if (buttonIndex < 0)
            {
                return false;
            }

            KeyCode slotButton = ResolveJoystickButton(slot, buttonIndex);
            if (slotButton != KeyCode.None && LegacyInput.GetKey(slotButton))
            {
                return true;
            }

            return ShouldUseGenericJoystickFallback(slot) && LegacyInput.GetKey(ResolveGenericJoystickButton(buttonIndex));
        }

        private void ApplyDefaultsIfNeeded()
        {
            if (!usePlayerDefaults)
            {
                return;
            }

            bool preserveGamepad = enableGamepad;
            actionMap = playerId == 2
                ? PlayerActionMap.CreateDefaultPlayerTwo()
                : PlayerActionMap.CreateDefaultPlayerOne();
            _editableGamepadBindings = EditableGamepadBindings.Reload();
            gamepadActionMap = ResolveConfiguredGamepadActionMap();
            enableGamepad = playerId == 1 || preserveGamepad;
            preferredGamepadIndex = Mathf.Max(0, playerId - 1);
        }

        private EditableGamepadBindings ResolveEditableBindings()
        {
            return _editableGamepadBindings ?? (_editableGamepadBindings = EditableGamepadBindings.Load());
        }

        private GamepadActionMap ResolveConfiguredGamepadActionMap()
        {
            GamepadControlProfileAsset profile = gamepadProfile != null
                ? gamepadProfile
                : LoadDefaultGamepadProfile();
            return profile != null
                ? profile.CreateRuntimeMap()
                : GamepadActionMap.CreateDefault();
        }

        private static GamepadControlProfileAsset LoadDefaultGamepadProfile()
        {
            if (s_defaultGamepadProfile == null)
            {
                s_defaultGamepadProfile = Resources.Load<GamepadControlProfileAsset>(DefaultGamepadProfileResourcePath);
            }

            return s_defaultGamepadProfile;
        }

        private Vector2 ReadGamepadDpadVector(int slot)
        {
            float horizontal = QuantizeDigitalAxis(ReadAxisForSlot(gamepadActionMap.dpadHorizontalAxis, slot));
            float vertical = QuantizeDigitalAxis(ReadAxisForSlot(gamepadActionMap.dpadVerticalAxis, slot));

            if (ReadGamepadButton(gamepadActionMap.dpadLeftButton))
            {
                horizontal -= 1f;
            }

            if (ReadGamepadButton(gamepadActionMap.dpadRightButton))
            {
                horizontal += 1f;
            }

            if (ReadGamepadButton(gamepadActionMap.dpadUpButton))
            {
                vertical += 1f;
            }

            if (ReadGamepadButton(gamepadActionMap.dpadDownButton))
            {
                vertical -= 1f;
            }

            return ClampVector(new Vector2(horizontal, vertical));
        }

        private static float QuantizeDigitalAxis(float value)
        {
            if (value >= 0.5f)
            {
                return 1f;
            }

            if (value <= -0.5f)
            {
                return -1f;
            }

            return 0f;
        }

        private string ResolveAxisNameForSlot(string axisName, int joystickSlot)
        {
            if (string.IsNullOrWhiteSpace(axisName) || joystickSlot <= 0)
            {
                return axisName;
            }

            for (int slot = 1; slot <= MaxSupportedGamepads; slot += 1)
            {
                if (axisName.EndsWith("_P" + slot, StringComparison.Ordinal))
                {
                    return axisName;
                }
            }

            return axisName + "_P" + joystickSlot;
        }
    }
}
