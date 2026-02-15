using UnityEngine;
using Terranova.Core;
using Terranova.Terrain;

namespace Terranova.Buildings
{
    /// <summary>
    /// Spawns natural shelters on terrain based on biome.
    /// Feature 5.1: Terrain-Generated Shelters.
    ///
    /// Caves: Mountains biome (rare).
    /// Rock overhangs: Near hills/slopes.
    /// Dense undergrowth: Forest biome.
    /// Fallen trees: Forest biome.
    ///
    /// Guaranteed: at least 1 shelter within 40 blocks of spawn.
    /// </summary>
    public class ShelterSpawner : MonoBehaviour
    {
        private bool _hasSpawned;
        private static Material _sharedShelterMat;

        private void Update()
        {
            if (_hasSpawned) return;

            var world = WorldManager.Instance;
            if (world == null || world.WorldBlocksX == 0 || !world.IsNavMeshReady) return;

            var sm = ShelterManager.Instance;
            if (sm == null) return;

            _hasSpawned = true;
            SpawnShelters(world, sm);
            enabled = false;
        }

        private void SpawnShelters(WorldManager world, ShelterManager sm)
        {
            int worldX = world.WorldBlocksX;
            int worldZ = world.WorldBlocksZ;
            int centerX = worldX / 2;
            int centerZ = worldZ / 2;

            var biome = GameState.SelectedBiome;
            var rng = new System.Random(GameState.Seed + 9999);

            int shelterCount = 0;
            bool hasNearSpawn = false;

            // Spawn biome-appropriate shelters
            int targetCount = biome switch
            {
                BiomeType.Forest => rng.Next(8, 14),
                BiomeType.Mountains => rng.Next(5, 10),
                BiomeType.Coast => rng.Next(4, 8),
                _ => 6
            };

            int attempts = 0;
            int maxAttempts = targetCount * 20;

            while (shelterCount < targetCount && attempts < maxAttempts)
            {
                attempts++;

                int x = rng.Next(4, worldX - 4);
                int z = rng.Next(4, worldZ - 4);
                int h = world.GetHeightAtWorldPos(x, z);
                if (h < 1) continue;

                var surface = world.GetSurfaceTypeAtWorldPos(x, z);
                if (!surface.IsSolid()) continue;

                NaturalShelterType? type = PickShelterType(biome, surface, rng);
                if (type == null) continue;

                float y = world.GetSmoothedHeightAtWorldPos(x + 0.5f, z + 0.5f);
                var pos = new Vector3(x + 0.5f, y, z + 0.5f);

                var shelter = CreateShelterObject(type.Value, pos);
                sm.Register(shelter);
                shelterCount++;

                float distToCenter = Vector2.Distance(
                    new Vector2(x, z), new Vector2(centerX, centerZ));
                if (distToCenter <= 40f)
                    hasNearSpawn = true;
            }

            // Guarantee at least 1 shelter within 40 blocks of spawn
            if (!hasNearSpawn && shelterCount > 0)
            {
                float angle = (float)(rng.NextDouble() * Mathf.PI * 2f);
                float radius = 20f + (float)rng.NextDouble() * 15f;
                int sx = centerX + Mathf.RoundToInt(Mathf.Cos(angle) * radius);
                int sz = centerZ + Mathf.RoundToInt(Mathf.Sin(angle) * radius);
                sx = Mathf.Clamp(sx, 4, worldX - 4);
                sz = Mathf.Clamp(sz, 4, worldZ - 4);

                int sh = world.GetHeightAtWorldPos(sx, sz);
                if (sh >= 1)
                {
                    float sy = world.GetSmoothedHeightAtWorldPos(sx + 0.5f, sz + 0.5f);
                    var pos = new Vector3(sx + 0.5f, sy, sz + 0.5f);

                    NaturalShelterType startType = biome == BiomeType.Mountains
                        ? NaturalShelterType.RockOverhang
                        : NaturalShelterType.DenseUndergrowth;

                    var shelter = CreateShelterObject(startType, pos);
                    sm.Register(shelter);
                    shelterCount++;
                }
            }

            Debug.Log($"ShelterSpawner: Spawned {shelterCount} natural shelters for {biome} biome.");
        }

        private static NaturalShelterType? PickShelterType(
            BiomeType biome, VoxelType surface, System.Random rng)
        {
            switch (biome)
            {
                case BiomeType.Forest:
                    if (surface == VoxelType.Grass)
                    {
                        int roll = rng.Next(100);
                        if (roll < 45) return NaturalShelterType.DenseUndergrowth;
                        if (roll < 85) return NaturalShelterType.FallenTree;
                        return NaturalShelterType.RockOverhang;
                    }
                    break;

                case BiomeType.Mountains:
                    if (surface == VoxelType.Stone)
                    {
                        int roll = rng.Next(100);
                        if (roll < 20) return NaturalShelterType.Cave;
                        if (roll < 70) return NaturalShelterType.RockOverhang;
                        return null;
                    }
                    if (surface == VoxelType.Grass)
                    {
                        return rng.Next(100) < 30 ? NaturalShelterType.FallenTree : null;
                    }
                    break;

                case BiomeType.Coast:
                    if (surface == VoxelType.Grass)
                    {
                        int roll = rng.Next(100);
                        if (roll < 40) return NaturalShelterType.DenseUndergrowth;
                        if (roll < 70) return NaturalShelterType.FallenTree;
                        return null;
                    }
                    break;
            }

            return null;
        }

        private NaturalShelter CreateShelterObject(NaturalShelterType type, Vector3 position)
        {
            var go = new GameObject($"Shelter_{type}");
            go.transform.position = position;

            var shelter = go.AddComponent<NaturalShelter>();
            shelter.Initialize(type);

            // Visual: colored primitive
            CreateShelterVisual(go, type);

            // Collider for selection
            var col = go.AddComponent<BoxCollider>();
            col.isTrigger = true;
            col.center = new Vector3(0f, 0.5f, 0f);
            col.size = GetColliderSize(type);

            return shelter;
        }

        private static void CreateShelterVisual(GameObject parent, NaturalShelterType type)
        {
            EnsureMaterial();

            Color color;
            Vector3 scale;
            PrimitiveType primType;

            switch (type)
            {
                case NaturalShelterType.Cave:
                    color = new Color(0.35f, 0.30f, 0.28f);
                    scale = new Vector3(2f, 1.5f, 2f);
                    primType = PrimitiveType.Sphere;
                    break;
                case NaturalShelterType.RockOverhang:
                    color = new Color(0.50f, 0.48f, 0.45f);
                    scale = new Vector3(1.8f, 0.6f, 1.2f);
                    primType = PrimitiveType.Cube;
                    break;
                case NaturalShelterType.DenseUndergrowth:
                    color = new Color(0.20f, 0.45f, 0.15f);
                    scale = new Vector3(1.5f, 1.0f, 1.5f);
                    primType = PrimitiveType.Sphere;
                    break;
                case NaturalShelterType.FallenTree:
                    color = new Color(0.45f, 0.30f, 0.15f);
                    scale = new Vector3(0.5f, 0.5f, 3.0f);
                    primType = PrimitiveType.Cylinder;
                    break;
                default:
                    return;
            }

            var visual = GameObject.CreatePrimitive(primType);
            visual.name = "Visual";
            visual.transform.SetParent(parent.transform, false);
            visual.transform.localScale = scale;
            visual.transform.localPosition = new Vector3(0f, scale.y * 0.5f, 0f);

            if (type == NaturalShelterType.FallenTree)
            {
                visual.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                visual.transform.localPosition = new Vector3(0f, 0.3f, 0f);
            }

            var meshCol = visual.GetComponent<Collider>();
            if (meshCol != null) Object.Destroy(meshCol);

            var renderer = visual.GetComponent<MeshRenderer>();
            if (renderer != null && _sharedShelterMat != null)
            {
                renderer.sharedMaterial = _sharedShelterMat;
                var pb = new MaterialPropertyBlock();
                pb.SetColor(Shader.PropertyToID("_BaseColor"), color);
                renderer.SetPropertyBlock(pb);
            }
        }

        private static Vector3 GetColliderSize(NaturalShelterType type) => type switch
        {
            NaturalShelterType.Cave => new Vector3(2.5f, 2f, 2.5f),
            NaturalShelterType.RockOverhang => new Vector3(2f, 1f, 1.5f),
            NaturalShelterType.DenseUndergrowth => new Vector3(2f, 1.5f, 2f),
            NaturalShelterType.FallenTree => new Vector3(1f, 1f, 3.5f),
            _ => Vector3.one
        };

        private static void EnsureMaterial()
        {
            if (_sharedShelterMat != null) return;

            Shader shader = Shader.Find("Universal Render Pipeline/Lit")
                         ?? Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader != null)
            {
                _sharedShelterMat = new Material(shader);
                _sharedShelterMat.name = "Shelter_Shared (Auto)";
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _sharedShelterMat = null;
        }
    }
}
