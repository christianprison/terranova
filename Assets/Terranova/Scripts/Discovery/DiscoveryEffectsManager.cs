using UnityEngine;
using Terranova.Core;
using Terranova.Buildings;
using Terranova.Resources;
using Terranova.Terrain;

namespace Terranova.Discovery
{
    /// <summary>
    /// Applies gameplay effects when discoveries are made.
    ///
    /// Feature 3: Discoveries change gameplay.
    /// 3.1 Capabilities: Fire = food decay -50%, Improved Tools = gather speed +30%.
    /// 3.2 Buildings: Cooking Fire after Fire, Trap Site after Animal Traps.
    /// 3.3 Resources: Resin spawns in forest after discovery, Flint in mountains.
    /// </summary>
    public class DiscoveryEffectsManager : MonoBehaviour
    {
        // Resource spawning counts after discovery
        private const int RESIN_SPAWN_COUNT = 15;
        private const int FLINT_SPAWN_COUNT = 20;
        private const int PLANT_FIBER_SPAWN_COUNT = 12;

        public static DiscoveryEffectsManager Instance { get; private set; }

        // Gameplay modifiers — other systems query these
        private float _foodDecayMultiplier = 1f;
        private float _gatherSpeedMultiplier = 1f;

        /// <summary>Multiplier for food decay rate (1.0 = normal, 0.5 = halved).</summary>
        public float FoodDecayMultiplier => _foodDecayMultiplier;

        /// <summary>Multiplier for gathering speed (1.0 = normal, 1.3 = 30% faster).</summary>
        public float GatherSpeedMultiplier => _gatherSpeedMultiplier;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnEnable()
        {
            EventBus.Subscribe<DiscoveryMadeEvent>(OnDiscoveryMade);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<DiscoveryMadeEvent>(OnDiscoveryMade);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void OnDiscoveryMade(DiscoveryMadeEvent evt)
        {
            var sm = DiscoveryStateManager.Instance;
            if (sm == null) return;

            // 3.1 Capabilities
            if (sm.HasCapability("fire") && _foodDecayMultiplier > 0.5f)
            {
                _foodDecayMultiplier = 0.5f;
                GameplayModifiers.FoodDecayMultiplier = 0.5f;
                Debug.Log("[DiscoveryEffects] Fire discovered — food decay reduced by 50%.");
            }

            if (sm.HasCapability("improved_tools") && _gatherSpeedMultiplier < 1.3f)
            {
                _gatherSpeedMultiplier = 1.3f;
                GameplayModifiers.GatherSpeedMultiplier = 1.3f;
                Debug.Log("[DiscoveryEffects] Improved Tools — gather speed +30%.");
            }

            // 3.3 Resources: spawn new resource props after discovery
            switch (evt.DiscoveryName)
            {
                case "Resin & Glue":
                    SpawnDiscoveryResources(ResourceType.Resin, RESIN_SPAWN_COUNT, VoxelType.Grass);
                    break;
                case "Flint":
                    SpawnDiscoveryResources(ResourceType.Flint, FLINT_SPAWN_COUNT, VoxelType.Stone);
                    break;
                case "Primitive Cord":
                    SpawnDiscoveryResources(ResourceType.PlantFiber, PLANT_FIBER_SPAWN_COUNT, VoxelType.Grass);
                    break;
            }
        }

        /// <summary>
        /// Spawn new gatherable resource nodes on appropriate biome tiles.
        /// </summary>
        private void SpawnDiscoveryResources(ResourceType type, int count, VoxelType targetBiome)
        {
            var world = WorldManager.Instance;
            if (world == null) return;

            var parent = GameObject.Find("Resources");
            if (parent == null)
            {
                parent = new GameObject("Resources");
            }

            var rng = new System.Random(type.GetHashCode() + 42);
            int spawned = 0;
            int maxX = world.WorldBlocksX - 4;
            int maxZ = world.WorldBlocksZ - 4;

            for (int attempt = 0; attempt < count * 10 && spawned < count; attempt++)
            {
                float x = 4 + (float)(rng.NextDouble() * (maxX - 4));
                float z = 4 + (float)(rng.NextDouble() * (maxZ - 4));
                int blockX = Mathf.FloorToInt(x);
                int blockZ = Mathf.FloorToInt(z);

                VoxelType surface = world.GetSurfaceTypeAtWorldPos(blockX, blockZ);
                if (surface != targetBiome) continue;

                float y = world.GetSmoothedHeightAtWorldPos(x, z);

                var node = CreateResourceProp(type, new Vector3(x, y, z), parent.transform, rng);
                if (node != null) spawned++;
            }

            Debug.Log($"[DiscoveryEffects] Spawned {spawned} {type} resource nodes.");

            // Notify UI about new resources
            EventBus.Publish(new ResourceChangedEvent
            {
                ResourceName = type.ToString(),
                NewAmount = 0
            });
        }

        private GameObject CreateResourceProp(ResourceType type, Vector3 position, Transform parent, System.Random rng)
        {
            Shader shader = TerrainShaderLibrary.PropLit;

            switch (type)
            {
                case ResourceType.Resin:
                    return CreateResinProp(position, parent, shader, rng);
                case ResourceType.Flint:
                    return CreateFlintProp(position, parent, shader, rng);
                case ResourceType.PlantFiber:
                    return CreatePlantFiberProp(position, parent, shader, rng);
                default:
                    return null;
            }
        }

        private GameObject CreateResinProp(Vector3 position, Transform parent, Shader shader, System.Random rng)
        {
            var obj = new GameObject($"Resin_{parent.childCount}");
            obj.transform.SetParent(parent);
            obj.transform.position = position;

            // Amber-colored small blob
            var visual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            visual.name = "Body";
            visual.transform.SetParent(obj.transform, false);
            float scale = 0.2f + (float)rng.NextDouble() * 0.1f;
            visual.transform.localScale = new Vector3(scale, scale * 0.7f, scale);
            visual.transform.localPosition = new Vector3(0f, scale * 0.3f, 0f);

            if (shader != null)
            {
                var mat = new Material(shader);
                mat.SetColor("_BaseColor", new Color(0.85f, 0.55f, 0.10f, 0.9f));
                visual.GetComponent<MeshRenderer>().sharedMaterial = mat;
            }
            var col = visual.GetComponent<Collider>();
            if (col != null) col.isTrigger = true;

            var node = obj.AddComponent<ResourceNode>();
            node.Initialize(ResourceType.Resin);
            return obj;
        }

        private GameObject CreateFlintProp(Vector3 position, Transform parent, Shader shader, System.Random rng)
        {
            var obj = new GameObject($"Flint_{parent.childCount}");
            obj.transform.SetParent(parent);
            obj.transform.position = position;
            obj.transform.rotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);

            // Dark angular stone shard
            var visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.name = "Body";
            visual.transform.SetParent(obj.transform, false);
            float sx = 0.15f + (float)rng.NextDouble() * 0.1f;
            float sy = 0.08f + (float)rng.NextDouble() * 0.05f;
            float sz = 0.2f + (float)rng.NextDouble() * 0.1f;
            visual.transform.localScale = new Vector3(sx, sy, sz);
            visual.transform.localPosition = new Vector3(0f, sy * 0.5f, 0f);
            visual.transform.localRotation = Quaternion.Euler(
                (float)rng.NextDouble() * 15f, 0f, (float)rng.NextDouble() * 15f);

            if (shader != null)
            {
                var mat = new Material(shader);
                mat.SetColor("_BaseColor", new Color(0.20f, 0.20f, 0.22f));
                visual.GetComponent<MeshRenderer>().sharedMaterial = mat;
            }
            var col = visual.GetComponent<Collider>();
            if (col != null) col.isTrigger = true;

            var node = obj.AddComponent<ResourceNode>();
            node.Initialize(ResourceType.Flint);
            return obj;
        }

        private GameObject CreatePlantFiberProp(Vector3 position, Transform parent, Shader shader, System.Random rng)
        {
            var obj = new GameObject($"Fiber_{parent.childCount}");
            obj.transform.SetParent(parent);
            obj.transform.position = position;

            // Small green tuft
            var visual = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            visual.name = "Body";
            visual.transform.SetParent(obj.transform, false);
            float radius = 0.15f + (float)rng.NextDouble() * 0.1f;
            float height = 0.3f + (float)rng.NextDouble() * 0.15f;
            visual.transform.localScale = new Vector3(radius, height * 0.5f, radius);
            visual.transform.localPosition = new Vector3(0f, height * 0.4f, 0f);

            if (shader != null)
            {
                var mat = new Material(shader);
                mat.SetColor("_BaseColor", new Color(0.35f, 0.55f, 0.20f));
                visual.GetComponent<MeshRenderer>().sharedMaterial = mat;
            }
            var col = visual.GetComponent<Collider>();
            if (col != null) col.isTrigger = true;

            var node = obj.AddComponent<ResourceNode>();
            node.Initialize(ResourceType.PlantFiber);
            return obj;
        }
    }
}
