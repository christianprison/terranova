using UnityEngine;
using Terranova.Terrain;

namespace Terranova.Resources
{
    /// <summary>
    /// Spawns placeholder resource objects (trees and rocks) on the terrain surface.
    ///
    /// Trees  = brown cylinders (blockout)
    /// Rocks  = gray spheres (blockout)
    ///
    /// All objects are positioned using GetSmoothedHeightAtWorldPos so they
    /// sit correctly on the smooth mesh surface.
    ///
    /// Story 0.6: Bestehende Objekte auf Mesh-Oberfläche
    /// Story 3.1: Sammelbare Objekte (placeholder visuals)
    /// </summary>
    public class ResourceSpawner : MonoBehaviour
    {
        [Header("Trees")]
        [SerializeField] private int _treeCount = 60;
        [SerializeField] private float _treeRadius = 0.3f;
        [SerializeField] private float _treeHeight = 2.0f;

        [Header("Rocks")]
        [SerializeField] private int _rockCount = 40;
        [SerializeField] private float _rockRadius = 0.4f;

        [Header("Placement")]
        [Tooltip("Minimum distance from world edge in blocks.")]
        [SerializeField] private int _edgeMargin = 4;
        [Tooltip("Random seed for deterministic placement.")]
        [SerializeField] private int _seed = 123;

        private bool _hasSpawned;

        private void Update()
        {
            if (_hasSpawned)
                return;

            var world = WorldManager.Instance;
            if (world == null || world.WorldBlocksX == 0)
                return;

            _hasSpawned = true;
            SpawnResources(world);
            enabled = false;
        }

        private void SpawnResources(WorldManager world)
        {
            var rng = new System.Random(_seed);
            var parent = new GameObject("Resources");

            int treeSpawned = SpawnObjects(world, rng, parent.transform,
                _treeCount, "Tree", PrimitiveType.Cylinder,
                new Color(0.45f, 0.28f, 0.10f), // Brown
                new Vector3(_treeRadius * 2f, _treeHeight * 0.5f, _treeRadius * 2f),
                _treeHeight * 0.5f);

            int rockSpawned = SpawnObjects(world, rng, parent.transform,
                _rockCount, "Rock", PrimitiveType.Sphere,
                new Color(0.55f, 0.55f, 0.55f), // Gray
                new Vector3(_rockRadius * 2f, _rockRadius * 2f, _rockRadius * 2f),
                _rockRadius);

            Debug.Log($"ResourceSpawner: Placed {treeSpawned} trees, {rockSpawned} rocks.");
        }

        private int SpawnObjects(
            WorldManager world, System.Random rng, Transform parent,
            int count, string namePrefix, PrimitiveType shape,
            Color color, Vector3 scale, float yOffset)
        {
            int maxX = world.WorldBlocksX - _edgeMargin;
            int maxZ = world.WorldBlocksZ - _edgeMargin;
            int spawned = 0;

            Shader shader = Shader.Find("Universal Render Pipeline/Lit")
                         ?? Shader.Find("Universal Render Pipeline/Particles/Unlit");
            Material mat = null;
            if (shader != null)
            {
                mat = new Material(shader);
                mat.name = $"{namePrefix}_Material (Auto)";
                mat.SetColor("_BaseColor", color);
            }

            for (int i = 0; i < count; i++)
            {
                // Random position within world bounds (with margin)
                float x = _edgeMargin + (float)(rng.NextDouble() * (maxX - _edgeMargin));
                float z = _edgeMargin + (float)(rng.NextDouble() * (maxZ - _edgeMargin));

                int blockX = Mathf.FloorToInt(x);
                int blockZ = Mathf.FloorToInt(z);

                // Only place on solid, non-water ground
                VoxelType surface = world.GetSurfaceTypeAtWorldPos(blockX, blockZ);
                if (!surface.IsSolid())
                    continue;

                // Position on smooth mesh surface
                float y = world.GetSmoothedHeightAtWorldPos(x, z);

                var obj = GameObject.CreatePrimitive(shape);
                obj.name = $"{namePrefix}_{spawned}";
                obj.transform.SetParent(parent);
                obj.transform.localScale = scale;
                // Offset Y so bottom sits on terrain surface
                obj.transform.position = new Vector3(x, y + yOffset, z);

                if (mat != null)
                    obj.GetComponent<MeshRenderer>().sharedMaterial = mat;

                // Remove collider from resource objects (not needed for QA testing,
                // prevents interference with terrain raycasting)
                var col = obj.GetComponent<Collider>();
                if (col != null) Object.Destroy(col);

                spawned++;
            }

            return spawned;
        }
    }
}
