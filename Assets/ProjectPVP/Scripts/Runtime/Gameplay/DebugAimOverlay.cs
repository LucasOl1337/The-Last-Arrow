using System.Collections.Generic;
using UnityEngine;

namespace ProjectPVP.Gameplay
{
    /// <summary>
    /// Visual debug overlay for the 8-directional aim and ballistic preview system.
    ///
    /// Toggle with  Ctrl + Shift + T  while in Play Mode.
    ///
    /// What is drawn around each living player
    /// ─────────────────────────────────────────
    ///   Grey  lines   → the 8 possible snap directions
    ///   Yellow line   → the direction currently being aimed (snapped)
    ///   Dark-yellow   → ±22° cone edges used for previewing viable 8-way sectors
    ///   Green  line   → line to enemy whose position matches the current snap
    ///                   sector and ballistic lane preview
    ///   Red    line   → line to enemy outside the current snap sector
    ///   Cyan   line   → the snap direction you would need to aim to line up
    ///                   with that enemy's ballistic lane
    ///
    /// The overlay is auto-installed at runtime — no scene changes needed.
    /// It is hidden in the Hierarchy and uses its own GL material so it never
    /// interferes with the game's own rendering.
    /// </summary>
    [AddComponentMenu("")]   // hidden — only ever added programmatically
    public sealed class DebugAimOverlay : MonoBehaviour
    {
        // ── Constants ────────────────────────────────────────────────────────────
        /// <summary>Length of each debug ray in world units.</summary>
        private const float RayLength   = 150f;
        /// <summary>Half-width of one 8-way sector, used only for the visual wedge.</summary>
        private const float AimConeDeg  = 22.5f;
        // Pre-built unit vectors for the 8 compass directions.
        private static readonly Vector2[] k_8Dirs = BuildEightDirs();

        // ── State ─────────────────────────────────────────────────────────────────
        private bool     _visible;
        private Material _mat;
        private readonly List<PlayerController> _playerQueryBuffer = new();

        // ── Auto-install ──────────────────────────────────────────────────────────
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoInstall()
        {
            GameObject go = new GameObject("[DebugAimOverlay]")
            {
                hideFlags = HideFlags.HideInHierarchy,
            };
            DontDestroyOnLoad(go);
            go.AddComponent<DebugAimOverlay>();
        }

        // ── Unity lifecycle ───────────────────────────────────────────────────────
        private void Awake()
        {
            CreateMaterial();
        }

        private void OnEnable()
        {
            Camera.onPostRender += OnCameraPostRender;
        }

        private void OnDisable()
        {
            Camera.onPostRender -= OnCameraPostRender;
        }

        private void Update()
        {
            bool ctrl  = UnityEngine.Input.GetKey(KeyCode.LeftControl)  || UnityEngine.Input.GetKey(KeyCode.RightControl);
            bool shift = UnityEngine.Input.GetKey(KeyCode.LeftShift)    || UnityEngine.Input.GetKey(KeyCode.RightShift);
            bool t     = UnityEngine.Input.GetKeyDown(KeyCode.T);

            if (ctrl && shift && t)
            {
                _visible = !_visible;
                Debug.Log($"[DebugAimOverlay] {(_visible ? "ATIVADO" : "DESATIVADO")}");
            }
        }

        // ── Rendering ─────────────────────────────────────────────────────────────
        private void OnCameraPostRender(Camera cam)
        {
            if (!_visible || _mat == null)
            {
                return;
            }

            PlayerController.CopyActivePlayers(_playerQueryBuffer);
            if (_playerQueryBuffer.Count == 0)
            {
                PlayerController[] players =
                    Object.FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
                for (int index = 0; index < players.Length; index += 1)
                {
                    if (players[index] != null)
                    {
                        _playerQueryBuffer.Add(players[index]);
                    }
                }
            }

            if (_playerQueryBuffer.Count == 0)
            {
                return;
            }

            _mat.SetPass(0);

            GL.PushMatrix();
            GL.LoadProjectionMatrix(cam.projectionMatrix);
            GL.modelview = cam.worldToCameraMatrix;

            foreach (PlayerController player in _playerQueryBuffer)
            {
                if (player == null || player.IsDead)
                {
                    continue;
                }

                DrawForPlayer(player, _playerQueryBuffer);
            }

            GL.PopMatrix();
            _playerQueryBuffer.Clear();
        }

        // ── Per-player drawing ────────────────────────────────────────────────────
        private static void DrawForPlayer(PlayerController player, IReadOnlyList<PlayerController> all)
        {
            Vector2 origin = player.RootPosition;

            // Prefer the live frame aim (updates in real-time as the player moves the stick
            // or presses WASD/D-pad) so the overlay reacts immediately without needing to
            // hold the shoot button.  Fall back to the last locked hold direction when no
            // live input is present.
            Vector2 liveAim = player.InputSource != null ? player.InputSource.CurrentFrame.aim : player.CurrentInputFrame.aim;
            bool liveHasAim = liveAim.sqrMagnitude > 0.01f;
            bool aimHoldActive = player.IsAimHoldActive;
            Vector2 rawAim = liveHasAim
                ? liveAim
                : (aimHoldActive ? player.AimHoldDirection : Vector2.zero);
            bool hasAim = rawAim.sqrMagnitude > 0.01f;

            // Snap the raw aim to 8 directions exactly as the combat system does.
            Vector2 aimDir8 = hasAim
                ? PlayerMovementSystem.Snap8Dir(rawAim)
                : new Vector2(player.Facing >= 0 ? 1f : -1f, 0f);

            // ── Grey: 8 possible snap directions ──────────────────────────────────
            DrawLines(new Color(0.55f, 0.55f, 0.55f, 0.35f), () =>
            {
                foreach (Vector2 d in k_8Dirs)
                {
                    GL.Vertex((Vector3)origin);
                    GL.Vertex((Vector3)(origin + d * RayLength));
                }
            });

            // ── Dark-yellow: cone boundary edges (±22°) ───────────────────────────
            float   baseAngle = Mathf.Atan2(aimDir8.y, aimDir8.x);
            float   coneRad   = AimConeDeg * Mathf.Deg2Rad;
            Vector2 edgeL     = AngleToDir(baseAngle - coneRad);
            Vector2 edgeR     = AngleToDir(baseAngle + coneRad);

            DrawLines(new Color(0.85f, 0.75f, 0f, 0.55f), () =>
            {
                GL.Vertex((Vector3)origin);
                GL.Vertex((Vector3)(origin + edgeL * RayLength));
                GL.Vertex((Vector3)origin);
                GL.Vertex((Vector3)(origin + edgeR * RayLength));
            });

            // ── White: raw aim direction before snapping (updates smoothly) ──────
            // This line moves in real-time with the mouse / stick, making it easy
            // to confirm that the input is being read even within the same 45° sector.
            if (hasAim)
            {
                DrawLines(new Color(1f, 1f, 1f, 0.60f), () =>
                {
                    GL.Vertex((Vector3)origin);
                    GL.Vertex((Vector3)(origin + rawAim.normalized * RayLength * 1.1f));
                });
            }

            // ── Bright yellow: snapped direction (one of the 8 compass points) ────
            DrawLines(new Color(1f, 0.95f, 0f, 1f), () =>
            {
                GL.Vertex((Vector3)origin);
                GL.Vertex((Vector3)(origin + aimDir8 * RayLength * 1.35f));
            });

            // ── Per enemy ─────────────────────────────────────────────────────────
            foreach (PlayerController other in all)
            {
                if (other == null || other == player || other.IsDead)
                {
                    continue;
                }

                // Use the same aim-point the combat system uses.
                Vector2 enemyPoint = PlayerAnchorSystem.ResolveCombatantAimPoint(other);
                Vector2 toEnemy    = enemyPoint - origin;
                float   dist       = toEnemy.magnitude;
                if (dist < 0.1f)
                {
                    continue;
                }

                Vector2 toEnemyN = toEnemy / dist;
                Vector2 bestSnap = BestSnapToward(toEnemyN);
                if (TryResolveRequiredSector(player, origin, enemyPoint, out Vector2 resolvedSector))
                {
                    bestSnap = resolvedSector;
                }
                bool sectorMatch = hasAim && Vector2.Dot(aimDir8.normalized, bestSnap.normalized) >= 0.999f;

                // Green only if the current 1-of-8 direction matches the previewed sector.
                Color lineColor = sectorMatch
                    ? new Color(0.15f, 1f, 0.3f,  0.80f)
                    : new Color(1f,    0.2f, 0.1f, 0.55f);

                DrawLines(lineColor, () =>
                {
                    GL.Vertex((Vector3)origin);
                    GL.Vertex((Vector3)enemyPoint);
                });

                // ── Cyan: the snap direction you NEED to aim to line up with the lane ────
                DrawLines(new Color(0.1f, 0.85f, 1f, 0.80f), () =>
                {
                    GL.Vertex((Vector3)origin);
                    GL.Vertex((Vector3)(origin + bestSnap * RayLength * 0.75f));
                });
            }
        }

        // ── Helpers ───────────────────────────────────────────────────────────────
        private static void DrawLines(Color color, System.Action body)
        {
            GL.Begin(GL.LINES);
            GL.Color(color);
            body();
            GL.End();
        }

        private static Vector2 BestSnapToward(Vector2 dir)
        {
            Vector2 best    = k_8Dirs[0];
            float   bestDot = Vector2.Dot(dir, best);

            for (int i = 1; i < k_8Dirs.Length; i++)
            {
                float dot = Vector2.Dot(dir, k_8Dirs[i]);
                if (dot > bestDot)
                {
                    bestDot = dot;
                    best    = k_8Dirs[i];
                }
            }

            return best;
        }

        private static bool TryResolveRequiredSector(PlayerController player, Vector2 origin, Vector2 target, out Vector2 requiredSector)
        {
            requiredSector = Vector2.zero;
            if (player == null)
            {
                return false;
            }

            float baseSpeed = player.ProjectileBaseSpeed;
            float gravity = player.characterDefinition != null ? player.characterDefinition.projectileGravity : 1500f;
            float inheritFactor = player.ProjectileInheritVelocityFactor;
            Vector2 inheritedVelocity = player.CurrentVelocity * inheritFactor;
            if (!ProjectileTrajectoryMath.TryResolvePreferredTravelDirection(
                    origin,
                    target,
                    baseSpeed,
                    gravity,
                    inheritedVelocity,
                    player.groundMask,
                    out Vector2 preferredDirection))
            {
                return false;
            }

            requiredSector = PlayerMovementSystem.Snap8Dir(preferredDirection);
            return requiredSector.sqrMagnitude > 0.01f;
        }

        private static Vector2 AngleToDir(float rad)
        {
            return new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
        }

        private static Vector2[] BuildEightDirs()
        {
            Vector2[] dirs = new Vector2[8];
            for (int i = 0; i < 8; i++)
            {
                float rad = i * 45f * Mathf.Deg2Rad;
                dirs[i]   = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
            }
            return dirs;
        }

        private void CreateMaterial()
        {
            Shader shader = Shader.Find("Hidden/Internal-Colored")
                         ?? Shader.Find("Sprites/Default");

            if (shader == null)
            {
                Debug.LogWarning("[DebugAimOverlay] Não encontrou shader adequado — overlay desativado.");
                enabled = false;
                return;
            }

            _mat = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            _mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            _mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            _mat.SetInt("_Cull",     (int)UnityEngine.Rendering.CullMode.Off);
            _mat.SetInt("_ZWrite",   0);
        }
    }
}
