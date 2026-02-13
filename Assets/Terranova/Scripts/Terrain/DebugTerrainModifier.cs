using UnityEngine;

namespace Terranova.Terrain
{
    /// <summary>
    /// DEBUG ONLY – Click on terrain to modify blocks.
    ///
    /// Left click  = remove block (set to Air)
    /// Right click = raise terrain (add Stone block on top)
    ///
    /// Demonstrates that WorldManager.ModifyBlock() correctly rebuilds
    /// the smooth mesh for the affected chunk and its neighbors.
    ///
    /// Story 0.5: Terrain-Modifikation aktualisiert Mesh
    /// Remove this when a proper terrain editing tool exists.
    /// </summary>
    public class DebugTerrainModifier : MonoBehaviour
    {
        private void Update()
        {
            if (Input.GetMouseButtonDown(0))
                TryModify(remove: true);
            else if (Input.GetMouseButtonDown(1))
                TryModify(remove: false);
        }

        private void TryModify(bool remove)
        {
            var world = WorldManager.Instance;
            if (world == null)
                return;

            var cam = Camera.main;
            if (cam == null)
                return;

            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            if (!Physics.Raycast(ray, out RaycastHit hit, 500f))
                return;

            int blockX = Mathf.FloorToInt(hit.point.x);
            int blockZ = Mathf.FloorToInt(hit.point.z);
            int height = world.GetHeightAtWorldPos(blockX, blockZ);

            if (height < 0)
                return;

            if (remove)
            {
                // Remove the surface block
                world.ModifyBlock(blockX, height, blockZ, VoxelType.Air);
                Debug.Log($"DebugTerrainModifier: Removed block at ({blockX}, {height}, {blockZ})");
            }
            else
            {
                // Add a stone block on top of the current surface
                int newY = height + 1;
                if (newY < ChunkData.HEIGHT)
                {
                    world.ModifyBlock(blockX, newY, blockZ, VoxelType.Stone);
                    Debug.Log($"DebugTerrainModifier: Added Stone at ({blockX}, {newY}, {blockZ})");
                }
            }
        }
    }
}
