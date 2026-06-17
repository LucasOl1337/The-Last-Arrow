using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using ProjectPVP.Input;
using UnityEngine;

namespace ProjectPVP.Tests.Editor
{
    public sealed class AiArenaProjectileSourceResolverTests
    {
        [Test]
        public void CollectSceneProjectileSources_UsesOnlyLegacyProjectileControllerNames()
        {
            GameObject projectileRoot = new GameObject("ProjectileSource");
            GameObject otherRoot = new GameObject("OtherSource");
            ProjectileController projectile = projectileRoot.AddComponent<ProjectileController>();
            OtherProjectile other = otherRoot.AddComponent<OtherProjectile>();

            try
            {
                var results = new List<MonoBehaviour>();

                AiArenaProjectileSourceResolver.CollectSceneProjectileSources(
                    new MonoBehaviour[] { other, null, projectile },
                    results);

                Assert.That(results, Has.Count.EqualTo(1));
                Assert.That(results[0], Is.SameAs(projectile));
            }
            finally
            {
                Object.DestroyImmediate(projectileRoot);
                Object.DestroyImmediate(otherRoot);
            }
        }

        [Test]
        public void CollectProjectileSources_UsesRegisteredSourcesBeforeSceneFallback()
        {
            ClearSnapshotRegistry();
            GameObject registeredRoot = new GameObject("RegisteredProjectileSource");
            GameObject sceneRoot = new GameObject("SceneProjectileSource");
            SnapshotSourceProjectile registered = registeredRoot.AddComponent<SnapshotSourceProjectile>();
            ProjectileController sceneProjectile = sceneRoot.AddComponent<ProjectileController>();

            try
            {
                AiArenaSnapshotSourceRegistry.Register(registered);
                var results = new List<MonoBehaviour>();

                bool found = AiArenaProjectileSourceResolver.CollectProjectileSources(results);

                Assert.That(found, Is.True);
                Assert.That(results, Has.Count.EqualTo(1));
                Assert.That(results[0], Is.SameAs(registered));
                Assert.That(results, Has.None.SameAs(sceneProjectile));
            }
            finally
            {
                ClearSnapshotRegistry();
                Object.DestroyImmediate(registeredRoot);
                Object.DestroyImmediate(sceneRoot);
            }
        }

        [Test]
        public void CollectProjectileSources_FallsBackToSceneWhenRegistryIsEmpty()
        {
            ClearSnapshotRegistry();
            GameObject projectileRoot = new GameObject("SceneProjectileSource");
            ProjectileController projectile = projectileRoot.AddComponent<ProjectileController>();

            try
            {
                var results = new List<MonoBehaviour>();

                bool found = AiArenaProjectileSourceResolver.CollectProjectileSources(results);

                Assert.That(found, Is.True);
                Assert.That(results, Has.Count.EqualTo(1));
                Assert.That(results[0], Is.SameAs(projectile));
            }
            finally
            {
                ClearSnapshotRegistry();
                Object.DestroyImmediate(projectileRoot);
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

        private sealed class SnapshotSourceProjectile : MonoBehaviour, IAiArenaProjectileSnapshotSource
        {
            public AiArenaProjectileSnapshot BuildAiArenaProjectileSnapshot()
            {
                return new AiArenaProjectileSnapshot
                {
                    isValid = true,
                };
            }
        }

        private sealed class ProjectileController : MonoBehaviour
        {
        }

        private sealed class OtherProjectile : MonoBehaviour
        {
        }
    }
}
