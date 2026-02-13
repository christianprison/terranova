using UnityEngine;
using Terranova.Core;
using Terranova.Buildings;
using Terranova.Resources;

namespace Terranova.Population
{
    /// <summary>
    /// Activates building functions when construction completes.
    ///
    /// - WoodcutterHut/HunterHut: periodically assigns an idle settler
    ///   to gather the corresponding resource near the building.
    /// - SimpleHut: increases housing capacity (tracked for future use).
    ///
    /// Story 4.4: Gebäude-Funktion
    /// </summary>
    public class BuildingFunctionManager : MonoBehaviour
    {
        private const float CHECK_INTERVAL = 2f;

        private float _checkTimer;
        private int _totalHousingCapacity;

        /// <summary>Total housing capacity from all completed SimpleHuts.</summary>
        public static int TotalHousingCapacity { get; private set; }

        private void OnEnable()
        {
            EventBus.Subscribe<BuildingCompletedEvent>(OnBuildingCompleted);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<BuildingCompletedEvent>(OnBuildingCompleted);
        }

        private void OnBuildingCompleted(BuildingCompletedEvent evt)
        {
            if (evt.BuildingObject == null) return;
            var building = evt.BuildingObject.GetComponent<Building>();
            if (building == null || building.Definition == null) return;

            // SimpleHut: immediately add housing capacity
            if (building.Definition.Type == BuildingType.SimpleHut)
            {
                _totalHousingCapacity += building.Definition.HousingCapacity;
                TotalHousingCapacity = _totalHousingCapacity;
                Debug.Log($"[BuildingFunction] Housing capacity now {_totalHousingCapacity}");
            }
        }

        private void Update()
        {
            _checkTimer -= Time.deltaTime;
            if (_checkTimer > 0f) return;

            _checkTimer = CHECK_INTERVAL;
            RefreshWorkerStatus();
            AssignWorkersToBuildings();
        }

        /// <summary>
        /// Clear HasWorker on buildings whose worker has gone idle or was reassigned.
        /// </summary>
        private void RefreshWorkerStatus()
        {
            var buildings = FindObjectsByType<Building>(FindObjectsSortMode.None);
            var settlers = FindObjectsByType<Settler>(FindObjectsSortMode.None);

            foreach (var building in buildings)
            {
                if (!building.HasWorker) continue;

                // Check if any settler is currently working for this building
                // (i.e., has a gather task targeting resources near this building)
                bool hasActiveWorker = false;
                foreach (var settler in settlers)
                {
                    if (!settler.HasTask) continue;
                    var task = settler.CurrentTask;
                    if (task == null) continue;

                    // Match by building type to task type
                    var bType = building.Definition?.Type;
                    if (bType == BuildingType.WoodcutterHut && task.TaskType == SettlerTaskType.GatherWood)
                    {
                        hasActiveWorker = true;
                        break;
                    }
                    if (bType == BuildingType.HunterHut && task.TaskType == SettlerTaskType.Hunt)
                    {
                        hasActiveWorker = true;
                        break;
                    }
                }

                if (!hasActiveWorker)
                    building.HasWorker = false;
            }
        }

        /// <summary>
        /// Find completed production buildings without workers and assign settlers.
        /// </summary>
        private void AssignWorkersToBuildings()
        {
            var campfire = GameObject.Find("Campfire");
            if (campfire == null) return;
            Vector3 basePos = campfire.transform.position;

            var buildings = FindObjectsByType<Building>(FindObjectsSortMode.None);
            var settlers = FindObjectsByType<Settler>(FindObjectsSortMode.None);

            foreach (var building in buildings)
            {
                if (!building.IsConstructed) continue;
                if (building.Definition == null) continue;

                var type = building.Definition.Type;
                if (type != BuildingType.WoodcutterHut && type != BuildingType.HunterHut)
                    continue;

                if (building.HasWorker) continue;
                Settler nearest = null;
                float nearestDist = float.MaxValue;

                foreach (var settler in settlers)
                {
                    if (settler.HasTask) continue;

                    float dist = Vector3.Distance(settler.transform.position, building.EntrancePosition);
                    if (dist < nearestDist)
                    {
                        nearestDist = dist;
                        nearest = settler;
                    }
                }

                if (nearest == null) return; // No idle settlers

                // Determine task type and find nearest resource
                SettlerTaskType taskType;
                ResourceType resourceType;
                if (type == BuildingType.WoodcutterHut)
                {
                    taskType = SettlerTaskType.GatherWood;
                    resourceType = ResourceType.Wood;
                }
                else
                {
                    taskType = SettlerTaskType.Hunt;
                    resourceType = ResourceType.Food;
                }

                // Find nearest resource of this type near the building
                var nodes = FindObjectsByType<ResourceNode>(FindObjectsSortMode.None);
                ResourceNode nearestNode = null;
                float nearestNodeDist = float.MaxValue;

                foreach (var node in nodes)
                {
                    if (node.Type != resourceType) continue;
                    if (!node.IsAvailable) continue;

                    float dist = Vector3.Distance(building.transform.position, node.transform.position);
                    if (dist < nearestNodeDist)
                    {
                        nearestNodeDist = dist;
                        nearestNode = node;
                    }
                }

                if (nearestNode == null) continue;
                if (!nearestNode.TryReserve()) continue;

                float duration = SettlerTask.GetDefaultDuration(taskType);
                var task = new SettlerTask(taskType, nearestNode.transform.position, basePos, duration);
                task.TargetResource = nearestNode;

                if (nearest.AssignTask(task))
                {
                    building.HasWorker = true;
                    Debug.Log($"[BuildingFunction] Assigned {nearest.name} to {building.name}");
                }
                else
                {
                    nearestNode.Release();
                }
            }
        }
    }
}
