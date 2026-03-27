using System;
using ProjectPVP.Gameplay;
using ProjectPVP.Input;
using ProjectPVP.Match;
using UnityEngine;

namespace ProjectPVP.AI
{
    public sealed class AiArenaMatchConfigurator : MonoBehaviour
    {
        [Serializable]
        public sealed class SlotRuntimeConfig
        {
            public CombatantSlotId slotId = CombatantSlotId.SlotOne;
            public AiArenaControlMode controlMode = AiArenaControlMode.Human;
            public string httpEndpoint = "http://127.0.0.1:8765/arena/act";
            [Min(10)] public int pollingIntervalMs = 33;
            [Min(10)] public int requestTimeoutMs = 60;
            [Min(20)] public int staleActionTimeoutMs = 120;
            public AiActionFallbackMode fallbackMode = AiActionFallbackMode.HoldLastContinuous;
        }

        public MatchController matchController;
        public SlotRuntimeConfig slotOne = new SlotRuntimeConfig { slotId = CombatantSlotId.SlotOne };
        public SlotRuntimeConfig slotTwo = new SlotRuntimeConfig { slotId = CombatantSlotId.SlotTwo, controlMode = AiArenaControlMode.Heuristic };

        private void Start()
        {
            Apply();
        }

        [ContextMenu("Apply AI Arena Configuration")]
        public void Apply()
        {
            if (matchController == null)
            {
                matchController = FindFirstObjectByType<MatchController>();
            }

            if (matchController == null)
            {
                return;
            }

            ApplySlot(slotOne);
            ApplySlot(slotTwo);
        }

        private void ApplySlot(SlotRuntimeConfig config)
        {
            if (config == null)
            {
                return;
            }

            CombatantSlotConfig slot = matchController.GetSlot(config.slotId);
            PlayerController player = slot != null ? slot.controller : null;
            if (player == null)
            {
                return;
            }

            MonoBehaviour selectedSource = ResolveSource(player, config);
            if (selectedSource == null)
            {
                return;
            }

            ToggleKnownSources(player, selectedSource);
            player.AssignInputSource(selectedSource);
        }

        private static MonoBehaviour ResolveSource(PlayerController player, SlotRuntimeConfig config)
        {
            switch (config.controlMode)
            {
                case AiArenaControlMode.Heuristic:
                {
                    AiHeuristicInputSource heuristic = player.GetComponent<AiHeuristicInputSource>();
                    if (heuristic == null)
                    {
                        heuristic = player.gameObject.AddComponent<AiHeuristicInputSource>();
                    }

                    heuristic.matchController = player.GetComponentInParent<MatchController>();
                    return heuristic;
                }
                case AiArenaControlMode.HttpBridge:
                {
                    AiHttpPollingInputSource http = player.GetComponent<AiHttpPollingInputSource>();
                    if (http == null)
                    {
                        http = player.gameObject.AddComponent<AiHttpPollingInputSource>();
                    }

                    http.matchController = player.GetComponentInParent<MatchController>();
                    http.endpoint = config.httpEndpoint;
                    http.pollingIntervalMs = config.pollingIntervalMs;
                    http.requestTimeoutMs = config.requestTimeoutMs;
                    http.staleActionTimeoutMs = config.staleActionTimeoutMs;
                    http.fallbackMode = config.fallbackMode;
                    return http;
                }
                case AiArenaControlMode.Idle:
                {
                    AiIdleInputSource idle = player.GetComponent<AiIdleInputSource>();
                    if (idle == null)
                    {
                        idle = player.gameObject.AddComponent<AiIdleInputSource>();
                    }

                    return idle;
                }
                default:
                {
                    KeyboardPlayerInputSource keyboard = player.GetComponent<KeyboardPlayerInputSource>();
                    if (keyboard == null)
                    {
                        keyboard = player.gameObject.AddComponent<KeyboardPlayerInputSource>();
                    }

                    return keyboard;
                }
            }
        }

        private static void ToggleKnownSources(PlayerController player, MonoBehaviour selectedSource)
        {
            MonoBehaviour[] behaviours = player.GetComponents<MonoBehaviour>();
            for (int index = 0; index < behaviours.Length; index += 1)
            {
                MonoBehaviour behaviour = behaviours[index];
                if (behaviour == null)
                {
                    continue;
                }

                if (behaviour is KeyboardPlayerInputSource
                    || behaviour is AiHeuristicInputSource
                    || behaviour is AiHttpPollingInputSource
                    || behaviour is AiIdleInputSource)
                {
                    behaviour.enabled = behaviour == selectedSource;
                }
            }
        }
    }
}
