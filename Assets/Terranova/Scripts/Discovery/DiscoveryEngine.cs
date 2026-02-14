using System.Collections.Generic;
using UnityEngine;
using Terranova.Core;
using Terranova.Terrain;

namespace Terranova.Discovery
{
    /// <summary>
    /// Core probability engine that evaluates and triggers discoveries.
    ///
    /// Story 1.3: Probability Engine
    /// Story 1.4: Bad Luck Protection
    ///
    /// Every CHECK_INTERVAL game-seconds, evaluates all undiscovered discoveries:
    ///   probability = base * biome_mod * activity_mod + (cycles_eligible * repetition_bonus)
    ///
    /// Bad luck protection: after BadLuckThreshold cycles without ANY discovery,
    /// the highest-probability eligible discovery is forced. Counter resets on discovery.
    /// </summary>
    public class DiscoveryEngine : MonoBehaviour
    {
        private const float CHECK_INTERVAL = 60f; // Game-seconds between checks

        // Radius around campfire to scan for biomes
        private const int BIOME_SCAN_RADIUS = 20;

        private float _checkTimer;
        private int _cyclesWithoutDiscovery;

        // Per-discovery tracking: how many cycles each has been eligible
        private readonly Dictionary<string, int> _eligibleCycles = new();

        // All registered discoveries
        private readonly List<DiscoveryDefinition> _allDiscoveries = new();

        /// <summary>Singleton instance.</summary>
        public static DiscoveryEngine Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>
        /// Register a discovery definition for evaluation.
        /// Called by DiscoveryRegistry during initialization.
        /// </summary>
        public void RegisterDiscovery(DiscoveryDefinition definition)
        {
            _allDiscoveries.Add(definition);
        }

        private void Update()
        {
            _checkTimer += Time.deltaTime;
            if (_checkTimer < CHECK_INTERVAL) return;
            _checkTimer -= CHECK_INTERVAL;

            EvaluateDiscoveries();
        }

        /// <summary>
        /// Run one evaluation cycle across all undiscovered discoveries.
        /// </summary>
        private void EvaluateDiscoveries()
        {
            var stateManager = DiscoveryStateManager.Instance;
            var activityTracker = ActivityTracker.Instance;
            if (stateManager == null) return;

            // Gather biome data around campfire
            var biomes = ScanBiomesAroundCampfire();

            bool anyDiscoveredThisCycle = false;
            float highestProb = 0f;
            DiscoveryDefinition bestCandidate = null;

            foreach (var discovery in _allDiscoveries)
            {
                // Skip already discovered
                if (stateManager.IsDiscovered(discovery.DisplayName)) continue;

                // Check eligibility
                float probability = CalculateProbability(discovery, biomes, activityTracker);

                if (probability <= 0f) continue;

                // Track eligible cycles for repetition bonus
                if (!_eligibleCycles.ContainsKey(discovery.DisplayName))
                    _eligibleCycles[discovery.DisplayName] = 0;
                _eligibleCycles[discovery.DisplayName]++;

                // Apply repetition bonus
                int cycles = _eligibleCycles[discovery.DisplayName];
                float finalProb = probability + cycles * discovery.RepetitionBonus;
                finalProb = Mathf.Clamp01(finalProb);

                // Track best candidate for bad luck protection
                if (finalProb > highestProb)
                {
                    highestProb = finalProb;
                    bestCandidate = discovery;
                }

                // Roll against probability
                if (Random.value < finalProb)
                {
                    TriggerDiscovery(discovery, stateManager);
                    anyDiscoveredThisCycle = true;
                    break; // One discovery per cycle
                }
            }

            // Bad luck protection (Story 1.4)
            if (!anyDiscoveredThisCycle)
            {
                _cyclesWithoutDiscovery++;

                if (bestCandidate != null &&
                    _cyclesWithoutDiscovery >= bestCandidate.BadLuckThreshold)
                {
                    Debug.Log($"[Discovery] Bad luck protection triggered after {_cyclesWithoutDiscovery} cycles.");
                    TriggerDiscovery(bestCandidate, stateManager);
                }
            }
        }

        /// <summary>
        /// Calculate base probability for a discovery based on its type and conditions.
        /// Returns 0 if prerequisites are not met.
        /// </summary>
        private float CalculateProbability(
            DiscoveryDefinition discovery,
            HashSet<VoxelType> availableBiomes,
            ActivityTracker tracker)
        {
            float baseProbability = discovery.BaseProbability;

            switch (discovery.Type)
            {
                case DiscoveryType.Biome:
                    // All required biomes must be present
                    if (discovery.RequiredBiomes != null)
                    {
                        foreach (var biome in discovery.RequiredBiomes)
                        {
                            if (!availableBiomes.Contains(biome))
                                return 0f; // Missing required biome
                        }
                    }
                    return baseProbability;

                case DiscoveryType.Activity:
                    // Required activity count must be met
                    if (tracker == null) return 0f;
                    int count = tracker.GetGlobalCount(discovery.RequiredActivity);
                    if (count < discovery.RequiredActivityCount)
                        return 0f; // Not enough activity yet
                    // Scale probability with how much the threshold has been exceeded
                    float activityMod = Mathf.Min(2f, (float)count / discovery.RequiredActivityCount);
                    return baseProbability * activityMod;

                case DiscoveryType.Spontaneous:
                    return baseProbability;

                default:
                    return 0f;
            }
        }

        private void TriggerDiscovery(DiscoveryDefinition discovery, DiscoveryStateManager stateManager)
        {
            stateManager.CompleteDiscovery(discovery);
            _cyclesWithoutDiscovery = 0;
            _eligibleCycles.Remove(discovery.DisplayName);
        }

        /// <summary>
        /// Scan terrain biomes in a radius around the campfire.
        /// Returns the set of unique biome types found.
        /// </summary>
        private HashSet<VoxelType> ScanBiomesAroundCampfire()
        {
            var biomes = new HashSet<VoxelType>();
            var world = WorldManager.Instance;
            var campfire = GameObject.Find("Campfire");

            if (world == null || campfire == null) return biomes;

            Vector3 pos = campfire.transform.position;
            int cx = Mathf.FloorToInt(pos.x);
            int cz = Mathf.FloorToInt(pos.z);

            for (int dx = -BIOME_SCAN_RADIUS; dx <= BIOME_SCAN_RADIUS; dx += 4)
            {
                for (int dz = -BIOME_SCAN_RADIUS; dz <= BIOME_SCAN_RADIUS; dz += 4)
                {
                    int wx = cx + dx;
                    int wz = cz + dz;
                    if (wx < 0 || wz < 0 || wx >= world.WorldBlocksX || wz >= world.WorldBlocksZ)
                        continue;

                    VoxelType surface = world.GetSurfaceTypeAtWorldPos(wx, wz);
                    biomes.Add(surface);
                }
            }

            return biomes;
        }
    }
}
