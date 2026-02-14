using UnityEngine;
using Terranova.Core;
using Terranova.Terrain;
using Terranova.Buildings;

namespace Terranova.Discovery
{
    /// <summary>
    /// Type of trigger condition for a discovery.
    /// </summary>
    public enum DiscoveryType
    {
        Biome,       // Requires specific terrain biomes near settlement
        Activity,    // Requires settlers performing specific activities
        Spontaneous  // Can happen at any time with base probability
    }

    /// <summary>
    /// ScriptableObject defining a single discovery that settlers can make.
    ///
    /// Story 1.1: Discovery Data Model
    ///
    /// Each discovery has:
    /// - Trigger conditions (biome presence, activity counts)
    /// - Probability parameters (base chance, repetition scaling, bad luck cap)
    /// - Rewards (unlocked buildings, resources, capabilities)
    /// </summary>
    [CreateAssetMenu(fileName = "NewDiscovery", menuName = "Terranova/Discovery")]
    public class DiscoveryDefinition : ScriptableObject
    {
        // ─── Identity ───────────────────────────────────────────
        [Header("Identity")]
        [Tooltip("Display name shown in discovery popup.")]
        public string DisplayName;

        [Tooltip("Description of what was discovered.")]
        [TextArea] public string Description;

        [Tooltip("Flavor text for immersion.")]
        [TextArea] public string FlavorText;

        // ─── Type & Requirements ────────────────────────────────
        [Header("Type & Requirements")]
        [Tooltip("What triggers this discovery.")]
        public DiscoveryType Type;

        [Tooltip("Required biomes near settlement (for Biome type).")]
        public VoxelType[] RequiredBiomes;

        [Tooltip("Required activity type (for Activity type).")]
        public SettlerTaskType RequiredActivity;

        [Tooltip("Number of times activity must be performed before eligible.")]
        public int RequiredActivityCount;

        // ─── Probability ────────────────────────────────────────
        [Header("Probability")]
        [Tooltip("Base probability per check cycle (0-1).")]
        [Range(0f, 1f)] public float BaseProbability = 0.1f;

        [Tooltip("Bonus added to probability each cycle the discovery is eligible.")]
        public float RepetitionBonus = 0.02f;

        [Tooltip("Force discovery after this many cycles without any discovery.")]
        public int BadLuckThreshold = 50;

        // ─── Unlocks ────────────────────────────────────────────
        [Header("Unlocks")]
        [Tooltip("Building types unlocked by this discovery.")]
        public BuildingType[] UnlockedBuildings;

        [Tooltip("Resource types unlocked by this discovery.")]
        public ResourceType[] UnlockedResources;

        [Tooltip("Named capabilities unlocked (e.g. 'fire', 'tools').")]
        public string[] UnlockedCapabilities;
    }
}
