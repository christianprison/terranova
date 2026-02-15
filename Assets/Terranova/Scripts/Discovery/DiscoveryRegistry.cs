using UnityEngine;
using Terranova.Core;
using Terranova.Terrain;
using Terranova.Buildings;

namespace Terranova.Discovery
{
    /// <summary>
    /// Creates and registers all GDD Epoch I.1 discovery definitions at runtime.
    ///
    /// Feature 2: Real discoveries replacing placeholders.
    ///
    /// Biome-Driven (2.1):
    ///   - Flint: mountains + stone gathering
    ///   - Resin & Glue: forest + wood gathering
    ///   - Various Wood Types: forest + wood gathering
    ///
    /// Activity-Driven (2.2):
    ///   - Friction Fire: lots of wood work + forest biome
    ///   - Spark Fire: stone work + mountains biome
    ///   - Improved Stone Tools: lots of stone work
    ///   - Primitive Cord: plant fiber gathering
    ///   - Animal Traps: hunting experience (requires Primitive Cord)
    ///
    /// Spontaneous (2.3):
    ///   - Lightning Fire: random lightning strikes tree, settler nearby = Fire
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
            Debug.Log("[DiscoveryRegistry] All Epoch I.1 discoveries registered.");
        }

        private void RegisterAllDiscoveries(DiscoveryEngine engine)
        {
            // ─── Biome-Driven Discoveries (2.1) ──────────────────

            engine.RegisterDiscovery(CreateDiscovery(
                "Flint",
                DiscoveryType.Biome,
                "Sharp stones found in the mountainside — they can be shaped into cutting edges.",
                "The mountain gives teeth to those who seek them.",
                requiredBiomes: new[] { VoxelType.Stone },
                requiredActivity: SettlerTaskType.GatherStone,
                requiredActivityCount: 5,
                baseProbability: 0.15f,
                repetitionBonus: 0.03f,
                badLuckThreshold: 35,
                unlockedResources: new[] { ResourceType.Flint },
                unlockedCapabilities: new[] { "flint" }
            ));

            engine.RegisterDiscovery(CreateDiscovery(
                "Resin & Glue",
                DiscoveryType.Biome,
                "Sticky sap oozing from forest trees — it hardens into a strong adhesive.",
                "The forest bleeds gold for the patient hands.",
                requiredBiomes: new[] { VoxelType.Grass },
                requiredActivity: SettlerTaskType.GatherWood,
                requiredActivityCount: 8,
                baseProbability: 0.12f,
                repetitionBonus: 0.02f,
                badLuckThreshold: 40,
                unlockedResources: new[] { ResourceType.Resin },
                unlockedCapabilities: new[] { "resin", "glue" }
            ));

            engine.RegisterDiscovery(CreateDiscovery(
                "Various Wood Types",
                DiscoveryType.Biome,
                "Different trees yield different wood — some bend, some don't. Knowledge brings versatility.",
                "Every tree tells a different story to the axe.",
                requiredBiomes: new[] { VoxelType.Grass },
                requiredActivity: SettlerTaskType.GatherWood,
                requiredActivityCount: 12,
                baseProbability: 0.10f,
                repetitionBonus: 0.02f,
                badLuckThreshold: 45,
                unlockedCapabilities: new[] { "wood_types" }
            ));

            // ─── Activity-Driven Discoveries (2.2) ───────────────

            engine.RegisterDiscovery(CreateDiscovery(
                "Friction Fire",
                DiscoveryType.Activity,
                "Rubbing dry sticks together creates heat — and eventually, flame! Fire changes everything.",
                "From friction, warmth. From warmth, survival.",
                requiredActivity: SettlerTaskType.GatherWood,
                requiredActivityCount: 15,
                requiredBiomes: new[] { VoxelType.Grass },
                baseProbability: 0.10f,
                repetitionBonus: 0.03f,
                badLuckThreshold: 30,
                unlockedBuildings: new[] { BuildingType.CookingFire },
                unlockedCapabilities: new[] { "fire" }
            ));

            engine.RegisterDiscovery(CreateDiscovery(
                "Spark Fire",
                DiscoveryType.Activity,
                "Striking flint against stone sends sparks flying — a faster way to create fire!",
                "Stone speaks to stone in tongues of light.",
                requiredActivity: SettlerTaskType.GatherStone,
                requiredActivityCount: 12,
                requiredBiomes: new[] { VoxelType.Stone },
                baseProbability: 0.10f,
                repetitionBonus: 0.03f,
                badLuckThreshold: 30,
                unlockedBuildings: new[] { BuildingType.CookingFire },
                unlockedCapabilities: new[] { "fire" }
            ));

            engine.RegisterDiscovery(CreateDiscovery(
                "Improved Stone Tools",
                DiscoveryType.Activity,
                "Repeated work with stone reveals how to knap sharper, longer-lasting tools.",
                "The stone teaches patience to those who shape it.",
                requiredActivity: SettlerTaskType.GatherStone,
                requiredActivityCount: 20,
                baseProbability: 0.12f,
                repetitionBonus: 0.03f,
                badLuckThreshold: 35,
                unlockedCapabilities: new[] { "improved_tools" }
            ));

            engine.RegisterDiscovery(CreateDiscovery(
                "Primitive Cord",
                DiscoveryType.Activity,
                "Twisting plant fibers together creates a flexible cord — useful for binding and construction.",
                "The weakest fiber, twisted, becomes unbreakable.",
                requiredActivity: SettlerTaskType.GatherWood,
                requiredActivityCount: 10,
                baseProbability: 0.12f,
                repetitionBonus: 0.03f,
                badLuckThreshold: 35,
                unlockedResources: new[] { ResourceType.PlantFiber },
                unlockedCapabilities: new[] { "cord" }
            ));

            engine.RegisterDiscovery(CreateDiscovery(
                "Animal Traps",
                DiscoveryType.Activity,
                "With cord and knowledge of animal paths, your settlers devise cunning traps.",
                "The patient hunter lets the prey come to them.",
                requiredActivity: SettlerTaskType.Hunt,
                requiredActivityCount: 10,
                baseProbability: 0.10f,
                repetitionBonus: 0.02f,
                badLuckThreshold: 40,
                prerequisiteDiscoveries: new[] { "Primitive Cord" },
                unlockedBuildings: new[] { BuildingType.TrapSite },
                unlockedCapabilities: new[] { "traps" }
            ));

            // ─── Feature 5.3: Structure-Enabling Discoveries ──────

            engine.RegisterDiscovery(CreateDiscovery(
                "Wickerwork",
                DiscoveryType.Activity,
                "Weaving plant fibers and branches together — strong enough to block wind and rain.",
                "The hands learn what the mind cannot teach.",
                requiredActivity: SettlerTaskType.GatherWood,
                requiredActivityCount: 12,
                baseProbability: 0.10f,
                repetitionBonus: 0.025f,
                badLuckThreshold: 40,
                prerequisiteDiscoveries: new[] { "Primitive Cord" },
                unlockedCapabilities: new[] { "wickerwork" }
            ));

            engine.RegisterDiscovery(CreateDiscovery(
                "Digging",
                DiscoveryType.Activity,
                "Using tools to dig into the earth — for storage, shelter, and fire pits.",
                "Beneath the surface lies warmth and safety.",
                requiredActivity: SettlerTaskType.GatherStone,
                requiredActivityCount: 15,
                baseProbability: 0.10f,
                repetitionBonus: 0.025f,
                badLuckThreshold: 40,
                unlockedCapabilities: new[] { "digging" }
            ));

            // ─── Spontaneous Discoveries (2.3) ───────────────────

            engine.RegisterDiscovery(CreateDiscovery(
                "Lightning Fire",
                DiscoveryType.Spontaneous,
                "A bolt from the sky sets a tree ablaze! A nearby settler witnesses the miracle of fire.",
                "The sky itself showed us the way.",
                baseProbability: 0f, // Handled by lightning system, not regular probability
                repetitionBonus: 0f,
                badLuckThreshold: 999,
                unlockedBuildings: new[] { BuildingType.CookingFire },
                unlockedCapabilities: new[] { "fire" }
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
            string[] prerequisiteDiscoveries = null,
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
            def.PrerequisiteDiscoveries = prerequisiteDiscoveries ?? new string[0];
            def.UnlockedBuildings = unlockedBuildings ?? new BuildingType[0];
            def.UnlockedResources = unlockedResources ?? new ResourceType[0];
            def.UnlockedCapabilities = unlockedCapabilities ?? new string[0];
            return def;
        }
    }
}
