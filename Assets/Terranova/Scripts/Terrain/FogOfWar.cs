using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Terranova.Core;

namespace Terranova.Terrain
{
    /// <summary>
    /// v0.5.1: Fog of War system.
    ///
    /// At game start the entire map is covered in dark semi-transparent fog
    /// EXCEPT a 30-block radius around the campfire. When a settler walks
    /// through fogged areas the fog permanently clears in a 10-block radius
    /// (15 for Curious trait). Explored areas stay visible forever.
    ///
    /// Unexplored areas hide resource props, decorations, and shelter markers.
    /// The fog is rendered as a single mesh with per-vertex alpha.
    /// </summary>
    public class FogOfWar : MonoBehaviour
    {
        public static FogOfWar Instance { get; private set; }

        private const float FOG_HEIGHT_OFFSET = 2.0f;
        private const int CAMPFIRE_REVEAL_RADIUS = 30;
        public const int SETTLER_REVEAL_RADIUS = 10;
        public const int CURIOUS_REVEAL_RADIUS = 15;
        private const float UPDATE_INTERVAL = 0.25f;

        private bool[,] _explored;
        private int _worldSizeX, _worldSizeZ;
        private Mesh _fogMesh;
        private MeshFilter _meshFilter;
        private MeshRenderer _meshRenderer;
        private Color32[] _vertexColors;
        private Vector3[] _vertices;
        private bool _meshDirty;
        private float _updateTimer;
        private bool _initialized;

        // Track hidden objects so we can reveal them when fog clears
        private readonly List<HideableEntry> _hideables = new();

        private struct HideableEntry
        {
            public GameObject Go;
            public int BlockX, BlockZ;
            public bool Revealed;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            // Instance is reset on destroy
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private IEnumerator Start()
        {
            // Wait for WorldManager to be fully ready
            while (WorldManager.Instance == null || WorldManager.Instance.WorldBlocksX == 0
                   || !WorldManager.Instance.IsNavMeshReady)
                yield return null;

            // Wait one extra frame for decorations and resources to spawn
            yield return null;
            yield return null;

            Initialize();
        }

        private void Initialize()
        {
            var world = WorldManager.Instance;
            _worldSizeX = world.WorldBlocksX;
            _worldSizeZ = world.WorldBlocksZ;
            _explored = new bool[_worldSizeX, _worldSizeZ];

            // Reveal around campfire
            RevealArea(world.CampfireBlockX, world.CampfireBlockZ, CAMPFIRE_REVEAL_RADIUS);

            BuildFogMesh(world);
            RegisterHideables();
            UpdateHideableVisibility();

            _initialized = true;
            Debug.Log($"[FogOfWar] Initialized {_worldSizeX}x{_worldSizeZ} grid, " +
                      $"revealed {CAMPFIRE_REVEAL_RADIUS}-block radius around campfire.");
        }

        private void Update()
        {
            if (!_initialized) return;

            _updateTimer -= Time.deltaTime;
            if (_updateTimer > 0f) return;
            _updateTimer = UPDATE_INTERVAL;

            if (_meshDirty)
            {
                _meshDirty = false;
                RefreshFogMesh();
                UpdateHideableVisibility();
            }
        }

        // ─── Public API ──────────────────────────────────────────

        /// <summary>
        /// Reveal a circular area of the fog. Returns true if any new cells
        /// were revealed (useful for discovery checks).
        /// </summary>
        public bool RevealArea(int centerX, int centerZ, int radius)
        {
            if (_explored == null) return false;

            bool anyNew = false;
            int r2 = radius * radius;

            int minX = Mathf.Max(0, centerX - radius);
            int maxX = Mathf.Min(_worldSizeX - 1, centerX + radius);
            int minZ = Mathf.Max(0, centerZ - radius);
            int maxZ = Mathf.Min(_worldSizeZ - 1, centerZ + radius);

            for (int x = minX; x <= maxX; x++)
            {
                for (int z = minZ; z <= maxZ; z++)
                {
                    if (_explored[x, z]) continue;
                    int dx = x - centerX;
                    int dz = z - centerZ;
                    if (dx * dx + dz * dz <= r2)
                    {
                        _explored[x, z] = true;
                        anyNew = true;
                    }
                }
            }

            if (anyNew) _meshDirty = true;
            return anyNew;
        }

        /// <summary>
        /// Reveal fog at a world position (settler walking).
        /// </summary>
        public bool RevealAtWorldPos(Vector3 worldPos, int radius)
        {
            int bx = Mathf.FloorToInt(worldPos.x);
            int bz = Mathf.FloorToInt(worldPos.z);
            return RevealArea(bx, bz, radius);
        }

        /// <summary>Check if a world block position is explored.</summary>
        public bool IsExplored(int blockX, int blockZ)
        {
            if (_explored == null) return true; // No fog = everything visible
            if (blockX < 0 || blockX >= _worldSizeX || blockZ < 0 || blockZ >= _worldSizeZ)
                return true;
            return _explored[blockX, blockZ];
        }

        /// <summary>Check if a world position is explored.</summary>
        public bool IsExploredAtWorldPos(Vector3 pos)
        {
            return IsExplored(Mathf.FloorToInt(pos.x), Mathf.FloorToInt(pos.z));
        }

        /// <summary>Count total explored cells (for stats/discovery).</summary>
        public int ExploredCount
        {
            get
            {
                if (_explored == null) return 0;
                int count = 0;
                for (int x = 0; x < _worldSizeX; x++)
                    for (int z = 0; z < _worldSizeZ; z++)
                        if (_explored[x, z]) count++;
                return count;
            }
        }

        /// <summary>Was any cell in this area previously unexplored?</summary>
        public bool IsAreaUnexplored(int centerX, int centerZ, int radius)
        {
            if (_explored == null) return false;
            int r2 = radius * radius;
            int minX = Mathf.Max(0, centerX - radius);
            int maxX = Mathf.Min(_worldSizeX - 1, centerX + radius);
            int minZ = Mathf.Max(0, centerZ - radius);
            int maxZ = Mathf.Min(_worldSizeZ - 1, centerZ + radius);

            for (int x = minX; x <= maxX; x++)
                for (int z = minZ; z <= maxZ; z++)
                {
                    int dx = x - centerX;
                    int dz = z - centerZ;
                    if (dx * dx + dz * dz <= r2 && !_explored[x, z])
                        return true;
                }
            return false;
        }

        /// <summary>
        /// Reset all fog (new tribe). Keeps the campfire reveal.
        /// </summary>
        public void ResetFog()
        {
            if (_explored == null) return;
            for (int x = 0; x < _worldSizeX; x++)
                for (int z = 0; z < _worldSizeZ; z++)
                    _explored[x, z] = false;

            var world = WorldManager.Instance;
            if (world != null)
                RevealArea(world.CampfireBlockX, world.CampfireBlockZ, CAMPFIRE_REVEAL_RADIUS);

            _meshDirty = true;

            // Re-hide all objects
            foreach (var entry in _hideables)
            {
                if (entry.Go != null)
                    SetObjectVisible(entry.Go, false);
            }
            for (int i = 0; i < _hideables.Count; i++)
            {
                var e = _hideables[i];
                e.Revealed = false;
                _hideables[i] = e;
            }

            UpdateHideableVisibility();
            Debug.Log("[FogOfWar] Fog reset for new tribe.");
        }

        // ─── Fog Mesh ───────────────────────────────────────────

        private void BuildFogMesh(WorldManager world)
        {
            // Use a lower resolution mesh (every 2 blocks) for performance
            int step = 2;
            int vertsX = _worldSizeX / step + 1;
            int vertsZ = _worldSizeZ / step + 1;

            _vertices = new Vector3[vertsX * vertsZ];
            _vertexColors = new Color32[vertsX * vertsZ];
            var uv = new Vector2[vertsX * vertsZ];

            for (int vz = 0; vz < vertsZ; vz++)
            {
                for (int vx = 0; vx < vertsX; vx++)
                {
                    int idx = vz * vertsX + vx;
                    float wx = vx * step;
                    float wz = vz * step;
                    float wy = world.GetSmoothedHeightAtWorldPos(wx, wz) + FOG_HEIGHT_OFFSET;
                    _vertices[idx] = new Vector3(wx, wy, wz);
                    uv[idx] = new Vector2(wx / _worldSizeX, wz / _worldSizeZ);
                }
            }

            // Build triangles
            int quadsX = vertsX - 1;
            int quadsZ = vertsZ - 1;
            var triangles = new int[quadsX * quadsZ * 6];
            int t = 0;
            for (int qz = 0; qz < quadsZ; qz++)
            {
                for (int qx = 0; qx < quadsX; qx++)
                {
                    int bl = qz * vertsX + qx;
                    int br = bl + 1;
                    int tl = bl + vertsX;
                    int tr = tl + 1;

                    triangles[t++] = bl;
                    triangles[t++] = tl;
                    triangles[t++] = br;
                    triangles[t++] = br;
                    triangles[t++] = tl;
                    triangles[t++] = tr;
                }
            }

            // Set vertex colors (dark fog)
            RefreshVertexColors(vertsX, vertsZ, step);

            _fogMesh = new Mesh();
            _fogMesh.name = "FogOfWarMesh";
            _fogMesh.vertices = _vertices;
            _fogMesh.triangles = triangles;
            _fogMesh.uv = uv;
            _fogMesh.colors32 = _vertexColors;
            _fogMesh.RecalculateNormals();
            _fogMesh.RecalculateBounds();

            // Create the fog GameObject
            var fogObj = new GameObject("FogMesh");
            fogObj.transform.SetParent(transform, false);
            _meshFilter = fogObj.AddComponent<MeshFilter>();
            _meshFilter.mesh = _fogMesh;
            _meshRenderer = fogObj.AddComponent<MeshRenderer>();

            // Custom fog shader with gradient edges and animated noise
            _meshRenderer.sharedMaterial = TerrainShaderLibrary.CreateFogMaterial();

            // Disable shadows
            _meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _meshRenderer.receiveShadows = false;
        }

        private void RefreshVertexColors(int vertsX, int vertsZ, int step)
        {
            byte fogAlpha = 210; // Dark but not fully opaque
            for (int vz = 0; vz < vertsZ; vz++)
            {
                for (int vx = 0; vx < vertsX; vx++)
                {
                    int idx = vz * vertsX + vx;
                    int wx = vx * step;
                    int wz = vz * step;

                    // Sample 2x2 area to get smooth transition
                    float explored = 0f;
                    int samples = 0;
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        for (int dz = -1; dz <= 1; dz++)
                        {
                            int sx = wx + dx * step;
                            int sz = wz + dz * step;
                            if (sx >= 0 && sx < _worldSizeX && sz >= 0 && sz < _worldSizeZ)
                            {
                                if (_explored[sx, sz]) explored += 1f;
                                samples++;
                            }
                        }
                    }

                    float fogAmount = 1f - (explored / samples);
                    byte alpha = (byte)(fogAmount * fogAlpha);
                    _vertexColors[idx] = new Color32(8, 12, 8, alpha);
                }
            }
        }

        private void RefreshFogMesh()
        {
            if (_fogMesh == null) return;
            int step = 2;
            int vertsX = _worldSizeX / step + 1;
            int vertsZ = _worldSizeZ / step + 1;
            RefreshVertexColors(vertsX, vertsZ, step);
            _fogMesh.colors32 = _vertexColors;
        }

        // ─── Hideable Objects ────────────────────────────────────

        private void RegisterHideables()
        {
            _hideables.Clear();

            // Find TerrainDecorations container and its children
            RegisterChildrenOf("TerrainDecorations");

            // Find Resources container
            RegisterChildrenOf("Resources");
        }

        private void RegisterChildrenOf(string parentName)
        {
            var parent = GameObject.Find(parentName);
            if (parent == null) return;

            // Iterate immediate children and their immediate children
            for (int i = 0; i < parent.transform.childCount; i++)
            {
                var child = parent.transform.GetChild(i);
                // Sub-containers: Trees, Rocks, Bushes, GroundPatches, NaturalShelters
                if (child.childCount > 0 && child.GetComponent<Renderer>() == null)
                {
                    for (int j = 0; j < child.childCount; j++)
                    {
                        var obj = child.GetChild(j).gameObject;
                        RegisterHideable(obj);
                    }
                }
                else
                {
                    RegisterHideable(child.gameObject);
                }
            }
        }

        private void RegisterHideable(GameObject go)
        {
            var pos = go.transform.position;
            int bx = Mathf.FloorToInt(pos.x);
            int bz = Mathf.FloorToInt(pos.z);

            bool revealed = IsExplored(bx, bz);
            _hideables.Add(new HideableEntry
            {
                Go = go,
                BlockX = bx,
                BlockZ = bz,
                Revealed = revealed
            });

            if (!revealed) SetObjectVisible(go, false);
        }

        private void UpdateHideableVisibility()
        {
            for (int i = 0; i < _hideables.Count; i++)
            {
                var entry = _hideables[i];
                if (entry.Revealed || entry.Go == null) continue;

                if (IsExplored(entry.BlockX, entry.BlockZ))
                {
                    entry.Revealed = true;
                    _hideables[i] = entry;
                    SetObjectVisible(entry.Go, true);
                }
            }
        }

        private static void SetObjectVisible(GameObject go, bool visible)
        {
            // Toggle all renderers on this object and children
            var renderers = go.GetComponentsInChildren<Renderer>(true);
            foreach (var r in renderers)
                r.enabled = visible;

            // Also toggle colliders so hidden objects can't be selected
            var colliders = go.GetComponentsInChildren<Collider>(true);
            foreach (var c in colliders)
                c.enabled = visible;
        }
    }
}
