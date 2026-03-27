using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ProjectPVP.EditorTools
{
    public static class ProjectPvpArenaCollisionTools
    {
        private const string GreyboxRootName = "Gameplay_Greybox";
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

        // ── GROUND-TRUTH WORLD COORDINATES extracted from _Recovery/0.unity ──
        // These are direct Unity world-space positions (no normalization needed).
        // Source: Assets/_Recovery/0.unity — the working reference scene.
        private static readonly EdgeStamp[] EdgeStamps =
        {
            // ── Walkable platform top surfaces ────────────────────────────────

            new EdgeStamp("Lower Left Ground",
                new Vector2(-1264.7925f, -435.1032f),
                new Vector2( -667.7438f, -442.6522f),
                new Vector2( -604.8356f, -498.0114f)),

            new EdgeStamp("Main Ramp",
                new Vector2(-793.5604f, -565.9524f),
                new Vector2(-612.9421f, -491.4210f),
                new Vector2(-668.5438f, -444.7473f),
                new Vector2(-575.8332f, -448.3095f),
                new Vector2(-510.3301f, -390.0785f),
                new Vector2(-464.4917f, -380.1967f),
                new Vector2(-377.8376f, -335.6317f),
                new Vector2(-326.8038f, -322.3172f),
                new Vector2(-153.7345f, -215.9421f)),

            new EdgeStamp("Center Bridge",
                new Vector2(-204.0611f, -263.7524f),
                new Vector2(-153.1168f, -213.4258f),
                new Vector2( 182.1729f, -213.4258f),
                new Vector2( 406.3524f, -220.9748f)),

            // WELDED: last point matches Right Lower Ledge end exactly
            new EdgeStamp("Right Lower Slope",
                new Vector2( 899.6585f, -400.3170f),
                new Vector2( 923.0845f, -718.6229f),
                new Vector2( 920.0106f, -715.5986f),
                new Vector2( 893.4595f, -395.7075f),
                new Vector2(1281.4905f, -396.6788f)),

            new EdgeStamp("Right Lower Ledge",
                new Vector2(1022.5819f, -406.0453f),
                new Vector2(1281.4905f, -396.6788f)),

            new EdgeStamp("Left Mid Platform",
                new Vector2(-618.3665f, 105.4300f),
                new Vector2(-266.5867f, 109.1841f),
                new Vector2(-257.5376f,  91.7969f)),

            new EdgeStamp("Left Upper Platform",
                new Vector2(-1268.6279f, 372.6032f),
                new Vector2( -874.3787f, 372.6032f)),

            new EdgeStamp("Upper Center Island",
                new Vector2(  40.6787f, 280.0601f),
                new Vector2( 410.6715f, 287.1154f)),

            new EdgeStamp("Upper Right Ledge",
                new Vector2( 896.9836f, 381.3927f),
                new Vector2(1281.3246f, 396.5228f)),

            new EdgeStamp("Right Mid Platform",
                new Vector2( 784.6702f, -79.3488f),
                new Vector2( 602.1859f, -25.4952f),
                new Vector2( 583.8926f,  11.4314f),
                new Vector2( 922.2067f,  15.9704f)),

            // ── Outer walls & floor closures ──────────────────────────────────

            new EdgeStamp("Far Left Wall",
                new Vector2(-1277.3738f,  319.6481f),
                new Vector2( -990.2047f,  294.7062f),
                new Vector2( -870.1959f,  359.9821f)),

            // WELDED: start matches Lower Left Ground end; end matches Bottom Closure start
            new EdgeStamp("Lower Left Ground Front Wall",
                new Vector2(-604.8356f, -498.0114f),
                new Vector2(-740.7175f, -605.2552f),
                new Vector2(-743.2338f, -694.0159f)),

            new EdgeStamp("Lower Left Ground Bottom Closure",
                new Vector2( -743.2338f, -694.0159f),
                new Vector2(-1279.8901f, -708.2452f)),

            // ADDED: closes the left side of the lower-left ground box (was 273u gap)
            new EdgeStamp("Lower Left Ground Left Wall",
                new Vector2(-1279.8901f, -708.2452f),
                new Vector2(-1264.7925f, -435.1032f)),

            // WELDED: last point matches Right Lower Slope Base Wall start exactly
            new EdgeStamp("Right Lower Ledge Outer Wall",
                new Vector2(1227.0225f, -434.5038f),
                new Vector2(1280.1850f, -397.2882f),
                new Vector2(1281.4905f, -725.7954f)),

            new EdgeStamp("Right Lower Ledge Bottom Closure",
                new Vector2(1227.0225f, -548.3381f),
                new Vector2(1273.7402f, -398.5508f)),

            new EdgeStamp("Right Lower Slope Base Wall",
                new Vector2(1290.5493f, -725.7954f),
                new Vector2( 916.8378f, -725.5774f)),

            new EdgeStamp("Upper Right Ledge Inner Wall",
                new Vector2(1275.2726f,  402.5747f),
                new Vector2(1267.5005f,   85.1920f),
                new Vector2(1270.7338f,  -36.6257f)),

            // ── Platform undersides (for up-jump collision) ───────────────────

            new EdgeStamp("Main Ramp Underside",
                new Vector2(-740.7175f, -605.2552f),
                new Vector2(-587.3870f, -562.5674f),
                new Vector2(-434.0566f, -477.1917f),
                new Vector2(-280.7264f, -391.8160f),
                new Vector2(-127.3959f, -320.6696f)),

            new EdgeStamp("Center Bridge Underside",
                new Vector2(-127.3959f, -320.6696f),
                new Vector2(  77.0445f, -334.8989f),
                new Vector2( 255.9301f, -334.8989f),
                new Vector2( 394.5542f, -303.0552f)),

            new EdgeStamp("Main Ramp Bridge Front Lip",
                new Vector2(-127.3959f, -320.6696f),
                new Vector2(-137.4613f, -218.4584f)),

            // WELDED: start matches Center Bridge Underside end; end matches Center Bridge end
            new EdgeStamp("Center Bridge Right Wall",
                new Vector2(394.5542f, -303.0552f),
                new Vector2(406.3524f, -220.9748f)),

            new EdgeStamp("Left Mid Platform Underside",
                new Vector2(-572.2570f,  18.7079f),
                new Vector2(-466.7690f,  23.4393f),
                new Vector2(-344.0264f,  27.6033f)),

            new EdgeStamp("Left Mid Platform Left Wall",
                new Vector2(-619.8795f, 103.9490f),
                new Vector2(-599.0181f,  42.5287f),
                new Vector2(-566.2370f,  14.6786f)),

            new EdgeStamp("Left Mid Platform Right Wall",
                new Vector2(-359.1244f, 25.0869f),
                new Vector2(-259.5443f, 91.3673f)),

            new EdgeStamp("Left Upper Platform Left Wall",
                new Vector2(-1270.1409f, 256.1020f),
                new Vector2(-1226.2637f, 248.5017f)),

            new EdgeStamp("Left Upper Platform Underside",
                new Vector2(-1226.2637f, 248.5017f),
                new Vector2(-1101.5142f, 247.8895f),
                new Vector2( -996.4340f, 277.2487f),
                new Vector2( -909.4053f, 285.7857f),
                new Vector2( -877.8784f, 312.4883f),
                new Vector2( -852.7954f, 349.8806f),
                new Vector2( -879.0960f, 366.8220f),
                new Vector2( -856.3887f, 350.1966f)),

            new EdgeStamp("Upper Center Island Left Wall",
                new Vector2( 39.6915f, 272.0173f),
                new Vector2(133.9708f, 184.9042f)),

            new EdgeStamp("Upper Center Island Right Wall",
                new Vector2(410.4356f, 287.2051f),
                new Vector2(432.2992f, 229.2397f)),

            new EdgeStamp("Upper Center Island Underside",
                new Vector2(124.7966f, 192.4853f),
                new Vector2(224.4848f, 170.0521f),
                new Vector2(281.4851f, 123.7334f),
                new Vector2(364.0396f, 133.4425f),
                new Vector2(431.5157f, 226.9031f),
                new Vector2(405.4029f, 279.6562f)),

            new EdgeStamp("Right Mid Platform Left Wall",
                new Vector2(613.7009f, -21.8547f),
                new Vector2(699.4442f, -38.8215f),
                new Vector2(693.3922f, -49.4125f)),

            new EdgeStamp("Right Mid Platform Underside",
                new Vector2(684.3141f, -46.3865f),
                new Vector2(743.1092f, -61.6202f),
                new Vector2(792.5864f, -78.7718f),
                new Vector2(893.7651f, -27.0156f),
                new Vector2(911.2864f,  -8.3215f),
                new Vector2(918.8489f,  20.7979f)),

            new EdgeStamp("Upper Right Ledge Underside",
                new Vector2(1252.5775f,   91.9796f),
                new Vector2(1271.3450f,   -0.6324f),
                new Vector2(1160.7823f,    2.4239f),
                new Vector2(1099.7422f,    2.1604f),
                new Vector2(1052.5100f,   56.9273f),
                new Vector2(1008.0419f,  235.4500f),
                new Vector2( 981.8886f,  260.5035f),
                new Vector2( 945.9167f,  276.9602f),
                new Vector2( 888.8810f,  344.9197f),
                new Vector2( 900.0096f,  379.8797f)),
        };

        [MenuItem("ProjectPVP/Environment/Stamp Default Arena Collisions")]
        public static void StampDefaultArenaCollisions()
        {
            Transform greyboxRoot = FindOrCreateGreyboxRoot();

            if (greyboxRoot == null)
            {
                EditorUtility.DisplayDialog(
                    "Arena collisions",
                    "Nao encontrei o root 'Gameplay_Greybox' na cena ativa.",
                    "OK");
                return;
            }

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Stamp Default Arena Collisions");

            ClearAutoCollisionsInternal(greyboxRoot);

            for (int index = 0; index < EdgeStamps.Length; index += 1)
            {
                CreateEdge(greyboxRoot, EdgeStamps[index]);
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

        // Creates an EdgeCollider2D using direct world-space coordinates.
        // The GameObject sits at world origin (0,0,0) so collider points = world positions.
        private static void CreateEdge(Transform parent, EdgeStamp stamp)
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
                worldPoints[index] = stamp.Points[index];
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
    }
}
