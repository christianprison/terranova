using UnityEngine;
using UnityEngine.UI;
using Terranova.Core;

namespace Terranova.UI
{
    /// <summary>
    /// Simple HUD showing resource counts (Wood, Stone).
    ///
    /// For MS1: Static numbers, no gathering yet. The display reacts to
    /// BuildingPlacedEvent to show that the event bus works.
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

        private Text _resourceText;
        private Text _eventText;
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
        }

        /// <summary>
        /// Helper to create a UI Text element anchored to the top of the screen.
        /// </summary>
        private Text CreateText(string name, Vector2 offset, Vector2 size, TextAnchor alignment)
        {
            var textObj = new GameObject(name);
            textObj.transform.SetParent(transform, false);

            var rectTransform = textObj.AddComponent<RectTransform>();

            // Anchor to top-left for resource text, top-center for events
            if (alignment == TextAnchor.UpperLeft)
            {
                rectTransform.anchorMin = new Vector2(0, 1);
                rectTransform.anchorMax = new Vector2(0, 1);
                rectTransform.pivot = new Vector2(0, 1);
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
