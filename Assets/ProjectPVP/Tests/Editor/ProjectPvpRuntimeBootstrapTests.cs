using System.Reflection;
using NUnit.Framework;
using ProjectPVP.Core;
using ProjectPVP.Presentation;
using UnityEngine;

namespace ProjectPVP.Tests.Editor
{
    public sealed class ProjectPvpRuntimeBootstrapTests
    {
        [Test]
        public void Awake_EnsuresAudioListenerAndCameraShakeOnAvailableCamera()
        {
            GameObject bootstrapRoot = new GameObject("bootstrap_test");
            GameObject cameraRoot = new GameObject("bootstrap_test_camera");
            cameraRoot.AddComponent<Camera>();
            ProjectPvpRuntimeBootstrap bootstrap = bootstrapRoot.AddComponent<ProjectPvpRuntimeBootstrap>();
            MethodInfo awake = typeof(ProjectPvpRuntimeBootstrap).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic);

            try
            {
                Assert.That(awake, Is.Not.Null);
                Assert.That(cameraRoot.GetComponent<AudioListener>(), Is.Null);
                Assert.That(cameraRoot.GetComponent<ProjectPvpCameraShake>(), Is.Null);

                awake.Invoke(bootstrap, null);

                Assert.That(cameraRoot.GetComponent<AudioListener>(), Is.Not.Null);
                Assert.That(cameraRoot.GetComponent<ProjectPvpCameraShake>(), Is.Not.Null);
            }
            finally
            {
                Object.DestroyImmediate(bootstrapRoot);
                Object.DestroyImmediate(cameraRoot);
            }
        }
    }
}
