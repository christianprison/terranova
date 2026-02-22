using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;
using Terranova.Core;

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

        [Header("Materials")]
        [Tooltip("Material for solid blocks (opaque, vertex colors). Auto-created if not assigned.")]
        [SerializeField] private Material _solidMaterial;

        [Tooltip("Material for water (transparent, vertex colors). Auto-created if not assigned.")]
        [SerializeField] private Material _waterMaterial;

        // Track whether materials/textures were auto-created (so we can clean them up)
        private bool _ownsSolidMaterial;
        private bool _ownsWaterMaterial;
        private readonly List<Texture2D> _autoTextures = new();

        // All chunks indexed by (chunkX, chunkZ) coordinates
        private readonly Dictionary<Vector2Int, ChunkRenderer> _chunks = new();

        // Terrain generator (deterministic from seed)
        private TerrainGenerator _generator;

        // NavMesh for settler pathfinding (Story 2.0)
        private NavMeshSurface _navMeshSurface;

        /// <summary>Whether the NavMesh has been baked and is ready for pathfinding.</summary>
        public bool IsNavMeshReady { get; private set; }

        // Public access for other systems (building placement, camera, etc.)
        public static WorldManager Instance { get; private set; }

        /// <summary>
        /// World dimensions in blocks (for camera bounds, etc.)
        /// </summary>
        public int WorldBlocksX => _worldSizeX * ChunkData.WIDTH;
        public int WorldBlocksZ => _worldSizeZ * ChunkData.DEPTH;

        /// <summary>
        /// Center of the freshwater pond near spawn.
        /// Settlers use this to find drinkable water (avoids ocean on Coast biome).
        /// Set by SettlerSpawner after placing the water primitive.
        /// </summary>
        public Vector3 FreshwaterCenter { get; set; }

        /// <summary>
        /// Block coordinates of campfire location, determined during terrain generation.
        /// SettlerSpawner uses this to place the visual campfire and spawn settlers.
        /// Terrain is already flattened at this position.
        /// </summary>
        public int CampfireBlockX { get; private set; }
        public int CampfireBlockZ { get; private set; }

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

        [Header("LOD")]
        [Tooltip("Distance in chunks for LOD 1 (medium detail). Chunks closer use LOD 0.")]
        [SerializeField] private int _lod1Distance = 4;
        [Tooltip("Distance in chunks for LOD 2 (low detail). Chunks closer use LOD 1.")]
        [SerializeField] private int _lod2Distance = 8;

        private IEnumerator Start()
        {
            // Publish initial progress so loading screen renders before material creation
            EventBus.Publish(new WorldGenerationProgressEvent
            {
                Progress = 0f,
                Status = "Preparing materials..."
            });
            yield return null;

            EnsureMaterials();

            if (_solidMaterial == null || _waterMaterial == null)
            {
                Debug.LogError("WorldManager: Cannot generate world – materials missing. Assign them in the Inspector.");
                yield break;
            }

            yield return StartCoroutine(GenerateWorldAsync());

            // Periodically update LOD based on camera position (every 0.5s)
            InvokeRepeating(nameof(UpdateChunkLODs), 1f, 0.5f);
        }

        /// <summary>
        /// Generate the entire world as a coroutine, publishing progress events
        /// so a loading screen can display a progress bar.
        ///
        /// v0.4.8 flow: campfire flattening happens BEFORE mesh building.
        /// Water pond is spawned as a visual primitive by SettlerSpawner (post-NavMesh).
        /// Loading screen stays until SettlerSpawner publishes progress 1.0.
        /// </summary>
        private IEnumerator GenerateWorldAsync()
        {
            _generator = new TerrainGenerator(GameState.Seed, GameState.SelectedBiome);
            _generator.SetWorldSize(WorldBlocksZ);
            int totalChunks = _worldSizeX * _worldSizeZ;
            int processed = 0;

            EventBus.Publish(new WorldGenerationProgressEvent
            {
                Progress = 0f,
                Status = "Generating terrain..."
            });
            yield return null;

            // Phase 1: Create all chunk data
            for (int cx = 0; cx < _worldSizeX; cx++)
            {
                for (int cz = 0; cz < _worldSizeZ; cz++)
                {
                    CreateChunk(cx, cz);
                    processed++;
                }
                EventBus.Publish(new WorldGenerationProgressEvent
                {
                    Progress = (float)processed / (totalChunks * 2),
                    Status = $"Generating terrain... {processed}/{totalChunks}"
                });
                yield return null;
            }

            // Phase 1.5: Prepare settlement area — flatten campfire zone.
            // All block modifications happen BEFORE mesh building so meshes are correct.
            EventBus.Publish(new WorldGenerationProgressEvent
            {
                Progress = 0.50f,
                Status = "Placing resources..."
            });
            yield return null;

            PrepareSettlementArea();

            // Phase 2: Build meshes (includes flattened campfire area)
            processed = 0;
            foreach (var chunk in _chunks.Values)
            {
                chunk.RebuildMesh(GetSolidHeightAtWorldPos, GetSolidSurfaceTypeAtWorldPos);
                processed++;
                EventBus.Publish(new WorldGenerationProgressEvent
                {
                    Progress = 0.52f + (float)processed / (totalChunks * 2.2f),
                    Status = "Placing resources..."
                });
                yield return null;
            }

            Debug.Log($"World generated: {_worldSizeX}×{_worldSizeZ} chunks " +
                      $"({WorldBlocksX}×{WorldBlocksZ} blocks), seed={GameState.Seed}");

            // Phase 3: NavMesh — ONE bake that includes flattened camp area.
            // No second bake needed because terrain modifications are already in the meshes.
            EventBus.Publish(new WorldGenerationProgressEvent
            {
                Progress = 0.95f,
                Status = "Placing resources..."
            });
            yield return null;

            BakeNavMesh();

            // Don't publish 1.0 yet — SettlerSpawner does that after spawning settlers.
            // Loading screen stays visible until settlers appear.
            EventBus.Publish(new WorldGenerationProgressEvent
            {
                Progress = 0.98f,
                Status = "Your tribe arrives..."
            });
        }

        /// <summary>
        /// Prepare the settlement area during terrain generation (Phase 1.5).
        /// Finds solid ground near world center and flattens it for the campfire.
        /// Water pond is spawned separately by SettlerSpawner as a visual primitive.
        /// </summary>
        private void PrepareSettlementArea()
        {
            int campX = WorldBlocksX / 2;
            int campZ = WorldBlocksZ / 2;

            // Step 1: Find solid ground near center
            FindSolidGroundNear(ref campX, ref campZ);
            CampfireBlockX = campX;
            CampfireBlockZ = campZ;

            // Step 2: Flatten terrain for campfire + settler spawn area (radius 4)
            FlattenBlockData(campX, campZ, 4);

            Debug.Log($"[Settlement] Campfire area prepared at ({campX},{campZ}).");
        }

        /// <summary>
        /// Search outward from (x,z) for solid ground. Modifies x,z in place.
        /// Used during terrain generation before campfire placement.
        /// </summary>
        private void FindSolidGroundNear(ref int x, ref int z)
        {
            if (GetHeightAtWorldPos(x, z) >= 0 && GetSurfaceTypeAtWorldPos(x, z).IsSolid())
                return;

            for (int radius = 1; radius <= 32; radius++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    for (int dz = -radius; dz <= radius; dz++)
                    {
                        if (Mathf.Abs(dx) != radius && Mathf.Abs(dz) != radius) continue;
                        int testX = x + dx;
                        int testZ = z + dz;
                        if (GetHeightAtWorldPos(testX, testZ) >= 0 &&
                            GetSurfaceTypeAtWorldPos(testX, testZ).IsSolid())
                        {
                            x = testX;
                            z = testZ;
                            return;
                        }
                    }
                }
            }
            Debug.LogWarning("WorldManager: Could not find solid ground near world center!");
        }

        /// <summary>
        /// Flatten terrain by modifying block data ONLY (no mesh rebuild, no NavMesh rebake).
        /// Used during terrain generation phase before mesh building.
        /// </summary>
        private void FlattenBlockData(int centerX, int centerZ, int radius)
        {
            int targetHeight = GetHeightAtWorldPos(centerX, centerZ);
            if (targetHeight < 0) return;

            VoxelType surfaceType = GetSurfaceTypeAtWorldPos(centerX, centerZ);
            if (!surfaceType.IsSolid()) surfaceType = VoxelType.Grass;

            var affectedChunks = new HashSet<Vector2Int>();
            for (int x = centerX - radius; x <= centerX + radius; x++)
            {
                for (int z = centerZ - radius; z <= centerZ + radius; z++)
                {
                    int currentHeight = GetHeightAtWorldPos(x, z);
                    if (currentHeight < 0) continue;

                    if (currentHeight > targetHeight)
                    {
                        for (int y = targetHeight + 1; y <= currentHeight; y++)
                            SetBlockInternal(x, y, z, VoxelType.Air, affectedChunks);
                    }
                    else if (currentHeight < targetHeight)
                    {
                        for (int y = currentHeight + 1; y <= targetHeight; y++)
                            SetBlockInternal(x, y, z, surfaceType, affectedChunks);
                    }
                }
            }
            // No mesh rebuild — Phase 2 handles that
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
        /// Update LOD levels for all chunks based on camera distance.
        /// Called periodically via InvokeRepeating.
        /// Only rebuilds meshes for chunks whose LOD level changed.
        ///
        /// Story 0.4: Performance und LOD
        /// </summary>
        private void UpdateChunkLODs()
        {
            var cam = Camera.main;
            if (cam == null)
                return;

            Vector3 camPos = cam.transform.position;

            foreach (var kvp in _chunks)
            {
                var chunk = kvp.Value;

                // Calculate distance from camera to chunk center (in chunk units)
                float chunkCenterX = (kvp.Key.x + 0.5f) * ChunkData.WIDTH;
                float chunkCenterZ = (kvp.Key.y + 0.5f) * ChunkData.DEPTH;
                float dx = camPos.x - chunkCenterX;
                float dz = camPos.z - chunkCenterZ;
                float distInChunks = Mathf.Sqrt(dx * dx + dz * dz) / ChunkData.WIDTH;

                // Determine desired LOD level
                int desiredLod;
                if (distInChunks < _lod1Distance)
                    desiredLod = 0;
                else if (distInChunks < _lod2Distance)
                    desiredLod = 1;
                else
                    desiredLod = 2;

                // Only rebuild if LOD changed
                if (chunk.CurrentLod != desiredLod)
                {
                    chunk.RebuildMesh(GetSolidHeightAtWorldPos, GetSolidSurfaceTypeAtWorldPos, desiredLod);
                }
            }
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
        /// Get the solid terrain height at a world position (skips water blocks).
        /// Returns the seafloor height for underwater columns.
        /// Used by: smooth mesh builder (terrain follows seafloor, not water surface).
        /// </summary>
        public int GetSolidHeightAtWorldPos(int worldX, int worldZ)
        {
            int chunkX = Mathf.FloorToInt((float)worldX / ChunkData.WIDTH);
            int chunkZ = Mathf.FloorToInt((float)worldZ / ChunkData.DEPTH);

            var key = new Vector2Int(chunkX, chunkZ);

            if (!_chunks.TryGetValue(key, out var chunk))
                return -1;

            int localX = worldX - chunkX * ChunkData.WIDTH;
            int localZ = worldZ - chunkZ * ChunkData.DEPTH;

            return chunk.Data.GetSolidHeightAt(localX, localZ);
        }

        /// <summary>
        /// Get the solid surface type at a world position (skips water blocks).
        /// Returns the seafloor block type for underwater columns.
        /// Used by: smooth mesh builder for terrain coloring under water.
        /// </summary>
        public VoxelType GetSolidSurfaceTypeAtWorldPos(int worldX, int worldZ)
        {
            int chunkX = Mathf.FloorToInt((float)worldX / ChunkData.WIDTH);
            int chunkZ = Mathf.FloorToInt((float)worldZ / ChunkData.DEPTH);

            var key = new Vector2Int(chunkX, chunkZ);

            if (!_chunks.TryGetValue(key, out var chunk))
                return VoxelType.Air;

            int localX = worldX - chunkX * ChunkData.WIDTH;
            int localZ = worldZ - chunkZ * ChunkData.DEPTH;

            return chunk.Data.GetSolidSurfaceType(localX, localZ);
        }

        /// <summary>
        /// Get the interpolated smooth mesh height at a world position.
        /// Uses the same 4-column averaging as SmoothTerrainBuilder for consistency.
        /// This is the visual surface height – use for positioning objects on the
        /// smooth terrain mesh (settlers, buildings, etc.).
        ///
        /// Story 0.6: Bestehende Objekte auf Mesh-Oberfläche
        /// </summary>
        public float GetSmoothedHeightAtWorldPos(float worldX, float worldZ)
        {
            // Determine which vertex "cell" this position falls in (same grid as mesh builder)
            // The vertex at (vx, vz) is the average of 4 surrounding columns.
            // We bilinearly interpolate between the 4 nearest vertices.
            float fx = worldX;  // Already in world space
            float fz = worldZ;

            // Floor to get the cell (integer vertex positions in world space)
            int x0 = Mathf.FloorToInt(fx);
            int z0 = Mathf.FloorToInt(fz);
            int x1 = x0 + 1;
            int z1 = z0 + 1;

            // Fractional position within cell for bilinear interpolation
            float tx = fx - x0;
            float tz = fz - z0;

            // Get averaged heights at the 4 cell corners
            float h00 = GetAveragedVertexHeight(x0, z0);
            float h10 = GetAveragedVertexHeight(x1, z0);
            float h01 = GetAveragedVertexHeight(x0, z1);
            float h11 = GetAveragedVertexHeight(x1, z1);

            // Bilinear interpolation
            float h0 = Mathf.Lerp(h00, h10, tx);
            float h1 = Mathf.Lerp(h01, h11, tx);
            return Mathf.Lerp(h0, h1, tz);
        }

        /// <summary>
        /// Get the averaged vertex height at a world-space vertex position.
        /// Replicates SmoothTerrainBuilder's 4-column averaging logic.
        /// Uses solid heights (skips water) to match the visible terrain mesh.
        /// </summary>
        private float GetAveragedVertexHeight(int worldVx, int worldVz)
        {
            float totalHeight = 0f;
            int count = 0;

            for (int dx = -1; dx <= 0; dx++)
            {
                for (int dz = -1; dz <= 0; dz++)
                {
                    int h = GetSolidHeightAtWorldPos(worldVx + dx, worldVz + dz);
                    if (h >= 0)
                    {
                        totalHeight += h;
                        count++;
                    }
                }
            }

            return count > 0 ? (totalHeight / count) + 1f : TerrainGenerator.SEA_LEVEL + 1f;
        }

        /// <summary>
        /// Bake (or rebake) the NavMesh from the current terrain colliders.
        /// Called after world generation and after any terrain modification.
        ///
        /// Story 2.0: Siedler-Bewegung auf NavMesh migrieren
        /// </summary>
        public void BakeNavMesh()
        {
            if (_navMeshSurface == null)
            {
                _navMeshSurface = gameObject.AddComponent<NavMeshSurface>();
                _navMeshSurface.collectObjects = CollectObjects.Children;
                _navMeshSurface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            }

            _navMeshSurface.BuildNavMesh();
            IsNavMeshReady = true;

            Debug.Log("NavMesh baked successfully.");
        }

        /// <summary>
        /// Flatten terrain in a square area around (centerX, centerZ) so all columns
        /// match the height of the center column. Columns that are too high get their
        /// upper blocks removed (set to Air); columns that are too low get filled up
        /// with the surface type of the center column.
        ///
        /// Rebuilds affected chunk meshes once at the end (not per block).
        ///
        /// Story 0.6: Flatten terrain before placing objects
        /// </summary>
        public void FlattenTerrain(int centerX, int centerZ, int radius, bool rebakeNavMesh = true)
        {
            int targetHeight = GetHeightAtWorldPos(centerX, centerZ);
            if (targetHeight < 0)
                return;

            VoxelType surfaceType = GetSurfaceTypeAtWorldPos(centerX, centerZ);
            if (!surfaceType.IsSolid())
                surfaceType = VoxelType.Grass;

            var affectedChunks = new HashSet<Vector2Int>();

            for (int x = centerX - radius; x <= centerX + radius; x++)
            {
                for (int z = centerZ - radius; z <= centerZ + radius; z++)
                {
                    int currentHeight = GetHeightAtWorldPos(x, z);
                    if (currentHeight < 0)
                        continue;

                    if (currentHeight > targetHeight)
                    {
                        // Remove blocks above target height
                        for (int y = targetHeight + 1; y <= currentHeight; y++)
                            SetBlockInternal(x, y, z, VoxelType.Air, affectedChunks);
                    }
                    else if (currentHeight < targetHeight)
                    {
                        // Fill blocks up to target height
                        for (int y = currentHeight + 1; y <= targetHeight; y++)
                            SetBlockInternal(x, y, z, surfaceType, affectedChunks);
                    }
                }
            }

            // Rebuild all affected chunks once
            foreach (var key in affectedChunks)
            {
                if (_chunks.TryGetValue(key, out var chunk))
                    chunk.RebuildMesh(GetSolidHeightAtWorldPos, GetSolidSurfaceTypeAtWorldPos, chunk.CurrentLod);
            }

            // Rebake NavMesh after terrain modification (Story 2.0)
            if (rebakeNavMesh && IsNavMeshReady)
                BakeNavMesh();
        }

        /// <summary>
        /// Set a block without rebuilding the mesh. Tracks which chunks are affected.
        /// Used by FlattenTerrain for batch modifications.
        /// </summary>
        private void SetBlockInternal(int worldX, int worldY, int worldZ, VoxelType type,
            HashSet<Vector2Int> affectedChunks)
        {
            int chunkX = Mathf.FloorToInt((float)worldX / ChunkData.WIDTH);
            int chunkZ = Mathf.FloorToInt((float)worldZ / ChunkData.DEPTH);
            var key = new Vector2Int(chunkX, chunkZ);

            if (!_chunks.TryGetValue(key, out var chunk))
                return;

            int localX = worldX - chunkX * ChunkData.WIDTH;
            int localZ = worldZ - chunkZ * ChunkData.DEPTH;

            chunk.Data.SetBlock(localX, worldY, localZ, type);
            affectedChunks.Add(key);

            // Also mark neighbor chunks if at boundary (smooth mesh averaging crosses boundaries)
            if (localX <= 0) affectedChunks.Add(new Vector2Int(chunkX - 1, chunkZ));
            if (localX >= ChunkData.WIDTH - 1) affectedChunks.Add(new Vector2Int(chunkX + 1, chunkZ));
            if (localZ <= 0) affectedChunks.Add(new Vector2Int(chunkX, chunkZ - 1));
            if (localZ >= ChunkData.DEPTH - 1) affectedChunks.Add(new Vector2Int(chunkX, chunkZ + 1));
        }

        /// <summary>
        /// Modify a block at a world position and rebuild the affected chunk mesh.
        /// Also rebuilds neighbor chunks if the modification is at a boundary.
        ///
        /// Story 0.5: Terrain-Modifikation aktualisiert Mesh
        /// </summary>
        public void ModifyBlock(int worldX, int worldY, int worldZ, VoxelType newType)
        {
            int chunkX = Mathf.FloorToInt((float)worldX / ChunkData.WIDTH);
            int chunkZ = Mathf.FloorToInt((float)worldZ / ChunkData.DEPTH);
            var key = new Vector2Int(chunkX, chunkZ);

            if (!_chunks.TryGetValue(key, out var chunk))
                return;

            int localX = worldX - chunkX * ChunkData.WIDTH;
            int localZ = worldZ - chunkZ * ChunkData.DEPTH;

            chunk.Data.SetBlock(localX, worldY, localZ, newType);
            chunk.RebuildMesh(GetSolidHeightAtWorldPos, GetSolidSurfaceTypeAtWorldPos, chunk.CurrentLod);

            // Rebuild neighbors if modification is at a chunk boundary (within 1 block of edge).
            // The smooth mesh averaging samples from neighboring chunks at boundaries.
            if (localX <= 0) RebuildNeighbor(chunkX - 1, chunkZ);
            if (localX >= ChunkData.WIDTH - 1) RebuildNeighbor(chunkX + 1, chunkZ);
            if (localZ <= 0) RebuildNeighbor(chunkX, chunkZ - 1);
            if (localZ >= ChunkData.DEPTH - 1) RebuildNeighbor(chunkX, chunkZ + 1);

            // Rebake NavMesh after terrain modification (Story 2.0)
            if (IsNavMeshReady)
                BakeNavMesh();
        }

        /// <summary>
        /// Rebuild a neighbor chunk's mesh if it exists.
        /// </summary>
        private void RebuildNeighbor(int chunkX, int chunkZ)
        {
            var key = new Vector2Int(chunkX, chunkZ);
            if (_chunks.TryGetValue(key, out var neighbor))
            {
                neighbor.RebuildMesh(GetSolidHeightAtWorldPos, GetSolidSurfaceTypeAtWorldPos, neighbor.CurrentLod);
            }
        }

        /// <summary>
        /// Create fallback materials if none were assigned in Inspector.
        /// Uses TerrainSplat shader for textured terrain with blending,
        /// falls back to VertexColorOpaque if splatting shader is not available.
        /// </summary>
        private void EnsureMaterials()
        {
            if (_solidMaterial == null)
            {
                // Prefer the terrain splatting shader, fall back to vertex color.
                // These shaders must be in GraphicsSettings → Always Included Shaders,
                // otherwise Shader.Find() returns null in builds (shader stripping).
                Shader shader = Shader.Find("Terranova/TerrainSplat");
                if (shader == null)
                {
                    Debug.LogWarning("WorldManager: TerrainSplat shader not found, trying VertexColorOpaque...");
                    shader = Shader.Find("Terranova/VertexColorOpaque");
                }
                if (shader == null)
                {
                    Debug.LogWarning("WorldManager: VertexColorOpaque shader not found, trying PropLit...");
                    shader = Shader.Find("Terranova/PropLit");
                }

                if (shader == null)
                {
                    Debug.LogError("WorldManager: No suitable shader found! " +
                        "All terrain shaders were stripped from the build. " +
                        "Add them to GraphicsSettings → Always Included Shaders.");
                    return;
                }

                Debug.Log($"WorldManager: Solid material using shader '{shader.name}'");
                _solidMaterial = new Material(shader);
                _solidMaterial.name = "Terrain_Splat (Auto)";
                _ownsSolidMaterial = true;

                // If using the splatting shader, generate and assign placeholder textures
                if (shader.name == "Terranova/TerrainSplat")
                {
                    AssignPlaceholderTextures(_solidMaterial);
                }
            }

            if (_waterMaterial == null)
            {
                Shader shader = Shader.Find("Terranova/VertexColorTransparent");
                if (shader == null)
                {
                    Debug.LogWarning("WorldManager: VertexColorTransparent shader not found, trying PropLit...");
                    shader = Shader.Find("Terranova/PropLit");
                }

                if (shader == null)
                {
                    Debug.LogError("WorldManager: No suitable water shader found! " +
                        "Add Terranova shaders to GraphicsSettings → Always Included Shaders.");
                    return;
                }

                Debug.Log($"WorldManager: Water material using shader '{shader.name}'");
                _waterMaterial = new Material(shader);
                _waterMaterial.name = "Water_Transparent (Auto)";
                _ownsWaterMaterial = true;

                // Configure transparency for the fallback particle shader
                if (_waterMaterial.HasProperty("_Surface"))
                {
                    _waterMaterial.SetFloat("_Surface", 1f); // 1 = Transparent
                    _waterMaterial.SetFloat("_Blend", 0f);   // 0 = Alpha blend
                    _waterMaterial.renderQueue = 3000;
                }
            }
        }

        /// <summary>
        /// Generate simple procedural placeholder textures for each terrain type
        /// and assign them to the splatting material. Each texture is a base color
        /// with subtle noise variation to avoid a flat, synthetic look.
        ///
        /// Story 0.3: Texturierung und Materialien
        /// </summary>
        private void AssignPlaceholderTextures(Material material)
        {
            const int TEX_SIZE = 128;

            // Base colors matching the vertex color palette
            var grassBase = new Color(0.30f, 0.65f, 0.20f);
            var dirtBase  = new Color(0.55f, 0.36f, 0.16f);
            var stoneBase = new Color(0.52f, 0.52f, 0.52f);
            var sandBase  = new Color(0.90f, 0.85f, 0.55f);

            material.SetTexture("_GrassTex", CreateNoisyTexture(TEX_SIZE, grassBase, 0.08f, 42));
            material.SetTexture("_DirtTex",  CreateNoisyTexture(TEX_SIZE, dirtBase,  0.10f, 137));
            material.SetTexture("_StoneTex", CreateNoisyTexture(TEX_SIZE, stoneBase, 0.12f, 271));
            material.SetTexture("_SandTex",  CreateNoisyTexture(TEX_SIZE, sandBase,  0.06f, 389));
            material.SetFloat("_TexScale", 0.25f);
        }

        /// <summary>
        /// Create a texture with a base color and subtle Perlin noise variation.
        /// The noise prevents the flat, synthetic look that solid colors produce.
        /// </summary>
        private Texture2D CreateNoisyTexture(int size, Color baseColor, float noiseStrength, int seed)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGB24, true);
            tex.wrapMode = TextureWrapMode.Repeat;
            tex.filterMode = FilterMode.Bilinear;

            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // Multi-octave Perlin noise for natural variation
                    float nx = (float)x / size;
                    float ny = (float)y / size;
                    float noise = Mathf.PerlinNoise(nx * 8f + seed, ny * 8f + seed) * 0.6f
                                + Mathf.PerlinNoise(nx * 16f + seed, ny * 16f + seed) * 0.3f
                                + Mathf.PerlinNoise(nx * 32f + seed, ny * 32f + seed) * 0.1f;

                    // Map noise (0..1) to a brightness variation around the base color
                    float variation = (noise - 0.5f) * 2f * noiseStrength;
                    Color pixel = new Color(
                        Mathf.Clamp01(baseColor.r + variation),
                        Mathf.Clamp01(baseColor.g + variation),
                        Mathf.Clamp01(baseColor.b + variation));

                    pixels[y * size + x] = pixel;
                }
            }

            tex.SetPixels(pixels);
            tex.Apply(true); // Generate mipmaps
            _autoTextures.Add(tex);
            return tex;
        }

        private void OnDestroy()
        {
            // Clean up auto-created textures and materials to prevent VRAM leaks
            foreach (var tex in _autoTextures)
            {
                if (tex != null) Destroy(tex);
            }
            _autoTextures.Clear();

            if (_ownsSolidMaterial && _solidMaterial != null)
                Destroy(_solidMaterial);
            if (_ownsWaterMaterial && _waterMaterial != null)
                Destroy(_waterMaterial);

            if (Instance == this)
                Instance = null;
        }
    }
}
