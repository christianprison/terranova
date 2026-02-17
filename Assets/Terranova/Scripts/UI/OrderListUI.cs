using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Terranova.Core;
using Terranova.Orders;

namespace Terranova.UI
{
    /// <summary>
    /// Feature 7.6 (v0.4.13): Active-orders list panel.
    ///
    /// Scrollable list of all active orders. Each row shows:
    ///   status icon + sentence + pause (44 px) + delete (44 px)
    /// </summary>
    public class OrderListUI : MonoBehaviour
    {
        public static OrderListUI Instance { get; private set; }

        private const float PANEL_WIDTH = 520f;
        private const float PANEL_HEIGHT = 480f;
        private const float ROW_HEIGHT = 56f;
        private const float TOUCH_SIZE = 44f;

        private static readonly Color BG_COLOR = new(0.08f, 0.10f, 0.08f, 0.95f);
        private static readonly Color ROW_BG = new(0.14f, 0.16f, 0.14f, 0.85f);
        private static readonly Color STATUS_ACTIVE = new(1f, 0.85f, 0.2f);
        private static readonly Color STATUS_PAUSED = new(0.6f, 0.6f, 0.6f);
        private static readonly Color NEGATED_TEXT = new(0.9f, 0.3f, 0.3f);

        private GameObject _panel;
        private Transform _listContent;
        private bool _isOpen;
        private bool _dirty;

        // ─── Lifecycle ───────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void OnEnable()
        {
            EventBus.Subscribe<OrderCreatedEvent>(OnOrderChanged);
            EventBus.Subscribe<OrderStatusChangedEvent>(OnStatusChanged);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<OrderCreatedEvent>(OnOrderChanged);
            EventBus.Unsubscribe<OrderStatusChangedEvent>(OnStatusChanged);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            if (_isOpen && _dirty)
            {
                _dirty = false;
                RebuildList();
            }

            if (_isOpen && UnityEngine.Input.GetKeyDown(KeyCode.Escape))
                Close();
        }

        private void OnOrderChanged(OrderCreatedEvent _) { _dirty = true; }
        private void OnStatusChanged(OrderStatusChangedEvent _) { _dirty = true; }

        // ─── Open / Close ────────────────────────────────────

        public void Toggle()
        {
            if (_isOpen) Close();
            else Open();
        }

        public void Open()
        {
            if (_isOpen) return;
            _isOpen = true;
            BuildPanel();
        }

        public void Close()
        {
            if (_panel != null) Destroy(_panel);
            _panel = null;
            _isOpen = false;
        }

        public bool IsOpen => _isOpen;

        // ─── Panel Construction ──────────────────────────────

        private void BuildPanel()
        {
            if (_panel != null) Destroy(_panel);

            // Overlay
            _panel = new GameObject("OrderListPanel");
            _panel.transform.SetParent(transform, false);
            _panel.transform.SetAsLastSibling();
            var overlay = _panel.AddComponent<Image>();
            overlay.color = new Color(0f, 0f, 0f, 0.5f);
            var overlayRect = _panel.GetComponent<RectTransform>();
            overlayRect.anchorMin = Vector2.zero;
            overlayRect.anchorMax = Vector2.one;
            overlayRect.offsetMin = Vector2.zero;
            overlayRect.offsetMax = Vector2.zero;
            _panel.AddComponent<Button>().onClick.AddListener(Close);

            // Card
            var card = MakeRect(_panel.transform, "Card", Vector2.zero,
                new Vector2(PANEL_WIDTH, PANEL_HEIGHT));
            card.AddComponent<Image>().color = BG_COLOR;
            card.AddComponent<Button>().onClick.AddListener(() => { }); // block click-through

            // Title
            var titleObj = MakeRect(card.transform, "Title",
                new Vector2(0, PANEL_HEIGHT / 2 - 24),
                new Vector2(PANEL_WIDTH - TOUCH_SIZE - 16, 40));
            var titleText = titleObj.AddComponent<Text>();
            titleText.font = GetFont();
            titleText.fontSize = 22;
            titleText.color = new Color(0.8f, 0.9f, 0.7f);
            titleText.alignment = TextAnchor.MiddleCenter;
            titleText.fontStyle = FontStyle.Bold;
            titleText.text = "ACTIVE ORDERS";

            // Close [X] — 44 × 44 px
            var closeX = MakeRect(card.transform, "CloseX",
                new Vector2(PANEL_WIDTH / 2 - TOUCH_SIZE / 2 - 4, PANEL_HEIGHT / 2 - TOUCH_SIZE / 2 - 2),
                new Vector2(TOUCH_SIZE, TOUCH_SIZE));
            closeX.AddComponent<Image>().color = new Color(0.5f, 0.2f, 0.2f, 0.8f);
            closeX.AddComponent<Button>().onClick.AddListener(Close);
            var closeLabel = MakeRect(closeX.transform, "X", Vector2.zero,
                new Vector2(TOUCH_SIZE, TOUCH_SIZE));
            var closeTxt = closeLabel.AddComponent<Text>();
            closeTxt.font = GetFont();
            closeTxt.fontSize = 22;
            closeTxt.color = Color.white;
            closeTxt.alignment = TextAnchor.MiddleCenter;
            closeTxt.fontStyle = FontStyle.Bold;
            closeTxt.text = "X";

            // Scroll area
            float scrollHeight = PANEL_HEIGHT - 70;
            var scrollBg = MakeRect(card.transform, "ScrollBg",
                new Vector2(0, -20), new Vector2(PANEL_WIDTH - 20, scrollHeight));
            scrollBg.AddComponent<Image>().color = new Color(0.06f, 0.08f, 0.06f, 0.6f);
            scrollBg.AddComponent<RectMask2D>();

            var scrollRect = scrollBg.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.scrollSensitivity = 30f;

            var content = new GameObject("Content");
            content.transform.SetParent(scrollBg.transform, false);
            var contentRect = content.AddComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0, 1);
            contentRect.anchorMax = new Vector2(1, 1);
            contentRect.pivot = new Vector2(0.5f, 1);
            contentRect.anchoredPosition = Vector2.zero;

            var layout = content.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 4;
            layout.padding = new RectOffset(8, 8, 8, 8);
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            content.AddComponent<ContentSizeFitter>().verticalFit =
                ContentSizeFitter.FitMode.PreferredSize;

            scrollRect.content = contentRect;
            _listContent = content.transform;

            RebuildList();
        }

        private void RebuildList()
        {
            if (_listContent == null) return;

            for (int i = _listContent.childCount - 1; i >= 0; i--)
                Destroy(_listContent.GetChild(i).gameObject);

            var manager = OrderManager.Instance;
            if (manager == null) return;

            var orders = manager.AllOrders;
            bool hasVisibleOrders = false;

            foreach (var order in orders)
            {
                if (order.Status == OrderStatus.Complete || order.Status == OrderStatus.Failed)
                    continue;
                hasVisibleOrders = true;
                CreateOrderRow(order);
            }

            if (!hasVisibleOrders)
            {
                var emptyObj = new GameObject("Empty");
                emptyObj.transform.SetParent(_listContent, false);
                emptyObj.AddComponent<LayoutElement>().preferredHeight = 60;
                emptyObj.AddComponent<RectTransform>();
                var emptyText = emptyObj.AddComponent<Text>();
                emptyText.font = GetFont();
                emptyText.fontSize = 16;
                emptyText.color = new Color(0.5f, 0.5f, 0.5f);
                emptyText.alignment = TextAnchor.MiddleCenter;
                emptyText.text = "No active orders.";
                emptyText.horizontalOverflow = HorizontalWrapMode.Wrap;
            }
        }

        private void CreateOrderRow(OrderDefinition order)
        {
            var row = new GameObject($"Order_{order.Id}");
            row.transform.SetParent(_listContent, false);
            row.AddComponent<LayoutElement>().preferredHeight = ROW_HEIGHT;
            row.AddComponent<Image>().color = ROW_BG;

            // Status icon
            string icon = order.Status == OrderStatus.Active ? ">>" : "||";
            Color iconColor = order.Status == OrderStatus.Active ? STATUS_ACTIVE : STATUS_PAUSED;

            var iconObj = new GameObject("StatusIcon");
            iconObj.transform.SetParent(row.transform, false);
            var iconRect = iconObj.AddComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0, 0);
            iconRect.anchorMax = new Vector2(0, 1);
            iconRect.pivot = new Vector2(0, 0.5f);
            iconRect.anchoredPosition = new Vector2(6, 0);
            iconRect.sizeDelta = new Vector2(30, 0);
            var iconText = iconObj.AddComponent<Text>();
            iconText.font = GetFont();
            iconText.fontSize = 14;
            iconText.color = iconColor;
            iconText.alignment = TextAnchor.MiddleCenter;
            iconText.fontStyle = FontStyle.Bold;
            iconText.text = icon;

            // Sentence text (fills middle area, leaving room for 2 buttons on right)
            float buttonsWidth = TOUCH_SIZE * 2 + 8;
            var sentenceObj = new GameObject("Sentence");
            sentenceObj.transform.SetParent(row.transform, false);
            var sentenceRect = sentenceObj.AddComponent<RectTransform>();
            sentenceRect.anchorMin = new Vector2(0, 0);
            sentenceRect.anchorMax = new Vector2(1, 1);
            sentenceRect.offsetMin = new Vector2(38, 2);
            sentenceRect.offsetMax = new Vector2(-buttonsWidth - 4, -2);
            var sentenceText = sentenceObj.AddComponent<Text>();
            sentenceText.font = GetFont();
            sentenceText.fontSize = 14;
            sentenceText.color = order.Negated ? NEGATED_TEXT : Color.white;
            sentenceText.alignment = TextAnchor.MiddleLeft;
            sentenceText.horizontalOverflow = HorizontalWrapMode.Wrap;
            sentenceText.text = order.BuildSentence();

            int orderId = order.Id;

            // Pause button — 44 × 44
            var pauseObj = new GameObject("PauseBtn");
            pauseObj.transform.SetParent(row.transform, false);
            var pauseRect = pauseObj.AddComponent<RectTransform>();
            pauseRect.anchorMin = new Vector2(1, 0.5f);
            pauseRect.anchorMax = new Vector2(1, 0.5f);
            pauseRect.pivot = new Vector2(1, 0.5f);
            pauseRect.anchoredPosition = new Vector2(-TOUCH_SIZE - 4, 0);
            pauseRect.sizeDelta = new Vector2(TOUCH_SIZE, TOUCH_SIZE);
            pauseObj.AddComponent<Image>().color = new Color(0.3f, 0.3f, 0.5f, 0.8f);
            pauseObj.AddComponent<Button>().onClick.AddListener(() =>
            {
                OrderManager.Instance?.TogglePause(orderId);
                _dirty = true;
            });
            var pauseLabel = new GameObject("PL");
            pauseLabel.transform.SetParent(pauseObj.transform, false);
            var plRect = pauseLabel.AddComponent<RectTransform>();
            plRect.anchorMin = Vector2.zero;
            plRect.anchorMax = Vector2.one;
            plRect.sizeDelta = Vector2.zero;
            var plText = pauseLabel.AddComponent<Text>();
            plText.font = GetFont();
            plText.fontSize = 16;
            plText.color = Color.white;
            plText.alignment = TextAnchor.MiddleCenter;
            plText.fontStyle = FontStyle.Bold;
            plText.text = order.Status == OrderStatus.Paused ? ">" : "||";

            // Cancel button — 44 × 44 (red X, cancels order, removes marker, frees settlers)
            var cancelObj = new GameObject("CancelBtn");
            cancelObj.transform.SetParent(row.transform, false);
            var cancelRect = cancelObj.AddComponent<RectTransform>();
            cancelRect.anchorMin = new Vector2(1, 0.5f);
            cancelRect.anchorMax = new Vector2(1, 0.5f);
            cancelRect.pivot = new Vector2(1, 0.5f);
            cancelRect.anchoredPosition = new Vector2(-2, 0);
            cancelRect.sizeDelta = new Vector2(TOUCH_SIZE, TOUCH_SIZE);
            var cancelImg = cancelObj.AddComponent<Image>();
            cancelImg.color = new Color(0.7f, 0.1f, 0.1f, 0.95f);
            cancelObj.AddComponent<Button>().onClick.AddListener(() =>
            {
                OrderManager.Instance?.CancelOrder(orderId);
                _dirty = true;
            });
            var cancelLabel = new GameObject("CL");
            cancelLabel.transform.SetParent(cancelObj.transform, false);
            var clRect = cancelLabel.AddComponent<RectTransform>();
            clRect.anchorMin = Vector2.zero;
            clRect.anchorMax = Vector2.one;
            clRect.sizeDelta = Vector2.zero;
            var clText = cancelLabel.AddComponent<Text>();
            clText.font = GetFont();
            clText.fontSize = 20;
            clText.color = Color.white;
            clText.alignment = TextAnchor.MiddleCenter;
            clText.fontStyle = FontStyle.Bold;
            clText.text = "X";
        }

        // ─── Helpers ─────────────────────────────────────────

        private static Font GetFont()
        {
            return UnityEngine.Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        private static GameObject MakeRect(Transform parent, string name,
            Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var r = go.AddComponent<RectTransform>();
            r.anchorMin = new Vector2(0.5f, 0.5f);
            r.anchorMax = new Vector2(0.5f, 0.5f);
            r.pivot = new Vector2(0.5f, 0.5f);
            r.anchoredPosition = pos;
            r.sizeDelta = size;
            return go;
        }
    }
}
