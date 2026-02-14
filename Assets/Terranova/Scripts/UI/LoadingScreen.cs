using UnityEngine;
using UnityEngine.UI;
using Terranova.Core;

namespace Terranova.UI
{
    /// <summary>
    /// Full-screen loading screen with progress bar shown during terrain generation.
    /// Subscribes to WorldGenerationProgressEvent and auto-destroys when complete.
    /// </summary>
    public class LoadingScreen : MonoBehaviour
    {
        private Image _progressFill;
        private Text _statusText;
        private GameObject _panel;

        private void OnEnable()
        {
            EventBus.Subscribe<WorldGenerationProgressEvent>(OnProgress);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<WorldGenerationProgressEvent>(OnProgress);
        }

        private void Start()
        {
            CreateLoadingUI();
        }

        private void CreateLoadingUI()
        {
            // Full-screen dark panel
            _panel = new GameObject("LoadingPanel");
            _panel.transform.SetParent(transform, false);
            var panelImage = _panel.AddComponent<Image>();
            panelImage.color = new Color(0.08f, 0.10f, 0.12f, 1f);
            var panelRect = _panel.GetComponent<RectTransform>();
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;

            // Title text
            var titleObj = new GameObject("Title");
            titleObj.transform.SetParent(_panel.transform, false);
            var titleRect = titleObj.AddComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0.5f, 0.5f);
            titleRect.anchorMax = new Vector2(0.5f, 0.5f);
            titleRect.pivot = new Vector2(0.5f, 0.5f);
            titleRect.anchoredPosition = new Vector2(0, 60);
            titleRect.sizeDelta = new Vector2(400, 60);
            var titleText = titleObj.AddComponent<Text>();
            titleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            titleText.fontSize = 42;
            titleText.color = new Color(0.9f, 0.85f, 0.7f);
            titleText.alignment = TextAnchor.MiddleCenter;
            titleText.fontStyle = FontStyle.Bold;
            titleText.text = "TERRANOVA";

            // Progress bar background
            var barBgObj = new GameObject("ProgressBarBg");
            barBgObj.transform.SetParent(_panel.transform, false);
            var barBgImage = barBgObj.AddComponent<Image>();
            barBgImage.color = new Color(0.2f, 0.2f, 0.2f, 1f);
            var barBgRect = barBgObj.GetComponent<RectTransform>();
            barBgRect.anchorMin = new Vector2(0.5f, 0.5f);
            barBgRect.anchorMax = new Vector2(0.5f, 0.5f);
            barBgRect.pivot = new Vector2(0.5f, 0.5f);
            barBgRect.anchoredPosition = new Vector2(0, -10);
            barBgRect.sizeDelta = new Vector2(400, 20);

            // Progress bar fill
            var barFillObj = new GameObject("ProgressBarFill");
            barFillObj.transform.SetParent(barBgObj.transform, false);
            _progressFill = barFillObj.AddComponent<Image>();
            _progressFill.color = new Color(0.4f, 0.7f, 0.3f, 1f);
            var barFillRect = barFillObj.GetComponent<RectTransform>();
            barFillRect.anchorMin = Vector2.zero;
            barFillRect.anchorMax = new Vector2(0, 1);
            barFillRect.pivot = new Vector2(0, 0.5f);
            barFillRect.offsetMin = Vector2.zero;
            barFillRect.offsetMax = Vector2.zero;

            // Status text
            var statusObj = new GameObject("StatusText");
            statusObj.transform.SetParent(_panel.transform, false);
            var statusRect = statusObj.AddComponent<RectTransform>();
            statusRect.anchorMin = new Vector2(0.5f, 0.5f);
            statusRect.anchorMax = new Vector2(0.5f, 0.5f);
            statusRect.pivot = new Vector2(0.5f, 0.5f);
            statusRect.anchoredPosition = new Vector2(0, -40);
            statusRect.sizeDelta = new Vector2(400, 30);
            _statusText = statusObj.AddComponent<Text>();
            _statusText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _statusText.fontSize = 18;
            _statusText.color = new Color(0.7f, 0.7f, 0.7f);
            _statusText.alignment = TextAnchor.MiddleCenter;
            _statusText.text = "Loading...";
        }

        private void OnProgress(WorldGenerationProgressEvent evt)
        {
            if (_progressFill != null)
            {
                var rect = _progressFill.GetComponent<RectTransform>();
                rect.anchorMax = new Vector2(evt.Progress, 1);
            }

            if (_statusText != null)
                _statusText.text = evt.Status;

            // Destroy loading screen when generation completes
            if (evt.Progress >= 1f)
            {
                Destroy(_panel, 0.3f);
                Destroy(this, 0.3f);
            }
        }
    }
}
