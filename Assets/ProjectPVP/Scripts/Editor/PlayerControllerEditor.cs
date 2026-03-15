using ProjectPVP.Data;
using ProjectPVP.Gameplay;
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
            EditorGUILayout.LabelField("Scene Anchors", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Cria filhos editaveis no hierarchy para Spawn, ProjectileOrigin, MeleeHitbox e UltimateHitbox. Depois disso voce pode mover/ajustar esses anchors manualmente na cena.", MessageType.Info);

            if (GUILayout.Button("Create / Refresh Scene Anchors"))
            {
                PlayerControllerAnchorAuthoring.CreateOrRefresh(controller);
            }
        }
    }

    internal static class PlayerControllerAnchorAuthoring
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

            EditorUtility.SetDirty(controller);
            EditorSceneManager.MarkSceneDirty(controller.gameObject.scene);
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
    }
}
