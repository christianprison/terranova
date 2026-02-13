using UnityEngine;
using UnityEngine.InputSystem;
using Terranova.Core;
using Terranova.Terrain;

namespace Terranova.Population
{
    /// <summary>
    /// DEBUG ONLY - Remove when building-driven task assignment exists (Story 4.4).
    ///
    /// Hotkeys:
    ///   T = Assign a task to the next idle settler
    ///   U = Invalidate the target of a busy settler (tests "target gone" behavior)
    ///
    /// All actions log to Console.
    /// </summary>
    public class DebugTaskAssigner : MonoBehaviour
    {
        [Header("Debug Settings")]
        [Tooltip("How far from campfire the work target is placed.")]
        [SerializeField] private float _taskTargetRadius = 15f;

        private static readonly SettlerTaskType[] TASK_TYPES =
        {
            SettlerTaskType.GatherWood,
            SettlerTaskType.GatherStone,
            SettlerTaskType.Hunt,
            SettlerTaskType.Build
        };

        private int _taskTypeIndex;

        private void Update()
        {
            var kb = Keyboard.current;
            if (kb == null)
                return;

            if (kb.tKey.wasPressedThisFrame)
                AssignTaskToNextIdleSettler();

            if (kb.uKey.wasPressedThisFrame)
                InvalidateFirstBusySettlerTarget();
        }

        private void AssignTaskToNextIdleSettler()
        {
            var world = WorldManager.Instance;
            if (world == null)
                return;

            // Find all settlers
            var settlers = FindObjectsByType<Settler>(FindObjectsSortMode.None);
            if (settlers.Length == 0)
            {
                Debug.Log("[DebugTaskAssigner] No settlers found.");
                return;
            }

            // Find first idle settler
            Settler idleSettler = null;
            foreach (var settler in settlers)
            {
                if (!settler.HasTask)
                {
                    idleSettler = settler;
                    break;
                }
            }

            if (idleSettler == null)
            {
                Debug.Log("[DebugTaskAssigner] No idle settlers available. All are busy.");
                return;
            }

            // Find the campfire to use as base position
            var campfire = GameObject.Find("Campfire");
            if (campfire == null)
            {
                Debug.LogWarning("[DebugTaskAssigner] No campfire found.");
                return;
            }
            Vector3 basePos = campfire.transform.position;

            // Find a valid target position
            Vector3 target = FindValidTargetPosition(world, basePos);
            if (target == Vector3.zero)
            {
                Debug.LogWarning("[DebugTaskAssigner] Could not find valid target position.");
                return;
            }

            // Cycle through task types
            var taskType = TASK_TYPES[_taskTypeIndex % TASK_TYPES.Length];
            _taskTypeIndex++;

            float duration = SettlerTask.GetDefaultDuration(taskType);
            var task = new SettlerTask(taskType, target, basePos, duration);
            idleSettler.AssignTask(task);

            Debug.Log($"[DebugTaskAssigner] Assigned {taskType} to {idleSettler.name} " +
                      $"(target: {target.x:F0},{target.z:F0})");
        }

        /// <summary>
        /// Invalidate the target of the first busy settler.
        /// Tests acceptance criterion: "Siedler sucht sich neues Ziel wenn altes nicht mehr existiert"
        /// </summary>
        private void InvalidateFirstBusySettlerTarget()
        {
            var settlers = FindObjectsByType<Settler>(FindObjectsSortMode.None);
            foreach (var settler in settlers)
            {
                if (settler.HasTask)
                {
                    settler.CurrentTask.IsTargetValid = false;
                    Debug.Log($"[DebugTaskAssigner] INVALIDATED target for {settler.name} " +
                              $"({settler.CurrentTask.TaskType}) - settler should go idle");
                    return;
                }
            }

            Debug.Log("[DebugTaskAssigner] No busy settlers to invalidate.");
        }

        private Vector3 FindValidTargetPosition(WorldManager world, Vector3 center)
        {
            for (int attempt = 0; attempt < 10; attempt++)
            {
                float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
                float r = Random.Range(_taskTargetRadius * 0.8f, _taskTargetRadius * 1.2f);
                float x = center.x + Mathf.Cos(angle) * r;
                float z = center.z + Mathf.Sin(angle) * r;

                int blockX = Mathf.FloorToInt(x);
                int blockZ = Mathf.FloorToInt(z);
                int height = world.GetHeightAtWorldPos(blockX, blockZ);

                if (height >= 0 && world.GetSurfaceTypeAtWorldPos(blockX, blockZ).IsSolid())
                {
                    // Use smooth mesh height for visual positioning (Story 0.6)
                    float smoothY = world.GetSmoothedHeightAtWorldPos(x, z);
                    return new Vector3(x, smoothY, z);
                }
            }

            return Vector3.zero;
        }
    }
}
