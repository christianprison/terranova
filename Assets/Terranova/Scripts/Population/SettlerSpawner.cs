using UnityEngine;
using Terranova.Core;
using Terranova.Terrain;

namespace Terranova.Population
{
    /// <summary>
    /// Spawns the initial settlers around the campfire at game start.
    ///
    /// Flow:
    ///   1. Wait for WorldManager to finish terrain generation
    ///   2. Place campfire at world center
    ///   3. Spawn 5 settlers in a circle around the campfire
    ///   4. Fire PopulationChangedEvent so the UI updates
    ///
    /// The settlers are placed at ~3 block radius from the campfire,
    /// evenly spaced in a circle. Each gets a unique color.
    /// </summary>
    public class SettlerSpawner : MonoBehaviour
    {
        [Header("Spawn Settings")]
        [Tooltip("Number of settlers to spawn at game start.")]
        [SerializeField] private int _initialSettlerCount = 5;

        [Tooltip("Radius (in blocks) around the campfire where settlers spawn.")]
        [SerializeField] private float _spawnRadius = 3f;

        // Track whether we've already spawned (to avoid double-spawning)
        private bool _hasSpawned;

        // We wait for WorldManager in Update because terrain generates in Start
        // and we can't guarantee execution order between different Start methods.
        private void Update()
        {
            if (_hasSpawned)
                return;

            // Wait until terrain is ready
            var world = WorldManager.Instance;
            if (world == null || world.WorldBlocksX == 0)
                return;

            _hasSpawned = true;
            SpawnSettlement(world);

            // Disable Update after spawning (no per-frame cost)
            enabled = false;
        }

        /// <summary>
        /// Place the campfire and spawn settlers around it.
        /// </summary>
        private void SpawnSettlement(WorldManager world)
        {
            // Place campfire at world center
            Vector3 campfirePos = PlaceCampfire(world);

            // Spawn settlers in a circle around the campfire
            SpawnSettlers(world, campfirePos);

            Debug.Log($"SettlerSpawner: Placed campfire and {_initialSettlerCount} settlers " +
                      $"at world center ({campfirePos.x:F0}, {campfirePos.z:F0}).");
        }

        /// <summary>
        /// Create the campfire at the center of the world.
        /// Returns the world position where it was placed.
        /// </summary>
        private Vector3 PlaceCampfire(WorldManager world)
        {
            int centerX = world.WorldBlocksX / 2;
            int centerZ = world.WorldBlocksZ / 2;
            int height = world.GetHeightAtWorldPos(centerX, centerZ);

            // Fallback: if center is water/air, search nearby for solid ground
            if (height < 0 || !world.GetSurfaceTypeAtWorldPos(centerX, centerZ).IsSolid())
            {
                FindNearestSolidGround(world, ref centerX, ref centerZ, ref height);
            }

            // Use smooth mesh height for visual positioning (Story 0.6)
            float y = world.GetSmoothedHeightAtWorldPos(centerX + 0.5f, centerZ + 0.5f);
            Vector3 position = new Vector3(centerX + 0.5f, y, centerZ + 0.5f);

            // Create campfire visual (same style as BuildingPlacer for consistency)
            var campfire = GameObject.CreatePrimitive(PrimitiveType.Cube);
            campfire.name = "Campfire";
            campfire.transform.position = position;
            campfire.transform.localScale = new Vector3(1f, 1f, 1f);

            // Apply warm yellow color
            var meshRenderer = campfire.GetComponent<MeshRenderer>();
            Shader shader = Shader.Find("Universal Render Pipeline/Lit")
                         ?? Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader != null)
            {
                var material = new Material(shader);
                material.name = "Campfire_Material (Auto)";
                material.SetColor("_BaseColor", new Color(1f, 0.8f, 0.2f));
                meshRenderer.material = material;
            }

            // Note: No BuildingPlacedEvent here. The starting campfire is free
            // (Story 4.3: "Lagerfeuer existiert bei Spielstart bereits").
            // Future player-built campfires will go through BuildingPlacer with costs.

            return position;
        }

        /// <summary>
        /// Spawn settlers evenly distributed in a circle around the campfire.
        /// Each settler snaps to the terrain surface independently.
        /// </summary>
        private void SpawnSettlers(WorldManager world, Vector3 campfirePos)
        {
            int count = _initialSettlerCount;
            float angleStep = 360f / count;

            for (int i = 0; i < count; i++)
            {
                float angle = i * angleStep * Mathf.Deg2Rad;
                float x = campfirePos.x + Mathf.Cos(angle) * _spawnRadius;
                float z = campfirePos.z + Mathf.Sin(angle) * _spawnRadius;

                int blockX = Mathf.FloorToInt(x);
                int blockZ = Mathf.FloorToInt(z);
                int height = world.GetHeightAtWorldPos(blockX, blockZ);

                if (height < 0)
                {
                    Debug.LogWarning($"Settler {i}: No valid terrain at ({blockX}, {blockZ}), skipping.");
                    continue;
                }

                var settlerObj = new GameObject($"Settler_{i}");
                settlerObj.transform.position = new Vector3(x, 0f, z);

                var settler = settlerObj.AddComponent<Settler>();
                settler.Initialize(i, campfirePos);
            }

            EventBus.Publish(new PopulationChangedEvent
            {
                CurrentPopulation = count
            });
        }

        /// <summary>
        /// Search in a spiral outward from the center to find solid ground.
        /// Used when the exact world center is water or otherwise unbuildable.
        /// </summary>
        private void FindNearestSolidGround(WorldManager world, ref int x, ref int z, ref int height)
        {
            // Search in expanding rings up to 16 blocks away
            for (int radius = 1; radius <= 16; radius++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    for (int dz = -radius; dz <= radius; dz++)
                    {
                        // Only check the ring edge (not the filled area)
                        if (Mathf.Abs(dx) != radius && Mathf.Abs(dz) != radius)
                            continue;

                        int testX = x + dx;
                        int testZ = z + dz;
                        int testHeight = world.GetHeightAtWorldPos(testX, testZ);

                        if (testHeight >= 0 && world.GetSurfaceTypeAtWorldPos(testX, testZ).IsSolid())
                        {
                            x = testX;
                            z = testZ;
                            height = testHeight;
                            return;
                        }
                    }
                }
            }

            Debug.LogWarning("SettlerSpawner: Could not find solid ground near world center!");
        }
    }
}
