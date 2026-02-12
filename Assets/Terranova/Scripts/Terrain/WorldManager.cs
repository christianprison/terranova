using System.Collections.Generic;
using UnityEngine;

namespace Terranova.Terrain
{
    /// <summary>
    /// Central manager for the voxel world. Coordinates chunk creation,
    /// terrain generation, and mesh building.
    ///
    /// For MS1: Generates a fixed grid of chunks at startup (no streaming).
    /// The world size is configurable in the Inspector. Default is 8×8 chunks
    /// (128×128 blocks), which exceeds the MS1 minimum of 64×64.
    ///
    /// Scene setup:
    ///   1. Create empty GameObject named "World"
    ///   2. Add this component
    ///   3. Assign solid and water materials (or leave empty for auto-created ones)
    ///   4. Hit Play
    /// </summary>
    public class WorldManager : MonoBehaviour
    {
        [Header("World Size")]
        [Tooltip("Number of chunks along X axis. 8 chunks = 128 blocks = 128m.")]
        [SerializeField] private int _worldSizeX = 8;

        [Tooltip("Number of chunks along Z axis.")]
        [SerializeField] private int _worldSizeZ = 8;

        [Header("Generation")]
        [Tooltip("Seed for terrain generation. Same seed = same world.")]
        [SerializeField] private int _seed = 42;

        [Header("Materials")]
        [Tooltip("Material for solid blocks (opaque, vertex colors). Auto-created if not assigned.")]
        [SerializeField] private Material _solidMaterial;

        [Tooltip("Material for water (transparent, vertex colors). Auto-created if not assigned.")]
        [SerializeField] private Material _waterMaterial;

        // All chunks indexed by (chunkX, chunkZ) coordinates
        private readonly Dictionary<Vector2Int, ChunkRenderer> _chunks = new();

        // Terrain generator (deterministic from seed)
        private TerrainGenerator _generator;

        // Public access for other systems (building placement, camera, etc.)
        public static WorldManager Instance { get; private set; }

        /// <summary>
        /// World dimensions in blocks (for camera bounds, etc.)
        /// </summary>
        public int WorldBlocksX => _worldSizeX * ChunkData.WIDTH;
        public int WorldBlocksZ => _worldSizeZ * ChunkData.DEPTH;

        private void Awake()
        {
            // Simple singleton – only one world at a time
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("Multiple WorldManagers detected. Destroying duplicate.");
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            EnsureMaterials();
            GenerateWorld();
        }

        /// <summary>
        /// Generate the entire world: create chunks, fill with terrain, build meshes.
        /// </summary>
        private void GenerateWorld()
        {
            _generator = new TerrainGenerator(_seed);

            // Phase 1: Create all chunk data
            for (int cx = 0; cx < _worldSizeX; cx++)
            {
                for (int cz = 0; cz < _worldSizeZ; cz++)
                {
                    CreateChunk(cx, cz);
                }
            }

            // Phase 2: Build meshes (done separately so neighbor lookups work
            // across chunk boundaries – all data must exist first)
            foreach (var chunk in _chunks.Values)
            {
                chunk.RebuildMesh(GetBlockAtWorldPos);
            }

            Debug.Log($"World generated: {_worldSizeX}×{_worldSizeZ} chunks " +
                      $"({WorldBlocksX}×{WorldBlocksZ} blocks), seed={_seed}");
        }

        /// <summary>
        /// Create a single chunk: allocate data, generate terrain, create GameObject.
        /// </summary>
        private void CreateChunk(int chunkX, int chunkZ)
        {
            // Create and populate chunk data
            var data = new ChunkData(chunkX, chunkZ);
            _generator.GenerateChunk(data);

            // Create the visible GameObject
            var chunkObj = new GameObject();
            chunkObj.transform.SetParent(transform);

            var renderer = chunkObj.AddComponent<ChunkRenderer>();
            renderer.Initialize(data, _solidMaterial, _waterMaterial);

            _chunks[new Vector2Int(chunkX, chunkZ)] = renderer;
        }

        /// <summary>
        /// Look up a block type at any world position.
        /// Finds the right chunk and returns the block. Returns Air if out of bounds.
        /// Used by: ChunkMeshBuilder (seamless edges), BuildingPlacer (placement validation).
        /// </summary>
        public VoxelType GetBlockAtWorldPos(int worldX, int worldY, int worldZ)
        {
            // Convert world coords to chunk coords
            int chunkX = Mathf.FloorToInt((float)worldX / ChunkData.WIDTH);
            int chunkZ = Mathf.FloorToInt((float)worldZ / ChunkData.DEPTH);

            var key = new Vector2Int(chunkX, chunkZ);

            if (!_chunks.TryGetValue(key, out var chunk))
                return VoxelType.Air;

            // Convert world coords to local chunk coords
            int localX = worldX - chunkX * ChunkData.WIDTH;
            int localZ = worldZ - chunkZ * ChunkData.DEPTH;

            return chunk.Data.GetBlock(localX, worldY, localZ);
        }

        /// <summary>
        /// Get the terrain height at a world position (highest non-air block Y).
        /// Returns -1 if the position is outside the world or the column is empty.
        /// </summary>
        public int GetHeightAtWorldPos(int worldX, int worldZ)
        {
            int chunkX = Mathf.FloorToInt((float)worldX / ChunkData.WIDTH);
            int chunkZ = Mathf.FloorToInt((float)worldZ / ChunkData.DEPTH);

            var key = new Vector2Int(chunkX, chunkZ);

            if (!_chunks.TryGetValue(key, out var chunk))
                return -1;

            int localX = worldX - chunkX * ChunkData.WIDTH;
            int localZ = worldZ - chunkZ * ChunkData.DEPTH;

            return chunk.Data.GetHeightAt(localX, localZ);
        }

        /// <summary>
        /// Get the surface block type at a world position.
        /// Returns Air if outside the world.
        /// </summary>
        public VoxelType GetSurfaceTypeAtWorldPos(int worldX, int worldZ)
        {
            int chunkX = Mathf.FloorToInt((float)worldX / ChunkData.WIDTH);
            int chunkZ = Mathf.FloorToInt((float)worldZ / ChunkData.DEPTH);

            var key = new Vector2Int(chunkX, chunkZ);

            if (!_chunks.TryGetValue(key, out var chunk))
                return VoxelType.Air;

            int localX = worldX - chunkX * ChunkData.WIDTH;
            int localZ = worldZ - chunkZ * ChunkData.DEPTH;

            return chunk.Data.GetSurfaceType(localX, localZ);
        }

        /// <summary>
        /// Create fallback materials if none were assigned in Inspector.
        /// Uses URP Particles/Unlit shader which supports vertex colors.
        /// </summary>
        private void EnsureMaterials()
        {
            if (_solidMaterial == null)
            {
                // Try the custom Terranova shader first, fall back to particle shader
                Shader shader = Shader.Find("Terranova/VertexColorOpaque")
                             ?? Shader.Find("Universal Render Pipeline/Particles/Unlit");

                if (shader == null)
                {
                    Debug.LogError("No suitable shader found! Assign materials manually.");
                    return;
                }

                _solidMaterial = new Material(shader);
                _solidMaterial.name = "Terrain_Solid (Auto)";
            }

            if (_waterMaterial == null)
            {
                Shader shader = Shader.Find("Terranova/VertexColorTransparent")
                             ?? Shader.Find("Universal Render Pipeline/Particles/Unlit");

                if (shader == null)
                {
                    Debug.LogError("No suitable shader found! Assign materials manually.");
                    return;
                }

                _waterMaterial = new Material(shader);
                _waterMaterial.name = "Water_Transparent (Auto)";

                // Configure transparency for the fallback particle shader
                if (_waterMaterial.HasProperty("_Surface"))
                {
                    _waterMaterial.SetFloat("_Surface", 1f); // 1 = Transparent
                    _waterMaterial.SetFloat("_Blend", 0f);   // 0 = Alpha blend
                    _waterMaterial.renderQueue = 3000;
                }
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }
    }
}
