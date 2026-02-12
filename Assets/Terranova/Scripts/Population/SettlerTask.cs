using UnityEngine;
using Terranova.Core;

namespace Terranova.Population
{
    /// <summary>
    /// Represents a single task assigned to a settler.
    ///
    /// A task defines: what to do, where to do it, and where to bring the result.
    /// The settler's state machine (Settler.cs) handles execution.
    ///
    /// For now (Stories 1.3/1.4): targets are placeholder positions.
    /// Later (Stories 3.x/4.x): targets will reference actual resource objects
    /// and buildings.
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

        /// <summary>Whether the target still exists (tree not yet felled, etc.).</summary>
        public bool IsTargetValid { get; set; } = true;

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
            IsTargetValid = true;
        }

        /// <summary>
        /// Default work durations per task type (in game-time seconds).
        /// </summary>
        public static float GetDefaultDuration(SettlerTaskType type)
        {
            return type switch
            {
                SettlerTaskType.GatherWood => 4f,
                SettlerTaskType.GatherStone => 5f,
                SettlerTaskType.Hunt => 6f,
                SettlerTaskType.Build => 8f,
                _ => 3f
            };
        }
    }
}
