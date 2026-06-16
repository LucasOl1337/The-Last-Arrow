using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using ProjectPVP.Input;
using UnityEngine;

namespace ProjectPVP.Tests.Editor
{
    public sealed class AiArenaControllerSourceCacheTests
    {
        [Test]
        public void CollectSceneControllerSources_PrefersTypedSourcesOverLegacyPlayerControllerNames()
        {
            GameObject typedRoot = new GameObject("TypedControllerSource");
            GameObject legacyRoot = new GameObject("LegacyControllerSource");
            SnapshotSourceController typedSource = typedRoot.AddComponent<SnapshotSourceController>();
            PlayerController legacySource = legacyRoot.AddComponent<PlayerController>();

            try
            {
                var results = new List<MonoBehaviour>();

                AiArenaControllerSourceCache.CollectSceneControllerSources(
                    new MonoBehaviour[] { legacySource, typedSource },
                    results);

                Assert.That(results, Has.Count.EqualTo(1));
                Assert.That(results[0], Is.SameAs(typedSource));
            }
            finally
            {
                Object.DestroyImmediate(typedRoot);
                Object.DestroyImmediate(legacyRoot);
            }
        }

        [Test]
        public void CollectSceneControllerSources_FallsBackToLegacyPlayerControllerNames()
        {
            GameObject otherRoot = new GameObject("OtherControllerSource");
            GameObject legacyRoot = new GameObject("LegacyControllerSource");
            OtherController otherSource = otherRoot.AddComponent<OtherController>();
            PlayerController legacySource = legacyRoot.AddComponent<PlayerController>();

            try
            {
                var results = new List<MonoBehaviour>();

                AiArenaControllerSourceCache.CollectSceneControllerSources(
                    new MonoBehaviour[] { otherSource, legacySource },
                    results);

                Assert.That(results, Has.Count.EqualTo(1));
                Assert.That(results[0], Is.SameAs(legacySource));
            }
            finally
            {
                Object.DestroyImmediate(otherRoot);
                Object.DestroyImmediate(legacyRoot);
            }
        }

        [Test]
        public void RefreshIfNeeded_UsesRegisteredSourcesAndKeepsRefreshWindow()
        {
            ClearSnapshotRegistry();
            GameObject root = new GameObject("RegisteredControllerSource");
            SnapshotSourceController source = root.AddComponent<SnapshotSourceController>();

            try
            {
                AiArenaSnapshotSourceRegistry.Register(source);
                var cache = new AiArenaControllerSourceCache();

                cache.RefreshIfNeeded();
                AiArenaSnapshotSourceRegistry.Unregister(source);
                cache.RefreshIfNeeded();

                Assert.That(cache.Count, Is.EqualTo(1));
                Assert.That(cache[0], Is.SameAs(source));
                Assert.That(cache.FindByOwner(root), Is.SameAs(source));

                cache.Tick(1f);
                root.SetActive(false);
                cache.RefreshIfNeeded();

                Assert.That(cache.Count, Is.Zero);
                Assert.That(cache.FindByOwner(root), Is.Null);
            }
            finally
            {
                ClearSnapshotRegistry();
                Object.DestroyImmediate(root);
            }
        }

        private static void ClearSnapshotRegistry()
        {
            MethodInfo method = typeof(AiArenaSnapshotSourceRegistry).GetMethod(
                "ClearForTests",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method.Invoke(null, null);
        }

        private sealed class SnapshotSourceController : MonoBehaviour, IAiArenaControllerSnapshotSource
        {
            public AiArenaControllerSnapshot BuildAiArenaControllerSnapshot(int fallbackSlotId, Vector2 fallbackPosition)
            {
                return new AiArenaControllerSnapshot
                {
                    isValid = true,
                    slotId = fallbackSlotId,
                    position = fallbackPosition,
                };
            }
        }

        private sealed class PlayerController : MonoBehaviour
        {
        }

        private sealed class OtherController : MonoBehaviour
        {
        }
    }
}
