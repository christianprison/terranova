using System.Collections.Generic;
using UnityEngine;
using Terranova.Core;
using Terranova.Terrain;

namespace Terranova.Resources
{
    /// <summary>
    /// Spawns material-based resource nodes as visible props based on biome.
    ///
    /// Uses GameState.SelectedBiome and GameState.Seed for deterministic,
    /// biome-specific placement of 150-250 resource nodes across the map.
    ///
    /// Biome-specific spawning:
    ///   Forest    - Dense deadwood, berry bushes (some poisonous!), mushrooms, resin, insects
    ///   Mountains - Rock formations (flint, granite, limestone), sparse trees, cave markers
    ///   Coast     - River stones at water edge, reeds/grasses, clay, sand, driftwood, fish spots
    ///
    /// Guaranteed start conditions (within range of world center):
    ///   - Water within 30 blocks (terrain handles via sea level)
    ///   - Food sources (berries in forest, roots near water, insects everywhere)
    ///   - Stone source (type varies by biome)
    ///   - Shelter marker within 40 blocks (cave/overhang/undergrowth)
    ///
    /// Each node uses ResourceNode with a MaterialId from MaterialDatabase.
    /// Visual distinction via primitive shapes and colors per material type.
    /// </summary>
    public class ResourceSpawner : MonoBehaviour
    {
        [Header("Placement")]
        [Tooltip("Minimum distance from world edge in blocks.")]
        [SerializeField] private int _edgeMargin = 4;

        [Header("Node Counts")]
        [Tooltip("Minimum total resource nodes to spawn.")]
        [SerializeField] private int _minNodes = 150;
        [Tooltip("Maximum total resource nodes to spawn.")]
        [SerializeField] private int _maxNodes = 250;

        [Header("Start Conditions")]
        [Tooltip("Max distance from center for guaranteed start resources.")]
        [SerializeField] private float _startRadius = 30f;
        [Tooltip("Max distance from center for shelter marker.")]
        [SerializeField] private float _shelterRadius = 40f;

        private bool _hasSpawned;

        // ─── Cached materials (created once, reused) ───────────────

        private static Material _matWood;
        private static Material _matBerry;
        private static Material _matBerryPoison;
        private static Material _matStone;
        private static Material _matFlint;
        private static Material _matReeds;
        private static Material _matResin;
        private static Material _matClay;
        private static Material _matFish;
        private static Material _matShelter;
        private static Material _matInsects;
        private static Material _matRoots;
        private static Material _matHoney;
        private static Material _matDriftwood;
        private static Material _matGranite;
        private static Material _matLimestone;

        /// <summary>Reset static state when domain reload is disabled.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _matWood = null;
            _matBerry = null;
            _matBerryPoison = null;
            _matStone = null;
            _matFlint = null;
            _matReeds = null;
            _matResin = null;
            _matClay = null;
            _matFish = null;
            _matShelter = null;
            _matInsects = null;
            _matRoots = null;
            _matHoney = null;
            _matDriftwood = null;
            _matGranite = null;
            _matLimestone = null;
        }

        // ─── Biome spawn tables ─────────────────────────────────────

        /// <summary>
        /// Defines a material to spawn, its relative weight, and placement rules.
        /// </summary>
        private struct SpawnEntry
        {
            public string MaterialId;
            public float Weight;
            public bool NearWater;  // Must spawn near water/sand edge
            public bool InCenter;   // Prefer spawning near world center (start area)
        }

        /// <summary>Respawn speed multiplier per biome (higher = slower).</summary>
        private static float GetBiomeRespawnMultiplier(BiomeType biome)
        {
            return biome switch
            {
                BiomeType.Forest    => 0.8f,  // Forest regrows fast
                BiomeType.Mountains => 1.5f,  // Mountains are harsh, slow regrowth
                BiomeType.Coast     => 1.0f,  // Moderate
                _                   => 1.0f
            };
        }

        private static List<SpawnEntry> GetSpawnTable(BiomeType biome)
        {
            return biome switch
            {
                BiomeType.Forest => new List<SpawnEntry>
                {
                    new SpawnEntry { MaterialId = "deadwood",       Weight = 25f },
                    new SpawnEntry { MaterialId = "berries_safe",   Weight = 12f },
                    new SpawnEntry { MaterialId = "berries_poison", Weight = 5f },
                    new SpawnEntry { MaterialId = "resin",          Weight = 10f },
                    new SpawnEntry { MaterialId = "insects",        Weight = 15f },
                    new SpawnEntry { MaterialId = "honey",          Weight = 4f },
                    new SpawnEntry { MaterialId = "plant_fibers",   Weight = 10f },
                    new SpawnEntry { MaterialId = "river_stone",    Weight = 6f,  NearWater = true },
                    new SpawnEntry { MaterialId = "roots",          Weight = 5f,  NearWater = true },
                    new SpawnEntry { MaterialId = "grasses_reeds",  Weight = 5f,  NearWater = true },
                    new SpawnEntry { MaterialId = "flint",          Weight = 3f },
                },

                BiomeType.Mountains => new List<SpawnEntry>
                {
                    new SpawnEntry { MaterialId = "flint",          Weight = 20f },
                    new SpawnEntry { MaterialId = "granite",        Weight = 15f },
                    new SpawnEntry { MaterialId = "limestone",      Weight = 12f },
                    new SpawnEntry { MaterialId = "river_stone",    Weight = 8f },
                    new SpawnEntry { MaterialId = "deadwood",       Weight = 8f },
                    new SpawnEntry { MaterialId = "insects",        Weight = 10f },
                    new SpawnEntry { MaterialId = "plant_fibers",   Weight = 6f },
                    new SpawnEntry { MaterialId = "roots",          Weight = 5f,  NearWater = true },
                    new SpawnEntry { MaterialId = "berries_safe",   Weight = 4f },
                    new SpawnEntry { MaterialId = "berries_poison", Weight = 2f },
                    new SpawnEntry { MaterialId = "resin",          Weight = 4f },
                    new SpawnEntry { MaterialId = "honey",          Weight = 2f },
                    new SpawnEntry { MaterialId = "grasses_reeds",  Weight = 4f,  NearWater = true },
                },

                BiomeType.Coast => new List<SpawnEntry>
                {
                    new SpawnEntry { MaterialId = "river_stone",    Weight = 15f, NearWater = true },
                    new SpawnEntry { MaterialId = "grasses_reeds",  Weight = 15f, NearWater = true },
                    new SpawnEntry { MaterialId = "clay",           Weight = 10f, NearWater = true },
                    new SpawnEntry { MaterialId = "deadwood",       Weight = 10f, NearWater = true },  // driftwood
                    new SpawnEntry { MaterialId = "fish",           Weight = 8f,  NearWater = true },
                    new SpawnEntry { MaterialId = "roots",          Weight = 8f,  NearWater = true },
                    new SpawnEntry { MaterialId = "insects",        Weight = 8f },
                    new SpawnEntry { MaterialId = "plant_fibers",   Weight = 8f },
                    new SpawnEntry { MaterialId = "berries_safe",   Weight = 5f },
                    new SpawnEntry { MaterialId = "berries_poison", Weight = 2f },
                    new SpawnEntry { MaterialId = "sandstone",      Weight = 5f },
                    new SpawnEntry { MaterialId = "flint",          Weight = 3f,  NearWater = true },
                    new SpawnEntry { MaterialId = "honey",          Weight = 3f },
                },

                _ => new List<SpawnEntry>
                {
                    new SpawnEntry { MaterialId = "deadwood",     Weight = 30f },
                    new SpawnEntry { MaterialId = "river_stone",  Weight = 20f },
                    new SpawnEntry { MaterialId = "berries_safe", Weight = 15f },
                    new SpawnEntry { MaterialId = "insects",      Weight = 15f },
                    new SpawnEntry { MaterialId = "plant_fibers", Weight = 10f },
                    new SpawnEntry { MaterialId = "flint",        Weight = 10f },
                }
            };
        }

        // ─── Main spawn logic ───────────────────────────────────────

        private void Update()
        {
            if (_hasSpawned) return;

            var world = WorldManager.Instance;
            if (world == null || world.WorldBlocksX == 0 || !world.IsNavMeshReady)
                return;

            _hasSpawned = true;
            SpawnResources(world);
            enabled = false;
        }

        private void SpawnResources(WorldManager world)
        {
            var biome = GameState.SelectedBiome;
            var rng = new System.Random(GameState.Seed);
            var parent = new GameObject("Resources");

            EnsureMaterials();

            int targetCount = _minNodes + rng.Next(_maxNodes - _minNodes + 1);
            var spawnTable = GetSpawnTable(biome);
            float respawnMult = GetBiomeRespawnMultiplier(biome);

            float centerX = world.WorldBlocksX * 0.5f;
            float centerZ = world.WorldBlocksZ * 0.5f;

            // Phase 1: Guarantee start conditions near world center
            int startSpawned = 0;
            startSpawned += SpawnGuaranteedStartResources(world, rng, parent.transform, biome, centerX, centerZ, respawnMult);

            // Phase 2: Spawn remaining nodes across the map using weighted table
            int distributed = startSpawned;
            int remaining = targetCount - distributed;
            int attempts = 0;
            int maxAttempts = remaining * 5;

            // Precompute total weight for weighted random selection
            float totalWeight = 0f;
            foreach (var entry in spawnTable)
                totalWeight += entry.Weight;

            while (distributed < targetCount && attempts < maxAttempts)
            {
                attempts++;

                // Weighted random material selection
                float roll = (float)rng.NextDouble() * totalWeight;
                SpawnEntry selected = spawnTable[0];
                float cumulative = 0f;
                for (int i = 0; i < spawnTable.Count; i++)
                {
                    cumulative += spawnTable[i].Weight;
                    if (roll <= cumulative)
                    {
                        selected = spawnTable[i];
                        break;
                    }
                }

                // Pick random position
                float x = _edgeMargin + (float)(rng.NextDouble() * (world.WorldBlocksX - _edgeMargin * 2));
                float z = _edgeMargin + (float)(rng.NextDouble() * (world.WorldBlocksZ - _edgeMargin * 2));
                int blockX = Mathf.FloorToInt(x);
                int blockZ = Mathf.FloorToInt(z);

                VoxelType surface = world.GetSurfaceTypeAtWorldPos(blockX, blockZ);

                // Near-water check: must be on sand or adjacent to water
                if (selected.NearWater)
                {
                    if (!IsNearWater(world, blockX, blockZ))
                        continue;
                }

                // Must be on solid ground (or sand for near-water nodes)
                if (!surface.IsSolid()) continue;

                world.FlattenTerrain(blockX, blockZ, 1);
                float y = world.GetSmoothedHeightAtWorldPos(x, z);
                Vector3 pos = new Vector3(x, y, z);

                var go = CreateResourceProp(selected.MaterialId, pos, rng, parent.transform, biome);
                if (go == null) continue;

                var node = go.AddComponent<ResourceNode>();
                node.Initialize(selected.MaterialId);
                node.RespawnMultiplier = respawnMult;

                distributed++;
            }

            Debug.Log($"[ResourceSpawner] Biome={biome}, Seed={GameState.Seed}: " +
                      $"Placed {distributed} resource nodes ({startSpawned} guaranteed start).");
        }

        // ─── Guaranteed start resources ─────────────────────────────

        /// <summary>
        /// Spawn guaranteed resources near world center so the player always has
        /// food, stone, and shelter accessible at the start.
        /// </summary>
        private int SpawnGuaranteedStartResources(
            WorldManager world, System.Random rng, Transform parent,
            BiomeType biome, float centerX, float centerZ, float respawnMult)
        {
            int spawned = 0;

            // Food source near center
            string foodMat = biome switch
            {
                BiomeType.Forest    => "berries_safe",
                BiomeType.Mountains => "insects",
                BiomeType.Coast     => "roots",
                _                   => "berries_safe"
            };
            spawned += SpawnGuaranteedNode(world, rng, parent, foodMat, centerX, centerZ, _startRadius, respawnMult, false);
            spawned += SpawnGuaranteedNode(world, rng, parent, foodMat, centerX, centerZ, _startRadius, respawnMult, false);
            // Extra insects everywhere as fallback food
            spawned += SpawnGuaranteedNode(world, rng, parent, "insects", centerX, centerZ, _startRadius, respawnMult, false);

            // Stone source near center (type varies by biome)
            string stoneMat = biome switch
            {
                BiomeType.Forest    => "river_stone",
                BiomeType.Mountains => "flint",
                BiomeType.Coast     => "river_stone",
                _                   => "river_stone"
            };
            spawned += SpawnGuaranteedNode(world, rng, parent, stoneMat, centerX, centerZ, _startRadius, respawnMult, false);

            // Wood near center
            spawned += SpawnGuaranteedNode(world, rng, parent, "deadwood", centerX, centerZ, _startRadius, respawnMult, false);
            spawned += SpawnGuaranteedNode(world, rng, parent, "deadwood", centerX, centerZ, _startRadius, respawnMult, false);

            // Plant fibers near center
            spawned += SpawnGuaranteedNode(world, rng, parent, "plant_fibers", centerX, centerZ, _startRadius, respawnMult, false);

            // Shelter marker near center (cave/overhang/undergrowth)
            spawned += SpawnGuaranteedNode(world, rng, parent, "shelter_marker", centerX, centerZ, _shelterRadius, respawnMult, false);

            return spawned;
        }

        /// <summary>
        /// Attempt to place a single guaranteed node near a center point.
        /// Tries up to 30 times to find valid placement within radius.
        /// Returns 1 on success, 0 on failure.
        /// </summary>
        private int SpawnGuaranteedNode(
            WorldManager world, System.Random rng, Transform parent,
            string materialId, float centerX, float centerZ, float radius,
            float respawnMult, bool requireWater)
        {
            for (int attempt = 0; attempt < 30; attempt++)
            {
                float angle = (float)(rng.NextDouble() * Mathf.PI * 2f);
                float dist = (float)(rng.NextDouble() * radius);
                float x = centerX + Mathf.Cos(angle) * dist;
                float z = centerZ + Mathf.Sin(angle) * dist;

                int blockX = Mathf.FloorToInt(x);
                int blockZ = Mathf.FloorToInt(z);

                if (blockX < _edgeMargin || blockX >= world.WorldBlocksX - _edgeMargin) continue;
                if (blockZ < _edgeMargin || blockZ >= world.WorldBlocksZ - _edgeMargin) continue;

                VoxelType surface = world.GetSurfaceTypeAtWorldPos(blockX, blockZ);
                if (!surface.IsSolid()) continue;
                if (requireWater && !IsNearWater(world, blockX, blockZ)) continue;

                world.FlattenTerrain(blockX, blockZ, 1);
                float y = world.GetSmoothedHeightAtWorldPos(x, z);
                Vector3 pos = new Vector3(x, y, z);

                var go = CreateResourceProp(materialId, pos, rng, parent, GameState.SelectedBiome);
                if (go == null) continue;

                var node = go.AddComponent<ResourceNode>();
                // Shelter markers use deadwood MaterialId (large structure, not a real material)
                string nodeMatId = materialId == "shelter_marker" ? "deadwood" : materialId;
                node.Initialize(nodeMatId);
                node.RespawnMultiplier = respawnMult;
                return 1;
            }

            Debug.LogWarning($"[ResourceSpawner] Failed to place guaranteed {materialId} near center.");
            return 0;
        }

        // ─── Water proximity check ──────────────────────────────────

        /// <summary>
        /// Check if a position is near water (within 5 blocks of a water or sand block).
        /// </summary>
        private bool IsNearWater(WorldManager world, int blockX, int blockZ)
        {
            const int searchRadius = 5;
            for (int dx = -searchRadius; dx <= searchRadius; dx++)
            {
                for (int dz = -searchRadius; dz <= searchRadius; dz++)
                {
                    int nx = blockX + dx;
                    int nz = blockZ + dz;
                    if (nx < 0 || nx >= world.WorldBlocksX || nz < 0 || nz >= world.WorldBlocksZ)
                        continue;

                    VoxelType s = world.GetSurfaceTypeAtWorldPos(nx, nz);
                    if (s == VoxelType.Water || s == VoxelType.Sand)
                        return true;
                }
            }
            return false;
        }

        // ─── Visual prop creation ───────────────────────────────────

        /// <summary>
        /// Create a visible prop GameObject for the given material at the given position.
        /// Uses primitive shapes with distinct colors per material type.
        /// </summary>
        private GameObject CreateResourceProp(
            string materialId, Vector3 position, System.Random rng, Transform parent, BiomeType biome)
        {
            float sizeVariation = 0.8f + (float)rng.NextDouble() * 0.4f;
            float yRotation = (float)rng.NextDouble() * 360f;

            switch (materialId)
            {
                case "deadwood":
                    return CreateWoodProp(position, sizeVariation, yRotation, parent, biome);

                case "berries_safe":
                    return CreateBerryProp(position, sizeVariation, yRotation, parent, false);

                case "berries_poison":
                    return CreateBerryProp(position, sizeVariation, yRotation, parent, true);

                case "river_stone":
                case "sandstone":
                    return CreateStoneProp(position, sizeVariation, yRotation, parent, _matStone, "Stone");

                case "flint":
                    return CreateStoneProp(position, sizeVariation, yRotation, parent, _matFlint, "Flint");

                case "granite":
                    return CreateStoneProp(position, sizeVariation, yRotation, parent, _matGranite, "Granite");

                case "limestone":
                    return CreateStoneProp(position, sizeVariation, yRotation, parent, _matLimestone, "Limestone");

                case "grasses_reeds":
                    return CreateReedsProp(position, sizeVariation, yRotation, parent);

                case "resin":
                    return CreateResinProp(position, sizeVariation, parent);

                case "clay":
                    return CreateClayProp(position, sizeVariation, yRotation, parent);

                case "fish":
                    return CreateFishProp(position, sizeVariation, parent);

                case "shelter_marker":
                    return CreateShelterProp(position, sizeVariation, yRotation, parent);

                case "insects":
                    return CreateInsectsProp(position, sizeVariation, parent);

                case "roots":
                    return CreateRootsProp(position, sizeVariation, yRotation, parent);

                case "honey":
                    return CreateHoneyProp(position, sizeVariation, parent);

                case "plant_fibers":
                    return CreateReedsProp(position, sizeVariation * 0.7f, yRotation, parent);

                default:
                    // Fallback: small grey cube
                    return CreateGenericProp(position, sizeVariation, yRotation, parent, materialId);
            }
        }

        // ─── Individual prop builders ───────────────────────────────

        /// <summary>Wood/Deadwood: brown cube.</summary>
        private GameObject CreateWoodProp(Vector3 pos, float scale, float yRot, Transform parent, BiomeType biome)
        {
            // In coast biome, deadwood is driftwood (slightly different color)
            Material mat = biome == BiomeType.Coast ? _matDriftwood : _matWood;
            string label = biome == BiomeType.Coast ? "Driftwood" : "Deadwood";

            var go = new GameObject(label);
            go.transform.SetParent(parent);
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(0f, yRot, 0f);

            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "Mesh";
            cube.transform.SetParent(go.transform, false);
            // Elongated log shape
            float length = 0.6f * scale;
            float height = 0.2f * scale;
            float width = 0.2f * scale;
            cube.transform.localScale = new Vector3(length, height, width);
            cube.transform.localPosition = new Vector3(0f, height * 0.5f, 0f);
            // Slight tilt for natural look
            cube.transform.localRotation = Quaternion.Euler(0f, 0f, (float)(new System.Random((int)(pos.x * 100)).NextDouble()) * 10f - 5f);

            if (mat != null)
                cube.GetComponent<MeshRenderer>().sharedMaterial = mat;

            var col = cube.GetComponent<Collider>();
            if (col != null) col.isTrigger = true;

            return go;
        }

        /// <summary>Berry bush: small green sphere with red/dark top spheres.</summary>
        private GameObject CreateBerryProp(Vector3 pos, float scale, float yRot, Transform parent, bool poisonous)
        {
            var go = new GameObject(poisonous ? "PoisonBerry" : "BerryBush");
            go.transform.SetParent(parent);
            go.transform.position = pos;

            // Bush body (flattened sphere)
            var body = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            body.name = "Body";
            body.transform.SetParent(go.transform, false);
            float r = 0.35f * scale;
            body.transform.localScale = new Vector3(r * 2f, r * 1.2f, r * 2f);
            body.transform.localPosition = new Vector3(0f, r * 0.6f, 0f);

            // Green bush material for both types (bush body is always green)
            Shader shader = FindShader();
            if (shader != null)
            {
                var bushMat = new Material(shader);
                bushMat.SetColor("_BaseColor", new Color(0.18f, 0.50f, 0.12f));
                body.GetComponent<MeshRenderer>().sharedMaterial = bushMat;
            }
            var bodyCol = body.GetComponent<Collider>();
            if (bodyCol != null) bodyCol.isTrigger = true;

            // Berry spheres on top
            Material berryMat = poisonous ? _matBerryPoison : _matBerry;
            float berrySize = 0.10f * scale;
            float berryY = r * 1.0f;
            Vector3[] berryOffsets =
            {
                new Vector3(0.12f, berryY, 0.08f),
                new Vector3(-0.08f, berryY, 0.12f),
                new Vector3(0.04f, berryY, -0.12f)
            };

            for (int b = 0; b < berryOffsets.Length; b++)
            {
                var berry = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                berry.name = $"Berry_{b}";
                berry.transform.SetParent(go.transform, false);
                berry.transform.localScale = new Vector3(berrySize, berrySize, berrySize);
                berry.transform.localPosition = berryOffsets[b];
                if (berryMat != null)
                    berry.GetComponent<MeshRenderer>().sharedMaterial = berryMat;
                var berryCol = berry.GetComponent<Collider>();
                if (berryCol != null) Object.Destroy(berryCol);
            }

            return go;
        }

        /// <summary>Stone/Flint/Granite/Limestone: gray cube with color variation.</summary>
        private GameObject CreateStoneProp(Vector3 pos, float scale, float yRot, Transform parent, Material mat, string label)
        {
            var go = new GameObject(label);
            go.transform.SetParent(parent);
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(
                (float)(new System.Random((int)(pos.x * 73)).NextDouble()) * 15f,
                yRot,
                (float)(new System.Random((int)(pos.z * 97)).NextDouble()) * 15f);

            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "Mesh";
            cube.transform.SetParent(go.transform, false);
            float sz = 0.35f * scale;
            // Slightly irregular dimensions
            cube.transform.localScale = new Vector3(
                sz * (0.8f + (float)(new System.Random((int)(pos.x * 37)).NextDouble()) * 0.4f),
                sz * (0.6f + (float)(new System.Random((int)(pos.z * 41)).NextDouble()) * 0.4f),
                sz * (0.8f + (float)(new System.Random((int)(pos.x * 53 + pos.z)).NextDouble()) * 0.4f));
            cube.transform.localPosition = new Vector3(0f, sz * 0.3f, 0f);

            if (mat != null)
                cube.GetComponent<MeshRenderer>().sharedMaterial = mat;

            var col = cube.GetComponent<Collider>();
            if (col != null) col.isTrigger = true;

            return go;
        }

        /// <summary>Reeds: thin green cylinder.</summary>
        private GameObject CreateReedsProp(Vector3 pos, float scale, float yRot, Transform parent)
        {
            var go = new GameObject("Reeds");
            go.transform.SetParent(parent);
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(0f, yRot, 0f);

            // Cluster of 3 thin cylinders
            for (int i = 0; i < 3; i++)
            {
                var cyl = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                cyl.name = $"Reed_{i}";
                cyl.transform.SetParent(go.transform, false);
                float h = (0.5f + i * 0.15f) * scale;
                float r = 0.03f * scale;
                cyl.transform.localScale = new Vector3(r, h * 0.5f, r);
                float offset = (i - 1) * 0.08f;
                cyl.transform.localPosition = new Vector3(offset, h * 0.5f, offset * 0.5f);
                // Slight lean
                cyl.transform.localRotation = Quaternion.Euler((i - 1) * 5f, 0f, (i - 1) * 3f);

                if (_matReeds != null)
                    cyl.GetComponent<MeshRenderer>().sharedMaterial = _matReeds;

                var col = cyl.GetComponent<Collider>();
                if (col != null) col.isTrigger = true;
            }

            return go;
        }

        /// <summary>Resin: small amber sphere on tree surface.</summary>
        private GameObject CreateResinProp(Vector3 pos, float scale, Transform parent)
        {
            var go = new GameObject("Resin");
            go.transform.SetParent(parent);
            go.transform.position = pos;

            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = "Mesh";
            sphere.transform.SetParent(go.transform, false);
            float r = 0.15f * scale;
            sphere.transform.localScale = new Vector3(r, r, r);
            sphere.transform.localPosition = new Vector3(0f, r * 0.5f, 0f);

            if (_matResin != null)
                sphere.GetComponent<MeshRenderer>().sharedMaterial = _matResin;

            var col = sphere.GetComponent<Collider>();
            if (col != null) col.isTrigger = true;

            return go;
        }

        /// <summary>Clay: flat brown cylinder near water.</summary>
        private GameObject CreateClayProp(Vector3 pos, float scale, float yRot, Transform parent)
        {
            var go = new GameObject("Clay");
            go.transform.SetParent(parent);
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(0f, yRot, 0f);

            var cyl = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            cyl.name = "Mesh";
            cyl.transform.SetParent(go.transform, false);
            float r = 0.4f * scale;
            float h = 0.1f * scale;
            cyl.transform.localScale = new Vector3(r, h, r);
            cyl.transform.localPosition = new Vector3(0f, h * 0.3f, 0f);

            if (_matClay != null)
                cyl.GetComponent<MeshRenderer>().sharedMaterial = _matClay;

            var col = cyl.GetComponent<Collider>();
            if (col != null) col.isTrigger = true;

            return go;
        }

        /// <summary>Fish spot: blue shimmer sphere near water.</summary>
        private GameObject CreateFishProp(Vector3 pos, float scale, Transform parent)
        {
            var go = new GameObject("FishSpot");
            go.transform.SetParent(parent);
            // Fish spots sit slightly lower (at water level)
            go.transform.position = pos + new Vector3(0f, -0.1f, 0f);

            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = "Mesh";
            sphere.transform.SetParent(go.transform, false);
            float r = 0.3f * scale;
            sphere.transform.localScale = new Vector3(r, r * 0.5f, r);
            sphere.transform.localPosition = new Vector3(0f, 0.05f, 0f);

            if (_matFish != null)
                sphere.GetComponent<MeshRenderer>().sharedMaterial = _matFish;

            var col = sphere.GetComponent<Collider>();
            if (col != null) col.isTrigger = true;

            return go;
        }

        /// <summary>Shelter marker: large dark brown cube (cave/overhang/undergrowth).</summary>
        private GameObject CreateShelterProp(Vector3 pos, float scale, float yRot, Transform parent)
        {
            var go = new GameObject("ShelterMarker");
            go.transform.SetParent(parent);
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(0f, yRot, 0f);

            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "Mesh";
            cube.transform.SetParent(go.transform, false);
            float sz = 1.2f * scale;
            cube.transform.localScale = new Vector3(sz, sz * 0.8f, sz * 0.7f);
            cube.transform.localPosition = new Vector3(0f, sz * 0.4f, 0f);

            if (_matShelter != null)
                cube.GetComponent<MeshRenderer>().sharedMaterial = _matShelter;

            var col = cube.GetComponent<Collider>();
            if (col != null) col.isTrigger = true;

            return go;
        }

        /// <summary>Insects: tiny brown sphere.</summary>
        private GameObject CreateInsectsProp(Vector3 pos, float scale, Transform parent)
        {
            var go = new GameObject("Insects");
            go.transform.SetParent(parent);
            go.transform.position = pos;

            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = "Mesh";
            sphere.transform.SetParent(go.transform, false);
            float r = 0.08f * scale;
            sphere.transform.localScale = new Vector3(r, r, r);
            sphere.transform.localPosition = new Vector3(0f, r * 0.5f, 0f);

            if (_matInsects != null)
                sphere.GetComponent<MeshRenderer>().sharedMaterial = _matInsects;

            var col = sphere.GetComponent<Collider>();
            if (col != null) col.isTrigger = true;

            return go;
        }

        /// <summary>Roots: brown elongated cube near water.</summary>
        private GameObject CreateRootsProp(Vector3 pos, float scale, float yRot, Transform parent)
        {
            var go = new GameObject("Roots");
            go.transform.SetParent(parent);
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(0f, yRot, 0f);

            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "Mesh";
            cube.transform.SetParent(go.transform, false);
            float length = 0.4f * scale;
            float height = 0.08f * scale;
            float width = 0.1f * scale;
            cube.transform.localScale = new Vector3(length, height, width);
            cube.transform.localPosition = new Vector3(0f, height * 0.3f, 0f);
            // Roots twist slightly
            cube.transform.localRotation = Quaternion.Euler(5f, 0f, 8f);

            if (_matRoots != null)
                cube.GetComponent<MeshRenderer>().sharedMaterial = _matRoots;

            var col = cube.GetComponent<Collider>();
            if (col != null) col.isTrigger = true;

            return go;
        }

        /// <summary>Honey: golden sphere in forest areas.</summary>
        private GameObject CreateHoneyProp(Vector3 pos, float scale, Transform parent)
        {
            var go = new GameObject("Honey");
            go.transform.SetParent(parent);
            go.transform.position = pos;

            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = "Mesh";
            sphere.transform.SetParent(go.transform, false);
            float r = 0.18f * scale;
            sphere.transform.localScale = new Vector3(r, r * 0.8f, r);
            sphere.transform.localPosition = new Vector3(0f, r * 0.5f + 0.3f, 0f); // elevated (on tree)

            if (_matHoney != null)
                sphere.GetComponent<MeshRenderer>().sharedMaterial = _matHoney;

            var col = sphere.GetComponent<Collider>();
            if (col != null) col.isTrigger = true;

            return go;
        }

        /// <summary>Fallback generic prop: small grey cube.</summary>
        private GameObject CreateGenericProp(Vector3 pos, float scale, float yRot, Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent);
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(0f, yRot, 0f);

            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "Mesh";
            cube.transform.SetParent(go.transform, false);
            float sz = 0.25f * scale;
            cube.transform.localScale = new Vector3(sz, sz, sz);
            cube.transform.localPosition = new Vector3(0f, sz * 0.5f, 0f);

            var col = cube.GetComponent<Collider>();
            if (col != null) col.isTrigger = true;

            return go;
        }

        // ─── Material setup ─────────────────────────────────────────

        private static Shader FindShader()
        {
            return Shader.Find("Universal Render Pipeline/Lit")
                ?? Shader.Find("Universal Render Pipeline/Particles/Unlit");
        }

        private static Material MakeMat(string name, Color color)
        {
            Shader shader = FindShader();
            if (shader == null) return null;
            var mat = new Material(shader);
            mat.name = name;
            mat.SetColor("_BaseColor", color);
            return mat;
        }

        private static void EnsureMaterials()
        {
            if (_matWood != null) return;

            // Wood/Deadwood: brown (0.5, 0.3, 0.15)
            _matWood = MakeMat("Wood_Material (Auto)", new Color(0.50f, 0.30f, 0.15f));

            // Driftwood: lighter grey-brown
            _matDriftwood = MakeMat("Driftwood_Material (Auto)", new Color(0.55f, 0.45f, 0.35f));

            // Berries (safe): red (0.8, 0.2, 0.2)
            _matBerry = MakeMat("Berry_Material (Auto)", new Color(0.80f, 0.20f, 0.20f));

            // Berries (poisonous): dark purple-red
            _matBerryPoison = MakeMat("BerryPoison_Material (Auto)", new Color(0.45f, 0.10f, 0.35f));

            // Stone: gray (0.5, 0.5, 0.55)
            _matStone = MakeMat("Stone_Material (Auto)", new Color(0.50f, 0.50f, 0.55f));

            // Flint: darker gray with slight blue
            _matFlint = MakeMat("Flint_Material (Auto)", new Color(0.35f, 0.35f, 0.40f));

            // Granite: speckled gray
            _matGranite = MakeMat("Granite_Material (Auto)", new Color(0.60f, 0.58f, 0.55f));

            // Limestone: light warm gray
            _matLimestone = MakeMat("Limestone_Material (Auto)", new Color(0.75f, 0.72f, 0.65f));

            // Reeds: green
            _matReeds = MakeMat("Reeds_Material (Auto)", new Color(0.30f, 0.55f, 0.20f));

            // Resin: amber (0.8, 0.6, 0.2)
            _matResin = MakeMat("Resin_Material (Auto)", new Color(0.80f, 0.60f, 0.20f));

            // Clay: flat brown
            _matClay = MakeMat("Clay_Material (Auto)", new Color(0.55f, 0.35f, 0.20f));

            // Fish spot: blue shimmer
            _matFish = MakeMat("Fish_Material (Auto)", new Color(0.20f, 0.50f, 0.80f));

            // Shelter marker: large dark brown
            _matShelter = MakeMat("Shelter_Material (Auto)", new Color(0.25f, 0.18f, 0.10f));

            // Insects: tiny brown
            _matInsects = MakeMat("Insects_Material (Auto)", new Color(0.40f, 0.30f, 0.15f));

            // Roots: brown
            _matRoots = MakeMat("Roots_Material (Auto)", new Color(0.45f, 0.30f, 0.18f));

            // Honey: golden (0.8, 0.6, 0.2) - slightly different from resin
            _matHoney = MakeMat("Honey_Material (Auto)", new Color(0.85f, 0.65f, 0.10f));
        }
    }
}
