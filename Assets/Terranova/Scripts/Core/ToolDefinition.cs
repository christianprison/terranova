using System.Collections.Generic;

namespace Terranova.Core
{
    /// <summary>
    /// Defines a tool with quality level, recipe, and durability.
    /// MS4 Feature 3: Tool System.
    /// </summary>
    [System.Serializable]
    public class ToolDefinition
    {
        public string Id;
        public string DisplayName;
        public int Quality; // Q1-Q5
        public string[] Recipe; // MaterialDefinition IDs
        public int MaxDurability;
        public float CraftDuration; // seconds
        public float GatherSpeedMultiplier;
        public string[] RequiredDiscoveries;

        /// <summary>
        /// Quality badge color (Q1=grey, Q2=green, Q3=blue, Q4=purple, Q5=gold).
        /// </summary>
        public UnityEngine.Color QualityColor => Quality switch
        {
            1 => new UnityEngine.Color(0.6f, 0.6f, 0.6f),
            2 => new UnityEngine.Color(0.3f, 0.8f, 0.3f),
            3 => new UnityEngine.Color(0.3f, 0.5f, 0.9f),
            4 => new UnityEngine.Color(0.7f, 0.3f, 0.9f),
            5 => new UnityEngine.Color(1f, 0.8f, 0.2f),
            _ => UnityEngine.Color.white
        };
    }

    /// <summary>
    /// Central registry of all tool definitions.
    /// MS4 Feature 3.1: Tool Data Model.
    /// </summary>
    public static class ToolDatabase
    {
        private static Dictionary<string, ToolDefinition> _tools;
        private static bool _initialized;

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _tools = null;
            _initialized = false;
        }

        public static Dictionary<string, ToolDefinition> All
        {
            get
            {
                if (!_initialized) Initialize();
                return _tools;
            }
        }

        public static ToolDefinition Get(string id)
        {
            if (!_initialized) Initialize();
            return _tools.TryGetValue(id, out var tool) ? tool : null;
        }

        public static List<ToolDefinition> GetByQuality(int quality)
        {
            if (!_initialized) Initialize();
            var result = new List<ToolDefinition>();
            foreach (var t in _tools.Values)
                if (t.Quality == quality) result.Add(t);
            return result;
        }

        private static void Initialize()
        {
            _initialized = true;
            _tools = new Dictionary<string, ToolDefinition>();

            Register(new ToolDefinition {
                Id = "simple_hand_axe", DisplayName = "Simple Hand Axe",
                Quality = 1, Recipe = new[] { "river_stone" },
                MaxDurability = 15, CraftDuration = 0f,
                GatherSpeedMultiplier = 1.0f,
                RequiredDiscoveries = new string[0]
            });
            Register(new ToolDefinition {
                Id = "knapped_hand_axe", DisplayName = "Knapped Hand Axe",
                Quality = 2, Recipe = new[] { "flint" },
                MaxDurability = 25, CraftDuration = 30f,
                GatherSpeedMultiplier = 1.3f,
                RequiredDiscoveries = new[] { "RockKnowledge" }
            });
            Register(new ToolDefinition {
                Id = "flint_blade", DisplayName = "Flint Blade",
                Quality = 3, Recipe = new[] { "flint" },
                MaxDurability = 35, CraftDuration = 45f,
                GatherSpeedMultiplier = 1.6f,
                RequiredDiscoveries = new[] { "FlintKnapping" }
            });
            Register(new ToolDefinition {
                Id = "composite_tool", DisplayName = "Composite Tool",
                Quality = 4, Recipe = new[] { "flint", "deadwood", "resin" },
                MaxDurability = 60, CraftDuration = 90f,
                GatherSpeedMultiplier = 2.0f,
                RequiredDiscoveries = new[] { "CompositeTools" }
            });
            Register(new ToolDefinition {
                Id = "specialized_tool", DisplayName = "Specialized Tool",
                Quality = 5, Recipe = new[] { "flint", "hardwood", "sinew" },
                MaxDurability = 100, CraftDuration = 120f,
                GatherSpeedMultiplier = 2.5f,
                RequiredDiscoveries = new[] { "SpecializedTools" }
            });
        }

        private static void Register(ToolDefinition tool)
        {
            _tools[tool.Id] = tool;
        }
    }
}
