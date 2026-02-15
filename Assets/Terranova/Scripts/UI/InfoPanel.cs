using UnityEngine;
using UnityEngine.UI;
using Terranova.Core;
using Terranova.Population;
using Terranova.Buildings;

namespace Terranova.UI
{
    /// <summary>
    /// Displays info panel for selected settlers or buildings.
    ///
    /// Story 6.1: Tap on settler -> name, hunger bar, current task.
    ///            Tap on building -> type, status, assigned worker.
    /// Story 6.2: Deselect closes panel.
    /// Story 6.3: Long press shows extended info (all stats).
    ///
    /// MS4 Changes:
    ///   Feature 3.4 - Tool info when tapping settler: name, quality badge,
    ///                 durability bar, capabilities.
    ///   Feature 4.5 - Full needs panel: thirst, hunger, shelter, health.
    ///
    /// The panel is anchored bottom-left and updates every frame while visible.
    /// </summary>
    public class InfoPanel : MonoBehaviour
    {
        // ─── Settings ──────────────────────────────────────────
        private const float PANEL_WIDTH = 300f;
        private const float PANEL_PADDING = 12f;
        private const int FONT_SIZE = 18;
        private const int FONT_SIZE_SMALL = 15;
        private const int FONT_SIZE_TITLE = 22;
        private const int FONT_SIZE_TINY = 12;

        // ─── State ─────────────────────────────────────────────
        private GameObject _selectedObject;
        private bool _isDetailView;
        private bool _isVisible;

        // ─── UI References ─────────────────────────────────────
        private static readonly Color PANEL_COLOR_BASIC = new Color(0.1f, 0.1f, 0.1f, 0.85f);
        private static readonly Color PANEL_COLOR_DETAIL = new Color(0.08f, 0.12f, 0.2f, 0.92f);

        // Bar colors
        private static readonly Color THIRST_COLOR = new Color(0.3f, 0.6f, 1f);         // Blue
        private static readonly Color HUNGER_COLOR = new Color(1f, 0.6f, 0.2f);          // Orange
        private static readonly Color DURABILITY_GREEN = new Color(0.3f, 0.8f, 0.3f);
        private static readonly Color DURABILITY_YELLOW = new Color(0.9f, 0.8f, 0.2f);
        private static readonly Color DURABILITY_RED = new Color(0.9f, 0.2f, 0.2f);

        private GameObject _panelRoot;
        private Image _panelImage;
        private Text _titleText;
        private Text _traitText;
        private Text _infoText;

        // Hunger bar (legacy, kept for building worker display)
        private RectTransform _hungerBarFill;
        private Image _hungerBarFillImage;
        private GameObject _hungerBarRoot;

        // Needs panel (Feature 4.5)
        private GameObject _needsRoot;
        private RectTransform _thirstBarFill;
        private Image _thirstBarFillImage;
        private Text _thirstLabel;
        private RectTransform _hungerNeedsBarFill;
        private Image _hungerNeedsBarFillImage;
        private Text _hungerNeedsLabel;
        private Text _shelterStatusText;
        private Text _healthStatusText;

        // Tool info panel (Feature 3.4)
        private GameObject _toolRoot;
        private Text _toolNameText;
        private Text _toolQualityText;
        private RectTransform _durabilityBarFill;
        private Image _durabilityBarFillImage;
        private Text _durabilityLabel;
        private Text _toolCapabilitiesText;

        // ─── Lifecycle ─────────────────────────────────────────

        private void Start()
        {
            CreatePanel();
            HidePanel();

            EventBus.Subscribe<SelectionChangedEvent>(OnSelectionChanged);
        }

        private void Update()
        {
            if (_isVisible && _selectedObject != null)
                RefreshContent();
        }

        private void OnDestroy()
        {
            EventBus.Unsubscribe<SelectionChangedEvent>(OnSelectionChanged);
        }

        // ─── Event Handler ─────────────────────────────────────

        private void OnSelectionChanged(SelectionChangedEvent evt)
        {
            _selectedObject = evt.SelectedObject;
            _isDetailView = evt.IsDetailView;

            if (_selectedObject == null)
            {
                HidePanel();
            }
            else
            {
                _panelImage.color = _isDetailView ? PANEL_COLOR_DETAIL : PANEL_COLOR_BASIC;
                ShowPanel();
                RefreshContent();
            }
        }

        // ─── Content Rendering ──────────────────────────────────

        private void RefreshContent()
        {
            if (_selectedObject == null)
            {
                HidePanel();
                return;
            }

            var settler = _selectedObject.GetComponent<Settler>();
            if (settler != null)
            {
                RefreshSettlerInfo(settler);
                return;
            }

            var building = _selectedObject.GetComponent<Building>();
            if (building != null)
            {
                RefreshBuildingInfo(building);
                return;
            }

            var shelter = _selectedObject.GetComponent<NaturalShelter>();
            if (shelter != null)
            {
                RefreshShelterInfo(shelter);
                return;
            }
        }

        /// <summary>
        /// Show settler info with MS4 features:
        ///   - Feature 3.4: Tool info (name, quality badge, durability bar, capabilities)
        ///   - Feature 4.5: Full needs panel (thirst, hunger, shelter, health)
        /// Basic view: quick overview. Detail view (long press): comprehensive stats.
        /// </summary>
        private void RefreshSettlerInfo(Settler settler)
        {
            // Show needs panel, hide legacy hunger bar for settlers
            _hungerBarRoot.SetActive(false);
            _needsRoot.SetActive(true);

            // ─── Needs: Thirst bar (blue) ──────────────────────
            float thirstPct = GetSettlerThirstPercent(settler);
            _thirstBarFill.anchorMax = new Vector2(Mathf.Clamp01(1f - thirstPct), 1f);
            _thirstBarFillImage.color = THIRST_COLOR;
            string thirstState = GetSettlerThirstState(settler);
            _thirstLabel.text = $"Thirst: {thirstState}";

            // ─── Needs: Hunger bar (orange) ────────────────────
            float hungerPct = settler.HungerPercent;
            _hungerNeedsBarFill.anchorMax = new Vector2(Mathf.Clamp01(1f - hungerPct), 1f);

            // Color intensity based on hunger severity
            if (hungerPct > 0.75f)
                _hungerNeedsBarFillImage.color = DURABILITY_RED;
            else if (hungerPct > 0.5f)
                _hungerNeedsBarFillImage.color = HUNGER_COLOR;
            else
                _hungerNeedsBarFillImage.color = HUNGER_COLOR;
            _hungerNeedsLabel.text = $"Hunger: {settler.Hunger:F0}/100";

            // ─── Needs: Shelter status ─────────────────────────
            string shelterStatus = GetSettlerShelterStatus(settler);
            _shelterStatusText.text = $"Shelter: {shelterStatus}";

            // ─── Needs: Health status ──────────────────────────
            string healthStatus = GetSettlerHealthStatus(settler);
            _healthStatusText.text = $"Health: {healthStatus}";

            // ─── Tool Info (Feature 3.4) ───────────────────────
            RefreshToolInfo(settler);

            // ─── Trait (dedicated label above needs, Feature 6.1) ────
            if (settler.HasTrait)
            {
                _traitText.gameObject.SetActive(true);
                _traitText.text = $"Trait: {settler.TraitIcon} {settler.TraitName}";
            }
            else
            {
                _traitText.gameObject.SetActive(false);
            }

            // ─── Task & State Info ─────────────────────────────
            string task = settler.HasTask
                ? settler.CurrentTask?.TaskType.ToString() ?? "Eating"
                : "Idle";
            string state = settler.StateName;

            if (_isDetailView)
            {
                _titleText.text = $"-- {settler.name} --";

                string info = $"State: {state}";
                info += $"\nTask: {task}";

                if (settler.IsStarving)
                    info += "\nSTARVING!";

                if (settler.CurrentTask != null)
                {
                    var taskObj = settler.CurrentTask;
                    info += $"\n\nTask Details:";
                    info += $"\n  Type: {taskObj.TaskType}";
                    info += $"\n  Work Time: {taskObj.WorkDuration:F1}s";
                    if (taskObj.IsSpecialized)
                        info += "\n  Specialized: Yes";
                    info += $"\n  Speed: {taskObj.SpeedMultiplier:F1}x";
                }

                // Capabilities based on tool
                info += GetToolCapabilitiesDetail(settler);

                // Feature 6.3: Experience bars (only categories with > 0 XP)
                var experience = settler.AllExperience;
                if (experience.Count > 0)
                {
                    info += "\n\n--- Experience ---";
                    foreach (var kvp in experience)
                    {
                        int level = settler.GetExperienceLevel(kvp.Key);
                        float xp = kvp.Value;
                        string bar = new string('\u2588', level) + new string('\u2591', 10 - level);
                        info += $"\n{kvp.Key}: [{bar}] Lv{level}";
                    }
                }

                var pos = settler.transform.position;
                info += $"\n\nPosition: ({pos.x:F0}, {pos.z:F0})";

                _infoText.text = info;
            }
            else
            {
                _titleText.text = settler.name;
                _infoText.text = $"Task: {task}\nState: {state}";
            }
        }

        /// <summary>
        /// Feature 3.4: Refresh the tool info section for a settler.
        /// Shows current tool name, quality badge (Q1-Q5 with color),
        /// durability bar (green -> yellow -> red), and "No tool" state.
        /// </summary>
        private void RefreshToolInfo(Settler settler)
        {
            // Try to get tool data from settler via reflection-safe property access.
            // The EquippedTool property will be added to Settler in the tool system implementation.
            // For now, use a duck-typing approach: check if the settler has tool fields.
            ToolDefinition toolDef = null;
            int durability = 0;
            int maxDurability = 1;

            // Access tool data if the settler exposes it
            var settlerType = settler.GetType();

            // Try to read EquippedToolId property
            var toolIdProp = settlerType.GetProperty("EquippedToolId");
            if (toolIdProp != null)
            {
                string toolId = toolIdProp.GetValue(settler) as string;
                if (!string.IsNullOrEmpty(toolId))
                    toolDef = ToolDatabase.Get(toolId);
            }

            // Try to read ToolDurability property
            var durProp = settlerType.GetProperty("ToolDurability");
            if (durProp != null)
            {
                object durVal = durProp.GetValue(settler);
                if (durVal is int d) durability = d;
                else if (durVal is float f) durability = (int)f;
            }

            // Try to read ToolMaxDurability property
            var maxDurProp = settlerType.GetProperty("ToolMaxDurability");
            if (maxDurProp != null)
            {
                object maxVal = maxDurProp.GetValue(settler);
                if (maxVal is int m) maxDurability = m;
                else if (maxVal is float f) maxDurability = (int)f;
            }

            if (toolDef != null && maxDurability > 0)
            {
                _toolRoot.SetActive(true);

                // Tool name
                _toolNameText.text = toolDef.DisplayName;

                // Quality badge with color
                _toolQualityText.text = $"Q{toolDef.Quality}";
                _toolQualityText.color = toolDef.QualityColor;

                // Durability bar
                float durPct = (float)durability / maxDurability;
                _durabilityBarFill.anchorMax = new Vector2(Mathf.Clamp01(durPct), 1f);

                // Green -> Yellow -> Red based on remaining durability
                if (durPct > 0.5f)
                    _durabilityBarFillImage.color = DURABILITY_GREEN;
                else if (durPct > 0.2f)
                    _durabilityBarFillImage.color = DURABILITY_YELLOW;
                else
                    _durabilityBarFillImage.color = DURABILITY_RED;

                _durabilityLabel.text = $"{durability}/{maxDurability}";

                // Capabilities text
                _toolCapabilitiesText.text = $"Speed: x{toolDef.GatherSpeedMultiplier:F1}";
            }
            else
            {
                // No tool equipped
                _toolRoot.SetActive(true);
                _toolNameText.text = "No tool";
                _toolNameText.color = new Color(0.6f, 0.6f, 0.6f);
                _toolQualityText.text = "";
                _durabilityBarFill.anchorMax = new Vector2(0f, 1f);
                _durabilityBarFillImage.color = DURABILITY_RED;
                _durabilityLabel.text = "--";
                _toolCapabilitiesText.text = "Bare hands only";
            }
        }

        /// <summary>
        /// Feature 3.4: Get detailed tool capabilities for detail view.
        /// Shows what settler CAN do vs CANNOT do with current tool.
        /// </summary>
        private string GetToolCapabilitiesDetail(Settler settler)
        {
            var settlerType = settler.GetType();
            var toolIdProp = settlerType.GetProperty("EquippedToolId");
            string toolId = toolIdProp?.GetValue(settler) as string;
            ToolDefinition toolDef = !string.IsNullOrEmpty(toolId) ? ToolDatabase.Get(toolId) : null;

            int toolQuality = toolDef?.Quality ?? 0;

            string capabilities = "\n\nCapabilities:";

            // Check all material types and report which can/cannot be gathered
            var allMaterials = MaterialDatabase.All;
            bool canGatherHardwood = false;
            bool canGatherGranite = false;
            bool canHunt = false;

            foreach (var kvp in allMaterials)
            {
                var mat = kvp.Value;
                bool canGather = !mat.RequiresTool || toolQuality >= mat.MinToolQuality;

                if (mat.Category == MaterialCategory.Wood && mat.Id == "hardwood")
                    canGatherHardwood = canGather;
                else if (mat.Category == MaterialCategory.Stone && mat.Id == "granite")
                    canGatherGranite = canGather;
                else if (mat.Category == MaterialCategory.Animal && mat.Id == "large_meat")
                    canHunt = canGather;
            }

            capabilities += $"\n  Gather wood: Yes";
            capabilities += $"\n  Hardwood: {(canGatherHardwood ? "Yes" : "No (Q4+ required)")}";
            capabilities += $"\n  Gather stone: Yes";
            capabilities += $"\n  Granite: {(canGatherGranite ? "Yes" : "No (Q3+ required)")}";
            capabilities += $"\n  Large game: {(canHunt ? "Yes" : "No (Q3+ required)")}";

            return capabilities;
        }

        // ─── Settler Needs Helpers ──────────────────────────────

        /// <summary>
        /// Get thirst as a percent (0.0 = hydrated, 1.0 = dying).
        /// Uses reflection to read ThirstPercent if available, falls back to 0.
        /// </summary>
        private float GetSettlerThirstPercent(Settler settler)
        {
            var prop = settler.GetType().GetProperty("ThirstPercent");
            if (prop != null)
            {
                object val = prop.GetValue(settler);
                if (val is float f) return f;
            }
            return 0f;
        }

        /// <summary>
        /// Get the thirst state label. Uses reflection for ThirstState enum if present.
        /// </summary>
        private string GetSettlerThirstState(Settler settler)
        {
            var prop = settler.GetType().GetProperty("CurrentThirstState");
            if (prop != null)
            {
                object val = prop.GetValue(settler);
                if (val != null) return val.ToString();
            }

            // Fallback: derive from percent
            float pct = GetSettlerThirstPercent(settler);
            if (pct > 0.9f) return "Dying";
            if (pct > 0.6f) return "Dehydrated";
            if (pct > 0.3f) return "Thirsty";
            return "Hydrated";
        }

        /// <summary>
        /// Get shelter status text. Reads directly from settler.
        /// </summary>
        private string GetSettlerShelterStatus(Settler settler)
        {
            return settler.CurrentShelterState.ToString();
        }

        /// <summary>
        /// Get health status text. Now reads directly from settler's HealthStatusDisplay.
        /// </summary>
        private string GetSettlerHealthStatus(Settler settler)
        {
            return settler.HealthStatus;
        }

        // ─── Building Info ──────────────────────────────────────

        /// <summary>
        /// Show building info: type, construction status, worker.
        /// Story 6.1: Basic info on tap. Story 6.3: Extended info on long press.
        /// </summary>
        private void RefreshBuildingInfo(Building building)
        {
            string displayName = building.Definition != null
                ? building.Definition.DisplayName : building.name;

            // Hide needs, tool, and trait panels for buildings
            _hungerBarRoot.SetActive(false);
            _needsRoot.SetActive(false);
            _toolRoot.SetActive(false);
            _traitText.gameObject.SetActive(false);

            // Basic status
            string statusLine;
            if (!building.IsConstructed)
            {
                float progress = building.ConstructionProgress * 100f;
                statusLine = $"Under construction: {progress:F0}%";
                statusLine += building.IsBeingBuilt ? "\nBuilder assigned" : "\nWaiting for builder";
            }
            else
            {
                statusLine = "Operational";

                if (building.HasWorker && building.AssignedWorker != null)
                {
                    var worker = building.AssignedWorker.GetComponent<Settler>();
                    string workerName = worker != null ? worker.name : building.AssignedWorker.name;
                    statusLine += $"\nWorker: {workerName}";
                }
                else if (building.Definition != null
                         && building.Definition.Type != BuildingType.Campfire
                         && building.Definition.Type != BuildingType.SimpleHut)
                {
                    statusLine += "\nNo worker assigned";
                }
            }

            if (_isDetailView)
            {
                _titleText.text = $"-- {displayName} --";

                string info = statusLine;

                if (building.Definition != null)
                {
                    var def = building.Definition;
                    info += $"\n\nBuilding Type: {def.Type}";
                    info += $"\nBuild Cost: {def.WoodCost} Wood, {def.StoneCost} Stone";
                    info += $"\nFootprint: {def.FootprintSize.x}x{def.FootprintSize.y}";
                    info += $"\nHeight: {def.VisualHeight:F1}m";

                    if (!building.IsConstructed)
                    {
                        float buildTime = building.GetBuildStepDuration();
                        info += $"\n\nBuild Time: {buildTime:F0}s";
                        info += $"\nProgress: {building.ConstructionProgress * 100f:F1}%";
                    }
                }

                // Worker details
                if (building.HasWorker && building.AssignedWorker != null)
                {
                    var worker = building.AssignedWorker.GetComponent<Settler>();
                    if (worker != null)
                    {
                        info += $"\n\nWorker Details:";
                        info += $"\n  {worker.name}";
                        info += $"\n  Hunger: {worker.Hunger:F0}/100";
                        info += $"\n  State: {worker.StateName}";
                    }
                }

                var pos = building.transform.position;
                info += $"\n\nPosition: ({pos.x:F0}, {pos.z:F0})";

                _infoText.text = info;
            }
            else
            {
                _titleText.text = displayName;
                _infoText.text = statusLine;
            }
        }

        // ─── Natural Shelter Info (Feature 5.4) ──────────────────

        /// <summary>
        /// Show natural shelter info when tapped.
        /// </summary>
        private void RefreshShelterInfo(NaturalShelter shelter)
        {
            _hungerBarRoot.SetActive(false);
            _needsRoot.SetActive(false);
            _toolRoot.SetActive(false);
            _traitText.gameObject.SetActive(false);

            _titleText.text = shelter.DisplayName;

            string info = shelter.IsDiscovered ? "Discovered" : "Undiscovered";
            info += $"\nProtection: {shelter.ProtectionValue * 100f:F0}%";
            info += $"\nCapacity: {shelter.CurrentOccupants}/{shelter.Capacity}";
            info += shelter.HasRoom ? "\nRoom available" : "\nFULL";

            if (_isDetailView)
            {
                info += $"\n\nType: {shelter.ShelterType}";
                var pos = shelter.transform.position;
                info += $"\nPosition: ({pos.x:F0}, {pos.z:F0})";
            }

            _infoText.text = info;
        }

        // ─── Panel Visibility ───────────────────────────────────

        private void ShowPanel()
        {
            _isVisible = true;
            if (_panelRoot != null)
                _panelRoot.SetActive(true);
        }

        private void HidePanel()
        {
            _isVisible = false;
            _selectedObject = null;
            if (_panelRoot != null)
                _panelRoot.SetActive(false);
        }

        // ─── UI Construction ────────────────────────────────────

        /// <summary>
        /// Build the info panel UI. Anchored to bottom-left of screen.
        /// </summary>
        private void CreatePanel()
        {
            // Panel background
            _panelRoot = new GameObject("InfoPanel");
            _panelRoot.transform.SetParent(transform, false);

            var panelRect = _panelRoot.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0, 0);
            panelRect.anchorMax = new Vector2(0, 0);
            panelRect.pivot = new Vector2(0, 0);
            panelRect.anchoredPosition = new Vector2(16, 16);
            panelRect.sizeDelta = new Vector2(PANEL_WIDTH, 160);

            _panelImage = _panelRoot.AddComponent<Image>();
            _panelImage.color = PANEL_COLOR_BASIC;

            // Vertical layout
            var layout = _panelRoot.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(
                (int)PANEL_PADDING, (int)PANEL_PADDING,
                (int)PANEL_PADDING, (int)PANEL_PADDING);
            layout.spacing = 6f;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.childControlWidth = true;
            layout.childControlHeight = true;

            var fitter = _panelRoot.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // Title
            _titleText = CreateLabel("Title", FONT_SIZE_TITLE, Color.white);

            // Trait label (below title, above needs bars – Feature 6.1)
            _traitText = CreateLabel("Trait", FONT_SIZE_SMALL, new Color(0.9f, 0.8f, 0.5f));

            // Legacy hunger bar (kept for backward compatibility, used in building worker info)
            CreateHungerBar();

            // Needs panel (Feature 4.5)
            CreateNeedsPanel();

            // Tool info panel (Feature 3.4)
            CreateToolPanel();

            // Info text
            _infoText = CreateLabel("Info", FONT_SIZE_SMALL, new Color(0.85f, 0.85f, 0.85f));
        }

        /// <summary>
        /// Create the legacy hunger bar (hidden for settlers, used by building worker display).
        /// </summary>
        private void CreateHungerBar()
        {
            _hungerBarRoot = new GameObject("HungerBar");
            _hungerBarRoot.transform.SetParent(_panelRoot.transform, false);

            var barLayout = _hungerBarRoot.AddComponent<LayoutElement>();
            barLayout.preferredHeight = 14f;

            _hungerBarRoot.AddComponent<RectTransform>();

            var bgImage = _hungerBarRoot.AddComponent<Image>();
            bgImage.color = new Color(0.2f, 0.2f, 0.2f, 1f);

            var fillObj = new GameObject("Fill");
            fillObj.transform.SetParent(_hungerBarRoot.transform, false);

            _hungerBarFill = fillObj.AddComponent<RectTransform>();
            _hungerBarFill.anchorMin = Vector2.zero;
            _hungerBarFill.anchorMax = Vector2.one;
            _hungerBarFill.sizeDelta = Vector2.zero;
            _hungerBarFill.offsetMin = Vector2.zero;
            _hungerBarFill.offsetMax = Vector2.zero;

            _hungerBarFillImage = fillObj.AddComponent<Image>();
            _hungerBarFillImage.color = new Color(0.3f, 0.8f, 0.3f);

            var labelObj = new GameObject("Label");
            labelObj.transform.SetParent(_hungerBarRoot.transform, false);

            var labelRect = labelObj.AddComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.sizeDelta = Vector2.zero;
            labelRect.offsetMin = new Vector2(4, 0);
            labelRect.offsetMax = Vector2.zero;

            var label = labelObj.AddComponent<Text>();
            label.font = UnityEngine.Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = 11;
            label.color = Color.white;
            label.alignment = TextAnchor.MiddleLeft;
            label.text = "Hunger";

            var labelShadow = labelObj.AddComponent<Shadow>();
            labelShadow.effectColor = new Color(0, 0, 0, 0.8f);
            labelShadow.effectDistance = new Vector2(1, -1);
        }

        /// <summary>
        /// Feature 4.5: Create the full needs panel with thirst bar (blue),
        /// hunger bar (orange), shelter status, and health status.
        /// </summary>
        private void CreateNeedsPanel()
        {
            _needsRoot = new GameObject("NeedsPanel");
            _needsRoot.transform.SetParent(_panelRoot.transform, false);

            _needsRoot.AddComponent<RectTransform>();

            var needsLayout = _needsRoot.AddComponent<VerticalLayoutGroup>();
            needsLayout.spacing = 4f;
            needsLayout.childForceExpandWidth = true;
            needsLayout.childForceExpandHeight = false;
            needsLayout.childControlWidth = true;
            needsLayout.childControlHeight = true;

            var needsFitter = _needsRoot.AddComponent<LayoutElement>();
            needsFitter.flexibleWidth = 1f;

            // ─── Thirst Bar (Blue) ─────────────────────────────
            var thirstBarRoot = new GameObject("ThirstBar");
            thirstBarRoot.transform.SetParent(_needsRoot.transform, false);
            var thirstBarLayout = thirstBarRoot.AddComponent<LayoutElement>();
            thirstBarLayout.preferredHeight = 14f;
            thirstBarRoot.AddComponent<RectTransform>();
            var thirstBg = thirstBarRoot.AddComponent<Image>();
            thirstBg.color = new Color(0.2f, 0.2f, 0.2f, 1f);

            var thirstFillObj = new GameObject("Fill");
            thirstFillObj.transform.SetParent(thirstBarRoot.transform, false);
            _thirstBarFill = thirstFillObj.AddComponent<RectTransform>();
            _thirstBarFill.anchorMin = Vector2.zero;
            _thirstBarFill.anchorMax = Vector2.one;
            _thirstBarFill.sizeDelta = Vector2.zero;
            _thirstBarFill.offsetMin = Vector2.zero;
            _thirstBarFill.offsetMax = Vector2.zero;
            _thirstBarFillImage = thirstFillObj.AddComponent<Image>();
            _thirstBarFillImage.color = THIRST_COLOR;

            _thirstLabel = CreateBarLabel(thirstBarRoot.transform, "Thirst: Hydrated");

            // ─── Hunger Bar (Orange) ───────────────────────────
            var hungerBarRoot = new GameObject("HungerNeedsBar");
            hungerBarRoot.transform.SetParent(_needsRoot.transform, false);
            var hungerBarLayout = hungerBarRoot.AddComponent<LayoutElement>();
            hungerBarLayout.preferredHeight = 14f;
            hungerBarRoot.AddComponent<RectTransform>();
            var hungerBg = hungerBarRoot.AddComponent<Image>();
            hungerBg.color = new Color(0.2f, 0.2f, 0.2f, 1f);

            var hungerFillObj = new GameObject("Fill");
            hungerFillObj.transform.SetParent(hungerBarRoot.transform, false);
            _hungerNeedsBarFill = hungerFillObj.AddComponent<RectTransform>();
            _hungerNeedsBarFill.anchorMin = Vector2.zero;
            _hungerNeedsBarFill.anchorMax = Vector2.one;
            _hungerNeedsBarFill.sizeDelta = Vector2.zero;
            _hungerNeedsBarFill.offsetMin = Vector2.zero;
            _hungerNeedsBarFill.offsetMax = Vector2.zero;
            _hungerNeedsBarFillImage = hungerFillObj.AddComponent<Image>();
            _hungerNeedsBarFillImage.color = HUNGER_COLOR;

            _hungerNeedsLabel = CreateBarLabel(hungerBarRoot.transform, "Hunger: 0/100");

            // ─── Shelter Status ────────────────────────────────
            _shelterStatusText = CreateLabel("ShelterStatus", FONT_SIZE_TINY, new Color(0.7f, 0.7f, 0.7f));
            _shelterStatusText.transform.SetParent(_needsRoot.transform, false);
            _shelterStatusText.text = "Shelter: Unknown";

            // ─── Health Status ─────────────────────────────────
            _healthStatusText = CreateLabel("HealthStatus", FONT_SIZE_TINY, new Color(0.7f, 0.7f, 0.7f));
            _healthStatusText.transform.SetParent(_needsRoot.transform, false);
            _healthStatusText.text = "Health: Healthy";
        }

        /// <summary>
        /// Feature 3.4: Create the tool info panel showing tool name,
        /// quality badge, durability bar, and capabilities.
        /// </summary>
        private void CreateToolPanel()
        {
            _toolRoot = new GameObject("ToolPanel");
            _toolRoot.transform.SetParent(_panelRoot.transform, false);

            _toolRoot.AddComponent<RectTransform>();

            var toolLayout = _toolRoot.AddComponent<VerticalLayoutGroup>();
            toolLayout.spacing = 3f;
            toolLayout.childForceExpandWidth = true;
            toolLayout.childForceExpandHeight = false;
            toolLayout.childControlWidth = true;
            toolLayout.childControlHeight = true;

            var toolFitter = _toolRoot.AddComponent<LayoutElement>();
            toolFitter.flexibleWidth = 1f;

            // Separator label
            var sepLabel = CreateLabel("ToolSep", FONT_SIZE_TINY, new Color(0.5f, 0.5f, 0.5f));
            sepLabel.transform.SetParent(_toolRoot.transform, false);
            sepLabel.text = "--- Tool ---";
            sepLabel.alignment = TextAnchor.MiddleCenter;

            // Tool name + quality on same line (we use two separate texts)
            var toolHeaderObj = new GameObject("ToolHeader");
            toolHeaderObj.transform.SetParent(_toolRoot.transform, false);
            toolHeaderObj.AddComponent<RectTransform>();
            var toolHeaderLayout = toolHeaderObj.AddComponent<LayoutElement>();
            toolHeaderLayout.preferredHeight = 20f;

            // Tool name (left-aligned)
            var nameObj = new GameObject("ToolName");
            nameObj.transform.SetParent(toolHeaderObj.transform, false);
            var nameRect = nameObj.AddComponent<RectTransform>();
            nameRect.anchorMin = Vector2.zero;
            nameRect.anchorMax = new Vector2(0.7f, 1f);
            nameRect.sizeDelta = Vector2.zero;
            nameRect.offsetMin = Vector2.zero;
            nameRect.offsetMax = Vector2.zero;
            _toolNameText = nameObj.AddComponent<Text>();
            _toolNameText.font = UnityEngine.Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _toolNameText.fontSize = FONT_SIZE_SMALL;
            _toolNameText.color = Color.white;
            _toolNameText.alignment = TextAnchor.MiddleLeft;

            // Quality badge (right-aligned)
            var qualityObj = new GameObject("ToolQuality");
            qualityObj.transform.SetParent(toolHeaderObj.transform, false);
            var qualityRect = qualityObj.AddComponent<RectTransform>();
            qualityRect.anchorMin = new Vector2(0.7f, 0f);
            qualityRect.anchorMax = Vector2.one;
            qualityRect.sizeDelta = Vector2.zero;
            qualityRect.offsetMin = Vector2.zero;
            qualityRect.offsetMax = Vector2.zero;
            _toolQualityText = qualityObj.AddComponent<Text>();
            _toolQualityText.font = UnityEngine.Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _toolQualityText.fontSize = FONT_SIZE_SMALL;
            _toolQualityText.color = Color.white;
            _toolQualityText.alignment = TextAnchor.MiddleRight;
            _toolQualityText.fontStyle = FontStyle.Bold;

            // Durability bar
            var durBarRoot = new GameObject("DurabilityBar");
            durBarRoot.transform.SetParent(_toolRoot.transform, false);
            var durBarLayout = durBarRoot.AddComponent<LayoutElement>();
            durBarLayout.preferredHeight = 12f;
            durBarRoot.AddComponent<RectTransform>();
            var durBg = durBarRoot.AddComponent<Image>();
            durBg.color = new Color(0.2f, 0.2f, 0.2f, 1f);

            var durFillObj = new GameObject("Fill");
            durFillObj.transform.SetParent(durBarRoot.transform, false);
            _durabilityBarFill = durFillObj.AddComponent<RectTransform>();
            _durabilityBarFill.anchorMin = Vector2.zero;
            _durabilityBarFill.anchorMax = Vector2.one;
            _durabilityBarFill.sizeDelta = Vector2.zero;
            _durabilityBarFill.offsetMin = Vector2.zero;
            _durabilityBarFill.offsetMax = Vector2.zero;
            _durabilityBarFillImage = durFillObj.AddComponent<Image>();
            _durabilityBarFillImage.color = DURABILITY_GREEN;

            _durabilityLabel = CreateBarLabel(durBarRoot.transform, "Durability");

            // Capabilities text
            _toolCapabilitiesText = CreateLabel("ToolCapabilities", FONT_SIZE_TINY, new Color(0.7f, 0.7f, 0.7f));
            _toolCapabilitiesText.transform.SetParent(_toolRoot.transform, false);
            _toolCapabilitiesText.text = "";
        }

        /// <summary>
        /// Helper to create a label overlaying a bar (for thirst/hunger/durability labels).
        /// </summary>
        private Text CreateBarLabel(Transform parent, string defaultText)
        {
            var labelObj = new GameObject("Label");
            labelObj.transform.SetParent(parent, false);

            var labelRect = labelObj.AddComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.sizeDelta = Vector2.zero;
            labelRect.offsetMin = new Vector2(4, 0);
            labelRect.offsetMax = Vector2.zero;

            var label = labelObj.AddComponent<Text>();
            label.font = UnityEngine.Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = 11;
            label.color = Color.white;
            label.alignment = TextAnchor.MiddleLeft;
            label.text = defaultText;

            var labelShadow = labelObj.AddComponent<Shadow>();
            labelShadow.effectColor = new Color(0, 0, 0, 0.8f);
            labelShadow.effectDistance = new Vector2(1, -1);

            return label;
        }

        /// <summary>
        /// Helper to create a Text element as a child of the panel.
        /// </summary>
        private Text CreateLabel(string name, int fontSize, Color color)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(_panelRoot.transform, false);

            obj.AddComponent<RectTransform>();

            var text = obj.AddComponent<Text>();
            text.font = UnityEngine.Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = TextAnchor.UpperLeft;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;

            var shadow = obj.AddComponent<Shadow>();
            shadow.effectColor = new Color(0, 0, 0, 0.6f);
            shadow.effectDistance = new Vector2(1, -1);

            return text;
        }
    }
}
