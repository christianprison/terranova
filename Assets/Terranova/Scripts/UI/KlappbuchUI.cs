using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Terranova.Core;
using Terranova.Orders;
using Terranova.Population;

namespace Terranova.UI
{
    /// <summary>
    /// Feature 7.3 (v0.4.15): Klappbuch UI — iOS-style scroll-picker.
    ///
    /// Three columns (WHO / DOES / WHAT-WHERE) where the user scrolls each
    /// column and the center item is the current selection (like UIPickerView).
    ///
    /// Layout: 80 % of screen width, columns WHO 25 % / DOES 35 % / WHAT 40 %.
    /// Pauses game and disables camera input while open.
    /// </summary>
    public class KlappbuchUI : MonoBehaviour
    {
        public static KlappbuchUI Instance { get; private set; }

        // ─── Fixed Layout Constants ─────────────────────────────
        private const float ROW_HEIGHT = 44f;
        private const float ROW_HEIGHT_LOCKED = 56f;
        private const float SPACING = 2f;
        private const float COL_PAD = 5f;
        private const float TITLE_H = 40f;
        private const float RESULT_H = 56f;
        private const float BTN_H = 50f;
        private const float CLOSE_SIZE = 44f;
        private const int FONT_MIN = 14;
        private const float SNAP_THRESHOLD = 80f;
        private const float SNAP_DURATION = 0.2f;

        // ─── Colors ─────────────────────────────────────────────
        private static readonly Color BG = new(0.08f, 0.10f, 0.08f, 0.95f);
        private static readonly Color COL_BG = new(0.12f, 0.14f, 0.12f, 0.9f);
        private static readonly Color BAND_COLOR = new(0.3f, 0.55f, 0.35f, 0.35f);
        private static readonly Color ROW_N = new(0.16f, 0.20f, 0.16f, 0.8f);
        private static readonly Color ROW_LOCK = new(0.12f, 0.12f, 0.12f, 0.6f);
        private static readonly Color ROW_BUSY = new(0.14f, 0.14f, 0.14f, 0.5f);
        private static readonly Color TXT_N = Color.white;
        private static readonly Color TXT_L = new(0.5f, 0.5f, 0.5f);
        private static readonly Color TXT_B = new(0.6f, 0.6f, 0.6f);
        private static readonly Color NEG_C = new(0.9f, 0.25f, 0.25f);
        private static readonly Color VALID_C = new(0.3f, 0.9f, 0.4f);
        private static readonly Color INVALID_C = new(1f, 0.6f, 0.2f);
        private static readonly Color CONFIRM_ON = new(0.2f, 0.55f, 0.3f, 0.95f);
        private static readonly Color CONFIRM_OFF = new(0.2f, 0.2f, 0.2f, 0.5f);

        // ─── State ──────────────────────────────────────────────
        private GameObject _panel;
        private bool _isOpen;
        private float _savedTimeScale;
        private bool _isNegated;
        private Vector3? _tapPosition;
        private float _canvasW, _canvasH;

        // Computed layout
        private float _panelW, _panelH, _whoColW, _doesColW, _whatColW, _colH;

        // Picker scroll rects and content rects
        private ScrollRect _whoScroll, _doesScroll, _whatScroll;
        private RectTransform _whoContentRect, _doesContentRect, _whatContentRect;

        // Item data
        private readonly List<WhoItem> _whoItems = new();
        private readonly List<DoesItem> _doesItems = new();
        private readonly List<WhatItem> _whatItems = new();

        // Selected center indices
        private int _whoIdx, _doesIdx, _whatIdx;
        private int _prevDoesIdx = -1;

        // UI refs
        private Text _resultText;
        private Image _confirmBg;
        private Button _confirmBtn;
        private Image _negateImg;
        private Text _activeOrdersText;

        // ─── Data structs ───────────────────────────────────────
        private struct WhoItem
        {
            public OrderSubject Subject;
            public string SettlerName;
            public bool IsBusy;
            public string DisplayLabel;
            public string Subtitle;
        }

        private struct DoesItem
        {
            public OrderPredicate Predicate;
            public bool IsLocked;
            public string RequiredDiscovery;
        }

        private struct WhatItem
        {
            public OrderObject Object;
        }

        // ─── Lifecycle ──────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void OnEnable()
        {
            EventBus.Subscribe<OpenKlappbuchEvent>(OnOpenRequest);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<OpenKlappbuchEvent>(OnOpenRequest);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            if (!_isOpen) return;

            if (UnityEngine.Input.GetKeyDown(KeyCode.Escape))
            {
                Close();
                return;
            }

            // Snap each picker column to nearest valid item
            SnapColumn(_whoScroll, _whoContentRect, _whoItems.Count, ROW_HEIGHT,
                ref _whoIdx, null);
            SnapDoesColumn();
            SnapColumn(_whatScroll, _whatContentRect, _whatItems.Count, ROW_HEIGHT,
                ref _whatIdx, null);

            // Check if DOES selection changed → rebuild WHAT
            if (_doesIdx != _prevDoesIdx)
            {
                _prevDoesIdx = _doesIdx;
                RebuildWhatColumn();
            }

            UpdateResultLine();
        }

        // ─── Open / Close ───────────────────────────────────────

        private void OnOpenRequest(OpenKlappbuchEvent evt) => Open(evt);

        public void Open(OpenKlappbuchEvent context = default)
        {
            if (_isOpen) Close();

            // Pause game and disable camera
            // Use tiny timeScale (not 0) so ScrollRect inertia/snap works
            _savedTimeScale = Time.timeScale;
            Time.timeScale = 0.0001f;
            Terranova.Camera.RTSCameraController.InputDisabled = true;

            // Reset state
            _isNegated = false;
            _tapPosition = context.TapPosition;
            _whoIdx = 0;
            _doesIdx = 0;
            _whatIdx = 0;
            _prevDoesIdx = -1;

            BuildPanel(context);
            _isOpen = true;
        }

        public void Close()
        {
            // Restore game state
            Time.timeScale = _savedTimeScale;
            Terranova.Camera.RTSCameraController.InputDisabled = false;

            if (_panel != null) Destroy(_panel);
            _panel = null;
            _isOpen = false;
            _whoItems.Clear();
            _doesItems.Clear();
            _whatItems.Clear();
        }

        public bool IsOpen => _isOpen;

        // ─── Panel Construction ─────────────────────────────────

        private void BuildPanel(OpenKlappbuchEvent context)
        {
            // Get canvas-space dimensions (NOT Screen pixels — CanvasScaler changes the coordinate space)
            var canvasRT = transform as RectTransform;
            _canvasW = canvasRT != null ? canvasRT.rect.width : Screen.width;
            _canvasH = canvasRT != null ? canvasRT.rect.height : Screen.height;

            // Compute layout: exactly 80 % of canvas width, centered
            _panelW = _canvasW * 0.8f;
            _panelH = Mathf.Min(_canvasH * 0.85f, 600f);
            float usableW = _panelW - COL_PAD * 4f;
            _whoColW = usableW * 0.25f;
            _doesColW = usableW * 0.35f;
            _whatColW = usableW * 0.40f;
            _colH = _panelH - TITLE_H - RESULT_H - BTN_H - 24f;

            Debug.Log($"[Klappbuch] Canvas={_canvasW}x{_canvasH} Panel={_panelW}x{_panelH} (80% of {_canvasW})");

            // Full-screen overlay (transparent — only catches taps to close; terrain visible on sides)
            _panel = new GameObject("KlappbuchPanel");
            _panel.transform.SetParent(transform, false);
            _panel.transform.SetAsLastSibling();
            var overlay = _panel.AddComponent<Image>();
            overlay.color = new Color(0f, 0f, 0f, 0.01f);
            var overlayRect = _panel.GetComponent<RectTransform>();
            overlayRect.anchorMin = Vector2.zero;
            overlayRect.anchorMax = Vector2.one;
            overlayRect.offsetMin = Vector2.zero;
            overlayRect.offsetMax = Vector2.zero;

            // Click overlay to close
            _panel.AddComponent<Button>().onClick.AddListener(Close);

            // Main card
            var card = MakeRect(_panel.transform, "Card", Vector2.zero,
                new Vector2(_panelW, _panelH));
            card.AddComponent<Image>().color = BG;
            card.AddComponent<Button>().onClick.AddListener(() => { }); // block click-through

            // ── Title bar ──
            var titleGo = MakeRect(card.transform, "Title",
                new Vector2(0, _panelH / 2 - TITLE_H / 2),
                new Vector2(_panelW - CLOSE_SIZE - 20, TITLE_H));
            var titleText = titleGo.AddComponent<Text>();
            titleText.font = GetFont();
            titleText.fontSize = 20;
            titleText.color = new Color(0.8f, 0.9f, 0.7f);
            titleText.alignment = TextAnchor.MiddleCenter;
            titleText.fontStyle = FontStyle.Bold;
            titleText.text = "ORDERS";

            // ── Close [X] button (44 × 44) ──
            var closeGo = MakeRect(card.transform, "CloseX",
                new Vector2(_panelW / 2 - CLOSE_SIZE / 2 - 4, _panelH / 2 - CLOSE_SIZE / 2 - 2),
                new Vector2(CLOSE_SIZE, CLOSE_SIZE));
            closeGo.AddComponent<Image>().color = new Color(0.5f, 0.2f, 0.2f, 0.8f);
            var closeBtn = closeGo.AddComponent<Button>();
            closeBtn.onClick.AddListener(Close);
            var closeLabel = MakeRect(closeGo.transform, "X", Vector2.zero,
                new Vector2(CLOSE_SIZE, CLOSE_SIZE));
            var closeTxt = closeLabel.AddComponent<Text>();
            closeTxt.font = GetFont();
            closeTxt.fontSize = 22;
            closeTxt.color = Color.white;
            closeTxt.alignment = TextAnchor.MiddleCenter;
            closeTxt.fontStyle = FontStyle.Bold;
            closeTxt.text = "X";

            // ── Active Orders button (top-left) ──
            var listBtnGo = MakeRect(card.transform, "ActiveOrdersBtn",
                new Vector2(-_panelW / 2 + 70, _panelH / 2 - TITLE_H / 2),
                new Vector2(120, 32));
            listBtnGo.AddComponent<Image>().color = new Color(0.25f, 0.30f, 0.45f, 0.8f);
            listBtnGo.AddComponent<Button>().onClick.AddListener(() =>
            {
                if (OrderListUI.Instance != null) OrderListUI.Instance.Toggle();
            });
            var listTxt = MakeRect(listBtnGo.transform, "T", Vector2.zero, new Vector2(120, 32));
            _activeOrdersText = listTxt.AddComponent<Text>();
            _activeOrdersText.font = GetFont();
            _activeOrdersText.fontSize = FONT_MIN;
            _activeOrdersText.color = Color.white;
            _activeOrdersText.alignment = TextAnchor.MiddleCenter;
            UpdateActiveOrdersLabel();

            // ── Three picker columns ──
            float columnsY = (_panelH / 2 - TITLE_H) - _colH / 2 - 4;
            float col1X = -_panelW / 2 + COL_PAD + _whoColW / 2;
            float col2X = col1X + _whoColW / 2 + COL_PAD + _doesColW / 2;
            float col3X = col2X + _doesColW / 2 + COL_PAD + _whatColW / 2;

            PopulateWhoItems(context);
            PopulateDoesItems(context);
            PopulateWhatItems();

            _whoScroll = BuildPickerColumn(card.transform, "WHO", col1X, columnsY, _whoColW,
                _whoItems.Count, BuildWhoRows, out _whoContentRect);
            _doesScroll = BuildPickerColumn(card.transform, "DOES", col2X, columnsY, _doesColW,
                _doesItems.Count, BuildDoesRows, out _doesContentRect);
            _whatScroll = BuildPickerColumn(card.transform, "WHAT / WHERE", col3X, columnsY, _whatColW,
                _whatItems.Count, BuildWhatRows, out _whatContentRect);

            // ── Result line ──
            float resultY = -_panelH / 2 + BTN_H + RESULT_H / 2 + 8;
            BuildResultLine(card.transform, resultY);

            // ── Confirm button ──
            float btnY = -_panelH / 2 + BTN_H / 2 + 4;
            BuildConfirmButton(card.transform, btnY);

            // ── Scroll to context pre-fills ──
            ApplyContextScroll(context);
            _prevDoesIdx = _doesIdx;

            UpdateResultLine();
        }

        // ─── Picker Column Builder ──────────────────────────────

        private ScrollRect BuildPickerColumn(Transform parent, string header,
            float x, float y, float colWidth, int itemCount,
            System.Action<Transform> populateRows, out RectTransform contentRect)
        {
            float totalH = _colH + 28; // header + column
            var col = MakeRect(parent, $"Col_{header}", new Vector2(x, y),
                new Vector2(colWidth, totalH));

            // Header
            var hdrGo = MakeRect(col.transform, "Header",
                new Vector2(0, totalH / 2 - 14), new Vector2(colWidth, 28));
            var hdrTxt = hdrGo.AddComponent<Text>();
            hdrTxt.font = GetFont();
            hdrTxt.fontSize = 15;
            hdrTxt.color = new Color(0.7f, 0.8f, 0.6f);
            hdrTxt.alignment = TextAnchor.MiddleCenter;
            hdrTxt.fontStyle = FontStyle.Bold;
            hdrTxt.text = header;

            // Scroll viewport
            var vpGo = MakeRect(col.transform, "Viewport",
                new Vector2(0, -14), new Vector2(colWidth, _colH));
            vpGo.AddComponent<Image>().color = COL_BG;
            vpGo.AddComponent<RectMask2D>();

            // Highlight band (center selection indicator, non-interactive)
            var band = MakeRect(vpGo.transform, "Band",
                Vector2.zero, new Vector2(colWidth, ROW_HEIGHT + 4));
            var bandImg = band.AddComponent<Image>();
            bandImg.color = BAND_COLOR;
            bandImg.raycastTarget = false;

            // ScrollRect
            var scroll = vpGo.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.elasticity = 0.1f;
            scroll.inertia = true;
            scroll.decelerationRate = 0.08f;
            scroll.scrollSensitivity = 30f;

            // Content container
            var content = new GameObject("Content");
            content.transform.SetParent(vpGo.transform, false);
            contentRect = content.AddComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0, 1);
            contentRect.anchorMax = new Vector2(1, 1);
            contentRect.pivot = new Vector2(0.5f, 1);
            contentRect.anchoredPosition = Vector2.zero;

            // Top/bottom padding so first/last items can scroll to center
            float pad = Mathf.FloorToInt(_colH / 2f - ROW_HEIGHT / 2f);

            var layout = content.AddComponent<VerticalLayoutGroup>();
            layout.spacing = SPACING;
            layout.padding = new RectOffset(2, 2, (int)pad, (int)pad);
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            content.AddComponent<ContentSizeFitter>().verticalFit =
                ContentSizeFitter.FitMode.PreferredSize;

            scroll.content = contentRect;

            // Populate rows
            populateRows(content.transform);

            return scroll;
        }

        // ─── WHO Items ──────────────────────────────────────────

        private void PopulateWhoItems(OpenKlappbuchEvent context)
        {
            _whoItems.Clear();
            _whoItems.Add(new WhoItem
            {
                Subject = OrderSubject.All,
                DisplayLabel = "All Settlers",
                Subtitle = ""
            });
            _whoItems.Add(new WhoItem
            {
                Subject = OrderSubject.NextFree,
                DisplayLabel = "Next Free",
                Subtitle = ""
            });

            var settlers = Object.FindObjectsByType<Settler>(FindObjectsSortMode.None);
            int contextIdx = -1;
            for (int i = 0; i < settlers.Length; i++)
            {
                var s = settlers[i];
                bool busy = s.HasTask;
                _whoItems.Add(new WhoItem
                {
                    Subject = OrderSubject.Named,
                    SettlerName = s.name,
                    IsBusy = busy,
                    DisplayLabel = s.name,
                    Subtitle = busy ? s.StateName : $"[{s.Trait.ToString()[0]}]"
                });

                if (!string.IsNullOrEmpty(context.SettlerName) && s.name == context.SettlerName)
                    contextIdx = _whoItems.Count - 1;
            }

            if (contextIdx >= 0) _whoIdx = contextIdx;
        }

        private void BuildWhoRows(Transform content)
        {
            foreach (var item in _whoItems)
            {
                Color bg = item.IsBusy ? ROW_BUSY : ROW_N;
                CreatePickerRow(content, item.DisplayLabel, item.Subtitle,
                    false, ROW_HEIGHT, bg, Color.white);
            }
        }

        // ─── DOES Items ─────────────────────────────────────────

        private void PopulateDoesItems(OpenKlappbuchEvent context)
        {
            _doesItems.Clear();
            var vocab = OrderVocabulary.Instance;
            if (vocab == null) return;

            var entries = vocab.GetAllPredicates();
            int contextIdx = -1;
            foreach (var entry in entries)
            {
                _doesItems.Add(new DoesItem
                {
                    Predicate = entry.Predicate,
                    IsLocked = !entry.IsUnlocked,
                    RequiredDiscovery = entry.RequiredDiscovery
                });

                if (context.PredicateHint.HasValue &&
                    entry.Predicate == context.PredicateHint.Value && entry.IsUnlocked)
                    contextIdx = _doesItems.Count - 1;
            }

            if (contextIdx >= 0) _doesIdx = contextIdx;
        }

        private void BuildDoesRows(Transform content)
        {
            bool pastDivider = false;
            foreach (var item in _doesItems)
            {
                // Divider before first locked item
                if (!pastDivider && item.IsLocked)
                {
                    pastDivider = true;

                    // Label
                    var divLabel = new GameObject("DividerLabel");
                    divLabel.transform.SetParent(content, false);
                    divLabel.AddComponent<LayoutElement>().preferredHeight = 18;
                    var dlTxt = divLabel.AddComponent<Text>();
                    dlTxt.font = GetFont();
                    dlTxt.fontSize = 11;
                    dlTxt.color = new Color(0.5f, 0.5f, 0.5f, 0.7f);
                    dlTxt.alignment = TextAnchor.MiddleCenter;
                    dlTxt.text = "-- locked --";

                    // Line
                    var divLine = new GameObject("DividerLine");
                    divLine.transform.SetParent(content, false);
                    divLine.AddComponent<LayoutElement>().preferredHeight = 2;
                    divLine.AddComponent<Image>().color = new Color(0.4f, 0.5f, 0.4f, 0.6f);
                }

                string subtitle = item.IsLocked
                    ? $"Requires: {item.RequiredDiscovery ?? "???"}"
                    : "";
                float h = (item.IsLocked && !string.IsNullOrEmpty(subtitle))
                    ? ROW_HEIGHT_LOCKED : ROW_HEIGHT;
                Color bg = item.IsLocked ? ROW_LOCK : ROW_N;

                CreatePickerRow(content, item.Predicate.ToString(), subtitle,
                    item.IsLocked, h, bg, Color.white);
            }
        }

        // ─── WHAT Items ─────────────────────────────────────────

        private void PopulateWhatItems()
        {
            _whatItems.Clear();
            var vocab = OrderVocabulary.Instance;
            if (vocab == null) return;

            // Get predicate from current DOES selection
            OrderPredicate pred = OrderPredicate.Gather;
            if (_doesIdx >= 0 && _doesIdx < _doesItems.Count)
                pred = _doesItems[_doesIdx].Predicate;

            var objects = vocab.GetObjectsForPredicate(pred);
            foreach (var obj in objects)
                _whatItems.Add(new WhatItem { Object = obj });
        }

        private void BuildWhatRows(Transform content)
        {
            if (_whatItems.Count == 0)
            {
                var emptyGo = new GameObject("Empty");
                emptyGo.transform.SetParent(content, false);
                emptyGo.AddComponent<LayoutElement>().preferredHeight = ROW_HEIGHT;
                var et = emptyGo.AddComponent<Text>();
                et.font = GetFont();
                et.fontSize = FONT_MIN;
                et.color = TXT_L;
                et.alignment = TextAnchor.MiddleCenter;
                et.text = "(none)";
                return;
            }

            foreach (var item in _whatItems)
            {
                CreatePickerRow(content, item.Object.DisplayName, "",
                    false, ROW_HEIGHT, ROW_N, TXT_N);
            }
        }

        private void RebuildWhatColumn()
        {
            if (_whatContentRect == null) return;

            // Destroy old rows
            for (int i = _whatContentRect.childCount - 1; i >= 0; i--)
                Destroy(_whatContentRect.GetChild(i).gameObject);

            PopulateWhatItems();
            BuildWhatRows(_whatContentRect);

            // Reset scroll to top (index 0)
            _whatIdx = 0;
            if (_whatContentRect != null)
                _whatContentRect.anchoredPosition = Vector2.zero;
        }

        // ─── Snap-to-Center Logic ───────────────────────────────

        private void SnapColumn(ScrollRect scroll, RectTransform content,
            int itemCount, float rowH, ref int selectedIdx,
            System.Action<int> onChange)
        {
            if (scroll == null || content == null || itemCount == 0) return;

            float step = rowH + SPACING;
            float y = content.anchoredPosition.y;

            // Calculate nearest item index
            int nearest = Mathf.Clamp(Mathf.RoundToInt(y / step), 0, itemCount - 1);

            // When velocity is low, snap to nearest with 0.2s lerp
            if (Mathf.Abs(scroll.velocity.y) < SNAP_THRESHOLD)
            {
                float targetY = nearest * step;
                float lerpT = 1f - Mathf.Pow(0.001f, Time.unscaledDeltaTime / SNAP_DURATION);
                float newY = Mathf.Lerp(y, targetY, lerpT);
                content.anchoredPosition = new Vector2(content.anchoredPosition.x, newY);

                if (Mathf.Abs(newY - targetY) < 0.5f)
                {
                    content.anchoredPosition = new Vector2(content.anchoredPosition.x, targetY);
                    scroll.velocity = Vector2.zero;
                }
            }

            if (nearest != selectedIdx)
            {
                selectedIdx = nearest;
                onChange?.Invoke(nearest);
            }
        }

        /// <summary>
        /// Snap DOES column with special logic: skip locked items.
        /// </summary>
        private void SnapDoesColumn()
        {
            if (_doesScroll == null || _doesContentRect == null || _doesItems.Count == 0) return;

            // DOES column has mixed row heights (locked vs unlocked) and divider elements.
            // Use uniform step based on ROW_HEIGHT for snapping since most items are unlocked.
            // The divider adds offset, but snapping to nearest unlocked is more important.
            float step = ROW_HEIGHT + SPACING;
            float y = _doesContentRect.anchoredPosition.y;
            int nearest = Mathf.Clamp(Mathf.RoundToInt(y / step), 0, _doesItems.Count - 1);

            // If nearest is locked, find closest unlocked
            if (nearest < _doesItems.Count && _doesItems[nearest].IsLocked)
            {
                int below = nearest - 1;
                int above = nearest + 1;
                while (below >= 0 || above < _doesItems.Count)
                {
                    if (below >= 0 && !_doesItems[below].IsLocked) { nearest = below; break; }
                    if (above < _doesItems.Count && !_doesItems[above].IsLocked) { nearest = above; break; }
                    below--;
                    above++;
                }
            }

            if (Mathf.Abs(_doesScroll.velocity.y) < SNAP_THRESHOLD)
            {
                float targetY = nearest * step;
                float lerpT = 1f - Mathf.Pow(0.001f, Time.unscaledDeltaTime / SNAP_DURATION);
                float newY = Mathf.Lerp(y, targetY, lerpT);
                _doesContentRect.anchoredPosition =
                    new Vector2(_doesContentRect.anchoredPosition.x, newY);

                if (Mathf.Abs(newY - targetY) < 0.5f)
                {
                    _doesContentRect.anchoredPosition =
                        new Vector2(_doesContentRect.anchoredPosition.x, targetY);
                    _doesScroll.velocity = Vector2.zero;
                }
            }

            _doesIdx = nearest;
        }

        // ─── Context Pre-Scroll ─────────────────────────────────

        private void ApplyContextScroll(OpenKlappbuchEvent context)
        {
            float step = ROW_HEIGHT + SPACING;

            // WHO
            if (_whoContentRect != null && _whoIdx > 0)
                _whoContentRect.anchoredPosition = new Vector2(0, _whoIdx * step);

            // DOES
            if (_doesContentRect != null && _doesIdx > 0)
                _doesContentRect.anchoredPosition = new Vector2(0, _doesIdx * step);

            // WHAT
            if (_whatContentRect != null && _whatIdx > 0)
                _whatContentRect.anchoredPosition = new Vector2(0, _whatIdx * step);
        }

        // ─── Result Line ────────────────────────────────────────

        private void BuildResultLine(Transform parent, float y)
        {
            var bg = MakeRect(parent, "ResultLine",
                new Vector2(0, y), new Vector2(_panelW - 40, RESULT_H));
            bg.AddComponent<Image>().color = new Color(0.06f, 0.08f, 0.06f, 0.9f);

            // Result text
            var txt = MakeRect(bg.transform, "Text",
                new Vector2(30, 0), new Vector2(_panelW - 140, RESULT_H - 8));
            _resultText = txt.AddComponent<Text>();
            _resultText.font = GetFont();
            _resultText.fontSize = 18;
            _resultText.color = INVALID_C;
            _resultText.alignment = TextAnchor.MiddleCenter;
            _resultText.horizontalOverflow = HorizontalWrapMode.Wrap;
            _resultText.verticalOverflow = VerticalWrapMode.Overflow;

            // NICHT toggle (left)
            var negGo = MakeRect(parent, "NegateBtn",
                new Vector2(-_panelW / 2 + 50, y), new Vector2(70, CLOSE_SIZE));
            _negateImg = negGo.AddComponent<Image>();
            _negateImg.color = _isNegated ? NEG_C : new Color(0.3f, 0.3f, 0.3f, 0.7f);
            negGo.AddComponent<Button>().onClick.AddListener(() =>
            {
                _isNegated = !_isNegated;
                _negateImg.color = _isNegated ? NEG_C : new Color(0.3f, 0.3f, 0.3f, 0.7f);
                UpdateResultLine();
            });
            var negLabel = MakeRect(negGo.transform, "L", Vector2.zero, new Vector2(70, CLOSE_SIZE));
            var nt = negLabel.AddComponent<Text>();
            nt.font = GetFont();
            nt.fontSize = FONT_MIN;
            nt.color = Color.white;
            nt.alignment = TextAnchor.MiddleCenter;
            nt.fontStyle = FontStyle.Bold;
            nt.text = "NICHT";
        }

        private void BuildConfirmButton(Transform parent, float y)
        {
            var btn = MakeRect(parent, "ConfirmBtn",
                new Vector2(0, y), new Vector2(220, BTN_H));
            _confirmBg = btn.AddComponent<Image>();
            _confirmBg.color = CONFIRM_OFF;
            _confirmBtn = btn.AddComponent<Button>();
            _confirmBtn.targetGraphic = _confirmBg;
            _confirmBtn.onClick.AddListener(ConfirmOrder);

            var label = MakeRect(btn.transform, "L", Vector2.zero, new Vector2(220, BTN_H));
            var lt = label.AddComponent<Text>();
            lt.font = GetFont();
            lt.fontSize = 20;
            lt.color = Color.white;
            lt.alignment = TextAnchor.MiddleCenter;
            lt.fontStyle = FontStyle.Bold;
            lt.text = "Give Order";
        }

        private void UpdateResultLine()
        {
            if (_resultText == null) return;

            var order = BuildCurrentOrder();
            if (order == null)
            {
                _resultText.text = "...";
                _resultText.color = INVALID_C;
                _confirmBg.color = CONFIRM_OFF;
                _confirmBtn.interactable = false;
                return;
            }

            string sentence = order.BuildSentence();
            bool valid = order.IsValid();

            _resultText.text = sentence;
            _resultText.color = valid ? VALID_C : INVALID_C;
            _resultText.fontStyle = _isNegated ? FontStyle.Italic : FontStyle.Normal;

            _confirmBg.color = valid ? CONFIRM_ON : CONFIRM_OFF;
            _confirmBtn.interactable = valid;

            UpdateActiveOrdersLabel();
        }

        private void UpdateActiveOrdersLabel()
        {
            if (_activeOrdersText == null) return;
            int count = OrderManager.Instance != null
                ? OrderManager.Instance.ActiveOrders.Count : 0;
            _activeOrdersText.text = count > 0
                ? $"Active Orders ({count})" : "Active Orders";
        }

        // ─── Order Construction ─────────────────────────────────

        /// <summary>
        /// Read the center item index from a column's scroll position.
        /// </summary>
        private int GetCenterIndex(RectTransform content, int itemCount, float rowH)
        {
            if (content == null || itemCount == 0) return 0;
            float step = rowH + SPACING;
            float y = content.anchoredPosition.y;
            return Mathf.Clamp(Mathf.RoundToInt(y / step), 0, itemCount - 1);
        }

        private OrderDefinition BuildCurrentOrder()
        {
            // Read center item from each column's ACTUAL scroll position
            int whoIdx = GetCenterIndex(_whoContentRect, _whoItems.Count, ROW_HEIGHT);
            int doesIdx = GetCenterIndex(_doesContentRect, _doesItems.Count, ROW_HEIGHT);
            int whatIdx = GetCenterIndex(_whatContentRect, _whatItems.Count, ROW_HEIGHT);

            // Skip locked DOES items
            if (doesIdx >= 0 && doesIdx < _doesItems.Count && _doesItems[doesIdx].IsLocked)
            {
                // Find nearest unlocked
                for (int d = 1; d < _doesItems.Count; d++)
                {
                    if (doesIdx - d >= 0 && !_doesItems[doesIdx - d].IsLocked) { doesIdx -= d; break; }
                    if (doesIdx + d < _doesItems.Count && !_doesItems[doesIdx + d].IsLocked) { doesIdx += d; break; }
                }
            }

            if (doesIdx < 0 || doesIdx >= _doesItems.Count) return null;
            var doesItem = _doesItems[doesIdx];
            if (doesItem.IsLocked) return null;

            var order = new OrderDefinition
            {
                Predicate = doesItem.Predicate,
                Negated = _isNegated
            };

            // WHO
            if (whoIdx >= 0 && whoIdx < _whoItems.Count)
            {
                var who = _whoItems[whoIdx];
                order.Subject = who.Subject;
                order.SettlerName = who.SettlerName;
            }

            // WHAT (single selection from picker)
            if (whatIdx >= 0 && whatIdx < _whatItems.Count)
            {
                var whatObj = _whatItems[whatIdx].Object;
                order.Objects.Add(whatObj);

                // "Here" stores the tap world position so settlers pathfind there
                if (whatObj.Id == "here" && _tapPosition.HasValue)
                    order.TargetPosition = _tapPosition;
            }

            return order;
        }

        private void ConfirmOrder()
        {
            var order = BuildCurrentOrder();
            if (order == null || !order.IsValid()) return;

            string sentence = order.BuildSentence();
            Debug.Log($"[Klappbuch] ORDER: {sentence} | WHO={order.Subject} DOES={order.Predicate} WHAT={string.Join(",", order.Objects.ConvertAll(o => o.DisplayName))} NEG={order.Negated} pos={order.TargetPosition}");
            OrderManager.Instance?.CreateOrder(order);

            // Brief green flash on confirm button
            if (_confirmBg != null)
                _confirmBg.color = new Color(0.2f, 0.8f, 0.3f, 1f);

            Close();

            // Show floating notification for 2 seconds
            StartCoroutine(ShowOrderNotification(sentence));
        }

        private IEnumerator ShowOrderNotification(string sentence)
        {
            var go = new GameObject("OrderNotification");
            go.transform.SetParent(transform, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0, 60);
            float notifW = _canvasW > 0 ? _canvasW * 0.7f : 500f;
            rt.sizeDelta = new Vector2(notifW, 48);

            var bg = go.AddComponent<Image>();
            bg.color = new Color(0.12f, 0.35f, 0.18f, 0.92f);

            var txtGo = new GameObject("Text");
            txtGo.transform.SetParent(go.transform, false);
            var tr = txtGo.AddComponent<RectTransform>();
            tr.anchorMin = Vector2.zero;
            tr.anchorMax = Vector2.one;
            tr.offsetMin = new Vector2(8, 0);
            tr.offsetMax = new Vector2(-8, 0);
            var txt = txtGo.AddComponent<Text>();
            txt.font = GetFont();
            txt.fontSize = 16;
            txt.color = VALID_C;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.text = $"\u2713  Order given: {sentence}";

            yield return new WaitForSeconds(2f);

            // Fade out over 0.3s
            float fade = 0.3f;
            float t = 0f;
            Color bgC = bg.color;
            Color txtC = txt.color;
            while (t < fade)
            {
                t += Time.deltaTime;
                float a = 1f - (t / fade);
                bg.color = new Color(bgC.r, bgC.g, bgC.b, bgC.a * a);
                txt.color = new Color(txtC.r, txtC.g, txtC.b, txtC.a * a);
                yield return null;
            }

            Destroy(go);
        }

        // ─── Row Builder ────────────────────────────────────────

        private void CreatePickerRow(Transform parent, string label, string subtitle,
            bool isLocked, float height, Color bgColor, Color textColor)
        {
            bool hasSub = !string.IsNullOrEmpty(subtitle);
            var row = new GameObject($"Row_{label}");
            row.transform.SetParent(parent, false);

            row.AddComponent<LayoutElement>().preferredHeight = height;
            row.AddComponent<Image>().color = bgColor;

            // Main label
            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(row.transform, false);
            var lr = labelGo.AddComponent<RectTransform>();
            lr.anchorMin = new Vector2(0, hasSub ? 0.42f : 0);
            lr.anchorMax = Vector2.one;
            lr.offsetMin = new Vector2(4, 0);
            lr.offsetMax = new Vector2(-4, -2);
            var lt = labelGo.AddComponent<Text>();
            lt.font = GetFont();
            lt.fontSize = FONT_MIN;
            lt.color = textColor;
            lt.alignment = TextAnchor.MiddleCenter;
            lt.horizontalOverflow = HorizontalWrapMode.Wrap;
            lt.verticalOverflow = VerticalWrapMode.Truncate;
            lt.text = label;

            // Lock icon
            if (isLocked)
            {
                var lockGo = new GameObject("Lock");
                lockGo.transform.SetParent(row.transform, false);
                var lkr = lockGo.AddComponent<RectTransform>();
                lkr.anchorMin = new Vector2(1, 0.5f);
                lkr.anchorMax = new Vector2(1, 0.5f);
                lkr.pivot = new Vector2(1, 0.5f);
                lkr.anchoredPosition = new Vector2(-4, 0);
                lkr.sizeDelta = new Vector2(20, 20);
                lockGo.AddComponent<Image>().color = new Color(0.6f, 0.4f, 0.2f, 0.8f);
            }

            // Subtitle
            if (hasSub)
            {
                var subGo = new GameObject("Sub");
                subGo.transform.SetParent(row.transform, false);
                var sr = subGo.AddComponent<RectTransform>();
                sr.anchorMin = Vector2.zero;
                sr.anchorMax = new Vector2(1, 0.42f);
                sr.offsetMin = new Vector2(4, 2);
                sr.offsetMax = new Vector2(-4, 0);
                var st = subGo.AddComponent<Text>();
                st.font = GetFont();
                st.fontSize = FONT_MIN - 1;
                st.color = isLocked ? new Color(1f, 0.6f, 0.15f) : Color.white;
                st.alignment = TextAnchor.MiddleCenter;
                st.horizontalOverflow = HorizontalWrapMode.Wrap;
                st.text = subtitle;
            }
        }

        // ─── Helpers ────────────────────────────────────────────

        private static bool PredicateUsesLocations(OrderPredicate pred)
        {
            return pred switch
            {
                OrderPredicate.Gather => true,
                OrderPredicate.Explore => true,
                OrderPredicate.Avoid => true,
                OrderPredicate.Hunt => true,
                OrderPredicate.Fell => true,
                OrderPredicate.Dig => true,
                _ => false
            };
        }

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
