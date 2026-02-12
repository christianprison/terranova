using NUnit.Framework;
using Terranova.Terrain;

namespace Terranova.Tests.EditMode
{
    /// <summary>
    /// Tests for VoxelType extensions (transparency, solidity checks).
    /// </summary>
    public class VoxelTypeTests
    {
        [Test]
        public void Air_IsTransparent()
        {
            Assert.IsTrue(VoxelType.Air.IsTransparent());
        }

        [Test]
        public void Water_IsTransparent()
        {
            Assert.IsTrue(VoxelType.Water.IsTransparent());
        }

        [Test]
        public void SolidBlocks_AreNotTransparent()
        {
            Assert.IsFalse(VoxelType.Grass.IsTransparent());
            Assert.IsFalse(VoxelType.Dirt.IsTransparent());
            Assert.IsFalse(VoxelType.Stone.IsTransparent());
            Assert.IsFalse(VoxelType.Sand.IsTransparent());
        }

        [Test]
        public void Air_IsNotSolid()
        {
            Assert.IsFalse(VoxelType.Air.IsSolid());
        }

        [Test]
        public void Water_IsNotSolid()
        {
            Assert.IsFalse(VoxelType.Water.IsSolid());
        }

        [Test]
        public void SolidBlocks_AreSolid()
        {
            Assert.IsTrue(VoxelType.Grass.IsSolid());
            Assert.IsTrue(VoxelType.Dirt.IsSolid());
            Assert.IsTrue(VoxelType.Stone.IsSolid());
            Assert.IsTrue(VoxelType.Sand.IsSolid());
        }
    }
}
