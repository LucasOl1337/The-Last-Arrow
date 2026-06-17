using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ProjectPVP.Data;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProjectPVP.Tests.Editor
{
    public sealed class MizuUltimateReplayModuleTests
    {
        [Test]
        public void ResolveReplayRootEnd_UsesUltimateDashDirectionWhenAvailable()
        {
            Type moduleType = ResolveMizuUltimateReplayModuleType();
            Assert.That(moduleType, Is.Not.Null);

            MethodInfo resolveMethod = moduleType.GetMethod(
                "ResolveReplayRootEnd",
                BindingFlags.Instance | BindingFlags.Public,
                null,
                new[] { typeof(Vector2), typeof(Vector2), typeof(int), typeof(CharacterDefinition) },
                null);
            Assert.That(resolveMethod, Is.Not.Null);

            ScriptableObject module = ScriptableObject.CreateInstance(moduleType);
            CharacterDefinition definition = ScriptableObject.CreateInstance<CharacterDefinition>();

            try
            {
                definition.ultimateReplayDashDistance = 220f;

                Vector2 rootEnd = (Vector2)resolveMethod.Invoke(
                    module,
                    new object[] { Vector2.zero, Vector2.up, 1, definition });

                Assert.That(rootEnd.x, Is.EqualTo(0f).Within(0.0001f));
                Assert.That(rootEnd.y, Is.EqualTo(definition.ultimateReplayDashDistance).Within(0.0001f));
            }
            finally
            {
                Object.DestroyImmediate(definition);
                Object.DestroyImmediate(module);
            }
        }

        private static Type ResolveMizuUltimateReplayModuleType()
        {
            return AppDomain.CurrentDomain
                .GetAssemblies()
                .Select(assembly => assembly.GetType("ProjectPVP.Characters.Mizu.MizuUltimateReplayModule"))
                .FirstOrDefault(type => type != null);
        }
    }
}
