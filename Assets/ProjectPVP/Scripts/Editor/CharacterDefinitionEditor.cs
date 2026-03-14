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
        private SerializedProperty _idProperty;
        private SerializedProperty _displayNameProperty;
        private SerializedProperty _defaultSpriteProperty;
        private SerializedProperty _spriteScaleProperty;
        private SerializedProperty _spriteAnchorOffsetProperty;
        private SerializedProperty _overridesStatsProperty;
        private SerializedProperty _moveSpeedProperty;
        private SerializedProperty _accelerationProperty;
        private SerializedProperty _frictionProperty;
        private SerializedProperty _jumpVelocityProperty;
        private SerializedProperty _gravityProperty;
        private SerializedProperty _maxFallSpeedProperty;
        private SerializedProperty _shootCooldownProperty;
        private SerializedProperty _maxArrowsProperty;
        private SerializedProperty _meleeCooldownProperty;
        private SerializedProperty _meleeDurationProperty;
        private SerializedProperty _colliderSizeProperty;
        private SerializedProperty _colliderOffsetProperty;
        private SerializedProperty _wallJumpHorizontalForceProperty;
        private SerializedProperty _wallJumpVerticalForceProperty;
        private SerializedProperty _wallSlideSpeedProperty;
        private SerializedProperty _wallGravityScaleProperty;
        private SerializedProperty _runtimeMoveScaleProperty;
        private SerializedProperty _runtimeJumpScaleProperty;
        private SerializedProperty _runtimeGravityScaleProperty;
        private SerializedProperty _runtimeDashScaleProperty;
        private SerializedProperty _dashMultiplierProperty;
        private SerializedProperty _dashDurationProperty;
        private SerializedProperty _dashCooldownProperty;
        private SerializedProperty _dashDistanceProperty;
        private SerializedProperty _dashUpwardMultiplierProperty;
        private SerializedProperty _projectileForwardProperty;
        private SerializedProperty _projectileForwardFacingProperty;
        private SerializedProperty _projectileVerticalOffsetProperty;
        private SerializedProperty _projectileInheritVelocityFactorProperty;
        private SerializedProperty _projectileScaleProperty;
        private SerializedProperty _projectileOriginModeProperty;
        private SerializedProperty _projectileOriginOffsetProperty;
        private SerializedProperty _projectileUseBowNodeProperty;
        private SerializedProperty _projectileSpriteProperty;
        private SerializedProperty _actionConfigProperty;
        private SerializedProperty _actionAnimationDurationsProperty;
        private SerializedProperty _actionAnimationCancelableProperty;
        private SerializedProperty _actionAnimationSpeedsProperty;
        private SerializedProperty _actionColliderOverridesProperty;
        private SerializedProperty _actionSpriteAnimationsProperty;

        private bool _showGameplay = true;
        private bool _showMovementTuning = true;
        private bool _showProjectile = true;
        private bool _showActionTuning = true;
        private bool _showAnimationSummary = true;
        private bool _showRawAnimations;

        private void OnEnable()
        {
            _idProperty = serializedObject.FindProperty("id");
            _displayNameProperty = serializedObject.FindProperty("displayName");
            _defaultSpriteProperty = serializedObject.FindProperty("defaultSprite");
            _spriteScaleProperty = serializedObject.FindProperty("spriteScale");
            _spriteAnchorOffsetProperty = serializedObject.FindProperty("spriteAnchorOffset");
            _overridesStatsProperty = serializedObject.FindProperty("overridesStats");
            _moveSpeedProperty = serializedObject.FindProperty("moveSpeed");
            _accelerationProperty = serializedObject.FindProperty("acceleration");
            _frictionProperty = serializedObject.FindProperty("friction");
            _jumpVelocityProperty = serializedObject.FindProperty("jumpVelocity");
            _gravityProperty = serializedObject.FindProperty("gravity");
            _maxFallSpeedProperty = serializedObject.FindProperty("maxFallSpeed");
            _shootCooldownProperty = serializedObject.FindProperty("shootCooldown");
            _maxArrowsProperty = serializedObject.FindProperty("maxArrows");
            _meleeCooldownProperty = serializedObject.FindProperty("meleeCooldown");
            _meleeDurationProperty = serializedObject.FindProperty("meleeDuration");
            _colliderSizeProperty = serializedObject.FindProperty("colliderSize");
            _colliderOffsetProperty = serializedObject.FindProperty("colliderOffset");
            _wallJumpHorizontalForceProperty = serializedObject.FindProperty("wallJumpHorizontalForce");
            _wallJumpVerticalForceProperty = serializedObject.FindProperty("wallJumpVerticalForce");
            _wallSlideSpeedProperty = serializedObject.FindProperty("wallSlideSpeed");
            _wallGravityScaleProperty = serializedObject.FindProperty("wallGravityScale");
            _runtimeMoveScaleProperty = serializedObject.FindProperty("runtimeMoveScale");
            _runtimeJumpScaleProperty = serializedObject.FindProperty("runtimeJumpScale");
            _runtimeGravityScaleProperty = serializedObject.FindProperty("runtimeGravityScale");
            _runtimeDashScaleProperty = serializedObject.FindProperty("runtimeDashScale");
            _dashMultiplierProperty = serializedObject.FindProperty("dashMultiplier");
            _dashDurationProperty = serializedObject.FindProperty("dashDuration");
            _dashCooldownProperty = serializedObject.FindProperty("dashCooldown");
            _dashDistanceProperty = serializedObject.FindProperty("dashDistance");
            _dashUpwardMultiplierProperty = serializedObject.FindProperty("dashUpwardMultiplier");
            _projectileForwardProperty = serializedObject.FindProperty("projectileForward");
            _projectileForwardFacingProperty = serializedObject.FindProperty("projectileForwardFacing");
            _projectileVerticalOffsetProperty = serializedObject.FindProperty("projectileVerticalOffset");
            _projectileInheritVelocityFactorProperty = serializedObject.FindProperty("projectileInheritVelocityFactor");
            _projectileScaleProperty = serializedObject.FindProperty("projectileScale");
            _projectileOriginModeProperty = serializedObject.FindProperty("projectileOriginMode");
            _projectileOriginOffsetProperty = serializedObject.FindProperty("projectileOriginOffset");
            _projectileUseBowNodeProperty = serializedObject.FindProperty("projectileUseBowNode");
            _projectileSpriteProperty = serializedObject.FindProperty("projectileSprite");
            _actionConfigProperty = serializedObject.FindProperty("actionConfig");
            _actionAnimationDurationsProperty = serializedObject.FindProperty("actionAnimationDurations");
            _actionAnimationCancelableProperty = serializedObject.FindProperty("actionAnimationCancelable");
            _actionAnimationSpeedsProperty = serializedObject.FindProperty("actionAnimationSpeeds");
            _actionColliderOverridesProperty = serializedObject.FindProperty("actionColliderOverrides");
            _actionSpriteAnimationsProperty = serializedObject.FindProperty("actionSpriteAnimations");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            CharacterDefinition definition = (CharacterDefinition)target;

            DrawHeader(definition);
            DrawFolderToolbar();
            EditorGUILayout.Space(6f);

            EditorGUILayout.PropertyField(_idProperty);
            EditorGUILayout.PropertyField(_displayNameProperty);
            EditorGUILayout.PropertyField(_defaultSpriteProperty);
            EditorGUILayout.PropertyField(_spriteScaleProperty);
            EditorGUILayout.PropertyField(_spriteAnchorOffsetProperty);

            _showGameplay = EditorGUILayout.BeginFoldoutHeaderGroup(_showGameplay, "Gameplay Core");
            if (_showGameplay)
            {
                EditorGUILayout.PropertyField(_overridesStatsProperty);
                EditorGUILayout.PropertyField(_moveSpeedProperty);
                EditorGUILayout.PropertyField(_accelerationProperty);
                EditorGUILayout.PropertyField(_frictionProperty);
                EditorGUILayout.PropertyField(_jumpVelocityProperty);
                EditorGUILayout.PropertyField(_gravityProperty);
                EditorGUILayout.PropertyField(_maxFallSpeedProperty);
                EditorGUILayout.PropertyField(_shootCooldownProperty);
                EditorGUILayout.PropertyField(_maxArrowsProperty);
                EditorGUILayout.PropertyField(_meleeCooldownProperty);
                EditorGUILayout.PropertyField(_meleeDurationProperty);
                EditorGUILayout.PropertyField(_colliderSizeProperty);
                EditorGUILayout.PropertyField(_colliderOffsetProperty);
            }

            EditorGUILayout.EndFoldoutHeaderGroup();

            _showMovementTuning = EditorGUILayout.BeginFoldoutHeaderGroup(_showMovementTuning, "Movement Tuning");
            if (_showMovementTuning)
            {
                EditorGUILayout.PropertyField(_wallJumpHorizontalForceProperty);
                EditorGUILayout.PropertyField(_wallJumpVerticalForceProperty);
                EditorGUILayout.PropertyField(_wallSlideSpeedProperty);
                EditorGUILayout.PropertyField(_wallGravityScaleProperty);
                EditorGUILayout.PropertyField(_runtimeMoveScaleProperty);
                EditorGUILayout.PropertyField(_runtimeJumpScaleProperty);
                EditorGUILayout.PropertyField(_runtimeGravityScaleProperty);
                EditorGUILayout.PropertyField(_runtimeDashScaleProperty);
                EditorGUILayout.PropertyField(_dashMultiplierProperty);
                EditorGUILayout.PropertyField(_dashDurationProperty);
                EditorGUILayout.PropertyField(_dashCooldownProperty);
                EditorGUILayout.PropertyField(_dashDistanceProperty);
                EditorGUILayout.PropertyField(_dashUpwardMultiplierProperty);
            }

            EditorGUILayout.EndFoldoutHeaderGroup();

            _showProjectile = EditorGUILayout.BeginFoldoutHeaderGroup(_showProjectile, "Projectile");
            if (_showProjectile)
            {
                EditorGUILayout.PropertyField(_projectileForwardProperty);
                EditorGUILayout.PropertyField(_projectileForwardFacingProperty);
                EditorGUILayout.PropertyField(_projectileVerticalOffsetProperty);
                EditorGUILayout.PropertyField(_projectileInheritVelocityFactorProperty);
                EditorGUILayout.PropertyField(_projectileScaleProperty);
                EditorGUILayout.PropertyField(_projectileOriginModeProperty);
                EditorGUILayout.PropertyField(_projectileOriginOffsetProperty);
                EditorGUILayout.PropertyField(_projectileUseBowNodeProperty);
                EditorGUILayout.PropertyField(_projectileSpriteProperty);
            }

            EditorGUILayout.EndFoldoutHeaderGroup();

            _showActionTuning = EditorGUILayout.BeginFoldoutHeaderGroup(_showActionTuning, "Action Tuning");
            if (_showActionTuning)
            {
                EditorGUILayout.PropertyField(_actionConfigProperty, new GUIContent("Action Config"));
                EditorGUILayout.HelpBox("Use as mesmas action keys dos clips: dash, shoot, melee, ult, jump_start, jump_air, running, aim.", MessageType.None);

                if (_actionConfigProperty.objectReferenceValue != null)
                {
                    EditorGUILayout.BeginHorizontal();
                    if (GUILayout.Button("Ping Action Config Asset"))
                    {
                        EditorGUIUtility.PingObject(_actionConfigProperty.objectReferenceValue);
                    }

                    if (GUILayout.Button("Selecionar Action Config"))
                    {
                        Selection.activeObject = _actionConfigProperty.objectReferenceValue;
                    }
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.HelpBox("Os timings e cancel windows deste personagem agora devem ser editados no asset Action Config dentro da pasta Data.", MessageType.Info);
                }
                else
                {
                    EditorGUILayout.PropertyField(_actionAnimationDurationsProperty, new GUIContent("Animation Durations"), true);
                    EditorGUILayout.PropertyField(_actionAnimationCancelableProperty, new GUIContent("Animation Cancelable"), true);
                    EditorGUILayout.PropertyField(_actionAnimationSpeedsProperty, new GUIContent("Animation Speeds"), true);
                    EditorGUILayout.PropertyField(_actionColliderOverridesProperty, new GUIContent("Collider Overrides"), true);
                }
            }

            EditorGUILayout.EndFoldoutHeaderGroup();

            _showAnimationSummary = EditorGUILayout.BeginFoldoutHeaderGroup(_showAnimationSummary, "Animation Summary");
            if (_showAnimationSummary)
            {
                DrawAnimationSummary(definition);
            }

            EditorGUILayout.EndFoldoutHeaderGroup();

            _showRawAnimations = EditorGUILayout.BeginFoldoutHeaderGroup(_showRawAnimations, "Raw Animation Clips");
            if (_showRawAnimations)
            {
                EditorGUILayout.HelpBox("Fluxo recomendado: coloque os PNGs em Animations/<acao>/<left|right|east|west>/ e clique em Sync Clips. O runtime usa somente left/right; qualquer outra pasta e ignorada.", MessageType.Info);
                if (GUILayout.Button("Rebuild Clips From Folders"))
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

                EditorGUILayout.PropertyField(_actionSpriteAnimationsProperty, true);
            }

            EditorGUILayout.EndFoldoutHeaderGroup();

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawHeader(CharacterDefinition definition)
        {
            string displayName = definition != null && !string.IsNullOrWhiteSpace(definition.displayName)
                ? definition.displayName
                : "Character Definition";
            string id = definition != null ? definition.id : string.Empty;

            EditorGUILayout.LabelField(displayName, EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Id: " + id, EditorStyles.miniLabel);
        }

        private void DrawFolderToolbar()
        {
            CharacterDefinition definition = (CharacterDefinition)target;
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

            if (GUILayout.Button("Ping Action Config"))
            {
                PingActionConfig();
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

            if (GUILayout.Button("Optimize Imports"))
            {
                EditorApplication.ExecuteMenuItem("ProjectPVP/Characters/Optimize Character Sprite Imports");
                serializedObject.Update();
            }

            if (GUILayout.Button("Abrir Pasta"))
            {
                string assetPath = AssetDatabase.GetAssetPath(target);
                if (!string.IsNullOrWhiteSpace(assetPath))
                {
                    EditorUtility.RevealInFinder(assetPath);
                }
            }

            EditorGUILayout.EndHorizontal();
        }

        private void PingActionConfig()
        {
            if (_actionConfigProperty == null || _actionConfigProperty.objectReferenceValue == null)
            {
                return;
            }

            EditorGUIUtility.PingObject(_actionConfigProperty.objectReferenceValue);
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
            if (definition == null || definition.actionSpriteAnimations == null || definition.actionSpriteAnimations.Count == 0)
            {
                EditorGUILayout.HelpBox("Nenhum clip configurado.", MessageType.Warning);
                return;
            }

            var clipCounts = new Dictionary<string, int>();
            var frameCounts = new Dictionary<string, int>();
            int totalFrames = 0;

            for (int index = 0; index < definition.actionSpriteAnimations.Count; index += 1)
            {
                ActionSpriteAnimation clip = definition.actionSpriteAnimations[index];
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

            EditorGUILayout.HelpBox(
                "Clips: " + definition.actionSpriteAnimations.Count + " | Frames: " + totalFrames,
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
            string[] guids = AssetDatabase.FindAssets("t:CharacterDefinition", new[] { "Assets/ProjectPVP/Characters" });
            var assetPaths = new List<string>(guids.Length);
            for (int index = 0; index < guids.Length; index += 1)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guids[index]);
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
