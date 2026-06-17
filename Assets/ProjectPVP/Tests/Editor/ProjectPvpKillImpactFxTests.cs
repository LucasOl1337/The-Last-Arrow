using NUnit.Framework;
using ProjectPVP.Presentation;
using UnityEngine;

namespace ProjectPVP.Tests.Editor
{
    public sealed class ProjectPvpKillImpactFxTests
    {
        [SetUp]
        public void SetUp()
        {
            DestroyKillImpactFx();
        }

        [TearDown]
        public void TearDown()
        {
            DestroyKillImpactFx();
        }

        [Test]
        public void SpawnDefault_CreatesVisibleImpactAtWorldPosition()
        {
            Vector2 position = new Vector2(12f, -4f);

            ProjectPvpKillImpactFx fx = ProjectPvpKillImpactFx.SpawnDefault(position, "Projectile");

            Assert.That(fx, Is.Not.Null);
            Assert.That(fx.SpriteRenderer, Is.Not.Null);
            Assert.That(fx.SpriteRenderer.sprite, Is.Not.Null);
            Assert.That(fx.SpriteRenderer.sortingOrder, Is.GreaterThan(10));
            Assert.That((Vector2)fx.transform.position, Is.EqualTo(position));
            Assert.That(fx.BaseColor, Is.EqualTo(ProjectPvpKillImpactFx.ResolveImpactColor("Projectile")));
        }

        [Test]
        public void Tick_FadesAndExpandsBeforeDestroyingItself()
        {
            ProjectPvpKillImpactFx fx = ProjectPvpKillImpactFx.SpawnDefault(Vector2.zero, "Ultimate");
            float initialScale = fx.transform.localScale.x;
            float initialAlpha = fx.SpriteRenderer.color.a;

            fx.Tick(fx.Duration * 0.5f);

            Assert.That(fx == null, Is.False);
            Assert.That(fx.transform.localScale.x, Is.GreaterThan(initialScale));
            Assert.That(fx.SpriteRenderer.color.a, Is.LessThan(initialAlpha));

            fx.Tick(fx.Duration);

            Assert.That(fx == null, Is.True);
        }

        [Test]
        public void ResolveImpactProfile_GivesHighCommitmentKillsMorePresence()
        {
            ProjectPvpKillImpactFx.ResolveImpactProfile("Melee", out _, out float meleeDuration, out float meleeScale);
            ProjectPvpKillImpactFx.ResolveImpactProfile("Head Stomp", out _, out float headStompDuration, out float headStompScale);
            ProjectPvpKillImpactFx.ResolveImpactProfile("Ring Out", out _, out float ringOutDuration, out float ringOutScale);
            ProjectPvpKillImpactFx.ResolveImpactProfile("Ultimate", out _, out float ultimateDuration, out float ultimateScale);

            Assert.That(headStompDuration, Is.GreaterThan(meleeDuration));
            Assert.That(headStompScale, Is.GreaterThan(meleeScale));
            Assert.That(ringOutDuration, Is.GreaterThan(headStompDuration));
            Assert.That(ringOutScale, Is.GreaterThan(headStompScale));
            Assert.That(ultimateDuration, Is.GreaterThan(ringOutDuration));
            Assert.That(ultimateScale, Is.GreaterThan(ringOutScale));
        }

        private static void DestroyKillImpactFx()
        {
            ProjectPvpKillImpactFx[] effects = Object.FindObjectsByType<ProjectPvpKillImpactFx>(FindObjectsSortMode.None);
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
