using System.Collections.Generic;
using UnityEngine;

namespace ProjectPVP.Data
{
    [CreateAssetMenu(fileName = "ArenaDefinition", menuName = "ProjectPVP/Arena Definition")]
    public sealed class ArenaDefinitionAsset : ScriptableObject
    {
        [Header("Identity")]
        public string id = "arena";
        public string displayName = string.Empty;

        [Header("Visual")]
        public Sprite backgroundSprite;

        [Header("Audio")]
        public UnityEngine.Object backgroundMusicAsset;
        public string backgroundMusicResourcesPath = string.Empty;
        public float backgroundMusicVolumeDb = -14f;

        [Header("Gameplay")]
        public List<Vector2> spawnPoints = new List<Vector2>
        {
            new Vector2(-520f, -360f),
            new Vector2(520f, -360f),
        };

        public Rect wrapBounds = new Rect(-1200f, -700f, 2400f, 1400f);
        public Vector2 wrapPadding = new Vector2(40f, 40f);

        public Vector2 GetSpawnPoint(int index)
        {
            if (spawnPoints == null || spawnPoints.Count == 0)
            {
                return Vector2.zero;
            }

            index = Mathf.Clamp(index, 0, spawnPoints.Count - 1);
            return spawnPoints[index];
        }

        public AudioClip ResolveBackgroundMusicClip()
        {
            if (backgroundMusicAsset is AudioClip directClip)
            {
                return directClip;
            }

            if (string.IsNullOrWhiteSpace(backgroundMusicResourcesPath))
            {
                return null;
            }

            return Resources.Load<AudioClip>(backgroundMusicResourcesPath);
        }
    }
}
