using System.Collections.Generic;
using UnityEngine;
using Terranova.Core;

namespace Terranova.Discovery
{
    /// <summary>
    /// Tracks per-settler and global activity counts for the discovery system.
    ///
    /// Story 1.2: Activity Tracking
    ///
    /// Subscribes to ResourceDeliveredEvent and SettlerDiedEvent to maintain
    /// accurate counts. The discovery engine queries these counts to evaluate
    /// activity-based discovery eligibility.
    /// </summary>
    public class ActivityTracker : MonoBehaviour
    {
        // Global activity counts (across all settlers, all time)
        private readonly Dictionary<SettlerTaskType, int> _globalCounts = new();

        /// <summary>Singleton instance for easy access.</summary>
        public static ActivityTracker Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnEnable()
        {
            EventBus.Subscribe<ResourceDeliveredEvent>(OnResourceDelivered);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<ResourceDeliveredEvent>(OnResourceDelivered);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void OnResourceDelivered(ResourceDeliveredEvent evt)
        {
            if (!_globalCounts.ContainsKey(evt.TaskType))
                _globalCounts[evt.TaskType] = 0;
            _globalCounts[evt.TaskType]++;
        }

        /// <summary>
        /// Get the global count for a specific activity type.
        /// </summary>
        public int GetGlobalCount(SettlerTaskType taskType)
        {
            return _globalCounts.TryGetValue(taskType, out int count) ? count : 0;
        }

        /// <summary>
        /// Get all global activity counts (for debug/UI).
        /// </summary>
        public IReadOnlyDictionary<SettlerTaskType, int> GlobalCounts => _globalCounts;
    }
}
