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

        public int Wood => _wood;
        public int Stone => _stone;
        public int Food => _food;

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
            switch (evt.TaskType)
            {
                case SettlerTaskType.GatherWood: Add(ResourceType.Wood); break;
                case SettlerTaskType.GatherStone: Add(ResourceType.Stone); break;
                case SettlerTaskType.Hunt: Add(ResourceType.Food); break;
            }
        }

        /// <summary>Check if we have enough resources for a purchase.</summary>
        public bool CanAfford(int woodCost, int stoneCost)
        {
            return _wood >= woodCost && _stone >= stoneCost;
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
                case ResourceType.Wood:  _wood += amount; break;
                case ResourceType.Stone: _stone += amount; break;
                case ResourceType.Food:  _food += amount; break;
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
