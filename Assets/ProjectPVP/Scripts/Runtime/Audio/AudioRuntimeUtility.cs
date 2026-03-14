using UnityEngine;

namespace ProjectPVP.Audio
{
    internal static class AudioRuntimeUtility
    {
        public static float DecibelsToLinear(float volumeDb)
        {
            return Mathf.Clamp01(Mathf.Pow(10f, volumeDb / 20f));
        }
    }
}
