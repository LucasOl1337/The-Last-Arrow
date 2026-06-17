using UnityEngine;
using ProjectPVP.Presentation;

namespace ProjectPVP.Core
{
    public sealed class ProjectPvpRuntimeBootstrap : MonoBehaviour
    {
        private void Awake()
        {
            Application.runInBackground = true;
            EnsureAudioListener();
            EnsureCameraShake();
        }

        private static void EnsureAudioListener()
        {
            if (Object.FindFirstObjectByType<AudioListener>() != null)
            {
                return;
            }

            Camera targetCamera = Camera.main;
            if (targetCamera == null)
            {
                targetCamera = Object.FindFirstObjectByType<Camera>();
            }

            if (targetCamera == null || targetCamera.GetComponent<AudioListener>() != null)
            {
                return;
            }

            targetCamera.gameObject.AddComponent<AudioListener>();
        }

        private static void EnsureCameraShake()
        {
            Camera targetCamera = Camera.main;
            if (targetCamera == null)
            {
                targetCamera = Object.FindFirstObjectByType<Camera>();
            }

            if (targetCamera == null || targetCamera.GetComponent<ProjectPvpCameraShake>() != null)
            {
                return;
            }

            targetCamera.gameObject.AddComponent<ProjectPvpCameraShake>();
        }
    }
}
