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
            if (!Application.isPlaying)
            {
                AddMissingComponentToEditorCameras<AudioListener>();
                return;
            }

            Camera targetCamera = FindCameraNeeding<AudioListener>();

            if (targetCamera == null || targetCamera.GetComponent<AudioListener>() != null)
            {
                return;
            }

            targetCamera.gameObject.AddComponent<AudioListener>();
        }

        private static void EnsureCameraShake()
        {
            if (!Application.isPlaying)
            {
                AddMissingComponentToEditorCameras<ProjectPvpCameraShake>();
                return;
            }

            Camera targetCamera = FindCameraNeeding<ProjectPvpCameraShake>();

            if (targetCamera == null || targetCamera.GetComponent<ProjectPvpCameraShake>() != null)
            {
                return;
            }

            targetCamera.gameObject.AddComponent<ProjectPvpCameraShake>();
        }

        private static Camera FindCameraNeeding<T>() where T : Component
        {
            Camera mainCamera = Camera.main;
            if (mainCamera != null && mainCamera.GetComponent<T>() == null)
            {
                return mainCamera;
            }

            Camera[] cameras = Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);
            for (int index = 0; index < cameras.Length; index += 1)
            {
                Camera camera = cameras[index];
                if (camera != null && camera.GetComponent<T>() == null)
                {
                    return camera;
                }
            }

            return mainCamera != null ? mainCamera : (cameras.Length > 0 ? cameras[0] : null);
        }

        private static void AddMissingComponentToEditorCameras<T>() where T : Component
        {
            Camera[] cameras = Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);
            for (int index = 0; index < cameras.Length; index += 1)
            {
                Camera camera = cameras[index];
                if (camera != null && camera.GetComponent<T>() == null)
                {
                    camera.gameObject.AddComponent<T>();
                }
            }
        }
    }
}
