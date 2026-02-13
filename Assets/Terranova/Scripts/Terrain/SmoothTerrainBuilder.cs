using System.Collections.Generic;
using UnityEngine;

namespace Terranova.Terrain
{
    /// <summary>
    /// Builds a smooth terrain mesh from chunk voxel data.
    ///
    /// Instead of rendering individual block faces (Minecraft style), this builder
    /// treats the voxel heightmap as a continuous surface and generates a smooth
    /// triangle mesh. The result looks like Northgard or Empire Earth terrain.
    ///
    /// Algorithm:
    /// 1. Create a 17×17 vertex grid (one vertex per block corner).
    /// 2. Each vertex height = average of the 4 surrounding column heights.
    ///    This naturally smooths out 1-block height steps.
    /// 3. Generate 2 triangles per grid cell (16×16 = 512 triangles total).
    /// 4. Water is a separate flat mesh at sea level.
    ///
    /// The block data structure (ChunkData) is NOT modified – only the visual
    /// representation changes. Pathfinding and game logic still use block data.
    ///
    /// Story 0.1: Mesh-Generierung aus Voxel-Daten
    /// Story 0.2: Seamless chunk boundaries via custom normals
    /// Story 0.3: Texture splatting with per-vertex blend weights
    /// </summary>
    public static class SmoothTerrainBuilder
    {
        // 17 vertices per side (one per block corner, including the far edge)
        private const int VERTS_PER_SIDE = ChunkData.WIDTH + 1;

        // Sea level from TerrainGenerator
        private const int SEA_LEVEL = TerrainGenerator.SEA_LEVEL;

        // Water surface sits slightly above terrain to avoid z-fighting
        private const float WATER_Y_OFFSET = 0.15f;

        /// <summary>
        /// Callback for querying terrain height at any world position.
        /// Used for smooth interpolation at chunk boundaries.
        /// </summary>
        public delegate int HeightLookup(int worldX, int worldZ);

        /// <summary>
        /// Callback for querying surface block type at any world position.
        /// Used for vertex coloring at chunk boundaries.
        /// </summary>
        public delegate VoxelType SurfaceLookup(int worldX, int worldZ);

        /// <summary>
        /// Build a smooth terrain mesh for the given chunk.
        /// Returns a Mesh with 2 submeshes: [0] terrain (opaque), [1] water (transparent).
        ///
        /// lodStep controls mesh resolution for LOD:
        ///   1 = full detail (17×17 = 289 verts, 512 tris)
        ///   2 = medium (9×9 = 81 verts, 128 tris)
        ///   4 = low (5×5 = 25 verts, 32 tris)
        ///
        /// Story 0.4: Performance und LOD
        /// </summary>
        public static Mesh Build(ChunkData chunk,
            HeightLookup getHeight = null,
            SurfaceLookup getSurface = null,
            int lodStep = 1)
        {
            lodStep = Mathf.Clamp(lodStep, 1, 4);

            // Step 1: Build full vertex data grids (always 17×17 for consistency)
            var heightGrid = new float[VERTS_PER_SIDE, VERTS_PER_SIDE];
            var colorGrid = new Color[VERTS_PER_SIDE, VERTS_PER_SIDE];
            var blendGrid = new Vector4[VERTS_PER_SIDE, VERTS_PER_SIDE];

            FillVertexGrids(chunk, getHeight, getSurface, heightGrid, colorGrid, blendGrid);

            // Step 2: Generate terrain surface mesh (subsampled by lodStep)
            var terrain = new MeshData();
            BuildTerrainSurface(chunk, heightGrid, colorGrid, blendGrid, terrain, lodStep);
            ComputeTerrainNormals(heightGrid, chunk, getHeight, terrain, lodStep);

            // Step 3: Generate water surface mesh (also subsampled by lodStep)
            var water = new MeshData();
            BuildWaterSurface(chunk, heightGrid, water, lodStep);

            // Step 4: Combine into final mesh
            return CombineIntoMesh(terrain, water);
        }

        // ─── Grid Construction ──────────────────────────────────

        /// <summary>
        /// Fill the height, color, and blend-weight grids for all 17×17 vertices.
        ///
        /// Each vertex sits at a block corner shared by up to 4 columns.
        /// Its height is the average of those columns' heights, which
        /// produces the smooth interpolation between block heights.
        ///
        /// Blend weights represent the fraction of each terrain type among the
        /// surrounding columns. This enables soft texture blending at type transitions.
        /// Vector4: x=Grass, y=Dirt, z=Stone, w=Sand.
        ///
        /// Story 0.3: Texture splatting
        /// </summary>
        private static void FillVertexGrids(
            ChunkData chunk,
            HeightLookup getHeight,
            SurfaceLookup getSurface,
            float[,] heightGrid,
            Color[,] colorGrid,
            Vector4[,] blendGrid)
        {
            int originX = chunk.ChunkX * ChunkData.WIDTH;
            int originZ = chunk.ChunkZ * ChunkData.DEPTH;

            for (int vx = 0; vx < VERTS_PER_SIDE; vx++)
            {
                for (int vz = 0; vz < VERTS_PER_SIDE; vz++)
                {
                    // The 4 columns sharing this vertex corner are at
                    // offsets (-1,-1), (0,-1), (-1,0), (0,0) relative to vertex
                    float totalHeight = 0f;
                    int count = 0;

                    // Count occurrences of each surface type for blend weights
                    int grassCount = 0, dirtCount = 0, stoneCount = 0, sandCount = 0;

                    for (int dx = -1; dx <= 0; dx++)
                    {
                        for (int dz = -1; dz <= 0; dz++)
                        {
                            int localX = vx + dx;
                            int localZ = vz + dz;

                            int h;
                            VoxelType st;

                            if (localX >= 0 && localX < ChunkData.WIDTH &&
                                localZ >= 0 && localZ < ChunkData.DEPTH)
                            {
                                // Column is within this chunk
                                h = chunk.GetHeightAt(localX, localZ);
                                st = chunk.GetSurfaceType(localX, localZ);
                            }
                            else if (getHeight != null && getSurface != null)
                            {
                                // Column is in a neighbor chunk
                                int worldX = originX + localX;
                                int worldZ = originZ + localZ;
                                h = getHeight(worldX, worldZ);
                                st = getSurface(worldX, worldZ);
                            }
                            else
                            {
                                // No neighbor data available – skip
                                continue;
                            }

                            if (h < 0)
                                continue; // Out of world bounds

                            totalHeight += h;
                            count++;

                            // Tally surface type for blend weight computation
                            switch (st)
                            {
                                case VoxelType.Grass: grassCount++; break;
                                case VoxelType.Dirt:  dirtCount++;  break;
                                case VoxelType.Stone: stoneCount++; break;
                                case VoxelType.Sand:  sandCount++;  break;
                                // Water columns below sea level are handled by water mesh
                                default: grassCount++; break; // fallback
                            }
                        }
                    }

                    // Height: average of surrounding columns + 1 (surface is on TOP of block)
                    heightGrid[vx, vz] = count > 0
                        ? (totalHeight / count) + 1f
                        : SEA_LEVEL + 1f;

                    // Blend weights: fraction of each type (sum = 1.0)
                    float inv = count > 0 ? 1f / count : 1f;
                    Vector4 weights = new Vector4(
                        grassCount * inv,
                        dirtCount * inv,
                        stoneCount * inv,
                        sandCount * inv);
                    blendGrid[vx, vz] = weights;

                    // Vertex color: weighted blend of type colors (fallback for unlit rendering)
                    Color grassC = GetSurfaceColor(VoxelType.Grass);
                    Color dirtC  = GetSurfaceColor(VoxelType.Dirt);
                    Color stoneC = GetSurfaceColor(VoxelType.Stone);
                    Color sandC  = GetSurfaceColor(VoxelType.Sand);
                    colorGrid[vx, vz] = grassC * weights.x
                                      + dirtC  * weights.y
                                      + stoneC * weights.z
                                      + sandC  * weights.w;
                }
            }
        }

        // ─── Terrain Surface ────────────────────────────────────

        /// <summary>
        /// Generate the terrain surface mesh: a smooth triangulated grid.
        /// At lodStep=1: 16×16 cells × 2 tris = 512 triangles.
        /// At lodStep=2: 8×8 cells × 2 tris = 128 triangles.
        /// At lodStep=4: 4×4 cells × 2 tris = 32 triangles.
        ///
        /// Per vertex: position, color, UV0 (world-space XZ), UV1 (blend weights).
        /// </summary>
        private static void BuildTerrainSurface(
            ChunkData chunk,
            float[,] heights, Color[,] colors, Vector4[,] blends,
            MeshData mesh, int lodStep = 1)
        {
            float worldOriginX = chunk.ChunkX * ChunkData.WIDTH;
            float worldOriginZ = chunk.ChunkZ * ChunkData.DEPTH;

            // Subsampled vertex count per side
            int lodVertsPerSide = (ChunkData.WIDTH / lodStep) + 1;

            // Add vertices at subsampled positions (world-space coordinates to
            // eliminate floating-point seams at chunk boundaries – see Story 0.2)
            for (int gx = 0; gx < lodVertsPerSide; gx++)
            {
                for (int gz = 0; gz < lodVertsPerSide; gz++)
                {
                    // Map LOD grid position back to full-resolution grid position
                    int vx = gx * lodStep;
                    int vz = gz * lodStep;

                    mesh.Vertices.Add(new Vector3(worldOriginX + vx, heights[vx, vz], worldOriginZ + vz));
                    mesh.Colors.Add(colors[vx, vz]);
                    mesh.UVs.Add(new Vector2(worldOriginX + vx, worldOriginZ + vz));
                    mesh.BlendWeights.Add(blends[vx, vz]);
                }
            }

            // Add triangles for each cell (2 per cell)
            int cellCount = lodVertsPerSide - 1;
            for (int x = 0; x < cellCount; x++)
            {
                for (int z = 0; z < cellCount; z++)
                {
                    int v00 = x * lodVertsPerSide + z;
                    int v01 = x * lodVertsPerSide + (z + 1);
                    int v10 = (x + 1) * lodVertsPerSide + z;
                    int v11 = (x + 1) * lodVertsPerSide + (z + 1);

                    mesh.Triangles.Add(v00);
                    mesh.Triangles.Add(v01);
                    mesh.Triangles.Add(v11);

                    mesh.Triangles.Add(v00);
                    mesh.Triangles.Add(v11);
                    mesh.Triangles.Add(v10);
                }
            }
        }

        // ─── Water Surface ──────────────────────────────────────

        /// <summary>
        /// Generate a flat water surface for areas below sea level.
        /// Only creates water quads for cells where at least one vertex
        /// is below the water line.
        /// </summary>
        private static void BuildWaterSurface(ChunkData chunk, float[,] heights, MeshData mesh, int lodStep = 1)
        {
            float waterY = SEA_LEVEL + WATER_Y_OFFSET;
            Color waterColor = new Color(0.15f, 0.4f, 0.75f, 0.7f);

            float worldOriginX = chunk.ChunkX * ChunkData.WIDTH;
            float worldOriginZ = chunk.ChunkZ * ChunkData.DEPTH;

            int cellCount = ChunkData.WIDTH / lodStep;
            for (int cx = 0; cx < cellCount; cx++)
            {
                for (int cz = 0; cz < cellCount; cz++)
                {
                    int x = cx * lodStep;
                    int z = cz * lodStep;

                    // Check if any vertex of this cell is below water
                    bool hasWater = heights[x, z] < waterY
                                 || heights[x + lodStep, z] < waterY
                                 || heights[x, z + lodStep] < waterY
                                 || heights[x + lodStep, z + lodStep] < waterY;

                    if (!hasWater)
                        continue;

                    int vertStart = mesh.Vertices.Count;

                    // Flat quad at water level in world-space (eliminates chunk boundary seams)
                    float wx = worldOriginX + x;
                    float wz = worldOriginZ + z;
                    mesh.Vertices.Add(new Vector3(wx, waterY, wz));
                    mesh.Vertices.Add(new Vector3(wx, waterY, wz + lodStep));
                    mesh.Vertices.Add(new Vector3(wx + lodStep, waterY, wz + lodStep));
                    mesh.Vertices.Add(new Vector3(wx + lodStep, waterY, wz));

                    mesh.Colors.Add(waterColor);
                    mesh.Colors.Add(waterColor);
                    mesh.Colors.Add(waterColor);
                    mesh.Colors.Add(waterColor);

                    // Water is flat – all normals point straight up
                    mesh.Normals.Add(Vector3.up);
                    mesh.Normals.Add(Vector3.up);
                    mesh.Normals.Add(Vector3.up);
                    mesh.Normals.Add(Vector3.up);

                    // Two triangles forming a quad
                    mesh.Triangles.Add(vertStart);
                    mesh.Triangles.Add(vertStart + 1);
                    mesh.Triangles.Add(vertStart + 2);
                    mesh.Triangles.Add(vertStart);
                    mesh.Triangles.Add(vertStart + 2);
                    mesh.Triangles.Add(vertStart + 3);
                }
            }
        }

        // ─── Normal Computation ───────────────────────────────────

        /// <summary>
        /// Compute per-vertex normals from the height grid using central differences.
        ///
        /// Unlike RecalculateNormals() which only considers triangles within this mesh,
        /// this method computes normals analytically from terrain heights. For boundary
        /// vertices (vx=0/16, vz=0/16) where neighbors fall outside the 17×17 grid,
        /// heights are fetched via the HeightLookup delegate. This guarantees that
        /// adjacent chunks produce identical normals at shared boundary vertices,
        /// eliminating visible lighting seams.
        ///
        /// Story 0.2: Chunk-Grenzen nahtlos
        /// </summary>
        private static void ComputeTerrainNormals(
            float[,] heightGrid,
            ChunkData chunk,
            HeightLookup getHeight,
            MeshData terrain,
            int lodStep = 1)
        {
            int lodVertsPerSide = (ChunkData.WIDTH / lodStep) + 1;

            for (int gx = 0; gx < lodVertsPerSide; gx++)
            {
                for (int gz = 0; gz < lodVertsPerSide; gz++)
                {
                    int vx = gx * lodStep;
                    int vz = gz * lodStep;

                    // Central differences at lodStep spacing for consistent normals
                    float hLeft  = GetHeightForNormal(vx - lodStep, vz, heightGrid, chunk, getHeight);
                    float hRight = GetHeightForNormal(vx + lodStep, vz, heightGrid, chunk, getHeight);
                    float hDown  = GetHeightForNormal(vx, vz - lodStep, heightGrid, chunk, getHeight);
                    float hUp    = GetHeightForNormal(vx, vz + lodStep, heightGrid, chunk, getHeight);

                    // Scale the Y component by lodStep so normals are correct for cell size
                    float scale = 2f * lodStep;
                    Vector3 normal = new Vector3(hLeft - hRight, scale, hDown - hUp).normalized;
                    terrain.Normals.Add(normal);
                }
            }
        }

        /// <summary>
        /// Get the averaged vertex height at position (vx, vz) for normal computation.
        /// Returns the cached value from heightGrid if within bounds, otherwise
        /// computes the averaged height using the same 4-column averaging logic.
        /// </summary>
        private static float GetHeightForNormal(
            int vx, int vz,
            float[,] heightGrid,
            ChunkData chunk,
            HeightLookup getHeight)
        {
            // Within the 17×17 grid – use cached value
            if (vx >= 0 && vx < VERTS_PER_SIDE && vz >= 0 && vz < VERTS_PER_SIDE)
                return heightGrid[vx, vz];

            // Outside the grid – compute the averaged height for this vertex position.
            // This happens for boundary normals where we need heights one step beyond
            // the grid edge (e.g. vx=-1 or vx=17).
            return ComputeAveragedHeight(vx, vz, chunk, getHeight);
        }

        /// <summary>
        /// Compute the averaged vertex height at an arbitrary vertex position.
        /// Replicates the same 4-column averaging logic as FillVertexGrids but for
        /// positions outside the 17×17 grid. Used only for boundary normal computation.
        /// </summary>
        private static float ComputeAveragedHeight(
            int vx, int vz,
            ChunkData chunk,
            HeightLookup getHeight)
        {
            int originX = chunk.ChunkX * ChunkData.WIDTH;
            int originZ = chunk.ChunkZ * ChunkData.DEPTH;

            float totalHeight = 0f;
            int count = 0;

            // Average the 4 surrounding columns (same offsets as FillVertexGrids)
            for (int dx = -1; dx <= 0; dx++)
            {
                for (int dz = -1; dz <= 0; dz++)
                {
                    int localX = vx + dx;
                    int localZ = vz + dz;
                    int h;

                    if (localX >= 0 && localX < ChunkData.WIDTH &&
                        localZ >= 0 && localZ < ChunkData.DEPTH)
                    {
                        h = chunk.GetHeightAt(localX, localZ);
                    }
                    else if (getHeight != null)
                    {
                        h = getHeight(originX + localX, originZ + localZ);
                    }
                    else
                    {
                        continue;
                    }

                    if (h < 0)
                        continue;

                    totalHeight += h;
                    count++;
                }
            }

            return count > 0 ? (totalHeight / count) + 1f : SEA_LEVEL + 1f;
        }

        // ─── Color Mapping ──────────────────────────────────────

        /// <summary>
        /// Map voxel surface type to vertex color.
        /// Same palette as the old ChunkMeshBuilder for visual consistency.
        /// </summary>
        private static Color GetSurfaceColor(VoxelType type)
        {
            return type switch
            {
                VoxelType.Grass => new Color(0.30f, 0.65f, 0.20f),
                VoxelType.Dirt  => new Color(0.55f, 0.36f, 0.16f),
                VoxelType.Stone => new Color(0.52f, 0.52f, 0.52f),
                VoxelType.Sand  => new Color(0.90f, 0.85f, 0.55f),
                VoxelType.Water => new Color(0.15f, 0.4f, 0.75f),
                _               => Color.magenta
            };
        }

        // ─── Mesh Assembly ──────────────────────────────────────

        /// <summary>
        /// Combine terrain and water into a single mesh with 2 submeshes.
        /// Submesh 0 = terrain (opaque), Submesh 1 = water (transparent).
        /// Same structure as ChunkMeshBuilder for ChunkRenderer compatibility.
        /// </summary>
        private static Mesh CombineIntoMesh(MeshData terrain, MeshData water)
        {
            var mesh = new Mesh();
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

            // Merge vertices: terrain first, then water
            int terrainVertCount = terrain.Vertices.Count;
            var allVerts = new List<Vector3>(terrain.Vertices);
            allVerts.AddRange(water.Vertices);

            var allColors = new List<Color>(terrain.Colors);
            allColors.AddRange(water.Colors);

            var allNormals = new List<Vector3>(terrain.Normals);
            allNormals.AddRange(water.Normals);

            // Merge UVs: terrain has world-space XZ, water gets zero UVs
            var allUVs = new List<Vector2>(terrain.UVs);
            for (int i = 0; i < water.Vertices.Count; i++)
                allUVs.Add(Vector2.zero);

            // Merge blend weights: terrain has type weights, water gets zero
            var allBlends = new List<Vector4>(terrain.BlendWeights);
            for (int i = 0; i < water.Vertices.Count; i++)
                allBlends.Add(Vector4.zero);

            // Offset water triangle indices
            var waterTris = new List<int>(water.Triangles.Count);
            for (int i = 0; i < water.Triangles.Count; i++)
                waterTris.Add(water.Triangles[i] + terrainVertCount);

            mesh.subMeshCount = 2;
            mesh.SetVertices(allVerts);
            mesh.SetColors(allColors);
            mesh.SetNormals(allNormals);
            mesh.SetUVs(0, allUVs);            // UV0: world-space texture coords
            mesh.SetUVs(1, allBlends);          // UV1: terrain type blend weights
            mesh.SetTriangles(terrain.Triangles, 0);
            mesh.SetTriangles(waterTris, 1);

            // No RecalculateNormals() – custom normals for seamless chunk boundaries
            mesh.RecalculateBounds();

            return mesh;
        }

        /// <summary>
        /// Intermediate container for mesh geometry being built.
        /// </summary>
        private class MeshData
        {
            public readonly List<Vector3> Vertices = new();
            public readonly List<int> Triangles = new();
            public readonly List<Color> Colors = new();
            public readonly List<Vector3> Normals = new();
            public readonly List<Vector2> UVs = new();          // UV0: world-space XZ
            public readonly List<Vector4> BlendWeights = new(); // UV1: terrain type weights
        }
    }
}
