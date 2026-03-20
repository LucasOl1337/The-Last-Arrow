using System.Collections.Generic;
using ProjectPVP.Characters;
using ProjectPVP.Match;
using UnityEditor;
using UnityEngine;

namespace ProjectPVP.Editor
{
    [CustomEditor(typeof(MatchController))]
    internal sealed class MatchControllerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawPropertiesExcluding(serializedObject, "m_Script");
            serializedObject.ApplyModifiedProperties();

            DrawCharacterCatalogSummary((MatchController)target);
        }

        private static void DrawCharacterCatalogSummary(MatchController matchController)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Character Catalog", EditorStyles.boldLabel);

            if (matchController == null || matchController.characterCatalog == null)
            {
                EditorGUILayout.HelpBox(
                    "Associe um CharacterCatalog para manter a lista de personagens visivel e editavel no projeto.",
                    MessageType.Info);
                return;
            }

            IReadOnlyList<CharacterBootstrapProfile> availableCharacters = matchController.AvailableCharacters;
            if (availableCharacters.Count == 0)
            {
                EditorGUILayout.HelpBox("O CharacterCatalog atual nao possui personagens cadastrados.", MessageType.Warning);
                return;
            }

            EditorGUILayout.HelpBox(
                "Os slots do roster selecionam personagens desta lista. Edite os assets de CharacterBootstrapProfile para ajustar config, hitboxes e projectile prefab.",
                MessageType.None);

            for (int index = 0; index < availableCharacters.Count; index += 1)
            {
                CharacterBootstrapProfile profile = availableCharacters[index];
                if (profile == null)
                {
                    continue;
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.ObjectField(profile.ResolveDisplayName(), profile, typeof(CharacterBootstrapProfile), false);
                    if (GUILayout.Button("Select", GUILayout.Width(60f)))
                    {
                        Selection.activeObject = profile;
                        EditorGUIUtility.PingObject(profile);
                    }
                }
            }
        }
    }
}
