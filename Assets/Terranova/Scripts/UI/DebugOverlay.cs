// Comprehensive debug overlay for live game-state inspection.
//
// Displays: FPS, settlers (state, hunger, task), resources, buildings,
// terrain chunks, discovery progress, NavMesh status, and event log.
//
// Toggle with F3 in Editor and device builds.
// Tab sections: [1] Overview  [2] Settlers  [3] World  [4] Events

using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Terranova.Core;
using Terranova.Terrain;
using Terranova.Population;
using Terranova.Buildings;
using Terranova.Resources;
using Terranova.Discovery;

namespace Terranova.UI
{
    public class DebugOverlay : MonoBehaviour
    {
        private const int FONT_SIZE = 13;
        private const float UPDATE_INTERVAL = 0.25f; // 4 Hz refresh
        private const int MAX_EVENT_LOG = 20;

        private GameObject _panel;
        private Text _debugText;
        private float _updateTimer;
        private readonly StringBuilder _sb = new();

        private bool _isVisible;
        private int _activeTab; // 0=Overview, 1=Settlers, 2=World, 3=Events
        private static readonly string[] TAB_NAMES = { "Overview", "Settlers", "World", "Events" };

        // FPS tracking
        private float _fpsAccumulator;
        private int _fpsFrameCount;
        private float _fpsTimer;
        private float _currentFps;
        private float _minFps = float.MaxValue;
        private float _maxFps;

        // Event log (ring buffer)
        private readonly List<string> _eventLog = new();

        // ─── Lifecycle ──────────────────────────────────────────

        private void Start()
        {
            _isVisible = false;
            CreateOverlayUI();
            _panel.SetActive(false);
        }

        private void OnEnable()
        {
            EventBus.Subscribe<ResourceChangedEvent>(OnResourceChanged);
            EventBus.Subscribe<BuildingPlacedEvent>(OnBuildingPlaced);
            EventBus.Subscribe<BuildingCompletedEvent>(OnBuildingCompleted);
            EventBus.Subscribe<SettlerDiedEvent>(OnSettlerDied);
            EventBus.Subscribe<DiscoveryMadeEvent>(OnDiscoveryMade);
            EventBus.Subscribe<ResourceDeliveredEvent>(OnResourceDelivered);
            EventBus.Subscribe<PopulationChangedEvent>(OnPopulationChanged);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<ResourceChangedEvent>(OnResourceChanged);
            EventBus.Unsubscribe<BuildingPlacedEvent>(OnBuildingPlaced);
            EventBus.Unsubscribe<BuildingCompletedEvent>(OnBuildingCompleted);
            EventBus.Unsubscribe<SettlerDiedEvent>(OnSettlerDied);
            EventBus.Unsubscribe<DiscoveryMadeEvent>(OnDiscoveryMade);
            EventBus.Unsubscribe<ResourceDeliveredEvent>(OnResourceDelivered);
            EventBus.Unsubscribe<PopulationChangedEvent>(OnPopulationChanged);
        }

        private void Update()
        {
            // Track FPS every frame (even when hidden, so data is ready on open)
            _fpsAccumulator += Time.unscaledDeltaTime;
            _fpsFrameCount++;
            _fpsTimer += Time.unscaledDeltaTime;
            if (_fpsTimer >= 0.5f)
            {
                _currentFps = _fpsFrameCount / _fpsAccumulator;
                if (_currentFps < _minFps) _minFps = _currentFps;
                if (_currentFps > _maxFps) _maxFps = _currentFps;
                _fpsAccumulator = 0f;
                _fpsFrameCount = 0;
                _fpsTimer = 0f;
            }

            var kb = Keyboard.current;
            if (kb == null) return;

            // F3 toggles visibility
            if (kb.f3Key.wasPressedThisFrame)
            {
                _isVisible = !_isVisible;
                _panel.SetActive(_isVisible);
                if (_isVisible) RefreshDisplay();
            }

            if (!_isVisible) return;

            // Tab switching: 1-4 keys
            if (kb.digit1Key.wasPressedThisFrame) _activeTab = 0;
            if (kb.digit2Key.wasPressedThisFrame) _activeTab = 1;
            if (kb.digit3Key.wasPressedThisFrame) _activeTab = 2;
            if (kb.digit4Key.wasPressedThisFrame) _activeTab = 3;

            // Reset min/max FPS with R
            if (kb.rKey.wasPressedThisFrame)
            {
                _minFps = float.MaxValue;
                _maxFps = 0f;
            }

            _updateTimer -= Time.unscaledDeltaTime;
            if (_updateTimer > 0f) return;
            _updateTimer = UPDATE_INTERVAL;

            RefreshDisplay();
        }

        // ─── Display Rendering ──────────────────────────────────

        private void RefreshDisplay()
        {
            _sb.Clear();

            // Header with tab bar
            _sb.Append("=== DEBUG (F3) ===  ");
            for (int i = 0; i < TAB_NAMES.Length; i++)
            {
                if (i == _activeTab)
                    _sb.Append($"[{i + 1}:{TAB_NAMES[i]}] ");
                else
                    _sb.Append($" {i + 1}:{TAB_NAMES[i]}  ");
            }
            _sb.AppendLine();

            // FPS line (always visible)
            string fpsColor = _currentFps >= 30 ? "ok" : _currentFps >= 15 ? "WARN" : "CRIT";
            _sb.AppendLine($"FPS: {_currentFps:F0} ({fpsColor})  min:{_minFps:F0}  max:{_maxFps:F0}  [R=reset]");
            _sb.AppendLine($"Time: {Time.time:F0}s  Scale: {Time.timeScale:F1}x  dt: {Time.deltaTime * 1000:F1}ms");
            _sb.AppendLine();

            switch (_activeTab)
            {
                case 0: RenderOverviewTab(); break;
                case 1: RenderSettlersTab(); break;
                case 2: RenderWorldTab(); break;
                case 3: RenderEventsTab(); break;
            }

            _debugText.text = _sb.ToString();
        }

        // ─── Tab 1: Overview ────────────────────────────────────

        private void RenderOverviewTab()
        {
            // Resources
            _sb.AppendLine("--- Resources ---");
            var rm = ResourceManager.Instance;
            if (rm != null)
            {
                _sb.AppendLine($"  Wood: {rm.Wood}  Stone: {rm.Stone}  Food: {rm.Food}");
                _sb.AppendLine($"  Resin: {rm.Resin}  Flint: {rm.Flint}  Fiber: {rm.PlantFiber}");
            }
            else
            {
                _sb.AppendLine("  ResourceManager not found!");
            }
            _sb.AppendLine();

            // Population summary
            _sb.AppendLine("--- Population ---");
            var settlers = Object.FindObjectsByType<Settler>(FindObjectsSortMode.None);
            int idle = 0, working = 0, eating = 0, starving = 0;
            float avgHunger = 0f;
            foreach (var s in settlers)
            {
                avgHunger += s.Hunger;
                if (s.IsStarving) starving++;
                string state = s.StateName;
                if (state.StartsWith("Idle")) idle++;
                else if (state == "Eating" || state == "WalkingToEat") eating++;
                else working++;
            }
            if (settlers.Length > 0) avgHunger /= settlers.Length;
            _sb.AppendLine($"  Total: {settlers.Length}  Idle: {idle}  Working: {working}  Eating: {eating}");
            _sb.AppendLine($"  Avg Hunger: {avgHunger:F0}/100  Starving: {starving}");
            _sb.AppendLine();

            // Buildings summary
            _sb.AppendLine("--- Buildings ---");
            var buildings = Object.FindObjectsByType<Building>(FindObjectsSortMode.None);
            int constructed = 0, underConstruction = 0;
            foreach (var b in buildings)
            {
                if (b.IsConstructed) constructed++;
                else underConstruction++;
            }
            _sb.AppendLine($"  Total: {buildings.Length}  Ready: {constructed}  Building: {underConstruction}");
            _sb.AppendLine();

            // Resource nodes
            _sb.AppendLine("--- Resource Nodes ---");
            var nodes = Object.FindObjectsByType<ResourceNode>(FindObjectsSortMode.None);
            int available = 0, reserved = 0, depleted = 0;
            foreach (var n in nodes)
            {
                if (n.IsDepleted) depleted++;
                else if (n.IsReserved) reserved++;
                else available++;
            }
            _sb.AppendLine($"  Total: {nodes.Length}  Available: {available}  Reserved: {reserved}  Depleted: {depleted}");
            _sb.AppendLine();

            // Discovery
            _sb.AppendLine("--- Discoveries ---");
            var dsm = DiscoveryStateManager.Instance;
            if (dsm != null)
            {
                _sb.AppendLine($"  Completed: {dsm.CompletedCount}");
                foreach (var d in dsm.CompletedDiscoveries)
                    _sb.AppendLine($"    - {d}");
            }
            else
            {
                _sb.AppendLine("  DiscoveryStateManager not found!");
            }

            // NavMesh
            _sb.AppendLine();
            _sb.AppendLine("--- NavMesh ---");
            var world = WorldManager.Instance;
            _sb.AppendLine($"  Ready: {(world != null ? world.IsNavMeshReady.ToString() : "N/A")}");
        }

        // ─── Tab 2: Settlers ────────────────────────────────────

        private void RenderSettlersTab()
        {
            _sb.AppendLine("--- All Settlers ---");
            _sb.AppendLine("Name            State             Task         Hunger  Pos");
            _sb.AppendLine(new string('-', 80));

            var settlers = Object.FindObjectsByType<Settler>(FindObjectsSortMode.InstanceID);
            foreach (var s in settlers)
            {
                string name = s.name.Length > 15 ? s.name[..15] : s.name.PadRight(15);
                string state = s.StateName.Length > 17 ? s.StateName[..17] : s.StateName.PadRight(17);
                string task = s.CurrentTask != null ? s.CurrentTask.TaskType.ToString() : "None";
                task = task.Length > 12 ? task[..12] : task.PadRight(12);
                string hunger = $"{s.Hunger:F0}/100";
                if (s.IsStarving) hunger += "!";
                hunger = hunger.PadRight(7);
                Vector3 p = s.transform.position;
                string pos = $"({p.x:F0},{p.z:F0})";

                _sb.AppendLine($"{name} {state} {task} {hunger} {pos}");
            }

            if (settlers.Length == 0)
                _sb.AppendLine("  (no settlers)");
        }

        // ─── Tab 3: World ───────────────────────────────────────

        private void RenderWorldTab()
        {
            var world = WorldManager.Instance;

            _sb.AppendLine("--- Terrain ---");
            if (world != null)
            {
                _sb.AppendLine($"  World Size: {world.WorldBlocksX}x{world.WorldBlocksZ} blocks");
                _sb.AppendLine($"  NavMesh Ready: {world.IsNavMeshReady}");

                var chunks = world.GetComponentsInChildren<ChunkRenderer>();
                int totalVerts = 0;
                int[] lodCounts = new int[3];

                foreach (var chunk in chunks)
                {
                    if (!chunk.gameObject.activeInHierarchy) continue;
                    int lod = chunk.CurrentLod;
                    if (lod >= 0 && lod < 3) lodCounts[lod]++;

                    var mf = chunk.GetComponent<MeshFilter>();
                    if (mf != null && mf.sharedMesh != null)
                        totalVerts += mf.sharedMesh.vertexCount;
                }

                _sb.AppendLine($"  Chunks: {chunks.Length}  Vertices: {totalVerts:N0}");
                _sb.AppendLine($"  LOD0 (full): {lodCounts[0]}  LOD1 (med): {lodCounts[1]}  LOD2 (low): {lodCounts[2]}");
            }
            else
            {
                _sb.AppendLine("  WorldManager not found!");
            }
            _sb.AppendLine();

            // Camera
            _sb.AppendLine("--- Camera ---");
            var cam = Camera.main;
            if (cam != null)
            {
                Vector3 pos = cam.transform.position;
                Vector3 rot = cam.transform.eulerAngles;
                _sb.AppendLine($"  Pos: ({pos.x:F1}, {pos.y:F1}, {pos.z:F1})");
                _sb.AppendLine($"  Rot: ({rot.x:F0}, {rot.y:F0}, {rot.z:F0})");
                _sb.AppendLine($"  FOV: {cam.fieldOfView:F0}  Near/Far: {cam.nearClipPlane:F1}/{cam.farClipPlane:F0}");
            }
            _sb.AppendLine();

            // Buildings detail
            _sb.AppendLine("--- Buildings Detail ---");
            var buildings = Object.FindObjectsByType<Building>(FindObjectsSortMode.None);
            foreach (var b in buildings)
            {
                string bName = b.Definition != null ? b.Definition.DisplayName : b.name;
                string status = b.IsConstructed ? "Ready" : $"Building {b.ConstructionProgress * 100:F0}%";
                string worker = b.HasWorker ? "Worker" : "-";
                Vector3 bp = b.transform.position;
                _sb.AppendLine($"  {bName}: {status}  {worker}  ({bp.x:F0},{bp.z:F0})");
            }
            if (buildings.Length == 0)
                _sb.AppendLine("  (no buildings)");
            _sb.AppendLine();

            // Resource nodes by type
            _sb.AppendLine("--- Resource Nodes by Type ---");
            var nodes = Object.FindObjectsByType<ResourceNode>(FindObjectsSortMode.None);
            var typeCounts = new Dictionary<ResourceType, int[]>();
            foreach (var n in nodes)
            {
                if (!typeCounts.ContainsKey(n.Type))
                    typeCounts[n.Type] = new int[3]; // [available, reserved, depleted]

                if (n.IsDepleted) typeCounts[n.Type][2]++;
                else if (n.IsReserved) typeCounts[n.Type][1]++;
                else typeCounts[n.Type][0]++;
            }
            foreach (var kvp in typeCounts)
            {
                _sb.AppendLine($"  {kvp.Key}: avail={kvp.Value[0]} rsv={kvp.Value[1]} dep={kvp.Value[2]}");
            }
            if (nodes.Length == 0)
                _sb.AppendLine("  (no resource nodes)");

            // Memory
            _sb.AppendLine();
            _sb.AppendLine("--- Memory ---");
            long totalMem = System.GC.GetTotalMemory(false);
            _sb.AppendLine($"  GC Heap: {totalMem / (1024 * 1024):N0} MB");
            _sb.AppendLine($"  GC Collections: G0={System.GC.CollectionCount(0)} G1={System.GC.CollectionCount(1)} G2={System.GC.CollectionCount(2)}");
        }

        // ─── Tab 4: Events ──────────────────────────────────────

        private void RenderEventsTab()
        {
            _sb.AppendLine("--- Event Log (newest first) ---");
            _sb.AppendLine();

            if (_eventLog.Count == 0)
            {
                _sb.AppendLine("  (no events recorded yet)");
            }
            else
            {
                for (int i = _eventLog.Count - 1; i >= 0; i--)
                    _sb.AppendLine($"  {_eventLog[i]}");
            }
        }

        // ─── Event Handlers ─────────────────────────────────────

        private void LogEvent(string msg)
        {
            string entry = $"[{Time.time:F1}s] {msg}";
            _eventLog.Add(entry);
            if (_eventLog.Count > MAX_EVENT_LOG)
                _eventLog.RemoveAt(0);
        }

        private void OnResourceChanged(ResourceChangedEvent evt) { }
        // Intentionally silent – fires too often for the log.

        private void OnBuildingPlaced(BuildingPlacedEvent evt)
        {
            LogEvent($"Building placed: {evt.BuildingName} at ({evt.Position.x:F0},{evt.Position.z:F0})");
        }

        private void OnBuildingCompleted(BuildingCompletedEvent evt)
        {
            LogEvent($"Building done: {evt.BuildingName}");
        }

        private void OnSettlerDied(SettlerDiedEvent evt)
        {
            LogEvent($"DEATH: {evt.SettlerName} ({evt.CauseOfDeath})");
        }

        private void OnDiscoveryMade(DiscoveryMadeEvent evt)
        {
            LogEvent($"Discovery: {evt.DiscoveryName}");
        }

        private void OnResourceDelivered(ResourceDeliveredEvent evt)
        {
            LogEvent($"Delivered: {evt.ActualResourceType} ({evt.TaskType})");
        }

        private void OnPopulationChanged(PopulationChangedEvent evt)
        {
            LogEvent($"Population: {evt.CurrentPopulation}");
        }

        // ─── UI Construction ────────────────────────────────────

        private void CreateOverlayUI()
        {
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas == null) canvas = GetComponent<Canvas>();
            Transform parent = canvas != null ? canvas.transform : transform;

            // Panel – left side of screen, full height
            _panel = new GameObject("DebugOverlayPanel");
            _panel.transform.SetParent(parent, false);
            var panelImage = _panel.AddComponent<Image>();
            panelImage.color = new Color(0f, 0f, 0f, 0.85f);
            panelImage.raycastTarget = false;
            var panelRect = _panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0, 0);
            panelRect.anchorMax = new Vector2(0, 1);
            panelRect.pivot = new Vector2(0, 0.5f);
            panelRect.anchoredPosition = new Vector2(5, 0);
            panelRect.sizeDelta = new Vector2(520, 0);

            // Text
            var textGo = new GameObject("DebugText");
            textGo.transform.SetParent(_panel.transform, false);
            var textRect = textGo.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(8, 8);
            textRect.offsetMax = new Vector2(-8, -8);

            _debugText = textGo.AddComponent<Text>();
            _debugText.font = UnityEngine.Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _debugText.fontSize = FONT_SIZE;
            _debugText.color = new Color(0.2f, 1f, 0.6f); // Cyan-green terminal
            _debugText.alignment = TextAnchor.UpperLeft;
            _debugText.horizontalOverflow = HorizontalWrapMode.Overflow;
            _debugText.verticalOverflow = VerticalWrapMode.Overflow;
            _debugText.raycastTarget = false;
        }

        /// <summary>Whether the debug overlay is currently visible.</summary>
        public bool IsVisible => _isVisible;
    }
}
