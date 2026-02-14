using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using Terranova.Core;
using Terranova.Population;
using Terranova.Buildings;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;

namespace Terranova.Input
{
    /// <summary>
    /// Handles tap selection, deselection, long press, and highlight rings.
    /// Supports both mouse (left click) and touch (single finger tap).
    ///
    /// Story 6.1: Tap on settler/building → publish SelectionChangedEvent
    /// Story 6.2: Tap on empty terrain → deselect
    /// Story 6.3: Long press (>500ms) → publish with IsDetailView = true
    /// Story 6.4: Highlight ring on selected object
    ///
    /// Gesture Lexicon: SEL-01 (tap unit), SEL-02 (tap building),
    ///                  SEL-03 (long press), SEL-04 (tap empty = deselect)
    /// </summary>
    public class SelectionManager : MonoBehaviour
    {
        // ─── Settings ──────────────────────────────────────────

        private const float LONG_PRESS_DURATION = 0.5f;  // 500ms for detail view
        private const float RAYCAST_DISTANCE = 500f;
        private const float HIGHLIGHT_RING_RADIUS = 0.8f;
        private const int HIGHLIGHT_SEGMENTS = 32;
        private const float HIGHLIGHT_LINE_WIDTH = 0.05f;
        private const float TAP_MAX_DRIFT = 10f; // Max pixels finger can move and still count as tap

        private static readonly Color HIGHLIGHT_COLOR = new Color(1f, 0.9f, 0.2f, 0.9f);

        // ─── State ─────────────────────────────────────────────

        private Mouse _mouse;
        private GameObject _selectedObject;
        private GameObject _highlightRing;

        // Mouse input state
        private bool _isPressingDown;
        private float _pressTimer;
        private Vector2 _pressStartPosition;
        private bool _longPressTriggered;

        // Touch input state
        private bool _touchDown;
        private float _touchTimer;
        private Vector2 _touchStartPos;
        private bool _touchLongPressTriggered;
        private bool _touchCancelled; // Set when multi-touch or drag detected

        /// <summary>Currently selected object (settler or building).</summary>
        public GameObject SelectedObject => _selectedObject;

        // ─── Lifecycle ─────────────────────────────────────────

        private void OnEnable()
        {
            EnhancedTouchSupport.Enable();
        }

        private void OnDisable()
        {
            EnhancedTouchSupport.Disable();
        }

        private void Start()
        {
            _mouse = Mouse.current;
        }

        private void Update()
        {
            // Don't process selection when clicking/tapping on UI
            // (build menu, speed controls, discovery popup dismiss are handled by EventSystem)
            bool mouseOverUI = _mouse != null &&
                EventSystem.current != null &&
                EventSystem.current.IsPointerOverGameObject();

            if (!mouseOverUI)
                HandleMouseInput();

            HandleTouchInput();
        }

        // ─── Mouse Input ──────────────────────────────────────

        private void HandleMouseInput()
        {
            if (_mouse == null) return;

            // Left mouse button pressed
            if (_mouse.leftButton.wasPressedThisFrame)
            {
                _isPressingDown = true;
                _pressTimer = 0f;
                _pressStartPosition = _mouse.position.ReadValue();
                _longPressTriggered = false;
            }

            // Holding — check for long press
            if (_isPressingDown)
            {
                _pressTimer += Time.unscaledDeltaTime; // Unscaled so it works while paused

                // Check for drag (not a tap/long press if mouse moved too far)
                Vector2 currentPos = _mouse.position.ReadValue();
                if (Vector2.Distance(currentPos, _pressStartPosition) > TAP_MAX_DRIFT)
                {
                    _isPressingDown = false;
                    return;
                }

                // Long press triggered
                if (!_longPressTriggered && _pressTimer >= LONG_PRESS_DURATION)
                {
                    _longPressTriggered = true;
                    TrySelect(_pressStartPosition, isDetailView: true);
                }
            }

            // Left mouse button released
            if (_mouse.leftButton.wasReleasedThisFrame)
            {
                if (_isPressingDown && !_longPressTriggered)
                {
                    // Short tap — normal selection
                    TrySelect(_pressStartPosition, isDetailView: false);
                }

                _isPressingDown = false;
            }
        }

        // ─── Touch Input ──────────────────────────────────────

        /// <summary>
        /// Handle single-finger tap and long press for selection.
        /// Processes each touch by phase for reliable detection (handles fast taps
        /// where Began and Ended arrive in the same frame).
        /// Multi-touch and drag cancel the tap gesture.
        /// Two-finger gestures (pinch, zoom) are handled by RTSCameraController.
        /// </summary>
        private void HandleTouchInput()
        {
            // Multi-touch cancels any tap detection
            if (Touch.activeFingers.Count >= 2)
            {
                _touchCancelled = true;
            }

            // Process each touch event by phase
            foreach (var touch in Touch.activeTouches)
            {
                switch (touch.phase)
                {
                    case UnityEngine.InputSystem.TouchPhase.Began:
                        if (_touchDown) break;

                        // Skip if over UI (buttons handle their own taps via EventSystem)
                        if (EventSystem.current != null &&
                            EventSystem.current.IsPointerOverGameObject(touch.finger.index))
                            break;

                        _touchDown = true;
                        _touchTimer = 0f;
                        _touchStartPos = touch.screenPosition;
                        _touchLongPressTriggered = false;
                        _touchCancelled = false;
                        break;

                    case UnityEngine.InputSystem.TouchPhase.Moved:
                    case UnityEngine.InputSystem.TouchPhase.Stationary:
                        if (!_touchDown || _touchCancelled) break;

                        _touchTimer += Time.unscaledDeltaTime;

                        // Finger drifted too far → it's a pan, not a tap
                        if (Vector2.Distance(touch.screenPosition, _touchStartPos) > TAP_MAX_DRIFT)
                        {
                            _touchCancelled = true;
                        }

                        // Long press: select with detail view
                        if (!_touchLongPressTriggered && !_touchCancelled &&
                            _touchTimer >= LONG_PRESS_DURATION)
                        {
                            _touchLongPressTriggered = true;
                            TrySelect(_touchStartPos, isDetailView: true);
                        }
                        break;

                    case UnityEngine.InputSystem.TouchPhase.Ended:
                    case UnityEngine.InputSystem.TouchPhase.Canceled:
                        if (_touchDown && !_touchLongPressTriggered && !_touchCancelled)
                        {
                            TrySelect(_touchStartPos, isDetailView: false);
                        }
                        _touchDown = false;
                        break;
                }
            }
        }

        // ─── Selection Logic ────────────────────────────────────

        /// <summary>
        /// Raycast from screen position and select settler/building if hit.
        /// Story 6.1: Tap selection. Story 6.3: Long press detail view.
        /// </summary>
        private void TrySelect(Vector2 screenPos, bool isDetailView)
        {
            Ray ray = UnityEngine.Camera.main.ScreenPointToRay(screenPos);

            // QueryTriggerInteraction.Collide: settler body collider is a trigger
            if (!Physics.Raycast(ray, out RaycastHit hit, RAYCAST_DISTANCE,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide))
            {
                Deselect();
                return;
            }

            // Check if we hit a settler or building (walk up the hierarchy)
            var settler = hit.collider.GetComponentInParent<Settler>();
            if (settler != null)
            {
                Select(settler.gameObject, isDetailView);
                return;
            }

            var building = hit.collider.GetComponentInParent<Building>();
            if (building != null)
            {
                Select(building.gameObject, isDetailView);
                return;
            }

            // Hit terrain or other — deselect (Story 6.2)
            Deselect();
        }

        /// <summary>
        /// Select an object: update highlight ring and publish event.
        /// </summary>
        private void Select(GameObject obj, bool isDetailView)
        {
            // Same object, same view type — no change
            if (obj == _selectedObject && !isDetailView)
                return;

            _selectedObject = obj;
            CreateHighlightRing(obj);

            EventBus.Publish(new SelectionChangedEvent
            {
                SelectedObject = obj,
                IsDetailView = isDetailView
            });

            Debug.Log($"[Selection] Selected: {obj.name} (detail={isDetailView})");
        }

        /// <summary>
        /// Deselect current object: remove highlight, close panel.
        /// Story 6.2: Tap on empty terrain.
        /// </summary>
        private void Deselect()
        {
            if (_selectedObject == null) return;

            _selectedObject = null;
            DestroyHighlightRing();

            EventBus.Publish(new SelectionChangedEvent
            {
                SelectedObject = null,
                IsDetailView = false
            });

            Debug.Log("[Selection] Deselected");
        }

        // ─── Highlight Ring (Story 6.4) ─────────────────────────

        /// <summary>
        /// Create a yellow ring at the feet of the selected object.
        /// Uses a LineRenderer forming a circle on the ground.
        /// </summary>
        private void CreateHighlightRing(GameObject target)
        {
            DestroyHighlightRing();

            _highlightRing = new GameObject("SelectionRing");
            _highlightRing.transform.SetParent(target.transform, false);
            _highlightRing.transform.localPosition = new Vector3(0f, 0.05f, 0f);

            var lineRenderer = _highlightRing.AddComponent<LineRenderer>();
            lineRenderer.useWorldSpace = false;
            lineRenderer.loop = true;
            lineRenderer.positionCount = HIGHLIGHT_SEGMENTS;
            lineRenderer.startWidth = HIGHLIGHT_LINE_WIDTH;
            lineRenderer.endWidth = HIGHLIGHT_LINE_WIDTH;
            lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lineRenderer.receiveShadows = false;

            // Use unlit material for consistent visibility
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                         ?? Shader.Find("Sprites/Default");
            if (shader != null)
            {
                var mat = new Material(shader);
                mat.SetColor("_BaseColor", HIGHLIGHT_COLOR);
                mat.color = HIGHLIGHT_COLOR;
                lineRenderer.material = mat;
            }

            lineRenderer.startColor = HIGHLIGHT_COLOR;
            lineRenderer.endColor = HIGHLIGHT_COLOR;

            // Determine ring radius based on object type
            float radius = HIGHLIGHT_RING_RADIUS;
            var building = target.GetComponent<Building>();
            if (building != null && building.Definition != null)
            {
                // Larger ring for buildings
                radius = Mathf.Max(building.Definition.FootprintSize.x,
                                   building.Definition.FootprintSize.y) * 0.7f;
            }

            // Generate circle points
            for (int i = 0; i < HIGHLIGHT_SEGMENTS; i++)
            {
                float angle = i * Mathf.PI * 2f / HIGHLIGHT_SEGMENTS;
                float x = Mathf.Cos(angle) * radius;
                float z = Mathf.Sin(angle) * radius;
                lineRenderer.SetPosition(i, new Vector3(x, 0f, z));
            }
        }

        private void DestroyHighlightRing()
        {
            if (_highlightRing != null)
            {
                Destroy(_highlightRing);
                _highlightRing = null;
            }
        }

        // ─── Cleanup ────────────────────────────────────────────

        private void OnDestroy()
        {
            DestroyHighlightRing();
        }
    }
}
