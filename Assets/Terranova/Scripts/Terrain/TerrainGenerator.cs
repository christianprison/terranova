using UnityEngine;
using Terranova.Core;

namespace Terranova.Terrain
{
    /// <summary>
    /// Generates terrain data for chunks using biome-specific layered Perlin noise.
    ///
    /// Biome types produce visibly different terrain:
    ///   Forest    – Gentle rolling hills, mostly grass, some dirt patches. Height 60–75.
    ///   Mountains – Dramatic peaks and valleys, stone at high elevations. Height 55–90.
    ///   Coast     – Flat-ish terrain with water features and sand beaches. Height 58–70.
    ///
    /// Guaranteed start conditions:
    ///   - Water within 30 blocks of world center
    ///   - At least one stone area on the surface
    ///   - Minimum 128x128 surface area (8x8 chunks)
    ///
    /// GDD reference: Sea level at block 64, terrain height 0–256.
    /// </summary>
    public class TerrainGenerator
    {
        // GDD: sea level at block 64
        public const int SEA_LEVEL = 64;

        // Dirt layer thickness before stone
        private const int DIRT_DEPTH = 4;

        // Random offsets for this world's unique terrain (derived from seed)
        private readonly float _seedOffsetX;
        private readonly float _seedOffsetZ;

        // The biome used for terrain shape and surface material
        private readonly BiomeType _biome;

        // Total world size in blocks — set by WorldManager before generation
        private int _worldBlocksZ = 128;

        /// <summary>
        /// Create a terrain generator with a specific seed and biome.
        /// Same seed + same biome = same terrain (deterministic).
        /// </summary>
        public TerrainGenerator(int seed, BiomeType biome)
        {
            _biome = biome;
            var random = new System.Random(seed);
            _seedOffsetX = (float)(random.NextDouble() * 10000);
            _seedOffsetZ = (float)(random.NextDouble() * 10000);
        }

        /// <summary>Tell the generator the total world depth so Coast biome can place the ocean edge.</summary>
        public void SetWorldSize(int blocksZ) { _worldBlocksZ = blocksZ; }

        /// <summary>
        /// Create a terrain generator using GameState seed and biome.
        /// Backward-compatible overload for existing call sites.
        /// </summary>
        public TerrainGenerator(int seed = 42) : this(seed, GameState.SelectedBiome) { }

        /// <summary>
        /// Fill a chunk with terrain data based on its position in the world and the active biome.
        /// </summary>
        public void GenerateChunk(ChunkData chunk)
        {
            for (int x = 0; x < ChunkData.WIDTH; x++)
            {
                for (int z = 0; z < ChunkData.DEPTH; z++)
                {
                    // Convert local chunk position to world position for consistent noise
                    int worldX = chunk.ChunkX * ChunkData.WIDTH + x;
                    int worldZ = chunk.ChunkZ * ChunkData.DEPTH + z;

                    int surfaceHeight = CalculateHeight(worldX, worldZ);
                    FillColumn(chunk, x, z, surfaceHeight, worldX, worldZ);
                }
            }
        }

        /// <summary>
        /// Get the terrain height at any world position.
        /// Useful for placing objects, camera ground detection, etc.
        /// </summary>
        public int GetHeightAtWorldPos(float worldX, float worldZ)
        {
            return CalculateHeight((int)worldX, (int)worldZ);
        }

        /// <summary>
        /// Calculate terrain height using multiple layers of Perlin noise.
        /// The noise is shaped differently per biome to create distinct landscapes.
        /// </summary>
        private int CalculateHeight(int worldX, int worldZ)
        {
            float nx = (worldX + _seedOffsetX) * 0.02f;
            float nz = (worldZ + _seedOffsetZ) * 0.02f;

            // Base noise layers at different frequencies
            float primary = Mathf.PerlinNoise(nx, nz);
            float detail = Mathf.PerlinNoise(nx * 3f, nz * 3f) * 0.3f;
            float macro = Mathf.PerlinNoise(nx * 0.3f, nz * 0.3f);

            float combined = primary + detail;
            int height;

            switch (_biome)
            {
                case BiomeType.Forest:
                    // Gentle rolling hills: height 60–75
                    // Primary noise creates broad hills, macro adds subtle large-scale variation
                    height = SEA_LEVEL - 4 + (int)(combined * 12f + macro * 4f);
                    break;

                case BiomeType.Mountains:
                    // Dramatic peaks and valleys: height 55–90
                    // Ridge noise creates sharp mountain ridges from folded Perlin
                    float ridge = Mathf.Abs(primary - 0.5f) * 2f;
                    height = SEA_LEVEL - 9 + (int)(combined * 25f + ridge * 12f + macro * 8f);
                    break;

                case BiomeType.Coast:
                    // Ocean along the Z=0 edge, land rises toward Z=max.
                    // coastGrad: 0 at Z=0 (ocean) → 1 at Z=worldBlocksZ (inland).
                    float coastGrad = Mathf.Clamp01((float)worldZ / (_worldBlocksZ * 0.4f));
                    // Ocean floor below sea level, land rises above
                    float coastBase = Mathf.Lerp(-10f, 8f, coastGrad) + combined * 4f;
                    height = SEA_LEVEL + (int)coastBase;
                    break;

                default:
                    // Fallback: moderate rolling terrain
                    height = SEA_LEVEL + (int)(combined * 10f);
                    break;
            }

            return Mathf.Clamp(height, 1, ChunkData.HEIGHT - 1);
        }

        /// <summary>
        /// Fill a single vertical column of blocks.
        ///
        /// Layer structure (top to bottom):
        /// - Air (above surface, above sea level)
        /// - Water (above surface, at or below sea level)
        /// - Surface block (biome-dependent: grass, sand, stone)
        /// - Subsurface (dirt or stone depending on elevation)
        /// - Stone (everything deeper)
        /// </summary>
        private void FillColumn(ChunkData chunk, int x, int z, int surfaceHeight,
            int worldX, int worldZ)
        {
            for (int y = 0; y < ChunkData.HEIGHT; y++)
            {
                VoxelType type;

                if (y > surfaceHeight)
                {
                    // Above surface: water if at or below sea level, air otherwise
                    type = y <= SEA_LEVEL ? VoxelType.Water : VoxelType.Air;
                }
                else if (y == surfaceHeight)
                {
                    // Surface block depends on biome and elevation
                    type = GetSurfaceType(surfaceHeight, worldX, worldZ);
                }
                else if (y > surfaceHeight - DIRT_DEPTH)
                {
                    // Subsurface layer: stone if high elevation, dirt otherwise
                    type = surfaceHeight > SEA_LEVEL + 15 ? VoxelType.Stone : VoxelType.Dirt;
                }
                else
                {
                    // Deep underground: solid stone
                    type = VoxelType.Stone;
                }

                chunk.SetBlock(x, y, z, type);
            }
        }

        /// <summary>
        /// Determine the surface block type based on biome, elevation, and position.
        ///
        /// General rules:
        ///   - Near/below sea level is always sand (beach/shoreline)
        ///   - Mountains expose stone at high elevations
        ///   - Coast has wider sand bands near water
        ///   - Forest is mostly grass with occasional dirt patches
        /// </summary>
        private VoxelType GetSurfaceType(int height, int worldX, int worldZ)
        {
            // Universal rule: near or below sea level = sand (beach)
            if (height <= SEA_LEVEL + 1)
                return VoxelType.Sand;

            switch (_biome)
            {
                case BiomeType.Forest:
                    // Mostly grass with occasional dirt patches for visual variety
                    float dirtNoise = Mathf.PerlinNoise(
                        (worldX + _seedOffsetX) * 0.08f,
                        (worldZ + _seedOffsetZ) * 0.08f);
                    return dirtNoise > 0.75f ? VoxelType.Dirt : VoxelType.Grass;

                case BiomeType.Mountains:
                    // High elevations: exposed stone (granite/rock face)
                    if (height > SEA_LEVEL + 20)
                        return VoxelType.Stone;
                    // Mid elevations: mix of stone and grass
                    if (height > SEA_LEVEL + 12)
                    {
                        float stoneNoise = Mathf.PerlinNoise(
                            (worldX + _seedOffsetX) * 0.1f,
                            (worldZ + _seedOffsetZ) * 0.1f);
                        return stoneNoise > 0.5f ? VoxelType.Stone : VoxelType.Grass;
                    }
                    return VoxelType.Grass;

                case BiomeType.Coast:
                    // Wider sand band near water edges
                    if (height <= SEA_LEVEL + 3)
                        return VoxelType.Sand;
                    return VoxelType.Grass;

                default:
                    return VoxelType.Grass;
            }
        }
    }
}
