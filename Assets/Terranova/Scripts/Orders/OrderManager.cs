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
    /// - Creates location markers for "Here" orders
    ///
    /// Settler AI priority (Feature 7.7):
    ///   1. Automatic needs: thirst, hunger, danger (not overridable)
    ///   2. Specific order for this settler (Named) — preempts auto-tasks
    ///   3. Group order ("All" / "Next Free")
    ///   4. Free decision (auto-assign from ResourceTaskAssigner)
    ///
    /// v0.4.16: Named orders preempt auto-tasks, All orders persist until
    /// cancelled, location markers for "Here" orders, cancel cleanup.
    /// </summary>
    public class OrderManager : MonoBehaviour
    {
        public static OrderManager Instance { get; private set; }

        private const float ASSIGN_CHECK_INTERVAL = 0.5f;
        private const float MARKER_HEIGHT = 3f;
        private const float MARKER_RADIUS = 0.3f;

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
            OrderQueryBridge.GetActiveOrderSentence = GetActiveOrderSentence;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
                OrderQueryBridge.HasOrderForSettler = null;
                OrderQueryBridge.IsTaskForbidden = null;
                OrderQueryBridge.GetActiveOrderSentence = null;
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

            // Create location marker for "Here" orders
            if (order.TargetPosition.HasValue)
                CreateLocationMarker(order);

            EventBus.Publish(new OrderCreatedEvent { OrderId = order.Id });
            Debug.Log($"[Order] Created: {order.BuildSentence()} (id={order.Id}, subject={order.Subject}, settlerName='{order.SettlerName}', pos={order.TargetPosition})");

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
        /// Cancel an order: release settlers, destroy marker, remove from list.
        /// v0.4.17: CancelOrder now fully removes the order (was only setting Failed).
        /// </summary>
        public void CancelOrder(int orderId)
        {
            var order = FindOrder(orderId);
            if (order == null) return;

            string sentence = order.BuildSentence();
            ReleaseSettlersFromOrder(order);
            DestroyLocationMarker(order);
            order.AssignedSettlers.Clear();
            _orders.Remove(order);

            EventBus.Publish(new OrderStatusChangedEvent
            {
                OrderId = orderId,
                NewStatus = OrderStatus.Failed
            });

            Debug.Log($"[Order] Cancelled + removed: {sentence} (id={orderId})");
        }

        /// <summary>
        /// Delete an order entirely from the list.
        /// </summary>
        public void DeleteOrder(int orderId)
        {
            var order = FindOrder(orderId);
            if (order == null) return;

            ReleaseSettlersFromOrder(order);
            DestroyLocationMarker(order);
            order.AssignedSettlers.Clear();
            _orders.Remove(order);

            EventBus.Publish(new OrderStatusChangedEvent
            {
                OrderId = orderId,
                NewStatus = OrderStatus.Failed
            });

            Debug.Log($"[Order] Deleted: {order.BuildSentence()} (id={orderId})");
        }

        /// <summary>Remove completed/failed orders from the list.</summary>
        public void CleanupFinished()
        {
            for (int i = _orders.Count - 1; i >= 0; i--)
            {
                var o = _orders[i];
                if (o.Status == OrderStatus.Complete || o.Status == OrderStatus.Failed)
                {
                    DestroyLocationMarker(o);
                    _orders.RemoveAt(i);
                }
            }
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

        /// <summary>
        /// Get the display sentence for a settler's active order, or null.
        /// Used by InfoPanel via OrderQueryBridge.
        /// </summary>
        public string GetActiveOrderSentence(string settlerName)
        {
            foreach (var order in _orders)
            {
                if (order.Status != OrderStatus.Active) continue;
                if (order.Negated) continue;

                if (order.Subject == OrderSubject.Named && order.SettlerName == settlerName)
                    return order.BuildSentence();

                if (order.Subject == OrderSubject.All)
                    return order.BuildSentence();
            }
            return null;
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
                    // Find the specific settler and assign.
                    // v0.4.16: Named orders preempt auto-tasks (settler interrupts
                    // current resource gathering to follow the player's explicit order).
                    bool foundSettler = false;
                    foreach (var settler in settlers)
                    {
                        if (settler.name != order.SettlerName) continue;
                        foundSettler = true;

                        if (!settler.HasTask)
                        {
                            Debug.Log($"[Order] Named: {settler.name} is idle, assigning '{order.BuildSentence()}'");
                            TryExecuteOrder(order, settler, basePos);
                        }
                        else if (settler.ActiveOrderId == order.Id)
                        {
                            // Already executing this order — nothing to do
                        }
                        else if (settler.CanBeInterrupted)
                        {
                            // Preempt auto-task for explicit player order
                            Debug.Log($"[Order] Named: preempting {settler.name}'s current task ({settler.StateName}) for '{order.BuildSentence()}'");
                            settler.CancelTask();
                            TryExecuteOrder(order, settler, basePos);
                        }
                        else
                        {
                            Debug.Log($"[Order] Named: {settler.name} is in critical state ({settler.StateName}), waiting");
                        }
                        break; // Only one settler matches a Named order
                    }
                    if (!foundSettler)
                    {
                        Debug.LogWarning($"[Order] Named: settler '{order.SettlerName}' not found! Available: [{GetSettlerNames(settlers)}]");
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
                    // v0.4.16: Assign to ALL idle settlers, every cycle.
                    // Orders persist until cancelled — no AssignedSettlers gate.
                    foreach (var settler in settlers)
                    {
                        if (!settler.HasTask)
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

                // For gather-type tasks, find nearest resource near the tap position
                if (taskType == SettlerTaskType.GatherMaterial || taskType == SettlerTaskType.GatherWood
                    || taskType == SettlerTaskType.GatherStone || taskType == SettlerTaskType.Hunt)
                {
                    var nearRes = FindNearestResourceNear(targetPos, 15f);
                    if (nearRes != null)
                    {
                        targetResource = nearRes;
                        targetPos = nearRes.transform.position;
                    }
                    else
                    {
                        Debug.Log($"[Order] No resource found near tap position {order.TargetPosition.Value} for {settler.name}");
                    }
                }
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
            task.OrderId = order.Id;

            if (settler.AssignTask(task))
            {
                order.AssignedSettlers.Add(settler.name);

                // "Next Free" orders complete after one assignment
                if (order.Subject == OrderSubject.NextFree)
                    order.Status = OrderStatus.Complete;

                Debug.Log($"[Order] ASSIGNED '{order.BuildSentence()}' to {settler.name} (orderId={order.Id}, taskType={taskType})");
                return true;
            }

            // Clean up reservation if assignment failed
            if (targetResource != null && targetResource.IsReserved)
                targetResource.Release();

            Debug.Log($"[Order] {settler.name} REJECTED order '{order.BuildSentence()}' (state={settler.StateName})");
            return false;
        }

        // ─── Settler Release ──────────────────────────────────

        /// <summary>
        /// Find all settlers executing tasks for this order and cancel those tasks.
        /// </summary>
        private void ReleaseSettlersFromOrder(OrderDefinition order)
        {
            var settlers = Object.FindObjectsByType<Settler>(FindObjectsSortMode.None);
            foreach (var settler in settlers)
            {
                if (settler.ActiveOrderId == order.Id)
                {
                    Debug.Log($"[Order] Releasing {settler.name} from cancelled order '{order.BuildSentence()}'");
                    settler.CancelTask();
                }
            }
        }

        // ─── Location Markers ─────────────────────────────────

        /// <summary>
        /// Create a visible flag/marker at the order's target position.
        /// v0.4.16: Players can see WHERE a "Here" order points to.
        /// </summary>
        private void CreateLocationMarker(OrderDefinition order)
        {
            if (!order.TargetPosition.HasValue) return;

            var pos = order.TargetPosition.Value;

            // Root object
            var marker = new GameObject($"OrderMarker_{order.Id}");
            marker.transform.position = pos;

            // Pole (thin cylinder)
            var pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pole.name = "Pole";
            pole.transform.SetParent(marker.transform, false);
            pole.transform.localPosition = new Vector3(0, MARKER_HEIGHT / 2f, 0);
            pole.transform.localScale = new Vector3(0.06f, MARKER_HEIGHT / 2f, 0.06f);
            var poleCol = pole.GetComponent<Collider>();
            if (poleCol != null) Object.Destroy(poleCol);
            var poleRend = pole.GetComponent<MeshRenderer>();
            if (poleRend != null)
            {
                poleRend.material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                poleRend.material.color = new Color(0.6f, 0.4f, 0.2f);
            }

            // Flag (small quad at top of pole)
            var flag = GameObject.CreatePrimitive(PrimitiveType.Quad);
            flag.name = "Flag";
            flag.transform.SetParent(marker.transform, false);
            flag.transform.localPosition = new Vector3(0.3f, MARKER_HEIGHT - 0.25f, 0);
            flag.transform.localScale = new Vector3(0.5f, 0.4f, 1f);
            var flagCol = flag.GetComponent<Collider>();
            if (flagCol != null) Object.Destroy(flagCol);
            var flagRend = flag.GetComponent<MeshRenderer>();
            if (flagRend != null)
            {
                flagRend.material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                flagRend.material.SetColor("_BaseColor", new Color(0.3f, 0.9f, 0.4f, 0.9f));
                flagRend.material.SetColor("_EmissionColor", new Color(0.15f, 0.5f, 0.2f) * 2f);
                flagRend.material.EnableKeyword("_EMISSION");
            }

            // Ground glow (flat cylinder at ground level)
            var glow = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            glow.name = "Glow";
            glow.transform.SetParent(marker.transform, false);
            glow.transform.localPosition = new Vector3(0, 0.02f, 0);
            glow.transform.localScale = new Vector3(MARKER_RADIUS * 2f, 0.01f, MARKER_RADIUS * 2f);
            var glowCol = glow.GetComponent<Collider>();
            if (glowCol != null) Object.Destroy(glowCol);
            var glowRend = glow.GetComponent<MeshRenderer>();
            if (glowRend != null)
            {
                glowRend.material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                glowRend.material.SetColor("_BaseColor", new Color(0.3f, 0.9f, 0.4f, 0.5f));
                glowRend.material.SetColor("_EmissionColor", new Color(0.2f, 0.6f, 0.3f) * 1.5f);
                glowRend.material.EnableKeyword("_EMISSION");
            }

            order.MarkerObject = marker;
            Debug.Log($"[Order] Created location marker at {pos} for order {order.Id}");
        }

        /// <summary>
        /// Remove a location marker when an order is cancelled/deleted/completed.
        /// </summary>
        private void DestroyLocationMarker(OrderDefinition order)
        {
            if (order.MarkerObject != null)
            {
                Object.Destroy(order.MarkerObject);
                order.MarkerObject = null;
                Debug.Log($"[Order] Removed location marker for order {order.Id}");
            }
        }

        // ─── Resource Matching ────────────────────────────────

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

        /// <summary>
        /// Find nearest available resource within radius of a world position.
        /// Used for "Here" orders where the player tapped the ground.
        /// </summary>
        private ResourceNode FindNearestResourceNear(Vector3 position, float radius)
        {
            var nodes = Object.FindObjectsByType<ResourceNode>(FindObjectsSortMode.None);
            ResourceNode best = null;
            float bestDist = radius;

            foreach (var node in nodes)
            {
                if (!node.IsAvailable) continue;
                float dist = Vector3.Distance(position, node.transform.position);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    best = node;
                }
            }

            if (best != null && best.TryReserve())
                return best;

            return null;
        }

        // ─── Helpers ──────────────────────────────────────────

        private OrderDefinition FindOrder(int orderId)
        {
            foreach (var o in _orders)
                if (o.Id == orderId) return o;
            return null;
        }

        private static string GetSettlerNames(Settler[] settlers)
        {
            var names = new System.Text.StringBuilder();
            for (int i = 0; i < settlers.Length; i++)
            {
                if (i > 0) names.Append(", ");
                names.Append(settlers[i].name);
            }
            return names.ToString();
        }
    }
}
