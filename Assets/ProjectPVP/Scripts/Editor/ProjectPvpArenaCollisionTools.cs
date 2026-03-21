using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ProjectPVP.EditorTools
{
    public static class ProjectPvpArenaCollisionTools
    {
        private const string GreyboxRootName = "Gameplay_Greybox";
        private const string BackgroundSpriteName = "backg";
        private const string AutoPrefix = "[AUTO_COLLISION] ";

        private readonly struct EdgeStamp
        {
            public EdgeStamp(string name, params Vector2[] points)
            {
                Name = name;
                Points = points;
            }

            public string Name { get; }
            public IReadOnlyList<Vector2> Points { get; }
        }

        private static readonly EdgeStamp[] EdgeStamps =
        {
            new EdgeStamp(
                "Lower Left Ground",
                new Vector2(0.00f, 0.11f),
                new Vector2(0.21f, 0.11f)),
            new EdgeStamp(
                "Lower Left Ground Front Wall",
                new Vector2(0.21f, 0.11f),
                new Vector2(0.21f, 0.07f),
                new Vector2(0.21f, 0.02f)),
            new EdgeStamp(
                "Lower Left Ground Bottom Closure",
                new Vector2(0.21f, 0.02f),
                new Vector2(0.00f, 0.01f)),
            new EdgeStamp(
                "Far Left Wall",
                new Vector2(0.00f, 0.72f),
                new Vector2(0.00f, 0.11f)),
            new EdgeStamp(
                "Main Ramp",
                new Vector2(0.21f, 0.11f),
                new Vector2(0.26f, 0.15f),
                new Vector2(0.31f, 0.20f),
                new Vector2(0.37f, 0.26f),
                new Vector2(0.42f, 0.31f)),
            new EdgeStamp(
                "Main Ramp Underside",
                new Vector2(0.21f, 0.07f),
                new Vector2(0.27f, 0.10f),
                new Vector2(0.33f, 0.16f),
                new Vector2(0.39f, 0.22f),
                new Vector2(0.45f, 0.27f)),
            new EdgeStamp(
                "Main Ramp Bridge Front Lip",
                new Vector2(0.45f, 0.27f),
                new Vector2(0.45f, 0.31f)),
            new EdgeStamp(
                "Center Bridge",
                new Vector2(0.42f, 0.31f),
                new Vector2(0.50f, 0.31f),
                new Vector2(0.58f, 0.31f),
                new Vector2(0.65f, 0.31f)),
            new EdgeStamp(
                "Center Bridge Underside",
                new Vector2(0.45f, 0.27f),
                new Vector2(0.53f, 0.26f),
                new Vector2(0.60f, 0.26f),
                new Vector2(0.67f, 0.27f)),
            new EdgeStamp(
                "Center Bridge Right Wall",
                new Vector2(0.67f, 0.27f),
                new Vector2(0.65f, 0.31f)),
            new EdgeStamp(
                "Left Mid Platform",
                new Vector2(0.25f, 0.55f),
                new Vector2(0.39f, 0.55f)),
            new EdgeStamp(
                "Left Mid Platform Left Wall",
                new Vector2(0.25f, 0.55f),
                new Vector2(0.27f, 0.50f)),
            new EdgeStamp(
                "Left Mid Platform Underside",
                new Vector2(0.27f, 0.50f),
                new Vector2(0.33f, 0.48f),
                new Vector2(0.38f, 0.49f)),
            new EdgeStamp(
                "Left Mid Platform Right Wall",
                new Vector2(0.38f, 0.49f),
                new Vector2(0.39f, 0.55f)),
            new EdgeStamp(
                "Left Upper Platform",
                new Vector2(0.02f, 0.72f),
                new Vector2(0.15f, 0.72f)),
            new EdgeStamp(
                "Left Upper Platform Left Wall",
                new Vector2(0.02f, 0.72f),
                new Vector2(0.02f, 0.67f)),
            new EdgeStamp(
                "Left Upper Platform Underside",
                new Vector2(0.02f, 0.67f),
                new Vector2(0.07f, 0.66f),
                new Vector2(0.12f, 0.67f),
                new Vector2(0.16f, 0.70f)),
            new EdgeStamp(
                "Upper Center Island",
                new Vector2(0.54f, 0.66f),
                new Vector2(0.67f, 0.66f)),
            new EdgeStamp(
                "Upper Center Island Left Wall",
                new Vector2(0.54f, 0.66f),
                new Vector2(0.57f, 0.62f)),
            new EdgeStamp(
                "Upper Center Island Underside",
                new Vector2(0.57f, 0.62f),
                new Vector2(0.61f, 0.60f),
                new Vector2(0.65f, 0.60f),
                new Vector2(0.69f, 0.63f)),
            new EdgeStamp(
                "Upper Center Island Right Wall",
                new Vector2(0.69f, 0.63f),
                new Vector2(0.67f, 0.66f)),
            new EdgeStamp(
                "Upper Right Ledge",
                new Vector2(0.81f, 0.74f),
                new Vector2(0.99f, 0.74f)),
            new EdgeStamp(
                "Upper Right Ledge Inner Wall",
                new Vector2(0.99f, 0.74f),
                new Vector2(0.99f, 0.56f)),
            new EdgeStamp(
                "Upper Right Ledge Underside",
                new Vector2(0.99f, 0.56f),
                new Vector2(0.96f, 0.58f),
                new Vector2(0.92f, 0.63f),
                new Vector2(0.87f, 0.69f),
                new Vector2(0.81f, 0.74f)),
            new EdgeStamp(
                "Right Mid Platform",
                new Vector2(0.74f, 0.48f),
                new Vector2(0.84f, 0.48f)),
            new EdgeStamp(
                "Right Mid Platform Left Wall",
                new Vector2(0.74f, 0.48f),
                new Vector2(0.77f, 0.45f)),
            new EdgeStamp(
                "Right Mid Platform Underside",
                new Vector2(0.77f, 0.45f),
                new Vector2(0.81f, 0.44f),
                new Vector2(0.86f, 0.46f)),
            new EdgeStamp(
                "Right Lower Slope",
                new Vector2(0.74f, 0.18f),
                new Vector2(0.79f, 0.22f),
                new Vector2(0.84f, 0.28f),
                new Vector2(0.89f, 0.35f)),
            new EdgeStamp(
                "Right Lower Slope Base Wall",
                new Vector2(0.89f, 0.35f),
                new Vector2(0.89f, 0.23f)),
            new EdgeStamp(
                "Right Lower Ledge",
                new Vector2(0.90f, 0.21f),
                new Vector2(0.98f, 0.19f)),
            new EdgeStamp(
                "Right Lower Ledge Outer Wall",
                new Vector2(0.98f, 0.19f),
                new Vector2(0.98f, 0.11f)),
            new EdgeStamp(
                "Right Lower Ledge Bottom Closure",
                new Vector2(0.98f, 0.11f),
                new Vector2(0.90f, 0.11f)),
        };

        [MenuItem("ProjectPVP/Environment/Stamp Default Arena Collisions")]
        public static void StampDefaultArenaCollisions()
        {
            Transform greyboxRoot = FindOrCreateGreyboxRoot();
            SpriteRenderer backgroundRenderer = FindBackgroundRenderer();

            if (greyboxRoot == null || backgroundRenderer == null || backgroundRenderer.sprite == null)
            {
                EditorUtility.DisplayDialog(
                    "Arena collisions",
                    "Nao encontrei o root 'Gameplay_Greybox' ou o sprite 'backg' na cena ativa.",
                    "OK");
                return;
            }

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Stamp Default Arena Collisions");

            ClearAutoCollisionsInternal(greyboxRoot);

            Bounds bounds = backgroundRenderer.bounds;
            for (int index = 0; index < EdgeStamps.Length; index += 1)
            {
                CreateEdge(greyboxRoot, bounds, EdgeStamps[index]);
            }

            EditorSceneManager.MarkSceneDirty(greyboxRoot.gameObject.scene);
            Undo.CollapseUndoOperations(undoGroup);
            Selection.activeGameObject = greyboxRoot.gameObject;
        }

        [MenuItem("ProjectPVP/Environment/Clear Auto Arena Collisions")]
        public static void ClearAutoArenaCollisions()
        {
            Transform greyboxRoot = FindOrCreateGreyboxRoot();
            if (greyboxRoot == null)
            {
                return;
            }

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Clear Auto Arena Collisions");

            ClearAutoCollisionsInternal(greyboxRoot);
            EditorSceneManager.MarkSceneDirty(greyboxRoot.gameObject.scene);

            Undo.CollapseUndoOperations(undoGroup);
            Selection.activeGameObject = greyboxRoot.gameObject;
        }

        private static void CreateEdge(Transform parent, Bounds bounds, EdgeStamp stamp)
        {
            var collisionObject = new GameObject($"{AutoPrefix}{stamp.Name}");
            Undo.RegisterCreatedObjectUndo(collisionObject, $"Create {collisionObject.name}");
            collisionObject.transform.position = Vector3.zero;
            collisionObject.transform.rotation = Quaternion.identity;
            collisionObject.transform.localScale = Vector3.one;
            collisionObject.transform.SetParent(parent, true);

            EdgeCollider2D collider = Undo.AddComponent<EdgeCollider2D>(collisionObject);
            Vector2[] worldPoints = new Vector2[stamp.Points.Count];
            for (int index = 0; index < stamp.Points.Count; index += 1)
            {
                worldPoints[index] = ResolvePoint(bounds, stamp.Points[index]);
            }

            collider.points = worldPoints;
            collider.edgeRadius = 0f;
        }

        private static void ClearAutoCollisionsInternal(Transform parent)
        {
            var toDelete = new List<GameObject>();
            for (int index = 0; index < parent.childCount; index += 1)
            {
                Transform child = parent.GetChild(index);
                if (child != null && child.name.StartsWith(AutoPrefix))
                {
                    toDelete.Add(child.gameObject);
                }
            }

            for (int index = 0; index < toDelete.Count; index += 1)
            {
                Undo.DestroyObjectImmediate(toDelete[index]);
            }
        }

        private static Transform FindOrCreateGreyboxRoot()
        {
            GameObject root = GameObject.Find(GreyboxRootName);
            if (root != null)
            {
                return root.transform;
            }

            root = new GameObject(GreyboxRootName);
            Undo.RegisterCreatedObjectUndo(root, "Create Gameplay_Greybox");
            return root.transform;
        }

        private static SpriteRenderer FindBackgroundRenderer()
        {
            GameObject backgroundObject = GameObject.Find(BackgroundSpriteName);
            if (backgroundObject != null)
            {
                return backgroundObject.GetComponent<SpriteRenderer>();
            }

            return Object.FindFirstObjectByType<SpriteRenderer>();
        }

        private static Vector2 ResolvePoint(Bounds bounds, Vector2 normalizedPoint)
        {
            return new Vector2(
                Mathf.Lerp(bounds.min.x, bounds.max.x, normalizedPoint.x),
                Mathf.Lerp(bounds.min.y, bounds.max.y, normalizedPoint.y));
        }
    }
}
