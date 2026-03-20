using System;
using ProjectPVP.Match;
using LegacyInput = UnityEngine.Input;
using UnityEngine;
using UnityEngine.Serialization;

namespace ProjectPVP.Input
{
    // Canonical control semantics live in INPUT_SOURCE_OF_TRUTH.txt at the project root.
    public class KeyboardPlayerInputSource : MonoBehaviour, ICombatantInputSource
    {
        private const int MaxSupportedGamepads = 4;
        private const string DefaultGamepadProfileResourcePath = "ProjectPVP/Input/DefaultGamepadControlProfile";
        private const string GamepadProfileDefault = "Default";
        private const string GamepadProfileDualSense = "DualSense";
        private const string GamepadProfileXbox = "Xbox";

        [FormerlySerializedAs("playerId")]
        [Min(1)] public int slotId = 1;
        public bool usePlayerDefaults = true;
        public PlayerActionMap actionMap = PlayerActionMap.CreateDefaultPlayerOne();
        public GamepadControlProfileAsset gamepadProfile;
        public bool enableGamepad;
        [Min(0)] public int preferredGamepadIndex;
        public PreferredGamepadFamily preferredGamepadFamily = PreferredGamepadFamily.Any;
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
        private bool _dashSecondaryAxisHeldLastFrame;
        private int _activeGamepadSlot = -1;
        private readonly int[] _connectedGamepadSlots = new int[MaxSupportedGamepads];
        private static GamepadControlProfileAsset s_defaultGamepadProfile;

        public int playerId
        {
            get => slotId;
            set => slotId = value;
        }

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
            slotId = Mathf.Max(1, configuredPlayerId);
            if (usePlayerDefaults)
            {
                preferredGamepadIndex = Mathf.Max(0, slotId - 1);
            }

            _activeGamepadSlot = -1;
            _dashSecondaryAxisHeldLastFrame = false;
            ApplyDefaultsIfNeeded();
        }

        public void ConfigureForSlot(CombatantSlotId configuredSlotId)
        {
            ConfigureForPlayer(Mathf.Max(1, configuredSlotId.ToInt()));
        }

        public void ApplySlotProfile(CombatantSlotProfile profile, CombatantSlotId configuredSlotId)
        {
            slotId = Mathf.Max(1, configuredSlotId.ToInt());
            _activeGamepadSlot = -1;
            _dashSecondaryAxisHeldLastFrame = false;

            if (profile == null)
            {
                usePlayerDefaults = true;
                preferredGamepadFamily = PreferredGamepadFamily.Any;
                ApplyDefaultsIfNeeded();
                return;
            }

            usePlayerDefaults = false;
            actionMap = profile.CreateKeyboardBindings(configuredSlotId);
            gamepadProfile = profile.gamepadProfile;
            gamepadActionMap = profile.CreateGamepadBindings();
            enableGamepad = profile.enableGamepad;
            preferredGamepadIndex = profile.ResolvePreferredGamepadIndex(configuredSlotId);
            preferredGamepadFamily = profile.ResolvePreferredGamepadFamily();
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
            float value = !string.IsNullOrWhiteSpace(resolvedAxisName)
                ? ReadAxisRaw(resolvedAxisName)
                : 0f;

            if (Mathf.Abs(value) > 0.001f)
            {
                return value;
            }

            return ShouldUseGenericJoystickFallback(slot) ? ReadAxisRaw(axisName) : value;
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
                || ReadAnyGamepadButtonDown(gamepadActionMap.jumpButton, gamepadActionMap.jumpAlternateButton);
        }

        private bool IsJumpHeld()
        {
            return LegacyInput.GetKey(actionMap.jump)
                || ReadAnyGamepadButtonHeld(gamepadActionMap.jumpButton, gamepadActionMap.jumpAlternateButton);
        }

        private bool ReadShootPressed()
        {
            return LegacyInput.GetKeyDown(actionMap.shoot)
                || ReadAnyGamepadButtonDown(gamepadActionMap.shootButton);
        }

        private bool IsShootHeld()
        {
            return LegacyInput.GetKey(actionMap.shoot)
                || ReadAnyGamepadButtonHeld(gamepadActionMap.shootButton);
        }

        private bool ReadMeleePressed()
        {
            return LegacyInput.GetKeyDown(actionMap.melee)
                || ReadAnyGamepadButtonDown(gamepadActionMap.meleeButton);
        }

        private bool ReadUltimatePressed()
        {
            return LegacyInput.GetKeyDown(actionMap.ultimate)
                || ReadAnyGamepadButtonDown(gamepadActionMap.ultimateButton);
        }

        private bool ReadDashPrimaryPressed()
        {
            return LegacyInput.GetKeyDown(actionMap.dashPrimary)
                || ReadAnyGamepadButtonDown(
                    gamepadActionMap.dashPrimaryButton,
                    gamepadActionMap.dashPrimaryAlternateButton,
                    gamepadActionMap.dashPrimaryThirdButton);
        }

        private bool ReadDashSecondaryPressed()
        {
            return LegacyInput.GetKeyDown(actionMap.dashSecondary)
                || ReadAnyGamepadButtonDown(gamepadActionMap.dashSecondaryButton)
                || ReadDashSecondaryAxisPressed();
        }

        private bool ReadAnyGamepadButtonDown(params int[] buttonIndices)
        {
            if (!enableGamepad || buttonIndices == null)
            {
                return false;
            }

            for (int index = 0; index < buttonIndices.Length; index += 1)
            {
                if (ReadGamepadButtonDown(buttonIndices[index]))
                {
                    return true;
                }
            }

            return false;
        }

        private bool ReadAnyGamepadButtonHeld(params int[] buttonIndices)
        {
            if (!enableGamepad || buttonIndices == null)
            {
                return false;
            }

            for (int index = 0; index < buttonIndices.Length; index += 1)
            {
                if (ReadGamepadButton(buttonIndices[index]))
                {
                    return true;
                }
            }

            return false;
        }

        // Some Unity backends expose trigger presses only through axes, especially on
        // secondary gamepad slots, so the dash combo cannot rely on button indices alone.
        private bool ReadDashSecondaryAxisPressed()
        {
            if (!enableGamepad)
            {
                _dashSecondaryAxisHeldLastFrame = false;
                return false;
            }

            int slot = _activeGamepadSlot > 0 ? _activeGamepadSlot : ResolveActiveGamepadSlot();
            if (slot <= 0)
            {
                _dashSecondaryAxisHeldLastFrame = false;
                return false;
            }

            if (ReadGamepadDpadVector(slot).sqrMagnitude > 0.01f)
            {
                _dashSecondaryAxisHeldLastFrame = false;
                return false;
            }

            bool isHeld = ReadDashSecondaryAxisValue(slot) >= gamepadActionMap.triggerPressThreshold;
            bool justPressed = isHeld && !_dashSecondaryAxisHeldLastFrame;
            _dashSecondaryAxisHeldLastFrame = isHeld;
            return justPressed;
        }

        private float ReadDashSecondaryAxisValue(int slot)
        {
            float strongestValue = 0f;
            // The legacy TriggerR_A candidate shares a physical axis with other controls on
            // some Windows/XInput backends, which causes ghost dash presses on D-pad/right-stick input.
            strongestValue = Mathf.Max(strongestValue, ReadAxisForSlot(gamepadActionMap.dashSecondaryAxisAlt, slot));
            strongestValue = Mathf.Max(strongestValue, ReadAxisForSlot(gamepadActionMap.dashSecondaryAxisThird, slot));
            return strongestValue;
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

            int resolvedButtonIndex = ResolveRuntimeButtonIndex(slot, buttonIndex);
            KeyCode preferredKey = ResolveJoystickButton(slot, resolvedButtonIndex);
            if (preferredKey != KeyCode.None && LegacyInput.GetKeyDown(preferredKey))
            {
                return true;
            }

            if (ShouldUseGenericJoystickFallback(slot))
            {
                return LegacyInput.GetKeyDown(ResolveGenericJoystickButton(resolvedButtonIndex));
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

            int resolvedButtonIndex = ResolveRuntimeButtonIndex(slot, buttonIndex);
            KeyCode preferredKey = ResolveJoystickButton(slot, resolvedButtonIndex);
            if (preferredKey != KeyCode.None && LegacyInput.GetKey(preferredKey))
            {
                return true;
            }

            if (ShouldUseGenericJoystickFallback(slot))
            {
                return LegacyInput.GetKey(ResolveGenericJoystickButton(resolvedButtonIndex));
            }

            return false;
        }

        private int ResolveRuntimeButtonIndex(int slot, int configuredButtonIndex)
        {
            if (configuredButtonIndex < 0)
            {
                return configuredButtonIndex;
            }

            if (!string.Equals(ResolveGamepadProfileName(slot), GamepadProfileDualSense, StringComparison.OrdinalIgnoreCase))
            {
                return configuredButtonIndex;
            }

            switch (configuredButtonIndex)
            {
                case 0: return 1;
                case 1: return 2;
                case 2: return 0;
                case 3: return 3;
                default: return configuredButtonIndex;
            }
        }

        private string ResolveGamepadProfileName(int slot)
        {
            string joystickName = GetJoystickName(slot);
            if (string.IsNullOrWhiteSpace(joystickName))
            {
                return GamepadProfileDefault;
            }

            string normalized = joystickName.Trim().ToUpperInvariant();
            if (normalized.Contains("DUALSENSE")
                || normalized.Contains("WIRELESS CONTROLLER")
                || normalized.Contains("PLAYSTATION"))
            {
                return GamepadProfileDualSense;
            }

            if (normalized.Contains("XBOX")
                || normalized.Contains("XINPUT")
                || normalized.Contains("X-INPUT")
                || normalized.Contains("360 CONTROLLER")
                || normalized.Contains("X-BOX")
                || normalized.Contains("GAMESIR"))
            {
                return GamepadProfileXbox;
            }

            return GamepadProfileDefault;
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

            int familyMatchedSlot = ResolvePreferredGamepadSlotByFamily(connectedCount);
            if (familyMatchedSlot > 0)
            {
                return familyMatchedSlot;
            }

            if (connectedCount == 1)
            {
                // Keep a single connected gamepad bound to player one only so the pad
                // never starts driving every enabled input source at the same time.
                return preferredGamepadIndex == 0 ? _connectedGamepadSlots[0] : -1;
            }

            if (preferredGamepadIndex < 0 || preferredGamepadIndex >= connectedCount)
            {
                return -1;
            }

            // With more than one controller connected, each player must stay locked to
            // its assigned slot. Falling back to "any active slot" causes one pad to
            // control both players whenever the other pad is idle for a frame.
            return _connectedGamepadSlots[preferredGamepadIndex];
        }

        private int ResolvePreferredGamepadSlotByFamily(int connectedCount)
        {
            if (preferredGamepadFamily == PreferredGamepadFamily.Any || connectedCount <= 0)
            {
                return -1;
            }

            for (int index = 0; index < connectedCount && index < _connectedGamepadSlots.Length; index += 1)
            {
                int slot = _connectedGamepadSlots[index];
                switch (preferredGamepadFamily)
                {
                    case PreferredGamepadFamily.XboxLike:
                        if (IsXboxProfile(slot))
                        {
                            return slot;
                        }
                        break;
                    case PreferredGamepadFamily.DualSense:
                        if (string.Equals(ResolveGamepadProfileName(slot), GamepadProfileDualSense, StringComparison.OrdinalIgnoreCase))
                        {
                            return slot;
                        }
                        break;
                }
            }

            return -1;
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

            string joystickName = GetJoystickName(slot);
            Vector2 dpad = ReadGamepadDpadVector(slot);
            Vector2 rawDpad = ReadConfiguredDpadAxesRaw(slot);
            return ResolveShortJoystickName(joystickName) +
                "/" + ResolveGamepadProfileName(slot) +
                " | DpadMode:" + ResolveDpadModeDebugName(slot) +
                " | Dpad:" + dpad.x.ToString("0") + "," + dpad.y.ToString("0") +
                " | RawDpad:" + rawDpad.x.ToString("0.00") + "," + rawDpad.y.ToString("0.00") +
                " | Trg:" + ReadDashSecondaryAxisValue(slot).ToString("0.00") +
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
            int preservedGamepadIndex = Mathf.Max(0, preferredGamepadIndex);
            PreferredGamepadFamily preservedGamepadFamily = preferredGamepadFamily;
            actionMap = PlayerActionMap.CreateDefaultForPlayer(slotId);
            gamepadActionMap = ResolveConfiguredGamepadActionMap();
            enableGamepad = preserveGamepad;
            preferredGamepadIndex = preservedGamepadIndex;
            preferredGamepadFamily = preservedGamepadFamily;
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
            return ReadGamepadDpadVectorDirect(slot);
        }

        private Vector2 ReadGamepadDpadVectorDirect(int slot)
        {
            Vector2 normalizedDpadAxes = NormalizeDpadAxesForSlot(slot, QuantizeDpadAxes(ReadConfiguredDpadAxesRaw(slot)));
            float horizontal = normalizedDpadAxes.x;
            float vertical = normalizedDpadAxes.y;

            if (gamepadActionMap.dpadLeftButton >= 0 && IsJoystickButtonHeld(slot, gamepadActionMap.dpadLeftButton))
            {
                horizontal -= 1f;
            }

            if (gamepadActionMap.dpadRightButton >= 0 && IsJoystickButtonHeld(slot, gamepadActionMap.dpadRightButton))
            {
                horizontal += 1f;
            }

            if (gamepadActionMap.dpadUpButton >= 0 && IsJoystickButtonHeld(slot, gamepadActionMap.dpadUpButton))
            {
                vertical += 1f;
            }

            if (gamepadActionMap.dpadDownButton >= 0 && IsJoystickButtonHeld(slot, gamepadActionMap.dpadDownButton))
            {
                vertical -= 1f;
            }

            bool useGenericFallback = ShouldUseGenericJoystickFallback(slot);
            Vector2 genericFallbackAxes = useGenericFallback
                ? NormalizeDpadAxesForSlot(slot, QuantizeDpadAxes(ReadGenericDpadAxesRaw()))
                : Vector2.zero;

            if (Mathf.Abs(horizontal) < 0.5f && useGenericFallback)
            {
                horizontal = genericFallbackAxes.x;
            }

            if (Mathf.Abs(vertical) < 0.5f && useGenericFallback)
            {
                vertical = genericFallbackAxes.y;
            }

            return ClampVector(new Vector2(horizontal, vertical));
        }

        private Vector2 ReadConfiguredDpadAxesRaw(int slot)
        {
            return new Vector2(
                ReadAxisForSlot(gamepadActionMap.dpadHorizontalAxis, slot),
                ReadAxisForSlot(gamepadActionMap.dpadVerticalAxis, slot));
        }

        private Vector2 ReadGenericDpadAxesRaw()
        {
            return new Vector2(
                ReadAxisRaw(gamepadActionMap.dpadHorizontalAxis),
                ReadAxisRaw(gamepadActionMap.dpadVerticalAxis));
        }

        private static Vector2 QuantizeDpadAxes(Vector2 rawAxes)
        {
            return new Vector2(
                QuantizeDigitalAxis(rawAxes.x),
                QuantizeDigitalAxis(rawAxes.y));
        }

        private Vector2 NormalizeDpadAxesForSlot(int slot, Vector2 dpadAxes)
        {
            return gamepadActionMap.swapDpadAxesForXbox && IsXboxProfile(slot)
                ? new Vector2(-dpadAxes.y, -dpadAxes.x)
                : dpadAxes;
        }

        private bool IsXboxProfile(int slot)
        {
            return string.Equals(ResolveGamepadProfileName(slot), GamepadProfileXbox, StringComparison.OrdinalIgnoreCase);
        }

        private string ResolveDpadModeDebugName(int slot)
        {
            return gamepadActionMap.swapDpadAxesForXbox && IsXboxProfile(slot) ? "SwapXY" : "Native";
        }

        private static float QuantizeDigitalAxis(float value)
        {
            if (value >= 0.2f)
            {
                return 1f;
            }

            if (value <= -0.2f)
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
