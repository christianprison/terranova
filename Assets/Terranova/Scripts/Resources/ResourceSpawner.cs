using UnityEngine;
using Terranova.Core;
using Terranova.Terrain;

namespace Terranova.Resources
{
    /// <summary>
    /// Spawns gatherable resource objects on the terrain surface
    /// and attaches ResourceNode components for gathering.
    ///
    /// Epoch I.1: Settlers pick up raw materials from the ground.
    /// Twigs        = small brown sticks (blockout) → ResourceType.Wood
    /// Stones       = small gray pebbles (blockout) → ResourceType.Stone
    /// Berry Bushes = green sphere + red berry spheres → ResourceType.Food
    ///
    /// Story 3.1: Sammelbare Objekte
    /// Story 0.6: Bestehende Objekte auf Mesh-Oberfläche
    /// </summary>
    public class ResourceSpawner : MonoBehaviour
    {
        [Header("Twigs (Wood)")]
        [SerializeField] private int _twigCount = 60;
        [SerializeField] private float _twigLength = 0.5f;
        [SerializeField] private float _twigThickness = 0.06f;

        [Header("Stones")]
        [SerializeField] private int _stoneCount = 40;
        [SerializeField] private float _stoneRadius = 0.18f;

        [Header("Berry Bushes")]
        [SerializeField] private int _bushCount = 30;
        [SerializeField] private float _bushRadius = 0.5f;

        [Header("Placement")]
        [Tooltip("Minimum distance from world edge in blocks.")]
        [SerializeField] private int _edgeMargin = 4;
        [Tooltip("Random seed for deterministic placement.")]
        [SerializeField] private int _seed = 123;

        private bool _hasSpawned;

        // Shared materials (created once, reused)
        private static Material _bushMaterial;
        private static Material _berryMaterial;

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

            int twigSpawned = SpawnTwigs(world, rng, parent.transform);

            int stoneSpawned = SpawnObjects(world, rng, parent.transform,
                _stoneCount, "Stone", PrimitiveType.Sphere, ResourceType.Stone,
                new Color(0.55f, 0.55f, 0.55f), // Gray
                new Vector3(_stoneRadius * 2f, _stoneRadius * 1.4f, _stoneRadius * 2f),
                _stoneRadius * 0.7f);

            int bushSpawned = SpawnBerryBushes(world, rng, parent.transform);

            Debug.Log($"ResourceSpawner: Placed {twigSpawned} twigs, {stoneSpawned} stones, {bushSpawned} berry bushes.");
        }

        /// <summary>
        /// Spawn twigs: small brown sticks lying flat on the ground.
        /// Epoch I.1 settlers pick up sticks, they don't chop trees.
        /// </summary>
        private int SpawnTwigs(WorldManager world, System.Random rng, Transform parent)
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
                mat.name = "Twig_Material (Auto)";
                mat.SetColor("_BaseColor", new Color(0.45f, 0.28f, 0.10f)); // Brown
            }

            for (int i = 0; i < _twigCount; i++)
            {
                float x = _edgeMargin + (float)(rng.NextDouble() * (maxX - _edgeMargin));
                float z = _edgeMargin + (float)(rng.NextDouble() * (maxZ - _edgeMargin));

                int blockX = Mathf.FloorToInt(x);
                int blockZ = Mathf.FloorToInt(z);

                VoxelType surface = world.GetSurfaceTypeAtWorldPos(blockX, blockZ);
                if (!surface.IsSolid())
                    continue;

                world.FlattenTerrain(blockX, blockZ, 1);
                float y = world.GetSmoothedHeightAtWorldPos(x, z);

                // Cylinder lying on its side (rotated 90° around Z)
                var obj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                obj.name = $"Twig_{spawned}";
                obj.transform.SetParent(parent);
                obj.transform.localScale = new Vector3(
                    _twigThickness * 2f, _twigLength * 0.5f, _twigThickness * 2f);
                obj.transform.position = new Vector3(x, y + _twigThickness, z);

                // Lie flat with random rotation around Y
                float angle = (float)(rng.NextDouble() * 360.0);
                obj.transform.rotation = Quaternion.Euler(0f, angle, 90f);

                if (mat != null)
                    obj.GetComponent<MeshRenderer>().sharedMaterial = mat;

                var col = obj.GetComponent<Collider>();
                if (col != null)
                    col.isTrigger = true;

                var node = obj.AddComponent<ResourceNode>();
                node.Initialize(ResourceType.Wood);

                spawned++;
            }

            return spawned;
        }

        private int SpawnObjects(
            WorldManager world, System.Random rng, Transform parent,
            int count, string namePrefix, PrimitiveType shape, ResourceType resourceType,
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

                // Flatten terrain under the object so it stands on level ground
                world.FlattenTerrain(blockX, blockZ, 1);

                // Position on smooth mesh surface (re-query after flattening)
                float y = world.GetSmoothedHeightAtWorldPos(x, z);

                var obj = GameObject.CreatePrimitive(shape);
                obj.name = $"{namePrefix}_{spawned}";
                obj.transform.SetParent(parent);
                obj.transform.localScale = scale;
                // Offset Y so bottom sits on terrain surface
                obj.transform.position = new Vector3(x, y + yOffset, z);

                if (mat != null)
                    obj.GetComponent<MeshRenderer>().sharedMaterial = mat;

                // Keep collider for NavMesh obstacle avoidance but set to trigger
                // so it doesn't interfere with terrain raycasting
                var col = obj.GetComponent<Collider>();
                if (col != null)
                    col.isTrigger = true;

                // Attach ResourceNode component (Story 3.1)
                var node = obj.AddComponent<ResourceNode>();
                node.Initialize(resourceType);

                spawned++;
            }

            return spawned;
        }

        /// <summary>
        /// Spawn berry bushes: green flattened sphere (bush body) with
        /// small red spheres on top (berries). Compound visual.
        /// </summary>
        private int SpawnBerryBushes(WorldManager world, System.Random rng, Transform parent)
        {
            int maxX = world.WorldBlocksX - _edgeMargin;
            int maxZ = world.WorldBlocksZ - _edgeMargin;
            int spawned = 0;

            EnsureBushMaterials();

            for (int i = 0; i < _bushCount; i++)
            {
                float x = _edgeMargin + (float)(rng.NextDouble() * (maxX - _edgeMargin));
                float z = _edgeMargin + (float)(rng.NextDouble() * (maxZ - _edgeMargin));

                int blockX = Mathf.FloorToInt(x);
                int blockZ = Mathf.FloorToInt(z);

                VoxelType surface = world.GetSurfaceTypeAtWorldPos(blockX, blockZ);
                if (!surface.IsSolid())
                    continue;

                world.FlattenTerrain(blockX, blockZ, 1);
                float y = world.GetSmoothedHeightAtWorldPos(x, z);

                // Parent object with ResourceNode
                var bush = new GameObject($"Bush_{spawned}");
                bush.transform.SetParent(parent);
                bush.transform.position = new Vector3(x, y, z);

                // Green bush body (flattened sphere)
                var body = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                body.name = "Body";
                body.transform.SetParent(bush.transform, false);
                float r = _bushRadius;
                body.transform.localScale = new Vector3(r * 2f, r * 1.2f, r * 2f);
                body.transform.localPosition = new Vector3(0f, r * 0.6f, 0f);
                if (_bushMaterial != null)
                    body.GetComponent<MeshRenderer>().sharedMaterial = _bushMaterial;
                var bodyCol = body.GetComponent<Collider>();
                if (bodyCol != null) bodyCol.isTrigger = true;

                // Red berries (3 small spheres on top)
                float berrySize = 0.12f;
                float berryY = r * 1.0f;
                Vector3[] berryOffsets =
                {
                    new Vector3(0.15f, berryY, 0.1f),
                    new Vector3(-0.1f, berryY, 0.15f),
                    new Vector3(0.05f, berryY, -0.15f)
                };

                for (int b = 0; b < berryOffsets.Length; b++)
                {
                    var berry = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    berry.name = $"Berry_{b}";
                    berry.transform.SetParent(bush.transform, false);
                    berry.transform.localScale = new Vector3(berrySize, berrySize, berrySize);
                    berry.transform.localPosition = berryOffsets[b];
                    if (_berryMaterial != null)
                        berry.GetComponent<MeshRenderer>().sharedMaterial = _berryMaterial;
                    var berryCol = berry.GetComponent<Collider>();
                    if (berryCol != null) Object.Destroy(berryCol);
                }

                // Attach ResourceNode to the parent bush object
                var node = bush.AddComponent<ResourceNode>();
                node.Initialize(ResourceType.Food);

                spawned++;
            }

            return spawned;
        }

        private static void EnsureBushMaterials()
        {
            if (_bushMaterial != null) return;

            Shader shader = Shader.Find("Universal Render Pipeline/Lit")
                         ?? Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null) return;

            _bushMaterial = new Material(shader);
            _bushMaterial.name = "Bush_Material (Auto)";
            _bushMaterial.SetColor("_BaseColor", new Color(0.20f, 0.55f, 0.15f)); // Dark green

            _berryMaterial = new Material(shader);
            _berryMaterial.name = "Berry_Material (Auto)";
            _berryMaterial.SetColor("_BaseColor", new Color(0.85f, 0.15f, 0.15f)); // Red
        }
    }
}
