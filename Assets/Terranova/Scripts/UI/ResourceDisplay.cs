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
        [Header("Starting Resources (MS1 test values)")]
        [SerializeField] private int _wood = 50;
        [SerializeField] private int _stone = 30;

        [Header("UI Settings")]
        [Tooltip("Font size for resource text.")]
        [SerializeField] private int _fontSize = 24;

        [Tooltip("Minimum touch target size in points (Apple HIG: 44pt).")]
        [SerializeField] private float _minTouchTarget = 44f;

        // Available game speeds (index 0 = pause)
        private static readonly float[] SPEED_VALUES = { 0f, 1f, 2f, 3f };
        private static readonly string[] SPEED_LABELS = { "❚❚", "1x", "2x", "3x" };
        private int _currentSpeedIndex = 1; // Start at 1x

        private Text _resourceText;
        private Text _eventText;
        private Text _epochText;
        private Button[] _speedButtons;
        private Text[] _speedButtonTexts;
        private float _eventDisplayTimer;

        private void Start()
        {
            CreateUI();
            UpdateDisplay();

            // Listen for building placements to update resource counts
            EventBus.Subscribe<BuildingPlacedEvent>(OnBuildingPlaced);
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
        }

        private void OnBuildingPlaced(BuildingPlacedEvent evt)
        {
            // Deduct resources (placeholder – actual costs come from BuildingDefinition in MS2)
            _wood = Mathf.Max(0, _wood - 5);
            UpdateDisplay();

            // Show notification
            if (_eventText != null)
            {
                _eventText.text = $"Built {evt.BuildingName}!";
                _eventDisplayTimer = 3f;
            }
        }

        /// <summary>
        /// Refresh the resource text.
        /// </summary>
        private void UpdateDisplay()
        {
            if (_resourceText != null)
                _resourceText.text = $"Wood: {_wood}    Stone: {_stone}";
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
        }
    }
}
