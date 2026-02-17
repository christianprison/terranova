using System.Collections.Generic;
using UnityEngine;
using Terranova.Core;
using Terranova.Population;
using Terranova.Resources;

namespace Terranova.Orders
{
    /// <summary>
    /// Feature 7.7: Order Execution.
    ///
    /// Manages the lifecycle of player-created orders:
    /// - Stores all active/paused orders
    /// - Assigns orders to idle settlers based on priority
    /// - Tracks order completion and failure
    ///
    /// Settler AI priority (Feature 7.7):
    ///   1. Automatic needs: thirst, hunger, danger (not overridable)
    ///   2. Specific order for this settler (Named)
    ///   3. Group order ("All" / "Next Free")
    ///   4. Free decision (auto-assign from ResourceTaskAssigner)
    /// </summary>
    public class OrderManager : MonoBehaviour
    {
        public static OrderManager Instance { get; private set; }

        private const float ASSIGN_CHECK_INTERVAL = 0.5f;

        private readonly List<OrderDefinition> _orders = new();
        private float _assignTimer;

        /// <summary>All orders (active, paused, completed, failed).</summary>
        public IReadOnlyList<OrderDefinition> AllOrders => _orders;

        /// <summary>Only active orders.</summary>
        public List<OrderDefinition> ActiveOrders
        {
            get
            {
                var result = new List<OrderDefinition>();
                foreach (var o in _orders)
                    if (o.Status == OrderStatus.Active)
                        result.Add(o);
                return result;
            }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            // Register bridge callbacks so Population assembly can query orders
            // without a direct reference to Orders (avoids circular dependency).
            OrderQueryBridge.HasOrderForSettler = HasOrderForSettler;
            OrderQueryBridge.IsTaskForbidden = IsTaskForbidden;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
                OrderQueryBridge.HasOrderForSettler = null;
                OrderQueryBridge.IsTaskForbidden = null;
            }
        }

        private void Update()
        {
            _assignTimer -= Time.deltaTime;
            if (_assignTimer > 0f) return;
            _assignTimer = ASSIGN_CHECK_INTERVAL;

            TryAssignOrders();
        }

        // ─── Order Creation ──────────────────────────────────

        /// <summary>
        /// Create and register a new order from the Klappbuch UI.
        /// </summary>
        public OrderDefinition CreateOrder(OrderDefinition order)
        {
            order.Priority = _orders.Count;
            _orders.Add(order);

            EventBus.Publish(new OrderCreatedEvent { OrderId = order.Id });
            Debug.Log($"[Order] Created: {order.BuildSentence()} (id={order.Id})");

            // Immediately try to assign
            _assignTimer = 0f;

            return order;
        }

        /// <summary>
        /// Toggle an order between Active and Paused.
        /// </summary>
        public void TogglePause(int orderId)
        {
            var order = FindOrder(orderId);
            if (order == null) return;

            if (order.Status == OrderStatus.Active)
                order.Status = OrderStatus.Paused;
            else if (order.Status == OrderStatus.Paused)
                order.Status = OrderStatus.Active;

            EventBus.Publish(new OrderStatusChangedEvent
            {
                OrderId = orderId,
                NewStatus = order.Status
            });
        }

        /// <summary>
        /// Cancel an order and release all assigned settlers.
        /// </summary>
        public void CancelOrder(int orderId)
        {
            var order = FindOrder(orderId);
            if (order == null) return;

            order.Status = OrderStatus.Failed;
            order.AssignedSettlers.Clear();

            EventBus.Publish(new OrderStatusChangedEvent
            {
                OrderId = orderId,
                NewStatus = OrderStatus.Failed
            });

            Debug.Log($"[Order] Cancelled: {order.BuildSentence()} (id={orderId})");
        }

        /// <summary>Remove completed/failed orders from the list.</summary>
        public void CleanupFinished()
        {
            _orders.RemoveAll(o => o.Status == OrderStatus.Complete || o.Status == OrderStatus.Failed);
        }

        // ─── Order Assignment ────────────────────────────────

        /// <summary>
        /// Check if a settler has a player order (blocks auto-assignment).
        /// Used by ResourceTaskAssigner to skip settlers with orders.
        /// </summary>
        public bool HasOrderForSettler(string settlerName)
        {
            foreach (var order in _orders)
            {
                if (order.Status != OrderStatus.Active) continue;
                if (order.Negated) continue;

                // Named orders for this specific settler
                if (order.Subject == OrderSubject.Named &&
                    order.SettlerName == settlerName)
                    return true;

                // "All" orders apply to everyone
                if (order.Subject == OrderSubject.All)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Check if a negated order prevents a settler from doing a task type.
        /// </summary>
        public bool IsTaskForbidden(string settlerName, SettlerTaskType taskType)
        {
            foreach (var order in _orders)
            {
                if (order.Status != OrderStatus.Active) continue;
                if (!order.Negated) continue;

                // Check if this negated order applies to the settler
                bool applies = order.Subject == OrderSubject.All
                    || (order.Subject == OrderSubject.Named && order.SettlerName == settlerName);

                if (!applies) continue;

                // Check if the negated predicate matches the task type
                var forbiddenType = order.ToTaskType();
                if (forbiddenType == taskType)
                    return true;
            }
            return false;
        }

        private void TryAssignOrders()
        {
            var settlers = Object.FindObjectsByType<Settler>(FindObjectsSortMode.None);
            var campfire = GameObject.Find("Campfire");
            if (campfire == null) return;
            Vector3 basePos = campfire.transform.position;

            foreach (var order in _orders)
            {
                if (order.Status != OrderStatus.Active) continue;
                if (order.Negated) continue; // Negated orders are passive constraints

                TryAssignOrderToSettlers(order, settlers, basePos);
            }
        }

        private void TryAssignOrderToSettlers(OrderDefinition order, Settler[] settlers, Vector3 basePos)
        {
            switch (order.Subject)
            {
                case OrderSubject.Named:
                    // Find the specific settler and assign
                    foreach (var settler in settlers)
                    {
                        if (settler.name == order.SettlerName && !settler.HasTask)
                        {
                            TryExecuteOrder(order, settler, basePos);
                        }
                    }
                    break;

                case OrderSubject.NextFree:
                    // Find first idle settler without a task
                    if (order.AssignedSettlers.Count > 0) return; // Already assigned once
                    foreach (var settler in settlers)
                    {
                        if (!settler.HasTask)
                        {
                            if (TryExecuteOrder(order, settler, basePos))
                                return;
                        }
                    }
                    break;

                case OrderSubject.All:
                    // Assign to all idle settlers
                    foreach (var settler in settlers)
                    {
                        if (!settler.HasTask && !order.AssignedSettlers.Contains(settler.name))
                        {
                            TryExecuteOrder(order, settler, basePos);
                        }
                    }
                    break;
            }
        }

        /// <summary>
        /// Convert an order into a SettlerTask and assign it to a settler.
        /// </summary>
        private bool TryExecuteOrder(OrderDefinition order, Settler settler, Vector3 basePos)
        {
            var taskType = order.ToTaskType();
            if (taskType == SettlerTaskType.None) return false;

            // Determine target position
            Vector3 targetPos;
            ResourceNode targetResource = null;

            if (order.TargetPosition.HasValue)
            {
                targetPos = order.TargetPosition.Value;
            }
            else if (order.Objects.Count > 0)
            {
                // Try to find a matching resource node
                var result = FindResourceForOrder(order, settler.transform.position);
                if (result.HasValue)
                {
                    targetPos = result.Value.position;
                    targetResource = result.Value.node;
                }
                else
                {
                    return false; // No matching resource found
                }
            }
            else
            {
                return false;
            }

            float duration = SettlerTask.GetDefaultDuration(taskType);
            var task = new SettlerTask(taskType, targetPos, basePos, duration);
            task.TargetResource = targetResource;

            if (settler.AssignTask(task))
            {
                order.AssignedSettlers.Add(settler.name);

                // "Next Free" orders complete after one assignment
                if (order.Subject == OrderSubject.NextFree)
                    order.Status = OrderStatus.Complete;

                Debug.Log($"[Order] Assigned order '{order.BuildSentence()}' to {settler.name}");
                return true;
            }

            // Clean up reservation if assignment failed
            if (targetResource != null && targetResource.IsReserved)
                targetResource.Release();

            return false;
        }

        private (Vector3 position, ResourceNode node)? FindResourceForOrder(
            OrderDefinition order, Vector3 settlerPos)
        {
            var nodes = Object.FindObjectsByType<ResourceNode>(FindObjectsSortMode.None);
            ResourceNode best = null;
            float bestDist = float.MaxValue;

            foreach (var node in nodes)
            {
                if (!node.IsAvailable) continue;

                // Check if this node matches any of the order's objects
                bool matches = false;
                foreach (var obj in order.Objects)
                {
                    // Match by object ID to resource material/type
                    if (MatchesObject(node, obj))
                    {
                        matches = true;
                        break;
                    }
                }

                if (!matches) continue;

                float dist = Vector3.Distance(settlerPos, node.transform.position);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    best = node;
                }
            }

            if (best != null && best.TryReserve())
                return (best.transform.position, best);

            return null;
        }

        private bool MatchesObject(ResourceNode node, OrderObject obj)
        {
            // Match "everything_nearby" to any resource
            if (obj.Id == "everything_nearby") return true;

            // Match by resource type name
            string nodeType = node.Type.ToString().ToLower();
            if (obj.Id == nodeType) return true;

            // Match by material ID if the node has one
            if (node.MaterialId != null && obj.Id == node.MaterialId.ToLower())
                return true;

            return false;
        }

        private OrderDefinition FindOrder(int orderId)
        {
            foreach (var o in _orders)
                if (o.Id == orderId) return o;
            return null;
        }
    }
}
