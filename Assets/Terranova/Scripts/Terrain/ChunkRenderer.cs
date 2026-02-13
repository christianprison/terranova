using UnityEngine;

namespace Terranova.Terrain
{
    /// <summary>
    /// Visual representation of a single chunk in the scene.
    ///
    /// Each chunk gets its own GameObject with MeshFilter + MeshRenderer.
    /// The ChunkRenderer connects the simulation data (ChunkData) to Unity's
    /// rendering system. It does NOT own the data – the WorldManager does.
    ///
    /// Two materials are used:
    ///   Submesh 0 → Opaque material (grass, dirt, stone, sand)
    ///   Submesh 1 → Transparent material (water)
    /// </summary>
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    [RequireComponent(typeof(MeshCollider))]
    public class ChunkRenderer : MonoBehaviour
    {
        private MeshFilter _meshFilter;
        private MeshRenderer _meshRenderer;
        private MeshCollider _meshCollider;

        // Reference to the chunk's data (owned by WorldManager)
        public ChunkData Data { get; private set; }

        /// <summary>
        /// Current LOD level. 0 = full detail, 1 = medium, 2 = low.
        /// Used by WorldManager to track when a rebuild is needed.
        /// Story 0.4: Performance und LOD
        /// </summary>
        public int CurrentLod { get; private set; }

        /// <summary>
        /// Initialize this renderer with chunk data and materials.
        /// Called by WorldManager when creating a new chunk.
        /// </summary>
        public void Initialize(ChunkData data, Material solidMaterial, Material waterMaterial)
        {
            Data = data;

            _meshFilter = GetComponent<MeshFilter>();
            _meshRenderer = GetComponent<MeshRenderer>();
            _meshCollider = GetComponent<MeshCollider>();

            // Assign both materials (submesh 0 = solid, submesh 1 = water)
            _meshRenderer.materials = new[] { solidMaterial, waterMaterial };

            // Position the chunk GameObject at its world-space location
            transform.position = new Vector3(
                data.ChunkX * ChunkData.WIDTH,
                0,
                data.ChunkZ * ChunkData.DEPTH
            );

            gameObject.name = $"Chunk ({data.ChunkX}, {data.ChunkZ})";
        }

        /// <summary>
        /// Rebuild the mesh from current chunk data using the smooth terrain builder.
        /// Call this after terrain generation or any block modification.
        /// </summary>
        public void RebuildMesh(
            SmoothTerrainBuilder.HeightLookup heightLookup = null,
            SmoothTerrainBuilder.SurfaceLookup surfaceLookup = null,
            int lodLevel = 0)
        {
            if (Data == null)
            {
                Debug.LogWarning($"ChunkRenderer.RebuildMesh called with no data on {gameObject.name}");
                return;
            }

            // Convert LOD level (0,1,2) to step size (1,2,4)
            int lodStep = 1 << lodLevel; // 0→1, 1→2, 2→4
            CurrentLod = lodLevel;

            // Destroy the old mesh to prevent memory leaks
            if (_meshFilter.sharedMesh != null)
                Destroy(_meshFilter.sharedMesh);

            // Build smooth terrain mesh from voxel data at the requested LOD
            Mesh mesh = SmoothTerrainBuilder.Build(Data, heightLookup, surfaceLookup, lodStep);
            _meshFilter.sharedMesh = mesh;

            // Update collider for raycasting (building placement, camera ground detection)
            _meshCollider.sharedMesh = null; // Clear first to force update
            _meshCollider.sharedMesh = mesh;
        }
    }
}
