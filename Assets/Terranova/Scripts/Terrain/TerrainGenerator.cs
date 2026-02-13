using UnityEngine;

namespace Terranova.Terrain
{
    /// <summary>
    /// Generates terrain data for chunks using layered Perlin noise.
    ///
    /// For MS1: Single Grassland biome with rolling hills, a water level,
    /// and natural-looking beaches. Later milestones will add biome-specific
    /// generation (desert dunes, mountain peaks, etc.).
    ///
    /// GDD reference: Sea level at block 64, terrain height 0–256.
    /// </summary>
    public class TerrainGenerator
    {
        // GDD: sea level at block 64
        public const int SEA_LEVEL = 64;

        // How far above sea level hills can rise
        private const int MAX_HILL_HEIGHT = 20;

        // How far below sea level valleys can go (creates water pools)
        private const int MAX_VALLEY_DEPTH = 8;

        // Noise frequencies – smaller values = smoother, larger hills
        private const float PRIMARY_SCALE = 0.02f;
        private const float DETAIL_SCALE = 0.08f;
        private const float DETAIL_WEIGHT = 0.3f;

        // Dirt layer thickness before stone
        private const int DIRT_DEPTH = 4;

        // Random offsets for this world's unique terrain
        private readonly float _seedOffsetX;
        private readonly float _seedOffsetZ;

        /// <summary>
        /// Create a terrain generator with a specific seed.
        /// Same seed = same terrain (deterministic).
        /// </summary>
        public TerrainGenerator(int seed = 42)
        {
            var random = new System.Random(seed);
            _seedOffsetX = (float)(random.NextDouble() * 10000);
            _seedOffsetZ = (float)(random.NextDouble() * 10000);
        }

        /// <summary>
        /// Fill a chunk with terrain data based on its position in the world.
        /// </summary>
        public void GenerateChunk(ChunkData chunk)
        {
            for (int x = 0; x < ChunkData.WIDTH; x++)
            {
                for (int z = 0; z < ChunkData.DEPTH; z++)
                {
                    // Convert local chunk position to world position for consistent noise
                    float worldX = chunk.ChunkX * ChunkData.WIDTH + x;
                    float worldZ = chunk.ChunkZ * ChunkData.DEPTH + z;

                    int surfaceHeight = CalculateHeight(worldX, worldZ);
                    FillColumn(chunk, x, z, surfaceHeight);
                }
            }
        }

        /// <summary>
        /// Get the terrain height at any world position.
        /// Useful for placing objects, camera ground detection, etc.
        /// </summary>
        public int GetHeightAtWorldPos(float worldX, float worldZ)
        {
            return CalculateHeight(worldX, worldZ);
        }

        /// <summary>
        /// Calculate terrain height using two layers of Perlin noise.
        /// Primary layer creates big rolling hills; detail layer adds small bumps.
        /// </summary>
        private int CalculateHeight(float worldX, float worldZ)
        {
            // Primary noise: large-scale terrain shape
            float primary = Mathf.PerlinNoise(
                (worldX + _seedOffsetX) * PRIMARY_SCALE,
                (worldZ + _seedOffsetZ) * PRIMARY_SCALE
            );

            // Detail noise: small bumps for natural feel
            float detail = Mathf.PerlinNoise(
                (worldX + _seedOffsetX) * DETAIL_SCALE,
                (worldZ + _seedOffsetZ) * DETAIL_SCALE
            );

            // Combine: primary terrain + subtle detail variation
            float combined = primary + detail * DETAIL_WEIGHT;

            // Map noise (roughly 0–1.3) to height range
            // Centered around sea level, with hills above and valleys below
            int height = SEA_LEVEL - MAX_VALLEY_DEPTH
                         + Mathf.RoundToInt(combined * (MAX_HILL_HEIGHT + MAX_VALLEY_DEPTH));

            return Mathf.Clamp(height, 1, ChunkData.HEIGHT - 1);
        }

        /// <summary>
        /// Fill a single vertical column of blocks.
        ///
        /// Layer structure (top to bottom):
        /// - Air (above surface, above sea level)
        /// - Water (above surface, below sea level)
        /// - Grass or Sand (surface block)
        /// - Dirt (3–4 blocks below surface)
        /// - Stone (everything deeper)
        /// </summary>
        private void FillColumn(ChunkData chunk, int x, int z, int surfaceHeight)
        {
            for (int y = 0; y < ChunkData.HEIGHT; y++)
            {
                VoxelType type;

                if (y > surfaceHeight)
                {
                    // Above surface: water if below sea level, air otherwise
                    type = y <= SEA_LEVEL ? VoxelType.Water : VoxelType.Air;
                }
                else if (y == surfaceHeight)
                {
                    // Surface block varies by elevation:
                    //   Sand  – near water edges (beach)
                    //   Dirt  – low-lying areas just above beach
                    //   Grass – normal terrain
                    //   Stone – exposed rock on high peaks
                    if (surfaceHeight <= SEA_LEVEL + 1)
                        type = VoxelType.Sand;
                    else if (surfaceHeight <= SEA_LEVEL + 3)
                        type = VoxelType.Dirt;
                    else if (surfaceHeight >= SEA_LEVEL + MAX_HILL_HEIGHT - 4)
                        type = VoxelType.Stone;
                    else
                        type = VoxelType.Grass;
                }
                else if (y > surfaceHeight - DIRT_DEPTH)
                {
                    // Just below surface: dirt layer
                    type = VoxelType.Dirt;
                }
                else
                {
                    // Deep underground: solid stone
                    type = VoxelType.Stone;
                }

                chunk.SetBlock(x, y, z, type);
            }
        }
    }
}
