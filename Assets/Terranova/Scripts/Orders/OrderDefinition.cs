using System.Collections.Generic;
using UnityEngine;
using Terranova.Core;

namespace Terranova.Orders
{
    /// <summary>
    /// Feature 7.1: Order Data Model.
    ///
    /// An order is a player-given sentence: WHO + DOES + WHAT/WHERE.
    /// Settlers execute orders by priority, with automatic needs overriding.
    /// </summary>
    public class OrderDefinition
    {
        private static int _nextId;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { _nextId = 0; }

        /// <summary>Unique order ID.</summary>
        public int Id { get; }

        /// <summary>WHO: subject of the order.</summary>
        public OrderSubject Subject { get; set; }

        /// <summary>Named settler (only when Subject == Named).</summary>
        public string SettlerName { get; set; }

        /// <summary>DOES: predicate (verb) of the order.</summary>
        public OrderPredicate Predicate { get; set; }

        /// <summary>WHAT/WHERE: one or more objects of the order.</summary>
        public List<OrderObject> Objects { get; set; } = new();

        /// <summary>NICHT: negated orders tell settlers to NOT do this.</summary>
        public bool Negated { get; set; }

        /// <summary>Execution priority (lower = higher priority).</summary>
        public int Priority { get; set; }

        /// <summary>Current execution status.</summary>
        public OrderStatus Status { get; set; } = OrderStatus.Active;

        /// <summary>World position for "here" orders (ground tap location).</summary>
        public Vector3? TargetPosition { get; set; }

        /// <summary>Progress 0-1 for display (build orders, etc.).</summary>
        public float Progress { get; set; }

        /// <summary>Which settlers are currently executing this order.</summary>
        public HashSet<string> AssignedSettlers { get; } = new();

        public OrderDefinition()
        {
            Id = _nextId++;
        }

        /// <summary>
        /// Feature 7.4: Build the human-readable sentence for the result line.
        /// </summary>
        public string BuildSentence()
        {
            // WHO
            string who = Subject switch
            {
                OrderSubject.All => "All",
                OrderSubject.NextFree => "Next Free",
                OrderSubject.Named => SettlerName ?? "???",
                _ => "???"
            };

            // NICHT
            string negation = Negated ? " do NOT" : "";

            // DOES — "All" uses plural verb (e.g. "All gather" not "All gathers")
            bool plural = Subject == OrderSubject.All;
            string verb = Predicate.ToVerb(Negated, plural);

            // WHAT/WHERE
            string what = "";
            if (Objects.Count > 0)
            {
                var parts = new List<string>();
                foreach (var obj in Objects)
                    parts.Add(obj.DisplayName);

                if (parts.Count == 1)
                    what = parts[0];
                else if (parts.Count == 2)
                    what = $"{parts[0]} and {parts[1]}";
                else
                {
                    what = string.Join(", ", parts.GetRange(0, parts.Count - 1))
                         + " and " + parts[parts.Count - 1];
                }
            }

            if (Negated)
                return $"{who}{negation} {Predicate.ToString().ToLower()}" +
                       (string.IsNullOrEmpty(what) ? "" : $" {what}");

            return $"{who} {verb}" +
                   (string.IsNullOrEmpty(what) ? "" : $" {what}");
        }

        /// <summary>
        /// Check if this order can be validated (all required fields set and combination valid).
        /// </summary>
        public bool IsValid()
        {
            // Must have subject and predicate
            if (Subject == OrderSubject.Named && string.IsNullOrEmpty(SettlerName))
                return false;

            // Negated orders don't need objects
            if (Negated) return true;

            // Most predicates need at least one object
            switch (Predicate)
            {
                case OrderPredicate.Build:
                    // Build requires at least one Structure object
                    return Objects.Exists(o => o.Category == OrderObjectCategory.Structure);
                case OrderPredicate.Gather:
                case OrderPredicate.Cook:
                case OrderPredicate.Smoke:
                case OrderPredicate.Craft:
                case OrderPredicate.Hunt:
                case OrderPredicate.Fell:
                case OrderPredicate.Dig:
                    return Objects.Count > 0;
                case OrderPredicate.Explore:
                    return Objects.Count > 0; // needs direction
                case OrderPredicate.Avoid:
                    return Objects.Count > 0; // needs what to avoid
                default:
                    return true;
            }
        }

        /// <summary>
        /// Map this order to a SettlerTaskType for execution.
        /// </summary>
        public SettlerTaskType ToTaskType()
        {
            return Predicate switch
            {
                OrderPredicate.Gather => SettlerTaskType.GatherMaterial,
                OrderPredicate.Hunt => SettlerTaskType.Hunt,
                OrderPredicate.Build => SettlerTaskType.Build,
                OrderPredicate.Craft => SettlerTaskType.CraftTool,
                OrderPredicate.Fell => SettlerTaskType.GatherWood,
                OrderPredicate.Dig => SettlerTaskType.GatherStone,
                OrderPredicate.Explore => SettlerTaskType.None,
                OrderPredicate.Avoid => SettlerTaskType.None,
                OrderPredicate.Cook => SettlerTaskType.CraftTool,
                OrderPredicate.Smoke => SettlerTaskType.CraftTool,
                _ => SettlerTaskType.None
            };
        }
    }

    /// <summary>
    /// A single object/target in an order (WHAT/WHERE column).
    /// </summary>
    public class OrderObject
    {
        /// <summary>Internal ID for matching (e.g. "flint", "here", "north").</summary>
        public string Id { get; set; }

        /// <summary>Display name shown in UI and sentence.</summary>
        public string DisplayName { get; set; }

        /// <summary>Category for context filtering.</summary>
        public OrderObjectCategory Category { get; set; }

        /// <summary>World position (for "here" tap targets).</summary>
        public Vector3? Position { get; set; }

        public OrderObject(string id, string displayName, OrderObjectCategory category)
        {
            Id = id;
            DisplayName = displayName;
            Category = category;
        }
    }

    /// <summary>
    /// Categories for filtering objects by predicate context.
    /// </summary>
    public enum OrderObjectCategory
    {
        Location,   // "here", directions
        Resource,   // flint, sandstone, berries, etc.
        Structure,  // windscreen, basket, fireplace
        Product,    // crafted items
        Direction   // north, south, east, west
    }
}
