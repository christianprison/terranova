using UnityEngine;

namespace Terranova.Buildings
{
    /// <summary>
    /// The functional type of a building. Determines what it does when complete.
    /// Story 4.3/4.4: Gebäude-Typen und Gebäude-Funktion.
    /// </summary>
    public enum BuildingType
    {
        Campfire,             // Gathering point, center of settlement
        WoodcutterHut,        // Auto-assigns settler to gather wood
        HunterHut,            // Auto-assigns settler to hunt (produce food)
        SimpleHut,            // Housing for 2 settlers
        CookingFire,          // Unlocked by Fire discovery — reduces food decay
        TrapSite,             // Unlocked by Animal Traps discovery — passive food

        // Feature 5.3: Buildable Structures (Epoch I.1)
        Windscreen,           // Wickerwork discovery. Moderate protection, capacity 3-4
        LeafHut,              // Wickerwork + Composite Tool. Good protection, capacity 2-3
        OpenFireplace,        // Fire discovery. Warmth + light + animal deterrent
        DugFireplace,         // Fire + Digging. Better warmth
        DryingRack,           // Cord discovery. Enables food drying
        StoragePit,           // Digging discovery. Food storage
        StoneCircleWindbreak  // Composite Tool + multiple settlers. Good protection, capacity 5+
    }

    /// <summary>
    /// ScriptableObject defining a building type's properties.
    ///
    /// Create instances via: Assets → Create → Terranova → Building Definition.
    /// Each building type (Campfire, Hut, Woodcutter's Hut, etc.) gets its own asset.
    ///
    /// Story 4.3: All 4 Epoch I.1 building types defined here.
    /// </summary>
    [CreateAssetMenu(fileName = "NewBuilding", menuName = "Terranova/Building Definition")]
    public class BuildingDefinition : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Display name shown in UI.")]
        public string DisplayName = "Building";

        [Tooltip("Short description for tooltips.")]
        [TextArea(2, 4)]
        public string Description = "";

        [Tooltip("Functional type of this building.")]
        public BuildingType Type;

        [Header("Costs")]
        [Tooltip("Wood required to build.")]
        public int WoodCost;

        [Tooltip("Stone required to build.")]
        public int StoneCost;

        [Header("Placement")]
        [Tooltip("Size in blocks (X × Z). Campfire = 1×1, Hut = 2×2.")]
        public Vector2Int FootprintSize = Vector2Int.one;

        [Tooltip("Can this building be rotated 90°?")]
        public bool NeedsRotation;

        [Header("Navigation (Story 2.3)")]
        [Tooltip("Local offset from building center to entrance. Settlers walk here.")]
        public Vector3 EntranceOffset = new Vector3(0f, 0f, -1f);

        [Header("Visuals (Prototype)")]
        [Tooltip("Color for the placeholder cube. Replaced by proper models later.")]
        public Color PreviewColor = Color.yellow;

        [Tooltip("Height of the placeholder cube in blocks.")]
        public float VisualHeight = 1f;

        [Header("Function (Story 4.4)")]
        [Tooltip("How many settlers this building can house (SimpleHut only).")]
        public int HousingCapacity;

        [Tooltip("How many worker slots this building has (WoodcutterHut/HunterHut).")]
        public int WorkerSlots;

        [Header("Feature 5.3: Shelter & Structure")]
        [Tooltip("Plant fiber / cord required to build.")]
        public int FiberCost;

        [Tooltip("Protection value 0-1 (0=none, 1=perfect). For shelters/structures.")]
        public float ProtectionValue;

        [Tooltip("How many settlers can shelter here. 0 = not a shelter.")]
        public int ShelterCapacity;

        [Tooltip("Discovery capabilities required (e.g. 'wickerwork', 'fire', 'digging').")]
        public string[] RequiredDiscoveries;
    }
}
