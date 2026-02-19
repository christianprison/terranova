using UnityEngine;
using Terranova.Core;

namespace Terranova.Terrain
{
    /// <summary>
    /// v0.5.0: Terrain visual upgrade. Spawns decorative trees, rocks, bushes,
    /// ground variation patches, natural shelters, and improved water features.
    /// All objects use simple geometry (primitives). Biome-aware placement.
    /// Runs once after WorldManager + NavMesh are ready.
    /// </summary>
    public class TerrainDecorator : MonoBehaviour
    {
        private bool _decorated;

        // Shared materials (created once, reused by all decorations)
        private Material _trunkMat;
        private Material _pineCanopyMat;
        private Material _oakCanopyMat;
        private Material _birchCanopyMat;
        private Material _palmCanopyMat;
        private Material _rockMat;
        private Material _darkRockMat;
        private Material _bushMat;
        private Material _berryBushMat;
        private Material _groundDirtMat;
        private Material _groundSandMat;
        private Material _groundDarkGrassMat;
        private Material _groundRockyMat;
        private Material _shelterRockMat;
        private Material _shelterThicketMat;
        private Material _shelterCaveMat;
        private Material _reedMat;
        private Material _waterPlaneMat;

        // Cached cone mesh for pine canopies
        private static Mesh _coneMesh;

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

            CreateMaterials();

            var container = new GameObject("TerrainDecorations");

            SpawnTrees(world, biome, rng, container.transform);
            SpawnRocks(world, biome, rng, container.transform);
            SpawnBushes(world, biome, rng, container.transform);
            SpawnGroundPatches(world, biome, rng, container.transform);
            SpawnShelters(world, biome, rng, container.transform);

            if (biome == BiomeType.Coast)
                SpawnOceanPlane(world, container.transform);

            Debug.Log($"[TerrainDecorator] Decoration complete for biome={biome}");
        }

        // ═══════════════════════════════════════════════════════════
        //  MATERIALS
        // ═══════════════════════════════════════════════════════════

        private void CreateMaterials()
        {
            // Trunk: brown bark, low smoothness
            _trunkMat       = TerrainShaderLibrary.CreateWoodMaterial("Decor_Trunk", new Color(0.40f, 0.26f, 0.13f));

            // Canopies: use WindFoliage shader for alpha cutout + wind sway
            _pineCanopyMat  = TerrainShaderLibrary.CreateFoliageMaterial("Decor_PineCanopy",  new Color(0.15f, 0.40f, 0.15f), 0.30f, 0.06f, 1.2f);
            _oakCanopyMat   = TerrainShaderLibrary.CreateFoliageMaterial("Decor_OakCanopy",   new Color(0.25f, 0.55f, 0.20f), 0.35f, 0.10f, 1.5f);
            _birchCanopyMat = TerrainShaderLibrary.CreateFoliageMaterial("Decor_BirchCanopy", new Color(0.40f, 0.65f, 0.30f), 0.32f, 0.12f, 1.8f);
            _palmCanopyMat  = TerrainShaderLibrary.CreateFoliageMaterial("Decor_PalmCanopy",  new Color(0.20f, 0.50f, 0.15f), 0.28f, 0.05f, 1.0f);

            // Rocks: low smoothness, no metallic
            _rockMat        = TerrainShaderLibrary.CreateRockMaterial("Decor_Rock",      new Color(0.50f, 0.48f, 0.45f));
            _darkRockMat    = TerrainShaderLibrary.CreateRockMaterial("Decor_DarkRock",  new Color(0.35f, 0.33f, 0.30f));

            // Bushes: wind foliage
            _bushMat        = TerrainShaderLibrary.CreateFoliageMaterial("Decor_Bush",      new Color(0.22f, 0.48f, 0.18f), 0.38f, 0.07f, 1.6f);
            _berryBushMat   = TerrainShaderLibrary.CreateFoliageMaterial("Decor_BerryBush", new Color(0.20f, 0.42f, 0.15f), 0.36f, 0.07f, 1.4f);

            // Reeds: wind foliage with thinner cutout
            _reedMat        = TerrainShaderLibrary.CreateFoliageMaterial("Decor_Reed",      new Color(0.55f, 0.60f, 0.30f), 0.25f, 0.15f, 2.0f);

            // Ground patches: use trampled path shader for terrain blending
            _groundDirtMat      = TerrainShaderLibrary.CreatePropMaterial("Decor_GroundDirt",      new Color(0.45f, 0.35f, 0.22f), 0.05f);
            _groundSandMat      = TerrainShaderLibrary.CreatePropMaterial("Decor_GroundSand",      new Color(0.75f, 0.68f, 0.50f), 0.05f);
            _groundDarkGrassMat = TerrainShaderLibrary.CreatePropMaterial("Decor_GroundDarkGrass", new Color(0.18f, 0.38f, 0.12f), 0.05f);
            _groundRockyMat     = TerrainShaderLibrary.CreatePropMaterial("Decor_GroundRocky",     new Color(0.42f, 0.40f, 0.38f), 0.05f);

            // Shelters
            _shelterRockMat   = TerrainShaderLibrary.CreateRockMaterial("Decor_ShelterRock",   new Color(0.48f, 0.44f, 0.40f));
            _shelterThicketMat= TerrainShaderLibrary.CreateFoliageMaterial("Decor_ShelterThicket", new Color(0.18f, 0.40f, 0.12f), 0.40f, 0.05f, 1.2f);
            _shelterCaveMat   = TerrainShaderLibrary.CreateCaveInteriorMaterial();

            // Water: enhanced water surface shader
            _waterPlaneMat    = TerrainShaderLibrary.CreateWaterMaterial();
        }

        // ═══════════════════════════════════════════════════════════
        //  TREES
        // ═══════════════════════════════════════════════════════════

        private void SpawnTrees(WorldManager world, BiomeType biome, System.Random rng, Transform parent)
        {
            int count;
            switch (biome)
            {
                case BiomeType.Forest:    count = 40 + rng.Next(21); break; // 40-60
                case BiomeType.Mountains: count = 10 + rng.Next(6);  break; // 10-15
                case BiomeType.Coast:     count = 15 + rng.Next(11); break; // 15-25
                default:                  count = 30; break;
            }

            var treeContainer = new GameObject("Trees");
            treeContainer.transform.SetParent(parent, false);

            int placed = 0;
            int campX = world.CampfireBlockX;
            int campZ = world.CampfireBlockZ;

            for (int attempt = 0; attempt < count * 5 && placed < count; attempt++)
            {
                float x = 4f + (float)(rng.NextDouble() * (world.WorldBlocksX - 8));
                float z = 4f + (float)(rng.NextDouble() * (world.WorldBlocksZ - 8));

                // Stay away from campfire (8 block radius)
                float distToCamp = Mathf.Sqrt((x - campX) * (x - campX) + (z - campZ) * (z - campZ));
                if (distToCamp < 8f) continue;

                int bx = Mathf.FloorToInt(x);
                int bz = Mathf.FloorToInt(z);
                int solidH = world.GetSolidHeightAtWorldPos(bx, bz);
                if (solidH < 0) continue;

                // Check surface type — don't place trees on sand/water
                var surface = world.GetSolidSurfaceTypeAtWorldPos(bx, bz);
                if (surface == VoxelType.Sand || surface == VoxelType.Water) continue;

                float y = world.GetSmoothedHeightAtWorldPos(x, z);
                Vector3 pos = new Vector3(x, y, z);

                // Pick tree type based on biome
                CreateTree(pos, biome, rng, treeContainer.transform);
                placed++;
            }

            Debug.Log($"[TerrainDecorator] Placed {placed} trees ({biome})");
        }

        private void CreateTree(Vector3 pos, BiomeType biome, System.Random rng, Transform parent)
        {
            int variant = rng.Next(3);
            float height, trunkRadius, canopyRadius;
            Material canopyMat;
            bool useCone = false;

            switch (biome)
            {
                case BiomeType.Forest:
                    if (variant == 0) // Pine
                    {
                        height = 4f + (float)(rng.NextDouble() * 2f); // 4-6m
                        trunkRadius = 0.12f;
                        canopyRadius = 1.0f + (float)(rng.NextDouble() * 0.5f);
                        canopyMat = _pineCanopyMat;
                        useCone = true;
                    }
                    else if (variant == 1) // Oak
                    {
                        height = 3f + (float)(rng.NextDouble() * 2f); // 3-5m
                        trunkRadius = 0.18f;
                        canopyRadius = 1.2f + (float)(rng.NextDouble() * 0.6f);
                        canopyMat = _oakCanopyMat;
                    }
                    else // Birch
                    {
                        height = 2f + (float)(rng.NextDouble() * 2f); // 2-4m
                        trunkRadius = 0.08f;
                        canopyRadius = 0.7f + (float)(rng.NextDouble() * 0.4f);
                        canopyMat = _birchCanopyMat;
                    }
                    break;

                case BiomeType.Mountains:
                    // Small pines only
                    height = 2f + (float)(rng.NextDouble() * 1.5f); // 2-3.5m
                    trunkRadius = 0.08f;
                    canopyRadius = 0.6f + (float)(rng.NextDouble() * 0.3f);
                    canopyMat = _pineCanopyMat;
                    useCone = true;
                    break;

                default: // Coast
                    if (variant == 0 && pos.z < 40f) // Palm near shore
                    {
                        height = 3f + (float)(rng.NextDouble() * 2f);
                        trunkRadius = 0.10f;
                        canopyRadius = 1.0f + (float)(rng.NextDouble() * 0.4f);
                        canopyMat = _palmCanopyMat;
                    }
                    else // Regular small tree
                    {
                        height = 2f + (float)(rng.NextDouble() * 2f);
                        trunkRadius = 0.10f;
                        canopyRadius = 0.8f + (float)(rng.NextDouble() * 0.4f);
                        canopyMat = _oakCanopyMat;
                    }
                    break;
            }

            var tree = new GameObject("Tree");
            tree.transform.SetParent(parent, false);
            tree.transform.position = pos;

            // Slight random Y rotation
            tree.transform.rotation = Quaternion.Euler(0f, (float)(rng.NextDouble() * 360.0), 0f);

            // Trunk: brown cylinder
            float trunkHeight = height * 0.55f;
            var trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            trunk.name = "Trunk";
            trunk.transform.SetParent(tree.transform, false);
            trunk.transform.localPosition = new Vector3(0f, trunkHeight * 0.5f, 0f);
            trunk.transform.localScale = new Vector3(trunkRadius * 2f, trunkHeight * 0.5f, trunkRadius * 2f);
            Object.Destroy(trunk.GetComponent<Collider>());
            if (_trunkMat != null) trunk.GetComponent<MeshRenderer>().sharedMaterial = _trunkMat;

            // Canopy
            if (useCone)
            {
                // Pine cone canopy
                var canopy = new GameObject("Canopy");
                canopy.transform.SetParent(tree.transform, false);
                canopy.transform.localPosition = new Vector3(0f, trunkHeight * 0.6f, 0f);
                var mf = canopy.AddComponent<MeshFilter>();
                mf.sharedMesh = GetConeMesh();
                var mr = canopy.AddComponent<MeshRenderer>();
                if (canopyMat != null) mr.sharedMaterial = canopyMat;
                float coneH = height * 0.6f;
                canopy.transform.localScale = new Vector3(canopyRadius * 2f, coneH, canopyRadius * 2f);
            }
            else
            {
                // Sphere canopy
                var canopy = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                canopy.name = "Canopy";
                canopy.transform.SetParent(tree.transform, false);
                canopy.transform.localPosition = new Vector3(0f, trunkHeight + canopyRadius * 0.6f, 0f);
                float scaleY = canopyRadius * (0.6f + (float)(rng.NextDouble() * 0.3f));
                canopy.transform.localScale = new Vector3(canopyRadius * 2f, scaleY * 2f, canopyRadius * 2f);
                Object.Destroy(canopy.GetComponent<Collider>());
                if (canopyMat != null) canopy.GetComponent<MeshRenderer>().sharedMaterial = canopyMat;
            }

            // Add a small collider on the trunk for raycast selection (obstacle)
            var col = tree.AddComponent<CapsuleCollider>();
            col.center = new Vector3(0f, height * 0.4f, 0f);
            col.radius = trunkRadius * 2f;
            col.height = height * 0.7f;
        }

        // ═══════════════════════════════════════════════════════════
        //  ROCKS & BOULDERS
        // ═══════════════════════════════════════════════════════════

        private void SpawnRocks(WorldManager world, BiomeType biome, System.Random rng, Transform parent)
        {
            int count;
            switch (biome)
            {
                case BiomeType.Forest:    count = 20 + rng.Next(6);  break; // 20-25
                case BiomeType.Mountains: count = 30 + rng.Next(11); break; // 30-40
                case BiomeType.Coast:     count = 15 + rng.Next(6);  break; // 15-20
                default:                  count = 25; break;
            }

            var rockContainer = new GameObject("Rocks");
            rockContainer.transform.SetParent(parent, false);

            int placed = 0;
            int campX = world.CampfireBlockX;
            int campZ = world.CampfireBlockZ;

            for (int attempt = 0; attempt < count * 5 && placed < count; attempt++)
            {
                float x = 4f + (float)(rng.NextDouble() * (world.WorldBlocksX - 8));
                float z = 4f + (float)(rng.NextDouble() * (world.WorldBlocksZ - 8));

                float distToCamp = Mathf.Sqrt((x - campX) * (x - campX) + (z - campZ) * (z - campZ));
                if (distToCamp < 6f) continue;

                int bx = Mathf.FloorToInt(x);
                int bz = Mathf.FloorToInt(z);
                int solidH = world.GetSolidHeightAtWorldPos(bx, bz);
                if (solidH < 0) continue;

                var surface = world.GetSolidSurfaceTypeAtWorldPos(bx, bz);
                if (surface == VoxelType.Water) continue;

                float y = world.GetSmoothedHeightAtWorldPos(x, z);
                Vector3 pos = new Vector3(x, y, z);

                CreateRockFormation(pos, biome, rng, rockContainer.transform);
                placed++;
            }

            Debug.Log($"[TerrainDecorator] Placed {placed} rock formations ({biome})");
        }

        private void CreateRockFormation(Vector3 pos, BiomeType biome, System.Random rng, Transform parent)
        {
            float baseSize;
            int rockCount;

            switch (biome)
            {
                case BiomeType.Mountains:
                    baseSize = 0.8f + (float)(rng.NextDouble() * 2.0f); // 0.8-2.8m
                    rockCount = 1 + rng.Next(3); // 1-3 rocks per formation
                    break;
                case BiomeType.Coast:
                    baseSize = 0.4f + (float)(rng.NextDouble() * 0.8f); // Smooth rounded
                    rockCount = 1 + rng.Next(2);
                    break;
                default: // Forest
                    baseSize = 0.5f + (float)(rng.NextDouble() * 1.2f);
                    rockCount = 1 + rng.Next(3);
                    break;
            }

            var formation = new GameObject("RockFormation");
            formation.transform.SetParent(parent, false);
            formation.transform.position = pos;

            for (int i = 0; i < rockCount; i++)
            {
                var rock = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                rock.name = $"Rock_{i}";
                rock.transform.SetParent(formation.transform, false);

                float size = baseSize * (0.5f + (float)(rng.NextDouble() * 0.8f));
                float sx = size * (0.7f + (float)(rng.NextDouble() * 0.6f));
                float sy = size * (0.5f + (float)(rng.NextDouble() * 0.5f));
                float sz = size * (0.7f + (float)(rng.NextDouble() * 0.6f));

                rock.transform.localScale = new Vector3(sx, sy, sz);
                rock.transform.localPosition = new Vector3(
                    (float)(rng.NextDouble() - 0.5) * baseSize * 0.8f,
                    sy * 0.3f,
                    (float)(rng.NextDouble() - 0.5) * baseSize * 0.8f);
                rock.transform.localRotation = Quaternion.Euler(
                    (float)(rng.NextDouble() * 20f),
                    (float)(rng.NextDouble() * 360f),
                    (float)(rng.NextDouble() * 20f));

                Object.Destroy(rock.GetComponent<Collider>());

                Material mat = (rng.Next(3) == 0) ? _darkRockMat : _rockMat;
                if (mat != null) rock.GetComponent<MeshRenderer>().sharedMaterial = mat;
            }
        }

        // ═══════════════════════════════════════════════════════════
        //  BUSHES & VEGETATION
        // ═══════════════════════════════════════════════════════════

        private void SpawnBushes(WorldManager world, BiomeType biome, System.Random rng, Transform parent)
        {
            int count;
            switch (biome)
            {
                case BiomeType.Forest:    count = 40 + rng.Next(11); break; // 40-50
                case BiomeType.Mountains: count = 15 + rng.Next(6);  break; // 15-20
                case BiomeType.Coast:     count = 25 + rng.Next(11); break; // 25-35
                default:                  count = 35; break;
            }

            var bushContainer = new GameObject("Bushes");
            bushContainer.transform.SetParent(parent, false);

            int placed = 0;
            int campX = world.CampfireBlockX;
            int campZ = world.CampfireBlockZ;

            for (int attempt = 0; attempt < count * 4 && placed < count; attempt++)
            {
                float x = 4f + (float)(rng.NextDouble() * (world.WorldBlocksX - 8));
                float z = 4f + (float)(rng.NextDouble() * (world.WorldBlocksZ - 8));

                float distToCamp = Mathf.Sqrt((x - campX) * (x - campX) + (z - campZ) * (z - campZ));
                if (distToCamp < 5f) continue;

                int bx = Mathf.FloorToInt(x);
                int bz = Mathf.FloorToInt(z);
                int solidH = world.GetSolidHeightAtWorldPos(bx, bz);
                if (solidH < 0) continue;

                var surface = world.GetSolidSurfaceTypeAtWorldPos(bx, bz);
                if (surface == VoxelType.Water) continue;

                float y = world.GetSmoothedHeightAtWorldPos(x, z);
                Vector3 pos = new Vector3(x, y, z);

                bool nearWater = surface == VoxelType.Sand;

                // Coast: reeds near water
                if (biome == BiomeType.Coast && nearWater && rng.Next(2) == 0)
                    CreateReedCluster(pos, rng, bushContainer.transform);
                else
                    CreateBush(pos, rng, bushContainer.transform);

                placed++;
            }

            Debug.Log($"[TerrainDecorator] Placed {placed} bushes/vegetation ({biome})");
        }

        private void CreateBush(Vector3 pos, System.Random rng, Transform parent)
        {
            float size = 0.3f + (float)(rng.NextDouble() * 0.5f); // 0.3-0.8m
            bool hasBerries = rng.Next(5) == 0; // 20% chance berry bush

            var bush = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            bush.name = hasBerries ? "BerryBush" : "Bush";
            bush.transform.SetParent(parent, false);
            bush.transform.position = pos + new Vector3(0f, size * 0.4f, 0f);
            float sx = size * (0.8f + (float)(rng.NextDouble() * 0.4f));
            float sy = size * (0.6f + (float)(rng.NextDouble() * 0.3f));
            float sz = size * (0.8f + (float)(rng.NextDouble() * 0.4f));
            bush.transform.localScale = new Vector3(sx * 2f, sy * 2f, sz * 2f);
            Object.Destroy(bush.GetComponent<Collider>());

            Material mat = hasBerries ? _berryBushMat : _bushMat;
            if (mat != null) bush.GetComponent<MeshRenderer>().sharedMaterial = mat;

            // Berry dots
            if (hasBerries)
            {
                for (int i = 0; i < 4; i++)
                {
                    var berry = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    berry.name = "Berry";
                    berry.transform.SetParent(bush.transform, false);
                    berry.transform.localPosition = new Vector3(
                        (float)(rng.NextDouble() - 0.5) * 0.6f,
                        (float)(rng.NextDouble() * 0.3f) + 0.2f,
                        (float)(rng.NextDouble() - 0.5) * 0.6f);
                    berry.transform.localScale = new Vector3(0.12f, 0.12f, 0.12f);
                    Object.Destroy(berry.GetComponent<Collider>());
                    var berryMr = berry.GetComponent<MeshRenderer>();
                    if (berryMr != null)
                    {
                        // Emissive red berries — visible from distance
                        berryMr.sharedMaterial = TerrainShaderLibrary.CreateEmissivePropMaterial(
                            "Berry_Emissive", new Color(0.85f, 0.15f, 0.15f),
                            new Color(0.3f, 0.02f, 0.02f), 0.4f);
                    }
                }
            }
        }

        private void CreateReedCluster(Vector3 pos, System.Random rng, Transform parent)
        {
            var cluster = new GameObject("ReedCluster");
            cluster.transform.SetParent(parent, false);
            cluster.transform.position = pos;

            int reeds = 3 + rng.Next(3); // 3-5 reeds
            for (int i = 0; i < reeds; i++)
            {
                var reed = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                reed.name = $"Reed_{i}";
                reed.transform.SetParent(cluster.transform, false);
                float h = 0.6f + (float)(rng.NextDouble() * 0.6f);
                reed.transform.localScale = new Vector3(0.03f, h * 0.5f, 0.03f);
                reed.transform.localPosition = new Vector3(
                    (float)(rng.NextDouble() - 0.5) * 0.4f,
                    h * 0.5f,
                    (float)(rng.NextDouble() - 0.5) * 0.4f);
                // Slight tilt
                reed.transform.localRotation = Quaternion.Euler(
                    (float)(rng.NextDouble() * 10f - 5f), 0f,
                    (float)(rng.NextDouble() * 10f - 5f));
                Object.Destroy(reed.GetComponent<Collider>());
                if (_reedMat != null) reed.GetComponent<MeshRenderer>().sharedMaterial = _reedMat;
            }
        }

        // ═══════════════════════════════════════════════════════════
        //  GROUND VARIATION
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

                float y = world.GetSmoothedHeightAtWorldPos(x, z) + 0.02f; // Slightly above terrain

                // Dirt paths near campfire
                float distToCamp = Mathf.Sqrt((x - campX) * (x - campX) + (z - campZ) * (z - campZ));
                Material patchMat;

                if (distToCamp < 12f && rng.Next(2) == 0)
                    patchMat = _groundDirtMat; // Worn ground near camp
                else if (surface == VoxelType.Sand)
                    patchMat = _groundSandMat;
                else if (biome == BiomeType.Mountains && rng.Next(2) == 0)
                    patchMat = _groundRockyMat;
                else
                    patchMat = _groundDarkGrassMat;

                float patchSize = 1.5f + (float)(rng.NextDouble() * 3.0f); // 1.5-4.5m

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
        //  NATURAL SHELTERS
        // ═══════════════════════════════════════════════════════════

        private void SpawnShelters(WorldManager world, BiomeType biome, System.Random rng, Transform parent)
        {
            var shelterContainer = new GameObject("NaturalShelters");
            shelterContainer.transform.SetParent(parent, false);

            int campX = world.CampfireBlockX;
            int campZ = world.CampfireBlockZ;

            // Rock overhangs — all biomes, 2-3 per map
            int overhangs = 2 + rng.Next(2);
            for (int i = 0; i < overhangs; i++)
            {
                Vector3 pos = FindShelterPosition(world, rng, campX, campZ, 15f, 50f);
                if (pos != Vector3.zero)
                    CreateRockOverhang(pos, rng, shelterContainer.transform);
            }

            // Biome-specific shelters
            if (biome == BiomeType.Mountains)
            {
                // Cave entrance — 1 per map
                Vector3 cavePos = FindShelterPosition(world, rng, campX, campZ, 20f, 55f);
                if (cavePos != Vector3.zero)
                    CreateCaveEntrance(cavePos, rng, shelterContainer.transform);
            }

            if (biome == BiomeType.Forest)
            {
                // Dense thickets — 2-3 per map
                int thickets = 2 + rng.Next(2);
                for (int i = 0; i < thickets; i++)
                {
                    Vector3 pos = FindShelterPosition(world, rng, campX, campZ, 12f, 45f);
                    if (pos != Vector3.zero)
                        CreateDenseThicket(pos, rng, shelterContainer.transform);
                }
            }
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

        private void CreateRockOverhang(Vector3 pos, System.Random rng, Transform parent)
        {
            var shelter = new GameObject("RockOverhang");
            shelter.transform.SetParent(parent, false);
            shelter.transform.position = pos;
            shelter.transform.rotation = Quaternion.Euler(0f, (float)(rng.NextDouble() * 360.0), 0f);

            // Large tilted rock slab
            var slab = GameObject.CreatePrimitive(PrimitiveType.Cube);
            slab.name = "Slab";
            slab.transform.SetParent(shelter.transform, false);
            slab.transform.localScale = new Vector3(3f, 0.4f, 2.5f);
            slab.transform.localPosition = new Vector3(0f, 1.6f, 0f);
            slab.transform.localRotation = Quaternion.Euler(12f, 0f, 5f);
            Object.Destroy(slab.GetComponent<Collider>());
            if (_shelterRockMat != null) slab.GetComponent<MeshRenderer>().sharedMaterial = _shelterRockMat;

            // Support pillar
            var pillar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pillar.name = "Pillar";
            pillar.transform.SetParent(shelter.transform, false);
            pillar.transform.localScale = new Vector3(0.5f, 0.9f, 0.5f);
            pillar.transform.localPosition = new Vector3(-0.8f, 0.9f, -0.5f);
            Object.Destroy(pillar.GetComponent<Collider>());
            if (_shelterRockMat != null) pillar.GetComponent<MeshRenderer>().sharedMaterial = _shelterRockMat;

            // Small decorative rocks at base
            for (int i = 0; i < 3; i++)
            {
                var rock = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                rock.name = $"BaseRock_{i}";
                rock.transform.SetParent(shelter.transform, false);
                float rs = 0.3f + (float)(rng.NextDouble() * 0.4f);
                rock.transform.localScale = new Vector3(rs, rs * 0.7f, rs);
                rock.transform.localPosition = new Vector3(
                    (float)(rng.NextDouble() - 0.5) * 2f, rs * 0.3f,
                    (float)(rng.NextDouble() - 0.5) * 2f);
                Object.Destroy(rock.GetComponent<Collider>());
                if (_darkRockMat != null) rock.GetComponent<MeshRenderer>().sharedMaterial = _darkRockMat;
            }

            // Collider for selection
            var col = shelter.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 1f, 0f);
            col.size = new Vector3(3.5f, 2f, 3f);

            // NaturalShelter component
            var ns = shelter.AddComponent<NaturalShelter>();
            ns.ShelterName = "Rock Overhang";
            ns.ShelterType = "Rock Overhang";
            ns.Capacity = 3;
            ns.ProtectionValue = 0.8f;
        }

        private void CreateCaveEntrance(Vector3 pos, System.Random rng, Transform parent)
        {
            var shelter = new GameObject("CaveEntrance");
            shelter.transform.SetParent(parent, false);
            shelter.transform.position = pos;
            shelter.transform.rotation = Quaternion.Euler(0f, (float)(rng.NextDouble() * 360.0), 0f);

            // Archway rocks
            var leftWall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            leftWall.name = "LeftWall";
            leftWall.transform.SetParent(shelter.transform, false);
            leftWall.transform.localScale = new Vector3(0.8f, 2.5f, 2f);
            leftWall.transform.localPosition = new Vector3(-1.3f, 1.2f, 0f);
            Object.Destroy(leftWall.GetComponent<Collider>());
            if (_darkRockMat != null) leftWall.GetComponent<MeshRenderer>().sharedMaterial = _darkRockMat;

            var rightWall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            rightWall.name = "RightWall";
            rightWall.transform.SetParent(shelter.transform, false);
            rightWall.transform.localScale = new Vector3(0.8f, 2.5f, 2f);
            rightWall.transform.localPosition = new Vector3(1.3f, 1.2f, 0f);
            Object.Destroy(rightWall.GetComponent<Collider>());
            if (_darkRockMat != null) rightWall.GetComponent<MeshRenderer>().sharedMaterial = _darkRockMat;

            // Top slab
            var roof = GameObject.CreatePrimitive(PrimitiveType.Cube);
            roof.name = "Roof";
            roof.transform.SetParent(shelter.transform, false);
            roof.transform.localScale = new Vector3(3.5f, 0.6f, 2.5f);
            roof.transform.localPosition = new Vector3(0f, 2.5f, 0f);
            Object.Destroy(roof.GetComponent<Collider>());
            if (_shelterRockMat != null) roof.GetComponent<MeshRenderer>().sharedMaterial = _shelterRockMat;

            // Dark interior (back wall)
            var back = GameObject.CreatePrimitive(PrimitiveType.Cube);
            back.name = "Interior";
            back.transform.SetParent(shelter.transform, false);
            back.transform.localScale = new Vector3(2f, 2f, 0.3f);
            back.transform.localPosition = new Vector3(0f, 1f, -0.9f);
            Object.Destroy(back.GetComponent<Collider>());
            if (_shelterCaveMat != null) back.GetComponent<MeshRenderer>().sharedMaterial = _shelterCaveMat;

            // Collider for selection
            var col = shelter.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 1.3f, 0f);
            col.size = new Vector3(3.5f, 2.8f, 2.5f);

            // NaturalShelter component
            var ns = shelter.AddComponent<NaturalShelter>();
            ns.ShelterName = "Cave Entrance";
            ns.ShelterType = "Cave Entrance";
            ns.Capacity = 4;
            ns.ProtectionValue = 1.0f;
        }

        private void CreateDenseThicket(Vector3 pos, System.Random rng, Transform parent)
        {
            var shelter = new GameObject("DenseThicket");
            shelter.transform.SetParent(parent, false);
            shelter.transform.position = pos;

            // Cluster of overlapping bush spheres
            int bushCount = 5 + rng.Next(4); // 5-8 bushes
            for (int i = 0; i < bushCount; i++)
            {
                var bush = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                bush.name = $"ThicketBush_{i}";
                bush.transform.SetParent(shelter.transform, false);
                float s = 0.6f + (float)(rng.NextDouble() * 0.5f);
                bush.transform.localScale = new Vector3(s * 1.3f, s, s * 1.3f);
                bush.transform.localPosition = new Vector3(
                    (float)(rng.NextDouble() - 0.5) * 2f,
                    s * 0.4f,
                    (float)(rng.NextDouble() - 0.5) * 2f);
                Object.Destroy(bush.GetComponent<Collider>());
                if (_shelterThicketMat != null)
                    bush.GetComponent<MeshRenderer>().sharedMaterial = _shelterThicketMat;
            }

            // Collider for selection
            var col = shelter.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.6f, 0f);
            col.size = new Vector3(3f, 1.5f, 3f);

            // NaturalShelter component
            var ns = shelter.AddComponent<NaturalShelter>();
            ns.ShelterName = "Dense Thicket";
            ns.ShelterType = "Dense Thicket";
            ns.Capacity = 2;
            ns.ProtectionValue = 0.6f;
        }

        // ═══════════════════════════════════════════════════════════
        //  WATER FEATURES
        // ═══════════════════════════════════════════════════════════

        private void SpawnOceanPlane(WorldManager world, Transform parent)
        {
            // Coast biome: large water plane along Z=0 edge
            float oceanWidth = world.WorldBlocksX;
            float oceanDepth = 30f; // 30 blocks deep from edge
            float seaY = 64.15f; // Slightly above sea level for visual

            var ocean = GameObject.CreatePrimitive(PrimitiveType.Quad);
            ocean.name = "OceanPlane";
            ocean.tag = "Water";
            ocean.transform.SetParent(parent, false);
            ocean.transform.position = new Vector3(oceanWidth * 0.5f, seaY, oceanDepth * 0.5f);
            ocean.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            ocean.transform.localScale = new Vector3(oceanWidth + 20f, oceanDepth + 10f, 1f);
            Object.Destroy(ocean.GetComponent<Collider>());

            if (_waterPlaneMat != null)
                ocean.GetComponent<MeshRenderer>().sharedMaterial = _waterPlaneMat;

            Debug.Log("[TerrainDecorator] Placed ocean plane for coast biome");
        }

        // ═══════════════════════════════════════════════════════════
        //  HELPERS
        // ═══════════════════════════════════════════════════════════

        private static Mesh GetConeMesh()
        {
            if (_coneMesh != null) return _coneMesh;
            _coneMesh = CreateConeMesh(0.5f, 1f, 8);
            return _coneMesh;
        }

        private static Mesh CreateConeMesh(float radius, float height, int segments)
        {
            var mesh = new Mesh();
            int vertCount = segments + 2;
            var verts = new Vector3[vertCount];
            var normals = new Vector3[vertCount];

            verts[0] = Vector3.zero;
            normals[0] = Vector3.down;

            for (int i = 0; i < segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                float x = Mathf.Cos(a) * radius;
                float z = Mathf.Sin(a) * radius;
                verts[i + 1] = new Vector3(x, 0f, z);
                Vector3 outward = new Vector3(x, 0f, z).normalized;
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
    }
}
