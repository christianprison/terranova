using NUnit.Framework;
using Terranova.Terrain;

namespace Terranova.Tests.EditMode
{
    /// <summary>
    /// Tests for TerrainGenerator – validates terrain generation produces
    /// correct layer structure and is deterministic from a seed.
    /// </summary>
    public class TerrainGeneratorTests
    {
        [Test]
        public void GenerateChunk_ProducesNonEmptyTerrain()
        {
            var generator = new TerrainGenerator(seed: 1);
            var chunk = new ChunkData(0, 0);

            generator.GenerateChunk(chunk);

            // At least some blocks should be non-air
            bool hasBlocks = false;
            for (int x = 0; x < ChunkData.WIDTH && !hasBlocks; x++)
                for (int z = 0; z < ChunkData.DEPTH && !hasBlocks; z++)
                    if (chunk.GetHeightAt(x, z) > 0)
                        hasBlocks = true;

            Assert.IsTrue(hasBlocks, "Generated chunk should contain non-air blocks");
        }

        [Test]
        public void GenerateChunk_SurfaceIsGrassOrSand()
        {
            var generator = new TerrainGenerator(seed: 42);
            var chunk = new ChunkData(0, 0);

            generator.GenerateChunk(chunk);

            // Check all surface blocks are grass or sand (Grassland biome)
            for (int x = 0; x < ChunkData.WIDTH; x++)
            {
                for (int z = 0; z < ChunkData.DEPTH; z++)
                {
                    VoxelType surface = chunk.GetSurfaceType(x, z);
                    // Surface should be Grass, Sand, or Water (for underwater columns)
                    bool valid = surface == VoxelType.Grass
                              || surface == VoxelType.Sand
                              || surface == VoxelType.Water;
                    Assert.IsTrue(valid,
                        $"Surface at ({x},{z}) is {surface}, expected Grass, Sand, or Water");
                }
            }
        }

        [Test]
        public void GenerateChunk_HasStoneAtDepth()
        {
            var generator = new TerrainGenerator(seed: 42);
            var chunk = new ChunkData(0, 0);

            generator.GenerateChunk(chunk);

            // Deep underground should be stone
            VoxelType deepBlock = chunk.GetBlock(8, 5, 8);
            Assert.AreEqual(VoxelType.Stone, deepBlock,
                "Deep underground blocks should be stone");
        }

        [Test]
        public void GenerateChunk_HasDirtBelowSurface()
        {
            var generator = new TerrainGenerator(seed: 42);
            var chunk = new ChunkData(2, 2);

            generator.GenerateChunk(chunk);

            // Find a grass column and check there's dirt below
            for (int x = 0; x < ChunkData.WIDTH; x++)
            {
                for (int z = 0; z < ChunkData.DEPTH; z++)
                {
                    int height = chunk.GetHeightAt(x, z);
                    if (height > TerrainGenerator.SEA_LEVEL + 1
                        && chunk.GetBlock(x, height, z) == VoxelType.Grass)
                    {
                        // Block just below grass should be dirt
                        VoxelType below = chunk.GetBlock(x, height - 1, z);
                        Assert.AreEqual(VoxelType.Dirt, below,
                            $"Block below grass at ({x},{height},{z}) should be dirt");
                        return; // One check is enough
                    }
                }
            }
        }

        [Test]
        public void GenerateChunk_SameSeedProducesSameResult()
        {
            // Determinism: same seed + same chunk position = identical terrain
            var gen1 = new TerrainGenerator(seed: 99);
            var gen2 = new TerrainGenerator(seed: 99);

            var chunk1 = new ChunkData(3, 5);
            var chunk2 = new ChunkData(3, 5);

            gen1.GenerateChunk(chunk1);
            gen2.GenerateChunk(chunk2);

            // Compare a sampling of blocks
            for (int x = 0; x < ChunkData.WIDTH; x += 4)
            {
                for (int z = 0; z < ChunkData.DEPTH; z += 4)
                {
                    for (int y = 0; y < 100; y += 10)
                    {
                        Assert.AreEqual(
                            chunk1.GetBlock(x, y, z),
                            chunk2.GetBlock(x, y, z),
                            $"Block mismatch at ({x},{y},{z}) with same seed");
                    }
                }
            }
        }

        [Test]
        public void GenerateChunk_DifferentSeedsProduceDifferentTerrain()
        {
            var gen1 = new TerrainGenerator(seed: 1);
            var gen2 = new TerrainGenerator(seed: 2);

            var chunk1 = new ChunkData(0, 0);
            var chunk2 = new ChunkData(0, 0);

            gen1.GenerateChunk(chunk1);
            gen2.GenerateChunk(chunk2);

            // At least one block should differ between different seeds
            bool hasDifference = false;
            for (int x = 0; x < ChunkData.WIDTH && !hasDifference; x++)
                for (int z = 0; z < ChunkData.DEPTH && !hasDifference; z++)
                    if (chunk1.GetHeightAt(x, z) != chunk2.GetHeightAt(x, z))
                        hasDifference = true;

            Assert.IsTrue(hasDifference, "Different seeds should produce different terrain");
        }

        [Test]
        public void SeaLevel_MatchesGDDSpec()
        {
            Assert.AreEqual(64, TerrainGenerator.SEA_LEVEL,
                "GDD specifies sea level at block 64");
        }
    }
}
