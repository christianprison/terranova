using UnityEngine;
using Terranova.Core;

namespace Terranova.Resources
{
    /// <summary>
    /// A gatherable resource in the world, now backed by MaterialDefinition.
    ///
    /// Each node has a MaterialId that maps to a MaterialDefinition from MaterialDatabase.
    /// The legacy ResourceType is kept for backward compatibility with existing systems
    /// (settler tasks, resource manager, event bus).
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
        private const float RESPAWN_TIME_RESIN = 75f;
        private const float RESPAWN_TIME_FLINT = 80f;
        private const float RESPAWN_TIME_PLANT_FIBER = 50f;

        // Epoch I.1: Each resource is picked up in a single action
        private const int DEFAULT_GATHERS_WOOD = 1;
        private const int DEFAULT_GATHERS_STONE = 1;
        private const int DEFAULT_GATHERS_FOOD = 1;
        private const int DEFAULT_GATHERS_RESIN = 1;
        private const int DEFAULT_GATHERS_FLINT = 1;
        private const int DEFAULT_GATHERS_PLANT_FIBER = 1;

        // ─── Material System ──────────────────────────────────────

        /// <summary>
        /// The MaterialDatabase ID for this resource node (e.g. "deadwood", "flint", "berries_safe").
        /// When set, MaterialDef will resolve from MaterialDatabase at runtime.
        /// </summary>
        public string MaterialId { get; private set; }

        /// <summary>
        /// Biome-dependent respawn time multiplier set by ResourceSpawner.
        /// Defaults to 1.0 (no change). Higher values = slower respawn.
        /// </summary>
        public float RespawnMultiplier { get; set; } = 1f;

        private MaterialDefinition _cachedMaterialDef;

        /// <summary>
        /// Resolved MaterialDefinition from MaterialDatabase.
        /// Returns null if MaterialId is not set or not found.
        /// </summary>
        public MaterialDefinition MaterialDef
        {
            get
            {
                if (_cachedMaterialDef == null && !string.IsNullOrEmpty(MaterialId))
                    _cachedMaterialDef = MaterialDatabase.Get(MaterialId);
                return _cachedMaterialDef;
            }
        }

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
        /// Set up this resource node with a MaterialId. Called by ResourceSpawner.
        /// Resolves the MaterialDefinition and derives a legacy ResourceType for backward compat.
        /// </summary>
        public void Initialize(string materialId)
        {
            MaterialId = materialId;
            _cachedMaterialDef = MaterialDatabase.Get(materialId);

            // Derive legacy ResourceType from MaterialDefinition for backward compatibility
            Type = DeriveResourceType(materialId, _cachedMaterialDef);

            MaxGathers = GetDefaultGathers(Type);
            RemainingGathers = MaxGathers;
            _originalScale = transform.localScale;
        }

        /// <summary>
        /// Legacy initializer. Called by older systems that still use ResourceType directly.
        /// Kept for backward compatibility.
        /// </summary>
        public void Initialize(ResourceType type)
        {
            Type = type;
            MaterialId = DeriveDefaultMaterialId(type);
            _cachedMaterialDef = MaterialDatabase.Get(MaterialId);

            MaxGathers = GetDefaultGathers(type);
            RemainingGathers = MaxGathers;
            _originalScale = transform.localScale;
        }

        /// <summary>
        /// Map a MaterialDefinition to a legacy ResourceType based on category and ID.
        /// </summary>
        private static ResourceType DeriveResourceType(string materialId, MaterialDefinition matDef)
        {
            // Handle specific material IDs that map to non-obvious legacy types
            switch (materialId)
            {
                case "resin":        return ResourceType.Resin;
                case "flint":        return ResourceType.Flint;
                case "plant_fibers":
                case "grasses_reeds": return ResourceType.PlantFiber;
            }

            if (matDef == null) return ResourceType.Wood;

            return matDef.Category switch
            {
                MaterialCategory.Wood  => ResourceType.Wood,
                MaterialCategory.Stone => ResourceType.Stone,
                MaterialCategory.Plant => ResourceType.Food,
                MaterialCategory.Animal => ResourceType.Food,
                _                      => ResourceType.Wood
            };
        }

        /// <summary>
        /// Map a legacy ResourceType to a default MaterialId for backward compatibility.
        /// </summary>
        private static string DeriveDefaultMaterialId(ResourceType type)
        {
            return type switch
            {
                ResourceType.Wood       => "deadwood",
                ResourceType.Stone      => "river_stone",
                ResourceType.Food       => "berries_safe",
                ResourceType.Resin      => "resin",
                ResourceType.Flint      => "flint",
                ResourceType.PlantFiber => "plant_fibers",
                _                       => "deadwood"
            };
        }

        private static int GetDefaultGathers(ResourceType type)
        {
            return type switch
            {
                ResourceType.Wood       => DEFAULT_GATHERS_WOOD,
                ResourceType.Stone      => DEFAULT_GATHERS_STONE,
                ResourceType.Food       => DEFAULT_GATHERS_FOOD,
                ResourceType.Resin      => DEFAULT_GATHERS_RESIN,
                ResourceType.Flint      => DEFAULT_GATHERS_FLINT,
                ResourceType.PlantFiber => DEFAULT_GATHERS_PLANT_FIBER,
                _                       => 1
            };
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

            string label = MaterialDef != null ? MaterialDef.DisplayName : Type.ToString();
            Debug.Log($"[ResourceNode] {label} picked up at ({transform.position.x:F0}, {transform.position.z:F0})");

            Deplete();
        }

        // ─── Depletion & Respawn ───────────────────────────────

        private void Deplete()
        {
            // Hide ALL renderers (handles compound objects like berry bushes)
            foreach (var r in GetComponentsInChildren<MeshRenderer>())
                r.enabled = false;

            // Start respawn timer (apply biome multiplier)
            float baseTime = Type switch
            {
                ResourceType.Wood       => RESPAWN_TIME_WOOD,
                ResourceType.Stone      => RESPAWN_TIME_STONE,
                ResourceType.Food       => RESPAWN_TIME_FOOD,
                ResourceType.Resin      => RESPAWN_TIME_RESIN,
                ResourceType.Flint      => RESPAWN_TIME_FLINT,
                ResourceType.PlantFiber => RESPAWN_TIME_PLANT_FIBER,
                _                       => RESPAWN_TIME_WOOD
            };
            _respawnTimer = baseTime * RespawnMultiplier;
            _respawning = true;

            EventBus.Publish(new ResourceDepletedEvent
            {
                Type = Type,
                Position = transform.position
            });

            string label = MaterialDef != null ? MaterialDef.DisplayName : Type.ToString();
            Debug.Log($"[ResourceNode] {label} DEPLETED at ({transform.position.x:F0}, {transform.position.z:F0})" +
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

            string label = MaterialDef != null ? MaterialDef.DisplayName : Type.ToString();
            Debug.Log($"[ResourceNode] {label} respawned at ({transform.position.x:F0}, {transform.position.z:F0})");
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
                ResourceType.Wood       => SettlerTaskType.GatherWood,
                ResourceType.Stone      => SettlerTaskType.GatherStone,
                ResourceType.Food       => SettlerTaskType.Hunt,
                ResourceType.Resin      => SettlerTaskType.GatherWood,
                ResourceType.Flint      => SettlerTaskType.GatherStone,
                ResourceType.PlantFiber => SettlerTaskType.GatherWood,
                _                       => SettlerTaskType.None
            };
        }
    }
}
