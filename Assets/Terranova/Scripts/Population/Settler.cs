using UnityEngine;
using Terranova.Core;
using Terranova.Terrain;

namespace Terranova.Population
{
    /// <summary>
    /// Represents a single settler in the world.
    ///
    /// Story 1.1: Colored capsule standing on terrain.
    /// Story 1.2: Idle wander behavior around the campfire.
    /// Story 1.3: Task system (can receive and hold one task).
    /// Story 1.4: Work cycle (walk to target -> work -> return -> deliver -> repeat).
    /// </summary>
    public class Settler : MonoBehaviour
    {
        // ─── Movement & Idle Settings ────────────────────────────

        private const float IDLE_RADIUS = 8f;
        private const float WALK_SPEED = 1.5f;
        private const float TASK_WALK_SPEED = 2f;       // Faster when on a task
        private const float MIN_PAUSE = 1f;
        private const float MAX_PAUSE = 3.5f;
        private const float ARRIVAL_THRESHOLD = 0.3f;

        // ─── Visual Settings ─────────────────────────────────────

        private static readonly Color[] SETTLER_COLORS =
        {
            new Color(0.85f, 0.25f, 0.25f), // Red
            new Color(0.25f, 0.55f, 0.85f), // Blue
            new Color(0.25f, 0.75f, 0.35f), // Green
            new Color(0.85f, 0.65f, 0.15f), // Orange
            new Color(0.70f, 0.30f, 0.75f), // Purple
        };

        private static Material _sharedMaterial;
        private static readonly int ColorID = Shader.PropertyToID("_BaseColor");

        // ─── State Machine ───────────────────────────────────────

        /// <summary>
        /// All possible settler states. The first two are idle behavior,
        /// the rest form the work cycle (Story 1.3/1.4).
        /// </summary>
        private enum SettlerState
        {
            // Idle behavior (Story 1.2)
            IdlePausing,
            IdleWalking,

            // Work cycle (Story 1.3/1.4)
            WalkingToTarget,    // Moving to work location (tree, rock, site)
            Working,            // At target, performing work (gathering, building)
            ReturningToBase,    // Walking back to campfire/storage
            Delivering          // At base, dropping off resources
        }

        private SettlerState _state = SettlerState.IdlePausing;
        private Vector3 _campfirePosition;
        private Vector3 _walkTarget;
        private float _stateTimer;

        // ─── Task System (Story 1.3) ─────────────────────────────

        private SettlerTask _currentTask;

        /// <summary>The settler's current task, or null if idle.</summary>
        public SettlerTask CurrentTask => _currentTask;

        /// <summary>Whether the settler is currently busy with a task.</summary>
        public bool HasTask => _currentTask != null;

        /// <summary>Current state name (for UI/debug display).</summary>
        public string StateName => _state.ToString();

        // ─── Instance Data ───────────────────────────────────────

        private MaterialPropertyBlock _propBlock;
        private int _colorIndex;

        public int ColorIndex => _colorIndex;

        // ─── Initialization ──────────────────────────────────────

        /// <summary>
        /// Initialize the settler. Called right after instantiation.
        /// </summary>
        public void Initialize(int colorIndex, Vector3 campfirePosition)
        {
            _colorIndex = colorIndex;
            _campfirePosition = campfirePosition;

            CreateVisual();
            SnapToTerrain();

            // Start with random pause (desync settlers)
            _state = SettlerState.IdlePausing;
            _stateTimer = Random.Range(0f, MAX_PAUSE);
        }

        // ─── Task Assignment (Story 1.3) ─────────────────────────

        /// <summary>
        /// Assign a task to this settler. Interrupts idle behavior
        /// and starts the work cycle (Story 1.4).
        /// Returns false if the settler already has a task.
        /// </summary>
        public bool AssignTask(SettlerTask task)
        {
            if (_currentTask != null)
                return false;

            _currentTask = task;
            _state = SettlerState.WalkingToTarget;
            _walkTarget = task.TargetPosition;
            return true;
        }

        /// <summary>
        /// Clear the current task and return to idle.
        /// Called when a task is canceled or can no longer be performed.
        /// </summary>
        public void ClearTask()
        {
            _currentTask = null;
            _state = SettlerState.IdlePausing;
            _stateTimer = Random.Range(MIN_PAUSE, MAX_PAUSE);
        }

        // ─── Update Loop ─────────────────────────────────────────

        private void Update()
        {
            switch (_state)
            {
                case SettlerState.IdlePausing:
                    UpdateIdlePausing();
                    break;
                case SettlerState.IdleWalking:
                    UpdateIdleWalking();
                    break;
                case SettlerState.WalkingToTarget:
                    UpdateWalkingToTarget();
                    break;
                case SettlerState.Working:
                    UpdateWorking();
                    break;
                case SettlerState.ReturningToBase:
                    UpdateReturningToBase();
                    break;
                case SettlerState.Delivering:
                    UpdateDelivering();
                    break;
            }
        }

        // ─── Idle States (Story 1.2) ─────────────────────────────

        private void UpdateIdlePausing()
        {
            _stateTimer -= Time.deltaTime;
            if (_stateTimer > 0f)
                return;

            if (TryPickWalkTarget())
                _state = SettlerState.IdleWalking;
            else
                _stateTimer = 0.5f;
        }

        private void UpdateIdleWalking()
        {
            if (MoveToward(_walkTarget, WALK_SPEED))
            {
                _state = SettlerState.IdlePausing;
                _stateTimer = Random.Range(MIN_PAUSE, MAX_PAUSE);
            }
        }

        // ─── Work Cycle States (Story 1.3/1.4) ──────────────────

        /// <summary>
        /// Walk toward the work target (tree, rock, building site).
        /// If the target becomes invalid, return to idle.
        /// </summary>
        private void UpdateWalkingToTarget()
        {
            if (_currentTask == null || !_currentTask.IsTargetValid)
            {
                ClearTask();
                return;
            }

            if (MoveToward(_currentTask.TargetPosition, TASK_WALK_SPEED))
            {
                // Arrived at work target - start working
                _state = SettlerState.Working;
                _stateTimer = _currentTask.WorkDuration;
            }
        }

        /// <summary>
        /// Perform work at the target location.
        /// For now just a timer. Later: animation, resource depletion.
        /// </summary>
        private void UpdateWorking()
        {
            _stateTimer -= Time.deltaTime;
            if (_stateTimer > 0f)
                return;

            // Work complete - head back to base
            _state = SettlerState.ReturningToBase;
            _walkTarget = _currentTask.BasePosition;
        }

        /// <summary>
        /// Walk back to the campfire/storage with gathered resources.
        /// </summary>
        private void UpdateReturningToBase()
        {
            if (MoveToward(_currentTask.BasePosition, TASK_WALK_SPEED))
            {
                // Arrived at base - deliver resources
                _state = SettlerState.Delivering;
                _stateTimer = 0.5f; // Brief delivery pause
            }
        }

        /// <summary>
        /// Deliver resources at the base. Then either repeat the cycle
        /// (if task is still valid) or return to idle.
        /// </summary>
        private void UpdateDelivering()
        {
            _stateTimer -= Time.deltaTime;
            if (_stateTimer > 0f)
                return;

            // Fire delivery event so UI and economy systems can react
            if (_currentTask != null)
            {
                EventBus.Publish(new ResourceDeliveredEvent
                {
                    TaskType = _currentTask.TaskType,
                    Position = transform.position
                });
            }

            // Repeat cycle if target is still valid, otherwise go idle
            if (_currentTask != null && _currentTask.IsTargetValid)
            {
                _state = SettlerState.WalkingToTarget;
                _walkTarget = _currentTask.TargetPosition;
            }
            else
            {
                ClearTask();
            }
        }

        // ─── Shared Movement ─────────────────────────────────────

        /// <summary>
        /// Move toward a target position with terrain snapping.
        /// Returns true when the target is reached.
        /// </summary>
        private bool MoveToward(Vector3 target, float speed)
        {
            Vector3 pos = transform.position;
            Vector3 direction = target - pos;
            direction.y = 0f;

            float distance = direction.magnitude;

            if (distance < ARRIVAL_THRESHOLD)
                return true;

            Vector3 step = direction.normalized * speed * Time.deltaTime;
            if (step.magnitude > distance)
                step = direction;

            Vector3 newPos = pos + step;

            // Snap Y to terrain
            var world = WorldManager.Instance;
            if (world != null)
            {
                int blockX = Mathf.FloorToInt(newPos.x);
                int blockZ = Mathf.FloorToInt(newPos.z);
                int height = world.GetHeightAtWorldPos(blockX, blockZ);
                VoxelType surface = world.GetSurfaceTypeAtWorldPos(blockX, blockZ);

                if (height < 0 || !surface.IsSolid())
                {
                    // Hit impassable terrain
                    if (_currentTask != null)
                    {
                        // On a task: target may be unreachable, go idle
                        ClearTask();
                    }
                    else
                    {
                        // Idle: just pick a new target
                        _state = SettlerState.IdlePausing;
                        _stateTimer = Random.Range(0.5f, 1f);
                    }
                    return false;
                }

                newPos.y = height + 1f;
            }

            transform.position = newPos;
            return false;
        }

        /// <summary>
        /// Pick a random idle walk target within the campfire radius.
        /// </summary>
        private bool TryPickWalkTarget()
        {
            var world = WorldManager.Instance;
            if (world == null)
                return false;

            for (int attempt = 0; attempt < 5; attempt++)
            {
                float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
                float radius = Random.Range(1f, IDLE_RADIUS);
                float x = _campfirePosition.x + Mathf.Cos(angle) * radius;
                float z = _campfirePosition.z + Mathf.Sin(angle) * radius;

                int blockX = Mathf.FloorToInt(x);
                int blockZ = Mathf.FloorToInt(z);
                int height = world.GetHeightAtWorldPos(blockX, blockZ);
                VoxelType surface = world.GetSurfaceTypeAtWorldPos(blockX, blockZ);

                if (height >= 0 && surface.IsSolid())
                {
                    _walkTarget = new Vector3(x, height + 1f, z);
                    return true;
                }
            }

            return false;
        }

        // ─── Visual Setup ────────────────────────────────────────

        private void CreateVisual()
        {
            var visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            visual.name = "Visual";
            visual.transform.SetParent(transform, false);
            visual.transform.localScale = new Vector3(0.4f, 0.4f, 0.4f);
            visual.transform.localPosition = new Vector3(0f, 0.4f, 0f);

            var collider = visual.GetComponent<Collider>();
            if (collider != null)
                Destroy(collider);

            EnsureSharedMaterial();

            var meshRenderer = visual.GetComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = _sharedMaterial;

            _propBlock = new MaterialPropertyBlock();
            Color color = SETTLER_COLORS[_colorIndex % SETTLER_COLORS.Length];
            _propBlock.SetColor(ColorID, color);
            meshRenderer.SetPropertyBlock(_propBlock);
        }

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
