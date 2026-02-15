using System.Collections.Generic;
using UnityEngine;

namespace Terranova.Core
{
    /// <summary>
    /// Tracks the settlement's material inventory using MaterialDefinitions.
    /// Replaces the old simple resource counters with a categorized system.
    /// MS4 Feature 2.4: Resource UI Update.
    /// </summary>
    public class MaterialInventory : MonoBehaviour
    {
        public static MaterialInventory Instance { get; private set; }

        // materialId -> count
        private readonly Dictionary<string, int> _stock = new();
        // materialId -> discovered (can be individually identified)
        private readonly HashSet<string> _discoveredMaterials = new();
        // discovery name -> unlocked
        private readonly HashSet<string> _unlockedDiscoveries = new();

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        public void Add(string materialId, int amount = 1)
        {
            if (!_stock.ContainsKey(materialId))
                _stock[materialId] = 0;
            _stock[materialId] += amount;

            // Also update legacy ResourceManager for backward compat
            var mat = MaterialDatabase.Get(materialId);
            if (mat != null && ResourceManager.Instance != null)
            {
                var legacyType = CategoryToLegacy(mat.Category);
                ResourceManager.Instance.Add(legacyType, amount);
            }

            EventBus.Publish(new ResourceChangedEvent { ResourceName = materialId, NewAmount = GetCount(materialId) });
        }

        public int GetCount(string materialId)
        {
            return _stock.TryGetValue(materialId, out var count) ? count : 0;
        }

        public int GetCategoryTotal(MaterialCategory category)
        {
            int total = 0;
            foreach (var kvp in _stock)
            {
                var mat = MaterialDatabase.Get(kvp.Key);
                if (mat != null && mat.Category == category)
                    total += kvp.Value;
            }
            return total;
        }

        public Dictionary<string, int> GetCategoryBreakdown(MaterialCategory category)
        {
            var result = new Dictionary<string, int>();
            foreach (var kvp in _stock)
            {
                if (kvp.Value <= 0) continue;
                var mat = MaterialDatabase.Get(kvp.Key);
                if (mat != null && mat.Category == category)
                    result[kvp.Key] = kvp.Value;
            }
            return result;
        }

        public bool TryConsume(string materialId, int amount = 1)
        {
            if (GetCount(materialId) < amount) return false;
            _stock[materialId] -= amount;
            EventBus.Publish(new ResourceChangedEvent { ResourceName = materialId, NewAmount = GetCount(materialId) });
            return true;
        }

        /// <summary>Try to consume any edible material. Returns the material consumed or null.</summary>
        public MaterialDefinition TryConsumeAnyFood()
        {
            // Prefer higher nutrition foods
            MaterialDefinition best = null;
            string bestId = null;
            foreach (var kvp in _stock)
            {
                if (kvp.Value <= 0) continue;
                var mat = MaterialDatabase.Get(kvp.Key);
                if (mat != null && mat.IsEdible && (best == null || mat.NutritionValue > best.NutritionValue))
                {
                    best = mat;
                    bestId = kvp.Key;
                }
            }
            if (best != null && bestId != null)
            {
                _stock[bestId]--;
                EventBus.Publish(new ResourceChangedEvent { ResourceName = bestId, NewAmount = GetCount(bestId) });
                return best;
            }
            return null;
        }

        public bool HasAnyFood()
        {
            foreach (var kvp in _stock)
            {
                if (kvp.Value <= 0) continue;
                var mat = MaterialDatabase.Get(kvp.Key);
                if (mat != null && mat.IsEdible) return true;
            }
            return false;
        }

        public void UnlockDiscovery(string discoveryName)
        {
            _unlockedDiscoveries.Add(discoveryName);
        }

        public bool IsDiscoveryUnlocked(string discoveryName)
        {
            return string.IsNullOrEmpty(discoveryName) || _unlockedDiscoveries.Contains(discoveryName);
        }

        public bool IsMaterialDiscovered(string materialId)
        {
            var mat = MaterialDatabase.Get(materialId);
            if (mat == null) return false;
            return IsDiscoveryUnlocked(mat.DiscoveryRequired);
        }

        /// <summary>Get display name for a material, respecting discovery state.</summary>
        public string GetDisplayName(string materialId)
        {
            var mat = MaterialDatabase.Get(materialId);
            if (mat == null) return materialId;
            return IsMaterialDiscovered(materialId) ? mat.DisplayName : mat.GenericName;
        }

        private static ResourceType CategoryToLegacy(MaterialCategory cat)
        {
            return cat switch
            {
                MaterialCategory.Wood => ResourceType.Wood,
                MaterialCategory.Stone => ResourceType.Stone,
                MaterialCategory.Plant => ResourceType.Food,
                MaterialCategory.Animal => ResourceType.Food,
                _ => ResourceType.Wood
            };
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
