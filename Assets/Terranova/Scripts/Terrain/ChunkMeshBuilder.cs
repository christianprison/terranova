using System.Collections.Generic;
using UnityEngine;

namespace Terranova.Terrain
{
    /// <summary>
    /// Builds a Unity Mesh from chunk voxel data using per-face generation.
    ///
    /// Algorithm: For each solid block, check all 6 neighbors. If a neighbor
    /// is transparent (air or water), add a quad face on that side. This is the
    /// simplest Minecraft-style mesh approach. It produces more triangles than
    /// greedy meshing, but is easier to understand and debug.
    ///
    /// Optimization note: Greedy meshing merges adjacent same-type faces into
    /// larger quads. We can add this later if performance requires it (GDD:
    /// "make it work → make it right → make it fast").
    ///
    /// The mesh uses vertex colors instead of textures for MS1 prototyping.
    /// Two submeshes: 0 = solid blocks (opaque), 1 = water (transparent).
    /// </summary>
    public static class ChunkMeshBuilder
    {
        // The 6 face directions, indexed 0–5
        // Order: Top, Bottom, Right, Left, Front, Back
        private static readonly Vector3Int[] DIRECTIONS =
        {
            new Vector3Int( 0,  1,  0),  // 0: Top    (+Y)
            new Vector3Int( 0, -1,  0),  // 1: Bottom (-Y)
            new Vector3Int( 1,  0,  0),  // 2: Right  (+X)
            new Vector3Int(-1,  0,  0),  // 3: Left   (-X)
            new Vector3Int( 0,  0,  1),  // 4: Front  (+Z)
            new Vector3Int( 0,  0, -1),  // 5: Back   (-Z)
        };

        /// <summary>
        /// Delegate for looking up blocks in neighboring chunks.
        /// Parameters: worldX, worldY, worldZ → returns VoxelType at that position.
        /// If null, out-of-bounds positions are treated as Air (faces rendered at chunk edges).
        /// </summary>
        public delegate VoxelType NeighborLookup(int worldX, int worldY, int worldZ);

        /// <summary>
        /// Build a complete mesh for the given chunk.
        /// Returns a Mesh with 2 submeshes: [0] solid blocks, [1] water.
        /// </summary>
        public static Mesh Build(ChunkData chunk, NeighborLookup neighborLookup = null)
        {
            // Separate geometry lists for solid blocks and water
            var solid = new MeshData();
            var water = new MeshData();

            for (int x = 0; x < ChunkData.WIDTH; x++)
            {
                for (int y = 0; y < ChunkData.HEIGHT; y++)
                {
                    for (int z = 0; z < ChunkData.DEPTH; z++)
                    {
                        VoxelType block = chunk.GetBlock(x, y, z);

                        if (block == VoxelType.Air)
                            continue;

                        if (block == VoxelType.Water)
                            AddWaterFaces(chunk, x, y, z, water, neighborLookup);
                        else
                            AddSolidFaces(chunk, x, y, z, block, solid, neighborLookup);
                    }
                }
            }

            return CombineIntoMesh(solid, water);
        }

        /// <summary>
        /// For a solid block, add a face for each side that borders a transparent block.
        /// </summary>
        private static void AddSolidFaces(ChunkData chunk, int x, int y, int z,
            VoxelType block, MeshData mesh, NeighborLookup neighborLookup)
        {
            Color color = GetBlockColor(block, y);

            for (int face = 0; face < 6; face++)
            {
                Vector3Int dir = DIRECTIONS[face];
                int nx = x + dir.x;
                int ny = y + dir.y;
                int nz = z + dir.z;

                VoxelType neighbor = GetBlock(chunk, nx, ny, nz, neighborLookup);

                // Render face only if the neighbor is see-through
                if (neighbor.IsTransparent())
                {
                    // Darken side and bottom faces slightly for visual depth
                    Color faceColor = ApplyFaceShading(color, face);
                    AddQuad(new Vector3(x, y, z), face, faceColor, mesh);
                }
            }
        }

        /// <summary>
        /// Water only renders its top face when exposed to air.
        /// The surface is slightly lowered (0.1 blocks) so it doesn't z-fight
        /// with adjacent solid blocks.
        /// </summary>
        private static void AddWaterFaces(ChunkData chunk, int x, int y, int z,
            MeshData mesh, NeighborLookup neighborLookup)
        {
            VoxelType above = GetBlock(chunk, x, y + 1, z, neighborLookup);

            if (above == VoxelType.Air)
            {
                Color waterColor = new Color(0.15f, 0.4f, 0.75f, 0.7f);
                // Lower the water surface slightly for a nice visual effect
                AddQuad(new Vector3(x, y - 0.1f, z), 0, waterColor, mesh);
            }
        }

        /// <summary>
        /// Look up a block, handling chunk boundaries.
        /// Within bounds: use chunk data directly.
        /// Out of bounds: use neighborLookup if available, otherwise Air.
        /// </summary>
        private static VoxelType GetBlock(ChunkData chunk, int x, int y, int z,
            NeighborLookup neighborLookup)
        {
            bool inBounds = x >= 0 && x < ChunkData.WIDTH
                         && y >= 0 && y < ChunkData.HEIGHT
                         && z >= 0 && z < ChunkData.DEPTH;

            if (inBounds)
                return chunk.GetBlock(x, y, z);

            if (neighborLookup != null)
            {
                int worldX = chunk.ChunkX * ChunkData.WIDTH + x;
                int worldZ = chunk.ChunkZ * ChunkData.DEPTH + z;
                return neighborLookup(worldX, y, worldZ);
            }

            return VoxelType.Air;
        }

        /// <summary>
        /// Simple face shading: top faces are brightest, side faces dimmer,
        /// bottom faces darkest. This gives the terrain visual depth without
        /// needing a real lighting system.
        /// </summary>
        private static Color ApplyFaceShading(Color baseColor, int faceIndex)
        {
            float shade = faceIndex switch
            {
                0 => 1.0f,   // Top: full brightness
                1 => 0.5f,   // Bottom: darkest
                2 => 0.8f,   // Right
                3 => 0.7f,   // Left
                4 => 0.9f,   // Front
                5 => 0.6f,   // Back
                _ => 1.0f
            };

            return new Color(baseColor.r * shade, baseColor.g * shade, baseColor.b * shade, baseColor.a);
        }

        /// <summary>
        /// Map voxel types to colors for vertex coloring.
        /// In later milestones, this will be replaced with texture atlas UVs.
        /// The y parameter adds subtle depth-based variation to stone.
        /// </summary>
        private static Color GetBlockColor(VoxelType type, int y)
        {
            return type switch
            {
                VoxelType.Grass => new Color(0.30f, 0.65f, 0.20f),  // Natural green
                VoxelType.Dirt  => new Color(0.55f, 0.36f, 0.16f),  // Earth brown
                VoxelType.Stone => new Color(                         // Gray with subtle variation
                    0.50f + (y % 3) * 0.02f,
                    0.50f + (y % 3) * 0.02f,
                    0.50f + (y % 3) * 0.02f),
                VoxelType.Sand  => new Color(0.90f, 0.85f, 0.55f),  // Warm sand
                _               => Color.magenta                      // Error: should never appear
            };
        }

        /// <summary>
        /// Add a quad (2 triangles, 4 vertices) for one face of a block.
        /// blockPos is the block's bottom-south-west corner (each block is 1×1×1).
        /// </summary>
        private static void AddQuad(Vector3 blockPos, int faceIndex, Color color, MeshData mesh)
        {
            int vertStart = mesh.Vertices.Count;

            // Get the 4 corners for this face
            GetFaceVertices(blockPos, faceIndex, out Vector3 v0, out Vector3 v1,
                out Vector3 v2, out Vector3 v3);

            mesh.Vertices.Add(v0);
            mesh.Vertices.Add(v1);
            mesh.Vertices.Add(v2);
            mesh.Vertices.Add(v3);

            mesh.Colors.Add(color);
            mesh.Colors.Add(color);
            mesh.Colors.Add(color);
            mesh.Colors.Add(color);

            // Two triangles forming a quad (clockwise winding for Unity)
            mesh.Triangles.Add(vertStart);
            mesh.Triangles.Add(vertStart + 1);
            mesh.Triangles.Add(vertStart + 2);
            mesh.Triangles.Add(vertStart);
            mesh.Triangles.Add(vertStart + 2);
            mesh.Triangles.Add(vertStart + 3);
        }

        /// <summary>
        /// Returns the 4 corner vertices of a block face.
        /// Vertices are ordered for clockwise winding (Unity's front-face convention).
        /// </summary>
        private static void GetFaceVertices(Vector3 pos, int faceIndex,
            out Vector3 v0, out Vector3 v1, out Vector3 v2, out Vector3 v3)
        {
            float x = pos.x, y = pos.y, z = pos.z;

            switch (faceIndex)
            {
                case 0: // Top (+Y)
                    v0 = new Vector3(x,     y + 1, z);
                    v1 = new Vector3(x,     y + 1, z + 1);
                    v2 = new Vector3(x + 1, y + 1, z + 1);
                    v3 = new Vector3(x + 1, y + 1, z);
                    break;
                case 1: // Bottom (-Y)
                    v0 = new Vector3(x,     y, z + 1);
                    v1 = new Vector3(x,     y, z);
                    v2 = new Vector3(x + 1, y, z);
                    v3 = new Vector3(x + 1, y, z + 1);
                    break;
                case 2: // Right (+X)
                    v0 = new Vector3(x + 1, y,     z);
                    v1 = new Vector3(x + 1, y + 1, z);
                    v2 = new Vector3(x + 1, y + 1, z + 1);
                    v3 = new Vector3(x + 1, y,     z + 1);
                    break;
                case 3: // Left (-X)
                    v0 = new Vector3(x, y,     z + 1);
                    v1 = new Vector3(x, y + 1, z + 1);
                    v2 = new Vector3(x, y + 1, z);
                    v3 = new Vector3(x, y,     z);
                    break;
                case 4: // Front (+Z)
                    v0 = new Vector3(x + 1, y,     z + 1);
                    v1 = new Vector3(x + 1, y + 1, z + 1);
                    v2 = new Vector3(x,     y + 1, z + 1);
                    v3 = new Vector3(x,     y,     z + 1);
                    break;
                default: // Back (-Z)
                    v0 = new Vector3(x,     y,     z);
                    v1 = new Vector3(x,     y + 1, z);
                    v2 = new Vector3(x + 1, y + 1, z);
                    v3 = new Vector3(x + 1, y,     z);
                    break;
            }
        }

        /// <summary>
        /// Combine solid and water MeshData into a single Mesh with 2 submeshes.
        /// Submesh 0 = solid (rendered with opaque material).
        /// Submesh 1 = water (rendered with transparent material).
        /// </summary>
        private static Mesh CombineIntoMesh(MeshData solid, MeshData water)
        {
            var mesh = new Mesh();

            // UInt32 index format supports meshes with more than 65,535 vertices.
            // Chunks with lots of exposed faces can easily exceed that limit.
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

            // Merge vertex arrays: solid first, then water
            int solidVertCount = solid.Vertices.Count;
            var allVerts = new List<Vector3>(solid.Vertices);
            allVerts.AddRange(water.Vertices);

            var allColors = new List<Color>(solid.Colors);
            allColors.AddRange(water.Colors);

            // Offset water triangle indices by the solid vertex count
            var waterTris = new List<int>(water.Triangles.Count);
            for (int i = 0; i < water.Triangles.Count; i++)
                waterTris.Add(water.Triangles[i] + solidVertCount);

            mesh.subMeshCount = 2;
            mesh.SetVertices(allVerts);
            mesh.SetColors(allColors);
            mesh.SetTriangles(solid.Triangles, 0);  // Submesh 0: solid
            mesh.SetTriangles(waterTris, 1);         // Submesh 1: water

            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            return mesh;
        }

        /// <summary>
        /// Intermediate container for mesh geometry being built.
        /// Keeps vertex, triangle, and color lists together.
        /// </summary>
        private class MeshData
        {
            public readonly List<Vector3> Vertices = new();
            public readonly List<int> Triangles = new();
            public readonly List<Color> Colors = new();
        }
    }
}
