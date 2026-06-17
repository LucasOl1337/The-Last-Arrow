using NUnit.Framework;
using ProjectPVP.Presentation;
using UnityEngine;

namespace ProjectPVP.Tests.Editor
{
    public sealed class ProjectPvpParryCueFxTests
    {
        [SetUp]
        public void SetUp()
        {
            DestroyParryCueFx();
        }

        [TearDown]
        public void TearDown()
        {
            DestroyParryCueFx();
        }

        [Test]
        public void Spawn_CreatesReadableRingAtParryPosition()
        {
            Vector2 center = new Vector2(18f, -12f);

            ProjectPvpParryCueFx fx = ProjectPvpParryCueFx.Spawn(center, radius: 72f, duration: 0.16f);

            Assert.That(fx, Is.Not.Null);
            Assert.That(fx.SpriteRenderer, Is.Not.Null);
            Assert.That(fx.SpriteRenderer.sprite, Is.Not.Null);
            Assert.That((Vector2)fx.transform.position, Is.EqualTo(center));
            Assert.That(fx.transform.localScale.x, Is.EqualTo(144f).Within(0.001f));
            Assert.That(fx.transform.localScale.y, Is.EqualTo(144f).Within(0.001f));
            Assert.That(fx.Duration, Is.EqualTo(0.16f).Within(0.001f));
            Assert.That(fx.BaseColor.a, Is.GreaterThan(0f));
        }

        [Test]
        public void Tick_FadesAndDestroysAfterDuration()
        {
            ProjectPvpParryCueFx fx = ProjectPvpParryCueFx.Spawn(Vector2.zero, radius: 64f, duration: 0.12f);
            float initialAlpha = fx.SpriteRenderer.color.a;

            fx.Tick(0.06f);

            Assert.That(fx == null, Is.False);
            Assert.That(fx.SpriteRenderer.color.a, Is.LessThan(initialAlpha));

            fx.Tick(0.2f);

            Assert.That(fx == null, Is.True);
        }

        private static void DestroyParryCueFx()
        {
            ProjectPvpParryCueFx[] effects = Object.FindObjectsByType<ProjectPvpParryCueFx>(FindObjectsSortMode.None);
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
