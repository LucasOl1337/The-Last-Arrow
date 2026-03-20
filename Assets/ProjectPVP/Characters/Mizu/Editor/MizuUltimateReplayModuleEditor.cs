using ProjectPVP.Gameplay;
using ProjectPVP.Editor;
using UnityEditor;
using UnityEngine;

namespace ProjectPVP.Characters.Mizu.Editor
{
    [CustomEditor(typeof(MizuUltimateReplayModule))]
    internal sealed class MizuUltimateReplayModuleEditor : UnityEditor.Editor
    {
        private SerializedProperty _replayAnimationProperty;
        private SerializedProperty _replayMovementProperty;
        private SerializedProperty _replayHitboxProperty;
        private SerializedProperty _shadowColorProperty;
        private SerializedProperty _glowColorProperty;
        private SerializedProperty _glowScaleProperty;
        private SerializedProperty _shadowSortingOffsetProperty;
        private SerializedProperty _shadowStartAlphaProperty;
        private SerializedProperty _shadowEndAlphaProperty;
        private SerializedProperty _glowStartAlphaProperty;
        private SerializedProperty _glowEndAlphaProperty;

        private void OnEnable()
        {
            _replayAnimationProperty = serializedObject.FindProperty("replayAnimation");
            _replayMovementProperty = serializedObject.FindProperty("replayMovement");
            _replayHitboxProperty = serializedObject.FindProperty("replayHitbox");
            _shadowColorProperty = serializedObject.FindProperty("shadowColor");
            _glowColorProperty = serializedObject.FindProperty("glowColor");
            _glowScaleProperty = serializedObject.FindProperty("glowScale");
            _shadowSortingOffsetProperty = serializedObject.FindProperty("shadowSortingOffset");
            _shadowStartAlphaProperty = serializedObject.FindProperty("shadowStartAlpha");
            _shadowEndAlphaProperty = serializedObject.FindProperty("shadowEndAlpha");
            _glowStartAlphaProperty = serializedObject.FindProperty("glowStartAlpha");
            _glowEndAlphaProperty = serializedObject.FindProperty("glowEndAlpha");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.HelpBox(
                "Este modulo agora concentra a configuracao do replay da ult da Mizu: animacao, fallbacks de movement, delay, duracao e hitbox. No Bootstrap, o objeto editavel fica como filho do Player da Mizu com o nome UltimateReplayHitbox.",
                MessageType.Info);

            DrawReplayAnimationSection();
            EditorGUILayout.Space(6f);
            DrawReplayMovementSection();
            EditorGUILayout.Space(6f);
            DrawReplayHitboxSection();
            EditorGUILayout.Space(6f);
            DrawShadowSection();

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawReplayAnimationSection()
        {
            EditorGUILayout.LabelField("Replay Animation", EditorStyles.boldLabel);

            SerializedProperty actionNameProperty = _replayAnimationProperty.FindPropertyRelative("actionName");
            SerializedProperty fallbackActionNameProperty = _replayAnimationProperty.FindPropertyRelative("fallbackActionName");
            SerializedProperty fallbackFramesPerSecondProperty = _replayAnimationProperty.FindPropertyRelative("fallbackFramesPerSecond");
            SerializedProperty loopProperty = _replayAnimationProperty.FindPropertyRelative("loop");

            EditorGUILayout.PropertyField(actionNameProperty, new GUIContent("Action Key"));
            EditorGUILayout.PropertyField(fallbackActionNameProperty, new GUIContent("Fallback Action"));
            EditorGUILayout.PropertyField(fallbackFramesPerSecondProperty, new GUIContent("Fallback FPS"));
            EditorGUILayout.PropertyField(loopProperty, new GUIContent("Loop"));

            EditorGUILayout.HelpBox(
                "Use uma action dedicada, como ult_replay, para controlar a velocidade dos frames no Action Config da Mizu. Se nao existir clip para essa action, o modulo usa o Fallback Action, mas ainda tenta respeitar a speed configurada na key principal.",
                MessageType.Info);
        }

        private void DrawReplayHitboxSection()
        {
            EditorGUILayout.LabelField("Replay Hitbox", EditorStyles.boldLabel);

            SerializedProperty enabledProperty = _replayHitboxProperty.FindPropertyRelative("enabled");
            SerializedProperty shapeKindProperty = _replayHitboxProperty.FindPropertyRelative("shapeKind");
            SerializedProperty mirrorXProperty = _replayHitboxProperty.FindPropertyRelative("mirrorX");
            SerializedProperty localOffsetProperty = _replayHitboxProperty.FindPropertyRelative("localOffset");
            SerializedProperty sizeProperty = _replayHitboxProperty.FindPropertyRelative("size");
            SerializedProperty radiusProperty = _replayHitboxProperty.FindPropertyRelative("radius");
            SerializedProperty angleProperty = _replayHitboxProperty.FindPropertyRelative("angle");
            SerializedProperty capsuleDirectionProperty = _replayHitboxProperty.FindPropertyRelative("capsuleDirection");

            EditorGUILayout.PropertyField(enabledProperty, new GUIContent("Use Dedicated Replay Hitbox"));
            if (!enabledProperty.boolValue)
            {
                EditorGUILayout.HelpBox("Desligado, o replay usa a hitbox final da ultimate normal como fallback.", MessageType.None);
                return;
            }

            EditorGUILayout.PropertyField(shapeKindProperty, new GUIContent("Shape"));
            EditorGUILayout.PropertyField(mirrorXProperty, new GUIContent("Mirror X"));
            EditorGUILayout.PropertyField(localOffsetProperty, new GUIContent("Local Offset"));

            int shapeKind = shapeKindProperty.enumValueIndex;
            if (shapeKind == (int)CombatShapeKind.Circle)
            {
                EditorGUILayout.PropertyField(radiusProperty, new GUIContent("Radius"));
            }
            else
            {
                EditorGUILayout.PropertyField(sizeProperty, new GUIContent("Size"));
                EditorGUILayout.PropertyField(angleProperty, new GUIContent("Angle"));
                if (shapeKind == (int)CombatShapeKind.Capsule)
                {
                    EditorGUILayout.PropertyField(capsuleDirectionProperty, new GUIContent("Capsule Direction"));
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Copy From Selected Player Ultimate"))
                {
                    CopyFromSelectedPlayerUltimateHitbox();
                }

                if (GUILayout.Button("Copy From Selected Player Melee"))
                {
                    CopyFromSelectedPlayerMeleeHitbox();
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Create / Refresh Replay Anchor"))
                {
                    CreateOrRefreshReplayAnchor();
                }

                if (GUILayout.Button("Select Replay Anchor"))
                {
                    SelectReplayAnchor();
                }
            }

            EditorGUILayout.HelpBox(
                "No Bootstrap, selecione o PlayerController da Mizu para ver o handle amarelo arrastavel do replay, ou mova o objeto UltimateReplayHitbox manualmente. Depois edite o Collider2D desse objeto para ajustar a hitbox da sombra.",
                MessageType.Info);
        }

        private void DrawReplayMovementSection()
        {
            EditorGUILayout.LabelField("Replay Movement", EditorStyles.boldLabel);

            SerializedProperty mirrorXProperty = _replayMovementProperty.FindPropertyRelative("mirrorX");
            SerializedProperty localEndpointProperty = _replayMovementProperty.FindPropertyRelative("localEndpoint");
            SerializedProperty startDelayProperty = _replayMovementProperty.FindPropertyRelative("startDelay");
            SerializedProperty travelDurationProperty = _replayMovementProperty.FindPropertyRelative("travelDuration");

            EditorGUILayout.PropertyField(localEndpointProperty, new GUIContent("Default Endpoint"));
            EditorGUILayout.PropertyField(mirrorXProperty, new GUIContent("Mirror X"));
            EditorGUILayout.PropertyField(startDelayProperty, new GUIContent("Start Delay"));
            EditorGUILayout.PropertyField(travelDurationProperty, new GUIContent("Travel Duration"));

            EditorGUILayout.HelpBox(
                "Default Endpoint e Travel Duration sao fallbacks do modulo. Se a MizuDefinition tiver Ultimate Replay Dash Distance e Ultimate Replay Dash Duration preenchidos, eles passam na frente. Se existir o objeto UltimateReplayHitbox na cena, a posicao dele vira o endpoint editavel no Bootstrap.",
                MessageType.Info);
        }

        private void DrawShadowSection()
        {
            EditorGUILayout.LabelField("Shadow Visual", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_shadowColorProperty);
            EditorGUILayout.PropertyField(_glowColorProperty);
            EditorGUILayout.PropertyField(_glowScaleProperty);
            EditorGUILayout.PropertyField(_shadowSortingOffsetProperty);
            EditorGUILayout.PropertyField(_shadowStartAlphaProperty);
            EditorGUILayout.PropertyField(_shadowEndAlphaProperty);
            EditorGUILayout.PropertyField(_glowStartAlphaProperty);
            EditorGUILayout.PropertyField(_glowEndAlphaProperty);
        }

        private void CopyFromSelectedPlayerUltimateHitbox()
        {
            CopyFromSelectedAnchor(player => player.ultimateHitboxAnchor, "ultimate");
        }

        private void CopyFromSelectedPlayerMeleeHitbox()
        {
            CopyFromSelectedAnchor(player => player.meleeHitboxAnchor, "melee");
        }

        private void CopyFromSelectedAnchor(System.Func<PlayerController, PlayerCombatAnchor> resolver, string label)
        {
            PlayerController player = ResolveSelectedPlayer();
            if (player == null)
            {
                Debug.LogWarning("ProjectPVP: selecione um PlayerController na cena para copiar a hitbox de replay da Mizu.");
                return;
            }

            PlayerCombatAnchor anchor = resolver(player);
            if (anchor == null || anchor.AttachedCollider == null)
            {
                Debug.LogWarning("ProjectPVP: o Player selecionado nao possui hitbox de " + label + " authorizada com Collider2D.");
                return;
            }

            var module = (MizuUltimateReplayModule)target;
            Undo.RecordObject(module, "Copy Mizu Replay Hitbox");
            if (!module.CopyReplayHitboxFromAnchor(anchor))
            {
                Debug.LogWarning("ProjectPVP: nao foi possivel copiar a hitbox de " + label + " para o replay da Mizu.");
                return;
            }

            EditorUtility.SetDirty(module);
            serializedObject.Update();
            Debug.Log("ProjectPVP: hitbox de " + label + " copiada para o replay da Mizu.");
        }

        private static PlayerController ResolveSelectedPlayer()
        {
            if (Selection.activeGameObject == null)
            {
                return null;
            }

            return Selection.activeGameObject.GetComponentInParent<PlayerController>();
        }

        private static void CreateOrRefreshReplayAnchor()
        {
            PlayerController player = ResolveSelectedPlayer();
            if (player == null)
            {
                Debug.LogWarning("ProjectPVP: selecione o PlayerController da Mizu na cena para criar o UltimateReplayHitbox.");
                return;
            }

            PlayerControllerAnchorAuthoring.CreateOrRefresh(player);
            SelectReplayAnchorOn(player);
        }

        private static void SelectReplayAnchor()
        {
            PlayerController player = ResolveSelectedPlayer();
            if (player == null)
            {
                Debug.LogWarning("ProjectPVP: selecione o PlayerController da Mizu na cena para localizar o UltimateReplayHitbox.");
                return;
            }

            SelectReplayAnchorOn(player);
        }

        private static void SelectReplayAnchorOn(PlayerController player)
        {
            if (player == null)
            {
                return;
            }

            Transform replayAnchor = player.transform.Find("UltimateReplayHitbox");
            if (replayAnchor == null)
            {
                Debug.LogWarning("ProjectPVP: o Player selecionado ainda nao possui o filho UltimateReplayHitbox.");
                return;
            }

            Selection.activeGameObject = replayAnchor.gameObject;
            EditorGUIUtility.PingObject(replayAnchor.gameObject);
        }
    }
}
