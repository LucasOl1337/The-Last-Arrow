using NUnit.Framework;
using ProjectPVP.Presentation;
using UnityEngine;

namespace ProjectPVP.Tests.Editor
{
    public sealed class ProjectPvpCameraShakeTests
    {
        [Test]
        public void Shake_OffsetsCameraAndRestoresRestPose()
        {
            GameObject cameraRoot = new GameObject("camera_shake_test");
            cameraRoot.AddComponent<Camera>();
            ProjectPvpCameraShake shake = cameraRoot.AddComponent<ProjectPvpCameraShake>();

            try
            {
                Vector3 restPosition = cameraRoot.transform.localPosition;

                shake.Shake(0.4f, 0.25f);

                Assert.That(shake.IsShaking, Is.True);
                Assert.That(shake.ActiveIntensity, Is.EqualTo(0.4f).Within(0.0001f));
                Assert.That(shake.ActiveDuration, Is.EqualTo(0.25f).Within(0.0001f));
                Assert.That(cameraRoot.transform.localPosition, Is.Not.EqualTo(restPosition));

                shake.Tick(0.3f);

                Assert.That(shake.IsShaking, Is.False);
                Assert.That(shake.ActiveIntensity, Is.Zero);
                Assert.That(shake.ActiveDuration, Is.Zero);
                Assert.That(cameraRoot.transform.localPosition, Is.EqualTo(restPosition));
            }
            finally
            {
                Object.DestroyImmediate(cameraRoot);
            }
        }

        [Test]
        public void Shake_RefreshesRestPoseAfterCameraMovesBetweenImpacts()
        {
            GameObject cameraRoot = new GameObject("camera_shake_follow_test");
            cameraRoot.AddComponent<Camera>();
            ProjectPvpCameraShake shake = cameraRoot.AddComponent<ProjectPvpCameraShake>();

            try
            {
                Vector3 firstRestPosition = cameraRoot.transform.localPosition;
                shake.Shake(0.4f, 0.25f);
                shake.Tick(0.3f);

                cameraRoot.transform.localPosition = new Vector3(4f, -2f, 0f);
                shake.Tick(0f);

                Vector3 secondRestPosition = cameraRoot.transform.localPosition;
                shake.Shake(0.35f, 0.2f);

                Assert.That(shake.IsShaking, Is.True);
                Assert.That(cameraRoot.transform.localPosition, Is.Not.EqualTo(secondRestPosition));

                shake.Tick(0.3f);

                Assert.That(shake.IsShaking, Is.False);
                Assert.That(cameraRoot.transform.localPosition, Is.EqualTo(secondRestPosition));
                Assert.That(secondRestPosition, Is.Not.EqualTo(firstRestPosition));
            }
            finally
            {
                Object.DestroyImmediate(cameraRoot);
            }
        }

        [Test]
        public void OnDisable_RestoresCameraToRestPose()
        {
            GameObject cameraRoot = new GameObject("camera_shake_disable_test");
            cameraRoot.AddComponent<Camera>();
            ProjectPvpCameraShake shake = cameraRoot.AddComponent<ProjectPvpCameraShake>();

            try
            {
                Vector3 restPosition = cameraRoot.transform.localPosition;
                shake.Shake(0.5f, 0.3f);

                Assert.That(cameraRoot.transform.localPosition, Is.Not.EqualTo(restPosition));

                shake.enabled = false;

                Assert.That(cameraRoot.transform.localPosition, Is.EqualTo(restPosition));
                Assert.That(shake.IsShaking, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(cameraRoot);
            }
        }
    }
}
