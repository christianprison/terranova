using UnityEngine;
using Terranova.Core;
using Terranova.Resources;

namespace Terranova.Population
{
    /// <summary>
    /// Automatically assigns idle settlers to gather from nearby resources.
    ///
    /// Picks a random resource type first, then finds the nearest available
    /// node of that type. This ensures all resource types get gathered,
    /// not just whichever is closest overall.
    ///
    /// Story 3.2: Sammel-Interaktion
    /// </summary>
    public class ResourceTaskAssigner : MonoBehaviour
    {
        private const float CHECK_INTERVAL = 1f;

        private static readonly ResourceType[] RESOURCE_TYPES =
        {
            ResourceType.Wood,
            ResourceType.Stone,
            ResourceType.Food
        };

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

            // Pick a random resource type, then try others if none available
            int startIndex = Random.Range(0, RESOURCE_TYPES.Length);

            for (int i = 0; i < RESOURCE_TYPES.Length; i++)
            {
                var targetType = RESOURCE_TYPES[(startIndex + i) % RESOURCE_TYPES.Length];
                ResourceNode nearest = FindNearest(nodes, settler.transform.position, targetType);

                if (nearest == null) continue;
                if (!nearest.TryReserve()) continue;

                var taskType = nearest.ToTaskType();
                float duration = SettlerTask.GetDefaultDuration(taskType);
                var task = new SettlerTask(taskType, nearest.transform.position, basePos, duration);
                task.TargetResource = nearest;

                if (settler.AssignTask(task))
                    return;

                nearest.Release();
            }
        }

        private static ResourceNode FindNearest(ResourceNode[] nodes, Vector3 position, ResourceType type)
        {
            ResourceNode nearest = null;
            float nearestDist = float.MaxValue;

            foreach (var node in nodes)
            {
                if (node.Type != type) continue;
                if (!node.IsAvailable) continue;

                float dist = Vector3.Distance(position, node.transform.position);
                if (dist < nearestDist)
                {
                    nearestDist = dist;
                    nearest = node;
                }
            }

            return nearest;
        }
    }
}
