using System.Collections;
using System.Collections.Generic;
using ProjectPVP.Audio;
using ProjectPVP.Characters;
using ProjectPVP.Data;
using ProjectPVP.Gameplay;
using UnityEngine;
using UnityEngine.Serialization;

namespace ProjectPVP.Match
{
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
        public Vector2 defaultPlayerOneSpawn = new Vector2(-420f, -540f);
        public Vector2 defaultPlayerTwoSpawn = new Vector2(420f, -540f);
        public Rect defaultWrapBounds = new Rect(-1280f, -720f, 2560f, 1440f);
        public Vector2 defaultWrapPadding = new Vector2(40f, 40f);

        private AudioSource _musicSource;
        [SerializeField] private int[] slotWins = new int[2];
        private Coroutine _roundResetRoutine;

        public IReadOnlyList<CombatantSlotConfig> Slots => roster != null ? roster.Slots : System.Array.Empty<CombatantSlotConfig>();
        public IReadOnlyList<CharacterBootstrapProfile> AvailableCharacters => characterCatalog != null ? characterCatalog.Characters : System.Array.Empty<CharacterBootstrapProfile>();
        public int PlayerOneWins => GetWins(CombatantSlotId.SlotOne);
        public int PlayerTwoWins => GetWins(CombatantSlotId.SlotTwo);
        public bool IsRoundResetPending => _roundResetRoutine != null;
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
            EnsureRuntimeCombatantsForConfiguredSlots();
        }

        private void OnValidate()
        {
            SyncRosterAliases();
        }

        private void OnEnable()
        {
            SyncRosterAliases();
            SubscribePlayers();
        }

        private void OnDisable()
        {
            UnsubscribePlayers();
        }

        private void Start()
        {
            SyncRosterAliases();
            CacheSceneSpawnPoints();
            EnsureMusicSource();
            PlayArenaMusic();
            RespawnPlayers();
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

        private void SyncRosterAliases()
        {
            roster ??= new MatchRoster();
            EnsureSlotWinsCapacity();
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

        private void HandlePlayerDeath(PlayerController deadPlayer)
        {
            if (_roundResetRoutine != null || deadPlayer == null)
            {
                return;
            }

            for (int index = 0; index < Slots.Count; index += 1)
            {
                CombatantSlotConfig slot = Slots[index];
                if (slot?.controller == null || slot.controller == deadPlayer)
                {
                    continue;
                }

                AddWin(slot.slotId);
            }

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

            if (GetWins(CombatantSlotId.SlotOne) >= maxWins || GetWins(CombatantSlotId.SlotTwo) >= maxWins)
            {
                ResetWins();
            }

            RespawnPlayers();
            _roundResetRoutine = null;
        }

        private void RespawnPlayers()
        {
            SyncRosterAliases();

            for (int index = 0; index < Slots.Count; index += 1)
            {
                CombatantSlotConfig slot = Slots[index];
                if (slot?.controller == null)
                {
                    continue;
                }

                slot.ApplySelectionToController();
                slot.controller.SetSpawnPosition(GetSpawnPoint(slot.slotId));
            }
        }

        private void CacheSceneSpawnPoints()
        {
            CacheSceneSpawnPoint(CombatantSlotId.SlotOne, defaultPlayerOneSpawn);
            CacheSceneSpawnPoint(CombatantSlotId.SlotTwo, defaultPlayerTwoSpawn);
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
