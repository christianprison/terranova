using System.Collections.Generic;
using UnityEngine;
using Terranova.Core;

namespace Terranova.Buildings
{
    /// <summary>
    /// Singleton managing all shelters (natural + built).
    /// Feature 5.1/5.2: Shelter tracking and settler assignment.
    /// </summary>
    public class ShelterManager : MonoBehaviour
    {
        public static ShelterManager Instance { get; private set; }

        private readonly List<NaturalShelter> _naturalShelters = new();
        private readonly HashSet<NaturalShelter> _discoveredShelters = new();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        /// <summary>Register a natural shelter.</summary>
        public void Register(NaturalShelter shelter)
        {
            if (!_naturalShelters.Contains(shelter))
                _naturalShelters.Add(shelter);
        }

        /// <summary>Unregister a natural shelter (destroyed).</summary>
        public void Unregister(NaturalShelter shelter)
        {
            _naturalShelters.Remove(shelter);
            _discoveredShelters.Remove(shelter);
        }

        /// <summary>Mark a shelter as discovered by settlers.</summary>
        public void MarkDiscovered(NaturalShelter shelter)
        {
            if (shelter == null) return;
            _discoveredShelters.Add(shelter);
            shelter.Discover();
        }

        /// <summary>All registered natural shelters.</summary>
        public IReadOnlyList<NaturalShelter> AllShelters => _naturalShelters;

        /// <summary>All discovered shelters.</summary>
        public IReadOnlyCollection<NaturalShelter> DiscoveredShelters => _discoveredShelters;

        /// <summary>
        /// Find the best available shelter nearest to the given position.
        /// Only considers discovered shelters with room.
        /// Also considers built structures with ShelterCapacity > 0.
        /// Returns null if no shelter available.
        /// </summary>
        public (Vector3 position, float protection)? FindBestShelter(Vector3 fromPosition)
        {
            float bestScore = -1f;
            Vector3 bestPos = Vector3.zero;
            float bestProtection = 0f;

            // Check natural shelters (discovered only)
            foreach (var shelter in _discoveredShelters)
            {
                if (shelter == null || !shelter.HasRoom) continue;

                float dist = Vector3.Distance(fromPosition, shelter.transform.position);
                // Score: priority / distance (prefer close high-priority shelters)
                float score = shelter.Priority / Mathf.Max(1f, dist);

                if (score > bestScore)
                {
                    bestScore = score;
                    bestPos = shelter.transform.position;
                    bestProtection = shelter.ProtectionValue;
                }
            }

            // Check built structures with shelter capacity
            var buildings = Object.FindObjectsByType<Building>(FindObjectsSortMode.None);
            foreach (var building in buildings)
            {
                if (!building.IsConstructed) continue;
                if (building.Definition == null) continue;
                if (building.Definition.ShelterCapacity <= 0) continue;

                float dist = Vector3.Distance(fromPosition, building.transform.position);
                // Built structures get priority 60 (between overhang and undergrowth)
                float score = 60f / Mathf.Max(1f, dist);

                if (score > bestScore)
                {
                    bestScore = score;
                    bestPos = building.EntrancePosition;
                    bestProtection = building.Definition.ProtectionValue;
                }
            }

            if (bestScore > 0f)
                return (bestPos, bestProtection);

            return null;
        }

        /// <summary>
        /// Check if a settler is near any undiscovered shelter and discover it.
        /// Called periodically from settler update.
        /// </summary>
        public void TryDiscoverNearbyShelters(Vector3 settlerPosition, float discoveryRadius = 10f)
        {
            foreach (var shelter in _naturalShelters)
            {
                if (shelter == null || shelter.IsDiscovered) continue;

                float dist = Vector3.Distance(settlerPosition, shelter.transform.position);
                if (dist <= discoveryRadius)
                {
                    MarkDiscovered(shelter);
                }
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
