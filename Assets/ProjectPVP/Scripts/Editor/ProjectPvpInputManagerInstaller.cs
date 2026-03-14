using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ProjectPVP.Editor
{
    [InitializeOnLoad]
    internal static class ProjectPvpInputManagerInstaller
    {
        private const string InputManagerAssetPath = "ProjectSettings/InputManager.asset";
        private const int MaxSupportedGamepads = 4;

        private readonly struct AxisSpec
        {
            public AxisSpec(string name, int axis, int joyNum, bool invert)
            {
                this.name = name;
                this.axis = axis;
                this.joyNum = joyNum;
                this.invert = invert;
            }

            public readonly string name;
            public readonly int axis;
            public readonly int joyNum;
            public readonly bool invert;
        }

        private static readonly AxisSpec[] RequiredAxes = BuildRequiredAxes();

        static ProjectPvpInputManagerInstaller()
        {
            EditorApplication.delayCall += EnsureAxesInstalled;
        }

        [MenuItem("ProjectPVP/Install Input Axes")]
        public static void InstallInputAxes()
        {
            EnsureAxesInstalled();
        }

        private static AxisSpec[] BuildRequiredAxes()
        {
            var axes = new List<AxisSpec>(MaxSupportedGamepads * 11);
            for (int joystick = 1; joystick <= MaxSupportedGamepads; joystick += 1)
            {
                axes.Add(new AxisSpec("ProjectPVP_GamepadMoveX_P" + joystick, 0, joystick, false));
                axes.Add(new AxisSpec("ProjectPVP_GamepadMoveY_P" + joystick, 1, joystick, true));
                axes.Add(new AxisSpec("ProjectPVP_GamepadDpadX_P" + joystick, 6, joystick, false));
                axes.Add(new AxisSpec("ProjectPVP_GamepadDpadY_P" + joystick, 7, joystick, true));
                axes.Add(new AxisSpec("ProjectPVP_GamepadLookX_A_P" + joystick, 3, joystick, false));
                axes.Add(new AxisSpec("ProjectPVP_GamepadLookY_A_P" + joystick, 4, joystick, true));
                axes.Add(new AxisSpec("ProjectPVP_GamepadLookX_B_P" + joystick, 2, joystick, false));
                axes.Add(new AxisSpec("ProjectPVP_GamepadLookY_B_P" + joystick, 5, joystick, true));
                axes.Add(new AxisSpec("ProjectPVP_GamepadTriggerR_A_P" + joystick, 5, joystick, false));
                axes.Add(new AxisSpec("ProjectPVP_GamepadTriggerR_B_P" + joystick, 8, joystick, false));
                axes.Add(new AxisSpec("ProjectPVP_GamepadTriggerR_C_P" + joystick, 9, joystick, false));
            }

            return axes.ToArray();
        }

        private static void EnsureAxesInstalled()
        {
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath(InputManagerAssetPath);
            if (assets == null || assets.Length == 0)
            {
                return;
            }

            var serializedObject = new SerializedObject(assets[0]);
            SerializedProperty axesProperty = serializedObject.FindProperty("m_Axes");
            if (axesProperty == null || !axesProperty.isArray)
            {
                return;
            }

            bool changed = false;
            for (int index = 0; index < RequiredAxes.Length; index += 1)
            {
                AxisSpec spec = RequiredAxes[index];
                if (HasAxis(axesProperty, spec.name))
                {
                    continue;
                }

                AddAxis(axesProperty, spec);
                changed = true;
            }

            if (!changed)
            {
                return;
            }

            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(assets[0]);
            AssetDatabase.SaveAssets();
            Debug.Log("ProjectPVP: eixos de gamepad P1-P4 instalados no InputManager.");
        }

        private static bool HasAxis(SerializedProperty axesProperty, string axisName)
        {
            for (int index = 0; index < axesProperty.arraySize; index += 1)
            {
                SerializedProperty axisProperty = axesProperty.GetArrayElementAtIndex(index);
                SerializedProperty nameProperty = axisProperty.FindPropertyRelative("m_Name");
                if (nameProperty != null && nameProperty.stringValue == axisName)
                {
                    return true;
                }
            }

            return false;
        }

        private static void AddAxis(SerializedProperty axesProperty, AxisSpec spec)
        {
            axesProperty.InsertArrayElementAtIndex(axesProperty.arraySize);
            SerializedProperty axisProperty = axesProperty.GetArrayElementAtIndex(axesProperty.arraySize - 1);
            axisProperty.FindPropertyRelative("m_Name").stringValue = spec.name;
            axisProperty.FindPropertyRelative("descriptiveName").stringValue = string.Empty;
            axisProperty.FindPropertyRelative("descriptiveNegativeName").stringValue = string.Empty;
            axisProperty.FindPropertyRelative("negativeButton").stringValue = string.Empty;
            axisProperty.FindPropertyRelative("positiveButton").stringValue = string.Empty;
            axisProperty.FindPropertyRelative("altNegativeButton").stringValue = string.Empty;
            axisProperty.FindPropertyRelative("altPositiveButton").stringValue = string.Empty;
            axisProperty.FindPropertyRelative("gravity").floatValue = 0f;
            axisProperty.FindPropertyRelative("dead").floatValue = 0.19f;
            axisProperty.FindPropertyRelative("sensitivity").floatValue = 1f;
            axisProperty.FindPropertyRelative("snap").boolValue = false;
            axisProperty.FindPropertyRelative("invert").boolValue = spec.invert;
            axisProperty.FindPropertyRelative("type").intValue = 2;
            axisProperty.FindPropertyRelative("axis").intValue = spec.axis;
            axisProperty.FindPropertyRelative("joyNum").intValue = spec.joyNum;
        }
    }
}
