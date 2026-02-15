using UnityEngine;

namespace Terranova.Core
{
    /// <summary>
    /// Central resource storage for the settlement.
    ///
    /// All resource changes go through this manager so every system
    /// sees the same values. Publishes ResourceChangedEvent on changes.
    ///
    /// Story 4.1: Baukosten-System
    /// </summary>
    public class ResourceManager : MonoBehaviour
    {
        /// <summary>Singleton instance, set by GameBootstrapper.</summary>
        public static ResourceManager Instance { get; private set; }

        [Header("Starting Resources")]
        [SerializeField] private int _wood = 50;
        [SerializeField] private int _stone = 30;
        [SerializeField] private int _food;
        private int _resin;
        private int _flint;
        private int _plantFiber;

        public int Wood => _wood;
        public int Stone => _stone;
        public int Food => _food;
        public int Resin => _resin;
        public int Flint => _flint;
        public int PlantFiber => _plantFiber;

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

        /// <summary>
        /// When a settler delivers a resource, add it to our stock.
        /// </summary>
        private void OnResourceDelivered(ResourceDeliveredEvent evt)
        {
            Add(evt.ActualResourceType);
        }

        /// <summary>Check if we have enough resources for a purchase.</summary>
        public bool CanAfford(int woodCost, int stoneCost)
        {
            return _wood >= woodCost && _stone >= stoneCost;
        }

        /// <summary>Check if we have enough resources including fiber cost.</summary>
        public bool CanAfford(int woodCost, int stoneCost, int fiberCost)
        {
            return _wood >= woodCost && _stone >= stoneCost && _plantFiber >= fiberCost;
        }

        /// <summary>
        /// Deduct resources. Returns false if not enough available.
        /// Publishes ResourceChangedEvent on success.
        /// </summary>
        public bool Spend(int woodCost, int stoneCost)
        {
            if (!CanAfford(woodCost, stoneCost))
                return false;

            _wood -= woodCost;
            _stone -= stoneCost;
            PublishChanged();
            return true;
        }

        /// <summary>Deduct resources including fiber. Returns false if not enough.</summary>
        public bool Spend(int woodCost, int stoneCost, int fiberCost)
        {
            if (!CanAfford(woodCost, stoneCost, fiberCost))
                return false;

            _wood -= woodCost;
            _stone -= stoneCost;
            _plantFiber -= fiberCost;
            PublishChanged();
            return true;
        }

        /// <summary>Partially spoil food (used by tribe restart). Removes a fraction.</summary>
        public void SpoilFood(float fraction)
        {
            int spoiled = Mathf.FloorToInt(_food * fraction);
            _food -= spoiled;
            if (_food < 0) _food = 0;
            PublishChanged();
        }

        /// <summary>
        /// Try to consume one unit of food. Returns true if food was available.
        /// Story 5.2: Settlers eat when hungry.
        /// </summary>
        public bool TryConsumeFood()
        {
            if (_food <= 0) return false;
            _food--;
            PublishChanged();
            return true;
        }

        /// <summary>Add a gathered resource. Publishes ResourceChangedEvent.</summary>
        public void Add(ResourceType type, int amount = 1)
        {
            switch (type)
            {
                case ResourceType.Wood:      _wood += amount; break;
                case ResourceType.Stone:     _stone += amount; break;
                case ResourceType.Food:      _food += amount; break;
                case ResourceType.Resin:     _resin += amount; break;
                case ResourceType.Flint:     _flint += amount; break;
                case ResourceType.PlantFiber: _plantFiber += amount; break;
            }
            PublishChanged();
        }

        private void PublishChanged()
        {
            EventBus.Publish(new ResourceChangedEvent
            {
                ResourceName = "All",
                NewAmount = 0
            });
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }
    }
}
