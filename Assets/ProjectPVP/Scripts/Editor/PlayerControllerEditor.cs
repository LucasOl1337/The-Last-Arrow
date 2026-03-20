using ProjectPVP.Characters;
using ProjectPVP.Data;
using ProjectPVP.Gameplay;
using ProjectPVP.Match;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ProjectPVP.Editor
{
    [CustomEditor(typeof(PlayerController))]
    internal sealed class PlayerControllerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var controller = (PlayerController)target;
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                "Fluxo recomendado: configure o slot e o personagem no MatchController > roster. " +
                "Os campos slotProfile e characterDefinition aqui no PlayerController servem como preview local e fallback de compatibilidade.",
                MessageType.Info);
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Scene Anchors", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Cria filhos editaveis no hierarchy para Spawn, ProjectileOrigin, MeleeHitbox, UltimateHitbox e anchors extras vindos do Mechanics Module. Para anchors extras como o replay da ult da Mizu, selecione o PlayerController raiz para ver o handle amarelo arrastavel na Scene View, ou mova o objeto filho no hierarchy. O Collider2D desse filho continua controlando a hitbox.", MessageType.Info);

            if (GUILayout.Button("Create / Refresh Scene Anchors"))
            {
                PlayerControllerAnchorAuthoring.CreateOrRefresh(controller);
            }

            DrawBootstrapProfileActions(controller);
            DrawMechanicsAnchorShortcuts(controller);
        }

        private void OnSceneGUI()
        {
            var controller = (PlayerController)target;
            if (controller == null || controller.transform == null)
            {
                return;
            }

            DrawAdditionalAnchorHandles(controller);
        }

        private static void DrawMechanicsAnchorShortcuts(PlayerController controller)
        {
            if (controller == null || controller.characterDefinition == null || controller.characterDefinition.mechanicsModule == null)
            {
                return;
            }

            foreach (CharacterMechanicsSceneAnchorDefinition definition in controller.characterDefinition.mechanicsModule.GetAdditionalSceneAnchors(controller, controller.characterDefinition))
            {
                if (definition == null || string.IsNullOrWhiteSpace(definition.childName))
                {
                    continue;
                }

                string childName = definition.childName.Trim();
                if (GUILayout.Button("Select " + childName))
                {
                    PlayerControllerAnchorAuthoring.CreateOrRefresh(controller);
                    Transform child = controller.transform.Find(childName);
                    if (child != null)
                    {
                        Selection.activeGameObject = child.gameObject;
                        EditorGUIUtility.PingObject(child.gameObject);
                    }
                }
            }
        }

        private static void DrawBootstrapProfileActions(PlayerController controller)
        {
            if (controller == null)
            {
                return;
            }

            CharacterBootstrapProfile profile = ResolveBootstrapProfile(controller);
            if (profile == null)
            {
                EditorGUILayout.HelpBox(
                    "Nenhum CharacterBootstrapProfile foi encontrado para este PlayerController. Ajustes nos anchors feitos so na cena podem ser sobrescritos em Play Mode.",
                    MessageType.Warning);
                return;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Bootstrap Profile", EditorStyles.boldLabel);
            EditorGUILayout.ObjectField("Resolved Profile", profile, typeof(CharacterBootstrapProfile), false);
            EditorGUILayout.HelpBox(
                "Use o botao abaixo para copiar SpawnAnchor, MeleeHitbox e UltimateHitbox da cena para o CharacterBootstrapProfile. Isso faz os ajustes sobreviverem ao Play Mode.",
                MessageType.Info);

            if (GUILayout.Button("Save Scene Anchors To Bootstrap Profile"))
            {
                SaveSceneAnchorsToProfile(controller, profile);
            }
        }

        private static CharacterBootstrapProfile ResolveBootstrapProfile(PlayerController controller)
        {
            MatchController matchController = Object.FindFirstObjectByType<MatchController>();
            CombatantSlotConfig slot = matchController != null ? matchController.GetSlotForController(controller) : null;
            if (slot?.characterProfile != null)
            {
                return slot.characterProfile;
            }

            string[] profileGuids = AssetDatabase.FindAssets("t:CharacterBootstrapProfile");
            for (int index = 0; index < profileGuids.Length; index += 1)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(profileGuids[index]);
                CharacterBootstrapProfile profile = AssetDatabase.LoadAssetAtPath<CharacterBootstrapProfile>(assetPath);
                if (profile != null && profile.characterDefinition == controller.characterDefinition)
                {
                    return profile;
                }
            }

            return null;
        }

        private static void SaveSceneAnchorsToProfile(PlayerController controller, CharacterBootstrapProfile profile)
        {
            if (controller == null || profile == null)
            {
                return;
            }

            if (controller.spawnAnchor == null || controller.meleeHitboxAnchor == null || controller.ultimateHitboxAnchor == null)
            {
                EditorUtility.DisplayDialog(
                    "Bootstrap Profile",
                    "O PlayerController precisa ter SpawnAnchor, MeleeHitbox e UltimateHitbox atribuidos antes de salvar no perfil.",
                    "OK");
                return;
            }

            Undo.RecordObject(profile, "Save Scene Anchors To Bootstrap Profile");

            profile.spawnAnchor = BuildAnchorConfig(controller.spawnAnchor, PlayerCombatAnchorKind.Spawn, "SpawnAnchor");
            profile.meleeHitbox = BuildAnchorConfig(controller.meleeHitboxAnchor, PlayerCombatAnchorKind.MeleeHitbox, "MeleeHitbox");
            profile.ultimateHitbox = BuildAnchorConfig(controller.ultimateHitboxAnchor, PlayerCombatAnchorKind.UltimateHitbox, "UltimateHitbox");

            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();

            EditorUtility.DisplayDialog(
                "Bootstrap Profile",
                "Anchors da cena copiados para o CharacterBootstrapProfile com sucesso.",
                "OK");
        }

        private static CharacterBootstrapAnchorConfig BuildAnchorConfig(PlayerCombatAnchor anchor, PlayerCombatAnchorKind fallbackKind, string fallbackName)
        {
            var config = new CharacterBootstrapAnchorConfig
            {
                childName = anchor != null && !string.IsNullOrWhiteSpace(anchor.name) ? anchor.name : fallbackName,
                anchorKind = anchor != null ? anchor.anchorKind : fallbackKind,
                mirrorX = anchor != null && anchor.mirrorX,
                localPosition = anchor != null ? (Vector2)anchor.transform.localPosition : Vector2.zero,
                localEulerAngles = anchor != null ? anchor.transform.localEulerAngles : Vector3.zero,
                collider = BuildColliderConfig(anchor),
            };

            return config;
        }

        private static CharacterBootstrapColliderConfig BuildColliderConfig(PlayerCombatAnchor anchor)
        {
            var colliderConfig = new CharacterBootstrapColliderConfig
            {
                shapeKind = CharacterBootstrapColliderShape.None,
                offset = Vector2.zero,
                size = anchor != null ? anchor.boxSize : new Vector2(96f, 72f),
                radius = anchor != null ? anchor.radius : 96f,
                capsuleDirection = CapsuleDirection2D.Horizontal,
            };

            if (anchor == null)
            {
                return colliderConfig;
            }

            switch (anchor.AttachedCollider)
            {
                case BoxCollider2D box:
                    colliderConfig.shapeKind = CharacterBootstrapColliderShape.Box;
                    colliderConfig.offset = box.offset;
                    colliderConfig.size = box.size;
                    break;
                case CircleCollider2D circle:
                    colliderConfig.shapeKind = CharacterBootstrapColliderShape.Circle;
                    colliderConfig.offset = circle.offset;
                    colliderConfig.radius = circle.radius;
                    break;
                case CapsuleCollider2D capsule:
                    colliderConfig.shapeKind = CharacterBootstrapColliderShape.Capsule;
                    colliderConfig.offset = capsule.offset;
                    colliderConfig.size = capsule.size;
                    colliderConfig.capsuleDirection = capsule.direction;
                    break;
                default:
                    colliderConfig.shapeKind = anchor.anchorKind == PlayerCombatAnchorKind.Spawn
                        ? CharacterBootstrapColliderShape.None
                        : CharacterBootstrapColliderShape.Box;
                    break;
            }

            return colliderConfig;
        }

        private static void DrawAdditionalAnchorHandles(PlayerController controller)
        {
            if (controller == null || controller.characterDefinition == null || controller.characterDefinition.mechanicsModule == null)
            {
                return;
            }

            Vector3 rootPosition = controller.transform.position;
            foreach (CharacterMechanicsSceneAnchorDefinition definition in controller.characterDefinition.mechanicsModule.GetAdditionalSceneAnchors(controller, controller.characterDefinition))
            {
                if (definition == null || string.IsNullOrWhiteSpace(definition.childName))
                {
                    continue;
                }

                Transform child = controller.transform.Find(definition.childName.Trim());
                if (child == null)
                {
                    continue;
                }

                float handleSize = HandleUtility.GetHandleSize(child.position) * 0.16f;
                Handles.color = new Color(1f, 0.72f, 0.18f, 0.95f);
                Handles.DrawLine(rootPosition, child.position);
                Handles.DrawWireDisc(rootPosition, Vector3.forward, handleSize * 0.9f);

                EditorGUI.BeginChangeCheck();
                Vector3 updatedPosition = Handles.FreeMoveHandle(
                    child.position,
                    handleSize,
                    Vector3.zero,
                    Handles.CircleHandleCap);

                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(child, "Move " + child.name);
                    child.position = updatedPosition;

                    var anchor = child.GetComponent<PlayerCombatAnchor>();
                    if (anchor != null)
                    {
                        Undo.RecordObject(anchor, "Update " + child.name + " Anchor");
                        anchor.CaptureAuthoredPose();
                        EditorUtility.SetDirty(anchor);
                    }

                    EditorUtility.SetDirty(child);
                    EditorSceneManager.MarkSceneDirty(child.gameObject.scene);
                }

                Handles.Label(
                    child.position + new Vector3(handleSize * 0.75f, handleSize * 0.55f, 0f),
                    child.name);
            }
        }
    }

    public static class PlayerControllerAnchorAuthoring
    {
        public static void CreateOrRefresh(PlayerController controller)
        {
            if (controller == null)
            {
                return;
            }

            Undo.RegisterFullObjectHierarchyUndo(controller.gameObject, "Create Player Scene Anchors");

            Vector2 colliderSize = ResolveColliderSize(controller);
            Vector2 colliderOffset = ResolveColliderOffset(controller);

            controller.spawnAnchor = EnsureAnchor(
                controller,
                controller.spawnAnchor,
                "SpawnAnchor",
                PlayerCombatAnchorKind.Spawn,
                Vector3.zero,
                Vector2.zero,
                0f,
                mirrorX: false);

            controller.projectileOrigin = EnsureProjectileOrigin(controller);

            Vector3 meleeLocalPosition = new Vector3(
                colliderOffset.x + (colliderSize.x * 0.65f) + 12f,
                colliderOffset.y + (colliderSize.y * 0.15f),
                0f);
            Vector2 meleeSize = ResolveMeleeSize(controller, colliderSize);
            controller.meleeHitboxAnchor = EnsureAnchor(
                controller,
                controller.meleeHitboxAnchor,
                "MeleeHitbox",
                PlayerCombatAnchorKind.MeleeHitbox,
                meleeLocalPosition,
                meleeSize,
                0f,
                mirrorX: true);

            float ultimateRadius = Mathf.Max(180f * 0.7f, colliderSize.x * 1.4f);
            Vector3 ultimateLocalPosition = new Vector3(
                colliderOffset.x + (colliderSize.x * 0.6f) + (ultimateRadius * 0.4f),
                colliderOffset.y + (colliderSize.y * 0.1f),
                0f);
            controller.ultimateHitboxAnchor = EnsureAnchor(
                controller,
                controller.ultimateHitboxAnchor,
                "UltimateHitbox",
                PlayerCombatAnchorKind.UltimateHitbox,
                ultimateLocalPosition,
                Vector2.zero,
                ultimateRadius,
                mirrorX: true);

            CreateAdditionalMechanicsAnchors(controller);

            EditorUtility.SetDirty(controller);
            EditorSceneManager.MarkSceneDirty(controller.gameObject.scene);
        }

        private static void CreateAdditionalMechanicsAnchors(PlayerController controller)
        {
            if (controller == null || controller.characterDefinition == null || controller.characterDefinition.mechanicsModule == null)
            {
                return;
            }

            foreach (CharacterMechanicsSceneAnchorDefinition definition in controller.characterDefinition.mechanicsModule.GetAdditionalSceneAnchors(controller, controller.characterDefinition))
            {
                if (definition == null || string.IsNullOrWhiteSpace(definition.childName))
                {
                    continue;
                }

                EnsureAdditionalAnchor(controller, definition);
            }
        }

        private static PlayerCombatAnchor EnsureAdditionalAnchor(PlayerController controller, CharacterMechanicsSceneAnchorDefinition definition)
        {
            PlayerCombatAnchor anchor = null;
            Transform existingChild = controller.transform.Find(definition.childName);
            if (existingChild != null)
            {
                anchor = existingChild.GetComponent<PlayerCombatAnchor>();
            }

            bool created = false;
            if (anchor == null)
            {
                var child = new GameObject(definition.childName);
                Undo.RegisterCreatedObjectUndo(child, "Create " + definition.childName);
                child.transform.SetParent(controller.transform, false);
                anchor = Undo.AddComponent<PlayerCombatAnchor>(child);
                created = true;
            }

            anchor.anchorKind = definition.anchorKind;
            anchor.mirrorX = definition.mirrorX;

            if (created)
            {
                anchor.transform.localPosition = definition.localPosition;
                anchor.transform.localEulerAngles = definition.localEulerAngles;
                anchor.boxSize = definition.boxSize;
                anchor.radius = definition.radius;
            }

            EnsureAdditionalAnchorCollider(anchor, definition);
            EditorUtility.SetDirty(anchor);
            return anchor;
        }

        private static PlayerCombatAnchor EnsureAnchor(
            PlayerController controller,
            PlayerCombatAnchor existingAnchor,
            string childName,
            PlayerCombatAnchorKind kind,
            Vector3 defaultLocalPosition,
            Vector2 defaultBoxSize,
            float defaultRadius,
            bool mirrorX)
        {
            PlayerCombatAnchor anchor = existingAnchor;
            if (anchor == null)
            {
                Transform existingChild = controller.transform.Find(childName);
                if (existingChild != null)
                {
                    anchor = existingChild.GetComponent<PlayerCombatAnchor>();
                }
            }

            bool created = false;
            if (anchor == null)
            {
                var child = new GameObject(childName);
                Undo.RegisterCreatedObjectUndo(child, "Create " + childName);
                child.transform.SetParent(controller.transform, false);
                anchor = Undo.AddComponent<PlayerCombatAnchor>(child);
                created = true;
            }

            anchor.anchorKind = kind;
            anchor.mirrorX = mirrorX;

            if (created)
            {
                anchor.transform.localPosition = defaultLocalPosition;
                anchor.boxSize = defaultBoxSize;
                anchor.radius = defaultRadius;
            }

            EnsureAnchorCollider(anchor, kind, defaultBoxSize, defaultRadius);

            EditorUtility.SetDirty(anchor);
            return anchor;
        }

        private static Transform EnsureProjectileOrigin(PlayerController controller)
        {
            if (controller.projectileOrigin != null)
            {
                return controller.projectileOrigin;
            }

            Transform existingChild = controller.transform.Find("ProjectileOrigin");
            if (existingChild != null)
            {
                return existingChild;
            }

            var child = new GameObject("ProjectileOrigin");
            Undo.RegisterCreatedObjectUndo(child, "Create ProjectileOrigin");
            child.transform.SetParent(controller.transform, false);
            child.transform.localPosition = ResolveProjectileOriginLocalPosition(controller);
            return child.transform;
        }

        private static Vector3 ResolveProjectileOriginLocalPosition(PlayerController controller)
        {
            if (controller.characterDefinition != null)
            {
                Vector2 offset = controller.characterDefinition.projectileOriginOffset;
                return new Vector3(Mathf.Abs(offset.x), offset.y, 0f);
            }

            return new Vector3(48f, 72f, 0f);
        }

        private static Vector2 ResolveColliderSize(PlayerController controller)
        {
            if (controller.characterDefinition != null)
            {
                return controller.characterDefinition.colliderSize;
            }

            return controller.bodyCollider != null ? controller.bodyCollider.size : new Vector2(90f, 210f);
        }

        private static Vector2 ResolveColliderOffset(PlayerController controller)
        {
            if (controller.characterDefinition != null)
            {
                return controller.characterDefinition.colliderOffset;
            }

            return controller.bodyCollider != null ? controller.bodyCollider.offset : Vector2.zero;
        }

        private static Vector2 ResolveMeleeSize(PlayerController controller, Vector2 colliderSize)
        {
            ActionColliderOverride overrideData = controller.characterDefinition != null
                ? controller.characterDefinition.FindActionColliderOverride("melee")
                : null;
            if (overrideData != null)
            {
                return overrideData.size;
            }

            return new Vector2(
                Mathf.Max(72f, colliderSize.x * 0.85f),
                Mathf.Max(64f, colliderSize.y * 0.45f));
        }

        private static void EnsureAnchorCollider(PlayerCombatAnchor anchor, PlayerCombatAnchorKind kind, Vector2 defaultBoxSize, float defaultRadius)
        {
            if (anchor == null || kind == PlayerCombatAnchorKind.Spawn)
            {
                return;
            }

            Collider2D existingCollider = anchor.GetComponent<Collider2D>();
            if (existingCollider != null)
            {
                existingCollider.isTrigger = true;
                return;
            }

            switch (kind)
            {
                case PlayerCombatAnchorKind.MeleeHitbox:
                {
                    BoxCollider2D box = Undo.AddComponent<BoxCollider2D>(anchor.gameObject);
                    box.isTrigger = true;
                    box.size = defaultBoxSize;
                    box.offset = Vector2.zero;
                    break;
                }
                case PlayerCombatAnchorKind.UltimateHitbox:
                {
                    CircleCollider2D circle = Undo.AddComponent<CircleCollider2D>(anchor.gameObject);
                    circle.isTrigger = true;
                    circle.radius = defaultRadius;
                    circle.offset = Vector2.zero;
                    break;
                }
            }
        }

        private static void EnsureAdditionalAnchorCollider(PlayerCombatAnchor anchor, CharacterMechanicsSceneAnchorDefinition definition)
        {
            if (anchor == null || definition == null)
            {
                return;
            }

            Collider2D existingCollider = anchor.GetComponent<Collider2D>();
            if (existingCollider != null)
            {
                existingCollider.isTrigger = true;
                return;
            }

            switch (definition.shapeKind)
            {
                case CombatShapeKind.Circle:
                {
                    CircleCollider2D circle = Undo.AddComponent<CircleCollider2D>(anchor.gameObject);
                    circle.isTrigger = true;
                    circle.radius = Mathf.Max(1f, definition.radius);
                    circle.offset = definition.colliderOffset;
                    break;
                }
                case CombatShapeKind.Capsule:
                {
                    CapsuleCollider2D capsule = Undo.AddComponent<CapsuleCollider2D>(anchor.gameObject);
                    capsule.isTrigger = true;
                    capsule.size = definition.boxSize;
                    capsule.direction = definition.capsuleDirection;
                    capsule.offset = definition.colliderOffset;
                    break;
                }
                default:
                {
                    BoxCollider2D box = Undo.AddComponent<BoxCollider2D>(anchor.gameObject);
                    box.isTrigger = true;
                    box.size = definition.boxSize;
                    box.offset = definition.colliderOffset;
                    break;
                }
            }
        }
    }
}
