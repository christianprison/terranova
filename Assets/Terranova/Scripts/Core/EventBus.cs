using System;
using System.Collections.Generic;

namespace Terranova.Core
{
    /// <summary>
    /// Simple event bus for decoupled communication between game systems.
    ///
    /// How it works: Systems publish events (structs), other systems subscribe
    /// to the event types they care about. No direct references needed.
    ///
    /// Usage:
    ///   EventBus.Subscribe&lt;ResourceChangedEvent&gt;(OnResourceChanged);
    ///   EventBus.Publish(new ResourceChangedEvent { ResourceType = "Wood", Amount = 5 });
    ///   EventBus.Unsubscribe&lt;ResourceChangedEvent&gt;(OnResourceChanged);
    /// </summary>
    public static class EventBus
    {
        // Maps each event type to its combined delegate handler
        private static readonly Dictionary<Type, Delegate> _handlers = new();

        /// <summary>
        /// Subscribe a handler to receive events of type T.
        /// Remember to unsubscribe in OnDisable/OnDestroy to avoid memory leaks.
        /// </summary>
        public static void Subscribe<T>(Action<T> handler)
        {
            var type = typeof(T);
            if (_handlers.TryGetValue(type, out var existing))
                _handlers[type] = Delegate.Combine(existing, handler);
            else
                _handlers[type] = handler;
        }

        /// <summary>
        /// Unsubscribe a handler from receiving events of type T.
        /// </summary>
        public static void Unsubscribe<T>(Action<T> handler)
        {
            var type = typeof(T);
            if (_handlers.TryGetValue(type, out var existing))
            {
                var result = Delegate.Remove(existing, handler);
                if (result == null)
                    _handlers.Remove(type);
                else
                    _handlers[type] = result;
            }
        }

        /// <summary>
        /// Publish an event to all subscribers of type T.
        /// </summary>
        public static void Publish<T>(T eventData)
        {
            if (_handlers.TryGetValue(typeof(T), out var handler))
                ((Action<T>)handler)?.Invoke(eventData);
        }

        /// <summary>
        /// Remove all subscriptions. Call this when resetting the game or in tests.
        /// </summary>
        public static void Clear()
        {
            _handlers.Clear();
        }
    }

    // ─── Event Definitions ──────────────────────────────────────
    // Define event structs here so all systems can reference them
    // from the Core assembly without circular dependencies.

    /// <summary>
    /// Fired when a resource count changes (e.g., player gains/spends resources).
    /// </summary>
    public struct ResourceChangedEvent
    {
        public string ResourceName;
        public int NewAmount;
    }

    /// <summary>
    /// Fired when a building is placed in the world.
    /// </summary>
    public struct BuildingPlacedEvent
    {
        public string BuildingName;
        public UnityEngine.Vector3 Position;
    }

    /// <summary>
    /// Fired when the settler population changes (spawn, death, birth).
    /// </summary>
    public struct PopulationChangedEvent
    {
        public int CurrentPopulation;
    }

    /// <summary>
    /// Fired when a settler delivers a resource at the base (campfire/storage).
    /// The economy system (Story 3.x) will listen to this to update resource counts.
    /// </summary>
    public struct ResourceDeliveredEvent
    {
        /// <summary>What type of task produced this delivery.</summary>
        public SettlerTaskType TaskType;
        public UnityEngine.Vector3 Position;
    }

    /// <summary>
    /// Fired when a resource node is fully depleted (all gathers used up).
    /// Story 3.2: Sammel-Interaktion
    /// </summary>
    public struct ResourceDepletedEvent
    {
        public ResourceType Type;
        public UnityEngine.Vector3 Position;
    }

    // ─── Shared Enums ────────────────────────────────────────
    // Placed in Core to avoid circular dependencies between assemblies.

    /// <summary>
    /// The types of tasks a settler can perform.
    /// Defined in Core so both Population and UI assemblies can reference it.
    /// </summary>
    public enum SettlerTaskType
    {
        None,           // No task (settler is idle)
        GatherWood,     // Go to tree, chop, bring wood back
        GatherStone,    // Go to rock, mine, bring stone back
        Hunt,           // Go to hunting ground, hunt, bring food back
        Build           // Go to construction site, build
    }

    /// <summary>
    /// Types of gatherable resources in the world.
    /// Story 3.1: Sammelbare Objekte
    /// </summary>
    public enum ResourceType
    {
        Wood,
        Stone
    }
}
