using UnityEngine;
using Terranova.Core;

namespace Terranova.Resources
{
    /// <summary>
    /// A gatherable resource in the world (twig, stone, or berry bush).
    ///
    /// Epoch I.1: Settlers pick up the resource in one action.
    /// The node disappears immediately and respawns after a timer.
    ///
    /// Story 3.1: Sammelbare Objekte
    /// Story 3.2: Sammel-Interaktion (depletion)
    /// Story 3.5: Ressourcen-Respawn
    /// </summary>
    public class ResourceNode : MonoBehaviour
    {
        // ─── Configuration ──────────────────────────────────────

        private const float RESPAWN_TIME_WOOD = 60f;   // Game-time seconds
        private const float RESPAWN_TIME_STONE = 90f;
        private const float RESPAWN_TIME_FOOD = 45f;

        // Epoch I.1: Each resource is picked up in a single action
        private const int DEFAULT_GATHERS_WOOD = 1;
        private const int DEFAULT_GATHERS_STONE = 1;
        private const int DEFAULT_GATHERS_FOOD = 1;

        // ─── State ─────────────────────────────────────────────

        public ResourceType Type { get; private set; }
        public int MaxGathers { get; private set; }
        public int RemainingGathers { get; private set; }
        public bool IsReserved { get; private set; }

        public bool IsDepleted => RemainingGathers <= 0;
        public bool IsAvailable => !IsDepleted && !IsReserved;

        private Vector3 _originalScale;
        private float _respawnTimer;
        private bool _respawning;

        // ─── Initialization ────────────────────────────────────

        /// <summary>
        /// Set up this resource node. Called by ResourceSpawner after creating the object.
        /// </summary>
        public void Initialize(ResourceType type)
        {
            Type = type;
            MaxGathers = type switch
            {
                ResourceType.Wood => DEFAULT_GATHERS_WOOD,
                ResourceType.Stone => DEFAULT_GATHERS_STONE,
                ResourceType.Food => DEFAULT_GATHERS_FOOD,
                _ => 3
            };
            RemainingGathers = MaxGathers;
            _originalScale = transform.localScale;
        }

        // ─── Gathering API ─────────────────────────────────────

        /// <summary>
        /// Try to reserve this node for a settler. Only one settler at a time.
        /// Returns false if already reserved or depleted.
        /// </summary>
        public bool TryReserve()
        {
            if (!IsAvailable) return false;
            IsReserved = true;
            return true;
        }

        /// <summary>
        /// Release reservation (e.g., settler's task was canceled).
        /// </summary>
        public void Release()
        {
            IsReserved = false;
        }

        /// <summary>
        /// Called when a settler picks up this resource.
        /// Epoch I.1: Disappears immediately (single pickup).
        /// </summary>
        public void CompleteGathering()
        {
            IsReserved = false;
            RemainingGathers--;

            Debug.Log($"[ResourceNode] {Type} picked up at ({transform.position.x:F0}, {transform.position.z:F0})");

            Deplete();
        }

        // ─── Depletion & Respawn ───────────────────────────────

        private void Deplete()
        {
            // Hide ALL renderers (handles compound objects like berry bushes)
            foreach (var r in GetComponentsInChildren<MeshRenderer>())
                r.enabled = false;

            // Start respawn timer
            _respawnTimer = Type switch
            {
                ResourceType.Wood => RESPAWN_TIME_WOOD,
                ResourceType.Stone => RESPAWN_TIME_STONE,
                ResourceType.Food => RESPAWN_TIME_FOOD,
                _ => RESPAWN_TIME_WOOD
            };
            _respawning = true;

            EventBus.Publish(new ResourceDepletedEvent
            {
                Type = Type,
                Position = transform.position
            });

            Debug.Log($"[ResourceNode] {Type} DEPLETED at ({transform.position.x:F0}, {transform.position.z:F0})" +
                      $" - respawns in {_respawnTimer:F0}s");
        }

        private void Update()
        {
            if (!_respawning) return;

            _respawnTimer -= Time.deltaTime;
            if (_respawnTimer > 0f) return;

            // Check if a building is blocking the respawn position
            if (IsBuildingNearby())
            {
                _respawnTimer = 10f; // Retry in 10s
                return;
            }

            Respawn();
        }

        private void Respawn()
        {
            RemainingGathers = MaxGathers;
            transform.localScale = _originalScale;
            _respawning = false;

            // Show ALL renderers again
            foreach (var r in GetComponentsInChildren<MeshRenderer>())
                r.enabled = true;

            Debug.Log($"[ResourceNode] {Type} respawned at ({transform.position.x:F0}, {transform.position.z:F0})");
        }

        private bool IsBuildingNearby()
        {
            var buildings = FindObjectsByType<Terranova.Buildings.Building>(FindObjectsSortMode.None);
            foreach (var b in buildings)
            {
                if (Vector3.Distance(transform.position, b.transform.position) < 2f)
                    return true;
            }
            return false;
        }

        // ─── Helpers ───────────────────────────────────────────

        /// <summary>
        /// Map ResourceType to SettlerTaskType.
        /// </summary>
        public SettlerTaskType ToTaskType()
        {
            return Type switch
            {
                ResourceType.Wood => SettlerTaskType.GatherWood,
                ResourceType.Stone => SettlerTaskType.GatherStone,
                ResourceType.Food => SettlerTaskType.Hunt,
                _ => SettlerTaskType.None
            };
        }
    }
}
