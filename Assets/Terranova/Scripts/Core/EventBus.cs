using System;
using System.Collections.Generic;
using UnityEngine;

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
        /// Reset static state when domain reload is disabled (Enter Play Mode Options).
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _handlers.Clear();
        }

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
    /// Story 4.1: Now includes the building definition and GameObject reference.
    /// </summary>
    public struct BuildingPlacedEvent
    {
        public string BuildingName;
        public UnityEngine.Vector3 Position;
        /// <summary>The placed building GameObject.</summary>
        public UnityEngine.GameObject BuildingObject;
    }

    /// <summary>
    /// Fired when a building's construction is complete.
    /// Story 4.2: Triggers building function activation.
    /// </summary>
    public struct BuildingCompletedEvent
    {
        public string BuildingName;
        public UnityEngine.Vector3 Position;
        public UnityEngine.GameObject BuildingObject;
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
        /// <summary>Actual resource type delivered (may differ from task type for discovery resources).</summary>
        public ResourceType ActualResourceType;
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

    /// <summary>
    /// Fired when a settler dies (starvation, old age, etc.).
    /// Story 5.4: Tod
    /// </summary>
    public struct SettlerDiedEvent
    {
        public string SettlerName;
        public UnityEngine.Vector3 Position;
        public string CauseOfDeath;
    }

    /// <summary>
    /// Fired when food supply is critically low.
    /// Story 5.4: Warning UI
    /// </summary>
    public struct FoodWarningEvent
    {
        public bool IsWarning;
    }

    /// <summary>
    /// Fired when a discovery is made by the discovery engine.
    /// Story 1.5: Discovery State Manager
    /// </summary>
    public struct DiscoveryMadeEvent
    {
        public string DiscoveryName;
        public string Description;
        /// <summary>Brief reason why the discovery happened, shown in toast.</summary>
        public string Reason;
    }

    /// <summary>
    /// Fired when a new day begins (day counter increments).
    /// MS4 Feature 1.5: Day-Night Cycle.
    /// </summary>
    public struct DayChangedEvent
    {
        public int DayCount;
    }

    /// <summary>
    /// Fired when a settler's tool breaks.
    /// MS4 Feature 3.2: Tool Wear.
    /// </summary>
    public struct ToolBrokeEvent
    {
        public string SettlerName;
        public string ToolName;
    }

    /// <summary>
    /// Fired when a settler's need reaches critical levels.
    /// MS4 Feature 4.5: Needs UI.
    /// </summary>
    public struct NeedsCriticalEvent
    {
        public string SettlerName;
        public string NeedType; // "Thirst" or "Hunger"
    }

    /// <summary>
    /// Fired when a settler gets poisoned from food.
    /// MS4 Feature 4.3: Food Sources.
    /// </summary>
    public struct SettlerPoisonedEvent
    {
        public string SettlerName;
        public string FoodName;
    }

    /// <summary>
    /// Fired during world generation to update loading bar progress.
    /// </summary>
    public struct WorldGenerationProgressEvent
    {
        /// <summary>Progress from 0 to 1.</summary>
        public float Progress;
        /// <summary>Description of current phase.</summary>
        public string Status;
    }

    /// <summary>
    /// Fired when the player selects or deselects an object.
    /// Story 6.1: Tap selection. Story 6.2: Deselection.
    /// Story 6.3: Long press (IsDetailView = true).
    /// </summary>
    public struct SelectionChangedEvent
    {
        /// <summary>Selected object (null = deselected).</summary>
        public UnityEngine.GameObject SelectedObject;
        /// <summary>True when long press triggered detail view.</summary>
        public bool IsDetailView;
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
        Build,          // Go to construction site, build
        GatherMaterial, // Go to material node, gather, bring back
        CraftTool,      // Craft a tool from materials
        DrinkWater,     // Walk to water source and drink
        SeekFood,       // Autonomously seek food
        SeekShelter     // Walk to nearest shelter
    }

    /// <summary>
    /// Types of gatherable resources in the world.
    /// Story 3.1: Sammelbare Objekte
    /// </summary>
    public enum ResourceType
    {
        Wood,
        Stone,
        Food,
        Resin,
        Flint,
        PlantFiber
    }

    /// <summary>
    /// Thirst states for settlers.
    /// MS4 Feature 4.1: Thirst Mechanic.
    /// </summary>
    public enum ThirstState
    {
        Hydrated,    // Normal
        Thirsty,     // -20% performance, seeks water
        Dehydrated,  // -60% performance, no orders
        Dying        // Will die soon
    }

    /// <summary>
    /// Hunger states for settlers.
    /// MS4 Feature 4.2: Extended Hunger System.
    /// </summary>
    public enum HungerState
    {
        Sated,       // 100-70%: normal
        Hungry,      // 70-40%: -20%, prioritizes food
        Exhausted,   // 40-10%: -50%, can only seek food
        Starving     // <10%: barely moves, dies soon
    }

    /// <summary>
    /// Protection/shelter states.
    /// MS4 Feature 4.5: Needs UI.
    /// </summary>
    public enum ShelterState
    {
        Sheltered,
        Exposed,
        Hypothermic,
        Sick
    }

    /// <summary>
    /// Food freshness states.
    /// MS4 Feature 4.4: Food Spoilage.
    /// </summary>
    public enum FoodFreshness
    {
        Fresh,   // 0-4h: full nutrition
        Stale,   // 4-8h: -50% nutrition
        Spoiled, // >8h: causes sickness
        Dried,   // indefinite, -20% nutrition
        Smoked   // indefinite, full nutrition
    }
}
