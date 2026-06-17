using NUnit.Framework;
using ProjectPVP.Presentation;
using UnityEngine;

namespace ProjectPVP.Tests.Editor
{
    public sealed class ProjectPvpAttackCueFxTests
    {
        [SetUp]
        public void SetUp()
        {
            DestroyAttackCueFx();
        }

        [TearDown]
        public void TearDown()
        {
            DestroyAttackCueFx();
        }

        [Test]
        public void SpawnMelee_CreatesReadableBoxAtHitboxPosition()
        {
            Vector2 center = new Vector2(24f, 12f);
            Vector2 size = new Vector2(80f, 44f);

            ProjectPvpAttackCueFx fx = ProjectPvpAttackCueFx.SpawnMelee(center, size, facing: 1, duration: 0.12f);

            Assert.That(fx, Is.Not.Null);
            Assert.That(fx.Kind, Is.EqualTo(ProjectPvpAttackCueKind.Melee));
            Assert.That(fx.SpriteRenderer, Is.Not.Null);
            Assert.That(fx.SpriteRenderer.sprite, Is.Not.Null);
            Assert.That((Vector2)fx.transform.position, Is.EqualTo(center));
            Assert.That(fx.transform.localScale.x, Is.EqualTo(size.x).Within(0.001f));
            Assert.That(fx.transform.localScale.y, Is.EqualTo(size.y).Within(0.001f));
            Assert.That(fx.Duration, Is.EqualTo(0.12f).Within(0.001f));
        }

        [Test]
        public void SpawnUltimate_CreatesWarningRingForFullWindup()
        {
            Vector2 center = new Vector2(-16f, 40f);

            ProjectPvpAttackCueFx fx = ProjectPvpAttackCueFx.SpawnUltimate(center, radius: 96f, duration: 0.28f);

            Assert.That(fx, Is.Not.Null);
            Assert.That(fx.Kind, Is.EqualTo(ProjectPvpAttackCueKind.Ultimate));
            Assert.That(fx.SpriteRenderer, Is.Not.Null);
            Assert.That(fx.SpriteRenderer.sprite, Is.Not.Null);
            Assert.That((Vector2)fx.transform.position, Is.EqualTo(center));
            Assert.That(fx.transform.localScale.x, Is.EqualTo(192f).Within(0.001f));
            Assert.That(fx.transform.localScale.y, Is.EqualTo(192f).Within(0.001f));
            Assert.That(fx.Duration, Is.EqualTo(0.28f).Within(0.001f));
        }

        [Test]
        public void Tick_FadesAndDestroysCueAfterDuration()
        {
            ProjectPvpAttackCueFx fx = ProjectPvpAttackCueFx.SpawnMelee(Vector2.zero, new Vector2(72f, 48f), facing: -1, duration: 0.12f);
            float initialAlpha = fx.SpriteRenderer.color.a;

            fx.Tick(0.06f);

            Assert.That(fx == null, Is.False);
            Assert.That(fx.SpriteRenderer.color.a, Is.LessThan(initialAlpha));

            fx.Tick(0.2f);

            Assert.That(fx == null, Is.True);
        }

        private static void DestroyAttackCueFx()
        {
            ProjectPvpAttackCueFx[] effects = Object.FindObjectsByType<ProjectPvpAttackCueFx>(FindObjectsSortMode.None);
            for (int index = 0; index < effects.Length; index += 1)
            {
                if (effects[index] != null)
                {
                    Object.DestroyImmediate(effects[index].gameObject);
                }
            }
        }
    }
}
