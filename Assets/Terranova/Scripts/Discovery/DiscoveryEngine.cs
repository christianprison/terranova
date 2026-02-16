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
    /// Feature 2: GDD Epoch I.1 discoveries (biome, activity, spontaneous, lightning)
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

        // Lightning Fire: random interval between lightning attempts
        private const float LIGHTNING_MIN_INTERVAL = 120f;
        private const float LIGHTNING_MAX_INTERVAL = 300f;
        private const float LIGHTNING_SETTLER_RANGE = 15f;

        private float _checkTimer;
        private int _cyclesWithoutDiscovery;
        private float _lightningTimer;

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
            _lightningTimer = Random.Range(LIGHTNING_MIN_INTERVAL, LIGHTNING_MAX_INTERVAL);
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
            if (_checkTimer >= CHECK_INTERVAL)
            {
                _checkTimer -= CHECK_INTERVAL;
                EvaluateDiscoveries();
            }

            // Lightning Fire system
            UpdateLightning();
        }

        /// <summary>
        /// Lightning Fire: periodically attempt a lightning strike on a tree.
        /// If a settler is nearby, Fire is discovered. Otherwise, missed opportunity.
        /// Feature 2.3: Spontaneous discovery.
        /// </summary>
        private void UpdateLightning()
        {
            var stateManager = DiscoveryStateManager.Instance;
            if (stateManager == null) return;

            // Skip if any fire discovery already made
            if (stateManager.HasCapability("fire")) return;

            _lightningTimer -= Time.deltaTime;
            if (_lightningTimer > 0f) return;

            _lightningTimer = Random.Range(LIGHTNING_MIN_INTERVAL, LIGHTNING_MAX_INTERVAL);

            // Find a random tree in the world
            var trees = FindTreesInWorld();
            if (trees.Count == 0) return;

            var targetTree = trees[Random.Range(0, trees.Count)];
            Vector3 strikePos = targetTree.transform.position;

            Debug.Log($"[Discovery] Lightning strikes tree at ({strikePos.x:F0}, {strikePos.z:F0})!");

            // Check if any settler is within range
            bool settlerNearby = false;
            foreach (var settler in SettlerLocator.ActiveSettlers)
            {
                if (settler == null) continue;
                if (Vector3.Distance(settler.position, strikePos) <= LIGHTNING_SETTLER_RANGE)
                {
                    settlerNearby = true;
                    break;
                }
            }

            if (settlerNearby)
            {
                // Find the Lightning Fire discovery and trigger it
                foreach (var discovery in _allDiscoveries)
                {
                    if (discovery.DisplayName == "Lightning Fire")
                    {
                        TriggerDiscovery(discovery, stateManager, "after witnessing a lightning strike");
                        Debug.Log("[Discovery] A settler witnessed lightning strike a tree — Fire discovered!");
                        return;
                    }
                }
            }
            else
            {
                Debug.Log("[Discovery] Lightning struck but no settler was nearby to witness it — missed opportunity!");
            }
        }

        private List<GameObject> FindTreesInWorld()
        {
            var result = new List<GameObject>();
            var parent = GameObject.Find("Resources");
            if (parent == null) return result;

            foreach (Transform child in parent.transform)
            {
                if (child.name.StartsWith("Tree_"))
                    result.Add(child.gameObject);
            }
            return result;
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

                // Check prerequisite discoveries
                if (!ArePrerequisitesMet(discovery, stateManager)) continue;

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
                    string reason = BuildDiscoveryReason(discovery, tracker);
                    TriggerDiscovery(discovery, stateManager, reason);
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
                    string reason = BuildDiscoveryReason(bestCandidate, activityTracker);
                    TriggerDiscovery(bestCandidate, stateManager, reason);
                }
            }
        }

        /// <summary>
        /// Check if all prerequisite discoveries have been completed.
        /// </summary>
        private bool ArePrerequisitesMet(DiscoveryDefinition discovery, DiscoveryStateManager stateManager)
        {
            if (discovery.PrerequisiteDiscoveries == null || discovery.PrerequisiteDiscoveries.Length == 0)
                return true;

            foreach (var prereq in discovery.PrerequisiteDiscoveries)
            {
                if (!stateManager.IsDiscovered(prereq))
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Calculate base probability for a discovery based on its type and conditions.
        /// Returns 0 if prerequisites are not met.
        ///
        /// Biome type: all required biomes must be present; activity requirements are optional bonus.
        /// Activity type: required activity count must be met; biome requirements are optional bonus.
        /// Spontaneous: base probability (lightning handled separately).
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
                    // Optional activity requirement for biome discoveries
                    if (discovery.RequiredActivity != SettlerTaskType.None && tracker != null)
                    {
                        int count = tracker.GetGlobalCount(discovery.RequiredActivity);
                        if (count < discovery.RequiredActivityCount)
                            return 0f;
                    }
                    return baseProbability;

                case DiscoveryType.Activity:
                    // Required activity count must be met
                    if (tracker == null) return 0f;
                    int actCount = tracker.GetGlobalCount(discovery.RequiredActivity);
                    if (actCount < discovery.RequiredActivityCount)
                        return 0f; // Not enough activity yet
                    // Optional biome requirement for activity discoveries
                    if (discovery.RequiredBiomes != null && discovery.RequiredBiomes.Length > 0)
                    {
                        foreach (var biome in discovery.RequiredBiomes)
                        {
                            if (!availableBiomes.Contains(biome))
                                return 0f;
                        }
                    }
                    // Scale probability with how much the threshold has been exceeded
                    float activityMod = Mathf.Min(2f, (float)actCount / discovery.RequiredActivityCount);
                    return baseProbability * activityMod;

                case DiscoveryType.Spontaneous:
                    return baseProbability;

                default:
                    return 0f;
            }
        }

        private void TriggerDiscovery(DiscoveryDefinition discovery, DiscoveryStateManager stateManager, string reason = null)
        {
            stateManager.CompleteDiscovery(discovery, reason);
            _cyclesWithoutDiscovery = 0;
            _eligibleCycles.Remove(discovery.DisplayName);
        }

        /// <summary>Build a human-readable reason for the discovery trigger.</summary>
        private string BuildDiscoveryReason(DiscoveryDefinition discovery, ActivityTracker tracker)
        {
            // Pick the best settler name for the toast
            string settlerName = "A settler";
            var settlers = SettlerLocator.ActiveSettlers;
            if (settlers != null && settlers.Count > 0)
            {
                int idx = Random.Range(0, settlers.Count);
                if (settlers[idx] != null) settlerName = settlers[idx].name;
            }

            switch (discovery.Type)
            {
                case DiscoveryType.Activity:
                    string actName = discovery.RequiredActivity.ToString().ToLower();
                    actName = actName.Replace("gather", "gathering ");
                    actName = actName.Replace("hunt", "foraging");
                    actName = actName.Replace("build", "building");
                    return $"{settlerName} discovered this from {actName}";

                case DiscoveryType.Biome:
                    return $"{settlerName} discovered this by exploring the terrain";

                case DiscoveryType.Spontaneous:
                    return $"{settlerName} stumbled upon this by chance";

                default:
                    return $"{settlerName} made a discovery";
            }
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
