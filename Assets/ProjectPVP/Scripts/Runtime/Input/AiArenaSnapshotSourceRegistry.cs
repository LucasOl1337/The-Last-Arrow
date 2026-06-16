using System.Collections.Generic;
using UnityEngine;

namespace ProjectPVP.Input
{
    public static class AiArenaSnapshotSourceRegistry
    {
        private static readonly List<MonoBehaviour> s_controllerSources = new List<MonoBehaviour>(4);
        private static readonly List<MonoBehaviour> s_projectileSources = new List<MonoBehaviour>(8);
        private static readonly List<MonoBehaviour> s_arenaSources = new List<MonoBehaviour>(2);

        public static void Register(MonoBehaviour source)
        {
            if (source == null)
            {
                return;
            }

            if (source is IAiArenaControllerSnapshotSource)
            {
                AddUnique(s_controllerSources, source);
            }

            if (source is IAiArenaProjectileSnapshotSource)
            {
                AddUnique(s_projectileSources, source);
            }

            if (source is IAiArenaArenaSnapshotSource)
            {
                AddUnique(s_arenaSources, source);
            }
        }

        public static void Unregister(MonoBehaviour source)
        {
            if (source == null)
            {
                return;
            }

            RemoveReference(s_controllerSources, source);
            RemoveReference(s_projectileSources, source);
            RemoveReference(s_arenaSources, source);
        }

        public static bool TryGetControllerSources(List<MonoBehaviour> destination)
        {
            return CopyLiveSources(s_controllerSources, destination);
        }

        public static bool TryGetProjectileSources(List<MonoBehaviour> destination)
        {
            return CopyLiveSources(s_projectileSources, destination);
        }

        public static bool TryGetArenaSource(out MonoBehaviour source)
        {
            CompactLiveSources(s_arenaSources);
            source = s_arenaSources.Count > 0 ? s_arenaSources[0] : null;
            return source != null;
        }

        private static void AddUnique(List<MonoBehaviour> sources, MonoBehaviour source)
        {
            CompactLiveSources(sources);
            for (int index = 0; index < sources.Count; index += 1)
            {
                if (ReferenceEquals(sources[index], source))
                {
                    return;
                }
            }

            sources.Add(source);
        }

        private static bool CopyLiveSources(List<MonoBehaviour> sources, List<MonoBehaviour> destination)
        {
            if (destination == null)
            {
                return false;
            }

            destination.Clear();
            CompactLiveSources(sources);
            for (int index = 0; index < sources.Count; index += 1)
            {
                destination.Add(sources[index]);
            }

            return destination.Count > 0;
        }

        private static void RemoveReference(List<MonoBehaviour> sources, MonoBehaviour source)
        {
            for (int index = sources.Count - 1; index >= 0; index -= 1)
            {
                MonoBehaviour candidate = sources[index];
                if (candidate == null || ReferenceEquals(candidate, source))
                {
                    sources.RemoveAt(index);
                }
            }
        }

        private static void CompactLiveSources(List<MonoBehaviour> sources)
        {
            for (int index = sources.Count - 1; index >= 0; index -= 1)
            {
                MonoBehaviour source = sources[index];
                if (source == null || !source.isActiveAndEnabled)
                {
                    sources.RemoveAt(index);
                }
            }
        }

        private static void ClearForTests()
        {
            s_controllerSources.Clear();
            s_projectileSources.Clear();
            s_arenaSources.Clear();
        }
    }
}
