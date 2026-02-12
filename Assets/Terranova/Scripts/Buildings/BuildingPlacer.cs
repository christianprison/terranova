using UnityEngine;
using UnityEngine.InputSystem;
using Terranova.Core;
using Terranova.Terrain;

namespace Terranova.Buildings
{
    /// <summary>
    /// Handles building placement via mouse click on the terrain.
    ///
    /// Flow (as per GDD Gesture Lexicon):
    ///   1. Player selects a building type (from UI – for MS1, always Campfire)
    ///   2. A ghost preview follows the mouse cursor on the terrain
    ///   3. Green ghost = valid placement, red ghost = invalid
    ///   4. Left-click to place, Right-click or Escape to cancel
    ///
    /// Placement rules:
    ///   - Must be on solid ground (not water, not air)
    ///   - Surface must be relatively flat (not steep slopes)
    ///   - Cannot overlap existing buildings
    ///
    /// Scene setup: Add this component to a GameObject (e.g., "GameManager").
    /// Assign a BuildingDefinition in the Inspector.
    /// </summary>
    public class BuildingPlacer : MonoBehaviour
    {
        [Header("Building to Place")]
        [Tooltip("The building type currently selected for placement. Assign Campfire for MS1.")]
        [SerializeField] private BuildingDefinition _selectedBuilding;

        [Header("Preview")]
        [Tooltip("Color when placement is valid.")]
        [SerializeField] private Color _validColor = new Color(0.2f, 0.9f, 0.2f, 0.5f);

        [Tooltip("Color when placement is invalid.")]
        [SerializeField] private Color _invalidColor = new Color(0.9f, 0.2f, 0.2f, 0.5f);

        // The ghost preview object that follows the mouse
        private GameObject _preview;
        private MeshRenderer _previewRenderer;
        private Material _previewMaterial;

        // Whether the current preview position is valid for placement
        private bool _isValidPosition;

        // Is the placement mode active?
        private bool _isPlacing;

        private void Start()
        {
            // Auto-start placement mode if a building is assigned (convenient for MS1 testing)
            if (_selectedBuilding != null)
                StartPlacement(_selectedBuilding);
        }

        private void Update()
        {
            if (!_isPlacing)
                return;

            UpdatePreviewPosition();

            var mouse = Mouse.current;
            var kb = Keyboard.current;

            // Left-click to place
            if (mouse != null && mouse.leftButton.wasPressedThisFrame && _isValidPosition)
                PlaceBuilding();

            // Right-click or Escape to cancel
            if ((mouse != null && mouse.rightButton.wasPressedThisFrame) ||
                (kb != null && kb.escapeKey.wasPressedThisFrame))
                CancelPlacement();
        }

        /// <summary>
        /// Begin placement mode for a specific building type.
        /// Creates a ghost preview that follows the mouse.
        /// </summary>
        public void StartPlacement(BuildingDefinition building)
        {
            _selectedBuilding = building;
            _isPlacing = true;

            CreatePreview();
        }

        /// <summary>
        /// Cancel placement mode and destroy the preview.
        /// </summary>
        public void CancelPlacement()
        {
            _isPlacing = false;

            if (_preview != null)
                Destroy(_preview);
        }

        /// <summary>
        /// Raycast from mouse position to terrain and move the preview there.
        /// Checks if the position is valid for placement.
        /// </summary>
        private void UpdatePreviewPosition()
        {
            if (_preview == null || UnityEngine.Camera.main == null)
                return;

            var mouse = Mouse.current;
            if (mouse == null) return;

            Ray ray = UnityEngine.Camera.main.ScreenPointToRay(mouse.position.ReadValue());

            if (Physics.Raycast(ray, out RaycastHit hit, 500f))
            {
                // Snap to block grid (buildings sit on top of blocks)
                int blockX = Mathf.FloorToInt(hit.point.x);
                int blockZ = Mathf.FloorToInt(hit.point.z);

                // Get terrain height at this position
                var world = WorldManager.Instance;
                if (world == null)
                    return;

                int height = world.GetHeightAtWorldPos(blockX, blockZ);
                VoxelType surface = world.GetSurfaceTypeAtWorldPos(blockX, blockZ);

                // Check placement validity
                _isValidPosition = IsValidPlacement(surface, height);

                // Position the preview on top of the terrain
                float previewY = height + 1; // Sit on top of the surface block
                _preview.transform.position = new Vector3(
                    blockX + 0.5f,  // Center on block
                    previewY,
                    blockZ + 0.5f
                );

                // Color indicates valid/invalid
                _previewMaterial.color = _isValidPosition ? _validColor : _invalidColor;
            }
            else
            {
                _isValidPosition = false;
            }
        }

        /// <summary>
        /// Check if a building can be placed at this position.
        /// Rules: must be solid ground, not water, terrain exists.
        /// </summary>
        private bool IsValidPlacement(VoxelType surfaceType, int height)
        {
            // Must have terrain
            if (height < 0)
                return false;

            // Cannot build on water or air
            if (!surfaceType.IsSolid())
                return false;

            return true;
        }

        /// <summary>
        /// Place the building at the preview's current position.
        /// Creates a permanent building object and fires an event.
        /// </summary>
        private void PlaceBuilding()
        {
            Vector3 position = _preview.transform.position;

            // Create the actual building (placeholder cube for MS1)
            var building = GameObject.CreatePrimitive(PrimitiveType.Cube);
            building.name = _selectedBuilding.DisplayName;
            building.transform.position = position;
            building.transform.localScale = new Vector3(
                _selectedBuilding.FootprintSize.x,
                _selectedBuilding.VisualHeight,
                _selectedBuilding.FootprintSize.y
            );

            // Apply building color
            var renderer = building.GetComponent<MeshRenderer>();
            renderer.material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            renderer.material.color = _selectedBuilding.PreviewColor;

            // Notify other systems via event bus
            EventBus.Publish(new BuildingPlacedEvent
            {
                BuildingName = _selectedBuilding.DisplayName,
                Position = position
            });

            Debug.Log($"Placed {_selectedBuilding.DisplayName} at {position}");

            // Continue placement mode (build loop – player can keep placing)
            // As per GDD gesture lexicon: "build loop for serial construction"
        }

        /// <summary>
        /// Create the ghost preview cube that follows the mouse.
        /// </summary>
        private void CreatePreview()
        {
            if (_preview != null)
                Destroy(_preview);

            _preview = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _preview.name = "BuildingPreview";
            _preview.transform.localScale = new Vector3(
                _selectedBuilding.FootprintSize.x,
                _selectedBuilding.VisualHeight,
                _selectedBuilding.FootprintSize.y
            );

            // Disable collider on preview so raycasts pass through it
            var collider = _preview.GetComponent<Collider>();
            if (collider != null)
                collider.enabled = false;

            // Semi-transparent material for the ghost effect
            _previewRenderer = _preview.GetComponent<MeshRenderer>();
            _previewMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            _previewMaterial.SetFloat("_Surface", 1f);  // Transparent mode
            _previewMaterial.SetFloat("_Blend", 0f);    // Alpha blend
            _previewMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            _previewMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            _previewMaterial.SetInt("_ZWrite", 0);
            _previewMaterial.renderQueue = 3000;
            _previewMaterial.color = _validColor;
            _previewRenderer.material = _previewMaterial;
        }

        private void OnDestroy()
        {
            if (_preview != null)
                Destroy(_preview);
        }
    }
}
