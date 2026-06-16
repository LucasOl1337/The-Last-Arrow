using System.Collections.Generic;
using UnityEngine;

namespace ProjectPVP.Input
{
    internal sealed class AiArenaControllerSourceCache
    {
        private const float RefreshIntervalSeconds = 0.5f;

        private readonly List<MonoBehaviour> _sources = new List<MonoBehaviour>(4);
        private float _refreshLeft;

        internal int Count => _sources.Count;
        internal IReadOnlyList<MonoBehaviour> Sources => _sources;
        internal MonoBehaviour this[int index] => _sources[index];

        internal void Tick(float deltaTime)
        {
            if (deltaTime <= 0f)
            {
                return;
            }

            _refreshLeft = Mathf.Max(0f, _refreshLeft - deltaTime);
        }

        internal void ForceRefresh()
        {
            _refreshLeft = 0f;
            _sources.Clear();
        }

        internal void RefreshIfNeeded()
        {
            if (_refreshLeft > 0f && _sources.Count > 0)
            {
                return;
            }

            _sources.Clear();
            if (AiArenaSnapshotSourceRegistry.TryGetControllerSources(_sources))
            {
                _refreshLeft = RefreshIntervalSeconds;
                return;
            }

            MonoBehaviour[] behaviours = Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
            CollectSceneControllerSources(behaviours, _sources);
            _refreshLeft = RefreshIntervalSeconds;
        }

        internal MonoBehaviour FindByOwner(GameObject owner)
        {
            if (owner == null)
            {
                return null;
            }

            for (int index = 0; index < _sources.Count; index += 1)
            {
                MonoBehaviour source = _sources[index];
                if (source == null || source.gameObject != owner)
                {
                    continue;
                }

                return source;
            }

            return null;
        }

        internal static void CollectSceneControllerSources(
            IReadOnlyList<MonoBehaviour> behaviours,
            List<MonoBehaviour> destination)
        {
            if (destination == null)
            {
                return;
            }

            destination.Clear();
            if (behaviours == null)
            {
                return;
            }

            for (int index = 0; index < behaviours.Count; index += 1)
            {
                MonoBehaviour behaviour = behaviours[index];
                if (behaviour == null || behaviour is not IAiArenaControllerSnapshotSource)
                {
                    continue;
                }

                destination.Add(behaviour);
            }

            if (destination.Count > 0)
            {
                return;
            }

            for (int index = 0; index < behaviours.Count; index += 1)
            {
                MonoBehaviour behaviour = behaviours[index];
                if (behaviour == null || behaviour.GetType().Name != "PlayerController")
                {
                    continue;
                }

                destination.Add(behaviour);
            }
        }
    }
}
