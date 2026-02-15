using UnityEngine;

namespace Terranova.Buildings
{
    /// <summary>
    /// Central registry of all available building definitions.
    /// Created at runtime by GameBootstrapper with GDD-defined values.
    ///
    /// Story 4.3: Gebäude-Typen Epoche I.1
    /// Story 4.5: Build menu reads from this registry.
    /// </summary>
    public class BuildingRegistry : MonoBehaviour
    {
        public static BuildingRegistry Instance { get; private set; }

        private BuildingDefinition[] _definitions;
        private BuildingDefinition _campfireDefinition;

        /// <summary>All buildable building definitions (shown in build menu).</summary>
        public BuildingDefinition[] Definitions => _definitions;

        /// <summary>The campfire definition (not buildable, placed at game start).</summary>
        public BuildingDefinition CampfireDefinition => _campfireDefinition;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            CreateDefinitions();
        }

        /// <summary>
        /// Create all Epoch I.1 building definitions from GDD values.
        /// Campfire is created separately – it exists at game start, not buildable.
        /// </summary>
        private void CreateDefinitions()
        {
            _campfireDefinition = CreateDef("Campfire",
                "Gathering point, center of the settlement.",
                BuildingType.Campfire, 0, 0,
                new Vector2Int(1, 1), 1.2f,
                new Color(0.9f, 0.45f, 0.1f));

            _definitions = new[]
            {
                CreateDef("Woodcutter's Hut", "Assigns a settler to chop wood nearby.",
                    BuildingType.WoodcutterHut, 10, 5,
                    new Vector2Int(2, 2), 2f,
                    new Color(0.45f, 0.28f, 0.10f), // Brown
                    workerSlots: 1),

                CreateDef("Hunter's Hut", "Assigns a settler to hunt for food.",
                    BuildingType.HunterHut, 8, 0,
                    new Vector2Int(2, 2), 2f,
                    new Color(0.20f, 0.50f, 0.15f), // Dark green
                    workerSlots: 1),

                CreateDef("Simple Hut", "Housing for 2 settlers.",
                    BuildingType.SimpleHut, 15, 5,
                    new Vector2Int(2, 2), 2.5f,
                    new Color(0.65f, 0.55f, 0.40f), // Tan
                    housingCapacity: 2),

                // Discovery-unlocked buildings (Feature 3.2)
                CreateDef("Cooking Fire", "Reduces food spoilage. Unlocked by Fire.",
                    BuildingType.CookingFire, 5, 3,
                    new Vector2Int(1, 1), 1.0f,
                    new Color(0.95f, 0.55f, 0.15f)), // Orange-flame

                CreateDef("Trap Site", "Passive food source. Unlocked by Animal Traps.",
                    BuildingType.TrapSite, 8, 2,
                    new Vector2Int(2, 2), 1.5f,
                    new Color(0.50f, 0.40f, 0.20f)), // Dark tan

                // Feature 5.3: Buildable Structures
                CreateStructureDef("Windscreen", "Woven barrier against wind. Moderate protection.",
                    BuildingType.Windscreen, 3, 0, 5,
                    new Vector2Int(2, 1), 1.5f,
                    new Color(0.70f, 0.65f, 0.40f), // Straw yellow
                    protectionValue: 0.5f, shelterCapacity: 4,
                    requiredDiscoveries: new[] { "wickerwork" }),

                CreateStructureDef("Leaf Hut", "Small shelter from branches and leaves. Good protection.",
                    BuildingType.LeafHut, 5, 0, 4,
                    new Vector2Int(2, 2), 2.0f,
                    new Color(0.35f, 0.55f, 0.25f), // Leaf green
                    protectionValue: 0.75f, shelterCapacity: 3,
                    requiredDiscoveries: new[] { "wickerwork", "improved_tools" }),

                CreateStructureDef("Open Fireplace", "Stone ring with fire. Warmth, light, deters animals.",
                    BuildingType.OpenFireplace, 3, 4, 0,
                    new Vector2Int(1, 1), 0.8f,
                    new Color(0.95f, 0.45f, 0.10f), // Flame orange
                    protectionValue: 0f, shelterCapacity: 0,
                    requiredDiscoveries: new[] { "fire" }),

                CreateStructureDef("Dug Fireplace", "Sunken fire pit. Better warmth retention.",
                    BuildingType.DugFireplace, 3, 5, 0,
                    new Vector2Int(1, 1), 0.5f,
                    new Color(0.85f, 0.40f, 0.10f), // Deep orange
                    protectionValue: 0f, shelterCapacity: 0,
                    requiredDiscoveries: new[] { "fire", "digging" }),

                CreateStructureDef("Drying Rack", "Frame for drying food. Enables food preservation.",
                    BuildingType.DryingRack, 4, 0, 3,
                    new Vector2Int(2, 1), 2.0f,
                    new Color(0.55f, 0.40f, 0.25f), // Wood brown
                    protectionValue: 0f, shelterCapacity: 0,
                    requiredDiscoveries: new[] { "cord" }),

                CreateStructureDef("Storage Pit", "Dug pit for storing food. Reduces spoilage.",
                    BuildingType.StoragePit, 2, 2, 0,
                    new Vector2Int(1, 1), 0.3f,
                    new Color(0.40f, 0.35f, 0.25f), // Earth brown
                    protectionValue: 0f, shelterCapacity: 0,
                    requiredDiscoveries: new[] { "digging" }),

                CreateStructureDef("Stone Circle Windbreak", "Ring of large stones. Good protection for the group.",
                    BuildingType.StoneCircleWindbreak, 0, 15, 0,
                    new Vector2Int(3, 3), 1.2f,
                    new Color(0.50f, 0.50f, 0.50f), // Stone grey
                    protectionValue: 0.7f, shelterCapacity: 6,
                    requiredDiscoveries: new[] { "improved_tools" }),
            };

            Debug.Log($"BuildingRegistry: Created {_definitions.Length} building definitions.");
        }

        private static BuildingDefinition CreateDef(
            string displayName, string description,
            BuildingType type, int woodCost, int stoneCost,
            Vector2Int footprint, float height, Color color,
            int housingCapacity = 0, int workerSlots = 0)
        {
            var def = ScriptableObject.CreateInstance<BuildingDefinition>();
            def.DisplayName = displayName;
            def.Description = description;
            def.Type = type;
            def.WoodCost = woodCost;
            def.StoneCost = stoneCost;
            def.FootprintSize = footprint;
            def.VisualHeight = height;
            def.PreviewColor = color;
            def.EntranceOffset = new Vector3(0f, 0f, -(footprint.y * 0.5f + 0.5f));
            def.HousingCapacity = housingCapacity;
            def.WorkerSlots = workerSlots;
            def.RequiredDiscoveries = new string[0];
            return def;
        }

        private static BuildingDefinition CreateStructureDef(
            string displayName, string description,
            BuildingType type, int woodCost, int stoneCost, int fiberCost,
            Vector2Int footprint, float height, Color color,
            float protectionValue = 0f, int shelterCapacity = 0,
            string[] requiredDiscoveries = null)
        {
            var def = CreateDef(displayName, description, type, woodCost, stoneCost,
                footprint, height, color);
            def.FiberCost = fiberCost;
            def.ProtectionValue = protectionValue;
            def.ShelterCapacity = shelterCapacity;
            def.RequiredDiscoveries = requiredDiscoveries ?? new string[0];
            return def;
        }

        /// <summary>Find a definition by type (includes campfire).</summary>
        public BuildingDefinition GetByType(BuildingType type)
        {
            if (type == BuildingType.Campfire)
                return _campfireDefinition;

            foreach (var def in _definitions)
            {
                if (def.Type == type) return def;
            }
            return null;
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }
    }
}
