using UnityEngine;
using Terranova.Terrain;

namespace Terranova.Population
{
    /// <summary>
    /// Represents a single settler in the world.
    ///
    /// Story 1.1: Colored capsule standing on terrain.
    /// Story 1.2: Idle wander behavior around the campfire.
    /// Later: task system (1.3), work cycles (1.4), hunger (5.x).
    /// </summary>
    public class Settler : MonoBehaviour
    {
        // ─── Idle Behavior Settings ──────────────────────────────
        // These could be SerializeFields on a config SO later,
        // but hardcoded is fine for now (no premature abstraction).

        private const float IDLE_RADIUS = 8f;        // Max wander distance from campfire
        private const float WALK_SPEED = 1.5f;        // Blocks per second
        private const float MIN_PAUSE = 1f;            // Minimum pause duration
        private const float MAX_PAUSE = 3.5f;          // Maximum pause duration
        private const float ARRIVAL_THRESHOLD = 0.3f;  // Close enough to target

        // ─── Visual Settings ─────────────────────────────────────

        // 5 distinct colors so settlers are visually distinguishable
        private static readonly Color[] SETTLER_COLORS =
        {
            new Color(0.85f, 0.25f, 0.25f), // Red
            new Color(0.25f, 0.55f, 0.85f), // Blue
            new Color(0.25f, 0.75f, 0.35f), // Green
            new Color(0.85f, 0.65f, 0.15f), // Orange
            new Color(0.70f, 0.30f, 0.75f), // Purple
        };

        // Shared material for all settlers (avoids per-settler material allocations)
        private static Material _sharedMaterial;
        private static readonly int ColorID = Shader.PropertyToID("_BaseColor");

        // ─── Idle State Machine ──────────────────────────────────

        private enum IdleState { Pausing, Walking }

        private IdleState _state = IdleState.Pausing;
        private Vector3 _campfirePosition;
        private Vector3 _walkTarget;
        private float _stateTimer;

        // ─── Instance Data ───────────────────────────────────────

        private MaterialPropertyBlock _propBlock;
        private int _colorIndex;

        /// <summary>
        /// Which color index this settler uses (0-4).
        /// </summary>
        public int ColorIndex => _colorIndex;

        /// <summary>
        /// Initialize the settler's visual appearance and snap to terrain.
        /// Call this right after instantiation.
        /// </summary>
        /// <param name="colorIndex">Index into the color palette (0-4).</param>
        /// <param name="campfirePosition">World position of the campfire (wander center).</param>
        public void Initialize(int colorIndex, Vector3 campfirePosition)
        {
            _colorIndex = colorIndex;
            _campfirePosition = campfirePosition;

            CreateVisual();
            SnapToTerrain();

            // Start each settler with a random pause so they don't all move at once
            _state = IdleState.Pausing;
            _stateTimer = Random.Range(0f, MAX_PAUSE);
        }

        // ─── Update Loop ─────────────────────────────────────────

        private void Update()
        {
            switch (_state)
            {
                case IdleState.Pausing:
                    UpdatePausing();
                    break;
                case IdleState.Walking:
                    UpdateWalking();
                    break;
            }
        }

        /// <summary>
        /// Wait for the pause timer to expire, then pick a new walk target.
        /// </summary>
        private void UpdatePausing()
        {
            _stateTimer -= Time.deltaTime;
            if (_stateTimer > 0f)
                return;

            // Pick a random target within idle radius of the campfire
            if (TryPickWalkTarget())
            {
                _state = IdleState.Walking;
            }
            else
            {
                // Couldn't find valid target, try again next frame
                _stateTimer = 0.5f;
            }
        }

        /// <summary>
        /// Move toward the walk target. On arrival, switch to pausing.
        /// </summary>
        private void UpdateWalking()
        {
            Vector3 pos = transform.position;
            Vector3 direction = _walkTarget - pos;
            direction.y = 0f; // Move horizontally only

            float distance = direction.magnitude;

            if (distance < ARRIVAL_THRESHOLD)
            {
                // Arrived at target - start pausing
                _state = IdleState.Pausing;
                _stateTimer = Random.Range(MIN_PAUSE, MAX_PAUSE);
                return;
            }

            // Move toward target
            Vector3 step = direction.normalized * WALK_SPEED * Time.deltaTime;

            // Don't overshoot
            if (step.magnitude > distance)
                step = direction;

            Vector3 newPos = pos + step;

            // Snap Y to terrain
            int blockX = Mathf.FloorToInt(newPos.x);
            int blockZ = Mathf.FloorToInt(newPos.z);
            var world = WorldManager.Instance;

            if (world != null)
            {
                int height = world.GetHeightAtWorldPos(blockX, blockZ);
                VoxelType surface = world.GetSurfaceTypeAtWorldPos(blockX, blockZ);

                if (height < 0 || !surface.IsSolid())
                {
                    // Hit water or invalid terrain - pick a new target instead
                    _state = IdleState.Pausing;
                    _stateTimer = Random.Range(0.5f, 1f);
                    return;
                }

                newPos.y = height + 1f;
            }

            transform.position = newPos;
        }

        /// <summary>
        /// Pick a random walk target within the idle radius of the campfire.
        /// Validates that the target is on solid, walkable terrain.
        /// Tries up to 5 times to find a valid position.
        /// </summary>
        private bool TryPickWalkTarget()
        {
            var world = WorldManager.Instance;
            if (world == null)
                return false;

            for (int attempt = 0; attempt < 5; attempt++)
            {
                // Random point within circle around campfire
                float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
                float radius = Random.Range(1f, IDLE_RADIUS);
                float x = _campfirePosition.x + Mathf.Cos(angle) * radius;
                float z = _campfirePosition.z + Mathf.Sin(angle) * radius;

                int blockX = Mathf.FloorToInt(x);
                int blockZ = Mathf.FloorToInt(z);
                int height = world.GetHeightAtWorldPos(blockX, blockZ);
                VoxelType surface = world.GetSurfaceTypeAtWorldPos(blockX, blockZ);

                // Must be solid terrain (not water, not out of bounds)
                if (height >= 0 && surface.IsSolid())
                {
                    _walkTarget = new Vector3(x, height + 1f, z);
                    return true;
                }
            }

            return false;
        }

        // ─── Visual Setup ────────────────────────────────────────

        /// <summary>
        /// Build the capsule mesh and apply a unique color.
        /// Uses a shared material with per-instance MaterialPropertyBlock
        /// to avoid material leaks (same pattern as BuildingPlacer).
        /// </summary>
        private void CreateVisual()
        {
            // Create capsule as child object (keeps settler root clean for future components)
            var visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            visual.name = "Visual";
            visual.transform.SetParent(transform, false);

            // Scale: 0.4m wide, 0.8m tall (settler is smaller than a 1m voxel)
            visual.transform.localScale = new Vector3(0.4f, 0.4f, 0.4f);
            // Capsule pivot is at center, so offset up by half height
            visual.transform.localPosition = new Vector3(0f, 0.4f, 0f);

            // Disable capsule collider (settlers don't need physics collision yet)
            var collider = visual.GetComponent<Collider>();
            if (collider != null)
                Destroy(collider);

            // Ensure shared material exists
            EnsureSharedMaterial();

            // Apply unique color via PropertyBlock (no material clone)
            var meshRenderer = visual.GetComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = _sharedMaterial;

            _propBlock = new MaterialPropertyBlock();
            Color color = SETTLER_COLORS[_colorIndex % SETTLER_COLORS.Length];
            _propBlock.SetColor(ColorID, color);
            meshRenderer.SetPropertyBlock(_propBlock);
        }

        /// <summary>
        /// Position the settler on top of the terrain surface.
        /// </summary>
        private void SnapToTerrain()
        {
            var world = WorldManager.Instance;
            if (world == null)
                return;

            int blockX = Mathf.FloorToInt(transform.position.x);
            int blockZ = Mathf.FloorToInt(transform.position.z);
            int height = world.GetHeightAtWorldPos(blockX, blockZ);

            if (height < 0)
            {
                Debug.LogWarning($"Settler at ({blockX}, {blockZ}): no terrain found!");
                return;
            }

            transform.position = new Vector3(
                transform.position.x,
                height + 1f,
                transform.position.z
            );
        }

        /// <summary>
        /// Create the shared material once. All settlers reference this same material.
        /// </summary>
        private static void EnsureSharedMaterial()
        {
            if (_sharedMaterial != null)
                return;

            Shader shader = Shader.Find("Universal Render Pipeline/Lit")
                         ?? Shader.Find("Universal Render Pipeline/Particles/Unlit");

            if (shader == null)
            {
                Debug.LogError("Settler: No URP shader found for settler material.");
                return;
            }

            _sharedMaterial = new Material(shader);
            _sharedMaterial.name = "Settler_Shared (Auto)";
        }
    }
}
