using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;
using Terranova.Core;
using Terranova.Buildings;
using Terranova.Resources;
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
    /// Story 2.0: Movement migrated to NavMesh (replaces block-grid pathfinding).
    /// Story 3.2: Gathering calls to ResourceNode on work completion.
    /// Story 3.3: Visual cargo indicator during transport.
    /// MS4 Feature 3: Tool System (equip, durability, quality multiplier).
    /// MS4 Feature 4.1: Thirst System (hydration, autonomous water-seeking).
    /// MS4 Feature 4.2: Extended Hunger (hunger states, food nutrition values).
    /// MS4 Feature 4.3: Food Sources (poison berries, honey damage).
    /// MS4 Feature 4.5: Overhead Bars (thirst/hunger world-space UI).
    /// </summary>
    public class Settler : MonoBehaviour
    {
        // ═══════════════════════════════════════════════════════════════
        // ─── Movement & Idle Settings ─────────────────────────────────
        // ═══════════════════════════════════════════════════════════════

        private const float IDLE_RADIUS = 8f;
        private const float BASE_WALK_SPEED = 3.5f;
        private const float TASK_WALK_SPEED = 3.5f;
        private const float MIN_PAUSE = 1f;
        private const float MAX_PAUSE = 3.5f;
        private const float ARRIVAL_THRESHOLD = 0.3f;
        private const float NAV_SAMPLE_RADIUS = 5f;

        // Movement animation speed thresholds
        private const float SPEED_NORMAL = 3.5f;        // Normal walking
        private const float SPEED_SLUGGISH = 2.5f;      // Hungry
        private const float SPEED_STUMBLING = 1.5f;     // Exhausted
        private const float SPEED_CRAWLING = 0.5f;      // Starving

        // ═══════════════════════════════════════════════════════════════
        // ─── Hunger Settings (MS4 Feature 4.2: Extended Hunger) ───────
        // ═══════════════════════════════════════════════════════════════

        private const float MAX_HUNGER = 100f;          // 100 = fully sated, 0 = starving
        private const float HUNGER_RATE = 0.1f;         // Per second (slow drain, ~16 min to starve at 1x)
        private const float STARVATION_GRACE = 30f;     // Seconds at 0 before death
        private const float EAT_DURATION = 1.5f;        // How long eating takes
        private const float DEFAULT_FOOD_RESTORE = 50f; // Fallback nutrition value

        // Hunger state thresholds (inverted: 100=full, 0=empty)
        // Sated: 100-70, Hungry: 70-40, Exhausted: 40-10, Starving: <10
        private const float HUNGER_SATED_THRESHOLD = 70f;
        private const float HUNGER_HUNGRY_THRESHOLD = 40f;
        private const float HUNGER_EXHAUSTED_THRESHOLD = 10f;

        // ═══════════════════════════════════════════════════════════════
        // ─── Thirst Settings (MS4 Feature 4.1) ───────────────────────
        // ═══════════════════════════════════════════════════════════════

        private const float MAX_THIRST = 100f;          // 100 = hydrated, 0 = dying
        private const float THIRST_RATE = 0.15f;        // Per second (slow drain, ~11 min at 1x)
        private const float DRINK_DURATION_MIN = 3f;
        private const float DRINK_DURATION_MAX = 5f;
        private const float DEHYDRATION_GRACE = 20f;    // Seconds at Dying before death
        private const float WATER_SEARCH_RADIUS = 50f;  // Max distance to search for water

        // Thirst state thresholds (100=hydrated, 0=dying)
        // Hydrated: 100-70, Thirsty: 70-40, Dehydrated: 40-10, Dying: <10
        private const float THIRST_HYDRATED_THRESHOLD = 70f;
        private const float THIRST_THIRSTY_THRESHOLD = 40f;
        private const float THIRST_DEHYDRATED_THRESHOLD = 10f;

        // ═══════════════════════════════════════════════════════════════
        // ─── Tool Settings (MS4 Feature 3) ────────────────────────────
        // ═══════════════════════════════════════════════════════════════

        private const float HONEY_GATHER_DAMAGE = 5f;   // Minor damage from honey gathering

        // ═══════════════════════════════════════════════════════════════
        // ─── Visual Settings ──────────────────────────────────────────
        // ═══════════════════════════════════════════════════════════════

        private static readonly Color[] SETTLER_COLORS =
        {
            new Color(0.85f, 0.55f, 0.45f),
            new Color(0.70f, 0.50f, 0.35f),
            new Color(0.55f, 0.45f, 0.40f),
            new Color(0.75f, 0.60f, 0.50f),
            new Color(0.60f, 0.42f, 0.35f),
        };

        private static readonly Color HUNGRY_COLOR = new Color(0.90f, 0.30f, 0.30f);

        // Role accent colors
        private static readonly Color GATHERER_ACCENT = new Color(0.95f, 0.95f, 0.95f);
        private static readonly Color WOODCUTTER_ACCENT = new Color(0.55f, 0.33f, 0.14f);
        private static readonly Color HUNTER_ACCENT = new Color(0.20f, 0.50f, 0.20f);

        // Overhead bar colors (MS4 Feature 4.5)
        private static readonly Color THIRST_BAR_COLOR = new Color(0.2f, 0.5f, 1.0f);     // Blue
        private static readonly Color HUNGER_BAR_COLOR = new Color(1.0f, 0.6f, 0.1f);     // Orange

        private static Material _sharedMaterial;
        private static readonly int ColorID = Shader.PropertyToID("_BaseColor");

        // ═══════════════════════════════════════════════════════════════
        // ─── State Machine ────────────────────────────────────────────
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// All possible settler states. Covers idle, work cycle,
        /// hunger, thirst, and autonomous survival behaviors.
        /// </summary>
        private enum SettlerState
        {
            // Idle behavior (Story 1.2)
            IdlePausing,
            IdleWalking,

            // Work cycle (Story 1.3/1.4)
            WalkingToTarget,
            Working,
            ReturningToBase,
            Delivering,

            // Hunger (Story 5.1/5.2, MS4 Feature 4.2)
            WalkingToEat,
            Eating,

            // Thirst (MS4 Feature 4.1)
            WalkingToDrink,
            Drinking,

            // Autonomous food seeking (MS4 Feature 4.2)
            SeekingFood,
            GatheringFood,

            // Night shelter (v0.4.10)
            WalkingToCampfire,
            RestingAtCampfire
        }

        private SettlerState _state = SettlerState.IdlePausing;
        private Vector3 _campfirePosition;
        private float _stateTimer;

        // ═══════════════════════════════════════════════════════════════
        // ─── NavMesh Agent (Story 2.0) ────────────────────────────────
        // ═══════════════════════════════════════════════════════════════

        private NavMeshAgent _agent;
        private bool _isMoving;
        private bool _pathReachable;

        // ═══════════════════════════════════════════════════════════════
        // ─── Task System (Story 1.3) ──────────────────────────────────
        // ═══════════════════════════════════════════════════════════════

        private static int _totalWoodDelivered;
        private static int _totalStoneDelivered;
        private static int _totalFoodDelivered;

        /// <summary>Reset static state when domain reload is disabled.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _totalWoodDelivered = 0;
            _totalStoneDelivered = 0;
            _totalFoodDelivered = 0;
            _sharedMaterial = null;
        }

        private SettlerTask _currentTask;

        /// <summary>The settler's current task, or null if idle.</summary>
        public SettlerTask CurrentTask => _currentTask;

        /// <summary>Whether the settler is currently busy (has task or is eating/drinking with saved task).</summary>
        public bool HasTask => _currentTask != null || _savedTask != null;

        /// <summary>Current state name (for UI/debug display).</summary>
        public string StateName => _state.ToString();

        /// <summary>
        /// Whether the settler can be interrupted by a player order.
        /// Returns false during critical need states (eating, drinking, night rest).
        /// </summary>
        public bool CanBeInterrupted =>
            _state != SettlerState.WalkingToEat && _state != SettlerState.Eating
            && _state != SettlerState.WalkingToDrink && _state != SettlerState.Drinking
            && _state != SettlerState.SeekingFood && _state != SettlerState.GatheringFood
            && _state != SettlerState.WalkingToCampfire && _state != SettlerState.RestingAtCampfire;

        /// <summary>The order ID of the current task, or -1 if auto-assigned / no task.</summary>
        public int ActiveOrderId => _currentTask?.OrderId ?? _savedTask?.OrderId ?? -1;

        // ═══════════════════════════════════════════════════════════════
        // ─── Hunger System (MS4 Feature 4.2: Extended Hunger) ─────────
        // ═══════════════════════════════════════════════════════════════

        private float _hunger;              // 100 = sated, 0 = starving
        private float _starvationTimer;
        private bool _isStarving;
        private SettlerTask _savedTask;     // Task saved while eating/drinking

        /// <summary>Current hunger value (100 = sated, 0 = starving).</summary>
        public float Hunger => _hunger;

        /// <summary>Hunger as percentage (1.0 = sated, 0.0 = starving).</summary>
        public float HungerPercent => _hunger / MAX_HUNGER;

        /// <summary>True when hunger is at 0 and grace period is ticking.</summary>
        public bool IsStarving => _isStarving;

        /// <summary>Current hunger state based on hunger level.</summary>
        public HungerState CurrentHungerState
        {
            get
            {
                if (_hunger >= HUNGER_SATED_THRESHOLD) return HungerState.Sated;
                if (_hunger >= HUNGER_HUNGRY_THRESHOLD) return HungerState.Hungry;
                if (_hunger >= HUNGER_EXHAUSTED_THRESHOLD) return HungerState.Exhausted;
                return HungerState.Starving;
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // ─── Thirst System (MS4 Feature 4.1) ─────────────────────────
        // ═══════════════════════════════════════════════════════════════

        private float _thirst;              // 100 = hydrated, 0 = dying
        private float _dehydrationTimer;
        private bool _isDying;              // Used for both starvation and dehydration

        /// <summary>Current thirst value (100 = hydrated, 0 = dying of thirst).</summary>
        public float Thirst => _thirst;

        /// <summary>Current thirst state based on thirst level.</summary>
        public ThirstState CurrentThirstState
        {
            get
            {
                if (_thirst >= THIRST_HYDRATED_THRESHOLD) return ThirstState.Hydrated;
                if (_thirst >= THIRST_THIRSTY_THRESHOLD) return ThirstState.Thirsty;
                if (_thirst >= THIRST_DEHYDRATED_THRESHOLD) return ThirstState.Dehydrated;
                return ThirstState.Dying;
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // ─── Tool System (MS4 Feature 3) ──────────────────────────────
        // ═══════════════════════════════════════════════════════════════

        private ToolDefinition _equippedTool;
        private int _toolDurability;

        /// <summary>Currently equipped tool, or null if none.</summary>
        public ToolDefinition EquippedTool => _equippedTool;

        /// <summary>Remaining durability of equipped tool. 0 if no tool.</summary>
        public int ToolDurability => _toolDurability;

        /// <summary>Tool durability as percentage (0.0 - 1.0). 0 if no tool.</summary>
        public float ToolDurabilityPercent =>
            _equippedTool != null && _equippedTool.MaxDurability > 0
                ? (float)_toolDurability / _equippedTool.MaxDurability
                : 0f;

        /// <summary>Whether the settler has a tool equipped.</summary>
        public bool HasTool => _equippedTool != null;

        /// <summary>ID of equipped tool for UI access. Null if no tool.</summary>
        public string EquippedToolId => _equippedTool?.Id;

        /// <summary>Max durability of current tool. 0 if no tool.</summary>
        public int ToolMaxDurability => _equippedTool?.MaxDurability ?? 0;

        /// <summary>Thirst as percentage (0.0 = empty, 1.0 = full).</summary>
        public float ThirstPercent => _thirst / 100f;

        /// <summary>Health status string for UI display.</summary>
        public string HealthStatus => _isSick ? "Sick" :
            (CurrentHungerState == HungerState.Starving ? "Critical" :
            (CurrentHungerState == HungerState.Exhausted ? "Weakened" : "Healthy"));

        /// <summary>
        /// Quality-based work speed multiplier.
        /// Q1=1.0, Q2=1.3, Q3=1.6, Q4=2.0, Q5=2.5
        /// </summary>
        public float ToolQualityMultiplier
        {
            get
            {
                if (_equippedTool == null) return 0.6f; // Toolless: 40% slower
                return _equippedTool.Quality switch
                {
                    1 => 1.0f,
                    2 => 1.3f,
                    3 => 1.6f,
                    4 => 2.0f,
                    5 => 2.5f,
                    _ => 1.0f
                };
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // ─── Shelter & Cold System (v0.4.10) ─────────────────────────
        // ═══════════════════════════════════════════════════════════════

        private const float CAMPFIRE_SHELTER_RADIUS = 10f;  // Blocks from campfire = sheltered
        private const float COLD_HUNGER_DRAIN_MULT = 3f;    // Hunger drains 3x faster in cold
        private const float COLD_THIRST_DRAIN_MULT = 1.5f;  // Thirst drains 1.5x faster in cold
        private const float HYPOTHERMIA_TIME = 90f;          // Seconds of cold exposure before death

        private ShelterState _shelterState = ShelterState.Exposed;
        private float _shelterCheckTimer;
        private float _coldExposureTimer;
        private bool _wasNight;  // Track night transition for task interruption

        /// <summary>Current shelter state.</summary>
        public ShelterState CurrentShelterState => _shelterState;

        // ═══════════════════════════════════════════════════════════════
        // ─── Cargo Visual (Story 3.3) ─────────────────────────────────
        // ═══════════════════════════════════════════════════════════════

        private GameObject _cargoVisual;

        // ═══════════════════════════════════════════════════════════════
        // ─── Overhead Bars (MS4 Feature 4.5) ─────────────────────────
        // ═══════════════════════════════════════════════════════════════

        private Canvas _overheadCanvas;
        private RectTransform _thirstBarFill;
        private RectTransform _hungerBarFill;
        private GameObject _thirstBarRoot;
        private GameObject _hungerBarRoot;

        // ═══════════════════════════════════════════════════════════════
        // ─── Instance Data ────────────────────────────────────────────
        // ═══════════════════════════════════════════════════════════════

        private MaterialPropertyBlock _propBlock;
        private MaterialPropertyBlock _headPropBlock;
        private MeshRenderer _bodyRenderer;
        private MeshRenderer _headRenderer;
        private int _colorIndex;
        private bool _isDeathPending;       // Prevent double-death
        private bool _isSick;               // Poison sickness flag
        private float _sicknessTimer;

        public int ColorIndex => _colorIndex;

        // ═══════════════════════════════════════════════════════════════
        // ─── Traits & Names (v0.4.0 bugfix) ─────────────────────────
        // ═══════════════════════════════════════════════════════════════

        private SettlerTrait _trait;
        private float _experience;

        /// <summary>This settler's personality trait.</summary>
        public SettlerTrait Trait => _trait;

        /// <summary>Accumulated experience points from completing tasks.</summary>
        public float Experience => _experience;

        /// <summary>26-name pool for settler names (A-Z).</summary>
        private static readonly string[] NAME_POOL =
        {
            "Ada", "Bruno", "Cora", "Dion", "Elva",
            "Finn", "Greta", "Hugo", "Iris", "Jasper",
            "Kira", "Lev", "Mara", "Nico", "Opal",
            "Pax", "Quinn", "Runa", "Soren", "Tova",
            "Uli", "Vera", "Wren", "Xena", "Yael", "Zara"
        };

        // ═══════════════════════════════════════════════════════════════
        //
        //  I N I T I A L I Z A T I O N
        //
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Initialize the settler. Called right after instantiation.
        /// </summary>
        public void Initialize(int colorIndex, Vector3 campfirePosition)
        {
            _colorIndex = colorIndex;
            _campfirePosition = campfirePosition;

            // Assign a unique name from the 26-name pool
            gameObject.name = NAME_POOL[colorIndex % NAME_POOL.Length];

            // Assign a random trait
            var traits = System.Enum.GetValues(typeof(SettlerTrait));
            _trait = (SettlerTrait)traits.GetValue(Random.Range(0, traits.Length));
            _experience = 0f;

            // Start fully sated and hydrated
            _hunger = MAX_HUNGER;
            _thirst = MAX_THIRST;

            CreateVisual();
            SetupNavMeshAgent();
            CreateOverheadBars();
            SettlerLocator.Register(transform);

            // Start with a Q1 Simple Hand Axe (MS4 Feature 3)
            EquipTool(ToolDatabase.Get("simple_hand_axe"));

            // Start with random pause (desync settlers)
            _state = SettlerState.IdlePausing;
            _stateTimer = Random.Range(0f, MAX_PAUSE);

            Debug.Log($"[{name}] Spawned with trait: {_trait}");
        }

        private void OnDestroy()
        {
            SettlerLocator.Unregister(transform);
        }

        /// <summary>
        /// Configure the NavMeshAgent component and warp to nearest NavMesh position.
        /// Story 2.0: Replaces manual terrain snapping and VoxelPathfinder.
        /// </summary>
        private void SetupNavMeshAgent()
        {
            _agent = gameObject.AddComponent<NavMeshAgent>();
            _agent.speed = BASE_WALK_SPEED;
            _agent.angularSpeed = 360f;
            _agent.acceleration = 8f;
            _agent.stoppingDistance = ARRIVAL_THRESHOLD;
            _agent.radius = 0.2f;
            _agent.height = 0.8f;
            _agent.baseOffset = 0f;
            _agent.autoBraking = true;
            _agent.autoRepath = true;

            if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, NAV_SAMPLE_RADIUS, NavMesh.AllAreas))
            {
                _agent.Warp(hit.position);
            }
        }

        // ═══════════════════════════════════════════════════════════════
        //
        //  T O O L   S Y S T E M  (MS4 Feature 3)
        //
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Equip a tool to this settler. Sets durability to max.
        /// </summary>
        public void EquipTool(ToolDefinition tool)
        {
            _equippedTool = tool;
            _toolDurability = tool != null ? tool.MaxDurability : 0;
            if (tool != null)
            {
                Debug.Log($"[{name}] Equipped {tool.DisplayName} (Q{tool.Quality}, durability {_toolDurability})");
            }
        }

        /// <summary>
        /// Decrement tool durability by one use. If durability reaches 0,
        /// the tool breaks: fire ToolBrokeEvent and unequip.
        /// </summary>
        private void UseToolOnce()
        {
            if (_equippedTool == null) return;

            _toolDurability--;
            if (_toolDurability <= 0)
            {
                _toolDurability = 0;
                string toolName = _equippedTool.DisplayName;
                Debug.Log($"[{name}] Tool BROKE: {toolName}");

                EventBus.Publish(new ToolBrokeEvent
                {
                    SettlerName = name,
                    ToolName = toolName
                });

                _equippedTool = null;
            }
        }

        /// <summary>
        /// Check whether the settler can perform the given task type.
        /// Settlers without a tool can only pick berries (Hunt) and drink water.
        /// </summary>
        private bool CanPerformTask(SettlerTaskType taskType)
        {
            // Epoch I.1: ALL basic gathering works without tools (deadwood,
            // berries, roots, insects, drinking). Tools only increase speed
            // and enable advanced actions. A toolless settler must never
            // stand idle and starve.
            return true;
        }

        // ═══════════════════════════════════════════════════════════════
        //
        //  T A S K   A S S I G N M E N T  (Story 1.3)
        //
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Assign a task to this settler. Interrupts idle behavior
        /// and starts the work cycle (Story 1.4).
        /// Returns false if the settler already has a task, is dehydrated,
        /// or cannot perform the task without a tool.
        /// </summary>
        public bool AssignTask(SettlerTask task)
        {
            if (_currentTask != null)
            {
                Debug.Log($"[{name}] REJECTED task {task.TaskType} - already has {_currentTask.TaskType}");
                return false;
            }

            // v0.4.10: Settlers at campfire at night refuse new work tasks
            var assignCycle = DayNightCycle.Instance;
            if (assignCycle != null && assignCycle.IsNight
                && (_state == SettlerState.RestingAtCampfire || _state == SettlerState.WalkingToCampfire))
            {
                Debug.Log($"[{name}] REJECTED task {task.TaskType} - resting at campfire (night)");
                return false;
            }

            // MS4 Feature 4.1: Dehydrated settlers refuse new orders
            if (CurrentThirstState == ThirstState.Dehydrated || CurrentThirstState == ThirstState.Dying)
            {
                Debug.Log($"[{name}] REJECTED task {task.TaskType} - too dehydrated to accept orders");
                return false;
            }

            // MS4 Feature 4.2: Exhausted settlers can only seek food
            if (CurrentHungerState == HungerState.Exhausted || CurrentHungerState == HungerState.Starving)
            {
                if (task.TaskType != SettlerTaskType.SeekFood && task.TaskType != SettlerTaskType.DrinkWater)
                {
                    Debug.Log($"[{name}] REJECTED task {task.TaskType} - too exhausted, can only seek food");
                    return false;
                }
            }

            // MS4 Feature 3: Check tool requirement
            if (!CanPerformTask(task.TaskType))
            {
                Debug.Log($"[{name}] REJECTED task {task.TaskType} - no tool equipped");
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
            _agent.speed = GetEffectiveSpeed(TASK_WALK_SPEED * task.SpeedMultiplier);
            UpdateVisualColor();
            Debug.Log($"[{name}] ASSIGNED {task.TaskType} - walking to target (speed x{task.SpeedMultiplier})");
            return true;
        }

        /// <summary>
        /// Cancel the current task externally. Alias for ClearTask.
        /// </summary>
        public void CancelTask()
        {
            ClearTask();
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
            _agent.speed = GetEffectiveSpeed(BASE_WALK_SPEED);
            _state = SettlerState.IdlePausing;
            _stateTimer = Random.Range(MIN_PAUSE, MAX_PAUSE);
            DestroyCargo();
            UpdateVisualColor();
            Debug.Log($"[{name}] Task ended ({wasTask}) - returning to IDLE");
        }

        // ═══════════════════════════════════════════════════════════════
        //
        //  U P D A T E   L O O P
        //
        // ═══════════════════════════════════════════════════════════════

        private void Update()
        {
            // Tick all need systems
            UpdateHunger();
            UpdateThirst();
            UpdateSickness();
            UpdateShelterState();
            UpdateOverheadBars();

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
                case SettlerState.WalkingToDrink:
                    UpdateWalkingToDrink();
                    break;
                case SettlerState.Drinking:
                    UpdateDrinking();
                    break;
                case SettlerState.SeekingFood:
                    UpdateSeekingFood();
                    break;
                case SettlerState.GatheringFood:
                    UpdateGatheringFood();
                    break;
                case SettlerState.WalkingToCampfire:
                    UpdateWalkingToCampfire();
                    break;
                case SettlerState.RestingAtCampfire:
                    UpdateRestingAtCampfire();
                    break;
            }
        }

        // ═══════════════════════════════════════════════════════════════
        //
        //  M O V E M E N T   S P E E D  (MS4 Feature 4.2 / 4.5)
        //
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Calculate effective movement speed factoring in hunger and thirst penalties.
        /// MS4 Feature 4.2: Hunger states affect speed.
        /// MS4 Feature 4.1: Thirst states affect speed.
        /// Returns the lowest resulting speed from all penalties.
        /// </summary>
        private float GetEffectiveSpeed(float baseSpeed)
        {
            float hungerMult = CurrentHungerState switch
            {
                HungerState.Sated => 1.0f,
                HungerState.Hungry => 0.8f,       // -20% speed
                HungerState.Exhausted => 0.5f,     // -50% speed
                HungerState.Starving => 0.14f,     // ~10% speed, barely moves
                _ => 1.0f
            };

            float thirstMult = CurrentThirstState switch
            {
                ThirstState.Hydrated => 1.0f,
                ThirstState.Thirsty => 0.8f,       // -20% speed
                ThirstState.Dehydrated => 0.4f,     // -60% speed
                ThirstState.Dying => 0.15f,
                _ => 1.0f
            };

            // Use the worst penalty
            float mult = Mathf.Min(hungerMult, thirstMult);
            return baseSpeed * mult;
        }

        /// <summary>
        /// Get the movement animation speed tier for overhead bar display.
        /// Returns: 3.5 (normal), 2.5 (sluggish), 1.5 (stumbling), 0.5 (crawling)
        /// </summary>
        private float GetAnimationSpeedTier()
        {
            var hunger = CurrentHungerState;
            var thirst = CurrentThirstState;

            // Worst condition takes priority
            if (hunger == HungerState.Starving || thirst == ThirstState.Dying)
                return SPEED_CRAWLING;
            if (hunger == HungerState.Exhausted || thirst == ThirstState.Dehydrated)
                return SPEED_STUMBLING;
            if (hunger == HungerState.Hungry || thirst == ThirstState.Thirsty)
                return SPEED_SLUGGISH;
            return SPEED_NORMAL;
        }

        // ═══════════════════════════════════════════════════════════════
        //
        //  I D L E   S T A T E S  (Story 1.2)
        //
        // ═══════════════════════════════════════════════════════════════

        private void UpdateIdlePausing()
        {
            _stateTimer -= Time.deltaTime;
            if (_stateTimer > 0f)
                return;

            // v0.4.10: At night, head to campfire instead of wandering
            var cycle = DayNightCycle.Instance;
            if (cycle != null && cycle.IsNight)
            {
                float distToCampfire = Vector3.Distance(
                    new Vector3(transform.position.x, 0f, transform.position.z),
                    new Vector3(_campfirePosition.x, 0f, _campfirePosition.z));
                if (distToCampfire <= CAMPFIRE_SHELTER_RADIUS)
                {
                    StartRestingAtCampfire();
                    return;
                }
                StartWalkingToCampfire();
                return;
            }

            if (TryPickWalkTarget())
            {
                _state = SettlerState.IdleWalking;
                _agent.speed = GetEffectiveSpeed(BASE_WALK_SPEED);
            }
            else
                _stateTimer = 0.5f;
        }

        private void UpdateIdleWalking()
        {
            if (HasReachedDestination())
            {
                _state = SettlerState.IdlePausing;
                _stateTimer = Random.Range(MIN_PAUSE, MAX_PAUSE);
            }
        }

        // ═══════════════════════════════════════════════════════════════
        //
        //  W O R K   C Y C L E   S T A T E S  (Story 1.3/1.4)
        //
        // ═══════════════════════════════════════════════════════════════

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
                // Apply tool quality multiplier to work duration (faster with better tools)
                float workDuration = _currentTask.WorkDuration;
                if (_equippedTool != null && workDuration > 0f)
                {
                    workDuration /= ToolQualityMultiplier;
                }
                // Skilled trait: +15% work speed
                if (_trait == SettlerTrait.Skilled) workDuration *= 0.85f;
                _stateTimer = workDuration;
                Debug.Log($"[{name}] Arrived at target - WORKING ({_currentTask.TaskType}, {_stateTimer:F1}s)");
            }
        }

        /// <summary>
        /// Perform work at the target location.
        /// Story 3.2: On completion, calls ResourceNode.CompleteGathering().
        /// Story 4.2: Build tasks complete construction and go idle.
        /// MS4 Feature 3: Each work action uses tool durability.
        /// MS4 Feature 4.3: Honey gathering causes minor damage.
        /// </summary>
        private void UpdateWorking()
        {
            _stateTimer -= Time.deltaTime;
            if (_stateTimer > 0f)
                return;

            // MS4 Feature 3: Use tool durability on work completion
            UseToolOnce();

            // Story 4.2: Build tasks complete the construction and return to idle
            if (_currentTask?.TaskType == SettlerTaskType.Build)
            {
                if (_currentTask.TargetBuilding != null)
                    _currentTask.TargetBuilding.CompleteConstruction();

                Debug.Log($"[{name}] Construction complete - going idle");
                _currentTask = null;
                _isMoving = false;
                _agent.ResetPath();
                _agent.speed = GetEffectiveSpeed(BASE_WALK_SPEED);
                _state = SettlerState.IdlePausing;
                _stateTimer = Random.Range(MIN_PAUSE, MAX_PAUSE);
                UpdateVisualColor();
                return;
            }

            // MS4 Feature 4.3: Honey gathering causes minor damage
            if (_currentTask?.TargetResource != null)
            {
                var matDef = GetMaterialDefinitionForResource(_currentTask.TargetResource);
                if (matDef != null && matDef.Id == "honey")
                {
                    // Minor damage from bee stings
                    _hunger = Mathf.Max(0f, _hunger - HONEY_GATHER_DAMAGE);
                    Debug.Log($"[{name}] Stung by bees while gathering honey! (-{HONEY_GATHER_DAMAGE} hunger)");
                }
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
            _agent.speed = GetEffectiveSpeed(TASK_WALK_SPEED * _currentTask.SpeedMultiplier);
            Debug.Log($"[{name}] Work done - RETURNING to base");
        }

        /// <summary>
        /// Walk back to the campfire/storage with gathered resources.
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

            DestroyCargo();

            if (_currentTask != null)
            {
                string resourceName = TrackDelivery(_currentTask.TaskType);

                // XP bonus on delivery (Curious trait: +20% XP)
                float xpGain = 10f;
                if (_trait == SettlerTrait.Curious) xpGain *= 1.2f;
                _experience += xpGain;

                var actualType = _currentTask.TaskType switch
                {
                    SettlerTaskType.GatherWood => ResourceType.Wood,
                    SettlerTaskType.GatherStone => ResourceType.Stone,
                    SettlerTaskType.Hunt => ResourceType.Food,
                    _ => ResourceType.Wood
                };
                if (_currentTask.TargetResource != null)
                    actualType = _currentTask.TargetResource.Type;

                EventBus.Publish(new ResourceDeliveredEvent
                {
                    TaskType = _currentTask.TaskType,
                    ActualResourceType = actualType,
                    Position = transform.position
                });

                Debug.Log($"[{name}] DELIVERED {resourceName} " +
                          $"(totals: Wood={_totalWoodDelivered}, Stone={_totalStoneDelivered}, Food={_totalFoodDelivered})");
            }

            // v0.4.10: If night, go to campfire instead of repeating work cycle
            var deliverCycle = DayNightCycle.Instance;
            if (deliverCycle != null && deliverCycle.IsNight)
            {
                Debug.Log($"[{name}] Delivery complete but night — heading to campfire");
                ClearTask();
                StartWalkingToCampfire();
                return;
            }

            // Re-evaluate priorities: construction sites take precedence
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
                _agent.speed = GetEffectiveSpeed(TASK_WALK_SPEED * _currentTask.SpeedMultiplier);
                Debug.Log($"[{name}] REPEATING cycle ({_currentTask.TaskType})");
            }
            else
            {
                if (_currentTask != null && _currentTask.IsSpecialized && TryFindNewResource())
                    return;

                Debug.Log($"[{name}] Target no longer valid - going idle");
                ClearTask();
            }
        }

        // ═══════════════════════════════════════════════════════════════
        //
        //  H U N G E R   S Y S T E M  (MS4 Feature 4.2: Extended Hunger)
        //
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Tick hunger down, check for starvation, and trigger eating when hungry.
        /// MS4 Feature 4.2: Extended hunger with multiple states.
        /// Hunger goes from 100 (sated) to 0 (starving).
        /// </summary>
        private void UpdateHunger()
        {
            if (_hunger > 0f)
            {
                float decayMult = GameplayModifiers.FoodDecayMultiplier;
                // Robust trait: hunger drains 25% slower
                if (_trait == SettlerTrait.Robust) decayMult *= 0.75f;
                // Cold exposure: hunger drains faster at night without shelter
                if (_shelterState == ShelterState.Exposed || _shelterState == ShelterState.Hypothermic)
                    decayMult *= COLD_HUNGER_DRAIN_MULT;
                _hunger -= HUNGER_RATE * decayMult * Time.deltaTime;
                if (_hunger < 0f) _hunger = 0f;
            }

            // Starvation: grace period before death
            if (_hunger <= 0f)
            {
                if (!_isStarving)
                {
                    _isStarving = true;
                    // Enduring trait: +30% grace period
                    _starvationTimer = _trait == SettlerTrait.Enduring
                        ? STARVATION_GRACE * 1.3f : STARVATION_GRACE;
                    UpdateVisualColor();
                    Debug.Log($"[{name}] STARVING - grace period {STARVATION_GRACE}s");

                    EventBus.Publish(new NeedsCriticalEvent
                    {
                        SettlerName = name,
                        NeedType = "Hunger"
                    });
                }

                _starvationTimer -= Time.deltaTime;
                if (_starvationTimer <= 0f)
                {
                    Die("Starvation");
                    return;
                }
            }
            else
            {
                _isStarving = false;
            }

            // MS4 Feature 4.2: Hungry settlers autonomously seek food
            if (CurrentHungerState == HungerState.Hungry || CurrentHungerState == HungerState.Exhausted)
            {
                // Only interrupt from safe states
                if (_state == SettlerState.IdlePausing
                    || _state == SettlerState.IdleWalking
                    || _state == SettlerState.WalkingToTarget
                    || _state == SettlerState.Working)
                {
                    if (_state != SettlerState.WalkingToEat
                        && _state != SettlerState.Eating
                        && _state != SettlerState.SeekingFood
                        && _state != SettlerState.GatheringFood)
                    {
                        StartEating();
                    }
                }
            }
        }

        /// <summary>
        /// Interrupt current activity and walk to campfire to eat.
        /// Saves the current task so it can be resumed after eating.
        /// </summary>
        private void StartEating()
        {
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
                _state = SettlerState.IdlePausing;
                _stateTimer = 2f;
                return;
            }

            _agent.speed = GetEffectiveSpeed(TASK_WALK_SPEED);
            _state = SettlerState.WalkingToEat;
            Debug.Log($"[{name}] HUNGRY ({_hunger:F0}) - walking to campfire to eat");
        }

        /// <summary>Walk to campfire to eat.</summary>
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
        /// MS4 Feature 4.2: Different foods restore different amounts
        /// based on MaterialDefinition.NutritionValue.
        /// MS4 Feature 4.3: Poisonous berries cause sickness.
        /// </summary>
        private void UpdateEating()
        {
            _stateTimer -= Time.deltaTime;
            if (_stateTimer > 0f) return;

            var rm = ResourceManager.Instance;
            if (rm != null && rm.TryConsumeFood())
            {
                // Restore hunger based on default nutrition value
                float nutrition = DEFAULT_FOOD_RESTORE;
                _hunger = Mathf.Min(MAX_HUNGER, _hunger + nutrition);
                _isStarving = false;
                Debug.Log($"[{name}] ATE food - hunger restored to {_hunger:F0}");
            }
            else
            {
                Debug.Log($"[{name}] No food available at campfire!");
            }

            UpdateVisualColor();
            ResumeAfterNeedsFulfilled();
        }

        // ═══════════════════════════════════════════════════════════════
        //
        //  T H I R S T   S Y S T E M  (MS4 Feature 4.1)
        //
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Tick thirst down and trigger water-seeking behavior when thirsty.
        /// Thirst goes from 100 (hydrated) to 0 (dying).
        /// Decay rate: 100/120 per second = empty in ~2 game-minutes.
        /// </summary>
        private void UpdateThirst()
        {
            if (_thirst > 0f)
            {
                float thirstMult = 1f;
                // Robust trait: thirst drains 25% slower
                if (_trait == SettlerTrait.Robust) thirstMult = 0.75f;
                // Cold exposure: thirst drains faster at night without shelter
                if (_shelterState == ShelterState.Exposed || _shelterState == ShelterState.Hypothermic)
                    thirstMult *= COLD_THIRST_DRAIN_MULT;
                _thirst -= THIRST_RATE * thirstMult * Time.deltaTime;
                if (_thirst < 0f) _thirst = 0f;
            }

            // Dying of dehydration: grace period before death
            if (CurrentThirstState == ThirstState.Dying)
            {
                if (_dehydrationTimer <= 0f)
                {
                    // Enduring trait: +30% grace period
                    _dehydrationTimer = _trait == SettlerTrait.Enduring
                        ? DEHYDRATION_GRACE * 1.3f : DEHYDRATION_GRACE;
                    Debug.Log($"[{name}] DYING OF THIRST - grace period {_dehydrationTimer}s");

                    EventBus.Publish(new NeedsCriticalEvent
                    {
                        SettlerName = name,
                        NeedType = "Thirst"
                    });
                }

                _dehydrationTimer -= Time.deltaTime;
                if (_dehydrationTimer <= 0f)
                {
                    Die("Dehydration");
                    return;
                }
            }
            else
            {
                _dehydrationTimer = 0f;
            }

            // MS4 Feature 4.1: Thirsty settlers abandon task and seek water autonomously
            if (CurrentThirstState == ThirstState.Thirsty
                || CurrentThirstState == ThirstState.Dehydrated
                || CurrentThirstState == ThirstState.Dying)
            {
                // Thirst overrides EVERYTHING except already walking-to/drinking.
                // Drinking requires no tool. Priority above hunger and work.
                if (_state != SettlerState.WalkingToDrink
                    && _state != SettlerState.Drinking)
                {
                    StartDrinking();
                }
            }
        }

        /// <summary>
        /// Interrupt current activity and walk to nearest water source.
        /// MS4 Feature 4.1: DrinkWater behavior.
        /// </summary>
        private void StartDrinking()
        {
            // Save task for later
            if (_currentTask != null && _savedTask == null)
            {
                _savedTask = _currentTask;
                _currentTask = null;
            }

            _isMoving = false;
            _agent.ResetPath();
            DestroyCargo();

            // Find nearest water source
            Vector3 waterPos;
            if (!TryFindWaterSource(out waterPos))
            {
                // No water found - stay put and hope
                Debug.Log($"[{name}] THIRSTY ({_thirst:F0}) but NO water source found! Will retry in 5s.");
                _state = SettlerState.IdlePausing;
                _stateTimer = 5f;
                return;
            }

            if (!SetAgentDestination(waterPos))
            {
                Debug.Log($"[{name}] THIRSTY ({_thirst:F0}) - water found at ({waterPos.x:F1},{waterPos.z:F1}) but NavMesh path FAILED!");
                _state = SettlerState.IdlePausing;
                _stateTimer = 3f;
                return;
            }

            _agent.speed = GetEffectiveSpeed(TASK_WALK_SPEED);
            _state = SettlerState.WalkingToDrink;
            Debug.Log($"[{name}] THIRSTY ({_thirst:F0}) - walking to water at ({waterPos.x:F1},{waterPos.z:F1}), dist={Vector3.Distance(transform.position, waterPos):F1}");
        }

        /// <summary>Walk to water source.</summary>
        private void UpdateWalkingToDrink()
        {
            if (HasReachedDestination())
            {
                if (!_pathReachable)
                {
                    // Path was partial — check if close enough to a tagged water object
                    var waterObjects = GameObject.FindGameObjectsWithTag("Water");
                    bool closeEnough = false;
                    if (waterObjects != null)
                    {
                        foreach (var wo in waterObjects)
                        {
                            if (wo != null && Vector3.Distance(transform.position, wo.transform.position) < 6f)
                            {
                                closeEnough = true;
                                break;
                            }
                        }
                    }

                    if (!closeEnough)
                    {
                        Debug.Log($"[{name}] Water path incomplete and not close enough - retrying");
                        _state = SettlerState.IdlePausing;
                        _stateTimer = 2f;
                        return;
                    }
                }

                _agent.ResetPath();
                _isMoving = false;
                _state = SettlerState.Drinking;
                _stateTimer = Random.Range(DRINK_DURATION_MIN, DRINK_DURATION_MAX);
                Debug.Log($"[{name}] Arrived at water - DRINKING ({_stateTimer:F1}s)");
            }
        }

        /// <summary>
        /// Drinking at water source. Kneels for 3-5 seconds then restores thirst.
        /// </summary>
        private void UpdateDrinking()
        {
            _stateTimer -= Time.deltaTime;
            if (_stateTimer > 0f) return;

            _thirst = MAX_THIRST;
            _dehydrationTimer = 0f;
            Debug.Log($"[{name}] Drank water - thirst fully restored");

            UpdateVisualColor();
            ResumeAfterNeedsFulfilled();
        }

        /// <summary>
        /// Search for the nearest drinkable (freshwater) water source.
        /// Priority 1: Tagged "Water" GameObjects (the freshwater pond primitive).
        /// Priority 2: Voxel water blocks (fallback for worlds with carved water).
        /// Logs detection results for debugging.
        /// </summary>
        private bool TryFindWaterSource(out Vector3 waterPosition)
        {
            waterPosition = Vector3.zero;

            // Priority 1: Find tagged "Water" GameObjects (freshwater pond)
            var waterObjects = GameObject.FindGameObjectsWithTag("Water");
            if (waterObjects != null && waterObjects.Length > 0)
            {
                float bestDist = float.MaxValue;
                GameObject bestWater = null;

                foreach (var wo in waterObjects)
                {
                    if (wo == null) continue;
                    float dist = Vector3.Distance(transform.position, wo.transform.position);
                    if (dist < WATER_SEARCH_RADIUS && dist < bestDist)
                    {
                        bestDist = dist;
                        bestWater = wo;
                    }
                }

                if (bestWater != null)
                {
                    // Sample NavMesh near the water object's edge (not center, which may be off-mesh)
                    Vector3 waterCenter = bestWater.transform.position;
                    Vector3 dirToWater = (waterCenter - transform.position).normalized;
                    // Aim for the near edge of the pond (radius ~2.5 from center)
                    Vector3 nearEdge = waterCenter - dirToWater * 2f;

                    if (NavMesh.SamplePosition(nearEdge, out NavMeshHit hit, NAV_SAMPLE_RADIUS * 2f, NavMesh.AllAreas))
                    {
                        waterPosition = hit.position;
                        Debug.Log($"[{name}] Water DETECTED: tagged object '{bestWater.name}' at dist={bestDist:F1}, pathfinding to ({hit.position.x:F1},{hit.position.z:F1})");
                        return true;
                    }

                    // Try directly at water position with larger sample radius
                    if (NavMesh.SamplePosition(waterCenter, out NavMeshHit hit2, NAV_SAMPLE_RADIUS * 3f, NavMesh.AllAreas))
                    {
                        waterPosition = hit2.position;
                        Debug.Log($"[{name}] Water DETECTED: tagged object '{bestWater.name}' at dist={bestDist:F1}, pathfinding to center ({hit2.position.x:F1},{hit2.position.z:F1})");
                        return true;
                    }

                    Debug.Log($"[{name}] Water object '{bestWater.name}' found at dist={bestDist:F1} but NO NavMesh path to it!");
                }
            }

            // Priority 2: Voxel water blocks (fallback)
            var world = WorldManager.Instance;
            if (world == null)
            {
                Debug.Log($"[{name}] No water source found: no WorldManager");
                return false;
            }

            // Search from freshwater center first, then from settler position
            Vector3 freshCenter = world.FreshwaterCenter;
            if (freshCenter != Vector3.zero)
            {
                if (TryFindVoxelWaterNear(freshCenter, 30, out waterPosition))
                    return true;
            }

            bool found = TryFindVoxelWaterNear(transform.position, Mathf.CeilToInt(WATER_SEARCH_RADIUS), out waterPosition);
            if (!found)
                Debug.Log($"[{name}] No water source found anywhere within {WATER_SEARCH_RADIUS} blocks!");
            return found;
        }

        /// <summary>
        /// Search for voxel water blocks in expanding rings from a given origin.
        /// Returns true if a walkable position adjacent to water is found.
        /// Fallback for worlds that use carved voxel water instead of primitives.
        /// </summary>
        private bool TryFindVoxelWaterNear(Vector3 origin, int searchRadius, out Vector3 waterPosition)
        {
            waterPosition = Vector3.zero;

            var world = WorldManager.Instance;
            if (world == null) return false;

            int cx = Mathf.RoundToInt(origin.x);
            int cz = Mathf.RoundToInt(origin.z);

            float bestDist = float.MaxValue;
            bool found = false;

            for (int r = 1; r <= searchRadius; r++)
            {
                for (int dx = -r; dx <= r; dx++)
                {
                    for (int dz = -r; dz <= r; dz++)
                    {
                        if (Mathf.Abs(dx) != r && Mathf.Abs(dz) != r) continue;

                        int wx = cx + dx;
                        int wz = cz + dz;

                        for (int wy = 0; wy < 64; wy++)
                        {
                            VoxelType voxel = world.GetBlockAtWorldPos(wx, wy, wz);
                            if (voxel == VoxelType.Water)
                            {
                                Vector3 candidate = new Vector3(wx + 0.5f, wy + 1f, wz + 0.5f);
                                float dist = Vector3.Distance(transform.position, candidate);
                                if (dist < bestDist)
                                {
                                    if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, NAV_SAMPLE_RADIUS, NavMesh.AllAreas))
                                    {
                                        waterPosition = hit.position;
                                        bestDist = dist;
                                        found = true;
                                    }
                                }
                                break;
                            }
                        }
                    }
                }

                if (found) return true;
            }

            return false;
        }

        // ═══════════════════════════════════════════════════════════════
        //
        //  F O O D   S O U R C E S  (MS4 Feature 4.3)
        //
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Autonomous food seeking when hungry. Walks to nearest berry bush
        /// or food source.
        /// </summary>
        private void UpdateSeekingFood()
        {
            if (HasReachedDestination())
            {
                if (!_pathReachable)
                {
                    Debug.Log($"[{name}] Food source unreachable - going idle");
                    _state = SettlerState.IdlePausing;
                    _stateTimer = 2f;
                    return;
                }

                _agent.ResetPath();
                _isMoving = false;
                _state = SettlerState.GatheringFood;
                _stateTimer = EAT_DURATION;
            }
        }

        /// <summary>
        /// Gathering food at a food source. On completion, restore hunger
        /// based on the food's nutrition value.
        /// MS4 Feature 4.3: Poisonous berries cause sickness.
        /// </summary>
        private void UpdateGatheringFood()
        {
            _stateTimer -= Time.deltaTime;
            if (_stateTimer > 0f) return;

            // Restore some hunger from foraging
            _hunger = Mathf.Min(MAX_HUNGER, _hunger + 15f);
            _isStarving = false;
            Debug.Log($"[{name}] Foraged food - hunger at {_hunger:F0}");

            UpdateVisualColor();
            ResumeAfterNeedsFulfilled();
        }

        /// <summary>
        /// Apply food nutrition and handle poisonous food effects.
        /// MS4 Feature 4.3: Different foods restore different amounts.
        /// Poisonous berries cause SettlerPoisonedEvent.
        /// </summary>
        public void ConsumeFood(MaterialDefinition food)
        {
            if (food == null) return;

            float nutrition = food.NutritionValue > 0 ? food.NutritionValue : DEFAULT_FOOD_RESTORE;
            _hunger = Mathf.Min(MAX_HUNGER, _hunger + nutrition);
            _isStarving = false;

            Debug.Log($"[{name}] Consumed {food.DisplayName} (+{nutrition} hunger, now {_hunger:F0})");

            // MS4 Feature 4.3: Poisonous berries cause sickness
            // Cautious trait: 30% chance to avoid poison
            if (food.IsPoisonous && !(_trait == SettlerTrait.Cautious && Random.value < 0.3f))
            {
                _isSick = true;
                _sicknessTimer = 30f; // 30 seconds of sickness
                // Poisoned food gives minimal nutrition
                _hunger = Mathf.Max(0f, _hunger - nutrition * 0.5f);

                EventBus.Publish(new SettlerPoisonedEvent
                {
                    SettlerName = name,
                    FoodName = food.DisplayName
                });

                Debug.Log($"[{name}] POISONED by {food.DisplayName}!");
            }
        }

        /// <summary>
        /// Update sickness timer. Sick settlers have reduced speed.
        /// </summary>
        private void UpdateSickness()
        {
            if (!_isSick) return;

            _sicknessTimer -= Time.deltaTime;
            if (_sicknessTimer <= 0f)
            {
                _isSick = false;
                Debug.Log($"[{name}] Recovered from sickness");
            }
        }

        /// <summary>
        /// Update shelter state based on time of day and campfire proximity.
        /// v0.4.10: The campfire IS the shelter. During the day, everyone is fine.
        /// At night, settlers within CAMPFIRE_SHELTER_RADIUS are Sheltered;
        /// those outside take cold damage (accelerated hunger/thirst drain,
        /// eventual hypothermia death).
        /// </summary>
        private void UpdateShelterState()
        {
            _shelterCheckTimer -= Time.deltaTime;
            if (_shelterCheckTimer > 0f) return;
            _shelterCheckTimer = 1f; // Check every second

            var cycle = DayNightCycle.Instance;
            bool isNight = cycle != null && cycle.IsNight;

            // Detect night transition — trigger walk to campfire
            if (isNight && !_wasNight)
            {
                _wasNight = true;
                TryReturnToCampfireForNight();
            }
            else if (!isNight && _wasNight)
            {
                _wasNight = false;
                // Dawn: if resting at campfire, resume normal behavior
                if (_state == SettlerState.RestingAtCampfire)
                {
                    Debug.Log($"[{name}] Dawn — resuming normal activity");
                    _coldExposureTimer = 0f;
                    _shelterState = ShelterState.Sheltered;
                    ResumeAfterNeedsFulfilled();
                }
            }

            if (!isNight)
            {
                // Daytime: everyone is fine
                _shelterState = ShelterState.Sheltered;
                _coldExposureTimer = 0f;
                return;
            }

            // Nighttime: check distance to campfire
            float distToCampfire = Vector3.Distance(
                new Vector3(transform.position.x, 0f, transform.position.z),
                new Vector3(_campfirePosition.x, 0f, _campfirePosition.z));

            if (distToCampfire <= CAMPFIRE_SHELTER_RADIUS)
            {
                _shelterState = ShelterState.Sheltered;
                _coldExposureTimer = Mathf.Max(0f, _coldExposureTimer - Time.deltaTime * 2f); // Warm up
            }
            else
            {
                _shelterState = ShelterState.Exposed;
                _coldExposureTimer += _shelterCheckTimer; // Accumulate cold

                if (_coldExposureTimer >= HYPOTHERMIA_TIME)
                {
                    _shelterState = ShelterState.Hypothermic;
                    Die("hypothermia");
                    return;
                }
                else if (_coldExposureTimer >= HYPOTHERMIA_TIME * 0.5f)
                {
                    _shelterState = ShelterState.Hypothermic;
                }
            }
        }

        /// <summary>
        /// Try to send this settler back to the campfire when night falls.
        /// Only interrupts interruptible states (idle, post-delivery).
        /// Settlers mid-task will finish their current action first.
        /// </summary>
        private void TryReturnToCampfireForNight()
        {
            // Already heading to or at campfire
            if (_state == SettlerState.WalkingToCampfire || _state == SettlerState.RestingAtCampfire)
                return;

            // Already near campfire — just rest
            float dist = Vector3.Distance(
                new Vector3(transform.position.x, 0f, transform.position.z),
                new Vector3(_campfirePosition.x, 0f, _campfirePosition.z));
            if (dist <= CAMPFIRE_SHELTER_RADIUS)
            {
                if (_state == SettlerState.IdlePausing || _state == SettlerState.IdleWalking)
                {
                    StartRestingAtCampfire();
                    return;
                }
                // If working, let them finish — they'll be redirected after
                return;
            }

            // Only interrupt idle/pause states immediately
            if (_state == SettlerState.IdlePausing || _state == SettlerState.IdleWalking)
            {
                StartWalkingToCampfire();
            }
            // Active tasks: will be redirected after completion via ResumeAfterNeedsFulfilled
        }

        /// <summary>
        /// Start walking to campfire for night shelter.
        /// </summary>
        private void StartWalkingToCampfire()
        {
            // Save current task for dawn resumption
            if (_currentTask != null && _savedTask == null)
            {
                _savedTask = _currentTask;
                _currentTask = null;
            }

            _isMoving = false;
            _agent.ResetPath();
            DestroyCargo();

            if (SetAgentDestination(_campfirePosition))
            {
                _agent.speed = GetEffectiveSpeed(TASK_WALK_SPEED);
                _state = SettlerState.WalkingToCampfire;
                Debug.Log($"[{name}] Night falling — walking to campfire for shelter");
            }
            else
            {
                // Can't pathfind to campfire — just idle near current position
                _state = SettlerState.IdlePausing;
                _stateTimer = 5f;
                Debug.Log($"[{name}] Night falling — can't reach campfire, staying put");
            }
        }

        /// <summary>
        /// Transition to resting at campfire (sleep/idle until dawn).
        /// </summary>
        private void StartRestingAtCampfire()
        {
            _agent.ResetPath();
            _isMoving = false;
            _state = SettlerState.RestingAtCampfire;
            _stateTimer = 0f;
            Debug.Log($"[{name}] Resting at campfire (sheltered)");
        }

        /// <summary>Walk to campfire for night shelter.</summary>
        private void UpdateWalkingToCampfire()
        {
            if (HasReachedDestination())
            {
                StartRestingAtCampfire();
            }
        }

        /// <summary>
        /// Rest at campfire until dawn. Settler stops moving (sleep state).
        /// Dawn detection is handled by UpdateShelterState.
        /// </summary>
        private void UpdateRestingAtCampfire()
        {
            // Check if dawn has arrived (UpdateShelterState handles the transition)
            var cycle = DayNightCycle.Instance;
            if (cycle != null && !cycle.IsNight)
            {
                // Dawn handler in UpdateShelterState will resume
                return;
            }

            // Sleep: stay completely still until dawn
            if (_agent.hasPath)
                _agent.ResetPath();
            _isMoving = false;
        }

        /// <summary>
        /// Get the MaterialDefinition associated with a resource node, if any.
        /// Used for checking food properties (poison, nutrition).
        /// </summary>
        private MaterialDefinition GetMaterialDefinitionForResource(ResourceNode resource)
        {
            if (resource == null) return null;

            // Map resource type to common material IDs
            return resource.Type switch
            {
                ResourceType.Food => MaterialDatabase.Get("berries_safe"),
                _ => null
            };
        }

        // ═══════════════════════════════════════════════════════════════
        //
        //  R E S U M E   A F T E R   N E E D S
        //
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// After eating or drinking, resume the saved task or go idle.
        /// Shared between hunger and thirst systems.
        /// </summary>
        private void ResumeAfterNeedsFulfilled()
        {
            // v0.4.10: If it's night, go to campfire instead of resuming tasks
            var cycle = DayNightCycle.Instance;
            if (cycle != null && cycle.IsNight)
            {
                // Release saved task — it will be reassigned at dawn by the task system
                if (_savedTask != null)
                {
                    if (_savedTask.TargetResource != null && _savedTask.TargetResource.IsReserved)
                        _savedTask.TargetResource.Release();
                    if (_savedTask.TargetBuilding != null && _savedTask.TargetBuilding.IsBeingBuilt)
                        _savedTask.TargetBuilding.ReleaseConstruction();
                    _savedTask = null;
                }
                _currentTask = null;

                float distToCampfire = Vector3.Distance(
                    new Vector3(transform.position.x, 0f, transform.position.z),
                    new Vector3(_campfirePosition.x, 0f, _campfirePosition.z));
                if (distToCampfire <= CAMPFIRE_SHELTER_RADIUS)
                {
                    StartRestingAtCampfire();
                }
                else
                {
                    StartWalkingToCampfire();
                }
                return;
            }

            if (_savedTask != null)
            {
                var task = _savedTask;
                _savedTask = null;

                if (task.IsTargetValid)
                {
                    _currentTask = task;

                    if (SetAgentDestination(task.TargetPosition))
                    {
                        _state = SettlerState.WalkingToTarget;
                        _agent.speed = GetEffectiveSpeed(TASK_WALK_SPEED * task.SpeedMultiplier);
                        UpdateVisualColor();
                        Debug.Log($"[{name}] Resuming {task.TaskType} after needs fulfilled");
                        return;
                    }
                }

                // Task no longer valid - release reservations
                if (task.TargetResource != null && task.TargetResource.IsReserved)
                    task.TargetResource.Release();
                if (task.TargetBuilding != null && task.TargetBuilding.IsBeingBuilt)
                    task.TargetBuilding.ReleaseConstruction();
            }

            // No saved task or it's invalid - go idle
            _currentTask = null;
            _agent.speed = GetEffectiveSpeed(BASE_WALK_SPEED);
            _state = SettlerState.IdlePausing;
            _stateTimer = Random.Range(MIN_PAUSE, MAX_PAUSE);
            UpdateVisualColor();
        }

        // ═══════════════════════════════════════════════════════════════
        //
        //  D E A T H
        //
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Settler dies from the given cause. Cleans up task, publishes event, destroys.
        /// Story 5.4: Tod. Handles starvation and dehydration.
        /// </summary>
        private void Die(string causeOfDeath)
        {
            if (_isDeathPending) return;
            _isDeathPending = true;

            Debug.Log($"[{name}] DIED of {causeOfDeath}!");

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
                CauseOfDeath = causeOfDeath
            });

            var settlers = FindObjectsByType<Settler>(FindObjectsSortMode.None);
            int alive = 0;
            foreach (var s in settlers)
                if (!s._isDeathPending) alive++;
            EventBus.Publish(new PopulationChangedEvent
            {
                CurrentPopulation = alive
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

        // ═══════════════════════════════════════════════════════════════
        //
        //  N A V M E S H   M O V E M E N T  (Story 2.0)
        //
        // ═══════════════════════════════════════════════════════════════

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
        /// </summary>
        private bool HasReachedDestination()
        {
            if (!_isMoving) return true;
            if (_agent.pathPending) return false;

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

                Vector3 toCampfire = candidate - _campfirePosition;
                toCampfire.y = 0f;
                if (toCampfire.magnitude < 1.2f)
                    continue;

                if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, NAV_SAMPLE_RADIUS, NavMesh.AllAreas))
                {
                    if (SetAgentDestination(hit.position))
                        return true;
                }
            }

            return false;
        }

        // ═══════════════════════════════════════════════════════════════
        //
        //  C A R G O   V I S U A L  (Story 3.3)
        //
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Show a small colored cube above the settler to indicate carried resource.
        /// </summary>
        private void CreateCargo()
        {
            if (_cargoVisual != null) return;
            if (_currentTask == null) return;

            _cargoVisual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _cargoVisual.name = "Cargo";
            _cargoVisual.transform.SetParent(transform, false);
            _cargoVisual.transform.localScale = new Vector3(0.15f, 0.15f, 0.15f);
            _cargoVisual.transform.localPosition = new Vector3(0f, 1.05f, 0f);

            var col = _cargoVisual.GetComponent<Collider>();
            if (col != null) Destroy(col);

            Color cargoColor = _currentTask.TaskType switch
            {
                SettlerTaskType.GatherWood => new Color(0.45f, 0.28f, 0.10f),
                SettlerTaskType.GatherStone => new Color(0.55f, 0.55f, 0.55f),
                SettlerTaskType.Hunt => new Color(0.20f, 0.65f, 0.20f),
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

        // ═══════════════════════════════════════════════════════════════
        //
        //  O V E R H E A D   B A R S  (MS4 Feature 4.5)
        //
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Create thin world-space UI bars above the settler for thirst (blue)
        /// and hunger (orange). Only visible when below 80%.
        /// </summary>
        private void CreateOverheadBars()
        {
            // Create a world-space canvas above the settler
            var canvasObj = new GameObject("OverheadBars");
            canvasObj.transform.SetParent(transform, false);
            canvasObj.transform.localPosition = new Vector3(0f, 1.3f, 0f);

            _overheadCanvas = canvasObj.AddComponent<Canvas>();
            _overheadCanvas.renderMode = RenderMode.WorldSpace;

            var canvasRect = canvasObj.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(0.5f, 0.12f);
            canvasRect.localScale = Vector3.one;

            // Scale down the canvas
            canvasObj.AddComponent<CanvasScaler>();

            // Thirst bar (blue) - top bar
            _thirstBarRoot = CreateOverheadBar(canvasObj.transform, "ThirstBar",
                new Vector2(0f, 0.03f), THIRST_BAR_COLOR, out _thirstBarFill);

            // Hunger bar (orange) - bottom bar
            _hungerBarRoot = CreateOverheadBar(canvasObj.transform, "HungerBar",
                new Vector2(0f, -0.03f), HUNGER_BAR_COLOR, out _hungerBarFill);

            // Start hidden
            _thirstBarRoot.SetActive(false);
            _hungerBarRoot.SetActive(false);
        }

        /// <summary>
        /// Create a single overhead bar (background + fill).
        /// </summary>
        private GameObject CreateOverheadBar(Transform parent, string barName,
            Vector2 localOffset, Color fillColor, out RectTransform fillRect)
        {
            var barObj = new GameObject(barName);
            barObj.transform.SetParent(parent, false);

            var barRect = barObj.AddComponent<RectTransform>();
            barRect.anchorMin = new Vector2(0.5f, 0.5f);
            barRect.anchorMax = new Vector2(0.5f, 0.5f);
            barRect.pivot = new Vector2(0.5f, 0.5f);
            barRect.sizeDelta = new Vector2(0.4f, 0.04f);
            barRect.anchoredPosition = localOffset;

            // Background
            var bgImage = barObj.AddComponent<Image>();
            bgImage.color = new Color(0.1f, 0.1f, 0.1f, 0.7f);

            // Fill
            var fillObj = new GameObject("Fill");
            fillObj.transform.SetParent(barObj.transform, false);

            fillRect = fillObj.AddComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            fillRect.pivot = new Vector2(0f, 0.5f);

            var fillImage = fillObj.AddComponent<Image>();
            fillImage.color = fillColor;

            return barObj;
        }

        /// <summary>
        /// Update overhead bar visibility and fill amounts.
        /// Only visible when the corresponding need is below 80%.
        /// The bars billboard toward the camera.
        /// </summary>
        private void UpdateOverheadBars()
        {
            if (_overheadCanvas == null) return;

            // Billboard: face the camera
            var cam = Camera.main;
            if (cam != null)
            {
                _overheadCanvas.transform.rotation = cam.transform.rotation;
            }

            // Thirst bar: visible when below 80%
            bool showThirst = _thirst < 80f;
            _thirstBarRoot.SetActive(showThirst);
            if (showThirst && _thirstBarFill != null)
            {
                float pct = _thirst / MAX_THIRST;
                _thirstBarFill.anchorMax = new Vector2(pct, 1f);
            }

            // Hunger bar: visible when below 80%
            bool showHunger = _hunger < 80f;
            _hungerBarRoot.SetActive(showHunger);
            if (showHunger && _hungerBarFill != null)
            {
                float pct = _hunger / MAX_HUNGER;
                _hungerBarFill.anchorMax = new Vector2(pct, 1f);
            }
        }

        // ═══════════════════════════════════════════════════════════════
        //
        //  V I S U A L   S E T U P
        //
        // ═══════════════════════════════════════════════════════════════

        private void CreateVisual()
        {
            EnsureSharedMaterial();

            // Body: cylinder (torso)
            var body = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            body.name = "Body";
            body.transform.SetParent(transform, false);
            body.transform.localScale = new Vector3(0.3f, 0.35f, 0.2f);
            body.transform.localPosition = new Vector3(0f, 0.35f, 0f);
            var bodyCol = body.GetComponent<Collider>();
            if (bodyCol != null) Destroy(bodyCol);

            _bodyRenderer = body.GetComponent<MeshRenderer>();
            _bodyRenderer.sharedMaterial = _sharedMaterial;

            _propBlock = new MaterialPropertyBlock();
            Color bodyColor = SETTLER_COLORS[_colorIndex % SETTLER_COLORS.Length];
            _propBlock.SetColor(ColorID, bodyColor);
            _bodyRenderer.SetPropertyBlock(_propBlock);

            // Head: sphere
            var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.name = "Head";
            head.transform.SetParent(transform, false);
            head.transform.localScale = new Vector3(0.22f, 0.22f, 0.22f);
            head.transform.localPosition = new Vector3(0f, 0.81f, 0f);
            var headCol = head.GetComponent<Collider>();
            if (headCol != null) Destroy(headCol);

            _headRenderer = head.GetComponent<MeshRenderer>();
            _headRenderer.sharedMaterial = _sharedMaterial;

            _headPropBlock = new MaterialPropertyBlock();
            Color headColor = Color.Lerp(bodyColor, Color.white, 0.35f);
            _headPropBlock.SetColor(ColorID, headColor);
            _headRenderer.SetPropertyBlock(_headPropBlock);

            // Legs: two small cylinders
            for (int i = 0; i < 2; i++)
            {
                var leg = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                leg.name = $"Leg_{i}";
                leg.transform.SetParent(transform, false);
                float xOff = i == 0 ? -0.08f : 0.08f;
                leg.transform.localScale = new Vector3(0.1f, 0.15f, 0.1f);
                leg.transform.localPosition = new Vector3(xOff, 0.08f, 0f);
                var legCol = leg.GetComponent<Collider>();
                if (legCol != null) Destroy(legCol);

                var legRenderer = leg.GetComponent<MeshRenderer>();
                legRenderer.sharedMaterial = _sharedMaterial;
                var legPb = new MaterialPropertyBlock();
                legPb.SetColor(ColorID, bodyColor * 0.7f);
                legRenderer.SetPropertyBlock(legPb);
            }

            // Selection collider
            var selectionCol = gameObject.AddComponent<BoxCollider>();
            selectionCol.isTrigger = true;
            selectionCol.center = new Vector3(0f, 0.5f, 0f);
            selectionCol.size = new Vector3(0.5f, 1.0f, 0.5f);
        }

        /// <summary>
        /// Update the settler's color to reflect role and hunger/thirst status.
        /// Priority: Critical needs (red) > Role color > Default skin tone.
        /// </summary>
        private void UpdateVisualColor()
        {
            if (_bodyRenderer == null || _propBlock == null) return;

            // Critical hunger or thirst overrides everything
            bool isCritical = CurrentHungerState == HungerState.Exhausted
                || CurrentHungerState == HungerState.Starving
                || CurrentThirstState == ThirstState.Dehydrated
                || CurrentThirstState == ThirstState.Dying;

            if (isCritical)
            {
                _propBlock.SetColor(ColorID, HUNGRY_COLOR);
                _bodyRenderer.SetPropertyBlock(_propBlock);
                if (_headRenderer != null && _headPropBlock != null)
                {
                    _headPropBlock.SetColor(ColorID, HUNGRY_COLOR);
                    _headRenderer.SetPropertyBlock(_headPropBlock);
                }
                return;
            }

            // Determine role color
            var roleTask = _currentTask ?? _savedTask;
            if (roleTask != null && roleTask.IsSpecialized)
            {
                UpdateRoleAccent(roleTask.TaskType);
            }
            else
            {
                _propBlock.SetColor(ColorID, GATHERER_ACCENT);
                _bodyRenderer.SetPropertyBlock(_propBlock);
                if (_headRenderer != null && _headPropBlock != null)
                {
                    _headPropBlock.SetColor(ColorID, GATHERER_ACCENT);
                    _headRenderer.SetPropertyBlock(_headPropBlock);
                }
            }
        }

        /// <summary>
        /// Update body and head color for specialized roles.
        /// </summary>
        public void UpdateRoleAccent(SettlerTaskType role)
        {
            Color accent = role switch
            {
                SettlerTaskType.GatherWood => WOODCUTTER_ACCENT,
                SettlerTaskType.Hunt => HUNTER_ACCENT,
                _ => GATHERER_ACCENT
            };

            if (_bodyRenderer != null && _propBlock != null)
            {
                _propBlock.SetColor(ColorID, accent);
                _bodyRenderer.SetPropertyBlock(_propBlock);
            }
            if (_headRenderer != null && _headPropBlock != null)
            {
                _headPropBlock.SetColor(ColorID, accent);
                _headRenderer.SetPropertyBlock(_headPropBlock);
            }
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

        // ═══════════════════════════════════════════════════════════════
        //
        //  S P E C I A L I Z E D   W O R K E R   H E L P E R S
        //
        // ═══════════════════════════════════════════════════════════════

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
            _agent.speed = GetEffectiveSpeed(TASK_WALK_SPEED * _currentTask.SpeedMultiplier);
            Debug.Log($"[{name}] Specialized worker found new {resType} target");
            return true;
        }

        // ═══════════════════════════════════════════════════════════════
        //
        //  P R I O R I T Y   C H E C K
        //
        // ═══════════════════════════════════════════════════════════════

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
