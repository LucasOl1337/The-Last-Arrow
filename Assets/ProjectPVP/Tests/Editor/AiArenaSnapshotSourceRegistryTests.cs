using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using ProjectPVP.Input;
using UnityEngine;

namespace ProjectPVP.Tests.Editor
{
    public sealed class AiArenaSnapshotSourceRegistryTests
    {
        [SetUp]
        public void SetUp()
        {
            ClearSnapshotRegistry();
        }

        [TearDown]
        public void TearDown()
        {
            ClearSnapshotRegistry();
        }

        [Test]
        public void Register_AddsSourceOnceToEveryImplementedSourceList()
        {
            GameObject root = new GameObject("AllSnapshotSources");
            AllSnapshotSource source = root.AddComponent<AllSnapshotSource>();

            try
            {
                AiArenaSnapshotSourceRegistry.Register(source);
                AiArenaSnapshotSourceRegistry.Register(source);

                var controllerSources = new List<MonoBehaviour>();
                var projectileSources = new List<MonoBehaviour>();

                Assert.That(AiArenaSnapshotSourceRegistry.TryGetControllerSources(controllerSources), Is.True);
                Assert.That(controllerSources, Has.Count.EqualTo(1));
                Assert.That(controllerSources[0], Is.SameAs(source));
                Assert.That(AiArenaSnapshotSourceRegistry.TryGetProjectileSources(projectileSources), Is.True);
                Assert.That(projectileSources, Has.Count.EqualTo(1));
                Assert.That(projectileSources[0], Is.SameAs(source));
                Assert.That(AiArenaSnapshotSourceRegistry.TryGetArenaSource(out MonoBehaviour arenaSource), Is.True);
                Assert.That(arenaSource, Is.SameAs(source));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void Unregister_RemovesSourceFromEverySourceList()
        {
            GameObject root = new GameObject("RegisteredSnapshotSources");
            AllSnapshotSource source = root.AddComponent<AllSnapshotSource>();

            try
            {
                AiArenaSnapshotSourceRegistry.Register(source);
                AiArenaSnapshotSourceRegistry.Unregister(source);
                var controllerSources = new List<MonoBehaviour> { source };
                var projectileSources = new List<MonoBehaviour> { source };

                Assert.That(AiArenaSnapshotSourceRegistry.TryGetControllerSources(controllerSources), Is.False);
                Assert.That(controllerSources, Is.Empty);
                Assert.That(AiArenaSnapshotSourceRegistry.TryGetProjectileSources(projectileSources), Is.False);
                Assert.That(projectileSources, Is.Empty);
                Assert.That(AiArenaSnapshotSourceRegistry.TryGetArenaSource(out MonoBehaviour arenaSource), Is.False);
                Assert.That(arenaSource, Is.Null);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void TryGetSources_CompactsDisabledSources()
        {
            GameObject root = new GameObject("DisabledSnapshotSources");
            AllSnapshotSource source = root.AddComponent<AllSnapshotSource>();

            try
            {
                AiArenaSnapshotSourceRegistry.Register(source);
                root.SetActive(false);

                Assert.That(AiArenaSnapshotSourceRegistry.TryGetControllerSources(new List<MonoBehaviour>()), Is.False);
                Assert.That(AiArenaSnapshotSourceRegistry.TryGetProjectileSources(new List<MonoBehaviour>()), Is.False);
                Assert.That(AiArenaSnapshotSourceRegistry.TryGetArenaSource(out MonoBehaviour arenaSource), Is.False);
                Assert.That(arenaSource, Is.Null);
            }
            finally
            {
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

        private sealed class AllSnapshotSource :
            MonoBehaviour,
            IAiArenaControllerSnapshotSource,
            IAiArenaProjectileSnapshotSource,
            IAiArenaArenaSnapshotSource
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

            public AiArenaProjectileSnapshot BuildAiArenaProjectileSnapshot()
            {
                return new AiArenaProjectileSnapshot
                {
                    isValid = true,
                };
            }

            public AiArenaArenaSnapshot BuildAiArenaArenaSnapshot()
            {
                return new AiArenaArenaSnapshot
                {
                    roundsToChampion = 1,
                };
            }
        }
    }
}
