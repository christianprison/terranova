using UnityEngine;
using Terranova.Core;
using Terranova.Resources;

namespace Terranova.Population
{
    /// <summary>
    /// Automatically assigns idle settlers to gather from the nearest available resource.
    ///
    /// Runs a periodic check (every 1s) to find idle settlers and match them
    /// with the closest unreserved ResourceNode. Creates a SettlerTask and
    /// assigns it via the existing task system.
    ///
    /// Story 3.2: Sammel-Interaktion
    /// </summary>
    public class ResourceTaskAssigner : MonoBehaviour
    {
        private const float CHECK_INTERVAL = 1f;

        private float _checkTimer;

        private void Update()
        {
            _checkTimer -= Time.deltaTime;
            if (_checkTimer > 0f) return;

            _checkTimer = CHECK_INTERVAL;
            AssignIdleSettlers();
        }

        private void AssignIdleSettlers()
        {
            var campfire = GameObject.Find("Campfire");
            if (campfire == null) return;
            Vector3 basePos = campfire.transform.position;

            var settlers = FindObjectsByType<Settler>(FindObjectsSortMode.None);
            foreach (var settler in settlers)
            {
                if (settler.HasTask) continue;
                TryAssignResource(settler, basePos);
            }
        }

        private void TryAssignResource(Settler settler, Vector3 basePos)
        {
            var nodes = FindObjectsByType<ResourceNode>(FindObjectsSortMode.None);
            ResourceNode nearest = null;
            float nearestDist = float.MaxValue;

            foreach (var node in nodes)
            {
                if (!node.IsAvailable) continue;

                float dist = Vector3.Distance(settler.transform.position, node.transform.position);
                if (dist < nearestDist)
                {
                    nearestDist = dist;
                    nearest = node;
                }
            }

            if (nearest == null) return;

            if (!nearest.TryReserve()) return;

            var taskType = nearest.ToTaskType();
            float duration = SettlerTask.GetDefaultDuration(taskType);
            var task = new SettlerTask(taskType, nearest.transform.position, basePos, duration);
            task.TargetResource = nearest;

            if (!settler.AssignTask(task))
            {
                nearest.Release();
            }
        }
    }
}
