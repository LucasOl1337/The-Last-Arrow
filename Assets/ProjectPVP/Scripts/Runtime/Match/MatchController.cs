using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
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

    public sealed class MatchController : MonoBehaviour, IAiArenaArenaSnapshotSource
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
        public bool verticalRingOutEnabled = true;
        public int maxWins = 5;
        public float roundResetDelay = 1.25f;
        public float respawnFreezeDuration = 0.5f;
        public float championAnnouncementDuration = 2f;
        [FormerlySerializedAs("autoBalanceShieldsEnabled")]
        public bool autoBalanceLoadoutEnabled = true;
        public bool corpsesDropArrowsEnabled = true;
        [SerializeField] private List<RoundRespawnSeed> roundRespawnSeeds = CreateDefaultRespawnSeeds();
        [SerializeField] private int currentRespawnSeedIndex;
        public Vector2 defaultPlayerOneSpawn = new Vector2(-420f, -540f);
        public Vector2 defaultPlayerTwoSpawn = new Vector2(420f, -540f);
        public Rect defaultWrapBounds = new Rect(-1280f, -720f, 2560f, 1440f);
        public Vector2 defaultWrapPadding = new Vector2(40f, 40f);
        [Header("Debug Shortcuts")]
        public bool enableDebugShortcuts = true;
        public bool autoEnableSlotTwoDebugBotOnPlay = true;
        public bool autoForceCodexBrokerForSlotTwoOnPlay = false;
        public AiBrainKind slotTwoDebugAiBrain = AiBrainKind.LocalHeuristic;
        public global::UnityEngine.KeyCode codexBotReplanKey = global::UnityEngine.KeyCode.F8;
        public global::UnityEngine.KeyCode codexBotRestartKey = global::UnityEngine.KeyCode.F9;
        public global::UnityEngine.KeyCode codexBotAgentModeToggleKey = global::UnityEngine.KeyCode.F10;

        private AudioSource _musicSource;
        [SerializeField] private int[] slotWins = new int[2];
        private Coroutine _roundResetRoutine;
        private readonly RuntimeBotAssignmentService _runtimeBotAssignments = new RuntimeBotAssignmentService();
        private readonly RoundTimerService _roundTimers = new RoundTimerService();
        private CombatantSlotProfile _slotTwoOriginalProfile;
        private CombatantSlotProfile _slotTwoRuntimeBotProfile;
        private bool _slotTwoBotShortcutEnabled;
        private CombatantSlotId _pendingRoundWinnerSlot = CombatantSlotId.None;
        private CombatantSlotId _pendingChampionSlot = CombatantSlotId.None;
        private string _lastRoundDeathSummary = string.Empty;
        private Vector2 _lastRoundDeathPosition = Vector2.zero;
        private readonly List<PlayerController> _pendingDeadPlayers = new List<PlayerController>(2);
        private bool _resolveQueuedDeathsPending;

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

                Debug.Log($"[CodexBot] Bootstrap applying runtime bot automation on MatchController instance {controller.name}.");
                controller.ApplyCodexBotAutomationForPlay();
            }
        }

        public IReadOnlyList<CombatantSlotConfig> Slots => roster != null ? roster.Slots : System.Array.Empty<CombatantSlotConfig>();
        public IReadOnlyList<CharacterBootstrapProfile> AvailableCharacters => characterCatalog != null ? characterCatalog.Characters : System.Array.Empty<CharacterBootstrapProfile>();
        public IReadOnlyList<RoundRespawnSeed> RoundRespawnSeeds => roundRespawnSeeds != null
            ? (IReadOnlyList<RoundRespawnSeed>)roundRespawnSeeds
            : System.Array.Empty<RoundRespawnSeed>();
        public int PlayerOneWins => GetWins(CombatantSlotId.SlotOne);
        public int PlayerTwoWins => GetWins(CombatantSlotId.SlotTwo);
        public int RoundsToChampion => Mathf.Max(1, maxWins);
        public bool IsRoundResetPending => _roundResetRoutine != null || _resolveQueuedDeathsPending || _pendingDeadPlayers.Count > 0;
        public bool IsRespawnFreezeActive => _roundTimers.IsRespawnFreezeActive;
        public int CurrentRespawnSeedIndex => NormalizeRespawnSeedIndex(currentRespawnSeedIndex);
        public string CurrentRespawnSeedLabel => TryGetRespawnSeed(CurrentRespawnSeedIndex, out RoundRespawnSeed seed)
            ? seed.ResolveLabel(CurrentRespawnSeedIndex + 1)
            : "Fallback";
        public CombatantSlotId PendingRoundWinnerSlot => _pendingRoundWinnerSlot;
        public CombatantSlotId PendingChampionSlot => _pendingChampionSlot;
        public CombatantSlotId ChampionAnnouncementSlot => _roundTimers.ChampionAnnouncementSlot;
        public string LastRoundDeathSummary => _lastRoundDeathSummary;
        public Vector2 LastRoundDeathPosition => _lastRoundDeathPosition;
        public Rect ActiveWrapBounds => arenaDefinition != null ? arenaDefinition.wrapBounds : defaultWrapBounds;
        public Vector2 PlayerOneSpawnPoint => GetSpawnPoint(CombatantSlotId.SlotOne);
        public Vector2 PlayerTwoSpawnPoint => GetSpawnPoint(CombatantSlotId.SlotTwo);
        public PlayerController PlayerOneController => GetSlot(CombatantSlotId.SlotOne)?.controller;
        public PlayerController PlayerTwoController => GetSlot(CombatantSlotId.SlotTwo)?.controller;

#pragma warning disable IDE1006
        public PlayerController playerOne => PlayerOneController;
        public PlayerController playerTwo => PlayerTwoController;
#pragma warning restore IDE1006

        public AiArenaArenaSnapshot BuildAiArenaArenaSnapshot()
        {
            return MatchArenaSnapshotService.Build(new MatchArenaSnapshotState(
                wrapBounds: ActiveWrapBounds,
                roundResetPending: IsRoundResetPending,
                roundsToChampion: RoundsToChampion,
                playerOneWins: PlayerOneWins,
                playerTwoWins: PlayerTwoWins,
                currentRespawnSeedIndex: CurrentRespawnSeedIndex,
                currentRespawnSeedLabel: CurrentRespawnSeedLabel,
                pendingRoundWinnerSlot: PendingRoundWinnerSlot,
                pendingChampionSlot: PendingChampionSlot,
                championAnnouncementSlot: ChampionAnnouncementSlot));
        }

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
            if (Application.isPlaying)
            {
                AiArenaSnapshotSourceRegistry.Register(this);
            }
        }

        private void OnDisable()
        {
            AiArenaSnapshotSourceRegistry.Unregister(this);
            ResetTransientRoundState();
            UnsubscribePlayers();
        }

        private void Start()
        {
            SyncRosterAliases();
            Debug.Log($"[CodexBot] MatchController.Start applying runtime bot automation auto-play={autoEnableSlotTwoDebugBotOnPlay} forceBrain={autoForceCodexBrokerForSlotTwoOnPlay}");
            ApplyCodexBotAutomationForPlay();
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

            HandleCodexBotControlShortcuts();
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

        private void HandleCodexBotControlShortcuts()
        {
            if (codexBotReplanKey != global::UnityEngine.KeyCode.None && RuntimeInput.GetKeyDown(codexBotReplanKey))
            {
                RequestCodexBotReplan("shortcut_" + codexBotReplanKey);
            }

            if (codexBotRestartKey != global::UnityEngine.KeyCode.None && RuntimeInput.GetKeyDown(codexBotRestartKey))
            {
                RestartCodexBotSessions("shortcut_" + codexBotRestartKey);
            }

            if (codexBotAgentModeToggleKey != global::UnityEngine.KeyCode.None && RuntimeInput.GetKeyDown(codexBotAgentModeToggleKey))
            {
                ToggleCodexBotAgentMode("shortcut_" + codexBotAgentModeToggleKey);
            }
        }

        private int RequestCodexBotReplan(string reason)
        {
            return ForEachActiveCodexBrokerInput(reason, input => input.RequestImmediateReplan(reason));
        }

        private int RestartCodexBotSessions(string reason)
        {
            return ForEachActiveCodexBrokerInput(reason, input => input.RestartBrokerSession(reason));
        }

        private int ToggleCodexBotAgentMode(string reason)
        {
            bool enableAgentMode = ShouldEnableCodexAgentModeOnToggle();
            return ForEachActiveCodexBrokerInput(reason, input => input.SetAgentDrivenMode(enableAgentMode));
        }

        private bool ShouldEnableCodexAgentModeOnToggle()
        {
            SyncRosterAliases();
            IReadOnlyList<CombatantSlotConfig> slots = Slots;
            for (int index = 0; index < slots.Count; index += 1)
            {
                CodexBrokerCombatantInputSource input = ResolveActiveCodexBrokerInput(slots[index]);
                if (input != null && !input.useAgentDrivenMode)
                {
                    return true;
                }
            }

            return false;
        }

        private int ForEachActiveCodexBrokerInput(string reason, Action<CodexBrokerCombatantInputSource> command)
        {
            SyncRosterAliases();
            int count = 0;
            IReadOnlyList<CombatantSlotConfig> slots = Slots;
            for (int index = 0; index < slots.Count; index += 1)
            {
                CodexBrokerCombatantInputSource input = ResolveActiveCodexBrokerInput(slots[index]);
                if (input == null)
                {
                    continue;
                }

                command(input);
                count += 1;
            }

            if (count > 0)
            {
                Debug.Log($"[CodexBot] Applied bot control command reason={reason} count={count}.");
            }

            return count;
        }

        private static CodexBrokerCombatantInputSource ResolveActiveCodexBrokerInput(CombatantSlotConfig slot)
        {
            PlayerController player = slot != null ? slot.controller : null;
            return player != null ? player.InputSource as CodexBrokerCombatantInputSource : null;
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
            return RoundFlowService.GetWins(slotWins, slotId);
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
                if (!_slotTwoBotShortcutEnabled)
                {
                    _slotTwoOriginalProfile = slot.playerProfile;
                }

                _slotTwoRuntimeBotProfile = RuntimeBotAssignmentService.CreateRuntimeControlOverrideProfile(
                    slot.ResolvePlayerProfile(),
                    CombatantSlotId.SlotTwo,
                    CombatantControlMode.AI,
                    slotTwoDebugAiBrain);
                slot.playerProfile = _slotTwoRuntimeBotProfile;
                _slotTwoBotShortcutEnabled = true;
            }
            else
            {
                slot.playerProfile = _slotTwoOriginalProfile;
                _slotTwoOriginalProfile = null;
                _slotTwoRuntimeBotProfile = null;
                _slotTwoBotShortcutEnabled = false;
            }

            slot.ApplySelectionToController();
            Debug.Log($"[CodexBot] Slot 2 bot enabled={enabled} brain={slotTwoDebugAiBrain} profileMode={slot.playerProfile?.controlMode} controller={(slot.controller != null ? slot.controller.name : "<null>")}");
            PrewarmCodexSessionForController(slot.controller);
        }

        private void EnsureSlotTwoCodexBotReadyForPlay()
        {
            EnsureSlotTwoDebugBotReadyForPlay(AiBrainKind.CodexBroker, "Forcing slot 2 into AI + CodexBroker at runtime.");
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

        private void ApplyCodexBotAutomationForPlay()
        {
            if (TryApplyRuntimeBotMenuAssignments())
            {
                return;
            }

            if (!ShouldApplySlotTwoBotFallback())
            {
                Debug.Log("[CodexBot] No runtime bot menu assignments found and slot 2 auto bot fallback is disabled.");
                return;
            }

            EnsureSlotTwoDebugBotReadyForPlay(
                ResolveSlotTwoAutoBotBrain(),
                autoForceCodexBrokerForSlotTwoOnPlay
                    ? "Auto enabling slot 2 as AI + CodexBroker at runtime."
                    : $"Auto enabling slot 2 as AI + {slotTwoDebugAiBrain} at runtime.");
        }

        private bool ShouldApplySlotTwoBotFallback()
        {
            return autoEnableSlotTwoDebugBotOnPlay;
        }

        private AiBrainKind ResolveSlotTwoAutoBotBrain()
        {
            return autoForceCodexBrokerForSlotTwoOnPlay
                ? AiBrainKind.CodexBroker
                : slotTwoDebugAiBrain;
        }

        private void EnsureSlotTwoDebugBotReadyForPlay(AiBrainKind aiBrain, string logMessage)
        {
            if (!Application.isPlaying)
            {
                return;
            }

            slotTwoDebugAiBrain = aiBrain;
            Debug.Log($"[CodexBot] {logMessage}");
            EnsurePlayerTwoDebugBotEnabled(true, forceReapply: true);
        }

        private bool TryApplyRuntimeBotMenuAssignments()
        {
            if (!Application.isPlaying)
            {
                return false;
            }

            RuntimeBotMenuAssignmentsFile runtimeAssignments = LoadRuntimeBotMenuAssignments();
            if (!ApplyRuntimeBotMenuAssignments(runtimeAssignments, out bool anyEnabled))
            {
                return false;
            }

            string path = ResolveRuntimeBotMenuAssignmentsPath();
            if (anyEnabled)
            {
                Debug.Log($"[CodexBot] Applied runtime bot menu assignments from {path}");
            }
            else
            {
                Debug.Log($"[CodexBot] Runtime bot menu assignments from {path} disabled all slots; skipping automatic slot 2 fallback.");
            }

            return true;
        }

        private bool ApplyRuntimeBotMenuAssignments(RuntimeBotMenuAssignmentsFile runtimeAssignments, out bool anyEnabled)
        {
            anyEnabled = false;
            if (runtimeAssignments == null || runtimeAssignments.slots == null || runtimeAssignments.slots.Count == 0)
            {
                return false;
            }

            SyncRosterAliases();
            List<CombatantSlotConfig> changedSlots = new List<CombatantSlotConfig>();
            bool processed = _runtimeBotAssignments.ApplyAssignments(Slots, runtimeAssignments, out anyEnabled, changedSlots);
            PrewarmRuntimeAssignmentSlots(changedSlots);
            return processed;
        }

        private void PrewarmRuntimeAssignmentSlots(IEnumerable<CombatantSlotConfig> changedSlots)
        {
            if (changedSlots == null)
            {
                return;
            }

            foreach (CombatantSlotConfig slot in changedSlots)
            {
                PrewarmCodexSessionForController(slot != null ? slot.controller : null);
            }
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
            if (_roundResetRoutine != null)
            {
                return;
            }

            if (deadPlayer != null && !_pendingDeadPlayers.Contains(deadPlayer))
            {
                DropCorpseArrows(deadPlayer);
                _pendingDeadPlayers.Add(deadPlayer);
            }

            if (_resolveQueuedDeathsPending)
            {
                return;
            }

            _resolveQueuedDeathsPending = true;
            StartCoroutine(ResolveQueuedDeathsAfterFrame());
        }

        private IEnumerator ResolveQueuedDeathsAfterFrame()
        {
            yield return null;
            ResolveQueuedDeaths();
        }

        private void ResolveQueuedDeaths()
        {
            _resolveQueuedDeathsPending = false;
            if (_roundResetRoutine != null || _pendingDeadPlayers.Count == 0)
            {
                _pendingDeadPlayers.Clear();
                return;
            }

            PlayerController resolvedDeadPlayer = _pendingDeadPlayers[_pendingDeadPlayers.Count - 1];
            _pendingDeadPlayers.Clear();

            RoundDeathResolution deathResolution = RoundDeathService.ResolveDeath(Slots, resolvedDeadPlayer);
            if (deathResolution.HasWinner)
            {
                for (int index = 0; index < deathResolution.WinningSlots.Count; index += 1)
                {
                    AddWin(deathResolution.WinningSlots[index]);
                }

                _pendingRoundWinnerSlot = deathResolution.RoundWinnerSlot;
                _lastRoundDeathSummary = ResolveDeathSummary(resolvedDeadPlayer);
                _lastRoundDeathPosition = ResolveDeathPosition(resolvedDeadPlayer);
                _pendingChampionSlot = ResolveChampionSlot();
            }
            else
            {
                _pendingRoundWinnerSlot = CombatantSlotId.None;
                _pendingChampionSlot = CombatantSlotId.None;
                _lastRoundDeathSummary = string.Empty;
                _lastRoundDeathPosition = Vector2.zero;
            }

            AdvanceRespawnSeed();
            _roundResetRoutine = StartCoroutine(ResetRoundAfterDelay());
        }

        private void AddWin(CombatantSlotId slotId)
        {
            EnsureSlotWinsCapacity();
            RoundFlowService.AddWin(slotWins, slotId);
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
            ApplyCodexBotAutomationForPlay();
            ProjectileController.DestroyActiveProjectilesForRoundReset();

            List<RespawnSlotCommand> respawnCommands = RespawnService.BuildRespawnCommands(
                Slots,
                GetSpawnPoint,
                applyFreeze);
            RespawnService.ApplyRespawnCommands(respawnCommands, HandleRespawnCommandApplied);
            ApplyAutoBalanceLoadout();

            if (!applyFreeze)
            {
                _lastRoundDeathSummary = string.Empty;
                _lastRoundDeathPosition = Vector2.zero;
            }

            if (applyFreeze)
            {
                BeginRespawnFreeze();
            }
            else
            {
                _roundTimers.ClearRespawnFreeze();
                SetPlayersExternalControlLock(false);
            }
        }

        private void HandleRespawnCommandApplied(RespawnSlotCommand command)
        {
            CombatantSlotConfig slot = command.Slot;
            if (slot != null && slot.slotId == CombatantSlotId.SlotTwo)
            {
                Debug.Log($"[CodexBot] Respawn applied slot 2 profileMode={slot.playerProfile?.controlMode} aiBrain={slot.playerProfile?.aiBrain} controller={(slot.controller != null ? slot.controller.name : "<null>")}");
            }

            PrewarmCodexSessionForController(command.Controller);
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
            ProjectPvpMatchRoundHudOverlay runtimeOverlay = GetComponent<ProjectPvpMatchRoundHudOverlay>();
            if (runtimeOverlay == null)
            {
                runtimeOverlay = gameObject.AddComponent<ProjectPvpMatchRoundHudOverlay>();
            }
            runtimeOverlay.SetMatchController(this);
            Debug.Log($"[Rounds] Attached round HUD overlay to MatchController '{name}'.");
        }

        private void EnsureSlotWinsCapacity()
        {
            slotWins = RoundFlowService.EnsureSlotWinsCapacity(slotWins);
        }

        private void ResetWins()
        {
            EnsureSlotWinsCapacity();
            RoundFlowService.ResetWins(slotWins);
        }

        private void ResetSeriesState()
        {
            ResetWins();
            ResetRespawnSeedCycle();
        }

        private void ResetTransientRoundState()
        {
            StopRoundResetRoutine();
            _roundTimers.ClearRespawnFreeze();
            _roundTimers.ClearChampionAnnouncement();
            _pendingRoundWinnerSlot = CombatantSlotId.None;
            _pendingChampionSlot = CombatantSlotId.None;
            _lastRoundDeathSummary = string.Empty;
            _lastRoundDeathPosition = Vector2.zero;
            _pendingDeadPlayers.Clear();
            _resolveQueuedDeathsPending = false;
        }

        private void StopRoundResetRoutine()
        {
            if (_roundResetRoutine == null)
            {
                return;
            }

            StopCoroutine(_roundResetRoutine);
            _roundResetRoutine = null;
        }

        private void ApplyAutoBalanceLoadout()
        {
            int highestWins = 0;
            int leaderCount = 0;
            for (int index = 0; index < Slots.Count; index += 1)
            {
                CombatantSlotConfig slot = Slots[index];
                if (slot == null || slot.controller == null)
                {
                    continue;
                }

                int wins = GetWins(slot.slotId);
                if (wins > highestWins)
                {
                    highestWins = wins;
                    leaderCount = 1;
                    continue;
                }

                if (wins == highestWins)
                {
                    leaderCount += 1;
                }
            }

            // TowerFall-style autobalance: leaders start a little lighter on arrows,
            // and players behind by 3+ rounds get a shield to help close the gap.
            for (int index = 0; index < Slots.Count; index += 1)
            {
                CombatantSlotConfig slot = Slots[index];
                PlayerController player = slot != null ? slot.controller : null;
                if (player == null)
                {
                    continue;
                }

                int wins = GetWins(slot.slotId);
                bool isLeader = autoBalanceLoadoutEnabled && highestWins > 0 && leaderCount == 1 && wins == highestWins;
                int arrowPenalty = isLeader ? 1 : 0;
                int targetArrows = isLeader ? Mathf.Max(1, player.CurrentArrows - arrowPenalty) : player.CurrentArrows;
                player.SetRoundArrowCount(targetArrows);

                bool hasShield = autoBalanceLoadoutEnabled && highestWins - wins >= 3;
                player.SetRoundShield(hasShield);
            }
        }

        private void DropCorpseArrows(PlayerController deadPlayer)
        {
            if (!corpsesDropArrowsEnabled || deadPlayer == null || deadPlayer.projectilePrefab == null)
            {
                return;
            }

            int arrowCount = Mathf.Max(0, deadPlayer.CurrentArrows);
            if (arrowCount == 0)
            {
                return;
            }

            Vector2 corpsePosition = ResolveDeathPosition(deadPlayer);
            float lateralSpacing = 8f;
            float verticalNudge = 4f;
            for (int index = 0; index < arrowCount; index += 1)
            {
                float centeredIndex = index - ((arrowCount - 1) * 0.5f);
                Vector2 dropOrigin = corpsePosition + new Vector2(centeredIndex * lateralSpacing, verticalNudge);
                ProjectileController.SpawnDroppedArrow(
                    deadPlayer.projectilePrefab,
                    deadPlayer.characterDefinition,
                    dropOrigin);
            }
        }

        private string ResolveDeathSummary(PlayerController deadPlayer)
        {
            if (deadPlayer == null || string.IsNullOrWhiteSpace(deadPlayer.LastFatalHitCause))
            {
                return string.Empty;
            }

            string sourceLabel = deadPlayer.LastFatalHitSource != null
                ? deadPlayer.LastFatalHitSource.BotDisplayName
                : "Environment";
            return sourceLabel + " via " + deadPlayer.LastFatalHitCause;
        }

        private Vector2 ResolveDeathPosition(PlayerController deadPlayer)
        {
            return deadPlayer != null ? deadPlayer.LastFatalHitPosition : Vector2.zero;
        }

        private void BeginRespawnFreeze(float durationOverride = -1f)
        {
            float duration = durationOverride >= 0f ? durationOverride : respawnFreezeDuration;
            SetPlayersExternalControlLock(_roundTimers.BeginRespawnFreeze(duration));
        }

        private void TickFreezeAndAnnouncements(float deltaTime)
        {
            RoundTimerTickResult result = _roundTimers.Tick(deltaTime);
            if (result.RespawnFreezeEnded)
            {
                SetPlayersExternalControlLock(false);
                if (_roundTimers.ChampionAnnouncementSlot == CombatantSlotId.None)
                {
                    _lastRoundDeathSummary = string.Empty;
                    _lastRoundDeathPosition = Vector2.zero;
                }
            }

            if (result.ChampionAnnouncementEnded)
            {
                _lastRoundDeathSummary = string.Empty;
                _lastRoundDeathPosition = Vector2.zero;
            }
        }

        private void ShowChampionAnnouncement(CombatantSlotId championSlot, float durationOverride = -1f)
        {
            float duration = durationOverride >= 0f ? durationOverride : championAnnouncementDuration;
            _roundTimers.ShowChampionAnnouncement(championSlot, duration);
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
            return RoundFlowService.ResolveChampionSlot(Slots, slotWins, RoundsToChampion);
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
            currentRespawnSeedIndex = RoundFlowService.AdvanceRespawnSeed(
                currentRespawnSeedIndex,
                roundRespawnSeeds != null ? roundRespawnSeeds.Count : 0);
        }

        private void ResetRespawnSeedCycle()
        {
            currentRespawnSeedIndex = RoundFlowService.ResetRespawnSeedCycle();
        }

        private int NormalizeRespawnSeedIndex(int seedIndex)
        {
            return RoundFlowService.NormalizeRespawnSeedIndex(
                seedIndex,
                roundRespawnSeeds != null ? roundRespawnSeeds.Count : 0);
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
            bool createdFallbackProfile = false;
            if (characterProfile == null)
            {
                CharacterDefinition selectedCharacter = slot.ResolveCharacterDefinition();
                if (selectedCharacter == null)
                {
                    return null;
                }

                if (characterCatalog != null)
                {
                    characterProfile = characterCatalog.FindByDefinition(selectedCharacter);
                }

                if (characterProfile == null)
                {
                    characterProfile = ScriptableObject.CreateInstance<CharacterBootstrapProfile>();
                    characterProfile.hideFlags = HideFlags.HideAndDontSave;
                    characterProfile.characterDefinition = selectedCharacter;
                    characterProfile.displayName = selectedCharacter.displayName;
                    createdFallbackProfile = true;
                }
            }

            try
            {
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
            finally
            {
                if (createdFallbackProfile && characterProfile != null)
                {
                    UnityEngine.Object.DestroyImmediate(characterProfile);
                }
            }
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
            if (player == null || player.IsDead)
            {
                return;
            }

            Rect wrapBounds = ActiveWrapBounds;
            Vector2 wrapPadding = GetWrapPadding();
            Vector3 position = player.transform.position;
            if (verticalRingOutEnabled && position.y < wrapBounds.yMin - wrapPadding.y)
            {
                player.Kill(null, "Ring Out");
                return;
            }

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
