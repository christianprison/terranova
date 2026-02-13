using UnityEngine;
using Terranova.Core;

namespace Terranova.Resources
{
    /// <summary>
    /// A gatherable resource in the world (tree or rock).
    ///
    /// Settlers reserve a node, gather from it (one gather per work cycle),
    /// and the node scales down with each gather until depleted.
    /// After depletion the visual disappears and a respawn timer starts.
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

        private const int DEFAULT_GATHERS_WOOD = 3;
        private const int DEFAULT_GATHERS_STONE = 3;

        // ─── State ─────────────────────────────────────────────

        public ResourceType Type { get; private set; }
        public int MaxGathers { get; private set; }
        public int RemainingGathers { get; private set; }
        public bool IsReserved { get; private set; }

        public bool IsDepleted => RemainingGathers <= 0;
        public bool IsAvailable => !IsDepleted && !IsReserved;

        private Vector3 _originalScale;
        private MeshRenderer _renderer;
        private float _respawnTimer;
        private bool _respawning;

        // ─── Initialization ────────────────────────────────────

        /// <summary>
        /// Set up this resource node. Called by ResourceSpawner after creating the object.
        /// </summary>
        public void Initialize(ResourceType type)
        {
            Type = type;
            MaxGathers = type == ResourceType.Wood ? DEFAULT_GATHERS_WOOD : DEFAULT_GATHERS_STONE;
            RemainingGathers = MaxGathers;
            _originalScale = transform.localScale;
            _renderer = GetComponent<MeshRenderer>();
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
        /// Called when a settler finishes one gather cycle at this node.
        /// Decrements remaining gathers, scales down the visual, and
        /// depletes the resource if no gathers remain.
        /// </summary>
        public void CompleteGathering()
        {
            IsReserved = false;
            RemainingGathers--;

            if (IsDepleted)
            {
                Deplete();
            }
            else
            {
                // Scale down proportionally (min 50% of original size)
                float t = (float)RemainingGathers / MaxGathers;
                transform.localScale = _originalScale * Mathf.Lerp(0.5f, 1f, t);
            }
        }

        // ─── Depletion & Respawn ───────────────────────────────

        private void Deplete()
        {
            // Hide the object
            if (_renderer != null)
                _renderer.enabled = false;

            // Start respawn timer
            _respawnTimer = Type == ResourceType.Wood ? RESPAWN_TIME_WOOD : RESPAWN_TIME_STONE;
            _respawning = true;

            EventBus.Publish(new ResourceDepletedEvent
            {
                Type = Type,
                Position = transform.position
            });

            Debug.Log($"[ResourceNode] {Type} depleted at ({transform.position.x:F0}, {transform.position.z:F0})" +
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

            if (_renderer != null)
                _renderer.enabled = true;

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
                _ => SettlerTaskType.None
            };
        }
    }
}
