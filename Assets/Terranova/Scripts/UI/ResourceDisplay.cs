using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using Terranova.Core;
using Terranova.Population;
using Terranova.Terrain;

namespace Terranova.UI
{
    /// <summary>
    /// HUD showing day counter, categorized resource panel, speed controls,
    /// event/tool-break/needs notifications, game over panel, and version label.
    ///
    /// MS4 Changes:
    ///   Feature 1.5 - "Day X" counter via DayNightCycle.Instance.DayCount.
    ///   Feature 2.4 - Categorized resource panel with expand/collapse per category.
    ///   Feature 3.4 - Tool break notifications.
    ///   Feature 4.5 - Warning notifications for critical thirst/hunger.
    ///
    /// Scene setup:
    ///   1. Create Canvas (Screen Space - Overlay)
    ///   2. Add this component to the Canvas
    ///   3. It auto-creates UI elements on Start
    /// </summary>
    public class ResourceDisplay : MonoBehaviour
    {
        [Header("UI Settings")]
        [Tooltip("Font size for resource text.")]
        [SerializeField] private int _fontSize = 24;

        [Tooltip("Minimum touch target size in points (Apple HIG: 44pt).")]
        [SerializeField] private float _minTouchTarget = 44f;

        // ─── Speed Widget ─────────────────────────────────────────
        private static readonly float[] SPEED_VALUES = { 0f, 1f, 3f, 5f };
        private static readonly string[] SPEED_LABELS = { "\u275A\u275A", "1x", "3x", "5x" };
        private int _currentSpeedIndex = 1;

        // ─── Game State ───────────────────────────────────────────
        private int _settlers;
        private bool _foodWarning;
        private bool _gameStarted;

        // ─── Category Colors ──────────────────────────────────────
        private static readonly Color COLOR_WOOD  = new Color(0.55f, 0.33f, 0.14f);
        private static readonly Color COLOR_STONE = new Color(0.60f, 0.60f, 0.60f);
        private static readonly Color COLOR_PLANT = new Color(0.30f, 0.75f, 0.30f);
        private static readonly Color COLOR_ANIMAL = new Color(0.75f, 0.40f, 0.30f);
        private static readonly Color COLOR_OTHER = new Color(0.50f, 0.50f, 0.70f);

        // ─── Category Expand/Collapse State ───────────────────────
        private bool _woodExpanded;
        private bool _stoneExpanded;
        private bool _plantExpanded;
        private bool _animalExpanded;
        private bool _otherExpanded;

        // Track which categories have discoveries unlocked (show detail after)
        private readonly HashSet<string> _discoveredCategories = new();

        // ─── UI References ────────────────────────────────────────
        private Text _resourceText;
        private Text _eventText;
        private Text _dayCounterText;
        private Text _warningText;
        private Button[] _speedButtons;
        private Text[] _speedButtonTexts;
        private float _eventDisplayTimer;
        private GameObject _gameOverPanel;

        // Category buttons for expand/collapse
        private Button _woodButton;
        private Button _stoneButton;
        private Button _plantButton;
        private Button _animalButton;
        private Button _otherButton;

        // ─── Lifecycle ────────────────────────────────────────────

        private void Start()
        {
            CreateUI();
            UpdateDisplay();

            // Legacy events
            EventBus.Subscribe<BuildingPlacedEvent>(OnBuildingPlaced);
            EventBus.Subscribe<BuildingCompletedEvent>(OnBuildingCompleted);
            EventBus.Subscribe<PopulationChangedEvent>(OnPopulationChanged);
            EventBus.Subscribe<ResourceChangedEvent>(OnResourceChanged);
            EventBus.Subscribe<SettlerDiedEvent>(OnSettlerDied);
            EventBus.Subscribe<FoodWarningEvent>(OnFoodWarning);
            EventBus.Subscribe<DiscoveryMadeEvent>(OnDiscoveryMade);

            // MS4 events
            EventBus.Subscribe<DayChangedEvent>(OnDayChanged);
            EventBus.Subscribe<ToolBrokeEvent>(OnToolBroke);
            EventBus.Subscribe<NeedsCriticalEvent>(OnNeedsCritical);
            EventBus.Subscribe<SettlerPoisonedEvent>(OnSettlerPoisoned);
        }

        private void Update()
        {
            // Fade out event notification after timer expires
            if (_eventDisplayTimer > 0)
            {
                _eventDisplayTimer -= Time.deltaTime;
                if (_eventDisplayTimer <= 0 && _eventText != null)
                {
                    _eventText.text = "";
                    _eventText.color = Color.yellow;
                }
            }

            // Check food supply for warning
            CheckFoodWarning();

            // Update day counter display
            UpdateDayCounter();
        }

        // ─── Food Warning ─────────────────────────────────────────

        private void CheckFoodWarning()
        {
            var rm = ResourceManager.Instance;
            if (rm == null) return;

            bool shouldWarn = rm.Food < 5 || (_settlers > 0 && rm.Food < _settlers);
            if (shouldWarn != _foodWarning)
            {
                _foodWarning = shouldWarn;
                EventBus.Publish(new FoodWarningEvent { IsWarning = shouldWarn });
            }
        }

        // ─── Event Handlers ───────────────────────────────────────

        private void OnPopulationChanged(PopulationChangedEvent evt)
        {
            _settlers = evt.CurrentPopulation;
            UpdateDisplay();

            if (_settlers > 0)
                _gameStarted = true;

            // Feature 6.4: TribeManager handles restart, show perish message instead of permanent game over
            if (_gameStarted && _settlers <= 0)
            {
                var tribeMgr = Population.TribeManager.Instance;
                if (tribeMgr != null)
                    ShowTribePerished();
                else
                    ShowGameOver();
            }

            // Dismiss perish panel when new tribe arrives
            if (_settlers > 0 && _gameOverPanel != null)
            {
                Destroy(_gameOverPanel);
                _gameOverPanel = null;
                Time.timeScale = 1f;
            }
        }

        private void OnResourceChanged(ResourceChangedEvent evt)
        {
            UpdateDisplay();
        }

        private void OnBuildingPlaced(BuildingPlacedEvent evt)
        {
            UpdateDisplay();
            ShowEvent($"Building {evt.BuildingName}...", Color.yellow, 3f);
        }

        private void OnBuildingCompleted(BuildingCompletedEvent evt)
        {
            ShowEvent($"{evt.BuildingName} complete!", Color.yellow, 3f);
        }

        private void OnSettlerDied(SettlerDiedEvent evt)
        {
            ShowEvent($"{evt.SettlerName} died ({evt.CauseOfDeath})", new Color(0.9f, 0.3f, 0.3f), 4f);
        }

        private void OnFoodWarning(FoodWarningEvent evt)
        {
            _foodWarning = evt.IsWarning;
            if (_warningText != null)
                _warningText.text = _foodWarning ? "Food is running low!" : "";
        }

        private void OnDiscoveryMade(DiscoveryMadeEvent evt)
        {
            // Track that categories may now show detail
            _discoveredCategories.Add(evt.DiscoveryName);
            ShowEvent($"Discovery: {evt.DiscoveryName}!", new Color(0.4f, 1f, 0.6f), 5f);
            UpdateDisplay();
        }

        /// <summary>Feature 1.5: Day counter updated via event.</summary>
        private void OnDayChanged(DayChangedEvent evt)
        {
            UpdateDayCounter();
        }

        /// <summary>Feature 3.4: Tool break notification.</summary>
        private void OnToolBroke(ToolBrokeEvent evt)
        {
            ShowEvent($"{evt.SettlerName}'s {evt.ToolName} broke!", new Color(1f, 0.6f, 0.2f), 4f);
        }

        /// <summary>Feature 4.5: Critical needs warning.</summary>
        private void OnNeedsCritical(NeedsCriticalEvent evt)
        {
            Color warningColor = evt.NeedType == "Thirst"
                ? new Color(0.3f, 0.6f, 1f)   // Blue for thirst
                : new Color(1f, 0.5f, 0.2f);   // Orange for hunger
            ShowEvent($"{evt.SettlerName}: {evt.NeedType} critical!", warningColor, 3f);
        }

        /// <summary>Feature 4.3: Settler poisoned notification.</summary>
        private void OnSettlerPoisoned(SettlerPoisonedEvent evt)
        {
            ShowEvent($"{evt.SettlerName} poisoned by {evt.FoodName}!", new Color(0.6f, 0.2f, 0.8f), 4f);
        }

        // ─── Display Helpers ──────────────────────────────────────

        private void ShowEvent(string message, Color color, float duration)
        {
            if (_eventText == null) return;
            _eventText.text = message;
            _eventText.color = color;
            _eventDisplayTimer = duration;
        }

        /// <summary>
        /// Feature 1.5: Update "Day X" counter from DayNightCycle.
        /// </summary>
        private void UpdateDayCounter()
        {
            if (_dayCounterText == null) return;
            var dnc = DayNightCycle.Instance;
            int day = dnc != null ? dnc.DayCount : GameState.DayCount;
            _dayCounterText.text = $"Day {day}";
        }

        /// <summary>
        /// Feature 2.4: Categorized resource panel.
        /// At start: simple "Wood: 12 | Stone: 8 | Food: 5"
        /// After discoveries: categories expand to show individual materials.
        /// </summary>
        private void UpdateDisplay()
        {
            if (_resourceText == null) return;

            var inv = MaterialInventory.Instance;
            if (inv == null)
            {
                // Fallback to legacy ResourceManager
                var rm = ResourceManager.Instance;
                if (rm != null)
                    _resourceText.text = $"Wood: {rm.Wood} | Stone: {rm.Stone} | Food: {rm.Food} | Settlers: {_settlers}";
                else
                    _resourceText.text = $"Settlers: {_settlers}";
                return;
            }

            int woodTotal  = inv.GetCategoryTotal(MaterialCategory.Wood);
            int stoneTotal = inv.GetCategoryTotal(MaterialCategory.Stone);
            int plantTotal = inv.GetCategoryTotal(MaterialCategory.Plant);
            int animalTotal = inv.GetCategoryTotal(MaterialCategory.Animal);
            int otherTotal = inv.GetCategoryTotal(MaterialCategory.Other);
            int foodTotal = plantTotal + animalTotal;

            // Build display string
            System.Text.StringBuilder sb = new();

            // Wood category
            sb.Append(BuildCategoryLine("Wood", woodTotal, MaterialCategory.Wood, _woodExpanded, inv));
            sb.Append(" | ");

            // Stone category
            sb.Append(BuildCategoryLine("Stone", stoneTotal, MaterialCategory.Stone, _stoneExpanded, inv));
            sb.Append(" | ");

            // Food (Plant + Animal combined for summary)
            sb.Append($"Food: {foodTotal}");

            // If Plant expanded, show plant detail
            if (_plantExpanded && HasDiscoveredMaterials(MaterialCategory.Plant, inv))
            {
                sb.Append(BuildCategoryDetail(MaterialCategory.Plant, inv));
            }

            // If Animal expanded, show animal detail
            if (_animalExpanded && HasDiscoveredMaterials(MaterialCategory.Animal, inv))
            {
                sb.Append(BuildCategoryDetail(MaterialCategory.Animal, inv));
            }

            // Other (only show if > 0)
            if (otherTotal > 0)
            {
                sb.Append(" | ");
                sb.Append(BuildCategoryLine("Other", otherTotal, MaterialCategory.Other, _otherExpanded, inv));
            }

            sb.Append($" | Settlers: {_settlers}");

            _resourceText.text = sb.ToString();
        }

        /// <summary>
        /// Build category header line (e.g., "Wood: 12").
        /// If expanded and has discovered materials, append detail underneath.
        /// </summary>
        private string BuildCategoryLine(string label, int total, MaterialCategory category,
            bool expanded, MaterialInventory inv)
        {
            string line = $"{label}: {total}";

            if (expanded && HasDiscoveredMaterials(category, inv))
            {
                line += BuildCategoryDetail(category, inv);
            }

            return line;
        }

        /// <summary>
        /// Build detail breakdown for a category showing individual materials.
        /// Uses colored square icon per material type: [#] Name: count
        /// </summary>
        private string BuildCategoryDetail(MaterialCategory category, MaterialInventory inv)
        {
            var breakdown = inv.GetCategoryBreakdown(category);
            if (breakdown.Count == 0) return "";

            System.Text.StringBuilder detail = new();
            foreach (var kvp in breakdown)
            {
                string displayName = inv.GetDisplayName(kvp.Key);
                detail.Append($"\n  \u25A0 {displayName}: {kvp.Value}");
            }

            return detail.ToString();
        }

        /// <summary>
        /// Check if a category has any materials whose discovery is unlocked
        /// (meaning we can show individual detail).
        /// </summary>
        private bool HasDiscoveredMaterials(MaterialCategory category, MaterialInventory inv)
        {
            var materials = MaterialDatabase.GetByCategory(category);
            foreach (var mat in materials)
            {
                if (!string.IsNullOrEmpty(mat.DiscoveryRequired) && inv.IsMaterialDiscovered(mat.Id))
                    return true;
            }
            return false;
        }

        // ─── Category Toggle ──────────────────────────────────────

        private void ToggleWood() { _woodExpanded = !_woodExpanded; UpdateDisplay(); }
        private void ToggleStone() { _stoneExpanded = !_stoneExpanded; UpdateDisplay(); }
        private void TogglePlant() { _plantExpanded = !_plantExpanded; UpdateDisplay(); }
        private void ToggleAnimal() { _animalExpanded = !_animalExpanded; UpdateDisplay(); }
        private void ToggleOther() { _otherExpanded = !_otherExpanded; UpdateDisplay(); }

        // ─── Tribe Perished (Feature 6.4) ────────────────────────

        private void ShowTribePerished()
        {
            if (_gameOverPanel != null) return;

            _gameOverPanel = new GameObject("TribePerishedPanel");
            _gameOverPanel.transform.SetParent(transform, false);
            var panelImage = _gameOverPanel.AddComponent<Image>();
            panelImage.color = new Color(0f, 0f, 0f, 0.8f);
            var panelRect = _gameOverPanel.GetComponent<RectTransform>();
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;

            var titleObj = new GameObject("PerishedTitle");
            titleObj.transform.SetParent(_gameOverPanel.transform, false);
            var titleRect = titleObj.AddComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0.5f, 0.55f);
            titleRect.anchorMax = new Vector2(0.5f, 0.55f);
            titleRect.pivot = new Vector2(0.5f, 0.5f);
            titleRect.sizeDelta = new Vector2(600, 60);
            var titleText = titleObj.AddComponent<Text>();
            titleText.font = UnityEngine.Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            titleText.fontSize = 36;
            titleText.color = new Color(0.9f, 0.7f, 0.5f);
            titleText.alignment = TextAnchor.MiddleCenter;
            titleText.text = "The tribe has perished...";

            var subObj = new GameObject("PerishedSub");
            subObj.transform.SetParent(_gameOverPanel.transform, false);
            var subRect = subObj.AddComponent<RectTransform>();
            subRect.anchorMin = new Vector2(0.5f, 0.45f);
            subRect.anchorMax = new Vector2(0.5f, 0.45f);
            subRect.pivot = new Vector2(0.5f, 0.5f);
            subRect.sizeDelta = new Vector2(600, 40);
            var subText = subObj.AddComponent<Text>();
            subText.font = UnityEngine.Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            subText.fontSize = 20;
            subText.color = new Color(0.7f, 0.7f, 0.7f);
            subText.alignment = TextAnchor.MiddleCenter;
            subText.text = "A new tribe finds the remains of an abandoned settlement...";
        }

        // ─── Game Over ────────────────────────────────────────────

        private void ShowGameOver()
        {
            if (_gameOverPanel != null) return;

            Time.timeScale = 0f;

            _gameOverPanel = new GameObject("GameOverPanel");
            _gameOverPanel.transform.SetParent(transform, false);
            var panelImage = _gameOverPanel.AddComponent<Image>();
            panelImage.color = new Color(0f, 0f, 0f, 0.75f);
            var panelRect = _gameOverPanel.GetComponent<RectTransform>();
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;

            // "Game Over" title
            var titleObj = new GameObject("GameOverTitle");
            titleObj.transform.SetParent(_gameOverPanel.transform, false);
            var titleRect = titleObj.AddComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0.5f, 0.5f);
            titleRect.anchorMax = new Vector2(0.5f, 0.5f);
            titleRect.pivot = new Vector2(0.5f, 0.5f);
            titleRect.anchoredPosition = new Vector2(0, 60);
            titleRect.sizeDelta = new Vector2(500, 80);
            var titleText = titleObj.AddComponent<Text>();
            titleText.font = UnityEngine.Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            titleText.fontSize = 48;
            titleText.color = new Color(0.9f, 0.3f, 0.3f);
            titleText.alignment = TextAnchor.MiddleCenter;
            titleText.fontStyle = FontStyle.Bold;
            titleText.text = "GAME OVER";

            // Subtitle with day count
            var dnc = DayNightCycle.Instance;
            int dayCount = dnc != null ? dnc.DayCount : GameState.DayCount;
            var subtitleObj = new GameObject("GameOverSubtitle");
            subtitleObj.transform.SetParent(_gameOverPanel.transform, false);
            var subRect = subtitleObj.AddComponent<RectTransform>();
            subRect.anchorMin = new Vector2(0.5f, 0.5f);
            subRect.anchorMax = new Vector2(0.5f, 0.5f);
            subRect.pivot = new Vector2(0.5f, 0.5f);
            subRect.anchoredPosition = new Vector2(0, 10);
            subRect.sizeDelta = new Vector2(500, 40);
            var subText = subtitleObj.AddComponent<Text>();
            subText.font = UnityEngine.Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            subText.fontSize = 22;
            subText.color = new Color(0.8f, 0.8f, 0.8f);
            subText.alignment = TextAnchor.MiddleCenter;
            subText.text = $"All settlers perished on Day {dayCount}";

            // Restart button
            float btnSize = _minTouchTarget * 2.5f;
            var btnObj = new GameObject("RestartButton");
            btnObj.transform.SetParent(_gameOverPanel.transform, false);
            var btnRect = btnObj.AddComponent<RectTransform>();
            btnRect.anchorMin = new Vector2(0.5f, 0.5f);
            btnRect.anchorMax = new Vector2(0.5f, 0.5f);
            btnRect.pivot = new Vector2(0.5f, 0.5f);
            btnRect.anchoredPosition = new Vector2(0, -60);
            btnRect.sizeDelta = new Vector2(btnSize, _minTouchTarget);
            var btnImage = btnObj.AddComponent<Image>();
            btnImage.color = new Color(0.2f, 0.5f, 0.3f, 0.9f);
            var button = btnObj.AddComponent<Button>();
            button.targetGraphic = btnImage;
            button.onClick.AddListener(RestartGame);

            var labelObj = new GameObject("Label");
            labelObj.transform.SetParent(btnObj.transform, false);
            var labelRect = labelObj.AddComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.sizeDelta = Vector2.zero;
            var label = labelObj.AddComponent<Text>();
            label.font = UnityEngine.Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = 28;
            label.color = Color.white;
            label.alignment = TextAnchor.MiddleCenter;
            label.fontStyle = FontStyle.Bold;
            label.text = "RESTART";
        }

        private void RestartGame()
        {
            EventBus.Clear();
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        // ─── UI Construction ──────────────────────────────────────

        private void CreateUI()
        {
            // Ensure we have a Canvas
            Canvas canvas = GetComponent<Canvas>();
            if (canvas == null)
            {
                canvas = gameObject.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 100;
            }

            // Canvas Scaler for consistent sizing
            if (GetComponent<CanvasScaler>() == null)
            {
                var scaler = gameObject.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
            }

            // GraphicRaycaster required for button clicks and IsPointerOverGameObject()
            if (GetComponent<GraphicRaycaster>() == null)
                gameObject.AddComponent<GraphicRaycaster>();

            // Resource text (top-left) — taller to support expanded category detail
            _resourceText = CreateText("ResourceText",
                new Vector2(20, -20),
                new Vector2(800, 200),
                TextAnchor.UpperLeft);
            _resourceText.verticalOverflow = VerticalWrapMode.Overflow;

            // Event notification (top-center)
            _eventText = CreateText("EventText",
                new Vector2(0, -20),
                new Vector2(500, 40),
                TextAnchor.UpperCenter);
            _eventText.color = Color.yellow;

            // Day counter (top-right) — replaces old calendar
            _dayCounterText = CreateText("DayCounterText",
                new Vector2(-20, -20),
                new Vector2(250, 40),
                TextAnchor.UpperRight);
            _dayCounterText.text = "Day 1";

            // Warning text (below resource text)
            _warningText = CreateText("WarningText",
                new Vector2(20, -230),
                new Vector2(400, 30),
                TextAnchor.UpperLeft);
            _warningText.color = new Color(1f, 0.3f, 0.3f);
            _warningText.fontSize = _fontSize - 2;
            _warningText.text = "";

            // Category toggle buttons (below resource text area)
            CreateCategoryButtons();

            // Speed widget (top-right, below day counter)
            CreateSpeedWidget();

            // Version label (bottom-right) with dark background
            var versionGo = new GameObject("VersionLabel");
            versionGo.transform.SetParent(transform, false);
            var versionBg = versionGo.AddComponent<Image>();
            versionBg.color = new Color(0f, 0f, 0f, 0.7f);
            var versionBgRt = versionGo.GetComponent<RectTransform>();
            versionBgRt.anchorMin = new Vector2(1, 0);
            versionBgRt.anchorMax = new Vector2(1, 0);
            versionBgRt.pivot = new Vector2(1, 0);
            versionBgRt.anchoredPosition = new Vector2(-8, 8);
            versionBgRt.sizeDelta = new Vector2(160, 32);

            var versionText = CreateText("VersionText",
                Vector2.zero, new Vector2(160, 32), TextAnchor.MiddleCenter);
            versionText.transform.SetParent(versionGo.transform, false);
            var vrt = versionText.GetComponent<RectTransform>();
            vrt.anchorMin = Vector2.zero;
            vrt.anchorMax = Vector2.one;
            vrt.offsetMin = Vector2.zero;
            vrt.offsetMax = Vector2.zero;
            versionText.fontSize = 18;
            versionText.fontStyle = FontStyle.Bold;
            versionText.color = Color.white;
            versionText.text = "v0.4.1";
        }

        /// <summary>
        /// Create small category toggle buttons next to the resource text.
        /// Tap to expand/collapse category detail.
        /// Feature 2.4: Categorized resource panel with expand/collapse.
        /// </summary>
        private void CreateCategoryButtons()
        {
            float btnW = 60f;
            float btnH = 28f;
            float spacing = 4f;
            float startX = 20f;
            float startY = -50f;

            var categories = new[]
            {
                ("Wood",   COLOR_WOOD),
                ("Stone",  COLOR_STONE),
                ("Plant",  COLOR_PLANT),
                ("Animal", COLOR_ANIMAL),
                ("Other",  COLOR_OTHER)
            };

            Button[] buttons = new Button[categories.Length];

            for (int i = 0; i < categories.Length; i++)
            {
                int idx = i;
                string label = categories[i].Item1;
                Color color = categories[i].Item2;

                var btnObj = new GameObject($"CategoryBtn_{label}");
                btnObj.transform.SetParent(transform, false);

                var btnRect = btnObj.AddComponent<RectTransform>();
                btnRect.anchorMin = new Vector2(0, 1);
                btnRect.anchorMax = new Vector2(0, 1);
                btnRect.pivot = new Vector2(0, 1);
                btnRect.anchoredPosition = new Vector2(startX + i * (btnW + spacing), startY);
                btnRect.sizeDelta = new Vector2(btnW, btnH);

                // Colored square icon
                var colorIcon = new GameObject("ColorIcon");
                colorIcon.transform.SetParent(btnObj.transform, false);
                var iconRect = colorIcon.AddComponent<RectTransform>();
                iconRect.anchorMin = new Vector2(0, 0.5f);
                iconRect.anchorMax = new Vector2(0, 0.5f);
                iconRect.pivot = new Vector2(0, 0.5f);
                iconRect.anchoredPosition = new Vector2(3, 0);
                iconRect.sizeDelta = new Vector2(10, 10);
                var iconImage = colorIcon.AddComponent<Image>();
                iconImage.color = color;

                // Button background
                var bgImage = btnObj.AddComponent<Image>();
                bgImage.color = new Color(0.15f, 0.15f, 0.15f, 0.8f);

                var button = btnObj.AddComponent<Button>();
                button.targetGraphic = bgImage;
                buttons[i] = button;

                // Label
                var labelObj = new GameObject("Label");
                labelObj.transform.SetParent(btnObj.transform, false);
                var labelRect = labelObj.AddComponent<RectTransform>();
                labelRect.anchorMin = Vector2.zero;
                labelRect.anchorMax = Vector2.one;
                labelRect.offsetMin = new Vector2(15, 0);
                labelRect.offsetMax = Vector2.zero;
                var labelText = labelObj.AddComponent<Text>();
                labelText.font = UnityEngine.Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                labelText.fontSize = 14;
                labelText.color = Color.white;
                labelText.alignment = TextAnchor.MiddleLeft;
                labelText.text = label;
            }

            buttons[0].onClick.AddListener(ToggleWood);
            buttons[1].onClick.AddListener(ToggleStone);
            buttons[2].onClick.AddListener(TogglePlant);
            buttons[3].onClick.AddListener(ToggleAnimal);
            buttons[4].onClick.AddListener(ToggleOther);

            _woodButton = buttons[0];
            _stoneButton = buttons[1];
            _plantButton = buttons[2];
            _animalButton = buttons[3];
            _otherButton = buttons[4];
        }

        // ─── Speed Widget ─────────────────────────────────────────

        private void CreateSpeedWidget()
        {
            float buttonSize = _minTouchTarget;
            float spacing = 4f;
            float totalWidth = SPEED_LABELS.Length * buttonSize + (SPEED_LABELS.Length - 1) * spacing;

            var container = new GameObject("SpeedWidget");
            container.transform.SetParent(transform, false);
            var containerRect = container.AddComponent<RectTransform>();
            containerRect.anchorMin = new Vector2(1, 1);
            containerRect.anchorMax = new Vector2(1, 1);
            containerRect.pivot = new Vector2(1, 1);
            containerRect.anchoredPosition = new Vector2(-20, -60);
            containerRect.sizeDelta = new Vector2(totalWidth, buttonSize);

            _speedButtons = new Button[SPEED_LABELS.Length];
            _speedButtonTexts = new Text[SPEED_LABELS.Length];

            for (int i = 0; i < SPEED_LABELS.Length; i++)
            {
                int speedIndex = i;

                var btnObj = new GameObject($"SpeedBtn_{SPEED_LABELS[i]}");
                btnObj.transform.SetParent(container.transform, false);

                var btnRect = btnObj.AddComponent<RectTransform>();
                btnRect.anchorMin = new Vector2(0, 0.5f);
                btnRect.anchorMax = new Vector2(0, 0.5f);
                btnRect.pivot = new Vector2(0, 0.5f);
                btnRect.anchoredPosition = new Vector2(i * (buttonSize + spacing), 0);
                btnRect.sizeDelta = new Vector2(buttonSize, buttonSize);

                var image = btnObj.AddComponent<Image>();
                image.color = new Color(0.2f, 0.2f, 0.2f, 0.7f);

                var button = btnObj.AddComponent<Button>();
                button.targetGraphic = image;
                button.onClick.AddListener(() => SetSpeed(speedIndex));
                _speedButtons[i] = button;

                var labelObj = new GameObject("Label");
                labelObj.transform.SetParent(btnObj.transform, false);
                var labelRect = labelObj.AddComponent<RectTransform>();
                labelRect.anchorMin = Vector2.zero;
                labelRect.anchorMax = Vector2.one;
                labelRect.sizeDelta = Vector2.zero;

                var label = labelObj.AddComponent<Text>();
                label.font = UnityEngine.Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                label.fontSize = _fontSize - 4;
                label.color = Color.white;
                label.alignment = TextAnchor.MiddleCenter;
                label.text = SPEED_LABELS[i];
                _speedButtonTexts[i] = label;
            }

            UpdateSpeedButtons();
        }

        private void SetSpeed(int speedIndex)
        {
            if (speedIndex < 0 || speedIndex >= SPEED_VALUES.Length) return;
            if (_gameOverPanel != null) return;

            _currentSpeedIndex = speedIndex;
            Time.timeScale = SPEED_VALUES[speedIndex];
            UpdateSpeedButtons();
        }

        private void UpdateSpeedButtons()
        {
            for (int i = 0; i < _speedButtons.Length; i++)
            {
                bool active = i == _currentSpeedIndex;
                var image = _speedButtons[i].GetComponent<Image>();
                image.color = active
                    ? new Color(0.3f, 0.6f, 0.9f, 0.9f)
                    : new Color(0.2f, 0.2f, 0.2f, 0.7f);
                _speedButtonTexts[i].color = active ? Color.white : new Color(0.7f, 0.7f, 0.7f);
            }
        }

        // ─── Text Helper ──────────────────────────────────────────

        private Text CreateText(string name, Vector2 offset, Vector2 size, TextAnchor alignment)
        {
            var textObj = new GameObject(name);
            textObj.transform.SetParent(transform, false);

            var rectTransform = textObj.AddComponent<RectTransform>();

            if (alignment == TextAnchor.UpperLeft)
            {
                rectTransform.anchorMin = new Vector2(0, 1);
                rectTransform.anchorMax = new Vector2(0, 1);
                rectTransform.pivot = new Vector2(0, 1);
            }
            else if (alignment == TextAnchor.UpperRight)
            {
                rectTransform.anchorMin = new Vector2(1, 1);
                rectTransform.anchorMax = new Vector2(1, 1);
                rectTransform.pivot = new Vector2(1, 1);
            }
            else
            {
                rectTransform.anchorMin = new Vector2(0.5f, 1);
                rectTransform.anchorMax = new Vector2(0.5f, 1);
                rectTransform.pivot = new Vector2(0.5f, 1);
            }

            rectTransform.anchoredPosition = offset;
            rectTransform.sizeDelta = size;

            var text = textObj.AddComponent<Text>();
            text.font = UnityEngine.Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = _fontSize;
            text.color = Color.white;
            text.alignment = alignment;

            var shadow = textObj.AddComponent<Shadow>();
            shadow.effectColor = new Color(0, 0, 0, 0.8f);
            shadow.effectDistance = new Vector2(1, -1);

            return text;
        }

        // ─── Cleanup ──────────────────────────────────────────────

        private void OnDestroy()
        {
            // Legacy events
            EventBus.Unsubscribe<BuildingPlacedEvent>(OnBuildingPlaced);
            EventBus.Unsubscribe<BuildingCompletedEvent>(OnBuildingCompleted);
            EventBus.Unsubscribe<PopulationChangedEvent>(OnPopulationChanged);
            EventBus.Unsubscribe<ResourceChangedEvent>(OnResourceChanged);
            EventBus.Unsubscribe<SettlerDiedEvent>(OnSettlerDied);
            EventBus.Unsubscribe<FoodWarningEvent>(OnFoodWarning);
            EventBus.Unsubscribe<DiscoveryMadeEvent>(OnDiscoveryMade);

            // MS4 events
            EventBus.Unsubscribe<DayChangedEvent>(OnDayChanged);
            EventBus.Unsubscribe<ToolBrokeEvent>(OnToolBroke);
            EventBus.Unsubscribe<NeedsCriticalEvent>(OnNeedsCritical);
            EventBus.Unsubscribe<SettlerPoisonedEvent>(OnSettlerPoisoned);

            if (_speedButtons != null)
            {
                foreach (var btn in _speedButtons)
                {
                    if (btn != null)
                        btn.onClick.RemoveAllListeners();
                }
            }

            if (_woodButton != null) _woodButton.onClick.RemoveAllListeners();
            if (_stoneButton != null) _stoneButton.onClick.RemoveAllListeners();
            if (_plantButton != null) _plantButton.onClick.RemoveAllListeners();
            if (_animalButton != null) _animalButton.onClick.RemoveAllListeners();
            if (_otherButton != null) _otherButton.onClick.RemoveAllListeners();

            Time.timeScale = 1f;
        }
    }
}
