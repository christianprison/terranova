using System;
using System.Collections.Generic;
using UnityEngine;
using Terranova.Core;
using Terranova.Discovery;

namespace Terranova.Orders
{
    /// <summary>
    /// Feature 7.2: Tracks which predicates and objects are available to the player.
    ///
    /// Start vocabulary is always available. Discoveries unlock new words.
    /// Listens to DiscoveryMadeEvent to expand the vocabulary.
    ///
    /// Discovery → Vocabulary mapping:
    ///   "Rock Knowledge"    → Objects: Flint, Sandstone, Granite
    ///   "Clubs for Defense" → Predicates: Hunt (small)
    ///   "Wickerwork"        → Predicates: Build; Objects: Windscreen, Basket
    ///   "Fire"              → Predicates: Cook, Smoke; Objects: Fireplace
    ///   "Composite Tool"    → Predicates: Fell, Dig, Craft
    /// </summary>
    public class OrderVocabulary : MonoBehaviour
    {
        public static OrderVocabulary Instance { get; private set; }

        // ─── Start Vocabulary (always available) ─────────────

        private static readonly OrderObject[] START_OBJECTS =
        {
            new("here", "Here", OrderObjectCategory.Location),
            new("everything_nearby", "Everything nearby", OrderObjectCategory.Location),
            new("north", "North", OrderObjectCategory.Direction),
            new("south", "South", OrderObjectCategory.Direction),
            new("east", "East", OrderObjectCategory.Direction),
            new("west", "West", OrderObjectCategory.Direction),
        };

        // ─── Discovery → Unlock mappings ─────────────────────

        private static readonly Dictionary<string, OrderPredicate[]> DISCOVERY_PREDICATES = new()
        {
            ["Clubs for Defense"] = new[] { OrderPredicate.Hunt },
            ["Wickerwork"] = new[] { OrderPredicate.Build },
            ["Fire"] = new[] { OrderPredicate.Cook, OrderPredicate.Smoke },
            ["Composite Tool"] = new[] { OrderPredicate.Fell, OrderPredicate.Dig, OrderPredicate.Craft },
        };

        private static readonly Dictionary<string, OrderObject[]> DISCOVERY_OBJECTS = new()
        {
            ["Rock Knowledge"] = new[]
            {
                new OrderObject("flint", "Flint", OrderObjectCategory.Resource),
                new OrderObject("sandstone", "Sandstone", OrderObjectCategory.Resource),
                new OrderObject("granite", "Granite", OrderObjectCategory.Resource),
            },
            ["Wickerwork"] = new[]
            {
                new OrderObject("windscreen", "Windscreen", OrderObjectCategory.Structure),
                new OrderObject("basket", "Basket", OrderObjectCategory.Structure),
            },
            ["Fire"] = new[]
            {
                new OrderObject("fireplace", "Fireplace", OrderObjectCategory.Structure),
            },
        };

        // ─── State ───────────────────────────────────────────

        private readonly HashSet<OrderPredicate> _unlockedPredicates = new();
        private readonly List<OrderObject> _unlockedObjects = new();
        private readonly HashSet<string> _unlockedObjectIds = new();

        // ─── Lifecycle ───────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            InitializeStartVocabulary();
        }

        private void OnEnable()
        {
            EventBus.Subscribe<DiscoveryMadeEvent>(OnDiscoveryMade);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<DiscoveryMadeEvent>(OnDiscoveryMade);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Start()
        {
            // Retroactively unlock vocabulary for discoveries already made
            var dsm = DiscoveryStateManager.Instance;
            if (dsm != null)
            {
                foreach (var discovery in dsm.CompletedDiscoveries)
                    UnlockForDiscovery(discovery);
            }
        }

        // ─── Initialization ─────────────────────────────────

        private void InitializeStartVocabulary()
        {
            // Start predicates (always available)
            _unlockedPredicates.Add(OrderPredicate.Gather);
            _unlockedPredicates.Add(OrderPredicate.Explore);
            _unlockedPredicates.Add(OrderPredicate.Avoid);

            // Start objects
            foreach (var obj in START_OBJECTS)
            {
                if (_unlockedObjectIds.Add(obj.Id))
                    _unlockedObjects.Add(obj);
            }
        }

        // ─── Discovery Handler ──────────────────────────────

        private void OnDiscoveryMade(DiscoveryMadeEvent evt)
        {
            UnlockForDiscovery(evt.DiscoveryName);
        }

        private void UnlockForDiscovery(string discoveryName)
        {
            if (DISCOVERY_PREDICATES.TryGetValue(discoveryName, out var predicates))
            {
                foreach (var p in predicates)
                    _unlockedPredicates.Add(p);
            }

            if (DISCOVERY_OBJECTS.TryGetValue(discoveryName, out var objects))
            {
                foreach (var obj in objects)
                {
                    if (_unlockedObjectIds.Add(obj.Id))
                        _unlockedObjects.Add(obj);
                }
            }
        }

        // ─── Queries ─────────────────────────────────────────

        /// <summary>Is this predicate unlocked for use?</summary>
        public bool IsPredicateUnlocked(OrderPredicate predicate)
        {
            return _unlockedPredicates.Contains(predicate);
        }

        /// <summary>Get all predicates, marking each as unlocked or locked.</summary>
        public List<PredicateEntry> GetAllPredicates()
        {
            var result = new List<PredicateEntry>();
            foreach (OrderPredicate p in Enum.GetValues(typeof(OrderPredicate)))
            {
                result.Add(new PredicateEntry
                {
                    Predicate = p,
                    IsUnlocked = _unlockedPredicates.Contains(p),
                    RequiredDiscovery = p.RequiredDiscovery()
                });
            }
            return result;
        }

        /// <summary>Get objects filtered by predicate context.</summary>
        public List<OrderObject> GetObjectsForPredicate(OrderPredicate predicate)
        {
            var result = new List<OrderObject>();

            foreach (var obj in _unlockedObjects)
            {
                switch (predicate)
                {
                    case OrderPredicate.Gather:
                        if (obj.Category == OrderObjectCategory.Resource ||
                            obj.Category == OrderObjectCategory.Location)
                            result.Add(obj);
                        break;
                    case OrderPredicate.Explore:
                        if (obj.Category == OrderObjectCategory.Direction ||
                            obj.Category == OrderObjectCategory.Location)
                            result.Add(obj);
                        break;
                    case OrderPredicate.Avoid:
                        // Can avoid anything
                        result.Add(obj);
                        break;
                    case OrderPredicate.Build:
                        if (obj.Category == OrderObjectCategory.Structure)
                            result.Add(obj);
                        break;
                    case OrderPredicate.Hunt:
                        if (obj.Category == OrderObjectCategory.Location ||
                            obj.Category == OrderObjectCategory.Direction)
                            result.Add(obj);
                        break;
                    case OrderPredicate.Cook:
                    case OrderPredicate.Smoke:
                        if (obj.Category == OrderObjectCategory.Resource ||
                            obj.Category == OrderObjectCategory.Product)
                            result.Add(obj);
                        break;
                    case OrderPredicate.Craft:
                        if (obj.Category == OrderObjectCategory.Resource ||
                            obj.Category == OrderObjectCategory.Product ||
                            obj.Category == OrderObjectCategory.Structure)
                            result.Add(obj);
                        break;
                    case OrderPredicate.Fell:
                    case OrderPredicate.Dig:
                        if (obj.Category == OrderObjectCategory.Resource ||
                            obj.Category == OrderObjectCategory.Location)
                            result.Add(obj);
                        break;
                }
            }

            return result;
        }

        /// <summary>All currently unlocked objects.</summary>
        public IReadOnlyList<OrderObject> UnlockedObjects => _unlockedObjects;

        /// <summary>All currently unlocked predicates.</summary>
        public IReadOnlyCollection<OrderPredicate> UnlockedPredicates => _unlockedPredicates;
    }

    /// <summary>
    /// A predicate entry with unlock state for UI display.
    /// Feature 7.3: Three states per entry.
    /// </summary>
    public struct PredicateEntry
    {
        public OrderPredicate Predicate;
        public bool IsUnlocked;
        public string RequiredDiscovery;
    }
}
