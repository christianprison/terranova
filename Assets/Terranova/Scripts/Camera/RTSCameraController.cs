using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;

namespace Terranova.Camera
{
    /// <summary>
    /// RTS-style camera for viewing the voxel terrain.
    ///
    /// Controls:
    ///   Keyboard/Mouse (always active):
    ///     Pan:    WASD or Arrow keys
    ///     Zoom:   Mouse scroll wheel
    ///     Rotate: Hold middle mouse button + move mouse, or Q/E keys
    ///
    ///   Touch (iPad):
    ///     Pan:    One-finger drag
    ///     Zoom:   Two-finger pinch
    ///     Rotate: Two-finger twist (snaps to 90° on release)
    ///
    /// The camera looks down at an angle (like Anno, Settlers, Age of Empires).
    /// It stays above the terrain and clamps to world boundaries.
    ///
    /// Gesture Lexicon v0.4: CAM-01 (pan), CAM-02 (pinch zoom), CAM-03 (rotate)
    /// </summary>
    [RequireComponent(typeof(UnityEngine.Camera))]
    public class RTSCameraController : MonoBehaviour
    {
        [Header("Movement")]
        [Tooltip("Camera pan speed in units/second.")]
        [SerializeField] private float _panSpeed = 30f;

        [Tooltip("Speed multiplier when holding Shift.")]
        [SerializeField] private float _fastMultiplier = 2.5f;

        [Header("Zoom")]
        [Tooltip("Closest zoom distance (units above ground).")]
        [SerializeField] private float _minZoom = 10f;

        [Tooltip("Farthest zoom distance.")]
        [SerializeField] private float _maxZoom = 120f;

        [Tooltip("How fast the scroll wheel zooms.")]
        [SerializeField] private float _zoomSpeed = 15f;

        [Tooltip("Smoothing factor for zoom. Higher = smoother but more laggy.")]
        [SerializeField] private float _zoomSmoothing = 8f;

        [Header("Rotation")]
        [Tooltip("Mouse rotation speed (degrees per pixel of mouse movement).")]
        [SerializeField] private float _rotateSpeed = 0.3f;

        [Tooltip("Smoothing speed for snap-to-90° when releasing MMB.")]
        [SerializeField] private float _snapSmoothing = 10f;

        [Header("Touch")]
        [Tooltip("Safe zone in points from screen edges. Touch inside this zone is ignored to prevent accidental gestures. GDD spec: 20pt.")]
        [SerializeField] private float _safeZonePoints = 20f;

        [Tooltip("Touch pan sensitivity. Higher = faster panning per pixel dragged.")]
        [SerializeField] private float _touchPanScale = 0.003f;

        [Tooltip("Pinch zoom sensitivity.")]
        [SerializeField] private float _pinchZoomScale = 0.005f;

        [Header("Initial Position")]
        [Tooltip("Starting camera angle (degrees from horizontal). 60 = steep top-down, 30 = more side view.")]
        [SerializeField] private float _defaultPitch = 50f;

        [Tooltip("Starting height above terrain.")]
        [SerializeField] private float _defaultHeight = 60f;

        // Current zoom level (interpolated smoothly)
        private float _currentZoom;
        private float _targetZoom;

        // Camera rig: the script controls a pivot point on the ground;
        // the actual camera is offset from this point.
        private Vector3 _pivotPosition;
        private float _yaw;
        private float _targetYaw;
        private bool _isRotatingWithMouse;
        private bool _isSnapping;
        private bool _initialized;

        // ─── Touch gesture state ──────────────────────────────────
        private int _prevFingerCount;
        private bool _wasTwoFingerGesture; // True from 2-finger start until all fingers lift
        private Vector2 _touchPanPrev;
        private bool _touchPanTracking;
        private float _prevPinchDist;
        private float _prevTwoFingerAngle;

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
            _currentZoom = _defaultHeight;
            _targetZoom = _defaultHeight;
            _yaw = 0f;
            _pivotPosition = new Vector3(64, 64, 64);
            _prevPinchDist = -1f;
            _prevTwoFingerAngle = float.MinValue;
            UpdateCameraTransform();
        }

        private void Update()
        {
            // Defer position setup until the world is generated
            // (WorldManager.Start may run after this script's Start)
            if (!_initialized)
            {
                var world = Terranova.Terrain.WorldManager.Instance;
                if (world != null && world.WorldBlocksX > 0)
                {
                    float centerX = world.WorldBlocksX * 0.5f;
                    float centerZ = world.WorldBlocksZ * 0.5f;
                    float surfaceY = world.GetSmoothedHeightAtWorldPos(centerX, centerZ);
                    if (surfaceY > 0)
                    {
                        _pivotPosition = new Vector3(centerX, surfaceY, centerZ);
                        _initialized = true;
                        Debug.Log($"Camera initialized: pivot=({centerX}, {surfaceY}, {centerZ})");
                    }
                }
            }

            HandlePan();
            HandleZoom();
            HandleRotation();
            HandleTouch();
            ClampToWorldBounds();
            UpdateCameraTransform();
        }

        // ─── Keyboard/Mouse Input ─────────────────────────────────

        /// <summary>
        /// WASD/Arrow keys to pan the camera across the terrain.
        /// Movement direction is relative to the camera's current facing.
        /// </summary>
        private void HandlePan()
        {
            var kb = Keyboard.current;
            if (kb == null) return;

            float horizontal = 0f;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) horizontal += 1f;
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) horizontal -= 1f;

            float vertical = 0f;
            if (kb.wKey.isPressed || kb.upArrowKey.isPressed) vertical += 1f;
            if (kb.sKey.isPressed || kb.downArrowKey.isPressed) vertical -= 1f;

            if (Mathf.Approximately(horizontal, 0) && Mathf.Approximately(vertical, 0))
                return;

            // Calculate movement direction relative to camera's yaw rotation
            Vector3 forward = Quaternion.Euler(0, _yaw, 0) * Vector3.forward;
            Vector3 right = Quaternion.Euler(0, _yaw, 0) * Vector3.right;

            Vector3 move = (forward * vertical + right * horizontal).normalized;

            // Speed scales with zoom level (panning feels consistent at all zoom levels)
            float speedFactor = _currentZoom / _defaultHeight;
            float speed = _panSpeed * speedFactor;

            // Hold Shift to move faster
            if (kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed)
                speed *= _fastMultiplier;

            _pivotPosition += move * speed * Time.deltaTime;
        }

        /// <summary>
        /// Scroll wheel to zoom in/out.
        /// </summary>
        private void HandleZoom()
        {
            var mouse = Mouse.current;
            if (mouse == null) return;

            float scroll = mouse.scroll.ReadValue().y;
            if (!Mathf.Approximately(scroll, 0))
            {
                // scroll.y is typically +-120 per notch, normalize it
                float normalizedScroll = scroll / 120f;
                _targetZoom -= normalizedScroll * _zoomSpeed * (_targetZoom * 0.3f);
                _targetZoom = Mathf.Clamp(_targetZoom, _minZoom, _maxZoom);
            }

            // Smooth zoom interpolation
            _currentZoom = Mathf.Lerp(_currentZoom, _targetZoom, Time.deltaTime * _zoomSmoothing);
        }

        /// <summary>
        /// Rotate the camera via MMB drag or Q/E keys.
        /// MMB drag: free rotation while held, snaps to nearest 90° on release.
        /// Q/E keys: instant snap to next 90° step.
        /// Ref: Gesture Lexicon v0.4, CAM-03.
        /// </summary>
        private void HandleRotation()
        {
            var mouse = Mouse.current;
            var kb = Keyboard.current;

            // MMB drag rotation (free rotation while held, snap on release)
            if (mouse != null)
            {
                if (mouse.middleButton.isPressed)
                {
                    float mouseDelta = mouse.delta.ReadValue().x;
                    _yaw += mouseDelta * _rotateSpeed;
                    _isRotatingWithMouse = true;
                    _isSnapping = false;
                }
                else if (_isRotatingWithMouse)
                {
                    // MMB released – start snapping to nearest 90°
                    _isRotatingWithMouse = false;
                    _targetYaw = Mathf.Round(_yaw / 90f) * 90f;
                    _isSnapping = true;
                }
            }

            // Q/E keys: instant snap to next 90° step
            if (kb != null && !_isRotatingWithMouse)
            {
                if (kb.eKey.wasPressedThisFrame)
                {
                    _targetYaw = Mathf.Ceil((_yaw + 1f) / 90f) * 90f;
                    _isSnapping = true;
                }
                else if (kb.qKey.wasPressedThisFrame)
                {
                    _targetYaw = Mathf.Floor((_yaw - 1f) / 90f) * 90f;
                    _isSnapping = true;
                }
            }

            // Smoothly interpolate to snap target
            if (_isSnapping)
            {
                _yaw = Mathf.Lerp(_yaw, _targetYaw, Time.deltaTime * _snapSmoothing);
                if (Mathf.Abs(_yaw - _targetYaw) < 0.5f)
                {
                    _yaw = _targetYaw;
                    _isSnapping = false;
                }
            }
        }

        // ─── Touch Input ──────────────────────────────────────────

        /// <summary>
        /// Process touch gestures: one-finger pan, pinch zoom, two-finger rotate.
        /// Uses EnhancedTouch for reliable multi-finger tracking.
        /// All gestures coexist with mouse/keyboard (both active simultaneously).
        /// </summary>
        private void HandleTouch()
        {
            int fingerCount = Touch.activeFingers.Count;

            // Detect transition: two-finger gesture ending → snap rotation
            if (_prevFingerCount >= 2 && fingerCount < 2)
            {
                if (_isRotatingWithMouse) // Flag reused for touch rotation
                {
                    _targetYaw = Mathf.Round(_yaw / 90f) * 90f;
                    _isSnapping = true;
                    _isRotatingWithMouse = false;
                }
                _prevPinchDist = -1f;
                _prevTwoFingerAngle = float.MinValue;
            }

            // Track multi-touch lifecycle: set when 2+ fingers, clear when 0
            if (fingerCount >= 2) _wasTwoFingerGesture = true;
            if (fingerCount == 0)
            {
                _wasTwoFingerGesture = false;
                _touchPanTracking = false;
            }

            // One-finger drag → pan (only if not transitioning out of pinch/rotate)
            if (fingerCount == 1 && !_wasTwoFingerGesture)
            {
                HandleTouchPan(Touch.activeFingers[0].currentTouch);
            }
            // Two-finger → simultaneous pinch zoom + rotation
            else if (fingerCount >= 2)
            {
                var t0 = Touch.activeFingers[0].currentTouch;
                var t1 = Touch.activeFingers[1].currentTouch;
                HandlePinchZoom(t0, t1);
                HandleTwoFingerRotation(t0, t1);
                _touchPanTracking = false;
            }

            _prevFingerCount = fingerCount;
        }

        /// <summary>
        /// One-finger drag pans the camera. Dragging right moves the world right
        /// under the camera (camera moves left relative to world).
        /// Speed scales with zoom for consistent feel at all distances.
        /// Gesture Lexicon: CAM-01.
        /// </summary>
        private void HandleTouchPan(Touch touch)
        {
            if (IsInSafeZone(touch.screenPosition)) return;

            // Skip touches over UI (build menu, speed buttons, etc.)
            if (EventSystem.current != null &&
                EventSystem.current.IsPointerOverGameObject(touch.finger.index))
                return;

            // First frame of tracking: record position, don't pan yet
            if (!_touchPanTracking)
            {
                _touchPanPrev = touch.screenPosition;
                _touchPanTracking = true;
                return;
            }

            Vector2 delta = touch.screenPosition - _touchPanPrev;
            _touchPanPrev = touch.screenPosition;

            if (delta.sqrMagnitude < 0.01f) return;

            // Convert screen delta to world movement relative to camera yaw
            Vector3 forward = Quaternion.Euler(0, _yaw, 0) * Vector3.forward;
            Vector3 right = Quaternion.Euler(0, _yaw, 0) * Vector3.right;

            // Inverted: drag finger right → world slides right → camera moves left
            float panFactor = _currentZoom * _touchPanScale;
            _pivotPosition -= right * delta.x * panFactor;
            _pivotPosition -= forward * delta.y * panFactor;
        }

        /// <summary>
        /// Two-finger pinch to zoom. Fingers apart = zoom in, together = zoom out.
        /// Gesture Lexicon: CAM-02.
        /// </summary>
        private void HandlePinchZoom(Touch t0, Touch t1)
        {
            float dist = Vector2.Distance(t0.screenPosition, t1.screenPosition);

            if (_prevPinchDist > 0)
            {
                float pinchDelta = dist - _prevPinchDist;
                _targetZoom -= pinchDelta * (_targetZoom * _pinchZoomScale);
                _targetZoom = Mathf.Clamp(_targetZoom, _minZoom, _maxZoom);
            }

            _prevPinchDist = dist;
        }

        /// <summary>
        /// Two-finger twist to rotate the camera. Snaps to nearest 90° on release
        /// (handled in HandleTouch when finger count drops below 2).
        /// Gesture Lexicon: CAM-03.
        /// </summary>
        private void HandleTwoFingerRotation(Touch t0, Touch t1)
        {
            Vector2 diff = t1.screenPosition - t0.screenPosition;
            float angle = Mathf.Atan2(diff.y, diff.x) * Mathf.Rad2Deg;

            if (_prevTwoFingerAngle > float.MinValue + 1f)
            {
                float angleDelta = Mathf.DeltaAngle(_prevTwoFingerAngle, angle);
                _yaw -= angleDelta;
                _isRotatingWithMouse = true; // Reuse flag so snap-to-90° triggers on release
                _isSnapping = false;
            }

            _prevTwoFingerAngle = angle;
        }

        // ─── Shared Utilities ─────────────────────────────────────

        /// <summary>
        /// Keep the camera pivot within world boundaries and follow terrain height.
        /// The pivot Y smoothly tracks the terrain surface so the camera doesn't
        /// sink into hills or float above valleys when panning.
        /// </summary>
        private void ClampToWorldBounds()
        {
            var world = Terranova.Terrain.WorldManager.Instance;
            if (world == null)
                return;

            float padding = 10f;
            _pivotPosition.x = Mathf.Clamp(_pivotPosition.x, -padding, world.WorldBlocksX + padding);
            _pivotPosition.z = Mathf.Clamp(_pivotPosition.z, -padding, world.WorldBlocksZ + padding);

            // Follow smooth terrain height so the camera stays grounded when panning (Story 0.6)
            float terrainHeight = world.GetSmoothedHeightAtWorldPos(
                _pivotPosition.x, _pivotPosition.z);
            _pivotPosition.y = Mathf.Lerp(_pivotPosition.y, terrainHeight,
                Time.deltaTime * _zoomSmoothing);
        }

        /// <summary>
        /// Check if a screen position falls within the safe zone (too close to edges).
        /// Touch input in the safe zone is ignored to prevent accidental gestures
        /// when holding the iPad.
        /// </summary>
        public bool IsInSafeZone(Vector2 screenPosition)
        {
            // Convert points to pixels (on iPad, 1pt ≈ 2px at 2x scale)
            float safePixels = _safeZonePoints * (Screen.dpi > 0 ? Screen.dpi / 163f : 1f);

            return screenPosition.x < safePixels
                || screenPosition.x > Screen.width - safePixels
                || screenPosition.y < safePixels
                || screenPosition.y > Screen.height - safePixels;
        }

        /// <summary>
        /// Position the actual camera based on pivot, zoom, pitch, and yaw.
        /// The camera orbits above the pivot point looking down at an angle.
        /// </summary>
        private void UpdateCameraTransform()
        {
            // Calculate camera offset from pivot based on pitch angle and zoom distance
            Quaternion rotation = Quaternion.Euler(_defaultPitch, _yaw, 0);
            Vector3 offset = rotation * new Vector3(0, 0, -_currentZoom);

            transform.position = _pivotPosition + offset;
            transform.rotation = rotation;
        }
    }
}
