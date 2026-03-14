using System.Collections;
using System.Collections.Generic;
using ProjectPVP.Data;
using ProjectPVP.Gameplay;
using UnityEngine;

namespace ProjectPVP.Audio
{
    [DisallowMultipleComponent]
    public sealed class CharacterAudioController : MonoBehaviour
    {
        private static readonly Dictionary<string, AudioClip> ResourceClipCache = new Dictionary<string, AudioClip>();

        public PlayerController player;
        public AudioSource actionSource;

        private Coroutine _stopRoutine;

        private void Reset()
        {
            player = GetComponent<PlayerController>();
            actionSource = GetComponent<AudioSource>();
        }

        private void Awake()
        {
            EnsureSource();
        }

        public void PlayAction(string actionName)
        {
            if (player == null || player.characterDefinition == null || player.characterDefinition.actionConfig == null)
            {
                return;
            }

            if (!player.characterDefinition.actionConfig.TryResolveActionAudioCue(actionName, out CharacterActionConfig.ActionAudioCue cue) || cue == null)
            {
                return;
            }

            AudioClip clip = ResolveClip(cue);
            if (clip == null)
            {
                return;
            }

            EnsureSource();
            if (actionSource == null)
            {
                return;
            }

            actionSource.Stop();
            actionSource.clip = clip;
            actionSource.loop = false;
            actionSource.pitch = Mathf.Max(0.01f, cue.playbackSpeed <= 0f ? 1f : cue.playbackSpeed);
            actionSource.volume = AudioRuntimeUtility.DecibelsToLinear(cue.volumeDb);
            actionSource.Play();

            if (_stopRoutine != null)
            {
                StopCoroutine(_stopRoutine);
                _stopRoutine = null;
            }

            if (cue.stopAfterSeconds > 0.01f)
            {
                _stopRoutine = StartCoroutine(StopAfterSeconds(cue.stopAfterSeconds));
            }
        }

        private void EnsureSource()
        {
            if (player == null)
            {
                player = GetComponent<PlayerController>();
            }

            if (actionSource == null)
            {
                actionSource = GetComponent<AudioSource>();
            }

            if (actionSource == null && Application.isPlaying)
            {
                actionSource = gameObject.AddComponent<AudioSource>();
            }

            if (actionSource == null)
            {
                return;
            }

            actionSource.playOnAwake = false;
            actionSource.loop = false;
            actionSource.spatialBlend = 0f;
            actionSource.volume = 1f;
        }

        private static AudioClip ResolveClip(CharacterActionConfig.ActionAudioCue cue)
        {
            if (cue.clip != null)
            {
                return cue.clip;
            }

            if (string.IsNullOrWhiteSpace(cue.resourcesPath))
            {
                return null;
            }

            if (ResourceClipCache.TryGetValue(cue.resourcesPath, out AudioClip cachedClip) && cachedClip != null)
            {
                return cachedClip;
            }

            AudioClip loadedClip = Resources.Load<AudioClip>(cue.resourcesPath);
            if (loadedClip != null)
            {
                ResourceClipCache[cue.resourcesPath] = loadedClip;
            }

            return loadedClip;
        }

        private IEnumerator StopAfterSeconds(float seconds)
        {
            yield return new WaitForSeconds(seconds);
            if (actionSource != null && actionSource.isPlaying)
            {
                actionSource.Stop();
            }

            _stopRoutine = null;
        }
    }
}
