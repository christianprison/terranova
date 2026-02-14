using System.Collections.Generic;
using UnityEngine;
using Terranova.Core;
using Terranova.Buildings;

namespace Terranova.Discovery
{
    /// <summary>
    /// Tracks which discoveries have been completed and prevents re-discovery.
    ///
    /// Story 1.5: Discovery State Manager
    ///
    /// Maintains the set of completed discoveries and fires DiscoveryMadeEvent
    /// on the EventBus when a new discovery is made. Other systems can query
    /// completed discoveries and unlocked capabilities.
    /// </summary>
    public class DiscoveryStateManager : MonoBehaviour
    {
        private readonly HashSet<string> _completedDiscoveries = new();
        private readonly HashSet<string> _unlockedCapabilities = new();
        private readonly HashSet<BuildingType> _unlockedBuildings = new();

        /// <summary>Singleton instance for easy access.</summary>
        public static DiscoveryStateManager Instance { get; private set; }

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
        /// Check if a discovery has already been completed.
        /// </summary>
        public bool IsDiscovered(string discoveryName)
        {
            return _completedDiscoveries.Contains(discoveryName);
        }

        /// <summary>
        /// Check if a capability has been unlocked by any discovery.
        /// </summary>
        public bool HasCapability(string capability)
        {
            return _unlockedCapabilities.Contains(capability);
        }

        /// <summary>
        /// Check if a building type has been unlocked by discovery.
        /// </summary>
        public bool IsBuildingUnlocked(BuildingType type)
        {
            return _unlockedBuildings.Contains(type);
        }

        /// <summary>
        /// Register a discovery as completed. Fires DiscoveryMadeEvent.
        /// Returns false if already discovered.
        /// </summary>
        public bool CompleteDiscovery(DiscoveryDefinition definition)
        {
            if (_completedDiscoveries.Contains(definition.DisplayName))
                return false;

            _completedDiscoveries.Add(definition.DisplayName);

            // Register unlocks
            if (definition.UnlockedCapabilities != null)
            {
                foreach (var cap in definition.UnlockedCapabilities)
                    _unlockedCapabilities.Add(cap);
            }
            if (definition.UnlockedBuildings != null)
            {
                foreach (var bt in definition.UnlockedBuildings)
                    _unlockedBuildings.Add(bt);
            }

            // Fire event
            EventBus.Publish(new DiscoveryMadeEvent
            {
                DiscoveryName = definition.DisplayName,
                Description = definition.Description
            });

            Debug.Log($"[Discovery] Discovered: {definition.DisplayName}");
            return true;
        }

        /// <summary>Number of completed discoveries.</summary>
        public int CompletedCount => _completedDiscoveries.Count;

        /// <summary>All completed discovery names (for serialization/UI).</summary>
        public IReadOnlyCollection<string> CompletedDiscoveries => _completedDiscoveries;
    }
}
