using System;

namespace Terranova.Core
{
    /// <summary>
    /// Broad categories for materials, replacing the old ResourceType enum.
    /// MS4 Feature 2: Extended Material System.
    /// </summary>
    public enum MaterialCategory
    {
        Wood,
        Stone,
        Plant,
        Animal,
        Other
    }

    /// <summary>
    /// Physical/chemical properties of materials. Flags allow multiple properties.
    /// </summary>
    [Flags]
    public enum MaterialProperty
    {
        None = 0,
        Hard = 1 << 0,
        Soft = 1 << 1,
        Flexible = 1 << 2,
        Sticky = 1 << 3,
        Sharp = 1 << 4,
        Edible = 1 << 5,
        Poisonous = 1 << 6
    }

    /// <summary>
    /// Defines a single material type in the game.
    /// Replaces the old ResourceType enum with rich, data-driven definitions.
    /// </summary>
    [Serializable]
    public class MaterialDefinition
    {
        public string Id;
        public string DisplayName;
        public MaterialCategory Category;
        public MaterialProperty Properties;
        public BiomeType PreferredBiome;
        public float GatherDuration;
        public int NutritionValue;
        public float SpoilageRate; // hours until spoiled, 0 = no spoilage
        public bool RequiresTool;
        public int MinToolQuality;
        public string GenericName; // shown before discovery (e.g. "Stone", "Wood")
        public string DiscoveryRequired; // discovery ID needed to distinguish this material

        public bool IsEdible => (Properties & MaterialProperty.Edible) != 0;
        public bool IsPoisonous => (Properties & MaterialProperty.Poisonous) != 0;
        public bool IsSharp => (Properties & MaterialProperty.Sharp) != 0;
    }
}
