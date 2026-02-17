using UnityEngine;
using UnityEngine.UI;
using Terranova.Core;

namespace Terranova.UI
{
    /// <summary>
    /// Main Menu scene UI. Displayed before the game starts.
    /// MS4 Feature 1.1: Main Menu.
    /// - New Game button with seed field and biome selector
    /// - Continue button (placeholder)
    /// </summary>
    public class MainMenuUI : MonoBehaviour
    {
        private InputField _seedInput;
        private Text _seedDisplay;
        private BiomeType _selectedBiome = BiomeType.Forest;
        private Button[] _biomeButtons;
        private Text _titleText;
        private Text _versionText;
        private Text _debugLogText;
        private string _debugLog = "";

        private void Start()
        {
            CreateUI();
            // Generate a random seed
            int randomSeed = Random.Range(10000, 99999);
            _seedInput.text = randomSeed.ToString();
            GameState.Seed = randomSeed;
        }

        private void CreateUI()
        {
            // Canvas setup
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 200;

            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            gameObject.AddComponent<GraphicRaycaster>();

            // Background
            var bgObj = new GameObject("Background");
            bgObj.transform.SetParent(transform, false);
            var bgImage = bgObj.AddComponent<Image>();
            bgImage.color = new Color(0.12f, 0.15f, 0.10f, 1f);
            var bgRect = bgObj.GetComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;

            // Title
            _titleText = CreateText("TERRANOVA", 64, Color.white, new Vector2(0, 200), transform);
            _titleText.fontStyle = FontStyle.Bold;

            // Subtitle
            CreateText("Deep Epoch I.1", 28, new Color(0.7f, 0.8f, 0.6f), new Vector2(0, 140), transform);

            // Seed label + input
            CreateText("World Seed:", 22, Color.white, new Vector2(-100, 60), transform);

            var seedInputGo = new GameObject("SeedInput");
            seedInputGo.transform.SetParent(transform, false);
            var seedRect = seedInputGo.AddComponent<RectTransform>();
            seedRect.anchorMin = new Vector2(0.5f, 0.5f);
            seedRect.anchorMax = new Vector2(0.5f, 0.5f);
            seedRect.pivot = new Vector2(0.5f, 0.5f);
            seedRect.anchoredPosition = new Vector2(80, 60);
            seedRect.sizeDelta = new Vector2(200, 40);

            var seedBg = seedInputGo.AddComponent<Image>();
            seedBg.color = new Color(0.2f, 0.25f, 0.18f, 1f);

            _seedInput = seedInputGo.AddComponent<InputField>();
            _seedInput.contentType = InputField.ContentType.IntegerNumber;

            var seedTextGo = new GameObject("Text");
            seedTextGo.transform.SetParent(seedInputGo.transform, false);
            var seedTextRect = seedTextGo.AddComponent<RectTransform>();
            seedTextRect.anchorMin = Vector2.zero;
            seedTextRect.anchorMax = Vector2.one;
            seedTextRect.offsetMin = new Vector2(8, 0);
            seedTextRect.offsetMax = new Vector2(-8, 0);
            var seedText = seedTextGo.AddComponent<Text>();
            seedText.font = UnityEngine.Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            seedText.fontSize = 24;
            seedText.color = Color.white;
            seedText.alignment = TextAnchor.MiddleLeft;
            _seedInput.textComponent = seedText;

            _seedInput.onValueChanged.AddListener(OnSeedChanged);

            // Biome selector label
            CreateText("Biome:", 22, Color.white, new Vector2(0, 10), transform);

            // Biome buttons
            _biomeButtons = new Button[3];
            string[] biomeNames = { "Forest", "Mountains", "Coast" };
            Color[] biomeColors = {
                new Color(0.2f, 0.5f, 0.2f, 0.8f),
                new Color(0.4f, 0.4f, 0.45f, 0.8f),
                new Color(0.2f, 0.4f, 0.6f, 0.8f)
            };

            for (int i = 0; i < 3; i++)
            {
                int biomeIdx = i;
                var btnGo = new GameObject($"Biome_{biomeNames[i]}");
                btnGo.transform.SetParent(transform, false);
                var btnRect = btnGo.AddComponent<RectTransform>();
                btnRect.anchorMin = new Vector2(0.5f, 0.5f);
                btnRect.anchorMax = new Vector2(0.5f, 0.5f);
                btnRect.pivot = new Vector2(0.5f, 0.5f);
                btnRect.anchoredPosition = new Vector2(-150 + i * 150, -40);
                btnRect.sizeDelta = new Vector2(130, 50);

                var img = btnGo.AddComponent<Image>();
                img.color = biomeColors[i];

                var btn = btnGo.AddComponent<Button>();
                btn.targetGraphic = img;
                btn.onClick.AddListener(() => SelectBiome(biomeIdx));
                _biomeButtons[i] = btn;

                var labelGo = new GameObject("Label");
                labelGo.transform.SetParent(btnGo.transform, false);
                var labelRect = labelGo.AddComponent<RectTransform>();
                labelRect.anchorMin = Vector2.zero;
                labelRect.anchorMax = Vector2.one;
                labelRect.sizeDelta = Vector2.zero;
                var label = labelGo.AddComponent<Text>();
                label.font = UnityEngine.Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                label.fontSize = 22;
                label.color = Color.white;
                label.alignment = TextAnchor.MiddleCenter;
                label.fontStyle = FontStyle.Bold;
                label.text = biomeNames[i];
            }

            UpdateBiomeButtons();

            // New Game button
            CreateButton("New Game", new Vector2(0, -120), new Vector2(220, 60),
                new Color(0.25f, 0.55f, 0.25f, 0.95f), 30, StartNewGame);

            // Continue button (placeholder)
            CreateButton("Continue", new Vector2(0, -200), new Vector2(220, 50),
                new Color(0.3f, 0.3f, 0.35f, 0.7f), 24, ContinueGame);

            // Version
            _versionText = CreateText("v0.4.11", 18, new Color(0.5f, 0.5f, 0.5f),
                new Vector2(0, -300), transform);

            // Debug log overlay – bottom-left, captures all Debug.Log output
            var debugGo = new GameObject("DebugLog");
            debugGo.transform.SetParent(transform, false);
            var debugRect = debugGo.AddComponent<RectTransform>();
            debugRect.anchorMin = new Vector2(0, 0);
            debugRect.anchorMax = new Vector2(1, 0);
            debugRect.pivot = new Vector2(0, 0);
            debugRect.anchoredPosition = new Vector2(8, 8);
            debugRect.sizeDelta = new Vector2(-16, 180);
            _debugLogText = debugGo.AddComponent<Text>();
            _debugLogText.font = UnityEngine.Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _debugLogText.fontSize = 12;
            _debugLogText.color = new Color(0f, 1f, 0.4f, 0.9f);
            _debugLogText.alignment = TextAnchor.LowerLeft;
            _debugLogText.horizontalOverflow = HorizontalWrapMode.Wrap;
            _debugLogText.verticalOverflow = VerticalWrapMode.Truncate;
            AppendDebug($"Menu ready. GameStarted={GameState.GameStarted}, scene={UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}");
        }

        private void OnEnable()
        {
            Application.logMessageReceived += OnLogMessage;
        }

        private void OnDisable()
        {
            Application.logMessageReceived -= OnLogMessage;
        }

        private void OnLogMessage(string message, string stackTrace, LogType type)
        {
            string prefix = type == LogType.Error || type == LogType.Exception ? "[ERR] " :
                            type == LogType.Warning ? "[WARN] " : "";
            AppendDebug($"{prefix}{message}");
        }

        private void AppendDebug(string msg)
        {
            _debugLog += msg + "\n";
            // Keep last 12 lines
            var lines = _debugLog.Split('\n');
            if (lines.Length > 13)
                _debugLog = string.Join("\n", lines, lines.Length - 13, 13);
            if (_debugLogText != null)
                _debugLogText.text = _debugLog;
        }

        private void OnSeedChanged(string value)
        {
            if (int.TryParse(value, out int seed))
                GameState.Seed = seed;
        }

        private void SelectBiome(int index)
        {
            _selectedBiome = (BiomeType)index;
            GameState.SelectedBiome = _selectedBiome;
            UpdateBiomeButtons();
        }

        private void UpdateBiomeButtons()
        {
            for (int i = 0; i < _biomeButtons.Length; i++)
            {
                var img = _biomeButtons[i].GetComponent<Image>();
                bool selected = i == (int)_selectedBiome;
                Color c = img.color;
                c.a = selected ? 1f : 0.4f;
                img.color = c;

                // Add border effect for selected
                var outline = _biomeButtons[i].GetComponent<Outline>();
                if (selected && outline == null)
                {
                    outline = _biomeButtons[i].gameObject.AddComponent<Outline>();
                    outline.effectColor = Color.white;
                    outline.effectDistance = new Vector2(2, -2);
                }
                else if (!selected && outline != null)
                {
                    Destroy(outline);
                }
            }
        }

        private void StartNewGame()
        {
            GameState.IsNewGame = true;
            GameState.DayCount = 1;
            GameState.GameTimeSeconds = 0f;
            GameState.GameStarted = true;

            if (int.TryParse(_seedInput.text, out int seed))
                GameState.Seed = seed;

            Debug.Log($"MainMenuUI: StartNewGame – seed={GameState.Seed}, biome={GameState.SelectedBiome}");
            LaunchGame();
        }

        private void ContinueGame()
        {
            // Placeholder: just start a new game
            GameState.IsNewGame = false;
            GameState.GameStarted = true;

            Debug.Log("MainMenuUI: ContinueGame");
            LaunchGame();
        }

        private void LaunchGame()
        {
            // Destroy the menu and bootstrap game systems directly.
            // No scene reload needed – we're already in SampleScene.
            // Uses callback registered by GameBootstrapper (avoids circular
            // assembly reference between Terranova.UI and Terranova.Bootstrap).
            Destroy(gameObject);
            GameState.LaunchGameCallback?.Invoke();
        }

        private Text CreateText(string content, int fontSize, Color color, Vector2 position, Transform parent)
        {
            var go = new GameObject(content);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(600, fontSize + 20);
            var text = go.AddComponent<Text>();
            text.font = UnityEngine.Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = TextAnchor.MiddleCenter;
            var shadow = go.AddComponent<Shadow>();
            shadow.effectColor = new Color(0, 0, 0, 0.6f);
            shadow.effectDistance = new Vector2(1, -1);
            return text;
        }

        private void CreateButton(string label, Vector2 pos, Vector2 size, Color bgColor,
            int fontSize, UnityEngine.Events.UnityAction onClick)
        {
            var btnGo = new GameObject($"Btn_{label}");
            btnGo.transform.SetParent(transform, false);
            var btnRect = btnGo.AddComponent<RectTransform>();
            btnRect.anchorMin = new Vector2(0.5f, 0.5f);
            btnRect.anchorMax = new Vector2(0.5f, 0.5f);
            btnRect.pivot = new Vector2(0.5f, 0.5f);
            btnRect.anchoredPosition = pos;
            btnRect.sizeDelta = size;

            var img = btnGo.AddComponent<Image>();
            img.color = bgColor;

            var btn = btnGo.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(onClick);

            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(btnGo.transform, false);
            var labelRect = labelGo.AddComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.sizeDelta = Vector2.zero;
            var text = labelGo.AddComponent<Text>();
            text.font = UnityEngine.Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleCenter;
            text.fontStyle = FontStyle.Bold;
            text.text = label;
        }
    }
}
