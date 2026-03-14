using ProjectPVP.Data;
using ProjectPVP.Match;
using UnityEngine;

namespace ProjectPVP.Presentation
{
    public sealed class ProjectPvpArenaGizmos : MonoBehaviour
    {
        public MatchController matchController;
        public Color boundsColor = new Color(0.2f, 0.85f, 1f, 0.9f);
        public Color spawnColor = new Color(1f, 0.9f, 0.25f, 0.95f);
        public float spawnMarkerRadius = 24f;

        private void Reset()
        {
            matchController = GetComponent<MatchController>();
        }

        private void OnDrawGizmos()
        {
            ArenaDefinitionAsset arena = matchController != null ? matchController.arenaDefinition : null;
            Rect bounds = matchController != null ? matchController.ActiveWrapBounds : new Rect(-1280f, -720f, 2560f, 1440f);

            Gizmos.color = boundsColor;
            Gizmos.DrawWireCube(bounds.center, bounds.size);

            if (arena == null || arena.spawnPoints == null)
            {
                return;
            }

            Gizmos.color = spawnColor;
            for (int index = 0; index < arena.spawnPoints.Count; index += 1)
            {
                Vector3 spawnPoint = arena.spawnPoints[index];
                Gizmos.DrawWireSphere(spawnPoint, spawnMarkerRadius);
                Gizmos.DrawLine(spawnPoint + Vector3.left * spawnMarkerRadius, spawnPoint + Vector3.right * spawnMarkerRadius);
                Gizmos.DrawLine(spawnPoint + Vector3.up * spawnMarkerRadius, spawnPoint + Vector3.down * spawnMarkerRadius);
            }
        }
    }
}
