using UnityEngine;
using Terranova.Core;
using Terranova.Buildings;
using Terranova.Resources;

namespace Terranova.Population
{
    /// <summary>
    /// Represents a single task assigned to a settler.
    ///
    /// A task defines: what to do, where to do it, and where to bring the result.
    /// The settler's state machine (Settler.cs) handles execution.
    ///
    /// Story 1.3/1.4: Basic task system with placeholder positions.
    /// Story 3.2: Tasks can reference a ResourceNode for gathering.
    /// Story 4.2: Tasks can reference a Building for construction.
    ///
    /// Note: SettlerTaskType enum is defined in Terranova.Core (EventBus.cs)
    /// to avoid circular assembly dependencies.
    /// </summary>
    public class SettlerTask
    {
        public SettlerTaskType TaskType { get; }

        /// <summary>Where to go to perform the work (tree, rock, construction site).</summary>
        public Vector3 TargetPosition { get; private set; }

        /// <summary>Where to deliver the result (campfire/storage building).</summary>
        public Vector3 BasePosition { get; }

        /// <summary>How long the work phase takes (seconds of game time).</summary>
        public float WorkDuration { get; }

        /// <summary>
        /// The resource node being gathered, or null for non-resource tasks.
        /// Story 3.2: Links task to an actual world resource.
        /// </summary>
        public ResourceNode TargetResource { get; set; }

        /// <summary>
        /// The building being constructed, or null for non-build tasks.
        /// Story 4.2: Links task to a construction site.
        /// </summary>
        public Building TargetBuilding { get; set; }

        /// <summary>
        /// Order ID that created this task, or -1 if auto-assigned.
        /// Used to link tasks back to orders for cancellation and UI display.
        /// </summary>
        public int OrderId { get; set; } = -1;

        /// <summary>
        /// Walk speed multiplier. Specialized workers (building-assigned) move faster.
        /// Default 1.0, building workers get 2.0.
        /// </summary>
        public float SpeedMultiplier { get; set; } = 1f;

        /// <summary>
        /// True when this task was assigned by a production building (WoodcutterHut, HunterHut).
        /// Specialized workers deliver to their building and are visually distinct.
        /// </summary>
        public bool IsSpecialized { get; set; }

        /// <summary>
        /// Whether the target still exists (tree not yet felled, etc.).
        /// Checks ResourceNode for gather tasks, Building for build tasks.
        /// </summary>
        public bool IsTargetValid
        {
            get
            {
                if (!_isTargetValid) return false;
                if (TargetResource != null && TargetResource.IsDepleted) return false;
                if (TargetBuilding != null && TargetBuilding.IsConstructed) return false;
                return true;
            }
            set => _isTargetValid = value;
        }
        private bool _isTargetValid = true;

        public SettlerTask(SettlerTaskType type, Vector3 target, Vector3 basePos, float workDuration)
        {
            TaskType = type;
            TargetPosition = target;
            BasePosition = basePos;
            WorkDuration = workDuration;
        }

        /// <summary>
        /// Update the target position (e.g., if the settler needs to find a new tree
        /// because the current one was felled by another settler).
        /// </summary>
        public void SetNewTarget(Vector3 newTarget)
        {
            TargetPosition = newTarget;
            _isTargetValid = true;
        }

        /// <summary>
        /// Default work durations per task type (in game-time seconds).
        /// Epoch I.1: Twigs and stones are picked up instantly (0s).
        /// </summary>
        public static float GetDefaultDuration(SettlerTaskType type)
        {
            return type switch
            {
                SettlerTaskType.GatherWood => 0f,   // Pick up twig instantly
                SettlerTaskType.GatherStone => 0f,   // Pick up stone instantly
                SettlerTaskType.Hunt => 6f,
                SettlerTaskType.Build => 8f,
                _ => 3f
            };
        }
    }
}
