using UnityEngine;
using Terranova.Core;
using Terranova.Terrain;

namespace Terranova.Resources
{
    /// <summary>
    /// Spawns resource objects (trees, rocks, berry bushes) on the terrain surface
    /// and attaches ResourceNode components for gathering.
    ///
    /// Trees       = low-poly pines (trunk + cone canopy) → ResourceType.Wood
    /// Rocks       = irregular angular shapes → ResourceType.Stone
    /// Berry Bushes = green sphere + red berry spheres → ResourceType.Food
    ///
    /// Story 3.1: Sammelbare Objekte
    /// Story 0.6: Bestehende Objekte auf Mesh-Oberfläche
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
            if (world == null || world.WorldBlocksX == 0 || !world.IsNavMeshReady)
                return;

            _hasSpawned = true;
            SpawnResources(world);
            enabled = false;
        }

        private void SpawnResources(WorldManager world)
        {
            var rng = new System.Random(_seed);
            var parent = new GameObject("Resources");

            EnsureTreeMeshes();
            EnsureRockMesh();

            int treeSpawned = SpawnTrees(world, rng, parent.transform);

            int rockSpawned = SpawnRocks(world, rng, parent.transform);

            int bushSpawned = SpawnBerryBushes(world, rng, parent.transform);

            Debug.Log($"ResourceSpawner: Placed {treeSpawned} trees, {rockSpawned} rocks, {bushSpawned} berry bushes.");
        }

        // ─── Shared mesh/material caches ─────────────────────────

        private static Mesh _treeCanopyMesh;  // Cone for pine canopy
        private static Mesh _rockMesh;        // Irregular angular rock
        private static Material _trunkMaterial;
        private static Material _canopyMaterial;
        private static Material _rockMaterial;

        /// <summary>
        /// Spawn trees as low-poly pines: brown cylinder trunk + green cone canopy.
        /// Slight size variation for a natural look.
        /// </summary>
        private int SpawnTrees(WorldManager world, System.Random rng, Transform parent)
        {
            int maxX = world.WorldBlocksX - _edgeMargin;
            int maxZ = world.WorldBlocksZ - _edgeMargin;
            int spawned = 0;

            for (int i = 0; i < _treeCount; i++)
            {
                float x = _edgeMargin + (float)(rng.NextDouble() * (maxX - _edgeMargin));
                float z = _edgeMargin + (float)(rng.NextDouble() * (maxZ - _edgeMargin));
                int blockX = Mathf.FloorToInt(x);
                int blockZ = Mathf.FloorToInt(z);

                VoxelType surface = world.GetSurfaceTypeAtWorldPos(blockX, blockZ);
                if (!surface.IsSolid()) continue;

                world.FlattenTerrain(blockX, blockZ, 1);
                float y = world.GetSmoothedHeightAtWorldPos(x, z);

                // Size variation: 0.8x to 1.2x
                float sizeScale = 0.8f + (float)rng.NextDouble() * 0.4f;
                float trunkH = _treeHeight * 0.35f * sizeScale;
                float canopyH = _treeHeight * 0.7f * sizeScale;
                float canopyR = _treeRadius * 2.5f * sizeScale;

                // Parent object
                var tree = new GameObject($"Tree_{spawned}");
                tree.transform.SetParent(parent);
                tree.transform.position = new Vector3(x, y, z);
                tree.transform.rotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);

                // Trunk: cylinder
                var trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                trunk.name = "Trunk";
                trunk.transform.SetParent(tree.transform, false);
                trunk.transform.localScale = new Vector3(_treeRadius * 0.6f, trunkH * 0.5f, _treeRadius * 0.6f);
                trunk.transform.localPosition = new Vector3(0f, trunkH * 0.5f, 0f);
                if (_trunkMaterial != null)
                    trunk.GetComponent<MeshRenderer>().sharedMaterial = _trunkMaterial;
                var trunkCol = trunk.GetComponent<Collider>();
                if (trunkCol != null) trunkCol.isTrigger = true;

                // Canopy: cone mesh
                var canopy = new GameObject("Canopy");
                canopy.transform.SetParent(tree.transform, false);
                canopy.transform.localPosition = new Vector3(0f, trunkH * 0.6f, 0f);
                canopy.transform.localScale = new Vector3(canopyR, canopyH, canopyR);
                var canopyMF = canopy.AddComponent<MeshFilter>();
                canopyMF.sharedMesh = _treeCanopyMesh;
                var canopyMR = canopy.AddComponent<MeshRenderer>();
                if (_canopyMaterial != null)
                    canopyMR.sharedMaterial = _canopyMaterial;

                // ResourceNode on parent
                var node = tree.AddComponent<ResourceNode>();
                node.Initialize(ResourceType.Wood);

                spawned++;
            }
            return spawned;
        }

        /// <summary>
        /// Spawn rocks as irregular angular shapes with grey tone variation.
        /// </summary>
        private int SpawnRocks(WorldManager world, System.Random rng, Transform parent)
        {
            int maxX = world.WorldBlocksX - _edgeMargin;
            int maxZ = world.WorldBlocksZ - _edgeMargin;
            int spawned = 0;

            for (int i = 0; i < _rockCount; i++)
            {
                float x = _edgeMargin + (float)(rng.NextDouble() * (maxX - _edgeMargin));
                float z = _edgeMargin + (float)(rng.NextDouble() * (maxZ - _edgeMargin));
                int blockX = Mathf.FloorToInt(x);
                int blockZ = Mathf.FloorToInt(z);

                VoxelType surface = world.GetSurfaceTypeAtWorldPos(blockX, blockZ);
                if (!surface.IsSolid()) continue;

                world.FlattenTerrain(blockX, blockZ, 1);
                float y = world.GetSmoothedHeightAtWorldPos(x, z);

                // Size variation: 0.7x to 1.3x
                float sizeScale = 0.7f + (float)rng.NextDouble() * 0.6f;
                float rx = _rockRadius * sizeScale * (0.8f + (float)rng.NextDouble() * 0.4f);
                float ry = _rockRadius * sizeScale * (0.5f + (float)rng.NextDouble() * 0.5f);
                float rz = _rockRadius * sizeScale * (0.8f + (float)rng.NextDouble() * 0.4f);

                var rock = new GameObject($"Rock_{spawned}");
                rock.transform.SetParent(parent);
                rock.transform.position = new Vector3(x, y, z);
                rock.transform.rotation = Quaternion.Euler(
                    (float)rng.NextDouble() * 15f,
                    (float)rng.NextDouble() * 360f,
                    (float)rng.NextDouble() * 15f);

                var meshObj = new GameObject("Mesh");
                meshObj.transform.SetParent(rock.transform, false);
                meshObj.transform.localScale = new Vector3(rx * 2f, ry * 2f, rz * 2f);
                meshObj.transform.localPosition = new Vector3(0f, ry * 0.7f, 0f);

                var mf = meshObj.AddComponent<MeshFilter>();
                mf.sharedMesh = _rockMesh;
                var mr = meshObj.AddComponent<MeshRenderer>();

                // Grey tone variation
                float grey = 0.45f + (float)rng.NextDouble() * 0.2f;
                if (_rockMaterial != null)
                {
                    mr.sharedMaterial = _rockMaterial;
                    var pb = new MaterialPropertyBlock();
                    pb.SetColor(Shader.PropertyToID("_BaseColor"),
                        new Color(grey, grey * 0.95f, grey * 0.9f));
                    mr.SetPropertyBlock(pb);
                }

                // Add a box collider for raycasting (trigger so no physics)
                var col = meshObj.AddComponent<BoxCollider>();
                col.isTrigger = true;

                var node = rock.AddComponent<ResourceNode>();
                node.Initialize(ResourceType.Stone);

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
            _bushMaterial.SetColor("_BaseColor", new Color(0.18f, 0.50f, 0.12f));

            _berryMaterial = new Material(shader);
            _berryMaterial.name = "Berry_Material (Auto)";
            _berryMaterial.SetColor("_BaseColor", new Color(0.80f, 0.10f, 0.15f));
        }

        /// <summary>Create shared cone mesh for tree canopies and trunk/canopy materials.</summary>
        private static void EnsureTreeMeshes()
        {
            if (_treeCanopyMesh != null) return;

            _treeCanopyMesh = CreateConeMesh(0.5f, 1f, 8); // Low-poly pine shape

            Shader shader = Shader.Find("Universal Render Pipeline/Lit")
                         ?? Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null) return;

            _trunkMaterial = new Material(shader);
            _trunkMaterial.name = "TreeTrunk_Material (Auto)";
            _trunkMaterial.SetColor("_BaseColor", new Color(0.40f, 0.26f, 0.12f));

            _canopyMaterial = new Material(shader);
            _canopyMaterial.name = "TreeCanopy_Material (Auto)";
            _canopyMaterial.SetColor("_BaseColor", new Color(0.15f, 0.45f, 0.12f));
        }

        /// <summary>Create a shared irregular rock mesh.</summary>
        private static void EnsureRockMesh()
        {
            if (_rockMesh != null) return;
            _rockMesh = CreateRockMesh();

            Shader shader = Shader.Find("Universal Render Pipeline/Lit")
                         ?? Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null) return;

            _rockMaterial = new Material(shader);
            _rockMaterial.name = "Rock_Material (Auto)";
            _rockMaterial.SetColor("_BaseColor", new Color(0.55f, 0.53f, 0.50f));
        }

        /// <summary>Create a simple cone mesh (base at y=0, apex at y=height).</summary>
        private static Mesh CreateConeMesh(float radius, float height, int segments)
        {
            var mesh = new Mesh { name = "Cone" };
            int vertCount = segments + 2;
            var verts = new Vector3[vertCount];
            var normals = new Vector3[vertCount];

            verts[0] = Vector3.zero;
            normals[0] = Vector3.down;

            for (int i = 0; i < segments; i++)
            {
                float angle = i * Mathf.PI * 2f / segments;
                verts[i + 1] = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                Vector3 outward = new Vector3(verts[i + 1].x, 0f, verts[i + 1].z).normalized;
                normals[i + 1] = Vector3.Lerp(outward, Vector3.up, 0.5f).normalized;
            }

            verts[segments + 1] = new Vector3(0f, height, 0f);
            normals[segments + 1] = Vector3.up;

            var tris = new int[segments * 6];
            for (int i = 0; i < segments; i++)
            {
                int next = (i + 1) % segments;
                tris[i * 6 + 0] = 0;
                tris[i * 6 + 1] = next + 1;
                tris[i * 6 + 2] = i + 1;
                tris[i * 6 + 3] = i + 1;
                tris[i * 6 + 4] = next + 1;
                tris[i * 6 + 5] = segments + 1;
            }

            mesh.vertices = verts;
            mesh.normals = normals;
            mesh.triangles = tris;
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// Create an irregular rock mesh by deforming a sphere.
        /// Low-poly angular look with 3 subdivisions.
        /// </summary>
        private static Mesh CreateRockMesh()
        {
            var mesh = new Mesh { name = "Rock" };

            int latSegments = 4;
            int lonSegments = 6;
            var rng = new System.Random(42); // Deterministic deformation

            var verts = new System.Collections.Generic.List<Vector3>();
            var tris = new System.Collections.Generic.List<int>();

            for (int lat = 0; lat <= latSegments; lat++)
            {
                float theta = lat * Mathf.PI / latSegments;
                for (int lon = 0; lon <= lonSegments; lon++)
                {
                    float phi = lon * 2f * Mathf.PI / lonSegments;
                    float deform = 0.7f + (float)rng.NextDouble() * 0.6f;
                    float x = Mathf.Sin(theta) * Mathf.Cos(phi) * 0.5f * deform;
                    float y = Mathf.Cos(theta) * 0.4f * deform + 0.4f;
                    float z = Mathf.Sin(theta) * Mathf.Sin(phi) * 0.5f * deform;
                    verts.Add(new Vector3(x, Mathf.Max(0f, y), z));
                }
            }

            for (int lat = 0; lat < latSegments; lat++)
            {
                for (int lon = 0; lon < lonSegments; lon++)
                {
                    int current = lat * (lonSegments + 1) + lon;
                    int next = current + lonSegments + 1;
                    tris.Add(current);
                    tris.Add(next);
                    tris.Add(current + 1);
                    tris.Add(current + 1);
                    tris.Add(next);
                    tris.Add(next + 1);
                }
            }

            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
