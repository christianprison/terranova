using UnityEngine;
using Terranova.Core;
using Terranova.Terrain;
using Terranova.Buildings;

namespace Terranova.Discovery
{
    /// <summary>
    /// Creates and registers all discovery definitions at runtime.
    ///
    /// This serves as the data source until .asset files are created in the editor.
    /// Each discovery is a ScriptableObject instance created programmatically.
    ///
    /// Discovery data files live in Assets/Terranova/Data/Discoveries/
    /// (currently empty; runtime definitions here take precedence).
    /// </summary>
    public class DiscoveryRegistry : MonoBehaviour
    {
        private void Start()
        {
            var engine = DiscoveryEngine.Instance;
            if (engine == null)
            {
                Debug.LogWarning("[DiscoveryRegistry] No DiscoveryEngine found.");
                return;
            }

            RegisterAllDiscoveries(engine);
            Debug.Log("[DiscoveryRegistry] All discoveries registered.");
        }

        private void RegisterAllDiscoveries(DiscoveryEngine engine)
        {
            // ─── Biome Discoveries ──────────────────────────────
            engine.RegisterDiscovery(CreateDiscovery(
                "Fertile Soil",
                DiscoveryType.Biome,
                "Your settlers notice the dark, rich soil in the grasslands. Crops could thrive here.",
                "The earth here smells of promise.",
                requiredBiomes: new[] { VoxelType.Grass, VoxelType.Dirt },
                baseProbability: 0.15f,
                repetitionBonus: 0.03f,
                badLuckThreshold: 40,
                unlockedCapabilities: new[] { "agriculture" }
            ));

            engine.RegisterDiscovery(CreateDiscovery(
                "Stone Deposits",
                DiscoveryType.Biome,
                "Exposed rock formations reveal veins of workable stone. Sturdier buildings are now possible.",
                "The mountain yields its secrets.",
                requiredBiomes: new[] { VoxelType.Stone },
                baseProbability: 0.12f,
                repetitionBonus: 0.02f,
                badLuckThreshold: 45,
                unlockedCapabilities: new[] { "masonry" }
            ));

            engine.RegisterDiscovery(CreateDiscovery(
                "Coastal Waters",
                DiscoveryType.Biome,
                "The shoreline offers shellfish and driftwood. A new source of sustenance.",
                "Where land meets water, abundance awaits.",
                requiredBiomes: new[] { VoxelType.Sand, VoxelType.Water },
                baseProbability: 0.10f,
                repetitionBonus: 0.02f,
                badLuckThreshold: 50,
                unlockedCapabilities: new[] { "fishing" }
            ));

            // ─── Activity Discoveries ───────────────────────────
            engine.RegisterDiscovery(CreateDiscovery(
                "Woodworking",
                DiscoveryType.Activity,
                "After felling many trees, your woodcutters have learned to shape timber into planks and beams.",
                "The grain of the wood speaks to those who listen.",
                requiredActivity: SettlerTaskType.GatherWood,
                requiredActivityCount: 10,
                baseProbability: 0.12f,
                repetitionBonus: 0.03f,
                badLuckThreshold: 40,
                unlockedCapabilities: new[] { "woodworking" }
            ));

            engine.RegisterDiscovery(CreateDiscovery(
                "Tracking",
                DiscoveryType.Activity,
                "Your hunters have become skilled at reading animal trails and predicting prey movement.",
                "The forest reveals its paths to the patient observer.",
                requiredActivity: SettlerTaskType.Hunt,
                requiredActivityCount: 8,
                baseProbability: 0.12f,
                repetitionBonus: 0.03f,
                badLuckThreshold: 40,
                unlockedCapabilities: new[] { "tracking" }
            ));

            engine.RegisterDiscovery(CreateDiscovery(
                "Tool Making",
                DiscoveryType.Activity,
                "Experience with stone and wood has inspired your settlers to craft simple tools.",
                "Necessity births invention.",
                requiredActivity: SettlerTaskType.GatherStone,
                requiredActivityCount: 5,
                baseProbability: 0.10f,
                repetitionBonus: 0.02f,
                badLuckThreshold: 50,
                unlockedCapabilities: new[] { "tools" }
            ));

            // ─── Spontaneous Discoveries ────────────────────────
            engine.RegisterDiscovery(CreateDiscovery(
                "Fire Mastery",
                DiscoveryType.Spontaneous,
                "Careful observation of the campfire has taught your settlers to control flame. Cooking and warmth improve.",
                "The dancing flame obeys the hand that feeds it.",
                baseProbability: 0.05f,
                repetitionBonus: 0.01f,
                badLuckThreshold: 50,
                unlockedCapabilities: new[] { "fire_mastery" }
            ));

            engine.RegisterDiscovery(CreateDiscovery(
                "Herbal Knowledge",
                DiscoveryType.Spontaneous,
                "A settler stumbles upon plants with healing properties. Basic medicine is now possible.",
                "Nature provides for those who seek.",
                baseProbability: 0.04f,
                repetitionBonus: 0.01f,
                badLuckThreshold: 60,
                unlockedCapabilities: new[] { "herbalism" }
            ));
        }

        /// <summary>
        /// Helper to create a DiscoveryDefinition ScriptableObject at runtime.
        /// </summary>
        private static DiscoveryDefinition CreateDiscovery(
            string displayName,
            DiscoveryType type,
            string description,
            string flavorText,
            VoxelType[] requiredBiomes = null,
            SettlerTaskType requiredActivity = SettlerTaskType.None,
            int requiredActivityCount = 0,
            float baseProbability = 0.1f,
            float repetitionBonus = 0.02f,
            int badLuckThreshold = 50,
            BuildingType[] unlockedBuildings = null,
            ResourceType[] unlockedResources = null,
            string[] unlockedCapabilities = null)
        {
            var def = ScriptableObject.CreateInstance<DiscoveryDefinition>();
            def.name = displayName;
            def.DisplayName = displayName;
            def.Type = type;
            def.Description = description;
            def.FlavorText = flavorText;
            def.RequiredBiomes = requiredBiomes ?? new VoxelType[0];
            def.RequiredActivity = requiredActivity;
            def.RequiredActivityCount = requiredActivityCount;
            def.BaseProbability = baseProbability;
            def.RepetitionBonus = repetitionBonus;
            def.BadLuckThreshold = badLuckThreshold;
            def.UnlockedBuildings = unlockedBuildings ?? new BuildingType[0];
            def.UnlockedResources = unlockedResources ?? new ResourceType[0];
            def.UnlockedCapabilities = unlockedCapabilities ?? new string[0];
            return def;
        }
    }
}
