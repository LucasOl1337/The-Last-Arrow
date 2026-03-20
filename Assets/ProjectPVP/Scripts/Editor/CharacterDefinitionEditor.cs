using System.Collections.Generic;
using System.IO;
using ProjectPVP.Data;
using UnityEditor;
using UnityEngine;

namespace ProjectPVP.Editor
{
    [CustomEditor(typeof(CharacterDefinition))]
    public sealed class CharacterDefinitionEditor : UnityEditor.Editor
    {
        private UnityEditor.Editor _mechanicsModuleInlineEditor;

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            CharacterDefinition definition = (CharacterDefinition)target;

            DrawHeader(definition);
            DrawToolbar(definition);
            EditorGUILayout.Space(6f);
            EditorGUILayout.HelpBox(
                "Este asset agora e a fonte unica de verdade do personagem. Tuning de movimento, colisor, projeteis, acoes e audio devem ser editados aqui.",
                MessageType.Info);

            DrawDefaultInspector();

            if (definition != null && definition.mechanicsModule != null)
            {
                EditorGUILayout.Space(8f);
                DrawInlineMechanicsModuleInspector(definition.mechanicsModule);
            }

            EditorGUILayout.Space(8f);
            DrawAnimationSummary(definition);

            serializedObject.ApplyModifiedProperties();
        }

        private static void DrawHeader(CharacterDefinition definition)
        {
            string displayName = definition != null && !string.IsNullOrWhiteSpace(definition.displayName)
                ? definition.displayName
                : "Character Definition";
            string id = definition != null ? definition.id : string.Empty;

            EditorGUILayout.LabelField(displayName, EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Id: " + id, EditorStyles.miniLabel);
        }

        private void DrawToolbar(CharacterDefinition definition)
        {
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Ping Data"))
            {
                EditorGUIUtility.PingObject(target);
            }

            if (GUILayout.Button("Ping Animations"))
            {
                PingSiblingFolder("Animations");
            }

            if (GUILayout.Button("Ping Rotations"))
            {
                PingSiblingFolder("Rotations");
            }

            if (GUILayout.Button("Ping Audio"))
            {
                if (definition != null && definition.audioDefinition != null)
                {
                    EditorGUIUtility.PingObject(definition.audioDefinition);
                }
            }

            if (GUILayout.Button("Sync PixelLab"))
            {
                if (ProjectPvpPixelLabImportTools.SyncFromPixelLab(definition, out string summary))
                {
                    Debug.Log(summary);
                    serializedObject.Update();
                }
                else
                {
                    Debug.LogWarning(summary);
                }
            }

            if (GUILayout.Button("Waifu2x"))
            {
                if (ProjectPvpWaifu2xSpriteUpgradeTools.TryUpscaleCharacterSprites(definition, out string summary))
                {
                    Debug.Log(summary);
                    serializedObject.Update();
                }
                else
                {
                    Debug.LogWarning(summary);
                }
            }

            if (GUILayout.Button("Sync Clips"))
            {
                if (ProjectPvpCharacterAnimationSync.RebuildFromFolders(definition, out string summary))
                {
                    Debug.Log(summary);
                    serializedObject.Update();
                }
                else
                {
                    Debug.LogWarning(summary);
                }
            }

            if (GUILayout.Button("Sync All Characters"))
            {
                ProjectPvpCharacterAnimationSync.RebuildAllCharactersFromMenu();
                serializedObject.Update();
            }

            if (GUILayout.Button("Open Folder"))
            {
                string assetPath = AssetDatabase.GetAssetPath(target);
                if (!string.IsNullOrWhiteSpace(assetPath))
                {
                    EditorUtility.RevealInFinder(assetPath);
                }
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawInlineMechanicsModuleInspector(Object mechanicsModuleObject)
        {
            if (mechanicsModuleObject == null)
            {
                return;
            }

            EditorGUILayout.LabelField("Mechanics Module Settings", EditorStyles.boldLabel);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                CreateCachedEditor(mechanicsModuleObject, null, ref _mechanicsModuleInlineEditor);
                _mechanicsModuleInlineEditor?.OnInspectorGUI();
            }
        }

        private void PingSiblingFolder(string folderName)
        {
            string assetPath = AssetDatabase.GetAssetPath(target);
            if (string.IsNullOrWhiteSpace(assetPath))
            {
                return;
            }

            string dataFolderPath = Path.GetDirectoryName(assetPath)?.Replace("\\", "/");
            if (string.IsNullOrWhiteSpace(dataFolderPath))
            {
                return;
            }

            string characterRoot = Path.GetDirectoryName(dataFolderPath)?.Replace("\\", "/");
            if (string.IsNullOrWhiteSpace(characterRoot))
            {
                return;
            }

            string folderPath = characterRoot + "/" + folderName;
            Object folderAsset = AssetDatabase.LoadAssetAtPath<Object>(folderPath);
            if (folderAsset != null)
            {
                EditorGUIUtility.PingObject(folderAsset);
            }
        }

        private static void DrawAnimationSummary(CharacterDefinition definition)
        {
            IReadOnlyList<ActionSpriteAnimation> animations = definition != null ? definition.GetActionAnimations() : null;
            if (animations == null || animations.Count == 0)
            {
                EditorGUILayout.HelpBox("Nenhum clip configurado.", MessageType.Warning);
                return;
            }

            var clipCounts = new Dictionary<string, int>();
            var frameCounts = new Dictionary<string, int>();
            int totalFrames = 0;

            for (int index = 0; index < animations.Count; index += 1)
            {
                ActionSpriteAnimation clip = animations[index];
                if (clip == null || string.IsNullOrWhiteSpace(clip.actionName))
                {
                    continue;
                }

                if (!clipCounts.ContainsKey(clip.actionName))
                {
                    clipCounts[clip.actionName] = 0;
                    frameCounts[clip.actionName] = 0;
                }

                int frameCount = clip.frames != null ? clip.frames.Count : 0;
                clipCounts[clip.actionName] += 1;
                frameCounts[clip.actionName] += frameCount;
                totalFrames += frameCount;
            }

            EditorGUILayout.LabelField("Animation Summary", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Clips: " + animations.Count + " | Frames: " + totalFrames,
                MessageType.None);

            foreach (KeyValuePair<string, int> pair in clipCounts)
            {
                int clipCount = pair.Value;
                int frameCount = frameCounts[pair.Key];
                EditorGUILayout.LabelField(pair.Key, clipCount + " clips / " + frameCount + " frames");
            }
        }
    }

    internal static class ProjectPvpCharacterMaintenance
    {
        [MenuItem("ProjectPVP/Characters/Reserialize Character Assets")]
        private static void ReserializeCharacterAssets()
        {
            var assetPaths = new List<string>();
            foreach (CharacterDefinition definition in ProjectPvpCharacterAssetPaths.EnumerateDefinitions())
            {
                string assetPath = AssetDatabase.GetAssetPath(definition);
                if (!string.IsNullOrWhiteSpace(assetPath))
                {
                    assetPaths.Add(assetPath);
                }
            }

            if (assetPaths.Count == 0)
            {
                Debug.LogWarning("ProjectPVP: nenhum CharacterDefinition encontrado para reserializar.");
                return;
            }

            AssetDatabase.ForceReserializeAssets(assetPaths);
            AssetDatabase.SaveAssets();
            Debug.Log("ProjectPVP: assets de personagem reserializados com sucesso.");
        }
    }
}
