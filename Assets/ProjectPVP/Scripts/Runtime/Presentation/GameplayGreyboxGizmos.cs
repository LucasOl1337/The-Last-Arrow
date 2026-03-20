using UnityEngine;

namespace ProjectPVP.Presentation
{
    [ExecuteAlways]
    public sealed class GameplayGreyboxGizmos : MonoBehaviour
    {
        public Color outlineColor = new Color(0.35f, 1f, 0.75f, 0.95f);
        public Color fillColor = new Color(0.1f, 0.9f, 0.7f, 0.08f);
        public bool includeInactive;
        public bool drawFilled = true;
        public bool skipTriggers = true;

        private void OnDrawGizmos()
        {
            Collider2D[] colliders = GetComponentsInChildren<Collider2D>(includeInactive);
            if (colliders == null)
            {
                return;
            }

            Matrix4x4 previousMatrix = Gizmos.matrix;
            Color previousColor = Gizmos.color;

            for (int index = 0; index < colliders.Length; index += 1)
            {
                Collider2D collider = colliders[index];
                if (collider == null || !collider.enabled || (skipTriggers && collider.isTrigger))
                {
                    continue;
                }

                DrawCollider(collider);
            }

            Gizmos.matrix = previousMatrix;
            Gizmos.color = previousColor;
        }

        private void DrawCollider(Collider2D collider)
        {
            switch (collider)
            {
                case BoxCollider2D box:
                    DrawBoxCollider(box);
                    break;
                case EdgeCollider2D edge:
                    DrawEdgeCollider(edge);
                    break;
                case PolygonCollider2D polygon:
                    DrawPolygonCollider(polygon);
                    break;
                case CircleCollider2D circle:
                    DrawCircleCollider(circle);
                    break;
                case CapsuleCollider2D capsule:
                    DrawCapsuleCollider(capsule);
                    break;
            }
        }

        private void DrawBoxCollider(BoxCollider2D collider)
        {
            Gizmos.matrix = collider.transform.localToWorldMatrix;
            if (drawFilled)
            {
                Gizmos.color = fillColor;
                Gizmos.DrawCube(collider.offset, collider.size);
            }

            Gizmos.color = outlineColor;
            Gizmos.DrawWireCube(collider.offset, collider.size);
        }

        private void DrawCircleCollider(CircleCollider2D collider)
        {
            Gizmos.matrix = collider.transform.localToWorldMatrix;
            if (drawFilled)
            {
                Gizmos.color = fillColor;
                Gizmos.DrawSphere(collider.offset, collider.radius);
            }

            Gizmos.color = outlineColor;
            Gizmos.DrawWireSphere(collider.offset, collider.radius);
        }

        private void DrawCapsuleCollider(CapsuleCollider2D collider)
        {
            Gizmos.matrix = collider.transform.localToWorldMatrix;
            if (drawFilled)
            {
                Gizmos.color = fillColor;
                Gizmos.DrawCube(collider.offset, collider.size);
            }

            Gizmos.color = outlineColor;
            Gizmos.DrawWireCube(collider.offset, collider.size);
        }

        private void DrawEdgeCollider(EdgeCollider2D collider)
        {
            Gizmos.matrix = collider.transform.localToWorldMatrix;
            Gizmos.color = outlineColor;
            Vector2[] points = collider.points;
            for (int index = 1; index < points.Length; index += 1)
            {
                Gizmos.DrawLine(points[index - 1], points[index]);
            }
        }

        private void DrawPolygonCollider(PolygonCollider2D collider)
        {
            Gizmos.matrix = collider.transform.localToWorldMatrix;
            Gizmos.color = outlineColor;
            for (int pathIndex = 0; pathIndex < collider.pathCount; pathIndex += 1)
            {
                Vector2[] points = collider.GetPath(pathIndex);
                for (int pointIndex = 0; pointIndex < points.Length; pointIndex += 1)
                {
                    Vector2 start = points[pointIndex];
                    Vector2 end = points[(pointIndex + 1) % points.Length];
                    Gizmos.DrawLine(start, end);
                }
            }
        }
    }
}
