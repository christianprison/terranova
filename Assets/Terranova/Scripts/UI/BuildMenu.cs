using UnityEngine;
using UnityEngine.UI;
using Terranova.Core;
using Terranova.Buildings;

namespace Terranova.UI
{
    /// <summary>
    /// Build menu UI showing all available buildings with costs.
    /// Player selects a building to enter placement mode.
    ///
    /// - Grays out buildings the player can't afford
    /// - Updates live when resources change
    /// - Press B or click the toggle button to open/close
    ///
    /// Story 4.5: Bau-Menü
    /// </summary>
    public class BuildMenu : MonoBehaviour
    {
        private const float BUTTON_WIDTH = 160f;
        private const float BUTTON_HEIGHT = 70f;
        private const float SPACING = 8f;
        private const int FONT_SIZE = 14;

        private GameObject _panel;
        private BuildingButton[] _buttons;
        private bool _isOpen;

        private struct BuildingButton
        {
            public Button Button;
            public Text Label;
            public Image Background;
            public BuildingDefinition Definition;
        }

        private void Start()
        {
            CreateToggleButton();
        }

        private void OnEnable()
        {
            EventBus.Subscribe<ResourceChangedEvent>(OnResourceChanged);
        }

        private void Update()
        {
            // B key toggles the build menu
            if (UnityEngine.InputSystem.Keyboard.current != null &&
                UnityEngine.InputSystem.Keyboard.current.bKey.wasPressedThisFrame)
            {
                ToggleMenu();
            }
        }

        private void ToggleMenu()
        {
            if (_isOpen)
                CloseMenu();
            else
                OpenMenu();
        }

        private void OpenMenu()
        {
            if (_panel == null)
                CreatePanel();

            if (_panel == null) return; // No registry yet

            _panel.SetActive(true);
            _isOpen = true;
            RefreshButtons();
        }

        private void CloseMenu()
        {
            if (_panel != null)
                _panel.SetActive(false);
            _isOpen = false;
        }

        private void OnResourceChanged(ResourceChangedEvent evt)
        {
            if (_isOpen)
                RefreshButtons();
        }

        /// <summary>
        /// Create the toggle button (bottom-left corner).
        /// </summary>
        private void CreateToggleButton()
        {
            var canvas = GetComponent<Canvas>();
            if (canvas == null) return;

            var btnObj = new GameObject("BuildMenuToggle");
            btnObj.transform.SetParent(transform, false);

            var rect = btnObj.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0, 0);
            rect.anchorMax = new Vector2(0, 0);
            rect.pivot = new Vector2(0, 0);
            rect.anchoredPosition = new Vector2(20, 20);
            rect.sizeDelta = new Vector2(100, 44);

            var image = btnObj.AddComponent<Image>();
            image.color = new Color(0.25f, 0.45f, 0.25f, 0.85f);

            var button = btnObj.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(ToggleMenu);

            var labelObj = new GameObject("Label");
            labelObj.transform.SetParent(btnObj.transform, false);
            var labelRect = labelObj.AddComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.sizeDelta = Vector2.zero;

            var label = labelObj.AddComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = 18;
            label.color = Color.white;
            label.alignment = TextAnchor.MiddleCenter;
            label.text = "Build [B]";
        }

        /// <summary>
        /// Create the building selection panel.
        /// </summary>
        private void CreatePanel()
        {
            var registry = BuildingRegistry.Instance;
            if (registry == null || registry.Definitions == null) return;

            var defs = registry.Definitions;

            _panel = new GameObject("BuildPanel");
            _panel.transform.SetParent(transform, false);

            var panelRect = _panel.AddComponent<RectTransform>();
            float totalWidth = defs.Length * (BUTTON_WIDTH + SPACING) + SPACING;
            panelRect.anchorMin = new Vector2(0.5f, 0);
            panelRect.anchorMax = new Vector2(0.5f, 0);
            panelRect.pivot = new Vector2(0.5f, 0);
            panelRect.anchoredPosition = new Vector2(0, 70);
            panelRect.sizeDelta = new Vector2(totalWidth, BUTTON_HEIGHT + SPACING * 2);

            var panelImage = _panel.AddComponent<Image>();
            panelImage.color = new Color(0.1f, 0.1f, 0.1f, 0.8f);

            _buttons = new BuildingButton[defs.Length];

            for (int i = 0; i < defs.Length; i++)
            {
                var def = defs[i];
                int index = i; // Capture for closure

                var btnObj = new GameObject($"Btn_{def.DisplayName}");
                btnObj.transform.SetParent(_panel.transform, false);

                var btnRect = btnObj.AddComponent<RectTransform>();
                btnRect.anchorMin = new Vector2(0, 0.5f);
                btnRect.anchorMax = new Vector2(0, 0.5f);
                btnRect.pivot = new Vector2(0, 0.5f);
                btnRect.anchoredPosition = new Vector2(SPACING + i * (BUTTON_WIDTH + SPACING), 0);
                btnRect.sizeDelta = new Vector2(BUTTON_WIDTH, BUTTON_HEIGHT);

                var bg = btnObj.AddComponent<Image>();
                bg.color = new Color(0.25f, 0.25f, 0.25f, 0.9f);

                var btn = btnObj.AddComponent<Button>();
                btn.targetGraphic = bg;
                btn.onClick.AddListener(() => OnBuildingSelected(index));

                // Label with name and cost
                var labelObj = new GameObject("Label");
                labelObj.transform.SetParent(btnObj.transform, false);
                var labelRect = labelObj.AddComponent<RectTransform>();
                labelRect.anchorMin = Vector2.zero;
                labelRect.anchorMax = Vector2.one;
                labelRect.sizeDelta = Vector2.zero;
                labelRect.offsetMin = new Vector2(4, 4);
                labelRect.offsetMax = new Vector2(-4, -4);

                var label = labelObj.AddComponent<Text>();
                label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                label.fontSize = FONT_SIZE;
                label.color = Color.white;
                label.alignment = TextAnchor.MiddleCenter;

                string costText = def.StoneCost > 0
                    ? $"{def.DisplayName}\n{def.WoodCost}W  {def.StoneCost}S"
                    : $"{def.DisplayName}\n{def.WoodCost}W";
                label.text = costText;

                _buttons[i] = new BuildingButton
                {
                    Button = btn,
                    Label = label,
                    Background = bg,
                    Definition = def
                };
            }

            _panel.SetActive(false);
        }

        /// <summary>
        /// Refresh button states based on current resources.
        /// </summary>
        private void RefreshButtons()
        {
            if (_buttons == null) return;

            var rm = ResourceManager.Instance;
            if (rm == null) return;

            foreach (var btn in _buttons)
            {
                bool canAfford = rm.CanAfford(btn.Definition.WoodCost, btn.Definition.StoneCost);
                btn.Button.interactable = canAfford;
                btn.Background.color = canAfford
                    ? new Color(0.25f, 0.25f, 0.25f, 0.9f)
                    : new Color(0.15f, 0.10f, 0.10f, 0.9f);
                btn.Label.color = canAfford ? Color.white : new Color(0.5f, 0.3f, 0.3f);
            }
        }

        /// <summary>
        /// Player selected a building to place.
        /// </summary>
        private void OnBuildingSelected(int index)
        {
            if (_buttons == null || index >= _buttons.Length) return;

            var def = _buttons[index].Definition;
            var rm = ResourceManager.Instance;
            if (rm != null && !rm.CanAfford(def.WoodCost, def.StoneCost))
                return;

            // Find the BuildingPlacer and start placement
            var placer = FindFirstObjectByType<BuildingPlacer>();
            if (placer != null)
            {
                placer.StartPlacement(def);
                CloseMenu();
            }
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<ResourceChangedEvent>(OnResourceChanged);
        }

        private void OnDestroy()
        {
            // Safety: ensure cleanup even if OnDisable wasn't called
            EventBus.Unsubscribe<ResourceChangedEvent>(OnResourceChanged);
        }
    }
}
