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
}
