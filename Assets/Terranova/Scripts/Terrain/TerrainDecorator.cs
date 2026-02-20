using UnityEngine;
using Terranova.Core;

namespace Terranova.Terrain
{
    /// <summary>
    /// v0.5.2: Visual overhaul — replaces all primitive objects with Explorer Stoneage
    /// asset prefabs. Biome-aware placement for Forest, Mountains, and Coast.
    ///
    /// Decoration categories:
    ///   DECORATION (permanent, non-interactive): Trees, Bushes, Ferns, Flowers,
    ///       Large/Medium Rocks, Tree Stumps, Cliff Formations
    ///   GATHERABLE (ResourceNode, disappears when collected): Mushrooms, Tree Logs,
    ///       Twigs, Small Stones, Bones, Animal Carcasses
    ///   SHELTER (NaturalShelter component, tappable): Cave Entrance, Canyon Overpass,
    ///       Rock Cluster, Dense Bush Cluster
    ///
    /// Atmospheric particles: Sun Shafts (Forest day), Fog (Mountains morning),
    ///   Fireflies (near campfire at night)
    ///
    /// Runs once after WorldManager + NavMesh are ready.
    /// </summary>
    public class TerrainDecorator : MonoBehaviour
    {
        private bool _decorated;

        // Ground patch materials (still use quads for terrain blending)
        private Material _groundDirtMat;
        private Material _groundSandMat;
        private Material _groundDarkGrassMat;
        private Material _groundRockyMat;

        private void Update()
        {
            if (_decorated) return;
            var world = WorldManager.Instance;
            if (world == null || world.WorldBlocksX == 0 || !world.IsNavMeshReady) return;
            _decorated = true;
            DecorateWorld(world);
            enabled = false;
        }

        private void DecorateWorld(WorldManager world)
        {
            var biome = GameState.SelectedBiome;
            var rng = new System.Random(GameState.Seed + 500);

            CreateGroundMaterials();

            var container = new GameObject("TerrainDecorations");

            SpawnTrees(world, biome, rng, container.transform);
            SpawnBushes(world, biome, rng, container.transform);
            SpawnFerns(world, biome, rng, container.transform);
            SpawnFlowers(world, biome, rng, container.transform);
            SpawnRocks(world, biome, rng, container.transform);
            SpawnTreeStumps(world, biome, rng, container.transform);
            SpawnGroundPatches(world, biome, rng, container.transform);
            SpawnShelters(world, biome, rng, container.transform);
            SpawnAtmosphericEffects(world, biome, rng, container.transform);

            if (biome == BiomeType.Coast)
                SpawnOceanPlane(world, container.transform);

            Debug.Log($"[TerrainDecorator] v0.5.2 prefab decoration complete for biome={biome}");
        }

        // ═══════════════════════════════════════════════════════════
        //  MATERIALS (only for ground patches and water)
        // ═══════════════════════════════════════════════════════════

        private void CreateGroundMaterials()
        {
            _groundDirtMat      = TerrainShaderLibrary.CreatePropMaterial("Decor_GroundDirt",      new Color(0.45f, 0.35f, 0.22f), 0.05f);
            _groundSandMat      = TerrainShaderLibrary.CreatePropMaterial("Decor_GroundSand",      new Color(0.75f, 0.68f, 0.50f), 0.05f);
            _groundDarkGrassMat = TerrainShaderLibrary.CreatePropMaterial("Decor_GroundDarkGrass", new Color(0.18f, 0.38f, 0.12f), 0.05f);
            _groundRockyMat     = TerrainShaderLibrary.CreatePropMaterial("Decor_GroundRocky",     new Color(0.42f, 0.40f, 0.38f), 0.05f);
        }

        // ═══════════════════════════════════════════════════════════
        //  POSITION HELPERS
        // ═══════════════════════════════════════════════════════════

        private bool TryFindPosition(WorldManager world, System.Random rng,
            int campX, int campZ, float minCampDist, out Vector3 pos,
            bool allowSand = false)
        {
            pos = Vector3.zero;
            float x = 4f + (float)(rng.NextDouble() * (world.WorldBlocksX - 8));
            float z = 4f + (float)(rng.NextDouble() * (world.WorldBlocksZ - 8));

            float distToCamp = Mathf.Sqrt((x - campX) * (x - campX) + (z - campZ) * (z - campZ));
            if (distToCamp < minCampDist) return false;

            int bx = Mathf.FloorToInt(x);
            int bz = Mathf.FloorToInt(z);
            int solidH = world.GetSolidHeightAtWorldPos(bx, bz);
            if (solidH < 0) return false;

            var surface = world.GetSolidSurfaceTypeAtWorldPos(bx, bz);
            if (surface == VoxelType.Water) return false;
            if (!allowSand && surface == VoxelType.Sand) return false;

            float y = world.GetSmoothedHeightAtWorldPos(x, z);
            pos = new Vector3(x, y, z);
            return true;
        }

        private Vector3 FindShelterPosition(WorldManager world, System.Random rng,
            int campX, int campZ, float minDist, float maxDist)
        {
            for (int attempt = 0; attempt < 20; attempt++)
            {
                float angle = (float)(rng.NextDouble() * Mathf.PI * 2f);
                float dist = minDist + (float)(rng.NextDouble() * (maxDist - minDist));
                float x = campX + Mathf.Cos(angle) * dist;
                float z = campZ + Mathf.Sin(angle) * dist;

                x = Mathf.Clamp(x, 6f, world.WorldBlocksX - 7f);
                z = Mathf.Clamp(z, 6f, world.WorldBlocksZ - 7f);

                int bx = Mathf.FloorToInt(x);
                int bz = Mathf.FloorToInt(z);
                int solidH = world.GetSolidHeightAtWorldPos(bx, bz);
                if (solidH < 0) continue;

                var surface = world.GetSolidSurfaceTypeAtWorldPos(bx, bz);
                if (surface == VoxelType.Water || surface == VoxelType.Sand) continue;

                float y = world.GetSmoothedHeightAtWorldPos(x, z);
                return new Vector3(x, y, z);
            }
            return Vector3.zero;
        }

        // ═══════════════════════════════════════════════════════════
        //  TREES (DECORATION — permanent, not gatherable)
        // ═══════════════════════════════════════════════════════════

        private void SpawnTrees(WorldManager world, BiomeType biome, System.Random rng, Transform parent)
        {
            int count;
            string[] pool;

            switch (biome)
            {
                case BiomeType.Forest:
                    count = 30 + rng.Next(11); // 30-40
                    // Mix pine and deciduous trees
                    pool = new string[AssetPrefabRegistry.PineTrees.Length + AssetPrefabRegistry.DeciduousTrees.Length];
                    AssetPrefabRegistry.PineTrees.CopyTo(pool, 0);
                    AssetPrefabRegistry.DeciduousTrees.CopyTo(pool, AssetPrefabRegistry.PineTrees.Length);
                    break;
                case BiomeType.Mountains:
                    count = 3 + rng.Next(3); // 3-5
                    pool = AssetPrefabRegistry.MountainTrees;
                    break;
                case BiomeType.Coast:
                    count = 3 + rng.Next(3); // 3-5
                    pool = AssetPrefabRegistry.CoastTrees;
                    break;
                default:
                    count = 20;
                    pool = AssetPrefabRegistry.DeciduousTrees;
                    break;
            }

            var treeContainer = new GameObject("Trees");
            treeContainer.transform.SetParent(parent, false);

            int placed = 0;
            int campX = world.CampfireBlockX;
            int campZ = world.CampfireBlockZ;

            for (int attempt = 0; attempt < count * 5 && placed < count; attempt++)
            {
                if (!TryFindPosition(world, rng, campX, campZ, 8f, out Vector3 pos))
                    continue;

                var tree = AssetPrefabRegistry.InstantiateRandom(pool, pos, rng, treeContainer.transform, 0.8f, 1.2f);
                if (tree != null)
                {
                    tree.name = "Tree";
                    placed++;
                }
            }

            Debug.Log($"[TerrainDecorator] Placed {placed} trees ({biome})");
        }

        // ═══════════════════════════════════════════════════════════
        //  BUSHES (DECORATION — permanent)
        // ═══════════════════════════════════════════════════════════

        private void SpawnBushes(WorldManager world, BiomeType biome, System.Random rng, Transform parent)
        {
            int count;
            string[] pool;

            switch (biome)
            {
                case BiomeType.Forest:
                    count = 20 + rng.Next(11); // 20-30
                    pool = AssetPrefabRegistry.Bushes;
                    break;
                case BiomeType.Mountains:
                    count = 8 + rng.Next(5); // 8-12
                    pool = AssetPrefabRegistry.Bushes;
                    break;
                case BiomeType.Coast:
                    count = 10 + rng.Next(6); // 10-15
                    pool = AssetPrefabRegistry.CoastBushes;
                    break;
                default:
                    count = 15;
                    pool = AssetPrefabRegistry.Bushes;
                    break;
            }

            var bushContainer = new GameObject("Bushes");
            bushContainer.transform.SetParent(parent, false);

            int placed = 0;
            int campX = world.CampfireBlockX;
            int campZ = world.CampfireBlockZ;

            // Place bushes in clusters of 2-4
            int clusterTarget = count;
            for (int attempt = 0; attempt < clusterTarget * 5 && placed < count; attempt++)
            {
                if (!TryFindPosition(world, rng, campX, campZ, 5f, out Vector3 clusterCenter, biome == BiomeType.Coast))
                    continue;

                int clusterSize = 2 + rng.Next(3); // 2-4 per cluster
                for (int c = 0; c < clusterSize && placed < count; c++)
                {
                    Vector3 offset = new Vector3(
                        (float)(rng.NextDouble() - 0.5) * 3f, 0f,
                        (float)(rng.NextDouble() - 0.5) * 3f);
                    Vector3 bushPos = clusterCenter + offset;
                    bushPos.y = world.GetSmoothedHeightAtWorldPos(bushPos.x, bushPos.z);

                    var bush = AssetPrefabRegistry.InstantiateRandom(pool, bushPos, rng, bushContainer.transform, 0.7f, 1.1f);
                    if (bush != null)
                    {
                        bush.name = "Bush";
                        placed++;
                    }
                }
            }

            Debug.Log($"[TerrainDecorator] Placed {placed} bushes ({biome})");
        }

        // ═══════════════════════════════════════════════════════════
        //  FERNS (DECORATION — ground cover)
        // ═══════════════════════════════════════════════════════════

        private void SpawnFerns(WorldManager world, BiomeType biome, System.Random rng, Transform parent)
        {
            int count;
            switch (biome)
            {
                case BiomeType.Forest: count = 15 + rng.Next(6); break; // 15-20
                case BiomeType.Coast:  count = 10 + rng.Next(6); break; // 10-15 (as reeds near water)
                default:               count = 5; break;
            }

            if (count <= 0) return;

            var fernContainer = new GameObject("Ferns");
            fernContainer.transform.SetParent(parent, false);

            int placed = 0;
            int campX = world.CampfireBlockX;
            int campZ = world.CampfireBlockZ;

            for (int attempt = 0; attempt < count * 5 && placed < count; attempt++)
            {
                bool allowSand = biome == BiomeType.Coast;
                if (!TryFindPosition(world, rng, campX, campZ, 5f, out Vector3 pos, allowSand))
                    continue;

                var fern = AssetPrefabRegistry.InstantiateRandom(AssetPrefabRegistry.Ferns, pos, rng, fernContainer.transform, 0.7f, 1.0f);
                if (fern != null)
                {
                    fern.name = "Fern";
                    placed++;
                }
            }

            Debug.Log($"[TerrainDecorator] Placed {placed} ferns ({biome})");
        }

        // ═══════════════════════════════════════════════════════════
        //  FLOWERS (DECORATION — Forest only)
        // ═══════════════════════════════════════════════════════════

        private void SpawnFlowers(WorldManager world, BiomeType biome, System.Random rng, Transform parent)
        {
            if (biome != BiomeType.Forest) return;

            int count = 10 + rng.Next(6); // 10-15
            var flowerContainer = new GameObject("Flowers");
            flowerContainer.transform.SetParent(parent, false);

            int placed = 0;
            int campX = world.CampfireBlockX;
            int campZ = world.CampfireBlockZ;

            for (int attempt = 0; attempt < count * 5 && placed < count; attempt++)
            {
                if (!TryFindPosition(world, rng, campX, campZ, 5f, out Vector3 pos))
                    continue;

                var flower = AssetPrefabRegistry.InstantiateRandom(AssetPrefabRegistry.Flowers, pos, rng, flowerContainer.transform, 0.8f, 1.1f);
                if (flower != null)
                {
                    flower.name = "Flower";
                    placed++;
                }
            }

            Debug.Log($"[TerrainDecorator] Placed {placed} flowers");
        }

        // ═══════════════════════════════════════════════════════════
        //  ROCKS (DECORATION — permanent, NOT gatherable)
        // ═══════════════════════════════════════════════════════════

        private void SpawnRocks(WorldManager world, BiomeType biome, System.Random rng, Transform parent)
        {
            var rockContainer = new GameObject("Rocks");
            rockContainer.transform.SetParent(parent, false);

            int campX = world.CampfireBlockX;
            int campZ = world.CampfireBlockZ;
            int placed = 0;

            switch (biome)
            {
                case BiomeType.Forest:
                {
                    // Large/Medium decorative rocks: 5-10
                    int largeCount = 5 + rng.Next(6);
                    for (int attempt = 0; attempt < largeCount * 5 && placed < largeCount; attempt++)
                    {
                        if (!TryFindPosition(world, rng, campX, campZ, 6f, out Vector3 pos))
                            continue;
                        string[] pool = rng.Next(2) == 0 ? AssetPrefabRegistry.RockLarge : AssetPrefabRegistry.RockMedium;
                        var rock = AssetPrefabRegistry.InstantiateRandom(pool, pos, rng, rockContainer.transform, 0.6f, 1.0f);
                        if (rock != null) { rock.name = "Rock_Decor"; placed++; }
                    }
                    break;
                }

                case BiomeType.Mountains:
                {
                    // Dominant large rocks + cliffs: 15-20
                    int targetLarge = 15 + rng.Next(6);
                    for (int attempt = 0; attempt < targetLarge * 5 && placed < targetLarge; attempt++)
                    {
                        if (!TryFindPosition(world, rng, campX, campZ, 6f, out Vector3 pos))
                            continue;

                        string[] pool;
                        int roll = rng.Next(10);
                        if (roll < 4)
                            pool = AssetPrefabRegistry.RockLarge;
                        else if (roll < 7)
                            pool = AssetPrefabRegistry.CliffFormations;
                        else
                            pool = AssetPrefabRegistry.CanyonWalls;

                        var rock = AssetPrefabRegistry.InstantiateRandom(pool, pos, rng, rockContainer.transform, 0.5f, 1.0f);
                        if (rock != null) { rock.name = "Rock_Decor"; placed++; }
                    }

                    // Rock formations as landmarks: 3-5
                    int formCount = 3 + rng.Next(3);
                    int formPlaced = 0;
                    for (int attempt = 0; attempt < formCount * 5 && formPlaced < formCount; attempt++)
                    {
                        if (!TryFindPosition(world, rng, campX, campZ, 15f, out Vector3 pos))
                            continue;
                        var form = AssetPrefabRegistry.InstantiateRandom(AssetPrefabRegistry.RockFormations, pos, rng, rockContainer.transform, 0.8f, 1.2f);
                        if (form != null) { form.name = "RockFormation_Landmark"; formPlaced++; placed++; }
                    }

                    // Sharp rocks: 5-8
                    int sharpCount = 5 + rng.Next(4);
                    int sharpPlaced = 0;
                    for (int attempt = 0; attempt < sharpCount * 5 && sharpPlaced < sharpCount; attempt++)
                    {
                        if (!TryFindPosition(world, rng, campX, campZ, 6f, out Vector3 pos))
                            continue;
                        var sharp = AssetPrefabRegistry.InstantiateRandom(AssetPrefabRegistry.RockSharp, pos, rng, rockContainer.transform, 0.7f, 1.1f);
                        if (sharp != null) { sharp.name = "Rock_Sharp"; sharpPlaced++; placed++; }
                    }
                    break;
                }

                case BiomeType.Coast:
                {
                    // Flat rocks near shore: 5-8
                    int slabCount = 5 + rng.Next(4);
                    for (int attempt = 0; attempt < slabCount * 5 && placed < slabCount; attempt++)
                    {
                        if (!TryFindPosition(world, rng, campX, campZ, 6f, out Vector3 pos, true))
                            continue;
                        var slab = AssetPrefabRegistry.InstantiateRandom(AssetPrefabRegistry.RockSlabs, pos, rng, rockContainer.transform, 0.7f, 1.0f);
                        if (slab != null) { slab.name = "Rock_Slab"; placed++; }
                    }

                    // Rock pavements: 3-5
                    int pavCount = 3 + rng.Next(3);
                    int pavPlaced = 0;
                    for (int attempt = 0; attempt < pavCount * 5 && pavPlaced < pavCount; attempt++)
                    {
                        if (!TryFindPosition(world, rng, campX, campZ, 6f, out Vector3 pos, true))
                            continue;
                        var pav = AssetPrefabRegistry.InstantiateRandom(AssetPrefabRegistry.RockPavements, pos, rng, rockContainer.transform, 0.8f, 1.1f);
                        if (pav != null) { pav.name = "Rock_Pavement"; pavPlaced++; placed++; }
                    }
                    break;
                }
            }

            Debug.Log($"[TerrainDecorator] Placed {placed} decorative rocks ({biome})");
        }

        // ═══════════════════════════════════════════════════════════
        //  TREE STUMPS (DECORATION — Forest/Coast only)
        // ═══════════════════════════════════════════════════════════

        private void SpawnTreeStumps(WorldManager world, BiomeType biome, System.Random rng, Transform parent)
        {
            if (biome == BiomeType.Mountains) return;

            int count = 5 + rng.Next(4); // 5-8
            var stumpContainer = new GameObject("TreeStumps");
            stumpContainer.transform.SetParent(parent, false);

            int placed = 0;
            int campX = world.CampfireBlockX;
            int campZ = world.CampfireBlockZ;

            for (int attempt = 0; attempt < count * 5 && placed < count; attempt++)
            {
                if (!TryFindPosition(world, rng, campX, campZ, 8f, out Vector3 pos))
                    continue;

                var stump = AssetPrefabRegistry.InstantiateRandom(AssetPrefabRegistry.TreeTrunks, pos, rng, stumpContainer.transform, 0.8f, 1.1f);
                if (stump != null)
                {
                    stump.name = "TreeStump";
                    placed++;
                }
            }

            Debug.Log($"[TerrainDecorator] Placed {placed} tree stumps ({biome})");
        }

        // ═══════════════════════════════════════════════════════════
        //  GROUND VARIATION (still quads for terrain blending)
        // ═══════════════════════════════════════════════════════════

        private void SpawnGroundPatches(WorldManager world, BiomeType biome, System.Random rng, Transform parent)
        {
            int count = 30 + rng.Next(21); // 30-50

            var groundContainer = new GameObject("GroundPatches");
            groundContainer.transform.SetParent(parent, false);

            int campX = world.CampfireBlockX;
            int campZ = world.CampfireBlockZ;
            int placed = 0;

            for (int attempt = 0; attempt < count * 3 && placed < count; attempt++)
            {
                float x = 4f + (float)(rng.NextDouble() * (world.WorldBlocksX - 8));
                float z = 4f + (float)(rng.NextDouble() * (world.WorldBlocksZ - 8));

                int bx = Mathf.FloorToInt(x);
                int bz = Mathf.FloorToInt(z);
                int solidH = world.GetSolidHeightAtWorldPos(bx, bz);
                if (solidH < 0) continue;

                var surface = world.GetSolidSurfaceTypeAtWorldPos(bx, bz);
                if (surface == VoxelType.Water) continue;

                float y = world.GetSmoothedHeightAtWorldPos(x, z) + 0.02f;

                float distToCamp = Mathf.Sqrt((x - campX) * (x - campX) + (z - campZ) * (z - campZ));
                Material patchMat;

                if (distToCamp < 12f && rng.Next(2) == 0)
                    patchMat = _groundDirtMat;
                else if (surface == VoxelType.Sand)
                    patchMat = _groundSandMat;
                else if (biome == BiomeType.Mountains && rng.Next(2) == 0)
                    patchMat = _groundRockyMat;
                else
                    patchMat = _groundDarkGrassMat;

                float patchSize = 1.5f + (float)(rng.NextDouble() * 3.0f);

                var patch = GameObject.CreatePrimitive(PrimitiveType.Quad);
                patch.name = "GroundPatch";
                patch.transform.SetParent(groundContainer.transform, false);
                patch.transform.position = new Vector3(x, y, z);
                patch.transform.rotation = Quaternion.Euler(90f, (float)(rng.NextDouble() * 360.0), 0f);
                patch.transform.localScale = new Vector3(patchSize, patchSize, 1f);
                Object.Destroy(patch.GetComponent<Collider>());

                if (patchMat != null) patch.GetComponent<MeshRenderer>().sharedMaterial = patchMat;
                placed++;
            }

            Debug.Log($"[TerrainDecorator] Placed {placed} ground patches ({biome})");
        }

        // ═══════════════════════════════════════════════════════════
        //  NATURAL SHELTERS (prefab-based)
        // ═══════════════════════════════════════════════════════════

        private void SpawnShelters(WorldManager world, BiomeType biome, System.Random rng, Transform parent)
        {
            var shelterContainer = new GameObject("NaturalShelters");
            shelterContainer.transform.SetParent(parent, false);

            int campX = world.CampfireBlockX;
            int campZ = world.CampfireBlockZ;

            // Cave Entrance — 1 per map (Mountains priority, but available in all biomes)
            {
                float minDist = biome == BiomeType.Mountains ? 20f : 30f;
                Vector3 cavePos = FindShelterPosition(world, rng, campX, campZ, minDist, 55f);
                if (cavePos != Vector3.zero)
                    CreatePrefabShelter(AssetPrefabRegistry.CaveEntrance, cavePos, rng,
                        shelterContainer.transform, "Cave Entrance", "Cave Entrance", 5, 1.0f);
            }

            // Canyon Overpass — 2-3 per map
            int overpassCount = 2 + rng.Next(2);
            for (int i = 0; i < overpassCount; i++)
            {
                Vector3 pos = FindShelterPosition(world, rng, campX, campZ, 15f, 50f);
                if (pos != Vector3.zero)
                    CreatePrefabShelter(AssetPrefabRegistry.CanyonOverpass, pos, rng,
                        shelterContainer.transform, "Canyon Overpass", "Canyon Overpass", 3, 0.8f);
            }

            // Rock Cluster — 2-3 per map
            int clusterCount = 2 + rng.Next(2);
            for (int i = 0; i < clusterCount; i++)
            {
                Vector3 pos = FindShelterPosition(world, rng, campX, campZ, 12f, 45f);
                if (pos != Vector3.zero)
                    CreatePrefabShelter(AssetPrefabRegistry.RockClusters, pos, rng,
                        shelterContainer.transform, "Rock Cluster", "Rock Cluster", 3, 0.6f);
            }

            // Dense Bush Cluster — Forest only, 2-3 per map
            if (biome == BiomeType.Forest)
            {
                int thicketCount = 2 + rng.Next(2);
                for (int i = 0; i < thicketCount; i++)
                {
                    Vector3 pos = FindShelterPosition(world, rng, campX, campZ, 12f, 45f);
                    if (pos != Vector3.zero)
                        CreateBushClusterShelter(pos, rng, shelterContainer.transform);
                }
            }
        }

        private void CreatePrefabShelter(string[] prefabPool, Vector3 pos, System.Random rng,
            Transform parent, string shelterName, string shelterType, int capacity, float protection)
        {
            var instance = AssetPrefabRegistry.InstantiateRandom(prefabPool, pos, rng, parent, 0.9f, 1.1f);
            if (instance == null) return;

            instance.name = shelterName;
            instance.tag = "Shelter";

            // Add collider for selection if not present
            if (instance.GetComponent<Collider>() == null)
            {
                var col = instance.AddComponent<BoxCollider>();
                // Estimate bounds from renderers
                var bounds = new Bounds(pos, Vector3.zero);
                foreach (var r in instance.GetComponentsInChildren<Renderer>())
                    bounds.Encapsulate(r.bounds);
                col.center = instance.transform.InverseTransformPoint(bounds.center);
                col.size = bounds.size;
            }

            var ns = instance.AddComponent<NaturalShelter>();
            ns.ShelterName = shelterName;
            ns.ShelterType = shelterType;
            ns.Capacity = capacity;
            ns.ProtectionValue = protection;
        }

        private void CreateBushClusterShelter(Vector3 pos, System.Random rng, Transform parent)
        {
            var shelter = new GameObject("DenseBushCluster");
            shelter.transform.SetParent(parent, false);
            shelter.transform.position = pos;
            shelter.tag = "Shelter";

            // Place 3-4 overlapping bush prefabs
            int bushCount = 3 + rng.Next(2);
            for (int i = 0; i < bushCount; i++)
            {
                Vector3 offset = new Vector3(
                    (float)(rng.NextDouble() - 0.5) * 2.5f, 0f,
                    (float)(rng.NextDouble() - 0.5) * 2.5f);
                Vector3 bushPos = pos + offset;

                var bush = AssetPrefabRegistry.InstantiateRandom(AssetPrefabRegistry.Bushes, bushPos, rng, shelter.transform, 1.0f, 1.4f);
                if (bush != null)
                    bush.name = $"ShelterBush_{i}";
            }

            var col = shelter.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.6f, 0f);
            col.size = new Vector3(3f, 1.5f, 3f);

            var ns = shelter.AddComponent<NaturalShelter>();
            ns.ShelterName = "Dense Bush Cluster";
            ns.ShelterType = "Dense Thicket";
            ns.Capacity = 2;
            ns.ProtectionValue = 0.4f;
        }

        // ═══════════════════════════════════════════════════════════
        //  ATMOSPHERIC EFFECTS
        // ═══════════════════════════════════════════════════════════

        private void SpawnAtmosphericEffects(WorldManager world, BiomeType biome, System.Random rng, Transform parent)
        {
            var fxContainer = new GameObject("AtmosphericFX");
            fxContainer.transform.SetParent(parent, false);

            int campX = world.CampfireBlockX;
            int campZ = world.CampfireBlockZ;
            float campY = world.GetSmoothedHeightAtWorldPos(campX + 0.5f, campZ + 0.5f);
            Vector3 campPos = new Vector3(campX + 0.5f, campY, campZ + 0.5f);

            // Sun Shaft in forest biome
            if (biome == BiomeType.Forest)
            {
                // Place 2-3 sun shafts in open areas
                for (int i = 0; i < 2 + rng.Next(2); i++)
                {
                    if (TryFindPosition(world, rng, campX, campZ, 10f, out Vector3 pos))
                    {
                        var sunShaft = AssetPrefabRegistry.InstantiateSpecific(
                            AssetPrefabRegistry.SunShaftParticle,
                            pos + new Vector3(0f, 3f, 0f),
                            Quaternion.identity, fxContainer.transform);
                        if (sunShaft != null)
                            sunShaft.name = "SunShaft";
                    }
                }
            }

            // Fog in mountains biome
            if (biome == BiomeType.Mountains)
            {
                // Place 2-3 fog volumes
                for (int i = 0; i < 2 + rng.Next(2); i++)
                {
                    if (TryFindPosition(world, rng, campX, campZ, 15f, out Vector3 pos))
                    {
                        var fog = AssetPrefabRegistry.InstantiateSpecific(
                            AssetPrefabRegistry.FogParticle,
                            pos + new Vector3(0f, 1f, 0f),
                            Quaternion.identity, fxContainer.transform);
                        if (fog != null)
                            fog.name = "MorningFog";
                    }
                }
            }

            // Fireflies near campfire (all biomes)
            {
                var fireflies = AssetPrefabRegistry.InstantiateSpecific(
                    AssetPrefabRegistry.FirefliesParticle,
                    campPos + new Vector3(2f, 1f, 2f),
                    Quaternion.identity, fxContainer.transform);
                if (fireflies != null)
                    fireflies.name = "Fireflies";
            }
        }

        // ═══════════════════════════════════════════════════════════
        //  WATER FEATURES
        // ═══════════════════════════════════════════════════════════

        private void SpawnOceanPlane(WorldManager world, Transform parent)
        {
            float oceanWidth = world.WorldBlocksX;
            float oceanDepth = 30f;
            float seaY = 64.15f;

            var ocean = GameObject.CreatePrimitive(PrimitiveType.Quad);
            ocean.name = "OceanPlane";
            ocean.tag = "Water";
            ocean.transform.SetParent(parent, false);
            ocean.transform.position = new Vector3(oceanWidth * 0.5f, seaY, oceanDepth * 0.5f);
            ocean.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            ocean.transform.localScale = new Vector3(oceanWidth + 20f, oceanDepth + 10f, 1f);
            Object.Destroy(ocean.GetComponent<Collider>());

            var waterMat = TerrainShaderLibrary.CreateWaterMaterial();
            if (waterMat != null)
                ocean.GetComponent<MeshRenderer>().sharedMaterial = waterMat;

            Debug.Log("[TerrainDecorator] Placed ocean plane for coast biome");
        }
    }
}
