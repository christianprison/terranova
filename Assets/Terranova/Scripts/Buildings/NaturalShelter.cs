using UnityEngine;
using Terranova.Core;

namespace Terranova.Buildings
{
    /// <summary>
    /// A natural shelter spawned on the terrain.
    /// Feature 5.1: Terrain-Generated Shelters.
    ///
    /// Types: Cave, RockOverhang, DenseUndergrowth, FallenTree.
    /// Each has a protection value, capacity, and discovery state.
    /// </summary>
    public class NaturalShelter : MonoBehaviour
    {
        private NaturalShelterType _shelterType;
        private float _protectionValue;
        private int _capacity;
        private int _currentOccupants;
        private bool _isDiscovered;

        /// <summary>Type of this natural shelter.</summary>
        public NaturalShelterType ShelterType => _shelterType;

        /// <summary>Protection value from 0 (none) to 1 (perfect).</summary>
        public float ProtectionValue => _protectionValue;

        /// <summary>Maximum number of settlers that can shelter here.</summary>
        public int Capacity => _capacity;

        /// <summary>Current number of settlers using this shelter.</summary>
        public int CurrentOccupants => _currentOccupants;

        /// <summary>Whether settlers have found this shelter.</summary>
        public bool IsDiscovered => _isDiscovered;

        /// <summary>Whether there is room for more settlers.</summary>
        public bool HasRoom => _currentOccupants < _capacity;

        /// <summary>Display name for UI.</summary>
        public string DisplayName => _shelterType switch
        {
            NaturalShelterType.Cave => "Cave",
            NaturalShelterType.RockOverhang => "Rock Overhang",
            NaturalShelterType.DenseUndergrowth => "Dense Undergrowth",
            NaturalShelterType.FallenTree => "Fallen Tree",
            _ => "Shelter"
        };

        /// <summary>
        /// Initialize the shelter with type-specific properties.
        /// </summary>
        public void Initialize(NaturalShelterType type)
        {
            _shelterType = type;

            switch (type)
            {
                case NaturalShelterType.Cave:
                    _protectionValue = 0.95f;
                    _capacity = Random.Range(5, 11);
                    break;
                case NaturalShelterType.RockOverhang:
                    _protectionValue = 0.7f;
                    _capacity = Random.Range(3, 6);
                    break;
                case NaturalShelterType.DenseUndergrowth:
                    _protectionValue = 0.4f;
                    _capacity = Random.Range(2, 4);
                    break;
                case NaturalShelterType.FallenTree:
                    _protectionValue = 0.45f;
                    _capacity = Random.Range(2, 4);
                    break;
            }
        }

        /// <summary>
        /// Mark this shelter as discovered. Publishes ShelterDiscoveredEvent.
        /// </summary>
        public void Discover()
        {
            if (_isDiscovered) return;
            _isDiscovered = true;

            EventBus.Publish(new ShelterDiscoveredEvent
            {
                ShelterName = DisplayName,
                ShelterType = _shelterType,
                Position = transform.position
            });

            Debug.Log($"[Shelter] Discovered {DisplayName} at ({transform.position.x:F0}, {transform.position.z:F0})");
        }

        /// <summary>Reserve a spot in this shelter. Returns false if full.</summary>
        public bool TryOccupy()
        {
            if (_currentOccupants >= _capacity) return false;
            _currentOccupants++;
            return true;
        }

        /// <summary>Release a spot in this shelter.</summary>
        public void Vacate()
        {
            _currentOccupants = Mathf.Max(0, _currentOccupants - 1);
        }

        /// <summary>
        /// Priority for shelter-seeking. Higher = more desirable.
        /// Cave > RockOverhang > built structure > undergrowth > open.
        /// </summary>
        public int Priority => _shelterType switch
        {
            NaturalShelterType.Cave => 100,
            NaturalShelterType.RockOverhang => 80,
            NaturalShelterType.DenseUndergrowth => 40,
            NaturalShelterType.FallenTree => 40,
            _ => 0
        };
    }
}
