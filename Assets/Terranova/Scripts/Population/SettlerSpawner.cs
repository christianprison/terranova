using UnityEngine;
using Terranova.Buildings;
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
        private static Mesh _cachedFlameConeMesh;

        // We wait for WorldManager in Update because terrain generates in Start
        // and we can't guarantee execution order between different Start methods.
        private void Update()
        {
            if (_hasSpawned)
                return;

            // Wait until terrain and NavMesh are ready (Story 2.0)
            var world = WorldManager.Instance;
            if (world == null || world.WorldBlocksX == 0 || !world.IsNavMeshReady)
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

            // Find solid ground near the world center, then flatten terrain
            // so campfire and settlers all stand on level ground.
            FindSolidGround(world, ref centerX, ref centerZ);

            // Flatten terrain in a radius that covers the campfire + settler area.
            // Settlers spawn at _spawnRadius (~3 blocks), so radius 4 covers everything.
            world.FlattenTerrain(centerX, centerZ, Mathf.CeilToInt(_spawnRadius) + 1);

            // Position on smooth mesh surface (re-query after flattening)
            float y = world.GetSmoothedHeightAtWorldPos(centerX + 0.5f, centerZ + 0.5f);
            Vector3 position = new Vector3(centerX + 0.5f, y, centerZ + 0.5f);

            // Create campfire visual: stone ring + flame cone
            var campfire = new GameObject("Campfire");
            campfire.transform.position = position;

            Shader shader = Shader.Find("Universal Render Pipeline/Lit")
                         ?? Shader.Find("Universal Render Pipeline/Particles/Unlit");
            Material stoneMat = null;
            Material flameMat = null;
            Material glowMat = null;
            if (shader != null)
            {
                stoneMat = new Material(shader);
                stoneMat.name = "CampfireStone_Material (Auto)";
                stoneMat.SetColor("_BaseColor", new Color(0.45f, 0.43f, 0.40f));

                flameMat = new Material(shader);
                flameMat.name = "CampfireFlame_Material (Auto)";
                flameMat.SetColor("_BaseColor", new Color(1f, 0.55f, 0.1f));

                glowMat = new Material(shader);
                glowMat.name = "CampfireGlow_Material (Auto)";
                glowMat.SetColor("_BaseColor", new Color(1f, 0.85f, 0.3f));
            }

            // Stone ring: 6 small cubes
            for (int s = 0; s < 6; s++)
            {
                float sAngle = s * Mathf.PI * 2f / 6f;
                var stone = GameObject.CreatePrimitive(PrimitiveType.Cube);
                stone.name = $"Stone_{s}";
                stone.transform.SetParent(campfire.transform, false);
                stone.transform.localScale = new Vector3(0.2f, 0.15f, 0.2f);
                stone.transform.localPosition = new Vector3(
                    Mathf.Cos(sAngle) * 0.35f, 0.07f, Mathf.Sin(sAngle) * 0.35f);
                stone.transform.localRotation = Quaternion.Euler(0f, sAngle * Mathf.Rad2Deg + 15f, 0f);
                var sCol = stone.GetComponent<Collider>();
                if (sCol != null) Destroy(sCol);
                if (stoneMat != null) stone.GetComponent<MeshRenderer>().sharedMaterial = stoneMat;
            }

            // Flame cone (center) – cache mesh for reuse
            if (_cachedFlameConeMesh == null)
                _cachedFlameConeMesh = CreateConeMesh(0.15f, 0.6f, 6);
            var flameConeMesh = _cachedFlameConeMesh;
            var flame = new GameObject("Flame");
            flame.transform.SetParent(campfire.transform, false);
            flame.transform.localPosition = new Vector3(0f, 0.05f, 0f);
            var flameMF = flame.AddComponent<MeshFilter>();
            flameMF.sharedMesh = flameConeMesh;
            var flameMR = flame.AddComponent<MeshRenderer>();
            if (flameMat != null) flameMR.sharedMaterial = flameMat;

            // Inner glow cone
            var glow = new GameObject("Glow");
            glow.transform.SetParent(campfire.transform, false);
            glow.transform.localPosition = new Vector3(0f, 0.05f, 0f);
            glow.transform.localScale = new Vector3(0.5f, 0.75f, 0.5f);
            var glowMF = glow.AddComponent<MeshFilter>();
            glowMF.sharedMesh = flameConeMesh;
            var glowMR = glow.AddComponent<MeshRenderer>();
            if (glowMat != null) glowMR.sharedMaterial = glowMat;

            // Box collider on root for selection
            var rootCol = campfire.AddComponent<BoxCollider>();
            rootCol.center = new Vector3(0f, 0.3f, 0f);
            rootCol.size = new Vector3(0.9f, 0.6f, 0.9f);

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
        /// Search outward from the given position for solid ground.
        /// The terrain will be flattened afterwards, so we only need solid surface.
        /// </summary>
        private static void FindSolidGround(WorldManager world, ref int x, ref int z)
        {
            // Check the starting position first
            if (IsSolid(world, x, z))
                return;

            // Search in expanding rings up to 32 blocks away
            for (int radius = 1; radius <= 32; radius++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    for (int dz = -radius; dz <= radius; dz++)
                    {
                        if (Mathf.Abs(dx) != radius && Mathf.Abs(dz) != radius)
                            continue;

                        int testX = x + dx;
                        int testZ = z + dz;

                        if (IsSolid(world, testX, testZ))
                        {
                            x = testX;
                            z = testZ;
                            return;
                        }
                    }
                }
            }

            Debug.LogWarning("SettlerSpawner: Could not find solid ground near world center!");
        }

        /// <summary>
        /// Check if the surface at (x,z) is solid ground.
        /// </summary>
        private static bool IsSolid(WorldManager world, int x, int z)
        {
            int h = world.GetHeightAtWorldPos(x, z);
            return h >= 0 && world.GetSurfaceTypeAtWorldPos(x, z).IsSolid();
        }

        /// <summary>
        /// Create a simple cone mesh with base at y=0 and apex at y=height.
        /// </summary>
        private static Mesh CreateConeMesh(float radius, float height, int segments)
        {
            var mesh = new Mesh();
            int vertCount = segments + 2; // base ring + apex + base center
            var verts = new Vector3[vertCount];
            var normals = new Vector3[vertCount];

            // Base center vertex
            verts[0] = Vector3.zero;
            normals[0] = Vector3.down;

            // Base ring vertices
            for (int i = 0; i < segments; i++)
            {
                float angle = i * Mathf.PI * 2f / segments;
                float x = Mathf.Cos(angle) * radius;
                float z = Mathf.Sin(angle) * radius;
                verts[i + 1] = new Vector3(x, 0f, z);

                // Approximate outward-up normal for the side
                Vector3 outward = new Vector3(x, 0f, z).normalized;
                normals[i + 1] = Vector3.Lerp(outward, Vector3.up, 0.5f).normalized;
            }

            // Apex vertex
            verts[segments + 1] = new Vector3(0f, height, 0f);
            normals[segments + 1] = Vector3.up;

            // Triangles: base disk + side cone
            var tris = new int[segments * 6];
            for (int i = 0; i < segments; i++)
            {
                int next = (i + 1) % segments;

                // Base triangle (facing down)
                tris[i * 6 + 0] = 0;
                tris[i * 6 + 1] = next + 1;
                tris[i * 6 + 2] = i + 1;

                // Side triangle (facing outward)
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
