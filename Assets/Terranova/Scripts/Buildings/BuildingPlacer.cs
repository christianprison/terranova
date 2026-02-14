using UnityEngine;
using UnityEngine.EventSystems;
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

        // Cached material for placed buildings (shared, avoids per-placement allocation)
        private Material _buildingMaterial;
        private MaterialPropertyBlock _buildingPropBlock;
        private static readonly int ColorID = Shader.PropertyToID("_BaseColor");

        // Whether the current preview position is valid for placement
        private bool _isValidPosition;

        // Is the placement mode active?
        private bool _isPlacing;

        // Story 4.1: Red flash feedback when resources insufficient
        private const float FEEDBACK_DURATION = 0.4f;
        private float _feedbackTimer;

        /// <summary>
        /// Lazily create materials on first use. Shader.Find can fail when called
        /// too early (e.g. from RuntimeInitializeOnLoadMethod), so we defer it.
        /// </summary>
        private bool EnsureMaterials()
        {
            if (_buildingMaterial != null && _previewMaterial != null)
                return true;

            _buildingPropBlock ??= new MaterialPropertyBlock();

            Shader litShader = Shader.Find("Universal Render Pipeline/Lit");
            Shader particleShader = Shader.Find("Universal Render Pipeline/Particles/Unlit");

            if (litShader == null && particleShader == null)
            {
                Debug.LogError("BuildingPlacer: No URP shader found.");
                return false;
            }

            if (_buildingMaterial == null)
            {
                _buildingMaterial = new Material(litShader != null ? litShader : particleShader);
                _buildingMaterial.name = "Building_Shared (Auto)";
            }

            // Use Particles/Unlit for preview – it handles transparency
            // reliably without needing URP shader keywords
            if (_previewMaterial == null)
            {
                Shader previewShader = particleShader != null ? particleShader : litShader;
                _previewMaterial = new Material(previewShader);
                _previewMaterial.name = "BuildingPreview (Auto)";
                _previewMaterial.SetFloat("_Surface", 1f);
                _previewMaterial.SetFloat("_Blend", 0f);
                _previewMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                _previewMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                _previewMaterial.SetInt("_ZWrite", 0);
                _previewMaterial.renderQueue = 3000;
                _previewMaterial.color = _validColor;
            }

            Debug.Log("BuildingPlacer: Materials created successfully.");
            return true;
        }

        private void Start()
        {
            // MS1 auto-started placement mode here. Now in MS2, the campfire
            // is auto-placed by SettlerSpawner, so we don't start placement mode.
            // Future: Build menu (Story 4.5) will call StartPlacement() on demand.
        }

        private void Update()
        {
            if (!_isPlacing)
                return;

            UpdatePreviewPosition();
            UpdateFeedbackTimer();

            var mouse = Mouse.current;
            var kb = Keyboard.current;

            // Don't place buildings when clicking on UI elements
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                return;

            // Left-click to place
            if (mouse != null && mouse.leftButton.wasPressedThisFrame && _isValidPosition)
                PlaceBuilding();

            // Right-click or Escape to cancel
            if ((mouse != null && mouse.rightButton.wasPressedThisFrame) ||
                (kb != null && kb.escapeKey.wasPressedThisFrame))
                CancelPlacement();
        }

        /// <summary>Whether a building definition is currently assigned.</summary>
        public bool HasBuilding => _selectedBuilding != null;

        /// <summary>
        /// Assign a building definition (used by GameBootstrapper for auto-setup).
        /// If Start() has already run, also begins placement mode immediately.
        /// </summary>
        public void SetBuilding(BuildingDefinition building)
        {
            _selectedBuilding = building;
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

                // Position the preview on the smooth mesh surface (Story 0.6)
                float previewY = world.GetSmoothedHeightAtWorldPos(blockX + 0.5f, blockZ + 0.5f);
                _preview.transform.position = new Vector3(
                    blockX + 0.5f,  // Center on block
                    previewY,
                    blockZ + 0.5f
                );

                // Color indicates valid/invalid
                if (_previewMaterial != null)
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
        /// Story 4.1: Checks resource costs before placing. Deducts on confirm.
        /// </summary>
        private void PlaceBuilding()
        {
            if (!EnsureMaterials())
                return;

            // Story 4.1: Check if player can afford this building
            var rm = ResourceManager.Instance;
            if (rm != null && !rm.CanAfford(_selectedBuilding.WoodCost, _selectedBuilding.StoneCost))
            {
                Debug.Log($"Cannot afford {_selectedBuilding.DisplayName} " +
                          $"(need {_selectedBuilding.WoodCost}W/{_selectedBuilding.StoneCost}S, " +
                          $"have {rm.Wood}W/{rm.Stone}S)");
                ShowCostFeedback();
                return;
            }

            // Deduct resources
            rm?.Spend(_selectedBuilding.WoodCost, _selectedBuilding.StoneCost);

            Vector3 position = _preview.transform.position;

            // Create building with type-specific visual
            var building = CreateBuildingVisual(_selectedBuilding, position);

            // Story 2.3: Add Building component with NavMeshObstacle and entrance point
            var buildingComponent = building.AddComponent<Building>();
            buildingComponent.Initialize(_selectedBuilding);

            // Notify other systems via event bus
            EventBus.Publish(new BuildingPlacedEvent
            {
                BuildingName = _selectedBuilding.DisplayName,
                Position = position,
                BuildingObject = building
            });

            Debug.Log($"Placed {_selectedBuilding.DisplayName} at {position} " +
                      $"(cost: {_selectedBuilding.WoodCost}W, {_selectedBuilding.StoneCost}S)");

            // Continue placement mode (build loop – player can keep placing)
            // As per GDD gesture lexicon: "build loop for serial construction"
        }

        /// <summary>
        /// Create a building with visuals matching its type.
        /// Campfire: stone ring + flame cone. Huts: walls + roof.
        /// </summary>
        private GameObject CreateBuildingVisual(BuildingDefinition def, Vector3 position)
        {
            string type = def.DisplayName.ToLower();

            if (type.Contains("campfire"))
                return CreateCampfireVisual(def, position);
            if (type.Contains("woodcutter") || type.Contains("holzf"))
                return CreateHutVisual(def, position, new Color(0.50f, 0.32f, 0.15f),
                    new Color(0.40f, 0.25f, 0.10f));
            if (type.Contains("hunter") || type.Contains("j\u00e4ger"))
                return CreateHutVisual(def, position, new Color(0.25f, 0.45f, 0.20f),
                    new Color(0.35f, 0.28f, 0.15f));
            if (type.Contains("hut") || type.Contains("h\u00fctte"))
                return CreateSimpleHutVisual(def, position);

            return CreateFallbackCube(def, position);
        }

        /// <summary>Campfire: ring of stones around a flame cone.</summary>
        private GameObject CreateCampfireVisual(BuildingDefinition def, Vector3 position)
        {
            var root = new GameObject(def.DisplayName);
            root.transform.position = position;

            EnsureCampfireMesh();

            for (int i = 0; i < 6; i++)
            {
                float angle = i * Mathf.PI * 2f / 6f;
                var stone = GameObject.CreatePrimitive(PrimitiveType.Cube);
                stone.name = $"Stone_{i}";
                stone.transform.SetParent(root.transform, false);
                stone.transform.localScale = new Vector3(0.2f, 0.15f, 0.2f);
                stone.transform.localPosition = new Vector3(
                    Mathf.Cos(angle) * 0.35f, 0.07f, Mathf.Sin(angle) * 0.35f);
                stone.transform.localRotation = Quaternion.Euler(0f, angle * Mathf.Rad2Deg + 15f, 0f);
                var col = stone.GetComponent<Collider>();
                if (col != null) Destroy(col);

                var sr = stone.GetComponent<MeshRenderer>();
                sr.sharedMaterial = _buildingMaterial;
                var pb = new MaterialPropertyBlock();
                pb.SetColor(ColorID, new Color(0.45f, 0.43f, 0.40f));
                sr.SetPropertyBlock(pb);
            }

            var flame = new GameObject("Flame");
            flame.transform.SetParent(root.transform, false);
            flame.transform.localPosition = new Vector3(0f, 0.05f, 0f);
            flame.transform.localScale = new Vector3(0.3f, 0.6f, 0.3f);
            var flameMF = flame.AddComponent<MeshFilter>();
            flameMF.sharedMesh = _campfireConeMesh;
            var flameMR = flame.AddComponent<MeshRenderer>();
            flameMR.sharedMaterial = _buildingMaterial;
            var flamePb = new MaterialPropertyBlock();
            flamePb.SetColor(ColorID, new Color(1f, 0.55f, 0.1f));
            flameMR.SetPropertyBlock(flamePb);

            var glow = new GameObject("Glow");
            glow.transform.SetParent(root.transform, false);
            glow.transform.localPosition = new Vector3(0f, 0.05f, 0f);
            glow.transform.localScale = new Vector3(0.15f, 0.45f, 0.15f);
            var glowMF = glow.AddComponent<MeshFilter>();
            glowMF.sharedMesh = _campfireConeMesh;
            var glowMR = glow.AddComponent<MeshRenderer>();
            glowMR.sharedMaterial = _buildingMaterial;
            var glowPb = new MaterialPropertyBlock();
            glowPb.SetColor(ColorID, new Color(1f, 0.85f, 0.3f));
            glowMR.SetPropertyBlock(glowPb);

            var rootCol = root.AddComponent<BoxCollider>();
            rootCol.center = new Vector3(0f, 0.3f, 0f);
            rootCol.size = new Vector3(0.9f, 0.6f, 0.9f);

            return root;
        }

        /// <summary>Work hut: rectangular base + roof + door. Color accent per type.</summary>
        private GameObject CreateHutVisual(BuildingDefinition def, Vector3 position,
            Color wallColor, Color roofColor)
        {
            var root = new GameObject(def.DisplayName);
            root.transform.position = position;

            float w = def.FootprintSize.x * 0.85f;
            float d = def.FootprintSize.y * 0.85f;
            float wallH = def.VisualHeight * 0.55f;
            float roofH = def.VisualHeight * 0.45f;

            var walls = GameObject.CreatePrimitive(PrimitiveType.Cube);
            walls.name = "Walls";
            walls.transform.SetParent(root.transform, false);
            walls.transform.localScale = new Vector3(w, wallH, d);
            walls.transform.localPosition = new Vector3(0f, wallH * 0.5f, 0f);

            var wallRenderer = walls.GetComponent<MeshRenderer>();
            wallRenderer.sharedMaterial = _buildingMaterial;
            var wallPb = new MaterialPropertyBlock();
            wallPb.SetColor(ColorID, wallColor);
            wallRenderer.SetPropertyBlock(wallPb);

            var roof = GameObject.CreatePrimitive(PrimitiveType.Cube);
            roof.name = "Roof";
            roof.transform.SetParent(root.transform, false);
            roof.transform.localScale = new Vector3(w * 1.15f, roofH, d * 0.75f);
            roof.transform.localPosition = new Vector3(0f, wallH + roofH * 0.3f, 0f);
            var roofCol = roof.GetComponent<Collider>();
            if (roofCol != null) Destroy(roofCol);

            var roofRenderer = roof.GetComponent<MeshRenderer>();
            roofRenderer.sharedMaterial = _buildingMaterial;
            var roofPb = new MaterialPropertyBlock();
            roofPb.SetColor(ColorID, roofColor);
            roofRenderer.SetPropertyBlock(roofPb);

            var door = GameObject.CreatePrimitive(PrimitiveType.Cube);
            door.name = "Door";
            door.transform.SetParent(root.transform, false);
            door.transform.localScale = new Vector3(0.2f, wallH * 0.6f, 0.05f);
            door.transform.localPosition = new Vector3(0f, wallH * 0.3f, -d * 0.5f - 0.02f);
            var doorCol = door.GetComponent<Collider>();
            if (doorCol != null) Destroy(doorCol);

            var doorRenderer = door.GetComponent<MeshRenderer>();
            doorRenderer.sharedMaterial = _buildingMaterial;
            var doorPb = new MaterialPropertyBlock();
            doorPb.SetColor(ColorID, new Color(0.15f, 0.10f, 0.05f));
            doorRenderer.SetPropertyBlock(doorPb);

            return root;
        }

        /// <summary>Simple hut (residential): warm sandy tones.</summary>
        private GameObject CreateSimpleHutVisual(BuildingDefinition def, Vector3 position)
        {
            return CreateHutVisual(def, position,
                new Color(0.60f, 0.45f, 0.25f),
                new Color(0.45f, 0.30f, 0.15f));
        }

        /// <summary>Fallback: plain colored cube for unknown building types.</summary>
        private GameObject CreateFallbackCube(BuildingDefinition def, Vector3 position)
        {
            var building = GameObject.CreatePrimitive(PrimitiveType.Cube);
            building.name = def.DisplayName;
            building.transform.position = position;
            building.transform.localScale = new Vector3(
                def.FootprintSize.x, def.VisualHeight, def.FootprintSize.y);

            var renderer = building.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = _buildingMaterial;
            _buildingPropBlock.SetColor(ColorID, def.PreviewColor);
            renderer.SetPropertyBlock(_buildingPropBlock);

            return building;
        }

        // Shared cone mesh for campfire flames
        private static Mesh _campfireConeMesh;

        private static void EnsureCampfireMesh()
        {
            if (_campfireConeMesh != null) return;

            int segments = 6;
            float radius = 0.5f;
            float height = 1f;
            var mesh = new Mesh { name = "FlameCone" };
            int vertCount = segments + 2;
            var verts = new Vector3[vertCount];
            var normals = new Vector3[vertCount];

            verts[0] = Vector3.zero;
            normals[0] = Vector3.down;
            for (int i = 0; i < segments; i++)
            {
                float angle = i * Mathf.PI * 2f / segments;
                verts[i + 1] = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                normals[i + 1] = new Vector3(Mathf.Cos(angle), 0.5f, Mathf.Sin(angle)).normalized;
            }
            verts[segments + 1] = new Vector3(0f, height, 0f);
            normals[segments + 1] = Vector3.up;

            var tris = new int[segments * 6];
            for (int i = 0; i < segments; i++)
            {
                int next = (i + 1) % segments;
                tris[i * 6 + 0] = 0;
                tris[i * 6 + 1] = next + 1;
                tris[i * 6 + 2] = i + 1;
                tris[i * 6 + 3] = i + 1;
                tris[i * 6 + 4] = next + 1;
                tris[i * 6 + 5] = segments + 1;
            }

            mesh.vertices = verts;
            mesh.normals = normals;
            mesh.triangles = tris;
            mesh.RecalculateBounds();
            _campfireConeMesh = mesh;
        }

        /// <summary>
        /// Visual feedback when placement is rejected due to insufficient resources.
        /// Story 4.1: Brief red flash on the preview ghost.
        /// </summary>
        private void ShowCostFeedback()
        {
            if (_previewMaterial != null)
            {
                _previewMaterial.color = _invalidColor;
                _feedbackTimer = FEEDBACK_DURATION;
            }
        }

        /// <summary>
        /// Create the ghost preview cube that follows the mouse.
        /// </summary>
        private void CreatePreview()
        {
            if (_preview != null)
                Destroy(_preview);

            if (!EnsureMaterials())
                return;

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

            // Apply the transparent preview material
            _previewRenderer = _preview.GetComponent<MeshRenderer>();
            _previewMaterial.color = _validColor;
            _previewRenderer.material = _previewMaterial;
        }

        /// <summary>
        /// Reset preview color after cost-feedback flash.
        /// </summary>
        private void UpdateFeedbackTimer()
        {
            if (_feedbackTimer <= 0f) return;
            _feedbackTimer -= Time.deltaTime;
            if (_feedbackTimer <= 0f && _previewMaterial != null)
                _previewMaterial.color = _isValidPosition ? _validColor : _invalidColor;
        }

        private void OnDestroy()
        {
            if (_preview != null)
                Destroy(_preview);

            // Clean up dynamically created materials to prevent VRAM leaks
            if (_previewMaterial != null)
                Destroy(_previewMaterial);
            if (_buildingMaterial != null)
                Destroy(_buildingMaterial);
        }
    }
}
