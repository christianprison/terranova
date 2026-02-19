using System.Collections.Generic;
using UnityEngine;

namespace Terranova.Terrain
{
    /// <summary>
    /// v0.5.0: A natural shelter that settlers can use at night.
    /// Types: Rock Overhang, Cave Entrance, Dense Thicket.
    /// Each has a capacity (max settlers), protection value, and display name.
    /// Settlers seek the nearest available shelter (or campfire) at night.
    /// </summary>
    public class NaturalShelter : MonoBehaviour
    {
        private static readonly List<NaturalShelter> _allShelters = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { _allShelters.Clear(); }

        public string ShelterName { get; set; } = "Shelter";
        public int Capacity { get; set; } = 2;
        public float ProtectionValue { get; set; } = 0.8f; // 0-1, 1 = full protection
        public string ShelterType { get; set; } = "Rock Overhang";

        private int _occupants;

        public int Occupants => _occupants;
        public bool HasSpace => _occupants < Capacity;
        public Vector3 Position => transform.position;

        private void OnEnable() { _allShelters.Add(this); }
        private void OnDisable() { _allShelters.Remove(this); }

        /// <summary>Try to claim a spot. Returns true if space available.</summary>
        public bool TryClaim()
        {
            if (_occupants >= Capacity) return false;
            _occupants++;
            return true;
        }

        /// <summary>Release a claimed spot (dawn or settler leaves).</summary>
        public void Release()
        {
            _occupants = Mathf.Max(0, _occupants - 1);
        }

        /// <summary>Release all occupants (dawn reset).</summary>
        public void ReleaseAll() { _occupants = 0; }

        /// <summary>Find the nearest shelter with available space.</summary>
        public static NaturalShelter FindNearest(Vector3 position)
        {
            NaturalShelter best = null;
            float bestDist = float.MaxValue;
            foreach (var s in _allShelters)
            {
                if (!s.HasSpace) continue;
                float dist = Vector3.Distance(position, s.Position);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    best = s;
                }
            }
            return best;
        }

        /// <summary>Get all registered shelters (read-only).</summary>
        public static IReadOnlyList<NaturalShelter> All => _allShelters;

        /// <summary>Release all occupants from all shelters (call at dawn).</summary>
        public static void ReleaseAllOccupants()
        {
            foreach (var s in _allShelters) s.ReleaseAll();
        }
    }
}
