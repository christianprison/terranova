namespace Terranova.Terrain
{
    /// <summary>
    /// Stores voxel data for a single chunk: 16×16×256 blocks.
    ///
    /// This is the simulation-layer data – no rendering logic here.
    /// One chunk represents a 16×16 meter column of the world, 256 meters tall.
    ///
    /// Memory: 16×16×256 × 1 byte (VoxelType) = 64 KB per chunk.
    /// An 8×8 chunk world = 64 chunks = ~4 MB. Very manageable.
    ///
    /// Coordinate system: X and Z are horizontal (0–15), Y is vertical (0–255).
    /// Block (0,0,0) is the bottom-south-west corner of the chunk.
    /// </summary>
    public class ChunkData
    {
        // GDD spec: 16×16×256 blocks per chunk
        public const int WIDTH = 16;    // X axis
        public const int DEPTH = 16;    // Z axis
        public const int HEIGHT = 256;  // Y axis
        public const int TOTAL_BLOCKS = WIDTH * DEPTH * HEIGHT;

        // Flat array of all blocks. Indexed as: x + (z * WIDTH) + (y * WIDTH * DEPTH)
        private readonly VoxelType[] _blocks;

        // Position in chunk-grid coordinates (not world coordinates).
        // World position = ChunkX * WIDTH, ChunkZ * DEPTH.
        public int ChunkX { get; }
        public int ChunkZ { get; }

        public ChunkData(int chunkX, int chunkZ)
        {
            ChunkX = chunkX;
            ChunkZ = chunkZ;
            _blocks = new VoxelType[TOTAL_BLOCKS];
        }

        /// <summary>
        /// Get the block type at a local position within this chunk.
        /// Returns Air for out-of-bounds coordinates (safe to call with any values).
        /// </summary>
        public VoxelType GetBlock(int x, int y, int z)
        {
            if (!IsInBounds(x, y, z))
                return VoxelType.Air;
            return _blocks[ToIndex(x, y, z)];
        }

        /// <summary>
        /// Set the block type at a local position. Ignores out-of-bounds silently.
        /// </summary>
        public void SetBlock(int x, int y, int z, VoxelType type)
        {
            if (!IsInBounds(x, y, z))
                return;
            _blocks[ToIndex(x, y, z)] = type;
        }

        /// <summary>
        /// Find the highest non-air block in a column (x, z).
        /// Returns -1 if the entire column is air.
        /// Used for: terrain height queries, building placement, camera ground level.
        /// </summary>
        public int GetHeightAt(int x, int z)
        {
            for (int y = HEIGHT - 1; y >= 0; y--)
            {
                if (GetBlock(x, y, z) != VoxelType.Air)
                    return y;
            }
            return -1;
        }

        /// <summary>
        /// Get the surface block type at a column (topmost non-air block).
        /// Returns Air if the column is empty.
        /// Used for: determining if a position is water, grass, etc.
        /// </summary>
        public VoxelType GetSurfaceType(int x, int z)
        {
            int height = GetHeightAt(x, z);
            return height >= 0 ? GetBlock(x, height, z) : VoxelType.Air;
        }

        private static bool IsInBounds(int x, int y, int z)
        {
            return x >= 0 && x < WIDTH
                && y >= 0 && y < HEIGHT
                && z >= 0 && z < DEPTH;
        }

        private static int ToIndex(int x, int y, int z)
        {
            return x + (z * WIDTH) + (y * WIDTH * DEPTH);
        }
    }
}
