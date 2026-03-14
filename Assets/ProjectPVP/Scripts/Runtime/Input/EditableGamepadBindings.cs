using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectPVP.Input
{
    public enum GamepadBindingAction
    {
        None = 0,
        Jump = 1,
        ShootArrow = 2,
        MeleeAttack = 3,
        Ult = 4,
        Dash = 5,
    }

    public enum GamepadBindingLabel
    {
        None = 0,
        X = 1,
        Square = 2,
        Circle = 3,
        Triangle = 4,
        L1 = 5,
        R1 = 6,
        L2 = 7,
        R2 = 8,
    }

    internal readonly struct RawGamepadBinding
    {
        public RawGamepadBinding(int buttonIndex)
        {
            ButtonIndex = buttonIndex;
        }

        public int ButtonIndex { get; }
    }

    public sealed class EditableGamepadBindings
    {
        private const string ResourcePath = "ProjectPVP/Input/GamepadBindings";

        private enum Section
        {
            None = 0,
            Actions = 1,
            Profile = 2,
        }

        private sealed class BindingProfile
        {
            public string Name;
            public readonly List<string> MatchTokens = new List<string>();
            public readonly Dictionary<GamepadBindingLabel, List<RawGamepadBinding>> RawBindings =
                new Dictionary<GamepadBindingLabel, List<RawGamepadBinding>>();
        }

        private static EditableGamepadBindings s_cached;

        private readonly Dictionary<GamepadBindingLabel, GamepadBindingAction> _actionsByLabel =
            new Dictionary<GamepadBindingLabel, GamepadBindingAction>();

        private readonly List<BindingProfile> _profiles = new List<BindingProfile>();

        public static EditableGamepadBindings Load()
        {
            return s_cached ?? Reload();
        }

        public static EditableGamepadBindings Reload()
        {
            TextAsset asset = Resources.Load<TextAsset>(ResourcePath);
            s_cached = asset != null ? Parse(asset.text) : CreateDefault();
            return s_cached;
        }

        public static EditableGamepadBindings CreateDefault()
        {
            var bindings = new EditableGamepadBindings();

            bindings._actionsByLabel[GamepadBindingLabel.X] = GamepadBindingAction.Jump;
            bindings._actionsByLabel[GamepadBindingLabel.Square] = GamepadBindingAction.ShootArrow;
            bindings._actionsByLabel[GamepadBindingLabel.Circle] = GamepadBindingAction.MeleeAttack;
            bindings._actionsByLabel[GamepadBindingLabel.Triangle] = GamepadBindingAction.Ult;
            bindings._actionsByLabel[GamepadBindingLabel.L1] = GamepadBindingAction.Dash;
            bindings._actionsByLabel[GamepadBindingLabel.R1] = GamepadBindingAction.Dash;
            bindings._actionsByLabel[GamepadBindingLabel.L2] = GamepadBindingAction.Dash;
            bindings._actionsByLabel[GamepadBindingLabel.R2] = GamepadBindingAction.Dash;

            BindingProfile defaultProfile = bindings.GetOrCreateProfile("Default");
            bindings.SetRawBinding(defaultProfile, GamepadBindingLabel.X, 0);
            bindings.SetRawBinding(defaultProfile, GamepadBindingLabel.Circle, 1);
            bindings.SetRawBinding(defaultProfile, GamepadBindingLabel.Square, 2);
            bindings.SetRawBinding(defaultProfile, GamepadBindingLabel.Triangle, 3);
            bindings.SetRawBinding(defaultProfile, GamepadBindingLabel.L1, 4);
            bindings.SetRawBinding(defaultProfile, GamepadBindingLabel.R1, 5);
            bindings.SetRawBinding(defaultProfile, GamepadBindingLabel.L2, 6);
            bindings.SetRawBinding(defaultProfile, GamepadBindingLabel.R2, 7);

            return bindings;
        }

        public bool MatchesAction(GamepadBindingLabel label, GamepadBindingAction action)
        {
            return _actionsByLabel.TryGetValue(label, out GamepadBindingAction mappedAction) && mappedAction == action;
        }

        public GamepadBindingAction GetAssignedAction(GamepadBindingLabel label)
        {
            return _actionsByLabel.TryGetValue(label, out GamepadBindingAction action)
                ? action
                : GamepadBindingAction.None;
        }

        internal IReadOnlyList<RawGamepadBinding> GetRawBindings(string joystickName, GamepadBindingLabel label)
        {
            BindingProfile profile = ResolveProfile(joystickName);
            if (profile != null && profile.RawBindings.TryGetValue(label, out List<RawGamepadBinding> profileBindings))
            {
                return profileBindings;
            }

            return Array.Empty<RawGamepadBinding>();
        }

        public string ResolveProfileName(string joystickName)
        {
            BindingProfile profile = ResolveProfile(joystickName);
            return profile != null ? profile.Name : "Default";
        }

        public string BuildDebugSummary(string joystickName)
        {
            return ResolveProfileName(joystickName) +
                " X=" + DescribeAction(GamepadBindingLabel.X) + ":" + DescribeRawBindings(joystickName, GamepadBindingLabel.X) +
                " Sq=" + DescribeAction(GamepadBindingLabel.Square) + ":" + DescribeRawBindings(joystickName, GamepadBindingLabel.Square) +
                " Ci=" + DescribeAction(GamepadBindingLabel.Circle) + ":" + DescribeRawBindings(joystickName, GamepadBindingLabel.Circle) +
                " Tr=" + DescribeAction(GamepadBindingLabel.Triangle) + ":" + DescribeRawBindings(joystickName, GamepadBindingLabel.Triangle) +
                " L1=" + DescribeAction(GamepadBindingLabel.L1) + ":" + DescribeRawBindings(joystickName, GamepadBindingLabel.L1) +
                " R1=" + DescribeAction(GamepadBindingLabel.R1) + ":" + DescribeRawBindings(joystickName, GamepadBindingLabel.R1) +
                " L2=" + DescribeAction(GamepadBindingLabel.L2) + ":" + DescribeRawBindings(joystickName, GamepadBindingLabel.L2) +
                " R2=" + DescribeAction(GamepadBindingLabel.R2) + ":" + DescribeRawBindings(joystickName, GamepadBindingLabel.R2);
        }

        private string DescribeAction(GamepadBindingLabel label)
        {
            switch (GetAssignedAction(label))
            {
                case GamepadBindingAction.Jump:
                    return "Jump";
                case GamepadBindingAction.ShootArrow:
                    return "Shoot";
                case GamepadBindingAction.MeleeAttack:
                    return "Melee";
                case GamepadBindingAction.Ult:
                    return "Ult";
                case GamepadBindingAction.Dash:
                    return "Dash";
                default:
                    return "-";
            }
        }

        private string DescribeRawBindings(string joystickName, GamepadBindingLabel label)
        {
            IReadOnlyList<RawGamepadBinding> rawBindings = GetRawBindings(joystickName, label);
            if (rawBindings == null || rawBindings.Count == 0)
            {
                return "-";
            }

            string summary = string.Empty;
            for (int index = 0; index < rawBindings.Count; index += 1)
            {
                if (index > 0)
                {
                    summary += "/";
                }

                summary += "B" + rawBindings[index].ButtonIndex;
            }

            return summary;
        }

        private static EditableGamepadBindings Parse(string text)
        {
            EditableGamepadBindings bindings = CreateDefault();
            if (string.IsNullOrWhiteSpace(text))
            {
                return bindings;
            }

            Section section = Section.None;
            BindingProfile currentProfile = null;
            string[] lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            for (int index = 0; index < lines.Length; index += 1)
            {
                string line = lines[index].Trim();
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#", StringComparison.Ordinal))
                {
                    continue;
                }

                if (line.StartsWith("[", StringComparison.Ordinal) && line.EndsWith("]", StringComparison.Ordinal))
                {
                    section = ParseSection(line, bindings, out currentProfile);
                    continue;
                }

                int separatorIndex = line.IndexOf('=');
                if (separatorIndex <= 0 || separatorIndex >= line.Length - 1)
                {
                    continue;
                }

                string left = line.Substring(0, separatorIndex).Trim();
                string right = line.Substring(separatorIndex + 1).Trim();

                switch (section)
                {
                    case Section.Actions:
                        if (!TryParseLabel(left, out GamepadBindingLabel actionLabel))
                        {
                            break;
                        }

                        if (TryParseAction(right, out GamepadBindingAction action))
                        {
                            bindings._actionsByLabel[actionLabel] = action;
                        }

                        break;

                    case Section.Profile:
                        if (currentProfile == null)
                        {
                            break;
                        }

                        if (Normalize(left) == "MATCH")
                        {
                            bindings.ParseProfileMatchList(currentProfile, right);
                            break;
                        }

                        if (TryParseLabel(left, out GamepadBindingLabel profileLabel))
                        {
                            bindings.ParseRawBindingList(currentProfile, profileLabel, right);
                        }

                        break;
                }
            }

            return bindings;
        }

        private static Section ParseSection(string token, EditableGamepadBindings bindings, out BindingProfile profile)
        {
            string normalized = token.Trim().TrimStart('[').TrimEnd(']').Trim().ToUpperInvariant();
            profile = null;
            switch (normalized)
            {
                case "ACTIONS":
                    return Section.Actions;
                default:
                    if (normalized.StartsWith("PROFILE ", StringComparison.Ordinal))
                    {
                        string profileName = token.Trim().TrimStart('[').TrimEnd(']').Trim().Substring("Profile ".Length).Trim();
                        profile = bindings.GetOrCreateProfile(profileName);
                        return Section.Profile;
                    }

                    return Section.None;
            }
        }

        private void ParseProfileMatchList(BindingProfile profile, string text)
        {
            if (profile == null || string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            string[] tokens = text.Split(',');
            profile.MatchTokens.Clear();
            for (int index = 0; index < tokens.Length; index += 1)
            {
                string normalized = Normalize(tokens[index]);
                if (!string.IsNullOrWhiteSpace(normalized))
                {
                    profile.MatchTokens.Add(normalized);
                }
            }
        }

        private void ParseRawBindingList(BindingProfile profile, GamepadBindingLabel label, string text)
        {
            if (profile == null || string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            string[] tokens = text.Split(',');
            var parsed = new List<RawGamepadBinding>();
            for (int index = 0; index < tokens.Length; index += 1)
            {
                if (TryParseRawBinding(tokens[index], out RawGamepadBinding rawBinding))
                {
                    parsed.Add(rawBinding);
                }
            }

            if (parsed.Count <= 0)
            {
                return;
            }

            profile.RawBindings[label] = parsed;
        }

        private void SetRawBinding(BindingProfile profile, GamepadBindingLabel label, int buttonIndex)
        {
            if (profile == null)
            {
                return;
            }

            profile.RawBindings[label] = new List<RawGamepadBinding>
            {
                new RawGamepadBinding(buttonIndex),
            };
        }

        private BindingProfile GetOrCreateProfile(string profileName)
        {
            string safeName = string.IsNullOrWhiteSpace(profileName) ? "Default" : profileName.Trim();
            for (int index = 0; index < _profiles.Count; index += 1)
            {
                if (string.Equals(_profiles[index].Name, safeName, StringComparison.OrdinalIgnoreCase))
                {
                    return _profiles[index];
                }
            }

            var profile = new BindingProfile
            {
                Name = safeName,
            };
            _profiles.Add(profile);
            return profile;
        }

        private BindingProfile ResolveProfile(string joystickName)
        {
            string normalizedJoystickName = Normalize(joystickName);
            BindingProfile defaultProfile = null;

            for (int index = 0; index < _profiles.Count; index += 1)
            {
                BindingProfile profile = _profiles[index];
                if (string.Equals(profile.Name, "Default", StringComparison.OrdinalIgnoreCase))
                {
                    defaultProfile = profile;
                    continue;
                }

                if (MatchesProfile(profile, normalizedJoystickName))
                {
                    return profile;
                }
            }

            return defaultProfile;
        }

        private static bool MatchesProfile(BindingProfile profile, string normalizedJoystickName)
        {
            if (profile == null || profile.MatchTokens.Count <= 0 || string.IsNullOrWhiteSpace(normalizedJoystickName))
            {
                return false;
            }

            for (int index = 0; index < profile.MatchTokens.Count; index += 1)
            {
                if (normalizedJoystickName.IndexOf(profile.MatchTokens[index], StringComparison.Ordinal) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryParseLabel(string text, out GamepadBindingLabel label)
        {
            string normalized = Normalize(text);
            switch (normalized)
            {
                case "X":
                    label = GamepadBindingLabel.X;
                    return true;
                case "SQUARE":
                    label = GamepadBindingLabel.Square;
                    return true;
                case "CIRCLE":
                    label = GamepadBindingLabel.Circle;
                    return true;
                case "TRIANGLE":
                    label = GamepadBindingLabel.Triangle;
                    return true;
                case "L1":
                    label = GamepadBindingLabel.L1;
                    return true;
                case "R1":
                    label = GamepadBindingLabel.R1;
                    return true;
                case "L2":
                    label = GamepadBindingLabel.L2;
                    return true;
                case "R2":
                    label = GamepadBindingLabel.R2;
                    return true;
                default:
                    label = GamepadBindingLabel.None;
                    return false;
            }
        }

        private static bool TryParseAction(string text, out GamepadBindingAction action)
        {
            string normalized = Normalize(text);
            switch (normalized)
            {
                case "JUMP":
                    action = GamepadBindingAction.Jump;
                    return true;
                case "SHOOTARROW":
                case "SHOOT":
                    action = GamepadBindingAction.ShootArrow;
                    return true;
                case "MELEEATTACK":
                case "MELEE":
                    action = GamepadBindingAction.MeleeAttack;
                    return true;
                case "ULT":
                case "ULTIMATE":
                    action = GamepadBindingAction.Ult;
                    return true;
                case "DASH":
                    action = GamepadBindingAction.Dash;
                    return true;
                default:
                    action = GamepadBindingAction.None;
                    return false;
            }
        }

        private static bool TryParseRawBinding(string text, out RawGamepadBinding rawBinding)
        {
            string normalized = Normalize(text);
            const string buttonPrefix = "BUTTON";
            if (normalized.StartsWith(buttonPrefix, StringComparison.Ordinal))
            {
                string numericToken = normalized.Substring(buttonPrefix.Length).TrimStart('_');
                if (int.TryParse(numericToken, out int buttonIndex) && buttonIndex >= 0 && buttonIndex <= 19)
                {
                    rawBinding = new RawGamepadBinding(buttonIndex);
                    return true;
                }
            }

            rawBinding = default;
            return false;
        }

        private static string Normalize(string text)
        {
            return string.IsNullOrWhiteSpace(text)
                ? string.Empty
                : text.Trim().Replace(" ", string.Empty).Replace("-", string.Empty).ToUpperInvariant();
        }
    }
}
