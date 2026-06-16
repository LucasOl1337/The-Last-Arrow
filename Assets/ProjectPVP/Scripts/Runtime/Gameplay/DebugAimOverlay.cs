using System.Collections.Generic;
using UnityEngine;

namespace ProjectPVP.Gameplay
{
    /// <summary>
    /// Visual debug overlay for the 8-directional aim and ballistic-assist system.
    ///
    /// Toggle with  Ctrl + Shift + T  while in Play Mode.
    ///
    /// What is drawn around each living player
    /// ─────────────────────────────────────────
    ///   Grey  lines   → the 8 possible snap directions
    ///   Yellow line   → the direction currently being aimed (snapped)
    ///   Dark-yellow   → ±22° cone edges (assist activates inside this cone)
    ///   Green  line   → line to enemy whose position is inside the aimed cone
    ///                   (ballistic assist WILL fire toward them)
    ///   Red    line   → line to enemy outside the cone (raw shot, no assist)
    ///   Cyan   line   → the snap direction you would need to aim to activate
    ///                   the assist toward that enemy
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
        private const float ElevatedTargetBiasHeight = 24f;

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
                bool assistEnabled = player.characterDefinition != null
                    ? player.characterDefinition.projectileAssistEnabled
                    : false;
                bool sectorMatch = hasAim && assistEnabled && Vector2.Dot(aimDir8.normalized, bestSnap.normalized) >= 0.999f;

                // Green only if the current 1-of-8 direction matches and assist is enabled.
                Color lineColor = sectorMatch
                    ? new Color(0.15f, 1f, 0.3f,  0.80f)
                    : new Color(1f,    0.2f, 0.1f, 0.55f);

                DrawLines(lineColor, () =>
                {
                    GL.Vertex((Vector3)origin);
                    GL.Vertex((Vector3)enemyPoint);
                });

                // ── Cyan: the snap direction you NEED to aim to activate assist ────
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

            float baseSpeed = player.characterDefinition != null ? player.characterDefinition.projectileBaseSpeed : 1600f;
            float gravity = player.characterDefinition != null ? player.characterDefinition.projectileGravity : 1500f;
            if (!TrySolveBallisticArc(origin, target, baseSpeed, gravity, out Vector2 lowArc, out Vector2 highArc))
            {
                return false;
            }

            float inheritFactor = player.characterDefinition != null ? player.characterDefinition.projectileInheritVelocityFactor : 1f;
            Vector2 inheritedVelocity = player.CurrentVelocity * inheritFactor;
            bool lowClear = IsBallisticPathClear(player, origin, target, lowArc, baseSpeed, gravity, inheritedVelocity);
            bool highClear = IsBallisticPathClear(player, origin, target, highArc, baseSpeed, gravity, inheritedVelocity);
            bool favorHighArc = target.y - origin.y > ElevatedTargetBiasHeight;

            Vector2 preferredDirection;
            if (lowClear && highClear)
            {
                preferredDirection = favorHighArc ? highArc : lowArc;
            }
            else if (highClear)
            {
                preferredDirection = highArc;
            }
            else if (lowClear)
            {
                preferredDirection = lowArc;
            }
            else
            {
                preferredDirection = favorHighArc ? highArc : lowArc;
            }

            requiredSector = PlayerMovementSystem.Snap8Dir(preferredDirection);
            return requiredSector.sqrMagnitude > 0.01f;
        }

        private static bool IsBallisticPathClear(PlayerController player, Vector2 origin, Vector2 target, Vector2 launchDirection, float baseSpeed, float gravity, Vector2 inheritedVelocity)
        {
            float initialSpeed = baseSpeed + Mathf.Max(0f, Vector2.Dot(inheritedVelocity, launchDirection.normalized));
            if (initialSpeed <= 0.01f)
            {
                return false;
            }

            const int sampleCount = 24;
            const float targetRadius = 24f;
            float estimatedFlightTime = ResolveEstimatedFlightTime(origin, target, launchDirection.normalized, initialSpeed);
            Vector2 previous = origin;

            for (int step = 1; step <= sampleCount; step += 1)
            {
                float t = estimatedFlightTime * (step / (float)sampleCount);
                Vector2 current = origin
                    + (launchDirection.normalized * initialSpeed * t)
                    + (Vector2.down * (0.5f * gravity * t * t));

                if (Physics2D.Linecast(previous, current, player.groundMask))
                {
                    return false;
                }

                if ((current - target).sqrMagnitude <= targetRadius * targetRadius)
                {
                    return true;
                }

                previous = current;
            }

            return (previous - target).sqrMagnitude <= targetRadius * targetRadius;
        }

        private static float ResolveEstimatedFlightTime(Vector2 origin, Vector2 target, Vector2 direction, float speed)
        {
            float horizontalSpeed = direction.x * speed;
            float dx = target.x - origin.x;
            if (Mathf.Abs(horizontalSpeed) > 0.01f)
            {
                float time = dx / horizontalSpeed;
                if (time > 0f)
                {
                    return Mathf.Clamp(time, 0.05f, 2.5f);
                }
            }

            return Mathf.Clamp(Vector2.Distance(origin, target) / Mathf.Max(speed, 0.01f), 0.05f, 2.5f);
        }

        private static bool TrySolveBallisticArc(Vector2 origin, Vector2 target, float speed, float gravity, out Vector2 lowArcDir, out Vector2 highArcDir)
        {
            lowArcDir = highArcDir = Vector2.zero;

            if (speed < 0.1f)
            {
                Vector2 fallback = (target - origin).normalized;
                lowArcDir = fallback;
                highArcDir = fallback;
                return true;
            }

            float dx = target.x - origin.x;
            float dy = target.y - origin.y;
            if (Mathf.Abs(dx) < 1f)
            {
                Vector2 direct = (target - origin).normalized;
                lowArcDir = direct;
                highArcDir = direct;
                return true;
            }

            float speedSq = speed * speed;
            float a = gravity * dx * dx / (2f * speedSq);
            if (Mathf.Abs(a) < 0.0001f)
            {
                return false;
            }

            float b = -dx;
            float c = dy + a;
            float discriminant = b * b - (4f * a * c);
            if (discriminant < 0f)
            {
                return false;
            }

            float sqrtDiscriminant = Mathf.Sqrt(discriminant);
            float tanA = (-b + sqrtDiscriminant) / (2f * a);
            float tanB = (-b - sqrtDiscriminant) / (2f * a);

            Vector2 TanToDirection(float tanValue)
            {
                float cos = 1f / Mathf.Sqrt(1f + (tanValue * tanValue));
                float sin = tanValue * cos;
                return new Vector2(cos * (dx >= 0f ? 1f : -1f), sin).normalized;
            }

            Vector2 first = TanToDirection(tanA);
            Vector2 second = TanToDirection(tanB);
            if (Mathf.Abs(tanA) <= Mathf.Abs(tanB))
            {
                lowArcDir = first;
                highArcDir = second;
            }
            else
            {
                lowArcDir = second;
                highArcDir = first;
            }

            return true;
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
