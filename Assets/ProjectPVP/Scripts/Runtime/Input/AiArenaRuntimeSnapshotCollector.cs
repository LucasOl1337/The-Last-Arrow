using System.Collections.Generic;
using UnityEngine;

namespace ProjectPVP.Input
{
    internal sealed class AiArenaRuntimeSnapshotCollector
    {
        private readonly AiArenaControllerSourceCache _controllers = new AiArenaControllerSourceCache();
        private readonly List<MonoBehaviour> _projectileSources = new List<MonoBehaviour>(8);

        public void Tick(float deltaTime)
        {
            _controllers.Tick(deltaTime);
        }

        public void ForceRefresh()
        {
            _controllers.ForceRefresh();
        }

        public void RefreshControllersIfNeeded()
        {
            _controllers.RefreshIfNeeded();
        }

        public AiArenaControllerSnapshot ResolveSelfSnapshot(GameObject owner, int fallbackSlotId)
        {
            return AiArenaSelfSnapshotResolver.Resolve(_controllers.Sources, owner, fallbackSlotId);
        }

        public AiArenaControllerSnapshot ResolveClosestOpponentSnapshot(AiArenaControllerSnapshot self)
        {
            return AiArenaOpponentSnapshotSelector.SelectClosest(_controllers.Sources, self);
        }

        public List<AiArenaProjectileSnapshot> ResolveProjectileSnapshots(AiArenaControllerSnapshot self)
        {
            AiArenaProjectileSourceResolver.CollectProjectileSources(_projectileSources);
            return AiArenaProjectileSnapshotResolver.Resolve(_projectileSources, self);
        }

        public AiArenaArenaSnapshot ResolveArenaSnapshot()
        {
            return AiArenaArenaSourceResolver.ResolveArenaSnapshot();
        }
    }
}
