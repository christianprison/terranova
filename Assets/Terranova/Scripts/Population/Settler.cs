using UnityEngine;
using UnityEngine.AI;
using Terranova.Core;
using Terranova.Resources;

namespace Terranova.Population
{
    /// <summary>
    /// Represents a single settler in the world.
    ///
    /// Story 1.1: Colored capsule standing on terrain.
    /// Story 1.2: Idle wander behavior around the campfire.
    /// Story 1.3: Task system (can receive and hold one task).
    /// Story 1.4: Work cycle (walk to target -> work -> return -> deliver -> repeat).
    /// Story 2.0: Movement migrated to NavMesh (replaces block-grid pathfinding).
    /// Story 3.2: Gathering calls to ResourceNode on work completion.
    /// Story 3.3: Visual cargo indicator during transport.
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

        // How far to search for a valid NavMesh point when sampling
        private const float NAV_SAMPLE_RADIUS = 5f;

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

        // ─── NavMesh Agent (Story 2.0) ───────────────────────────

        private NavMeshAgent _agent;
        private bool _isMoving;

        // True if the last completed path actually reached the destination.
        // False when the path was partial or invalid (Story 2.2: obstacle handling).
        private bool _pathReachable;

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

        // ─── Cargo Visual (Story 3.3) ──────────────────────────

        private GameObject _cargoVisual;

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
            SetupNavMeshAgent();

            // Start with random pause (desync settlers)
            _state = SettlerState.IdlePausing;
            _stateTimer = Random.Range(0f, MAX_PAUSE);
        }

        /// <summary>
        /// Configure the NavMeshAgent component and warp to nearest NavMesh position.
        /// Story 2.0: Replaces manual terrain snapping and VoxelPathfinder.
        /// </summary>
        private void SetupNavMeshAgent()
        {
            _agent = gameObject.AddComponent<NavMeshAgent>();
            _agent.speed = WALK_SPEED;
            _agent.angularSpeed = 360f;
            _agent.acceleration = 8f;
            _agent.stoppingDistance = ARRIVAL_THRESHOLD;
            _agent.radius = 0.2f;
            _agent.height = 0.8f;
            _agent.baseOffset = 0f;
            _agent.autoBraking = true;
            _agent.autoRepath = true;

            // Warp to nearest valid NavMesh position (replaces SnapToTerrain)
            if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, NAV_SAMPLE_RADIUS, NavMesh.AllAreas))
            {
                _agent.Warp(hit.position);
            }
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

            if (!SetAgentDestination(task.TargetPosition))
            {
                Debug.Log($"[{name}] REJECTED {task.TaskType} - no path to target");
                _currentTask = null;
                return false;
            }

            _state = SettlerState.WalkingToTarget;
            _agent.speed = TASK_WALK_SPEED;
            Debug.Log($"[{name}] ASSIGNED {task.TaskType} - walking to target");
            return true;
        }

        /// <summary>
        /// Clear the current task and return to idle.
        /// Called when a task is canceled or can no longer be performed.
        /// </summary>
        public void ClearTask()
        {
            // Release resource reservation if we had one (Story 3.2)
            if (_currentTask?.TargetResource != null && _currentTask.TargetResource.IsReserved)
                _currentTask.TargetResource.Release();

            var wasTask = _currentTask?.TaskType;
            _currentTask = null;
            _isMoving = false;
            _agent.ResetPath();
            _agent.speed = WALK_SPEED;
            _state = SettlerState.IdlePausing;
            _stateTimer = Random.Range(MIN_PAUSE, MAX_PAUSE);
            DestroyCargo();
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
            {
                _state = SettlerState.IdleWalking;
                _agent.speed = WALK_SPEED;
            }
            else
                _stateTimer = 0.5f;
        }

        private void UpdateIdleWalking()
        {
            if (HasReachedDestination())
            {
                // Path unreachable is fine for idle – just pause and pick a new spot
                _state = SettlerState.IdlePausing;
                _stateTimer = Random.Range(MIN_PAUSE, MAX_PAUSE);
            }
        }

        // ─── Work Cycle States (Story 1.3/1.4) ──────────────────

        /// <summary>
        /// Walk toward the work target (tree, rock, building site).
        /// If the target becomes invalid or unreachable, return to idle.
        /// Story 2.2: Settler stops gracefully when path is blocked.
        /// </summary>
        private void UpdateWalkingToTarget()
        {
            if (_currentTask == null || !_currentTask.IsTargetValid)
            {
                ClearTask();
                return;
            }

            if (HasReachedDestination())
            {
                if (!_pathReachable)
                {
                    Debug.Log($"[{name}] Target unreachable (blocked by obstacle) - going idle");
                    ClearTask();
                    return;
                }

                _agent.ResetPath();
                _isMoving = false;
                _state = SettlerState.Working;
                _stateTimer = _currentTask.WorkDuration;
                Debug.Log($"[{name}] Arrived at target - WORKING ({_currentTask.TaskType}, {_stateTimer:F1}s)");
            }
        }

        /// <summary>
        /// Perform work at the target location.
        /// Story 3.2: On completion, calls ResourceNode.CompleteGathering().
        /// </summary>
        private void UpdateWorking()
        {
            _stateTimer -= Time.deltaTime;
            if (_stateTimer > 0f)
                return;

            // Complete gathering on the resource node (Story 3.2)
            if (_currentTask?.TargetResource != null)
                _currentTask.TargetResource.CompleteGathering();

            // Show cargo visual during transport (Story 3.3)
            CreateCargo();

            if (!SetAgentDestination(_currentTask.BasePosition))
            {
                Debug.LogWarning($"[{name}] No path back to base - going idle");
                ClearTask();
                return;
            }
            _state = SettlerState.ReturningToBase;
            _agent.speed = TASK_WALK_SPEED;
            Debug.Log($"[{name}] Work done - RETURNING to base");
        }

        /// <summary>
        /// Walk back to the campfire/storage with gathered resources.
        /// Story 2.2: If return path blocked, go idle (don't freeze).
        /// </summary>
        private void UpdateReturningToBase()
        {
            if (HasReachedDestination())
            {
                if (!_pathReachable)
                {
                    Debug.Log($"[{name}] Return path blocked - going idle");
                    ClearTask();
                    return;
                }

                _agent.ResetPath();
                _isMoving = false;
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

            // Remove cargo visual (Story 3.3)
            DestroyCargo();

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
                // Re-reserve the resource for the next cycle (Story 3.2)
                if (_currentTask.TargetResource != null)
                {
                    if (!_currentTask.TargetResource.TryReserve())
                    {
                        Debug.Log($"[{name}] Resource no longer available - going idle");
                        ClearTask();
                        return;
                    }
                }

                if (!SetAgentDestination(_currentTask.TargetPosition))
                {
                    Debug.LogWarning($"[{name}] No path to target for repeat - going idle");
                    ClearTask();
                    return;
                }
                _state = SettlerState.WalkingToTarget;
                _agent.speed = TASK_WALK_SPEED;
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

        // ─── NavMesh Movement (Story 2.0) ────────────────────────

        /// <summary>
        /// Set the NavMeshAgent destination. Samples the target position onto the
        /// NavMesh to handle slight position mismatches.
        /// Returns false if the target is not reachable.
        /// </summary>
        private bool SetAgentDestination(Vector3 target)
        {
            if (!NavMesh.SamplePosition(target, out NavMeshHit hit, NAV_SAMPLE_RADIUS, NavMesh.AllAreas))
            {
                Debug.Log($"[{name}] Target not on NavMesh: ({target.x:F0}, {target.z:F0})");
                return false;
            }

            if (_agent.SetDestination(hit.position))
            {
                _isMoving = true;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Check whether the NavMeshAgent has reached its current destination.
        /// Handles edge cases: path pending, invalid/partial path, not yet moving.
        /// Sets _pathReachable so callers know if the destination was truly reached.
        /// Story 2.2: Settlers stay put when no valid path exists.
        /// </summary>
        private bool HasReachedDestination()
        {
            if (!_isMoving) return true;
            if (_agent.pathPending) return false;

            // Path failed or only partially reachable (Story 2.2)
            if (_agent.pathStatus != NavMeshPathStatus.PathComplete)
            {
                _isMoving = false;
                _pathReachable = false;
                return true;
            }

            if (_agent.remainingDistance <= _agent.stoppingDistance)
            {
                _isMoving = false;
                _pathReachable = true;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Pick a random idle walk target within the campfire radius.
        /// Uses NavMesh.SamplePosition to validate positions. (Story 2.0)
        /// </summary>
        private bool TryPickWalkTarget()
        {
            for (int attempt = 0; attempt < 5; attempt++)
            {
                float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
                float radius = Random.Range(1f, IDLE_RADIUS);
                float x = _campfirePosition.x + Mathf.Cos(angle) * radius;
                float z = _campfirePosition.z + Mathf.Sin(angle) * radius;

                Vector3 candidate = new Vector3(x, _campfirePosition.y, z);

                // Don't pick a target right on the campfire block
                Vector3 toCampfire = candidate - _campfirePosition;
                toCampfire.y = 0f;
                if (toCampfire.magnitude < 1.2f)
                    continue;

                // Validate position is on NavMesh
                if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, NAV_SAMPLE_RADIUS, NavMesh.AllAreas))
                {
                    if (SetAgentDestination(hit.position))
                        return true;
                }
            }

            return false;
        }

        // ─── Cargo Visual (Story 3.3) ──────────────────────────

        /// <summary>
        /// Show a small colored cube above the settler to indicate carried resource.
        /// Brown for wood, gray for stone.
        /// </summary>
        private void CreateCargo()
        {
            if (_cargoVisual != null) return;
            if (_currentTask == null) return;

            _cargoVisual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _cargoVisual.name = "Cargo";
            _cargoVisual.transform.SetParent(transform, false);
            _cargoVisual.transform.localScale = new Vector3(0.2f, 0.2f, 0.2f);
            _cargoVisual.transform.localPosition = new Vector3(0f, 1.0f, 0f);

            // Remove collider
            var col = _cargoVisual.GetComponent<Collider>();
            if (col != null) Destroy(col);

            // Color based on resource type
            Color cargoColor = _currentTask.TaskType switch
            {
                SettlerTaskType.GatherWood => new Color(0.45f, 0.28f, 0.10f),  // Brown
                SettlerTaskType.GatherStone => new Color(0.55f, 0.55f, 0.55f), // Gray
                SettlerTaskType.Hunt => new Color(0.85f, 0.35f, 0.35f),        // Red
                _ => Color.white
            };

            EnsureSharedMaterial();
            var renderer = _cargoVisual.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = _sharedMaterial;
            var block = new MaterialPropertyBlock();
            block.SetColor(ColorID, cargoColor);
            renderer.SetPropertyBlock(block);
        }

        private void DestroyCargo()
        {
            if (_cargoVisual != null)
            {
                Destroy(_cargoVisual);
                _cargoVisual = null;
            }
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
