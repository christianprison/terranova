namespace Terranova.Terrain
{
    /// <summary>
    /// All block types in the voxel world.
    /// Uses byte backing for memory efficiency – each block is just 1 byte,
    /// so a full chunk (16×16×256 = 65,536 blocks) uses only 64 KB.
    /// </summary>
    public enum VoxelType : byte
    {
        Air = 0,        // Empty space, not rendered
        Grass = 1,      // Surface layer in temperate biomes
        Dirt = 2,       // Below grass, above stone
        Stone = 3,      // Deep underground, also exposed in mountains
        Sand = 4,       // Beaches, desert biomes, near water
        Water = 5       // Fills areas below sea level
    }

    /// <summary>
    /// Extension methods for VoxelType to keep block-type logic centralized.
    /// </summary>
    public static class VoxelTypeExtensions
    {
        /// <summary>
        /// Returns true if this block is transparent (light/visibility passes through).
        /// The mesh builder uses this to decide which faces to render:
        /// a face is only visible if it borders a transparent block.
        /// </summary>
        public static bool IsTransparent(this VoxelType type)
        {
            return type == VoxelType.Air || type == VoxelType.Water;
        }

        /// <summary>
        /// Returns true if this block is solid (can support buildings, settlers walk on it).
        /// Water and air are not solid.
        /// </summary>
        public static bool IsSolid(this VoxelType type)
        {
            return type != VoxelType.Air && type != VoxelType.Water;
        }
    }
}
