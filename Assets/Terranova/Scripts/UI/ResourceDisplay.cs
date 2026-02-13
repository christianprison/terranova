using UnityEngine;
using UnityEngine.UI;
using Terranova.Core;

namespace Terranova.UI
{
    /// <summary>
    /// Simple HUD showing resource counts (Wood, Stone), game speed controls,
    /// and epoch indicator.
    ///
    /// For MS1: Static numbers, no gathering yet. The display reacts to
    /// BuildingPlacedEvent to show that the event bus works.
    /// Speed widget controls Time.timeScale (Pause/1x/2x/3x).
    /// Epoch indicator shows current epoch (static "Epoch I.1" for MS1).
    ///
    /// Scene setup:
    ///   1. Create Canvas (Screen Space - Overlay)
    ///   2. Add this component to the Canvas
    ///   3. It auto-creates Text elements on Start
    ///
    /// Later milestones will replace this with a proper UI framework.
    /// </summary>
    public class ResourceDisplay : MonoBehaviour
    {
        [Header("UI Settings")]
        [Tooltip("Font size for resource text.")]
        [SerializeField] private int _fontSize = 24;

        [Tooltip("Minimum touch target size in points (Apple HIG: 44pt).")]
        [SerializeField] private float _minTouchTarget = 44f;

        // Available game speeds (index 0 = pause)
        private static readonly float[] SPEED_VALUES = { 0f, 1f, 2f, 3f };
        private static readonly string[] SPEED_LABELS = { "❚❚", "1x", "2x", "3x" };
        private int _currentSpeedIndex = 1; // Start at 1x

        private int _settlers;
        private bool _foodWarning;

        private Text _resourceText;
        private Text _eventText;
        private Text _epochText;
        private Text _warningText;
        private Button[] _speedButtons;
        private Text[] _speedButtonTexts;
        private float _eventDisplayTimer;

        private void Start()
        {
            CreateUI();
            UpdateDisplay();

            // Listen for events that affect the display
            EventBus.Subscribe<BuildingPlacedEvent>(OnBuildingPlaced);
            EventBus.Subscribe<BuildingCompletedEvent>(OnBuildingCompleted);
            EventBus.Subscribe<PopulationChangedEvent>(OnPopulationChanged);
            EventBus.Subscribe<ResourceChangedEvent>(OnResourceChanged);
            EventBus.Subscribe<SettlerDiedEvent>(OnSettlerDied);
            EventBus.Subscribe<FoodWarningEvent>(OnFoodWarning);
        }

        private void Update()
        {
            // Fade out event notification after 3 seconds
            if (_eventDisplayTimer > 0)
            {
                _eventDisplayTimer -= Time.deltaTime;
                if (_eventDisplayTimer <= 0 && _eventText != null)
                    _eventText.text = "";
            }

            // Check food supply for warning (Story 5.4)
            CheckFoodWarning();
        }

        /// <summary>
        /// Publish food warning when supply drops below threshold.
        /// Story 5.4: Warning when food < 5 or < 1 per settler.
        /// </summary>
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

        private void OnPopulationChanged(PopulationChangedEvent evt)
        {
            _settlers = evt.CurrentPopulation;
            UpdateDisplay();
        }

        /// <summary>
        /// Story 4.1: ResourceManager publishes this when resources change.
        /// </summary>
        private void OnResourceChanged(ResourceChangedEvent evt)
        {
            UpdateDisplay();
        }

        private void OnBuildingPlaced(BuildingPlacedEvent evt)
        {
            UpdateDisplay();

            if (_eventText != null)
            {
                _eventText.text = $"Building {evt.BuildingName}...";
                _eventDisplayTimer = 3f;
            }
        }

        /// <summary>Story 5.4: Notification when settler dies.</summary>
        private void OnSettlerDied(SettlerDiedEvent evt)
        {
            if (_eventText != null)
            {
                _eventText.text = $"{evt.SettlerName} died ({evt.CauseOfDeath})";
                _eventDisplayTimer = 4f;
            }
        }

        /// <summary>Story 5.4: Food warning.</summary>
        private void OnFoodWarning(FoodWarningEvent evt)
        {
            _foodWarning = evt.IsWarning;
            if (_warningText != null)
                _warningText.text = _foodWarning ? "Food is running low!" : "";
        }

        /// <summary>Story 4.2: Notification when construction completes.</summary>
        private void OnBuildingCompleted(BuildingCompletedEvent evt)
        {
            if (_eventText != null)
            {
                _eventText.text = $"{evt.BuildingName} complete!";
                _eventDisplayTimer = 3f;
            }
        }

        /// <summary>
        /// Refresh the resource text from ResourceManager.
        /// Story 4.1: Now reads from central ResourceManager instead of local counters.
        /// </summary>
        private void UpdateDisplay()
        {
            if (_resourceText == null) return;

            var rm = ResourceManager.Instance;
            if (rm != null)
                _resourceText.text = $"Wood: {rm.Wood}    Stone: {rm.Stone}    Food: {rm.Food}    Settlers: {_settlers}";
            else
                _resourceText.text = $"Settlers: {_settlers}";
        }

        /// <summary>
        /// Auto-create the UI elements. For MS1, this is simpler than
        /// requiring manual UI setup in the scene.
        /// </summary>
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

            // Resource counter (top-left)
            _resourceText = CreateText("ResourceText",
                new Vector2(20, -20),    // Offset from top-left
                new Vector2(400, 40),    // Size
                TextAnchor.UpperLeft);

            // Event notification (top-center)
            _eventText = CreateText("EventText",
                new Vector2(0, -20),     // Centered, near top
                new Vector2(500, 40),
                TextAnchor.UpperCenter);
            _eventText.color = Color.yellow;

            // Epoch indicator (top-right)
            _epochText = CreateText("EpochText",
                new Vector2(-20, -20),   // Offset from top-right
                new Vector2(200, 40),
                TextAnchor.UpperRight);
            _epochText.text = "Epoch I.1";

            // Food warning (below resource text)
            _warningText = CreateText("WarningText",
                new Vector2(20, -50),
                new Vector2(400, 30),
                TextAnchor.UpperLeft);
            _warningText.color = new Color(1f, 0.3f, 0.3f); // Red warning
            _warningText.fontSize = _fontSize - 2;
            _warningText.text = "";

            // Speed widget (top-right, below epoch)
            CreateSpeedWidget();
        }

        /// <summary>
        /// Create the speed control buttons (Pause/1x/2x/3x) in the top-right area.
        /// All buttons meet the minimum 44×44pt touch target requirement.
        /// </summary>
        private void CreateSpeedWidget()
        {
            float buttonSize = _minTouchTarget;
            float spacing = 4f;
            float totalWidth = SPEED_LABELS.Length * buttonSize + (SPEED_LABELS.Length - 1) * spacing;

            // Container anchored to top-right, below epoch text
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
                int speedIndex = i; // Capture for closure

                var btnObj = new GameObject($"SpeedBtn_{SPEED_LABELS[i]}");
                btnObj.transform.SetParent(container.transform, false);

                var btnRect = btnObj.AddComponent<RectTransform>();
                btnRect.anchorMin = new Vector2(0, 0.5f);
                btnRect.anchorMax = new Vector2(0, 0.5f);
                btnRect.pivot = new Vector2(0, 0.5f);
                btnRect.anchoredPosition = new Vector2(i * (buttonSize + spacing), 0);
                btnRect.sizeDelta = new Vector2(buttonSize, buttonSize);

                // Button background
                var image = btnObj.AddComponent<Image>();
                image.color = new Color(0.2f, 0.2f, 0.2f, 0.7f);

                // Button component
                var button = btnObj.AddComponent<Button>();
                button.targetGraphic = image;
                button.onClick.AddListener(() => SetSpeed(speedIndex));
                _speedButtons[i] = button;

                // Button label
                var labelObj = new GameObject("Label");
                labelObj.transform.SetParent(btnObj.transform, false);
                var labelRect = labelObj.AddComponent<RectTransform>();
                labelRect.anchorMin = Vector2.zero;
                labelRect.anchorMax = Vector2.one;
                labelRect.sizeDelta = Vector2.zero;

                var label = labelObj.AddComponent<Text>();
                label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                label.fontSize = _fontSize - 4;
                label.color = Color.white;
                label.alignment = TextAnchor.MiddleCenter;
                label.text = SPEED_LABELS[i];
                _speedButtonTexts[i] = label;
            }

            UpdateSpeedButtons();
        }

        /// <summary>
        /// Set the game speed and update Time.timeScale.
        /// Index 0 = Pause, 1 = 1x, 2 = 2x, 3 = 3x.
        /// </summary>
        private void SetSpeed(int speedIndex)
        {
            if (speedIndex < 0 || speedIndex >= SPEED_VALUES.Length)
                return;

            _currentSpeedIndex = speedIndex;
            Time.timeScale = SPEED_VALUES[speedIndex];
            UpdateSpeedButtons();
        }

        /// <summary>
        /// Highlight the active speed button and dim the others.
        /// </summary>
        private void UpdateSpeedButtons()
        {
            for (int i = 0; i < _speedButtons.Length; i++)
            {
                bool active = i == _currentSpeedIndex;
                var image = _speedButtons[i].GetComponent<Image>();
                image.color = active
                    ? new Color(0.3f, 0.6f, 0.9f, 0.9f)  // Blue highlight
                    : new Color(0.2f, 0.2f, 0.2f, 0.7f);  // Dark background
                _speedButtonTexts[i].color = active ? Color.white : new Color(0.7f, 0.7f, 0.7f);
            }
        }

        /// <summary>
        /// Helper to create a UI Text element anchored to the top of the screen.
        /// Supports left, center, and right alignment.
        /// </summary>
        private Text CreateText(string name, Vector2 offset, Vector2 size, TextAnchor alignment)
        {
            var textObj = new GameObject(name);
            textObj.transform.SetParent(transform, false);

            var rectTransform = textObj.AddComponent<RectTransform>();

            // Set anchor based on alignment
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
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = _fontSize;
            text.color = Color.white;
            text.alignment = alignment;

            // Add shadow for readability over terrain
            var shadow = textObj.AddComponent<Shadow>();
            shadow.effectColor = new Color(0, 0, 0, 0.8f);
            shadow.effectDistance = new Vector2(1, -1);

            return text;
        }

        private void OnDestroy()
        {
            EventBus.Unsubscribe<BuildingPlacedEvent>(OnBuildingPlaced);
            EventBus.Unsubscribe<BuildingCompletedEvent>(OnBuildingCompleted);
            EventBus.Unsubscribe<PopulationChangedEvent>(OnPopulationChanged);
            EventBus.Unsubscribe<ResourceChangedEvent>(OnResourceChanged);
            EventBus.Unsubscribe<SettlerDiedEvent>(OnSettlerDied);
            EventBus.Unsubscribe<FoodWarningEvent>(OnFoodWarning);

            // Clean up button listeners to prevent memory leaks
            if (_speedButtons != null)
            {
                foreach (var btn in _speedButtons)
                {
                    if (btn != null)
                        btn.onClick.RemoveAllListeners();
                }
            }

            // Restore normal time scale if this UI is destroyed while paused
            Time.timeScale = 1f;
        }
    }
}
