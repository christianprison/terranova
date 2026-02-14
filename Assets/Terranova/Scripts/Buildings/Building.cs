using UnityEngine;
using UnityEngine.AI;
using Terranova.Core;

namespace Terranova.Buildings
{
    /// <summary>
    /// Represents a placed building in the world.
    ///
    /// Story 2.3: NavMesh entrance point and obstacle carving.
    /// Story 4.2: Construction progress – buildings start as construction sites
    ///            and must be built by settlers before becoming functional.
    /// </summary>
    public class Building : MonoBehaviour
    {
        // ─── Construction Constants ─────────────────────────────
        private const float MIN_CONSTRUCTION_TIME = 5f;
        private const float COST_TO_TIME_RATIO = 0.5f;
        private const float CONSTRUCTION_START_HEIGHT = 0.01f;
        private const float CONSTRUCTION_DIM_FACTOR = 0.4f;

        private BuildingDefinition _definition;
        private NavMeshObstacle _obstacle;

        // ─── Construction State (Story 4.2) ──────────────────────

        private bool _isConstructed;
        private float _constructionProgress;
        private float _constructionTime;
        private bool _isBeingBuilt;

        // Visual references for construction feedback
        private MeshRenderer[] _renderers;
        private MaterialPropertyBlock _propBlock;
        private float _originalScaleY;
        private static readonly int ColorID = Shader.PropertyToID("_BaseColor");

        /// <summary>The definition (type) of this building.</summary>
        public BuildingDefinition Definition => _definition;

        /// <summary>Whether construction is complete and the building is functional.</summary>
        public bool IsConstructed => _isConstructed;

        /// <summary>Whether a settler is currently building this.</summary>
        public bool IsBeingBuilt => _isBeingBuilt;

        /// <summary>Construction progress from 0 to 1.</summary>
        public float ConstructionProgress => _constructionProgress;

        // ─── Worker Tracking (Story 4.4) ─────────────────────────

        /// <summary>Whether this building currently has an assigned worker.</summary>
        public bool HasWorker { get; set; }

        /// <summary>Reference to the settler currently assigned to work here.</summary>
        public GameObject AssignedWorker { get; set; }

        /// <summary>
        /// World-space position of the building entrance.
        /// Settlers navigate here instead of the building center.
        /// </summary>
        public Vector3 EntrancePosition
        {
            get
            {
                if (_definition != null)
                    return transform.position + _definition.EntranceOffset;
                return transform.position + Vector3.back;
            }
        }

        /// <summary>
        /// Initialize the building with its definition.
        /// Story 4.2: Starts as construction site (unless skipConstruction is true).
        /// </summary>
        public void Initialize(BuildingDefinition definition, bool skipConstruction = false)
        {
            _definition = definition;

            // Cache renderers for construction visual feedback (children included for multi-part buildings)
            _renderers = GetComponentsInChildren<MeshRenderer>();
            _propBlock = new MaterialPropertyBlock();
            _originalScaleY = transform.localScale.y;

            // Carve building footprint from NavMesh so settlers walk around it
            _obstacle = gameObject.AddComponent<NavMeshObstacle>();
            _obstacle.shape = NavMeshObstacleShape.Box;
            _obstacle.size = new Vector3(
                definition.FootprintSize.x,
                definition.VisualHeight,
                definition.FootprintSize.y
            );
            _obstacle.center = Vector3.zero;
            _obstacle.carving = true;
            _obstacle.carvingMoveThreshold = 0.1f;

            // Construction time scales with building size (wood + stone cost)
            _constructionTime = Mathf.Max(MIN_CONSTRUCTION_TIME,
                (definition.WoodCost + definition.StoneCost) * COST_TO_TIME_RATIO);

            if (skipConstruction)
            {
                _isConstructed = true;
                _constructionProgress = 1f;
            }
            else
            {
                _isConstructed = false;
                _constructionProgress = 0f;
                UpdateConstructionVisual();
            }
        }

        private void Update()
        {
            // Gradually grow the building while a settler is constructing it
            if (_isBeingBuilt && !_isConstructed && _constructionTime > 0)
            {
                _constructionProgress += Time.deltaTime / _constructionTime;
                _constructionProgress = Mathf.Clamp01(_constructionProgress);
                UpdateConstructionVisual();
            }
        }

        /// <summary>
        /// Reserve this construction site for a settler.
        /// Returns false if already being built or already constructed.
        /// </summary>
        public bool TryReserveConstruction()
        {
            if (_isConstructed || _isBeingBuilt) return false;
            _isBeingBuilt = true;
            return true;
        }

        /// <summary>Release the construction reservation (settler was interrupted).</summary>
        public void ReleaseConstruction()
        {
            _isBeingBuilt = false;
        }

        /// <summary>
        /// Add construction progress from a settler's work.
        /// Returns the work duration needed for this build step.
        /// </summary>
        public float GetBuildStepDuration()
        {
            return _constructionTime;
        }

        /// <summary>
        /// Complete a construction work step. Called when the settler finishes working.
        /// </summary>
        public void CompleteConstruction()
        {
            _isBeingBuilt = false;
            _constructionProgress = 1f;
            _isConstructed = true;

            UpdateConstructionVisual();

            EventBus.Publish(new BuildingCompletedEvent
            {
                BuildingName = _definition != null ? _definition.DisplayName : name,
                Position = transform.position,
                BuildingObject = gameObject
            });

            Debug.Log($"[Building] {name} construction complete!");
        }

        /// <summary>
        /// Update the building's visual appearance based on construction progress.
        /// Under construction: dimmed color and scaled from 1% to 100% height.
        /// Complete: full color and original scale.
        /// </summary>
        private void UpdateConstructionVisual()
        {
            if (_propBlock == null) return;

            if (_isConstructed)
            {
                // Full color when complete
                if (_definition != null && _renderers != null)
                {
                    foreach (var r in _renderers)
                    {
                        if (r == null) continue;
                        _propBlock.SetColor(ColorID, _definition.PreviewColor);
                        r.SetPropertyBlock(_propBlock);
                    }
                }
                // Restore original scale
                var scale = transform.localScale;
                scale.y = _originalScaleY;
                transform.localScale = scale;
            }
            else
            {
                // Construction site: dim color
                Color dimColor = _definition != null
                    ? _definition.PreviewColor * CONSTRUCTION_DIM_FACTOR
                    : Color.gray;
                dimColor.a = 1f;
                if (_renderers != null)
                {
                    foreach (var r in _renderers)
                    {
                        if (r == null) continue;
                        _propBlock.SetColor(ColorID, dimColor);
                        r.SetPropertyBlock(_propBlock);
                    }
                }

                // Scale height from 1% to 100% during construction
                float heightFraction = CONSTRUCTION_START_HEIGHT
                    + (1f - CONSTRUCTION_START_HEIGHT) * _constructionProgress;
                var scale = transform.localScale;
                scale.y = _originalScaleY * heightFraction;
                transform.localScale = scale;
            }
        }
    }
}
