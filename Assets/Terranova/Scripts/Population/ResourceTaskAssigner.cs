using UnityEngine;
using Terranova.Core;
using Terranova.Buildings;
using Terranova.Orders;
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

            // Priority: construction > gathering.
            // If unreserved construction sites exist, leave idle settlers
            // for ConstructionTaskAssigner to pick up first.
            if (HasUnreservedConstructionSites())
                return;

            var settlers = FindObjectsByType<Settler>(FindObjectsSortMode.None);
            var orderMgr = OrderManager.Instance;
            foreach (var settler in settlers)
            {
                if (settler.HasTask) continue;

                // Feature 7.7: Player orders take priority over auto-assignment.
                // If the settler has an active player order, let OrderManager handle it.
                if (orderMgr != null && orderMgr.HasOrderForSettler(settler.name))
                    continue;

                TryAssignResource(settler, basePos);
            }
        }

        /// <summary>
        /// Check if any construction sites are waiting for a builder.
        /// </summary>
        private bool HasUnreservedConstructionSites()
        {
            var buildings = FindObjectsByType<Building>(FindObjectsSortMode.None);
            foreach (var building in buildings)
            {
                if (!building.IsConstructed && !building.IsBeingBuilt)
                    return true;
            }
            return false;
        }

        private void TryAssignResource(Settler settler, Vector3 basePos)
        {
            var nodes = FindObjectsByType<ResourceNode>(FindObjectsSortMode.None);
            var orderMgr = OrderManager.Instance;

            // Pick a random resource type, then try others if none available
            int startIndex = Random.Range(0, RESOURCE_TYPES.Length);

            for (int i = 0; i < RESOURCE_TYPES.Length; i++)
            {
                var targetType = RESOURCE_TYPES[(startIndex + i) % RESOURCE_TYPES.Length];

                // Feature 7.7: Check negated orders ("All do NOT gather stone" etc.)
                var taskType = targetType switch
                {
                    ResourceType.Wood => SettlerTaskType.GatherWood,
                    ResourceType.Stone => SettlerTaskType.GatherStone,
                    ResourceType.Food => SettlerTaskType.Hunt,
                    _ => SettlerTaskType.GatherMaterial
                };
                if (orderMgr != null && orderMgr.IsTaskForbidden(settler.name, taskType))
                    continue;

                ResourceNode nearest = FindNearest(nodes, settler.transform.position, targetType);

                if (nearest == null) continue;
                if (!nearest.TryReserve()) continue;

                var taskType = nearest.ToTaskType();
                float duration = SettlerTask.GetDefaultDuration(taskType);
                var task = new SettlerTask(taskType, nearest.transform.position, basePos, duration);
                task.TargetResource = nearest;

                // Feature 3.1: Improved Tools → gather speed +30%
                if (GameplayModifiers.GatherSpeedMultiplier > 1f)
                    task.SpeedMultiplier = GameplayModifiers.GatherSpeedMultiplier;

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
