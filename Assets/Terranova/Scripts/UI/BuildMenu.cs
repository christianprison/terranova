using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Terranova.Core;
using Terranova.Buildings;
using Terranova.Discovery;

namespace Terranova.UI
{
    /// <summary>
    /// Build menu UI showing available buildings with costs.
    /// Player selects a building to enter placement mode.
    ///
    /// - Grays out buildings the player can't afford
    /// - Updates live when resources change
    /// - Hides buildings that haven't been unlocked by discoveries (Feature 3.2)
    /// - Press B or click the toggle button to open/close
    ///
    /// Story 4.5: Bau-Menü
    /// Feature 3.2: Discovery-gated buildings
    /// </summary>
    public class BuildMenu : MonoBehaviour
    {
        private const float BUTTON_WIDTH = 160f;
        private const float BUTTON_HEIGHT = 70f;
        private const float SPACING = 8f;
        private const int FONT_SIZE = 14;

        // Building types that require discovery unlock
        private static readonly HashSet<BuildingType> DISCOVERY_GATED = new()
        {
            BuildingType.CookingFire,
            BuildingType.TrapSite,
            // Feature 5.3: New buildable structures
            BuildingType.Windscreen,
            BuildingType.LeafHut,
            BuildingType.OpenFireplace,
            BuildingType.DugFireplace,
            BuildingType.DryingRack,
            BuildingType.StoragePit,
            BuildingType.StoneCircleWindbreak
        };

        private GameObject _panel;
        private List<BuildingButton> _buttons;
        private bool _isOpen;
        private bool _panelDirty = true;

        private struct BuildingButton
        {
            public GameObject Root;
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
            EventBus.Subscribe<DiscoveryMadeEvent>(OnDiscoveryMade);
        }

        private void Update()
        {
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
            if (_panelDirty || _panel == null)
                RebuildPanel();

            if (_panel == null) return;

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

        private void OnDiscoveryMade(DiscoveryMadeEvent evt)
        {
            _panelDirty = true;
            if (_isOpen)
            {
                RebuildPanel();
                _panel.SetActive(true);
                RefreshButtons();
            }
        }

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
            label.font = UnityEngine.Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = 18;
            label.color = Color.white;
            label.alignment = TextAnchor.MiddleCenter;
            label.text = "Build [B]";
        }

        /// <summary>
        /// Rebuild the panel, filtering out buildings not yet unlocked by discoveries.
        /// </summary>
        private void RebuildPanel()
        {
            if (_panel != null)
                Destroy(_panel);

            var registry = BuildingRegistry.Instance;
            if (registry == null || registry.Definitions == null) return;

            var sm = DiscoveryStateManager.Instance;

            // Filter definitions: show always-available + discovery-unlocked
            var visibleDefs = new List<BuildingDefinition>();
            foreach (var def in registry.Definitions)
            {
                if (DISCOVERY_GATED.Contains(def.Type))
                {
                    // Feature 5.3: Check RequiredDiscoveries (capabilities)
                    if (def.RequiredDiscoveries != null && def.RequiredDiscoveries.Length > 0)
                    {
                        if (sm != null && HasAllCapabilities(sm, def.RequiredDiscoveries))
                            visibleDefs.Add(def);
                    }
                    else if (sm != null && sm.IsBuildingUnlocked(def.Type))
                    {
                        visibleDefs.Add(def);
                    }
                }
                else
                {
                    visibleDefs.Add(def);
                }
            }

            if (visibleDefs.Count == 0) return;

            _panel = new GameObject("BuildPanel");
            _panel.transform.SetParent(transform, false);

            var panelRect = _panel.AddComponent<RectTransform>();
            float totalWidth = visibleDefs.Count * (BUTTON_WIDTH + SPACING) + SPACING;
            panelRect.anchorMin = new Vector2(0.5f, 0);
            panelRect.anchorMax = new Vector2(0.5f, 0);
            panelRect.pivot = new Vector2(0.5f, 0);
            panelRect.anchoredPosition = new Vector2(0, 70);
            panelRect.sizeDelta = new Vector2(totalWidth, BUTTON_HEIGHT + SPACING * 2);

            var panelImage = _panel.AddComponent<Image>();
            panelImage.color = new Color(0.1f, 0.1f, 0.1f, 0.8f);

            _buttons = new List<BuildingButton>();

            for (int i = 0; i < visibleDefs.Count; i++)
            {
                var def = visibleDefs[i];
                int index = i;

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

                var labelObj = new GameObject("Label");
                labelObj.transform.SetParent(btnObj.transform, false);
                var labelRect = labelObj.AddComponent<RectTransform>();
                labelRect.anchorMin = Vector2.zero;
                labelRect.anchorMax = Vector2.one;
                labelRect.sizeDelta = Vector2.zero;
                labelRect.offsetMin = new Vector2(4, 4);
                labelRect.offsetMax = new Vector2(-4, -4);

                var label = labelObj.AddComponent<Text>();
                label.font = UnityEngine.Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                label.fontSize = FONT_SIZE;
                label.color = Color.white;
                label.alignment = TextAnchor.MiddleCenter;

                string costText = FormatCostText(def);
                label.text = costText;

                _buttons.Add(new BuildingButton
                {
                    Root = btnObj,
                    Button = btn,
                    Label = label,
                    Background = bg,
                    Definition = def
                });
            }

            _panel.SetActive(false);
            _panelDirty = false;
        }

        private static bool HasAllCapabilities(DiscoveryStateManager sm, string[] capabilities)
        {
            foreach (var cap in capabilities)
            {
                if (!sm.HasCapability(cap))
                    return false;
            }
            return true;
        }

        private void RefreshButtons()
        {
            if (_buttons == null) return;

            var rm = ResourceManager.Instance;
            if (rm == null) return;

            foreach (var btn in _buttons)
            {
                bool canAfford = rm.CanAfford(
                    btn.Definition.WoodCost, btn.Definition.StoneCost, btn.Definition.FiberCost);
                btn.Button.interactable = canAfford;
                btn.Background.color = canAfford
                    ? new Color(0.25f, 0.25f, 0.25f, 0.9f)
                    : new Color(0.15f, 0.10f, 0.10f, 0.9f);
                btn.Label.color = canAfford ? Color.white : new Color(0.5f, 0.3f, 0.3f);
            }
        }

        private static string FormatCostText(BuildingDefinition def)
        {
            var parts = new System.Collections.Generic.List<string>();
            if (def.WoodCost > 0) parts.Add($"{def.WoodCost}W");
            if (def.StoneCost > 0) parts.Add($"{def.StoneCost}S");
            if (def.FiberCost > 0) parts.Add($"{def.FiberCost}F");

            string costs = parts.Count > 0 ? string.Join("  ", parts) : "Free";
            return $"{def.DisplayName}\n{costs}";
        }

        private void OnBuildingSelected(int index)
        {
            if (_buttons == null || index >= _buttons.Count) return;

            var def = _buttons[index].Definition;
            var rm = ResourceManager.Instance;
            if (rm != null && !rm.CanAfford(def.WoodCost, def.StoneCost, def.FiberCost))
                return;

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
            EventBus.Unsubscribe<DiscoveryMadeEvent>(OnDiscoveryMade);
        }

        private void OnDestroy()
        {
            EventBus.Unsubscribe<ResourceChangedEvent>(OnResourceChanged);
            EventBus.Unsubscribe<DiscoveryMadeEvent>(OnDiscoveryMade);
        }
    }
}
