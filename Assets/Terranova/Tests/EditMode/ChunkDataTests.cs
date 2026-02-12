using NUnit.Framework;
using Terranova.Terrain;

namespace Terranova.Tests.EditMode
{
    /// <summary>
    /// Tests for ChunkData – the core voxel storage class.
    /// Validates block get/set, bounds checking, and height queries.
    /// </summary>
    public class ChunkDataTests
    {
        [Test]
        public void NewChunk_AllBlocksAreAir()
        {
            var chunk = new ChunkData(0, 0);

            Assert.AreEqual(VoxelType.Air, chunk.GetBlock(0, 0, 0));
            Assert.AreEqual(VoxelType.Air, chunk.GetBlock(8, 128, 8));
            Assert.AreEqual(VoxelType.Air, chunk.GetBlock(15, 255, 15));
        }

        [Test]
        public void SetBlock_GetBlock_RoundTrips()
        {
            var chunk = new ChunkData(0, 0);

            chunk.SetBlock(5, 10, 7, VoxelType.Stone);
            Assert.AreEqual(VoxelType.Stone, chunk.GetBlock(5, 10, 7));

            chunk.SetBlock(0, 0, 0, VoxelType.Grass);
            Assert.AreEqual(VoxelType.Grass, chunk.GetBlock(0, 0, 0));

            chunk.SetBlock(15, 255, 15, VoxelType.Water);
            Assert.AreEqual(VoxelType.Water, chunk.GetBlock(15, 255, 15));
        }

        [Test]
        public void GetBlock_OutOfBounds_ReturnsAir()
        {
            var chunk = new ChunkData(0, 0);

            Assert.AreEqual(VoxelType.Air, chunk.GetBlock(-1, 0, 0));
            Assert.AreEqual(VoxelType.Air, chunk.GetBlock(16, 0, 0));
            Assert.AreEqual(VoxelType.Air, chunk.GetBlock(0, -1, 0));
            Assert.AreEqual(VoxelType.Air, chunk.GetBlock(0, 256, 0));
            Assert.AreEqual(VoxelType.Air, chunk.GetBlock(0, 0, -1));
            Assert.AreEqual(VoxelType.Air, chunk.GetBlock(0, 0, 16));
        }

        [Test]
        public void SetBlock_OutOfBounds_DoesNotThrow()
        {
            var chunk = new ChunkData(0, 0);

            // These should silently do nothing
            Assert.DoesNotThrow(() => chunk.SetBlock(-1, 0, 0, VoxelType.Stone));
            Assert.DoesNotThrow(() => chunk.SetBlock(16, 0, 0, VoxelType.Stone));
            Assert.DoesNotThrow(() => chunk.SetBlock(0, 256, 0, VoxelType.Stone));
        }

        [Test]
        public void GetHeightAt_ReturnsHighestNonAirBlock()
        {
            var chunk = new ChunkData(0, 0);

            // Build a column: stone at y=0..60, dirt at y=61..63, grass at y=64
            for (int y = 0; y <= 60; y++)
                chunk.SetBlock(3, y, 3, VoxelType.Stone);
            for (int y = 61; y <= 63; y++)
                chunk.SetBlock(3, y, 3, VoxelType.Dirt);
            chunk.SetBlock(3, 64, 3, VoxelType.Grass);

            Assert.AreEqual(64, chunk.GetHeightAt(3, 3));
        }

        [Test]
        public void GetHeightAt_EmptyColumn_ReturnsNegativeOne()
        {
            var chunk = new ChunkData(0, 0);
            Assert.AreEqual(-1, chunk.GetHeightAt(5, 5));
        }

        [Test]
        public void GetSurfaceType_ReturnsTopBlockType()
        {
            var chunk = new ChunkData(0, 0);

            chunk.SetBlock(4, 60, 4, VoxelType.Stone);
            chunk.SetBlock(4, 61, 4, VoxelType.Grass);

            Assert.AreEqual(VoxelType.Grass, chunk.GetSurfaceType(4, 4));
        }

        [Test]
        public void GetSurfaceType_EmptyColumn_ReturnsAir()
        {
            var chunk = new ChunkData(0, 0);
            Assert.AreEqual(VoxelType.Air, chunk.GetSurfaceType(7, 7));
        }

        [Test]
        public void ChunkCoordinates_AreStored()
        {
            var chunk = new ChunkData(3, 7);
            Assert.AreEqual(3, chunk.ChunkX);
            Assert.AreEqual(7, chunk.ChunkZ);
        }

        [Test]
        public void Constants_MatchGDDSpec()
        {
            // GDD spec: chunk size 16×16×256
            Assert.AreEqual(16, ChunkData.WIDTH);
            Assert.AreEqual(16, ChunkData.DEPTH);
            Assert.AreEqual(256, ChunkData.HEIGHT);
            Assert.AreEqual(16 * 16 * 256, ChunkData.TOTAL_BLOCKS);
        }
    }
}
