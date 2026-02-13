using System.Collections.Generic;
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
        private float _stateTimer;

        // ─── Pathfinding (Story 2.1) ───────────────────────────

        private List<Vector3> _currentPath = new List<Vector3>();
        private int _pathIndex;

        // ─── Task System (Story 1.3) ─────────────────────────────

        // Simple delivery counter (placeholder until economy system in Feature 3.x)
        private static int _totalWoodDelivered;
        private static int _totalStoneDelivered;
        private static int _totalFoodDelivered;

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
            {
                Debug.Log($"[{name}] REJECTED task {task.TaskType} - already has {_currentTask.TaskType}");
                return false;
            }

            _currentTask = task;

            if (!ComputePath(task.TargetPosition))
            {
                Debug.Log($"[{name}] REJECTED {task.TaskType} - no path to target");
                _currentTask = null;
                return false;
            }

            _state = SettlerState.WalkingToTarget;
            Debug.Log($"[{name}] ASSIGNED {task.TaskType} - walking to target");
            return true;
        }

        /// <summary>
        /// Clear the current task and return to idle.
        /// Called when a task is canceled or can no longer be performed.
        /// </summary>
        public void ClearTask()
        {
            var wasTask = _currentTask?.TaskType;
            _currentTask = null;
            _state = SettlerState.IdlePausing;
            _stateTimer = Random.Range(MIN_PAUSE, MAX_PAUSE);
            Debug.Log($"[{name}] Task ended ({wasTask}) - returning to IDLE");
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
            if (FollowPath(WALK_SPEED))
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

            if (FollowPath(TASK_WALK_SPEED))
            {
                _state = SettlerState.Working;
                _stateTimer = _currentTask.WorkDuration;
                Debug.Log($"[{name}] Arrived at target - WORKING ({_currentTask.TaskType}, {_stateTimer:F1}s)");
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

            if (!ComputePath(_currentTask.BasePosition))
            {
                Debug.LogWarning($"[{name}] No path back to base - going idle");
                ClearTask();
                return;
            }
            _state = SettlerState.ReturningToBase;
            Debug.Log($"[{name}] Work done - RETURNING to base");
        }

        /// <summary>
        /// Walk back to the campfire/storage with gathered resources.
        /// </summary>
        private void UpdateReturningToBase()
        {
            if (FollowPath(TASK_WALK_SPEED))
            {
                _state = SettlerState.Delivering;
                _stateTimer = 0.5f;
                Debug.Log($"[{name}] Arrived at base - DELIVERING {_currentTask.TaskType}");
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

            // Deliver resources and log to console
            if (_currentTask != null)
            {
                string resourceName = TrackDelivery(_currentTask.TaskType);

                EventBus.Publish(new ResourceDeliveredEvent
                {
                    TaskType = _currentTask.TaskType,
                    Position = transform.position
                });

                Debug.Log($"[{name}] DELIVERED {resourceName} " +
                          $"(totals: Wood={_totalWoodDelivered}, Stone={_totalStoneDelivered}, Food={_totalFoodDelivered})");
            }

            if (_currentTask != null && _currentTask.IsTargetValid)
            {
                if (!ComputePath(_currentTask.TargetPosition))
                {
                    Debug.LogWarning($"[{name}] No path to target for repeat - going idle");
                    ClearTask();
                    return;
                }
                _state = SettlerState.WalkingToTarget;
                Debug.Log($"[{name}] REPEATING cycle ({_currentTask.TaskType})");
            }
            else
            {
                Debug.Log($"[{name}] Target no longer valid - going idle");
                ClearTask();
            }
        }

        /// <summary>
        /// Increment delivery counters. Placeholder until economy system (Feature 3.x).
        /// </summary>
        private static string TrackDelivery(SettlerTaskType taskType)
        {
            switch (taskType)
            {
                case SettlerTaskType.GatherWood:
                    _totalWoodDelivered++;
                    return "1x Wood";
                case SettlerTaskType.GatherStone:
                    _totalStoneDelivered++;
                    return "1x Stone";
                case SettlerTaskType.Hunt:
                    _totalFoodDelivered++;
                    return "1x Food";
                default:
                    return $"1x {taskType}";
            }
        }

        // ─── Pathfinding (Story 2.1) ───────────────────────────

        /// <summary>
        /// Compute an A* path from the current position to the destination.
        /// Stores result in _currentPath/_pathIndex.
        /// Returns false if no path exists.
        /// </summary>
        private bool ComputePath(Vector3 destination)
        {
            var world = WorldManager.Instance;
            if (world == null) return false;

            Vector2Int start = new Vector2Int(
                Mathf.FloorToInt(transform.position.x),
                Mathf.FloorToInt(transform.position.z));
            Vector2Int end = new Vector2Int(
                Mathf.FloorToInt(destination.x),
                Mathf.FloorToInt(destination.z));

            _currentPath = VoxelPathfinder.FindPath(world, start, end);
            _pathIndex = 0;

            if (_currentPath.Count == 0 && start != end)
            {
                Debug.Log($"[{name}] No path from ({start.x},{start.y}) to ({end.x},{end.y})");
                return false;
            }

            Debug.Log($"[{name}] Path: {_currentPath.Count} waypoints to ({end.x},{end.y})");
            return true;
        }

        /// <summary>
        /// Advance along the computed path. Returns true when final
        /// waypoint has been reached (or path is empty = already there).
        /// </summary>
        private bool FollowPath(float speed)
        {
            if (_pathIndex >= _currentPath.Count)
                return true; // Already at destination

            if (MoveToward(_currentPath[_pathIndex], speed))
            {
                _pathIndex++;
                if (_pathIndex >= _currentPath.Count)
                    return true; // Reached final waypoint
            }
            return false;
        }

        // ─── Shared Movement ─────────────────────────────────────

        /// <summary>
        /// Move toward a single waypoint with terrain snapping.
        /// Returns true when the waypoint is reached.
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

            // Snap Y to smooth mesh surface (Story 0.6: objects on mesh surface)
            var world = WorldManager.Instance;
            if (world != null)
            {
                newPos.y = world.GetSmoothedHeightAtWorldPos(newPos.x, newPos.z);
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

                if (!VoxelPathfinder.IsWalkable(world, blockX, blockZ))
                    continue;

                // Don't pick a target right on the campfire block
                Vector3 candidate = new Vector3(x, 0f, z);
                Vector3 toCampfire = candidate - _campfirePosition;
                toCampfire.y = 0f;
                if (toCampfire.magnitude < 1.2f)
                    continue;

                if (ComputePath(candidate))
                    return true;
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

        /// <summary>
        /// Snap settler Y position to the smooth mesh surface.
        /// Story 0.6: Bestehende Objekte auf Mesh-Oberfläche
        /// </summary>
        private void SnapToTerrain()
        {
            var world = WorldManager.Instance;
            if (world == null)
                return;

            transform.position = new Vector3(
                transform.position.x,
                world.GetSmoothedHeightAtWorldPos(transform.position.x, transform.position.z),
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
