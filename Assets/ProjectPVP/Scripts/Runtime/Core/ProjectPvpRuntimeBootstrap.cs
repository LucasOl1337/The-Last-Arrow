using UnityEngine;

namespace ProjectPVP.Core
{
    public sealed class ProjectPvpRuntimeBootstrap : MonoBehaviour
    {
        private void Awake()
        {
            Application.runInBackground = true;
            EnsureAudioListener();
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
    }
}
