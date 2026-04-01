using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace ProjectPVP.Gameplay
{
    /// <summary>
    /// Renders gizmos for player debugging and visualization.
    /// </summary>
    public sealed class PlayerGizmosSystem : MonoBehaviour
    {
        private PlayerController _controller;

        private void Awake()
        {
            _controller = GetComponent<PlayerController>();
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (_controller == null)
            {
                _controller = GetComponent<PlayerController>();
            }

            if (_controller == null)
            {
                return;
            }

            DrawMeleeGizmo();
            DrawUltimateGizmos();
        }

        private void OnDrawGizmos()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            if (_controller == null)
            {
                _controller = GetComponent<PlayerController>();
            }
        }

        private void DrawMeleeGizmo()
        {
            if (!Application.isPlaying || !_controller.IsMeleeActive)
            {
                return;
            }

            Gizmos.color = new Color(1f, 0.25f, 0.25f, 0.85f);
            DrawHitboxShapeGizmo(_controller.MeleeHitboxCenter, _controller.MeleeHitboxSize, 0f);
        }

        private void DrawUltimateGizmos()
        {
            Gizmos.color = new Color(1f, 0.2f, 0.85f, 0.95f);
            DrawHitboxShapeGizmo(_controller.UltimateHitboxCenter, Vector2.zero, _controller.UltimateHitboxRadius);
        }

        private void DrawHitboxShapeGizmo(Vector2 center, Vector2 size, float radius)
        {
            if (radius > 0.01f)
            {
                DrawPreviewShapeGizmo(center, Vector2.zero, radius, CombatShapeKind.Circle, 0f, CapsuleDirection2D.Horizontal);
            }
            else
            {
                DrawPreviewShapeGizmo(center, size, 0f, CombatShapeKind.Box, 0f, CapsuleDirection2D.Horizontal);
            }
        }

        private static void DrawPreviewShapeGizmo(
            Vector2 center,
            Vector2 size,
            float radius,
            CombatShapeKind shapeKind,
            float shapeAngle,
            CapsuleDirection2D capsuleDirection)
        {
            Color previousHandlesColor = Handles.color;
            Color outlineColor = Gizmos.color;
            Color fillColor = new Color(outlineColor.r, outlineColor.g, outlineColor.b, 0.28f);

            switch (shapeKind)
            {
                case CombatShapeKind.Box:
                    DrawSolidOrWireBox(center, size, shapeAngle, fillColor, outlineColor);
                    break;
                case CombatShapeKind.Capsule:
                    DrawSolidOrWireCapsule(center, size, shapeAngle, capsuleDirection, fillColor, outlineColor);
                    break;
                default:
                    DrawSolidOrWireCircle(center, radius, fillColor, outlineColor);
                    break;
            }

            Handles.color = previousHandlesColor;
        }

        private static void DrawSolidOrWireCircle(Vector2 center, float radius, Color fillColor, Color outlineColor)
        {
            float resolvedRadius = Mathf.Max(1f, radius);
            Handles.color = fillColor;
            Handles.DrawSolidDisc(center, Vector3.forward, resolvedRadius);
            Handles.color = outlineColor;
            Handles.DrawWireDisc(center, Vector3.forward, resolvedRadius);
        }

        private static void DrawSolidOrWireBox(Vector2 center, Vector2 size, float angle, Color fillColor, Color outlineColor)
        {
            Vector2 halfSize = size * 0.5f;
            Quaternion rotation = Quaternion.Euler(0f, 0f, angle);
            Vector3[] vertices =
            {
                center + (Vector2)(rotation * new Vector2(-halfSize.x, -halfSize.y)),
                center + (Vector2)(rotation * new Vector2(-halfSize.x, halfSize.y)),
                center + (Vector2)(rotation * new Vector2(halfSize.x, halfSize.y)),
                center + (Vector2)(rotation * new Vector2(halfSize.x, -halfSize.y)),
            };

            Handles.DrawSolidRectangleWithOutline(vertices, fillColor, outlineColor);
        }

        private static void DrawSolidOrWireCapsule(
            Vector2 center,
            Vector2 size,
            float angle,
            CapsuleDirection2D direction,
            Color fillColor,
            Color outlineColor)
        {
            Matrix4x4 previousMatrix = Handles.matrix;
            Handles.matrix = Matrix4x4.TRS(center, Quaternion.Euler(0f, 0f, angle), Vector3.one);

            float radius = direction == CapsuleDirection2D.Vertical
                ? size.x * 0.5f
                : size.y * 0.5f;
            radius = Mathf.Max(1f, radius);

            Vector2 lineOffset = direction == CapsuleDirection2D.Vertical
                ? new Vector2(0f, Mathf.Max(0f, size.y * 0.5f - radius))
                : new Vector2(Mathf.Max(0f, size.x * 0.5f - radius), 0f);
            Vector2 cross = direction == CapsuleDirection2D.Vertical ? Vector2.right : Vector2.up;
            Vector2 bodySize = direction == CapsuleDirection2D.Vertical
                ? new Vector2(radius * 2f, Mathf.Max(0f, size.y - (radius * 2f)))
                : new Vector2(Mathf.Max(0f, size.x - (radius * 2f)), radius * 2f);

            if (bodySize.x > 0.01f && bodySize.y > 0.01f)
            {
                Vector3[] vertices =
                {
                    new Vector2(-bodySize.x * 0.5f, -bodySize.y * 0.5f),
                    new Vector2(-bodySize.x * 0.5f, bodySize.y * 0.5f),
                    new Vector2(bodySize.x * 0.5f, bodySize.y * 0.5f),
                    new Vector2(bodySize.x * 0.5f, -bodySize.y * 0.5f),
                };
                Handles.DrawSolidRectangleWithOutline(vertices, fillColor, outlineColor);
            }

            Handles.color = fillColor;
            Handles.DrawSolidDisc(lineOffset, Vector3.forward, radius);
            Handles.DrawSolidDisc(-lineOffset, Vector3.forward, radius);
            Handles.color = outlineColor;
            Handles.DrawWireDisc(lineOffset, Vector3.forward, radius);
            Handles.DrawWireDisc(-lineOffset, Vector3.forward, radius);
            Handles.DrawLine(lineOffset + (cross * radius), -lineOffset + (cross * radius));
            Handles.DrawLine(lineOffset - (cross * radius), -lineOffset - (cross * radius));
            Handles.matrix = previousMatrix;
        }

        public static void DrawShapeSnapshotGizmo(CombatShapeSnapshot shape, Color color)
        {
            Color previousColor = Gizmos.color;
            Gizmos.color = color;
            DrawPreviewShapeGizmo(shape.center, shape.size, shape.radius, shape.shapeKind, shape.angle, shape.capsuleDirection);
            Gizmos.color = previousColor;
        }
#else
        private void OnDrawGizmosSelected()
        {
            if (_controller == null)
            {
                _controller = GetComponent<PlayerController>();
            }

            Gizmos.color = new Color(1f, 0.25f, 0.25f, 0.85f);
            if (_controller != null && _controller.IsMeleeActive)
            {
                Gizmos.DrawWireSphere(_controller.MeleeHitboxCenter, 30f);
            }

            Gizmos.color = new Color(1f, 0.2f, 0.85f, 0.95f);
            if (_controller != null)
            {
                Gizmos.DrawWireSphere(_controller.UltimateHitboxCenter, _controller.UltimateHitboxRadius);
            }
        }

        private void OnDrawGizmos()
        {
            if (!Application.isPlaying)
            {
                return;
            }
        }

        public static void DrawShapeSnapshotGizmo(CombatShapeSnapshot shape, Color color)
        {
            Color previousColor = Gizmos.color;
            Gizmos.color = color;
            if (shape.radius > 0.01f)
            {
                Gizmos.DrawWireSphere(shape.center, shape.radius);
            }
            else
            {
                Gizmos.DrawWireCube(shape.center, shape.size);
            }
            Gizmos.color = previousColor;
        }
#endif
    }
}
