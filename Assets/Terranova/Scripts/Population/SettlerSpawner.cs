using UnityEngine;
using UnityEngine.AI;
using Terranova.Buildings;
using Terranova.Core;
using Terranova.Terrain;

namespace Terranova.Population
{
    /// <summary>
    /// Spawns the initial settlers around the campfire at game start.
    ///
    /// Flow:
    ///   1. WorldManager.PrepareSettlementArea() flattens campfire zone
    ///      during terrain generation (before mesh building + NavMesh bake).
    ///   2. This spawner waits for NavMesh to be ready.
    ///   3. Places campfire visual (prefab + fire particle) at the pre-determined position.
    ///   4. Brute-force spawns a freshwater pond 8-12 blocks from campfire.
    ///      NOT tied to terrain generation.
    ///   5. Spawns settlers (avatar prefabs) around the campfire.
    ///   6. Publishes progress 1.0 so the loading screen hides.
    ///
    /// The settlers are placed at ~3 block radius from the campfire,
    /// evenly spaced in a circle. Each gets a unique skin tone.
    /// </summary>
    public class SettlerSpawner : MonoBehaviour
    {
        [Header("Spawn Settings")]
        [Tooltip("Number of settlers to spawn at game start.")]
        [SerializeField] private int _initialSettlerCount = 5;

        [Tooltip("Radius (in blocks) around the campfire where settlers spawn.")]
        [SerializeField] private float _spawnRadius = 3f;

        private bool _hasSpawned;

        // We wait for WorldManager in Update because terrain generates in Start
        // and we can't guarantee execution order between different Start methods.
        private void Update()
        {
            if (_hasSpawned)
                return;

            // Wait until terrain, NavMesh, and settlement area are ready
            var world = WorldManager.Instance;
            if (world == null || world.WorldBlocksX == 0 || !world.IsNavMeshReady)
                return;

            // WorldManager must have prepared the campfire position
            if (world.CampfireBlockX == 0 && world.CampfireBlockZ == 0)
                return;

            _hasSpawned = true;
            SpawnSettlement(world);

            // Disable Update after spawning (no per-frame cost)
            enabled = false;
        }

        /// <summary>
        /// Place the campfire visual, spawn freshwater pond, then settlers.
        /// Terrain is already flattened by WorldManager.PrepareSettlementArea().
        /// Publishes progress 1.0 to dismiss the loading screen.
        /// </summary>
        private void SpawnSettlement(WorldManager world)
        {
            // Place campfire at the pre-determined position (terrain already flat)
            Vector3 campfirePos = PlaceCampfire(world);

            // Brute-force freshwater pond — NOT tied to terrain generation
            SpawnFreshwaterPond(world, campfirePos);

            // Progress: tribe arriving
            EventBus.Publish(new WorldGenerationProgressEvent
            {
                Progress = 0.99f,
                Status = "Your tribe arrives..."
            });

            // Spawn settlers in a circle around the campfire
            SpawnSettlers(world, campfirePos);

            // Dismiss loading screen — everything is ready
            EventBus.Publish(new WorldGenerationProgressEvent
            {
                Progress = 1f,
                Status = "Ready!"
            });

            Debug.Log($"SettlerSpawner: Placed campfire, water pond, and {_initialSettlerCount} settlers " +
                      $"at ({campfirePos.x:F0}, {campfirePos.z:F0}).");
        }

        /// <summary>
        /// v0.5.1: Respawn settlers for a new tribe at the existing campfire.
        /// Called when "New Tribe" is selected after game over.
        /// Campfire and water already exist — just spawn new settlers.
        /// </summary>
        public void RespawnSettlers()
        {
            var world = WorldManager.Instance;
            if (world == null) return;

            // Get campfire position from WorldManager (campfire always at these coords)
            float cx = world.CampfireBlockX + 0.5f;
            float cz = world.CampfireBlockZ + 0.5f;
            float cy = world.GetSmoothedHeightAtWorldPos(cx, cz);
            Vector3 campfirePos = new Vector3(cx, cy, cz);

            // Release all natural shelter claims from dead settlers
            NaturalShelter.ReleaseAllOccupants();

            SpawnSettlers(world, campfirePos);

            Debug.Log($"SettlerSpawner: Respawned {_initialSettlerCount} settlers for new tribe " +
                      $"(generation {GameState.TribeGeneration}).");
        }

        /// <summary>
        /// v0.5.2: Create the campfire using Explorer Stoneage Camp_Fire prefab + Fire_1A particle.
        /// Terrain is already flattened — no FlattenTerrain call needed.
        /// Returns the world position where it was placed.
        /// </summary>
        private Vector3 PlaceCampfire(WorldManager world)
        {
            int centerX = world.CampfireBlockX;
            int centerZ = world.CampfireBlockZ;

            float y = world.GetSmoothedHeightAtWorldPos(centerX + 0.5f, centerZ + 0.5f);
            Vector3 position = new Vector3(centerX + 0.5f, y, centerZ + 0.5f);

            // v0.5.2: Use Camp_Fire prefab from Explorer Stoneage
            var campfire = AssetPrefabRegistry.InstantiateRandom(
                AssetPrefabRegistry.CampFires, position,
                new System.Random(GameState.Seed + 100), null, 1.0f, 1.0f);

            if (campfire == null)
            {
                // Fallback: create minimal campfire if prefab missing
                campfire = new GameObject("Campfire");
                campfire.transform.position = position;
            }
            else
            {
                campfire.transform.SetParent(null);
                campfire.transform.position = position;
            }
            campfire.name = "Campfire";

            // v0.5.2: Add Fire_1A particle effect on top of campfire
            var fireFx = AssetPrefabRegistry.InstantiateSpecific(
                AssetPrefabRegistry.FireParticle,
                position + new Vector3(0f, 0.1f, 0f),
                Quaternion.identity, campfire.transform);
            if (fireFx != null) fireFx.name = "FireFX";

            // v0.5.2: Add Fireflies_1A near campfire (visible at night)
            var fireflyFx = AssetPrefabRegistry.InstantiateSpecific(
                AssetPrefabRegistry.FirefliesParticle,
                position + new Vector3(2f, 1f, 2f),
                Quaternion.identity, campfire.transform);
            if (fireflyFx != null) fireflyFx.name = "FirefliesFX";

            // Point light for campfire glow
            var lightObj = new GameObject("CampfireLight");
            lightObj.transform.SetParent(campfire.transform, false);
            lightObj.transform.localPosition = new Vector3(0f, 0.5f, 0f);
            var pointLight = lightObj.AddComponent<Light>();
            pointLight.type = LightType.Point;
            pointLight.color = new Color(1f, 0.6f, 0.2f);
            pointLight.intensity = 2.5f;
            pointLight.range = 15f;
            pointLight.shadows = LightShadows.Soft;

            // Scorch/burn texture around campfire base
            var scorchQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            scorchQuad.name = "Scorch";
            scorchQuad.transform.SetParent(campfire.transform, false);
            scorchQuad.transform.localPosition = new Vector3(0f, 0.02f, 0f);
            scorchQuad.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            scorchQuad.transform.localScale = new Vector3(3f, 3f, 1f);
            Destroy(scorchQuad.GetComponent<Collider>());
            var scorchMr = scorchQuad.GetComponent<MeshRenderer>();
            scorchMr.sharedMaterial = TerrainShaderLibrary.CreateScorchMaterial();
            scorchMr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            // Box collider on root for selection
            if (campfire.GetComponent<Collider>() == null)
            {
                var rootCol = campfire.AddComponent<BoxCollider>();
                rootCol.center = new Vector3(0f, 0.3f, 0f);
                rootCol.size = new Vector3(0.9f, 0.6f, 0.9f);
            }

            // Attach Building component so campfire is a real building in the system
            var campfireDef = BuildingRegistry.Instance?.CampfireDefinition;
            if (campfireDef != null)
            {
                var building = campfire.AddComponent<Building>();
                building.Initialize(campfireDef, skipConstruction: true);
            }

            EventBus.Publish(new BuildingPlacedEvent
            {
                BuildingName = "Campfire",
                Position = position,
                BuildingObject = campfire
            });

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
                // Place at correct terrain height so NavMeshAgent can find the NavMesh
                float y = world.GetSmoothedHeightAtWorldPos(x, z);
                settlerObj.transform.position = new Vector3(x, y, z);

                var settler = settlerObj.AddComponent<Settler>();
                settler.Initialize(i, campfirePos);
            }

            EventBus.Publish(new PopulationChangedEvent
            {
                CurrentPopulation = count
            });
        }

        /// <summary>
        /// Brute-force spawn a freshwater pond 8-12 blocks from campfire.
        /// Creates a flat water plane tagged "Water". Not tied to terrain
        /// generation — works regardless of biome, seed, or terrain shape.
        /// Coast biome gets the same freshwater pond (ocean is not drinkable).
        /// Flattens terrain under the pond and places it on NavMesh-reachable ground.
        /// </summary>
        private void SpawnFreshwaterPond(WorldManager world, Vector3 campfirePos)
        {
            var rng = new System.Random(GameState.Seed + 7);

            Vector3 pondPos = Vector3.zero;
            bool placed = false;

            // Try several angles to find NavMesh-reachable ground
            for (int attempt = 0; attempt < 12; attempt++)
            {
                float angle = (float)((rng.NextDouble() + attempt * 0.25) * Mathf.PI * 2.0 / 12.0);
                int distance = 8 + rng.Next(5); // 8 to 12 blocks (closer than before)

                float pondX = campfirePos.x + Mathf.Cos(angle) * distance;
                float pondZ = campfirePos.z + Mathf.Sin(angle) * distance;

                // Clamp to world bounds (leave 4-block margin)
                pondX = Mathf.Clamp(pondX, 4f, world.WorldBlocksX - 5f);
                pondZ = Mathf.Clamp(pondZ, 4f, world.WorldBlocksZ - 5f);

                // Flatten terrain under pond area
                int blockX = Mathf.FloorToInt(pondX);
                int blockZ = Mathf.FloorToInt(pondZ);
                world.FlattenTerrain(blockX, blockZ, 3);

                // Snap to terrain surface
                float pondY = world.GetSmoothedHeightAtWorldPos(pondX, pondZ);
                Vector3 candidate = new Vector3(pondX, pondY, pondZ);

                // Verify NavMesh is reachable near the pond edge
                Vector3 edgeTest = candidate + (campfirePos - candidate).normalized * 3f;
                if (NavMesh.SamplePosition(edgeTest, out NavMeshHit hit, 5f, NavMesh.AllAreas))
                {
                    pondPos = candidate;
                    placed = true;
                    break;
                }
            }

            if (!placed)
            {
                // Fallback: place directly between campfire and center
                float pondX = campfirePos.x + 10f;
                float pondZ = campfirePos.z;
                pondX = Mathf.Clamp(pondX, 4f, world.WorldBlocksX - 5f);
                pondZ = Mathf.Clamp(pondZ, 4f, world.WorldBlocksZ - 5f);
                float pondY = world.GetSmoothedHeightAtWorldPos(pondX, pondZ);
                pondPos = new Vector3(pondX, pondY, pondZ);
                Debug.LogWarning("[Water] Used fallback pond position — NavMesh check failed for all attempts");
            }

            // v0.5.0: Flat water plane (replaces blue cylinder)
            var pond = new GameObject("FreshwaterPond");
            pond.tag = "Water";

            // Main water surface — flat quad sitting slightly below terrain
            var waterQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            waterQuad.name = "WaterSurface";
            waterQuad.transform.SetParent(pond.transform, false);
            waterQuad.transform.localPosition = new Vector3(0f, -0.08f, 0f);
            waterQuad.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            waterQuad.transform.localScale = new Vector3(5.5f, 5.5f, 1f);
            Destroy(waterQuad.GetComponent<Collider>());

            // Pond basin — subtle darker ring under water for depth illusion
            var basin = GameObject.CreatePrimitive(PrimitiveType.Quad);
            basin.name = "Basin";
            basin.transform.SetParent(pond.transform, false);
            basin.transform.localPosition = new Vector3(0f, -0.15f, 0f);
            basin.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            basin.transform.localScale = new Vector3(6f, 6f, 1f);
            Destroy(basin.GetComponent<Collider>());

            pond.transform.position = pondPos;

            // Trigger collider for selection (doesn't block pathfinding)
            var triggerCol = pond.AddComponent<BoxCollider>();
            triggerCol.isTrigger = true;
            triggerCol.center = Vector3.zero;
            triggerCol.size = new Vector3(5f, 0.5f, 5f);

            // Water materials: enhanced water shader with waves, ripples, fresnel
            waterQuad.GetComponent<MeshRenderer>().sharedMaterial = TerrainShaderLibrary.CreateWaterMaterial();
            basin.GetComponent<MeshRenderer>().sharedMaterial =
                TerrainShaderLibrary.CreatePropMaterial("PondBasin_Mat", new Color(0.08f, 0.20f, 0.30f), 0.05f);

            // Register freshwater center so settlers can find drinkable water
            world.FreshwaterCenter = pondPos;

            // Verify distance
            float dist = Vector3.Distance(
                new Vector3(campfirePos.x, 0, campfirePos.z),
                new Vector3(pondPos.x, 0, pondPos.z));

            Debug.Log($"[Water] Freshwater pond at ({pondPos.x:F1},{pondPos.z:F1}), " +
                      $"distance from campfire: {dist:F1} blocks, tag={pond.tag}");

            if (dist >= 30f)
                Debug.LogError($"BLOCKER: Freshwater pond too far! distance={dist:F1} blocks (max 30)");
        }

    }
}
