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
    /// Story 6.1: Tap on settler → name, hunger bar, current task.
    ///            Tap on building → type, status, assigned worker.
    /// Story 6.2: Deselect closes panel.
    /// Story 6.3: Long press shows extended info (all stats).
    ///
    /// The panel is anchored bottom-left and updates every frame while visible.
    /// </summary>
    public class InfoPanel : MonoBehaviour
    {
        // ─── Settings ──────────────────────────────────────────

        private const float PANEL_WIDTH = 280f;
        private const float PANEL_PADDING = 12f;
        private const int FONT_SIZE = 18;
        private const int FONT_SIZE_SMALL = 15;
        private const int FONT_SIZE_TITLE = 22;

        // ─── State ─────────────────────────────────────────────

        private GameObject _selectedObject;
        private bool _isDetailView;
        private bool _isVisible;

        // ─── UI References ─────────────────────────────────────

        private static readonly Color PANEL_COLOR_BASIC = new Color(0.1f, 0.1f, 0.1f, 0.85f);
        private static readonly Color PANEL_COLOR_DETAIL = new Color(0.08f, 0.12f, 0.2f, 0.92f);

        private GameObject _panelRoot;
        private Image _panelImage;
        private Text _titleText;
        private Text _infoText;
        private RectTransform _hungerBarFill;
        private Image _hungerBarFillImage;
        private GameObject _hungerBarRoot;

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
                // Visual distinction: detail view has a different panel color
                _panelImage.color = _isDetailView ? PANEL_COLOR_DETAIL : PANEL_COLOR_BASIC;
                ShowPanel();
                RefreshContent();
            }
        }

        // ─── Content Rendering ──────────────────────────────────

        /// <summary>
        /// Update panel content based on what's selected.
        /// Called every frame while panel is visible for live updates.
        /// </summary>
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
        }

        /// <summary>
        /// Show settler info: name, hunger, current task, state.
        /// Story 6.1: Basic info on tap. Story 6.3: Extended info on long press.
        /// </summary>
        private void RefreshSettlerInfo(Settler settler)
        {
            // Hunger bar
            _hungerBarRoot.SetActive(true);
            float hungerPct = settler.HungerPercent; // 0 = satt, 1 = starving
            _hungerBarFill.anchorMax = new Vector2(1f - hungerPct, 1f);

            // Color: green when full, yellow when hungry, red when starving
            if (hungerPct > 0.75f)
                _hungerBarFillImage.color = new Color(0.9f, 0.2f, 0.2f);
            else if (hungerPct > 0.5f)
                _hungerBarFillImage.color = new Color(0.9f, 0.7f, 0.2f);
            else
                _hungerBarFillImage.color = new Color(0.3f, 0.8f, 0.3f);

            // Info text
            string task = settler.HasTask
                ? settler.CurrentTask?.TaskType.ToString() ?? "Eating"
                : "Idle";
            string state = settler.StateName;

            if (_isDetailView)
            {
                // Detail view: comprehensive stats
                _titleText.text = $"-- {settler.name} --";

                string info = $"State: {state}";
                info += $"\nTask: {task}";
                info += $"\n\nHunger: {settler.Hunger:F0} / 100";

                if (settler.IsStarving)
                    info += "\nSTARVING!";

                // Show task target if available
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

                // Position
                var pos = settler.transform.position;
                info += $"\n\nPosition: ({pos.x:F0}, {pos.z:F0})";

                _infoText.text = info;
            }
            else
            {
                // Basic view: quick overview
                _titleText.text = settler.name;
                _infoText.text = $"Task: {task}\nState: {state}";
            }
        }

        /// <summary>
        /// Show building info: type, construction status, worker.
        /// Story 6.1: Basic info on tap. Story 6.3: Extended info on long press.
        /// </summary>
        private void RefreshBuildingInfo(Building building)
        {
            string displayName = building.Definition != null
                ? building.Definition.DisplayName : building.name;

            // No hunger bar for buildings
            _hungerBarRoot.SetActive(false);

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
                // Detail view: all stats
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

                // Position
                var pos = building.transform.position;
                info += $"\n\nPosition: ({pos.x:F0}, {pos.z:F0})";

                _infoText.text = info;
            }
            else
            {
                // Basic view: quick overview
                _titleText.text = displayName;
                _infoText.text = statusLine;
            }
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

            // Hunger bar
            CreateHungerBar();

            // Info text
            _infoText = CreateLabel("Info", FONT_SIZE_SMALL, new Color(0.85f, 0.85f, 0.85f));
        }

        /// <summary>
        /// Create a hunger bar: background (dark) + fill (colored).
        /// Fill shrinks from right as hunger increases (0=full bar, 100=empty).
        /// </summary>
        private void CreateHungerBar()
        {
            _hungerBarRoot = new GameObject("HungerBar");
            _hungerBarRoot.transform.SetParent(_panelRoot.transform, false);

            var barLayout = _hungerBarRoot.AddComponent<LayoutElement>();
            barLayout.preferredHeight = 14f;

            var barRect = _hungerBarRoot.AddComponent<RectTransform>();

            // Background (dark gray)
            var bgImage = _hungerBarRoot.AddComponent<Image>();
            bgImage.color = new Color(0.2f, 0.2f, 0.2f, 1f);

            // Fill (stretches from left)
            var fillObj = new GameObject("Fill");
            fillObj.transform.SetParent(_hungerBarRoot.transform, false);

            _hungerBarFill = fillObj.AddComponent<RectTransform>();
            _hungerBarFill.anchorMin = Vector2.zero;
            _hungerBarFill.anchorMax = Vector2.one; // Full width when hunger = 0
            _hungerBarFill.sizeDelta = Vector2.zero;
            _hungerBarFill.offsetMin = Vector2.zero;
            _hungerBarFill.offsetMax = Vector2.zero;

            _hungerBarFillImage = fillObj.AddComponent<Image>();
            _hungerBarFillImage.color = new Color(0.3f, 0.8f, 0.3f);

            // Label
            var labelObj = new GameObject("Label");
            labelObj.transform.SetParent(_hungerBarRoot.transform, false);

            var labelRect = labelObj.AddComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.sizeDelta = Vector2.zero;
            labelRect.offsetMin = new Vector2(4, 0);
            labelRect.offsetMax = Vector2.zero;

            var label = labelObj.AddComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = 11;
            label.color = Color.white;
            label.alignment = TextAnchor.MiddleLeft;
            label.text = "Hunger";

            var labelShadow = labelObj.AddComponent<Shadow>();
            labelShadow.effectColor = new Color(0, 0, 0, 0.8f);
            labelShadow.effectDistance = new Vector2(1, -1);
        }

        private Text CreateLabel(string name, int fontSize, Color color)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(_panelRoot.transform, false);

            obj.AddComponent<RectTransform>();

            var text = obj.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
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
