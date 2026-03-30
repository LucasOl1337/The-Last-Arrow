using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using ProjectPVP.Audio;
using ProjectPVP.Characters;
using ProjectPVP.Data;
using ProjectPVP.Gameplay;
using ProjectPVP.Input;
using RuntimeInput = global::UnityEngine.Input;
using UnityEngine;
using UnityEngine.Serialization;

namespace ProjectPVP.Match
{
    [System.Serializable]
    public sealed class RoundRespawnSeed
    {
        public string label = string.Empty;
        public Vector2 slotOneSpawnPoint;
        public Vector2 slotTwoSpawnPoint;

        public Vector2 GetSpawnPoint(CombatantSlotId slotId)
        {
            return slotId == CombatantSlotId.SlotTwo ? slotTwoSpawnPoint : slotOneSpawnPoint;
        }

        public string ResolveLabel(int seedNumber)
        {
            return string.IsNullOrWhiteSpace(label)
                ? $"Seed {seedNumber}"
                : label.Trim();
        }
    }

    [Serializable]
    internal sealed class RuntimeBotMenuSlotAssignment
    {
        public int slotId;
        public bool enabled;
        public string botId = string.Empty;
        public string displayName = string.Empty;
        public string provider = string.Empty;
        public string model = string.Empty;
    }

    [Serializable]
    internal sealed class RuntimeBotMenuAssignmentsFile
    {
        public string updatedAt = string.Empty;
        public List<RuntimeBotMenuSlotAssignment> slots = new List<RuntimeBotMenuSlotAssignment>();
    }

    public sealed class MatchController : MonoBehaviour
    {
        public ArenaDefinitionAsset arenaDefinition;
        [SerializeField] private MatchRoster roster = new MatchRoster();
        public CharacterCatalog characterCatalog;
        [FormerlySerializedAs("playerOne")]
        [SerializeField] private PlayerController legacySlotOneController;
        [FormerlySerializedAs("playerTwo")]
        [SerializeField] private PlayerController legacySlotTwoController;
        public bool useScenePlayerPositionsAsSpawn = true;
        public bool wrapEnabled = true;
        public int maxWins = 5;
        public float roundResetDelay = 1.25f;
        public float respawnFreezeDuration = 0.5f;
        public float championAnnouncementDuration = 2f;
        [SerializeField] private List<RoundRespawnSeed> roundRespawnSeeds = CreateDefaultRespawnSeeds();
        [SerializeField] private int currentRespawnSeedIndex;
        public Vector2 defaultPlayerOneSpawn = new Vector2(-420f, -540f);
        public Vector2 defaultPlayerTwoSpawn = new Vector2(420f, -540f);
        public Rect defaultWrapBounds = new Rect(-1280f, -720f, 2560f, 1440f);
        public Vector2 defaultWrapPadding = new Vector2(40f, 40f);
        [Header("Debug Shortcuts")]
        public bool enableDebugShortcuts = true;
        public bool autoEnableSlotTwoDebugBotOnPlay = true;
        public bool autoForceCodexBrokerForSlotTwoOnPlay = true;
        public AiBrainKind slotTwoDebugAiBrain = AiBrainKind.LocalHeuristic;

        private AudioSource _musicSource;
        [SerializeField] private int[] slotWins = new int[2];
        private Coroutine _roundResetRoutine;
        private readonly Dictionary<CombatantSlotId, CombatantSlotProfile> _runtimeOriginalProfiles = new Dictionary<CombatantSlotId, CombatantSlotProfile>();
        private readonly Dictionary<CombatantSlotId, CombatantSlotProfile> _runtimeOverrideProfiles = new Dictionary<CombatantSlotId, CombatantSlotProfile>();
        private CombatantSlotProfile _slotTwoOriginalProfile;
        private CombatantSlotProfile _slotTwoRuntimeBotProfile;
        private bool _slotTwoBotShortcutEnabled;
        private CombatantSlotId _pendingRoundWinnerSlot = CombatantSlotId.None;
        private CombatantSlotId _pendingChampionSlot = CombatantSlotId.None;
        private float _respawnFreezeTimeLeft;
        private CombatantSlotId _championAnnouncementSlot = CombatantSlotId.None;
        private float _championAnnouncementTimeLeft;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void BootstrapSlotTwoCodexBot()
        {
            MatchController[] controllers = UnityEngine.Object.FindObjectsByType<MatchController>(FindObjectsSortMode.None);
            if (controllers == null || controllers.Length == 0)
            {
                Debug.LogWarning("[CodexBot] Bootstrap could not find a MatchController after scene load.");
                return;
            }

            for (int index = 0; index < controllers.Length; index += 1)
            {
                MatchController controller = controllers[index];
                if (controller == null)
                {
                    continue;
                }

                Debug.Log($"[CodexBot] Bootstrap forcing runtime bot assignments on MatchController instance {controller.name}.");
                controller.ForceCodexBotsReadyForPlay();
            }
        }

        public IReadOnlyList<CombatantSlotConfig> Slots => roster != null ? roster.Slots : System.Array.Empty<CombatantSlotConfig>();
        public IReadOnlyList<CharacterBootstrapProfile> AvailableCharacters => characterCatalog != null ? characterCatalog.Characters : System.Array.Empty<CharacterBootstrapProfile>();
        public IReadOnlyList<RoundRespawnSeed> RoundRespawnSeeds => roundRespawnSeeds != null
            ? roundRespawnSeeds
            : System.Array.Empty<RoundRespawnSeed>();
        public int PlayerOneWins => GetWins(CombatantSlotId.SlotOne);
        public int PlayerTwoWins => GetWins(CombatantSlotId.SlotTwo);
        public int RoundsToChampion => Mathf.Max(1, maxWins);
        public bool IsRoundResetPending => _roundResetRoutine != null;
        public bool IsRespawnFreezeActive => _respawnFreezeTimeLeft > 0f;
        public int CurrentRespawnSeedIndex => NormalizeRespawnSeedIndex(currentRespawnSeedIndex);
        public string CurrentRespawnSeedLabel => TryGetRespawnSeed(CurrentRespawnSeedIndex, out RoundRespawnSeed seed)
            ? seed.ResolveLabel(CurrentRespawnSeedIndex + 1)
            : "Fallback";
        public CombatantSlotId PendingRoundWinnerSlot => _pendingRoundWinnerSlot;
        public CombatantSlotId PendingChampionSlot => _pendingChampionSlot;
        public CombatantSlotId ChampionAnnouncementSlot => _championAnnouncementTimeLeft > 0f ? _championAnnouncementSlot : CombatantSlotId.None;
        public Rect ActiveWrapBounds => arenaDefinition != null ? arenaDefinition.wrapBounds : defaultWrapBounds;
        public Vector2 PlayerOneSpawnPoint => GetSpawnPoint(CombatantSlotId.SlotOne);
        public Vector2 PlayerTwoSpawnPoint => GetSpawnPoint(CombatantSlotId.SlotTwo);
        public PlayerController PlayerOneController => GetSlot(CombatantSlotId.SlotOne)?.controller;
        public PlayerController PlayerTwoController => GetSlot(CombatantSlotId.SlotTwo)?.controller;

#pragma warning disable IDE1006
        public PlayerController playerOne => PlayerOneController;
        public PlayerController playerTwo => PlayerTwoController;
#pragma warning restore IDE1006

        private void Awake()
        {
            SyncRosterAliases();
            EnsureRespawnSeedConfiguration();
            EnsureRuntimeCombatantsForConfiguredSlots();
        }

        private void OnValidate()
        {
            SyncRosterAliases();
            EnsureRespawnSeedConfiguration();
        }

        private void OnEnable()
        {
            SyncRosterAliases();
            EnsureRespawnSeedConfiguration();
            SubscribePlayers();
        }

        private void OnDisable()
        {
            UnsubscribePlayers();
        }

        private void Start()
        {
            SyncRosterAliases();
            Debug.Log($"[CodexBot] MatchController.Start forcing runtime bot assignments auto-play={autoEnableSlotTwoDebugBotOnPlay} forceBrain={autoForceCodexBrokerForSlotTwoOnPlay}");
            ForceCodexBotsReadyForPlay();
            EnsureRoundHudOverlay();
            CacheSceneSpawnPoints();
            EnsureMusicSource();
            PlayArenaMusic();
            RespawnPlayers(applyFreeze: false);
            PrewarmCodexSessions();
        }

        private void Update()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            TickFreezeAndAnnouncements(Time.deltaTime);

            if (!enableDebugShortcuts)
            {
                return;
            }

            bool shiftHeld = RuntimeInput.GetKey(global::UnityEngine.KeyCode.LeftShift)
                || RuntimeInput.GetKey(global::UnityEngine.KeyCode.RightShift);
            bool togglePressed = RuntimeInput.GetKeyDown(global::UnityEngine.KeyCode.B)
                || RuntimeInput.GetKeyDown(global::UnityEngine.KeyCode.S);
            if (shiftHeld && togglePressed)
            {
                EnsurePlayerTwoDebugBotEnabled(!_slotTwoBotShortcutEnabled);
            }
        }

        private void LateUpdate()
        {
            if (!wrapEnabled)
            {
                return;
            }

            foreach (PlayerController player in EnumerateControllers())
            {
                ApplyWrap(player);
            }
        }

        public CombatantSlotConfig GetSlot(CombatantSlotId slotId)
        {
            SyncRosterAliases();
            return roster.GetSlot(slotId);
        }

        public void EnsureRuntimeCombatantsForConfiguredSlots()
        {
            SyncRosterAliases();
            for (int index = 0; index < Slots.Count; index += 1)
            {
                CombatantSlotConfig slot = Slots[index];
                if (slot == null || slot.controller != null)
                {
                    continue;
                }

                slot.controller = CreateRuntimeController(slot);
            }

            SyncRosterAliases();
        }

        public CombatantSlotConfig GetSlotForController(PlayerController controller)
        {
            if (controller == null)
            {
                return null;
            }

            SyncRosterAliases();
            for (int index = 0; index < Slots.Count; index += 1)
            {
                CombatantSlotConfig slot = Slots[index];
                if (slot?.controller == controller)
                {
                    return slot;
                }
            }

            return null;
        }

        public IEnumerable<PlayerController> EnumerateControllers()
        {
            SyncRosterAliases();
            foreach (PlayerController player in roster.EnumerateControllers())
            {
                if (player != null)
                {
                    yield return player;
                }
            }
        }

        public int GetWins(CombatantSlotId slotId)
        {
            int slotIndex = slotId.ToIndex();
            if (slotIndex < 0 || slotWins == null || slotIndex >= slotWins.Length)
            {
                return 0;
            }

            return slotWins[slotIndex];
        }

        public Vector2 GetSpawnPoint(CombatantSlotId slotId)
        {
            CombatantSlotConfig slot = GetSlot(slotId);
            int slotIndex = Mathf.Max(0, slotId.ToIndex());

            if (TryGetCurrentRespawnSeedPoint(slotId, out Vector2 respawnSeedPoint))
            {
                return respawnSeedPoint;
            }

            if (useScenePlayerPositionsAsSpawn && slot != null && slot.fallbackSpawnPoint != Vector2.zero)
            {
                if (!Application.isPlaying && slot.controller != null)
                {
                    return slot.controller.ConfiguredSpawnWorldPosition;
                }

                return slot.fallbackSpawnPoint;
            }

            if (arenaDefinition != null && arenaDefinition.spawnPoints != null && arenaDefinition.spawnPoints.Count > 0)
            {
                return arenaDefinition.GetSpawnPoint(Mathf.Min(slotIndex, arenaDefinition.spawnPoints.Count - 1));
            }

            if (slot != null && slot.fallbackSpawnPoint != Vector2.zero)
            {
                return slot.fallbackSpawnPoint;
            }

            return slotId == CombatantSlotId.SlotTwo ? defaultPlayerTwoSpawn : defaultPlayerOneSpawn;
        }

        public Vector2 GetRespawnSeedPoint(int seedIndex, CombatantSlotId slotId)
        {
            return TryGetRespawnSeed(seedIndex, out RoundRespawnSeed seed)
                ? seed.GetSpawnPoint(slotId)
                : GetFallbackSpawnPoint(slotId);
        }

        public string ResolveSlotDisplayName(CombatantSlotId slotId)
        {
            CombatantSlotConfig slot = GetSlot(slotId);
            return slot != null ? slot.ResolveDisplayName() : slotId.ToDisplayName();
        }

        private void SyncRosterAliases()
        {
            roster ??= new MatchRoster();
            EnsureSlotWinsCapacity();
            EnsureRespawnSeedConfiguration();
            roster.EnsureDefaults(legacySlotOneController, legacySlotTwoController);

            SyncSlotAlias(CombatantSlotId.SlotOne, defaultPlayerOneSpawn, ref legacySlotOneController);
            SyncSlotAlias(CombatantSlotId.SlotTwo, defaultPlayerTwoSpawn, ref legacySlotTwoController);
        }

        private void SyncSlotAlias(CombatantSlotId slotId, Vector2 defaultSpawnPoint, ref PlayerController alias)
        {
            CombatantSlotConfig slot = roster.GetSlot(slotId);
            if (slot == null)
            {
                return;
            }

            slot.slotId = slotId;
            if (slot.controller == null && alias != null)
            {
                slot.controller = alias;
            }

            if (slot.controller != null)
            {
                alias = slot.controller;
            }

            if (slot.controller != null)
            {
                slot.controller.slotId = Mathf.Max(1, slotId.ToInt());
                if (slot.playerProfile == null && slot.controller.SlotProfile != null)
                {
                    slot.playerProfile = slot.controller.SlotProfile;
                }

                if (slot.playerProfile != null)
                {
                    slot.controller.AssignSlotProfile(slot.playerProfile);
                }

                if (slot.selectedCharacter == null && slot.characterProfile != null)
                {
                    slot.selectedCharacter = slot.characterProfile.ResolveCharacterDefinition();
                }

                if (slot.selectedCharacter == null)
                {
                    slot.selectedCharacter = slot.controller.characterDefinition;
                }
            }

            if (slot.fallbackSpawnPoint == Vector2.zero)
            {
                slot.fallbackSpawnPoint = defaultSpawnPoint;
            }

            slot.displayName = slot.ResolveDisplayName();
        }

        private void SubscribePlayers()
        {
            foreach (PlayerController player in EnumerateControllers())
            {
                player.Died -= HandlePlayerDeath;
                player.Died += HandlePlayerDeath;
            }
        }

        private void UnsubscribePlayers()
        {
            foreach (PlayerController player in EnumerateControllers())
            {
                player.Died -= HandlePlayerDeath;
            }
        }

        private void TogglePlayerTwoBotShortcut()
        {
            EnsurePlayerTwoDebugBotEnabled(!_slotTwoBotShortcutEnabled);
        }

        private void EnsurePlayerTwoDebugBotEnabled(bool enabled, bool forceReapply = false)
        {
            CombatantSlotConfig slot = GetSlot(CombatantSlotId.SlotTwo);
            if (slot == null || (enabled == _slotTwoBotShortcutEnabled && !forceReapply))
            {
                return;
            }

            if (enabled)
            {
                _slotTwoOriginalProfile = slot.playerProfile;
                _slotTwoRuntimeBotProfile = CreateRuntimeControlOverrideProfile(slot.ResolvePlayerProfile(), CombatantSlotId.SlotTwo, CombatantControlMode.AI, slotTwoDebugAiBrain);
                slot.playerProfile = _slotTwoRuntimeBotProfile;
                _slotTwoBotShortcutEnabled = true;
            }
            else
            {
                slot.playerProfile = _slotTwoOriginalProfile;
                _slotTwoBotShortcutEnabled = false;
            }

            slot.ApplySelectionToController();
            Debug.Log($"[CodexBot] Slot 2 bot enabled={enabled} brain={slotTwoDebugAiBrain} profileMode={slot.playerProfile?.controlMode} controller={(slot.controller != null ? slot.controller.name : "<null>")}");
            PrewarmCodexSessionForController(slot.controller);
        }

        private void EnsureSlotTwoCodexBotReadyForPlay()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            // Existing scene instances may deserialize new flags as false, so force
            // the intended auto-play behavior in code instead of relying on Inspector state.
            slotTwoDebugAiBrain = AiBrainKind.CodexBroker;
            Debug.Log("[CodexBot] Forcing slot 2 into AI + CodexBroker at runtime.");
            EnsurePlayerTwoDebugBotEnabled(true, forceReapply: true);
        }

        public void ForceSlotTwoCodexBotReadyForPlay()
        {
            EnsureSlotTwoCodexBotReadyForPlay();
        }

        public void ForceCodexBotsReadyForPlay()
        {
            if (!TryApplyRuntimeBotMenuAssignments())
            {
                EnsureSlotTwoCodexBotReadyForPlay();
            }
        }

        private static CombatantSlotProfile CreateRuntimeControlOverrideProfile(
            CombatantSlotProfile sourceProfile,
            CombatantSlotId slotId,
            CombatantControlMode controlMode,
            AiBrainKind aiBrain)
        {
            CombatantSlotProfile templateProfile = sourceProfile != null
                ? sourceProfile
                : CombatantSlotProfile.ResolveRuntimeFallback(slotId);
            CombatantSlotProfile runtimeProfile = templateProfile != null
                ? Instantiate(templateProfile)
                : null;

            if (runtimeProfile == null)
            {
                return null;
            }

            runtimeProfile.hideFlags = HideFlags.HideAndDontSave;
            runtimeProfile.controlMode = controlMode;
            runtimeProfile.aiBrain = aiBrain;
            return runtimeProfile;
        }

        private bool TryApplyRuntimeBotMenuAssignments()
        {
            if (!Application.isPlaying)
            {
                return false;
            }

            RuntimeBotMenuAssignmentsFile runtimeAssignments = LoadRuntimeBotMenuAssignments();
            if (runtimeAssignments == null || runtimeAssignments.slots == null || runtimeAssignments.slots.Count == 0)
            {
                return false;
            }

            bool anyEnabled = false;
            for (int index = 0; index < Slots.Count; index += 1)
            {
                CombatantSlotConfig slot = Slots[index];
                if (slot == null)
                {
                    continue;
                }

                RuntimeBotMenuSlotAssignment assignment = FindRuntimeAssignment(runtimeAssignments, slot.slotId);
                if (assignment != null && assignment.enabled)
                {
                    ApplyRuntimeBotAssignment(slot, assignment);
                    anyEnabled = true;
                    continue;
                }

                RestoreRuntimeBotAssignment(slot);
            }

            if (anyEnabled)
            {
                Debug.Log($"[CodexBot] Applied runtime bot menu assignments from {ResolveRuntimeBotMenuAssignmentsPath()}");
            }

            return anyEnabled;
        }

        private void ApplyRuntimeBotAssignment(CombatantSlotConfig slot, RuntimeBotMenuSlotAssignment assignment)
        {
            if (slot == null || assignment == null)
            {
                return;
            }

            if (!_runtimeOriginalProfiles.ContainsKey(slot.slotId))
            {
                _runtimeOriginalProfiles[slot.slotId] = slot.playerProfile;
            }

            CombatantSlotProfile overrideProfile = CreateRuntimeControlOverrideProfile(
                slot.ResolvePlayerProfile(),
                slot.slotId,
                CombatantControlMode.AI,
                AiBrainKind.CodexBroker);
            if (overrideProfile == null)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(assignment.botId))
            {
                overrideProfile.botId = assignment.botId.Trim();
            }

            string resolvedName = !string.IsNullOrWhiteSpace(assignment.displayName)
                ? assignment.displayName.Trim()
                : slot.ResolveDisplayName();
            overrideProfile.botDisplayName = resolvedName;
            overrideProfile.displayName = resolvedName;
            slot.playerProfile = overrideProfile;
            _runtimeOverrideProfiles[slot.slotId] = overrideProfile;
            slot.ApplySelectionToController();
            Debug.Log($"[CodexBot] Runtime assignment applied slot={slot.slotId} botId={overrideProfile.botId} display={resolvedName} provider={assignment.provider} model={assignment.model}");
            PrewarmCodexSessionForController(slot.controller);
        }

        private void RestoreRuntimeBotAssignment(CombatantSlotConfig slot)
        {
            if (slot == null)
            {
                return;
            }

            if (!_runtimeOriginalProfiles.TryGetValue(slot.slotId, out CombatantSlotProfile originalProfile))
            {
                return;
            }

            slot.playerProfile = originalProfile;
            slot.ApplySelectionToController();
            _runtimeOriginalProfiles.Remove(slot.slotId);
            _runtimeOverrideProfiles.Remove(slot.slotId);
        }

        private static RuntimeBotMenuSlotAssignment FindRuntimeAssignment(RuntimeBotMenuAssignmentsFile runtimeAssignments, CombatantSlotId slotId)
        {
            if (runtimeAssignments == null || runtimeAssignments.slots == null)
            {
                return null;
            }

            int slotInt = slotId.ToInt();
            for (int index = 0; index < runtimeAssignments.slots.Count; index += 1)
            {
                RuntimeBotMenuSlotAssignment assignment = runtimeAssignments.slots[index];
                if (assignment != null && assignment.slotId == slotInt)
                {
                    return assignment;
                }
            }

            return null;
        }

        private static RuntimeBotMenuAssignmentsFile LoadRuntimeBotMenuAssignments()
        {
            string path = ResolveRuntimeBotMenuAssignmentsPath();
            if (!File.Exists(path))
            {
                return null;
            }

            try
            {
                string json = File.ReadAllText(path);
                if (string.IsNullOrWhiteSpace(json))
                {
                    return null;
                }

                RuntimeBotMenuAssignmentsFile payload = JsonUtility.FromJson<RuntimeBotMenuAssignmentsFile>(json);
                return payload != null && payload.slots != null ? payload : null;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[CodexBot] Failed to load runtime bot assignments from {path}: {exception.Message}");
                return null;
            }
        }

        private static string ResolveRuntimeBotMenuAssignmentsPath()
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", "tools", "bot_memory", "runtime_slot_assignments.json"));
        }

        private void HandlePlayerDeath(PlayerController deadPlayer)
        {
            if (_roundResetRoutine != null || deadPlayer == null)
            {
                return;
            }

            CombatantSlotId roundWinner = CombatantSlotId.None;
            for (int index = 0; index < Slots.Count; index += 1)
            {
                CombatantSlotConfig slot = Slots[index];
                if (slot?.controller == null || slot.controller == deadPlayer)
                {
                    continue;
                }

                AddWin(slot.slotId);
                roundWinner = slot.slotId;
            }

            if (roundWinner == CombatantSlotId.None)
            {
                return;
            }

            _pendingRoundWinnerSlot = roundWinner;
            AdvanceRespawnSeed();
            _pendingChampionSlot = ResolveChampionSlot();
            _roundResetRoutine = StartCoroutine(ResetRoundAfterDelay());
        }

        private void AddWin(CombatantSlotId slotId)
        {
            int slotIndex = slotId.ToIndex();
            if (slotIndex < 0)
            {
                return;
            }

            EnsureSlotWinsCapacity();
            slotWins[slotIndex] += 1;
        }

        private IEnumerator ResetRoundAfterDelay()
        {
            yield return new WaitForSeconds(roundResetDelay);

            if (_pendingChampionSlot != CombatantSlotId.None)
            {
                Debug.Log($"[Rounds] {_pendingChampionSlot.ToDisplayName()} venceu a serie. Resetando rounds e respawn seeds.");
                ShowChampionAnnouncement(_pendingChampionSlot);
                ResetSeriesState();
            }

            RespawnPlayers();
            _pendingRoundWinnerSlot = CombatantSlotId.None;
            _pendingChampionSlot = CombatantSlotId.None;
            _roundResetRoutine = null;
        }

        private void RespawnPlayers(bool applyFreeze = true)
        {
            SyncRosterAliases();
            ForceCodexBotsReadyForPlay();

            for (int index = 0; index < Slots.Count; index += 1)
            {
                CombatantSlotConfig slot = Slots[index];
                if (slot?.controller == null)
                {
                    continue;
                }

                slot.ApplySelectionToController();
                if (slot.slotId == CombatantSlotId.SlotTwo)
                {
                    Debug.Log($"[CodexBot] Respawn applied slot 2 profileMode={slot.playerProfile?.controlMode} aiBrain={slot.playerProfile?.aiBrain} controller={(slot.controller != null ? slot.controller.name : "<null>")}");
                }
                slot.controller.SetSpawnPosition(GetSpawnPoint(slot.slotId));
                slot.controller.SetExternalControlLock(applyFreeze);
                PrewarmCodexSessionForController(slot.controller);
            }

            if (applyFreeze)
            {
                BeginRespawnFreeze();
            }
            else
            {
                _respawnFreezeTimeLeft = 0f;
                SetPlayersExternalControlLock(false);
            }
        }

        private void PrewarmCodexSessions()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            for (int index = 0; index < Slots.Count; index += 1)
            {
                CombatantSlotConfig slot = Slots[index];
                PrewarmCodexSessionForController(slot != null ? slot.controller : null);
            }
        }

        private static void PrewarmCodexSessionForController(PlayerController controller)
        {
            if (controller == null)
            {
                return;
            }

            CodexBrokerCombatantInputSource codexInput = controller.GetComponent<CodexBrokerCombatantInputSource>();
            if (codexInput != null)
            {
                codexInput.PrewarmSession();
            }
        }

        private void CacheSceneSpawnPoints()
        {
            CacheSceneSpawnPoint(CombatantSlotId.SlotOne, defaultPlayerOneSpawn);
            CacheSceneSpawnPoint(CombatantSlotId.SlotTwo, defaultPlayerTwoSpawn);
        }

        private void EnsureRespawnSeedConfiguration()
        {
            if (roundRespawnSeeds == null || roundRespawnSeeds.Count == 0)
            {
                roundRespawnSeeds = CreateDefaultRespawnSeeds();
            }

            for (int index = 0; index < roundRespawnSeeds.Count; index += 1)
            {
                RoundRespawnSeed seed = roundRespawnSeeds[index];
                if (seed == null)
                {
                    roundRespawnSeeds[index] = new RoundRespawnSeed();
                    continue;
                }

                if (string.IsNullOrWhiteSpace(seed.label))
                {
                    seed.label = $"Seed {index + 1}";
                }
            }

            currentRespawnSeedIndex = NormalizeRespawnSeedIndex(currentRespawnSeedIndex);
            maxWins = Mathf.Max(1, maxWins);
            respawnFreezeDuration = Mathf.Max(0f, respawnFreezeDuration);
            championAnnouncementDuration = Mathf.Max(0f, championAnnouncementDuration);
        }

        private void EnsureRoundHudOverlay()
        {
            Type overlayType = ResolveRoundHudOverlayType();
            if (overlayType == null)
            {
                Debug.LogWarning("[Rounds] Round HUD overlay type not found.");
                return;
            }

            Component overlay = GetComponent(overlayType);
            if (overlay == null)
            {
                overlay = gameObject.AddComponent(overlayType);
            }

            MethodInfo setMatchController = overlayType.GetMethod("SetMatchController", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            setMatchController?.Invoke(overlay, new object[] { this });
        }

        private static Type ResolveRoundHudOverlayType()
        {
            const string overlayFullName = "ProjectPVP.Presentation.ProjectPvpRoundHudOverlay";
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int index = 0; index < assemblies.Length; index += 1)
            {
                Type overlayType = assemblies[index].GetType(overlayFullName, false);
                if (overlayType != null)
                {
                    return overlayType;
                }
            }

            return null;
        }

        private void EnsureSlotWinsCapacity()
        {
            if (slotWins == null || slotWins.Length < 2)
            {
                slotWins = new int[2];
            }
        }

        private void ResetWins()
        {
            EnsureSlotWinsCapacity();
            for (int index = 0; index < slotWins.Length; index += 1)
            {
                slotWins[index] = 0;
            }
        }

        private void ResetSeriesState()
        {
            ResetWins();
            ResetRespawnSeedCycle();
        }

        private void BeginRespawnFreeze()
        {
            if (respawnFreezeDuration <= 0f)
            {
                _respawnFreezeTimeLeft = 0f;
                SetPlayersExternalControlLock(false);
                return;
            }

            _respawnFreezeTimeLeft = respawnFreezeDuration;
            SetPlayersExternalControlLock(true);
        }

        private void TickFreezeAndAnnouncements(float deltaTime)
        {
            if (deltaTime <= 0f)
            {
                return;
            }

            if (_respawnFreezeTimeLeft > 0f)
            {
                _respawnFreezeTimeLeft = Mathf.Max(0f, _respawnFreezeTimeLeft - deltaTime);
                if (_respawnFreezeTimeLeft <= 0f)
                {
                    SetPlayersExternalControlLock(false);
                }
            }

            if (_championAnnouncementTimeLeft > 0f)
            {
                _championAnnouncementTimeLeft = Mathf.Max(0f, _championAnnouncementTimeLeft - deltaTime);
                if (_championAnnouncementTimeLeft <= 0f)
                {
                    _championAnnouncementSlot = CombatantSlotId.None;
                }
            }
        }

        private void ShowChampionAnnouncement(CombatantSlotId championSlot)
        {
            _championAnnouncementSlot = championSlot;
            _championAnnouncementTimeLeft = championAnnouncementDuration;
        }

        private void SetPlayersExternalControlLock(bool locked)
        {
            foreach (PlayerController player in EnumerateControllers())
            {
                if (player != null)
                {
                    player.SetExternalControlLock(locked);
                }
            }
        }

        private CombatantSlotId ResolveChampionSlot()
        {
            for (int index = 0; index < Slots.Count; index += 1)
            {
                CombatantSlotConfig slot = Slots[index];
                if (slot != null && GetWins(slot.slotId) >= RoundsToChampion)
                {
                    return slot.slotId;
                }
            }

            return CombatantSlotId.None;
        }

        private bool TryGetCurrentRespawnSeedPoint(CombatantSlotId slotId, out Vector2 spawnPoint)
        {
            if (TryGetRespawnSeed(CurrentRespawnSeedIndex, out RoundRespawnSeed seed))
            {
                spawnPoint = seed.GetSpawnPoint(slotId);
                return true;
            }

            spawnPoint = Vector2.zero;
            return false;
        }

        private bool TryGetRespawnSeed(int seedIndex, out RoundRespawnSeed seed)
        {
            EnsureRespawnSeedConfiguration();
            seed = null;

            if (roundRespawnSeeds == null || roundRespawnSeeds.Count == 0)
            {
                return false;
            }

            int normalizedIndex = NormalizeRespawnSeedIndex(seedIndex);
            if (normalizedIndex < 0 || normalizedIndex >= roundRespawnSeeds.Count)
            {
                return false;
            }

            seed = roundRespawnSeeds[normalizedIndex];
            return seed != null;
        }

        private void AdvanceRespawnSeed()
        {
            if (roundRespawnSeeds == null || roundRespawnSeeds.Count == 0)
            {
                currentRespawnSeedIndex = 0;
                return;
            }

            currentRespawnSeedIndex = (CurrentRespawnSeedIndex + 1) % roundRespawnSeeds.Count;
        }

        private void ResetRespawnSeedCycle()
        {
            currentRespawnSeedIndex = 0;
        }

        private int NormalizeRespawnSeedIndex(int seedIndex)
        {
            if (roundRespawnSeeds == null || roundRespawnSeeds.Count == 0)
            {
                return 0;
            }

            if (seedIndex < 0)
            {
                return 0;
            }

            return seedIndex % roundRespawnSeeds.Count;
        }

        private Vector2 GetFallbackSpawnPoint(CombatantSlotId slotId)
        {
            CombatantSlotConfig slot = GetSlot(slotId);
            int slotIndex = Mathf.Max(0, slotId.ToIndex());

            if (useScenePlayerPositionsAsSpawn && slot != null && slot.fallbackSpawnPoint != Vector2.zero)
            {
                if (!Application.isPlaying && slot.controller != null)
                {
                    return slot.controller.ConfiguredSpawnWorldPosition;
                }

                return slot.fallbackSpawnPoint;
            }

            if (arenaDefinition != null && arenaDefinition.spawnPoints != null && arenaDefinition.spawnPoints.Count > 0)
            {
                return arenaDefinition.GetSpawnPoint(Mathf.Min(slotIndex, arenaDefinition.spawnPoints.Count - 1));
            }

            if (slot != null && slot.fallbackSpawnPoint != Vector2.zero)
            {
                return slot.fallbackSpawnPoint;
            }

            return slotId == CombatantSlotId.SlotTwo ? defaultPlayerTwoSpawn : defaultPlayerOneSpawn;
        }

        private static List<RoundRespawnSeed> CreateDefaultRespawnSeeds()
        {
            return new List<RoundRespawnSeed>
            {
                new RoundRespawnSeed
                {
                    label = "Low Corner Reset",
                    slotOneSpawnPoint = new Vector2(-639f, -572f),
                    slotTwoSpawnPoint = new Vector2(690f, -576f),
                },
                new RoundRespawnSeed
                {
                    label = "Outer Mid Platforms",
                    slotOneSpawnPoint = new Vector2(-560f, 110f),
                    slotTwoSpawnPoint = new Vector2(830f, -6f),
                },
                new RoundRespawnSeed
                {
                    label = "High Perches",
                    slotOneSpawnPoint = new Vector2(-1110f, 366f),
                    slotTwoSpawnPoint = new Vector2(1108f, 382f),
                },
                new RoundRespawnSeed
                {
                    label = "Bridge Scramble",
                    slotOneSpawnPoint = new Vector2(-96f, -248f),
                    slotTwoSpawnPoint = new Vector2(364f, -250f),
                },
                new RoundRespawnSeed
                {
                    label = "Inner Mid Platforms",
                    slotOneSpawnPoint = new Vector2(-342f, 108f),
                    slotTwoSpawnPoint = new Vector2(632f, -4f),
                },
            };
        }

        private void CacheSceneSpawnPoint(CombatantSlotId slotId, Vector2 defaultSpawnPoint)
        {
            CombatantSlotConfig slot = GetSlot(slotId);
            if (slot == null)
            {
                return;
            }

            if (slot.controller != null && slot.fallbackSpawnPoint == Vector2.zero)
            {
                slot.fallbackSpawnPoint = slot.controller.ConfiguredSpawnWorldPosition;
                return;
            }

            if (slot.fallbackSpawnPoint == Vector2.zero)
            {
                slot.fallbackSpawnPoint = defaultSpawnPoint;
            }
        }

        private PlayerController CreateRuntimeController(CombatantSlotConfig slot)
        {
            if (slot == null)
            {
                return null;
            }

            CharacterBootstrapProfile characterProfile = slot.ResolveCharacterProfile();
            if (characterProfile == null)
            {
                return null;
            }

            Transform parent = transform.parent != null ? transform.parent : transform;
            PlayerController spawnedController = CharacterBootstrapFactory.CreateCombatant(
                characterProfile,
                slot.slotId,
                slot.ResolvePlayerProfile(),
                parent);

            if (spawnedController != null && slot.fallbackSpawnPoint != Vector2.zero)
            {
                spawnedController.transform.position = slot.fallbackSpawnPoint;
            }

            return spawnedController;
        }

        private void EnsureMusicSource()
        {
            if (_musicSource == null)
            {
                _musicSource = GetComponent<AudioSource>();
            }

            if (_musicSource == null && Application.isPlaying)
            {
                _musicSource = gameObject.AddComponent<AudioSource>();
            }

            if (_musicSource == null)
            {
                return;
            }

            _musicSource.playOnAwake = false;
            _musicSource.loop = true;
            _musicSource.spatialBlend = 0f;
        }

        private void PlayArenaMusic()
        {
            if (_musicSource == null)
            {
                return;
            }

            AudioClip musicClip = arenaDefinition != null ? arenaDefinition.ResolveBackgroundMusicClip() : null;
            if (musicClip == null)
            {
                _musicSource.Stop();
                _musicSource.clip = null;
                return;
            }

            if (_musicSource.clip == musicClip && _musicSource.isPlaying)
            {
                return;
            }

            _musicSource.Stop();
            _musicSource.clip = musicClip;
            _musicSource.volume = AudioRuntimeUtility.DecibelsToLinear(arenaDefinition != null ? arenaDefinition.backgroundMusicVolumeDb : -14f);
            _musicSource.Play();
        }

        private Vector2 GetWrapPadding()
        {
            return arenaDefinition != null ? arenaDefinition.wrapPadding : defaultWrapPadding;
        }

        private void ApplyWrap(PlayerController player)
        {
            if (player == null)
            {
                return;
            }

            Rect wrapBounds = ActiveWrapBounds;
            Vector2 wrapPadding = GetWrapPadding();
            Vector3 position = player.transform.position;
            if (position.x < wrapBounds.xMin - wrapPadding.x)
            {
                position.x = wrapBounds.xMax + wrapPadding.x;
            }
            else if (position.x > wrapBounds.xMax + wrapPadding.x)
            {
                position.x = wrapBounds.xMin - wrapPadding.x;
            }

            if (position.y < wrapBounds.yMin - wrapPadding.y)
            {
                position.y = wrapBounds.yMax + wrapPadding.y;
            }
            else if (position.y > wrapBounds.yMax + wrapPadding.y)
            {
                position.y = wrapBounds.yMin - wrapPadding.y;
            }

            if (player.TryGetComponent(out Rigidbody2D body))
            {
                body.position = new Vector2(position.x, position.y);
            }
            else
            {
                player.transform.position = position;
            }
        }
    }
}
