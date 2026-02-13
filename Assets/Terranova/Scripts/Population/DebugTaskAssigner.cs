using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using Terranova.Buildings;
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
    ///   B = Send next idle settler to the nearest building entrance (Story 2.3)
    ///   L = Send next idle settler on a long-distance cross-chunk path (Story 2.4)
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

            // Story 2.3: Send settler to nearest building
            if (kb.bKey.wasPressedThisFrame)
                SendSettlerToBuilding();

            // Story 2.4: Send settler on a long cross-chunk path
            if (kb.lKey.wasPressedThisFrame)
                SendSettlerLongDistance();
        }

        private void AssignTaskToNextIdleSettler()
        {
            var world = WorldManager.Instance;
            if (world == null)
                return;

            Settler idleSettler = FindFirstIdleSettler();
            if (idleSettler == null)
                return;

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

        /// <summary>
        /// Story 2.3: Send next idle settler to the nearest placed building's entrance.
        /// </summary>
        private void SendSettlerToBuilding()
        {
            var buildings = FindObjectsByType<Building>(FindObjectsSortMode.None);
            if (buildings.Length == 0)
            {
                Debug.Log("[DebugTaskAssigner] No buildings placed yet. Place one first (BuildingPlacer).");
                return;
            }

            Settler idleSettler = FindFirstIdleSettler();
            if (idleSettler == null)
                return;

            // Find the campfire for the base position
            var campfire = GameObject.Find("Campfire");
            Vector3 basePos = campfire != null ? campfire.transform.position : idleSettler.transform.position;

            // Pick the building closest to the settler
            Building closest = null;
            float closestDist = float.MaxValue;
            foreach (var b in buildings)
            {
                float dist = Vector3.Distance(idleSettler.transform.position, b.transform.position);
                if (dist < closestDist)
                {
                    closestDist = dist;
                    closest = b;
                }
            }

            Vector3 entrance = closest.EntrancePosition;
            var task = new SettlerTask(SettlerTaskType.Build, entrance, basePos, 3f);

            if (idleSettler.AssignTask(task))
            {
                Debug.Log($"[DebugTaskAssigner] Sent {idleSettler.name} to building " +
                          $"'{closest.Definition.DisplayName}' entrance ({entrance.x:F1}, {entrance.z:F1})");
            }
        }

        /// <summary>
        /// Story 2.4: Send next idle settler on a long-distance path (~50 blocks)
        /// to test cross-chunk navigation.
        /// </summary>
        private void SendSettlerLongDistance()
        {
            var world = WorldManager.Instance;
            if (world == null)
                return;

            Settler idleSettler = FindFirstIdleSettler();
            if (idleSettler == null)
                return;

            Vector3 basePos = idleSettler.transform.position;

            // Try to find a valid NavMesh point ~50 blocks away (crosses chunk boundaries)
            for (int attempt = 0; attempt < 10; attempt++)
            {
                float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
                float distance = Random.Range(40f, 60f);
                float x = basePos.x + Mathf.Cos(angle) * distance;
                float z = basePos.z + Mathf.Sin(angle) * distance;

                Vector3 candidate = new Vector3(x, basePos.y, z);

                if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, 10f, NavMesh.AllAreas))
                {
                    var task = new SettlerTask(SettlerTaskType.GatherWood, hit.position, basePos, 2f);
                    if (idleSettler.AssignTask(task))
                    {
                        Debug.Log($"[DebugTaskAssigner] Long-distance path: {idleSettler.name} " +
                                  $"walking {distance:F0} blocks to ({hit.position.x:F0}, {hit.position.z:F0})");
                        return;
                    }
                }
            }

            Debug.LogWarning("[DebugTaskAssigner] Could not find valid long-distance target.");
        }

        /// <summary>
        /// Find the first idle settler, or null if all are busy.
        /// </summary>
        private Settler FindFirstIdleSettler()
        {
            var settlers = FindObjectsByType<Settler>(FindObjectsSortMode.None);
            if (settlers.Length == 0)
            {
                Debug.Log("[DebugTaskAssigner] No settlers found.");
                return null;
            }

            foreach (var settler in settlers)
            {
                if (!settler.HasTask)
                    return settler;
            }

            Debug.Log("[DebugTaskAssigner] No idle settlers available. All are busy.");
            return null;
        }

        /// <summary>
        /// Find a valid target position on the NavMesh. (Story 2.0)
        /// </summary>
        private Vector3 FindValidTargetPosition(WorldManager world, Vector3 center)
        {
            for (int attempt = 0; attempt < 10; attempt++)
            {
                float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
                float r = Random.Range(_taskTargetRadius * 0.8f, _taskTargetRadius * 1.2f);
                float x = center.x + Mathf.Cos(angle) * r;
                float z = center.z + Mathf.Sin(angle) * r;

                Vector3 candidate = new Vector3(x, center.y, z);

                // Validate position is on NavMesh (Story 2.0: replaces block-based check)
                if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, 5f, NavMesh.AllAreas))
                {
                    return hit.position;
                }
            }

            return Vector3.zero;
        }
    }
}
