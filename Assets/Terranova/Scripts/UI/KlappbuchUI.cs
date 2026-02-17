using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Terranova.Core;
using Terranova.Orders;
using Terranova.Population;

namespace Terranova.UI
{
    /// <summary>
    /// Feature 7.3: Klappbuch UI (3-Column Picker).
    ///
    /// Inspired by children's flip-books (Klappbuch) where you flip head/body/legs
    /// independently. Three scrollable columns side by side:
    ///   Column 1 - WHO (Subject): All, Next Free, each settler by name
    ///   Column 2 - DOES (Predicate): verbs with lock states
    ///   Column 3 - WHAT/WHERE (Objects): context-dependent on predicate
    ///
    /// Feature 7.4: Result line at bottom showing assembled sentence.
    /// Feature 7.5: Contextual opening from taps.
    /// </summary>
    public class KlappbuchUI : MonoBehaviour
    {
        public static KlappbuchUI Instance { get; private set; }

        // ─── Layout Constants ────────────────────────────────

        private const float PANEL_WIDTH = 900f;
        private const float PANEL_HEIGHT = 520f;
        private const float COLUMN_WIDTH = 280f;
        private const float COLUMN_HEIGHT = 340f;
        private const float ROW_HEIGHT = 52f;
        private const float HEADER_HEIGHT = 36f;
        private const float RESULT_HEIGHT = 80f;
        private const float BUTTON_HEIGHT = 50f;
        private const float PADDING = 8f;

        private static readonly Color BG_COLOR = new(0.08f, 0.10f, 0.08f, 0.95f);
        private static readonly Color COLUMN_BG = new(0.12f, 0.14f, 0.12f, 0.9f);
        private static readonly Color ROW_NORMAL = new(0.16f, 0.20f, 0.16f, 0.8f);
        private static readonly Color ROW_SELECTED = new(0.25f, 0.50f, 0.30f, 0.95f);
        private static readonly Color ROW_LOCKED = new(0.12f, 0.12f, 0.12f, 0.6f);
        private static readonly Color ROW_BUSY = new(0.14f, 0.14f, 0.14f, 0.5f);
        private static readonly Color TEXT_NORMAL = Color.white;
        private static readonly Color TEXT_LOCKED = new(0.5f, 0.5f, 0.5f);
        private static readonly Color TEXT_BUSY = new(0.6f, 0.6f, 0.6f);
        private static readonly Color NEGATED_COLOR = new(0.9f, 0.25f, 0.25f);
        private static readonly Color VALID_COLOR = new(0.3f, 0.9f, 0.4f);
        private static readonly Color INVALID_COLOR = new(1f, 0.6f, 0.2f);
        private static readonly Color CONFIRM_ACTIVE = new(0.2f, 0.55f, 0.3f, 0.95f);
        private static readonly Color CONFIRM_INACTIVE = new(0.2f, 0.2f, 0.2f, 0.5f);

        // ─── State ───────────────────────────────────────────

        private GameObject _panel;
        private bool _isOpen;

        // Selection state
        private OrderSubject _selectedSubject = OrderSubject.All;
        private string _selectedSettlerName;
        private OrderPredicate _selectedPredicate = OrderPredicate.Gather;
        private List<OrderObject> _selectedObjects = new();
        private bool _isNegated;
        private Vector3? _tapPosition;

        // UI element references for rebuilding columns
        private Transform _whoContent;
        private Transform _doesContent;
        private Transform _whatContent;
        private Text _resultText;
        private Button _confirmButton;
        private Image _confirmBg;
        private Text _confirmLabel;

        // Track row GameObjects for selection highlight
        private readonly List<(GameObject go, int index)> _whoRows = new();
        private readonly List<(GameObject go, int index)> _doesRows = new();
        private readonly List<(GameObject go, OrderObject obj)> _whatRows = new();

        private int _whoSelectedIndex;
        private int _doesSelectedIndex;

        // ─── Lifecycle ───────────────────────────────────────

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
            // ESC or back button closes
            if (_isOpen && UnityEngine.Input.GetKeyDown(KeyCode.Escape))
                Close();
        }

        // ─── Open / Close ────────────────────────────────────

        private void OnOpenRequest(OpenKlappbuchEvent evt)
        {
            Open(evt);
        }

        /// <summary>Feature 7.5: Contextual opening.</summary>
        public void Open(OpenKlappbuchEvent context = default)
        {
            if (_isOpen) Close();

            // Reset selection
            _selectedSubject = OrderSubject.All;
            _selectedSettlerName = null;
            _selectedPredicate = OrderPredicate.Gather;
            _selectedObjects.Clear();
            _isNegated = false;
            _tapPosition = context.TapPosition;
            _whoSelectedIndex = 0;
            _doesSelectedIndex = 0;

            // Apply contextual pre-fills
            if (!string.IsNullOrEmpty(context.SettlerName))
            {
                _selectedSubject = OrderSubject.Named;
                _selectedSettlerName = context.SettlerName;
            }

            if (context.PredicateHint.HasValue)
            {
                _selectedPredicate = context.PredicateHint.Value;
            }

            if (context.TapPosition.HasValue)
            {
                var hereObj = new OrderObject("here", "Here", OrderObjectCategory.Location)
                    { Position = context.TapPosition };
                _selectedObjects.Add(hereObj);
            }

            if (!string.IsNullOrEmpty(context.ObjectId))
            {
                var vocab = OrderVocabulary.Instance;
                if (vocab != null)
                {
                    foreach (var obj in vocab.UnlockedObjects)
                    {
                        if (obj.Id == context.ObjectId)
                        {
                            _selectedObjects.Add(obj);
                            break;
                        }
                    }
                }
            }

            BuildPanel();
            _isOpen = true;
        }

        public void Close()
        {
            if (_panel != null)
                Destroy(_panel);
            _panel = null;
            _isOpen = false;
            _whoRows.Clear();
            _doesRows.Clear();
            _whatRows.Clear();
        }

        public bool IsOpen => _isOpen;

        // ─── Panel Construction ──────────────────────────────

        private void BuildPanel()
        {
            // Full-screen dark overlay
            _panel = new GameObject("KlappbuchPanel");
            _panel.transform.SetParent(transform, false);
            _panel.transform.SetAsLastSibling();
            var overlay = _panel.AddComponent<Image>();
            overlay.color = new Color(0f, 0f, 0f, 0.6f);
            var overlayRect = _panel.GetComponent<RectTransform>();
            overlayRect.anchorMin = Vector2.zero;
            overlayRect.anchorMax = Vector2.one;
            overlayRect.offsetMin = Vector2.zero;
            overlayRect.offsetMax = Vector2.zero;

            // Click overlay to close
            var closeBtn = _panel.AddComponent<Button>();
            closeBtn.onClick.AddListener(Close);

            // Main card (centered)
            var card = CreateChild(_panel.transform, "Card", Vector2.zero,
                new Vector2(PANEL_WIDTH, PANEL_HEIGHT));
            var cardImg = card.AddComponent<Image>();
            cardImg.color = BG_COLOR;
            // Prevent click-through to overlay close button
            card.AddComponent<Button>().onClick.AddListener(() => { });

            // Title bar
            var titleObj = CreateChild(card.transform, "Title",
                new Vector2(0, PANEL_HEIGHT / 2 - 20), new Vector2(PANEL_WIDTH - 100, 36));
            var titleText = titleObj.AddComponent<Text>();
            titleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            titleText.fontSize = 22;
            titleText.color = new Color(0.8f, 0.9f, 0.7f);
            titleText.alignment = TextAnchor.MiddleCenter;
            titleText.fontStyle = FontStyle.Bold;
            titleText.text = "ORDERS";

            // Close button (X) top-right
            var closeBtnObj = CreateChild(card.transform, "CloseX",
                new Vector2(PANEL_WIDTH / 2 - 30, PANEL_HEIGHT / 2 - 20), new Vector2(40, 36));
            var closeBtnImg = closeBtnObj.AddComponent<Image>();
            closeBtnImg.color = new Color(0.5f, 0.2f, 0.2f, 0.8f);
            var closeXBtn = closeBtnObj.AddComponent<Button>();
            closeXBtn.targetGraphic = closeBtnImg;
            closeXBtn.onClick.AddListener(Close);
            var closeLabel = CreateChild(closeBtnObj.transform, "X", Vector2.zero, new Vector2(40, 36));
            var closeLabelText = closeLabel.AddComponent<Text>();
            closeLabelText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            closeLabelText.fontSize = 20;
            closeLabelText.color = Color.white;
            closeLabelText.alignment = TextAnchor.MiddleCenter;
            closeLabelText.text = "X";

            // Three columns
            float columnsY = 20f;
            float colSpacing = (PANEL_WIDTH - 3 * COLUMN_WIDTH) / 4f;

            float col1X = -PANEL_WIDTH / 2 + colSpacing + COLUMN_WIDTH / 2;
            float col2X = col1X + COLUMN_WIDTH + colSpacing;
            float col3X = col2X + COLUMN_WIDTH + colSpacing;

            BuildColumn(card.transform, "WHO", col1X, columnsY, BuildWhoColumn, out _whoContent);
            BuildColumn(card.transform, "DOES", col2X, columnsY, BuildDoesColumn, out _doesContent);
            BuildColumn(card.transform, "WHAT / WHERE", col3X, columnsY, BuildWhatColumn, out _whatContent);

            // Result line at bottom
            float resultY = -PANEL_HEIGHT / 2 + RESULT_HEIGHT / 2 + BUTTON_HEIGHT + PADDING;
            BuildResultLine(card.transform, resultY);

            // Confirm button at very bottom
            float btnY = -PANEL_HEIGHT / 2 + BUTTON_HEIGHT / 2 + PADDING;
            BuildConfirmButton(card.transform, btnY);

            UpdateResultLine();
        }

        private void BuildColumn(Transform parent, string header, float x, float y,
            System.Action<Transform> populateContent, out Transform contentTransform)
        {
            // Column container
            var colObj = CreateChild(parent, $"Col_{header}",
                new Vector2(x, y), new Vector2(COLUMN_WIDTH, COLUMN_HEIGHT + HEADER_HEIGHT));

            // Header
            var headerObj = CreateChild(colObj.transform, "Header",
                new Vector2(0, COLUMN_HEIGHT / 2 + HEADER_HEIGHT / 2 - 4),
                new Vector2(COLUMN_WIDTH, HEADER_HEIGHT));
            var headerText = headerObj.AddComponent<Text>();
            headerText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            headerText.fontSize = 16;
            headerText.color = new Color(0.7f, 0.8f, 0.6f);
            headerText.alignment = TextAnchor.MiddleCenter;
            headerText.fontStyle = FontStyle.Bold;
            headerText.text = header;

            // Scroll area background
            var scrollBg = CreateChild(colObj.transform, "ScrollBg",
                new Vector2(0, -HEADER_HEIGHT / 2 + 4),
                new Vector2(COLUMN_WIDTH, COLUMN_HEIGHT));
            scrollBg.AddComponent<Image>().color = COLUMN_BG;
            scrollBg.AddComponent<RectMask2D>();

            // ScrollRect
            var scrollRect = scrollBg.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.scrollSensitivity = 30f;

            // Content container inside scroll
            var content = new GameObject("Content");
            content.transform.SetParent(scrollBg.transform, false);
            var contentRect = content.AddComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0, 1);
            contentRect.anchorMax = new Vector2(1, 1);
            contentRect.pivot = new Vector2(0.5f, 1);
            contentRect.anchoredPosition = Vector2.zero;
            // sizeDelta.y will be set by ContentSizeFitter

            var layout = content.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 2;
            layout.padding = new RectOffset(4, 4, 4, 4);
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var fitter = content.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scrollRect.content = contentRect;
            contentTransform = content.transform;

            // Populate
            populateContent(content.transform);
        }

        // ─── WHO Column (Feature 7.3) ──────────────────────

        private void BuildWhoColumn(Transform content)
        {
            _whoRows.Clear();
            int index = 0;

            // "All" option
            var allRow = CreateRow(content, "All", "", false, false, ROW_NORMAL, TEXT_NORMAL);
            bool allSelected = _selectedSubject == OrderSubject.All;
            if (allSelected) allRow.GetComponent<Image>().color = ROW_SELECTED;
            int allIdx = index;
            allRow.GetComponent<Button>().onClick.AddListener(() => SelectWho(OrderSubject.All, null, allIdx));
            _whoRows.Add((allRow, index++));

            // "Next Free" option
            var nextRow = CreateRow(content, "Next Free", "", false, false, ROW_NORMAL, TEXT_NORMAL);
            bool nextSelected = _selectedSubject == OrderSubject.NextFree;
            if (nextSelected) nextRow.GetComponent<Image>().color = ROW_SELECTED;
            int nextIdx = index;
            nextRow.GetComponent<Button>().onClick.AddListener(() => SelectWho(OrderSubject.NextFree, null, nextIdx));
            _whoRows.Add((nextRow, index++));

            // Each settler by name
            var settlers = Object.FindObjectsByType<Settler>(FindObjectsSortMode.None);
            foreach (var settler in settlers)
            {
                string settlerName = settler.name;
                string subtitle = settler.HasTask ? settler.StateName : "";
                bool isBusy = settler.HasTask;
                Color bgColor = isBusy ? ROW_BUSY : ROW_NORMAL;
                Color textColor = isBusy ? TEXT_BUSY : TEXT_NORMAL;

                var row = CreateRow(content, settlerName,
                    isBusy ? $"({subtitle})" : $"[{settler.Trait.ToString()[0]}]",
                    false, false, bgColor, textColor);

                bool isSelected = _selectedSubject == OrderSubject.Named &&
                                  _selectedSettlerName == settlerName;
                if (isSelected) row.GetComponent<Image>().color = ROW_SELECTED;

                int rowIdx = index;
                row.GetComponent<Button>().onClick.AddListener(
                    () => SelectWho(OrderSubject.Named, settlerName, rowIdx));
                _whoRows.Add((row, index++));
            }
        }

        private void SelectWho(OrderSubject subject, string settlerName, int rowIndex)
        {
            _selectedSubject = subject;
            _selectedSettlerName = settlerName;
            _whoSelectedIndex = rowIndex;

            // Update highlights
            foreach (var (go, idx) in _whoRows)
            {
                if (go == null) continue;
                var img = go.GetComponent<Image>();
                img.color = idx == rowIndex ? ROW_SELECTED : ROW_NORMAL;
            }

            UpdateResultLine();
        }

        // ─── DOES Column (Feature 7.3) ─────────────────────

        private void BuildDoesColumn(Transform content)
        {
            _doesRows.Clear();
            var vocab = OrderVocabulary.Instance;
            if (vocab == null) return;

            var entries = vocab.GetAllPredicates();
            int index = 0;
            bool pastDivider = false;

            foreach (var entry in entries)
            {
                // Add divider between start vocab and unlocked vocab
                if (!pastDivider && entry.RequiredDiscovery != null)
                {
                    pastDivider = true;
                    var divider = new GameObject("Divider");
                    divider.transform.SetParent(content, false);
                    var divRect = divider.AddComponent<RectTransform>();
                    divRect.sizeDelta = new Vector2(0, 2);
                    var divImg = divider.AddComponent<Image>();
                    divImg.color = new Color(0.4f, 0.5f, 0.4f, 0.6f);
                    var divLayout = divider.AddComponent<LayoutElement>();
                    divLayout.preferredHeight = 2;
                }

                bool unlocked = entry.IsUnlocked;
                string prefix = unlocked ? "" : "  ";
                string label = entry.Predicate.ToString();
                string subtitle = unlocked ? "" :
                    $"Requires: {entry.RequiredDiscovery ?? "???"}";

                Color bgColor = unlocked ? ROW_NORMAL : ROW_LOCKED;
                Color textColor = unlocked ? TEXT_NORMAL : TEXT_LOCKED;

                var row = CreateRow(content, label, subtitle, !unlocked, false, bgColor, textColor);

                bool isSelected = entry.Predicate == _selectedPredicate && unlocked;
                if (isSelected) row.GetComponent<Image>().color = ROW_SELECTED;

                int rowIdx = index;
                var predicate = entry.Predicate;
                bool isUnlocked = unlocked;

                row.GetComponent<Button>().onClick.AddListener(() =>
                {
                    if (!isUnlocked)
                    {
                        // Show lock tooltip (flash the requirement text)
                        return;
                    }
                    SelectPredicate(predicate, rowIdx);
                });

                _doesRows.Add((row, index++));
            }
        }

        private void SelectPredicate(OrderPredicate predicate, int rowIndex)
        {
            _selectedPredicate = predicate;
            _doesSelectedIndex = rowIndex;

            // Clear objects when predicate changes (context changes)
            _selectedObjects.Clear();

            // Re-apply tap position "here" if it was set
            if (_tapPosition.HasValue)
            {
                _selectedObjects.Add(new OrderObject("here", "Here", OrderObjectCategory.Location)
                    { Position = _tapPosition });
            }

            // Update does column highlights
            foreach (var (go, idx) in _doesRows)
            {
                if (go == null) continue;
                var img = go.GetComponent<Image>();
                // Keep locked rows their locked color
                if (img.color == ROW_LOCKED) continue;
                img.color = idx == rowIndex ? ROW_SELECTED : ROW_NORMAL;
            }

            // Rebuild WHAT column (context-dependent)
            RebuildWhatColumn();
            UpdateResultLine();
        }

        /// <summary>Feature 7.3: Swipe left on predicate toggles NICHT.</summary>
        public void ToggleNegation()
        {
            _isNegated = !_isNegated;
            UpdateResultLine();
        }

        // ─── WHAT/WHERE Column (Feature 7.3) ───────────────

        private void BuildWhatColumn(Transform content)
        {
            _whatRows.Clear();
            var vocab = OrderVocabulary.Instance;
            if (vocab == null) return;

            var objects = vocab.GetObjectsForPredicate(_selectedPredicate);

            foreach (var obj in objects)
            {
                bool isSelected = _selectedObjects.Exists(o => o.Id == obj.Id);
                Color bgColor = isSelected ? ROW_SELECTED : ROW_NORMAL;

                var row = CreateRow(content, obj.DisplayName, "", false, false, bgColor, TEXT_NORMAL);

                var capturedObj = obj;
                row.GetComponent<Button>().onClick.AddListener(() => ToggleObject(capturedObj));
                _whatRows.Add((row, obj));
            }
        }

        private void RebuildWhatColumn()
        {
            if (_whatContent == null) return;

            // Destroy old rows
            for (int i = _whatContent.childCount - 1; i >= 0; i--)
                Destroy(_whatContent.GetChild(i).gameObject);
            _whatRows.Clear();

            BuildWhatColumn(_whatContent);
        }

        /// <summary>Feature 7.3: Multiple objects selectable.</summary>
        private void ToggleObject(OrderObject obj)
        {
            int existingIdx = _selectedObjects.FindIndex(o => o.Id == obj.Id);
            if (existingIdx >= 0)
                _selectedObjects.RemoveAt(existingIdx);
            else
                _selectedObjects.Add(obj);

            // Update row highlights
            foreach (var (go, rowObj) in _whatRows)
            {
                if (go == null) continue;
                bool selected = _selectedObjects.Exists(o => o.Id == rowObj.Id);
                go.GetComponent<Image>().color = selected ? ROW_SELECTED : ROW_NORMAL;
            }

            UpdateResultLine();
        }

        // ─── Result Line (Feature 7.4) ──────────────────────

        private void BuildResultLine(Transform parent, float y)
        {
            var resultBg = CreateChild(parent, "ResultLine",
                new Vector2(0, y), new Vector2(PANEL_WIDTH - 40, RESULT_HEIGHT));
            resultBg.AddComponent<Image>().color = new Color(0.06f, 0.08f, 0.06f, 0.9f);

            var textObj = CreateChild(resultBg.transform, "ResultText",
                Vector2.zero, new Vector2(PANEL_WIDTH - 60, RESULT_HEIGHT - 10));
            _resultText = textObj.AddComponent<Text>();
            _resultText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _resultText.fontSize = 20;
            _resultText.color = INVALID_COLOR;
            _resultText.alignment = TextAnchor.MiddleCenter;
            _resultText.horizontalOverflow = HorizontalWrapMode.Wrap;
            _resultText.verticalOverflow = VerticalWrapMode.Overflow;

            // Negation toggle button (left of result)
            var negateBtnObj = CreateChild(parent, "NegateBtn",
                new Vector2(-PANEL_WIDTH / 2 + 50, y), new Vector2(70, 36));
            var negateImg = negateBtnObj.AddComponent<Image>();
            negateImg.color = _isNegated ? NEGATED_COLOR : new Color(0.3f, 0.3f, 0.3f, 0.7f);
            var negateBtn = negateBtnObj.AddComponent<Button>();
            negateBtn.targetGraphic = negateImg;
            negateBtn.onClick.AddListener(() =>
            {
                _isNegated = !_isNegated;
                negateImg.color = _isNegated ? NEGATED_COLOR : new Color(0.3f, 0.3f, 0.3f, 0.7f);
                UpdateResultLine();
            });
            var negateLabel = CreateChild(negateBtnObj.transform, "Label",
                Vector2.zero, new Vector2(70, 36));
            var negateText = negateLabel.AddComponent<Text>();
            negateText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            negateText.fontSize = 14;
            negateText.color = Color.white;
            negateText.alignment = TextAnchor.MiddleCenter;
            negateText.fontStyle = FontStyle.Bold;
            negateText.text = "NICHT";
        }

        private void BuildConfirmButton(Transform parent, float y)
        {
            var btnObj = CreateChild(parent, "ConfirmBtn",
                new Vector2(0, y), new Vector2(220, BUTTON_HEIGHT));
            _confirmBg = btnObj.AddComponent<Image>();
            _confirmBg.color = CONFIRM_INACTIVE;
            _confirmButton = btnObj.AddComponent<Button>();
            _confirmButton.targetGraphic = _confirmBg;
            _confirmButton.onClick.AddListener(ConfirmOrder);

            var labelObj = CreateChild(btnObj.transform, "Label",
                Vector2.zero, new Vector2(220, BUTTON_HEIGHT));
            _confirmLabel = labelObj.AddComponent<Text>();
            _confirmLabel.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _confirmLabel.fontSize = 22;
            _confirmLabel.color = Color.white;
            _confirmLabel.alignment = TextAnchor.MiddleCenter;
            _confirmLabel.fontStyle = FontStyle.Bold;
            _confirmLabel.text = "Give Order";
        }

        private void UpdateResultLine()
        {
            if (_resultText == null) return;

            var order = BuildCurrentOrder();
            string sentence = order.BuildSentence();
            bool valid = order.IsValid();

            _resultText.text = sentence;
            _resultText.color = valid ? VALID_COLOR : INVALID_COLOR;

            if (_isNegated)
                _resultText.fontStyle = FontStyle.Italic;
            else
                _resultText.fontStyle = FontStyle.Normal;

            _confirmBg.color = valid ? CONFIRM_ACTIVE : CONFIRM_INACTIVE;
            _confirmButton.interactable = valid;
        }

        // ─── Order Construction ──────────────────────────────

        private OrderDefinition BuildCurrentOrder()
        {
            var order = new OrderDefinition
            {
                Subject = _selectedSubject,
                SettlerName = _selectedSettlerName,
                Predicate = _selectedPredicate,
                Negated = _isNegated,
                TargetPosition = _tapPosition
            };

            foreach (var obj in _selectedObjects)
                order.Objects.Add(obj);

            return order;
        }

        /// <summary>Feature 7.4: Confirm and create the order.</summary>
        private void ConfirmOrder()
        {
            var order = BuildCurrentOrder();
            if (!order.IsValid()) return;

            var manager = OrderManager.Instance;
            if (manager != null)
            {
                manager.CreateOrder(order);
            }

            Close();
        }

        // ─── UI Helpers ──────────────────────────────────────

        private GameObject CreateRow(Transform parent, string label, string subtitle,
            bool isLocked, bool isBusy, Color bgColor, Color textColor)
        {
            var row = new GameObject($"Row_{label}");
            row.transform.SetParent(parent, false);

            var rowLayout = row.AddComponent<LayoutElement>();
            rowLayout.preferredHeight = ROW_HEIGHT;
            rowLayout.minHeight = ROW_HEIGHT;

            var rowImg = row.AddComponent<Image>();
            rowImg.color = bgColor;

            var rowBtn = row.AddComponent<Button>();
            rowBtn.targetGraphic = rowImg;

            // Main label
            var labelObj = new GameObject("Label");
            labelObj.transform.SetParent(row.transform, false);
            var labelRect = labelObj.AddComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(12, string.IsNullOrEmpty(subtitle) ? 0 : 12);
            labelRect.offsetMax = new Vector2(-12, 0);
            var labelText = labelObj.AddComponent<Text>();
            labelText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            labelText.fontSize = 18;
            labelText.color = textColor;
            labelText.alignment = TextAnchor.MiddleLeft;
            labelText.text = (isLocked ? "  " : "") + label;

            // Lock icon for locked predicates
            if (isLocked)
            {
                var lockObj = new GameObject("LockIcon");
                lockObj.transform.SetParent(row.transform, false);
                var lockRect = lockObj.AddComponent<RectTransform>();
                lockRect.anchorMin = new Vector2(1, 0.5f);
                lockRect.anchorMax = new Vector2(1, 0.5f);
                lockRect.pivot = new Vector2(1, 0.5f);
                lockRect.anchoredPosition = new Vector2(-8, 0);
                lockRect.sizeDelta = new Vector2(24, 24);
                var lockImg = lockObj.AddComponent<Image>();
                lockImg.color = new Color(0.6f, 0.4f, 0.2f, 0.8f);
            }

            // Subtitle (trait, busy state, requirement)
            if (!string.IsNullOrEmpty(subtitle))
            {
                var subObj = new GameObject("Subtitle");
                subObj.transform.SetParent(row.transform, false);
                var subRect = subObj.AddComponent<RectTransform>();
                subRect.anchorMin = Vector2.zero;
                subRect.anchorMax = new Vector2(1, 0.4f);
                subRect.offsetMin = new Vector2(12, 2);
                subRect.offsetMax = new Vector2(-12, 0);
                var subText = subObj.AddComponent<Text>();
                subText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                subText.fontSize = 12;
                subText.color = isLocked ? new Color(0.7f, 0.5f, 0.3f) : TEXT_BUSY;
                subText.alignment = TextAnchor.MiddleLeft;
                subText.text = subtitle;
            }

            return row;
        }

        private GameObject CreateChild(Transform parent, string name, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = pos;
            rect.sizeDelta = size;
            return go;
        }
    }
}
