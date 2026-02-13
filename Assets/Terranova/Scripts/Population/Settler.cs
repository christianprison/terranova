using UnityEngine;
using UnityEngine.AI;
using Terranova.Core;
using Terranova.Buildings;
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

        // ─── Hunger Settings (Story 5.1) ────────────────────────

        private const float MAX_HUNGER = 100f;
        private const float HUNGER_RATE = 0.55f;            // Per second (~3 min to starve at 1x)
        private const float HUNGER_EAT_THRESHOLD = 30f;     // Seek food below this %
        private const float HUNGER_SLOW_THRESHOLD = 25f;    // Speed penalty below this %
        private const float HUNGER_SPEED_PENALTY = 0.5f;    // Half speed when very hungry
        private const float STARVATION_GRACE = 30f;          // Seconds at 0 before death
        private const float EAT_DURATION = 1.5f;             // How long eating takes

        // ─── Visual Settings ─────────────────────────────────────

        private static readonly Color DEFAULT_COLOR = Color.white;
        private static readonly Color WOODCUTTER_COLOR = new Color(0.55f, 0.33f, 0.14f); // Brown
        private static readonly Color HUNTER_COLOR = new Color(0.20f, 0.65f, 0.20f);     // Green
        private static readonly Color HUNGRY_COLOR = new Color(0.90f, 0.30f, 0.30f);     // Red (hungry)

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
            Delivering,         // At base, dropping off resources

            // Hunger (Story 5.1/5.2)
            WalkingToEat,       // Going to campfire because hungry
            Eating              // At campfire, consuming food
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

        /// <summary>Whether the settler is currently busy (has task or is eating with saved task).</summary>
        public bool HasTask => _currentTask != null || _savedTask != null;

        /// <summary>Current state name (for UI/debug display).</summary>
        public string StateName => _state.ToString();

        // ─── Hunger System (Story 5.1) ─────────────────────────

        private float _hunger = MAX_HUNGER;
        private float _starvationTimer;
        private bool _isStarving;
        private SettlerTask _savedTask;       // Task saved while eating

        /// <summary>Current hunger value (0 = starving, 100 = full).</summary>
        public float Hunger => _hunger;
        /// <summary>Hunger as percentage (0.0–1.0).</summary>
        public float HungerPercent => _hunger / MAX_HUNGER;
        /// <summary>True when hunger is 0 and grace period is ticking.</summary>
        public bool IsStarving => _isStarving;

        // ─── Cargo Visual (Story 3.3) ──────────────────────────

        private GameObject _cargoVisual;

        // ─── Instance Data ───────────────────────────────────────

        private MaterialPropertyBlock _propBlock;
        private MeshRenderer _visualRenderer;

        // ─── Initialization ──────────────────────────────────────

        /// <summary>
        /// Initialize the settler. Called right after instantiation.
        /// </summary>
        public void Initialize(int colorIndex, Vector3 campfirePosition)
        {
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
            _agent.speed = TASK_WALK_SPEED * task.SpeedMultiplier;
            UpdateVisualColor();
            Debug.Log($"[{name}] ASSIGNED {task.TaskType} - walking to target (speed x{task.SpeedMultiplier})");
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

            // Release construction reservation if we had one (Story 4.2)
            if (_currentTask?.TargetBuilding != null && _currentTask.TargetBuilding.IsBeingBuilt)
                _currentTask.TargetBuilding.ReleaseConstruction();

            var wasTask = _currentTask?.TaskType;
            _currentTask = null;
            _isMoving = false;
            _agent.ResetPath();
            _agent.speed = WALK_SPEED;
            _state = SettlerState.IdlePausing;
            _stateTimer = Random.Range(MIN_PAUSE, MAX_PAUSE);
            DestroyCargo();
            UpdateVisualColor();
            Debug.Log($"[{name}] Task ended ({wasTask}) - returning to IDLE");
        }

        // ─── Update Loop ─────────────────────────────────────────

        private void Update()
        {
            // Decrease hunger every frame (scales with Time.timeScale automatically)
            UpdateHunger();

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
                case SettlerState.WalkingToEat:
                    UpdateWalkingToEat();
                    break;
                case SettlerState.Eating:
                    UpdateEating();
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
                _agent.speed = _hunger < HUNGER_SLOW_THRESHOLD
                    ? WALK_SPEED * HUNGER_SPEED_PENALTY : WALK_SPEED;
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
        /// Story 4.2: Build tasks complete construction and go idle (no delivery).
        /// </summary>
        private void UpdateWorking()
        {
            _stateTimer -= Time.deltaTime;
            if (_stateTimer > 0f)
                return;

            // Story 4.2: Build tasks complete the construction and return to idle
            if (_currentTask?.TaskType == SettlerTaskType.Build)
            {
                if (_currentTask.TargetBuilding != null)
                    _currentTask.TargetBuilding.CompleteConstruction();

                Debug.Log($"[{name}] Construction complete - going idle");
                _currentTask = null; // Don't call ClearTask – construction is already released
                _isMoving = false;
                _agent.ResetPath();
                _agent.speed = WALK_SPEED;
                _state = SettlerState.IdlePausing;
                _stateTimer = Random.Range(MIN_PAUSE, MAX_PAUSE);
                UpdateVisualColor();
                return;
            }

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
            _agent.speed = TASK_WALK_SPEED * _currentTask.SpeedMultiplier;
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

            // Re-evaluate priorities: construction sites take precedence over gathering
            // BUT specialized workers stay at their building — they don't get reassigned
            if (_currentTask != null && !_currentTask.IsSpecialized
                && _currentTask.TaskType != SettlerTaskType.Build
                && HasPendingConstructionSite())
            {
                Debug.Log($"[{name}] Construction site waiting - dropping gather task");
                ClearTask();
                return;
            }

            if (_currentTask != null && _currentTask.IsTargetValid)
            {
                // Re-reserve the resource for the next cycle (Story 3.2)
                if (_currentTask.TargetResource != null)
                {
                    if (!_currentTask.TargetResource.TryReserve())
                    {
                        // Specialized workers search for a new resource instead of going idle
                        if (_currentTask.IsSpecialized && TryFindNewResource())
                            return;

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
                _agent.speed = TASK_WALK_SPEED * _currentTask.SpeedMultiplier;
                Debug.Log($"[{name}] REPEATING cycle ({_currentTask.TaskType})");
            }
            else
            {
                // Specialized workers search for a new resource instead of going idle
                if (_currentTask != null && _currentTask.IsSpecialized && TryFindNewResource())
                    return;

                Debug.Log($"[{name}] Target no longer valid - going idle");
                ClearTask();
            }
        }

        // ─── Hunger System (Story 5.1/5.2) ──────────────────────

        /// <summary>
        /// Tick hunger down, check for starvation, and trigger eating when hungry.
        /// Called every frame before the state machine.
        /// </summary>
        private void UpdateHunger()
        {
            // Decrease hunger
            if (_hunger > 0f)
            {
                _hunger -= HUNGER_RATE * Time.deltaTime;
                if (_hunger < 0f) _hunger = 0f;
            }

            // Starvation: grace period before death
            if (_hunger <= 0f)
            {
                if (!_isStarving)
                {
                    _isStarving = true;
                    _starvationTimer = STARVATION_GRACE;
                    UpdateVisualColor();
                    Debug.Log($"[{name}] STARVING - grace period {STARVATION_GRACE}s");
                }

                _starvationTimer -= Time.deltaTime;
                if (_starvationTimer <= 0f)
                {
                    Die();
                    return;
                }
            }

            // Check if should eat (only interrupt safe states)
            if (_hunger < HUNGER_EAT_THRESHOLD
                && _state != SettlerState.WalkingToEat
                && _state != SettlerState.Eating
                && _state != SettlerState.ReturningToBase
                && _state != SettlerState.Delivering)
            {
                StartEating();
            }
        }

        /// <summary>
        /// Interrupt current activity and walk to campfire to eat.
        /// Saves the current task so it can be resumed after eating.
        /// Story 5.2: Nahrungsaufnahme
        /// </summary>
        private void StartEating()
        {
            // Save task for later (don't release reservations)
            if (_currentTask != null && _savedTask == null)
            {
                _savedTask = _currentTask;
                _currentTask = null;
            }

            _isMoving = false;
            _agent.ResetPath();
            DestroyCargo();

            if (!SetAgentDestination(_campfirePosition))
            {
                // Can't reach campfire — stay put and hope
                _state = SettlerState.IdlePausing;
                _stateTimer = 2f;
                return;
            }

            float speed = TASK_WALK_SPEED;
            if (_hunger < HUNGER_SLOW_THRESHOLD)
                speed *= HUNGER_SPEED_PENALTY;
            _agent.speed = speed;

            _state = SettlerState.WalkingToEat;
            Debug.Log($"[{name}] HUNGRY ({_hunger:F0}%) - walking to campfire to eat");
        }

        /// <summary>Walk to campfire to eat. Story 5.2.</summary>
        private void UpdateWalkingToEat()
        {
            if (HasReachedDestination())
            {
                _agent.ResetPath();
                _isMoving = false;
                _state = SettlerState.Eating;
                _stateTimer = EAT_DURATION;
            }
        }

        /// <summary>
        /// Try to consume food at the campfire.
        /// If food is available: hunger restored, resume previous task.
        /// If not: return hungry, try again later.
        /// Story 5.2: Nahrungsaufnahme
        /// </summary>
        private void UpdateEating()
        {
            _stateTimer -= Time.deltaTime;
            if (_stateTimer > 0f) return;

            var rm = ResourceManager.Instance;
            if (rm != null && rm.TryConsumeFood())
            {
                _hunger = MAX_HUNGER;
                _isStarving = false;
                Debug.Log($"[{name}] ATE food - hunger restored to {_hunger:F0}%");
            }
            else
            {
                Debug.Log($"[{name}] No food available at campfire!");
            }

            UpdateVisualColor();
            ResumeAfterEating();
        }

        /// <summary>
        /// After eating (or failing to eat), resume the saved task or go idle.
        /// </summary>
        private void ResumeAfterEating()
        {
            if (_savedTask != null)
            {
                var task = _savedTask;
                _savedTask = null;

                // Check if the saved task is still valid
                if (task.IsTargetValid)
                {
                    _currentTask = task;

                    if (SetAgentDestination(task.TargetPosition))
                    {
                        _state = SettlerState.WalkingToTarget;
                        float speed = TASK_WALK_SPEED * task.SpeedMultiplier;
                        if (_hunger < HUNGER_SLOW_THRESHOLD)
                            speed *= HUNGER_SPEED_PENALTY;
                        _agent.speed = speed;
                        UpdateVisualColor();
                        Debug.Log($"[{name}] Resuming {task.TaskType} after eating");
                        return;
                    }
                }

                // Task no longer valid — release reservations
                if (task.TargetResource != null && task.TargetResource.IsReserved)
                    task.TargetResource.Release();
                if (task.TargetBuilding != null && task.TargetBuilding.IsBeingBuilt)
                    task.TargetBuilding.ReleaseConstruction();
            }

            // No saved task or it's invalid — go idle
            _currentTask = null;
            _agent.speed = WALK_SPEED;
            _state = SettlerState.IdlePausing;
            _stateTimer = Random.Range(MIN_PAUSE, MAX_PAUSE);
            UpdateVisualColor();
        }

        /// <summary>
        /// Settler dies from starvation. Cleans up task, publishes event, destroys.
        /// Story 5.4: Tod
        /// </summary>
        private void Die()
        {
            Debug.Log($"[{name}] DIED of starvation!");

            // Release any held resources
            if (_currentTask?.TargetResource != null && _currentTask.TargetResource.IsReserved)
                _currentTask.TargetResource.Release();
            if (_currentTask?.TargetBuilding != null && _currentTask.TargetBuilding.IsBeingBuilt)
                _currentTask.TargetBuilding.ReleaseConstruction();
            if (_savedTask?.TargetResource != null && _savedTask.TargetResource.IsReserved)
                _savedTask.TargetResource.Release();

            // Free building assignment
            var buildings = FindObjectsByType<Building>(FindObjectsSortMode.None);
            foreach (var b in buildings)
            {
                if (b.AssignedWorker == gameObject)
                {
                    b.HasWorker = false;
                    b.AssignedWorker = null;
                }
            }

            EventBus.Publish(new SettlerDiedEvent
            {
                SettlerName = name,
                Position = transform.position,
                CauseOfDeath = "Starvation"
            });

            // Update population count
            var settlers = FindObjectsByType<Settler>(FindObjectsSortMode.None);
            EventBus.Publish(new PopulationChangedEvent
            {
                CurrentPopulation = settlers.Length - 1 // -1 because we're about to be destroyed
            });

            Destroy(gameObject);
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
                SettlerTaskType.Hunt => new Color(0.20f, 0.65f, 0.20f),        // Green (berries)
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

            _visualRenderer = visual.GetComponent<MeshRenderer>();
            _visualRenderer.sharedMaterial = _sharedMaterial;

            _propBlock = new MaterialPropertyBlock();
            _propBlock.SetColor(ColorID, DEFAULT_COLOR);
            _visualRenderer.SetPropertyBlock(_propBlock);
        }

        /// <summary>
        /// Update the settler's capsule color to reflect role and hunger status.
        /// Priority: Hungry (red) > Specialized role (brown/green) > Default (white).
        /// Story 5.5: Visuelles Feedback
        /// </summary>
        private void UpdateVisualColor()
        {
            if (_visualRenderer == null || _propBlock == null) return;

            Color color = DEFAULT_COLOR;

            // Hunger overrides role color when critical
            if (_hunger < HUNGER_SLOW_THRESHOLD)
            {
                color = HUNGRY_COLOR;
            }
            else
            {
                // Check specialized role (current or saved task during eating)
                var roleTask = _currentTask ?? _savedTask;
                if (roleTask != null && roleTask.IsSpecialized)
                {
                    color = roleTask.TaskType switch
                    {
                        SettlerTaskType.GatherWood => WOODCUTTER_COLOR,
                        SettlerTaskType.Hunt => HUNTER_COLOR,
                        _ => DEFAULT_COLOR
                    };
                }
            }

            _propBlock.SetColor(ColorID, color);
            _visualRenderer.SetPropertyBlock(_propBlock);
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

        // ─── Specialized Worker Helpers ─────────────────────────

        /// <summary>
        /// Find a new resource for a specialized worker whose current target
        /// is depleted. Keeps the task (and color/speed) but swaps the resource.
        /// </summary>
        private bool TryFindNewResource()
        {
            if (_currentTask == null) return false;

            ResourceType resType = _currentTask.TaskType switch
            {
                SettlerTaskType.GatherWood => ResourceType.Wood,
                SettlerTaskType.Hunt => ResourceType.Food,
                _ => ResourceType.Wood
            };

            var nodes = FindObjectsByType<ResourceNode>(FindObjectsSortMode.None);
            ResourceNode nearest = null;
            float nearestDist = float.MaxValue;

            foreach (var node in nodes)
            {
                if (node.Type != resType || !node.IsAvailable) continue;
                float dist = Vector3.Distance(_currentTask.BasePosition, node.transform.position);
                if (dist < nearestDist)
                {
                    nearestDist = dist;
                    nearest = node;
                }
            }

            if (nearest == null || !nearest.TryReserve()) return false;

            _currentTask.TargetResource = nearest;
            _currentTask.SetNewTarget(nearest.transform.position);

            if (!SetAgentDestination(nearest.transform.position))
            {
                nearest.Release();
                return false;
            }

            _state = SettlerState.WalkingToTarget;
            _agent.speed = TASK_WALK_SPEED * _currentTask.SpeedMultiplier;
            Debug.Log($"[{name}] Specialized worker found new {resType} target");
            return true;
        }

        // ─── Priority Check ──────────────────────────────────────

        /// <summary>
        /// Check if any construction site needs a builder.
        /// Called after delivery to re-evaluate task priorities.
        /// </summary>
        private static bool HasPendingConstructionSite()
        {
            var buildings = Object.FindObjectsByType<Building>(FindObjectsSortMode.None);
            foreach (var building in buildings)
            {
                if (!building.IsConstructed && !building.IsBeingBuilt)
                    return true;
            }
            return false;
        }
    }
}
