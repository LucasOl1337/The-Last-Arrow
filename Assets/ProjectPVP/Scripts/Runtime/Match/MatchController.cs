using System.Collections;
using ProjectPVP.Audio;
using ProjectPVP.Data;
using ProjectPVP.Gameplay;
using UnityEngine;

namespace ProjectPVP.Match
{
    public sealed class MatchController : MonoBehaviour
    {
        public ArenaDefinitionAsset arenaDefinition;
        public PlayerController playerOne;
        public PlayerController playerTwo;
        public bool useScenePlayerPositionsAsSpawn = true;
        public bool wrapEnabled = true;
        public int maxWins = 5;
        public float roundResetDelay = 1.25f;
        public Vector2 defaultPlayerOneSpawn = new Vector2(-420f, -540f);
        public Vector2 defaultPlayerTwoSpawn = new Vector2(420f, -540f);
        public Rect defaultWrapBounds = new Rect(-1280f, -720f, 2560f, 1440f);
        public Vector2 defaultWrapPadding = new Vector2(40f, 40f);

        private AudioSource _musicSource;
        private int _playerOneWins;
        private int _playerTwoWins;
        private Coroutine _roundResetRoutine;
        private Vector2 _scenePlayerOneSpawn;
        private Vector2 _scenePlayerTwoSpawn;

        public int PlayerOneWins => _playerOneWins;
        public int PlayerTwoWins => _playerTwoWins;
        public bool IsRoundResetPending => _roundResetRoutine != null;
        public Rect ActiveWrapBounds => arenaDefinition != null ? arenaDefinition.wrapBounds : defaultWrapBounds;
        public Vector2 PlayerOneSpawnPoint => GetSpawnPoint(0);
        public Vector2 PlayerTwoSpawnPoint => GetSpawnPoint(1);

        private void OnEnable()
        {
            if (playerOne != null)
            {
                playerOne.Died += HandlePlayerDeath;
            }

            if (playerTwo != null)
            {
                playerTwo.Died += HandlePlayerDeath;
            }
        }

        private void OnDisable()
        {
            if (playerOne != null)
            {
                playerOne.Died -= HandlePlayerDeath;
            }

            if (playerTwo != null)
            {
                playerTwo.Died -= HandlePlayerDeath;
            }
        }

        private void Start()
        {
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

            ApplyWrap(playerOne);
            ApplyWrap(playerTwo);
        }

        private void HandlePlayerDeath(PlayerController deadPlayer)
        {
            if (_roundResetRoutine != null)
            {
                return;
            }

            if (deadPlayer == playerOne)
            {
                _playerTwoWins += 1;
            }
            else if (deadPlayer == playerTwo)
            {
                _playerOneWins += 1;
            }

            _roundResetRoutine = StartCoroutine(ResetRoundAfterDelay());
        }

        private IEnumerator ResetRoundAfterDelay()
        {
            yield return new WaitForSeconds(roundResetDelay);

            if (_playerOneWins >= maxWins || _playerTwoWins >= maxWins)
            {
                _playerOneWins = 0;
                _playerTwoWins = 0;
            }

            RespawnPlayers();
            _roundResetRoutine = null;
        }

        private void RespawnPlayers()
        {
            if (playerOne != null)
            {
                playerOne.SetSpawnPosition(PlayerOneSpawnPoint);
            }

            if (playerTwo != null)
            {
                playerTwo.SetSpawnPosition(PlayerTwoSpawnPoint);
            }
        }

        private void CacheSceneSpawnPoints()
        {
            if (playerOne != null)
            {
                _scenePlayerOneSpawn = playerOne.ConfiguredSpawnWorldPosition;
            }
            else
            {
                _scenePlayerOneSpawn = defaultPlayerOneSpawn;
            }

            if (playerTwo != null)
            {
                _scenePlayerTwoSpawn = playerTwo.ConfiguredSpawnWorldPosition;
            }
            else
            {
                _scenePlayerTwoSpawn = defaultPlayerTwoSpawn;
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

        private Vector2 GetSpawnPoint(int index)
        {
            if (useScenePlayerPositionsAsSpawn)
            {
                if (!Application.isPlaying)
                {
                    PlayerController player = index == 0 ? playerOne : playerTwo;
                    if (player != null)
                    {
                        return player.ConfiguredSpawnWorldPosition;
                    }
                }

                return index == 0 ? _scenePlayerOneSpawn : _scenePlayerTwoSpawn;
            }

            if (arenaDefinition == null || arenaDefinition.spawnPoints == null || arenaDefinition.spawnPoints.Count == 0)
            {
                return index == 0 ? defaultPlayerOneSpawn : defaultPlayerTwoSpawn;
            }

            if (index == 0)
            {
                return arenaDefinition.GetSpawnPoint(0);
            }

            return arenaDefinition.GetSpawnPoint(Mathf.Min(1, arenaDefinition.spawnPoints.Count - 1));
        }

        private Rect GetWrapBounds()
        {
            return arenaDefinition != null ? arenaDefinition.wrapBounds : defaultWrapBounds;
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

            Rect wrapBounds = GetWrapBounds();
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

            if (player.TryGetComponent<Rigidbody2D>(out var body))
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
