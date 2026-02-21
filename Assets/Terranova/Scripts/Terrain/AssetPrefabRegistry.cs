using System.Collections.Generic;
using UnityEngine;

namespace Terranova.Terrain
{
    /// <summary>
    /// v0.5.2: Registry of Explorer Stoneage asset prefabs organized by category.
    /// Provides randomized prefab selection for terrain decoration, shelters,
    /// settlers, campfire, and gatherable resources.
    /// All paths are relative to Assets/ and loaded via Resources.Load at runtime,
    /// or direct path loading via AssetDatabase in editor / prefab instantiation.
    /// </summary>
    public static class AssetPrefabRegistry
    {
        private const string BASE = "Assets/EXPLORER - Stone Age/Prefabs/";

        // ─── Trees ──────────────────────────────────────────────

        public static readonly string[] PineTrees = {
            "Vegetation/Trees/Pine_Tree_1A", "Vegetation/Trees/Pine_Tree_1B",
            "Vegetation/Trees/Pine_Tree_1C", "Vegetation/Trees/Pine_Tree_1D",
            "Vegetation/Trees/Pine_Tree_1E", "Vegetation/Trees/Pine_Tree_1F",
            "Vegetation/Trees/Pine_Tree_1G", "Vegetation/Trees/Pine_Tree_1H",
            "Vegetation/Trees/Pine_Tree_2A", "Vegetation/Trees/Pine_Tree_2B",
            "Vegetation/Trees/Pine_Tree_2C", "Vegetation/Trees/Pine_Tree_2D",
            "Vegetation/Trees/Pine_Tree_2E", "Vegetation/Trees/Pine_Tree_2F",
            "Vegetation/Trees/Pine_Tree_2G", "Vegetation/Trees/Pine_Tree_2H",
            "Vegetation/Trees/Pine_Tree_3A", "Vegetation/Trees/Pine_Tree_3B",
            "Vegetation/Trees/Pine_Tree_3C", "Vegetation/Trees/Pine_Tree_3D",
            "Vegetation/Trees/Pine_Tree_3E", "Vegetation/Trees/Pine_Tree_3F",
            "Vegetation/Trees/Pine_Tree_3G", "Vegetation/Trees/Pine_Tree_3H",
        };

        public static readonly string[] DeciduousTrees = {
            "Vegetation/Trees/Tree_1A", "Vegetation/Trees/Tree_1B",
            "Vegetation/Trees/Tree_1C", "Vegetation/Trees/Tree_1D",
            "Vegetation/Trees/Tree_1E",
            "Vegetation/Trees/Tree_2A", "Vegetation/Trees/Tree_2B",
            "Vegetation/Trees/Tree_2C", "Vegetation/Trees/Tree_2D",
            "Vegetation/Trees/Tree_2E", "Vegetation/Trees/Tree_2F",
            "Vegetation/Trees/Tree_3A", "Vegetation/Trees/Tree_3B",
            "Vegetation/Trees/Tree_3C", "Vegetation/Trees/Tree_3D",
            "Vegetation/Trees/Tree_3E",
            "Vegetation/Trees/Tree_4A", "Vegetation/Trees/Tree_4B",
            "Vegetation/Trees/Tree_4C",
            "Vegetation/Trees/Tree_5A", "Vegetation/Trees/Tree_5B",
            "Vegetation/Trees/Tree_5C",
            "Vegetation/Trees/Tree_6A", "Vegetation/Trees/Tree_6B",
            "Vegetation/Trees/Tree_6C", "Vegetation/Trees/Tree_6D",
            "Vegetation/Trees/Tree_7A", "Vegetation/Trees/Tree_7B",
            "Vegetation/Trees/Tree_7C",
            "Vegetation/Trees/Tree_8A", "Vegetation/Trees/Tree_8B",
            "Vegetation/Trees/Tree_8C", "Vegetation/Trees/Tree_8D",
            "Vegetation/Trees/Tree_8E",
        };

        // Coast-only: limited tree set
        public static readonly string[] CoastTrees = {
            "Vegetation/Trees/Tree_1A", "Vegetation/Trees/Tree_2A",
            "Vegetation/Trees/Tree_3A",
        };

        // Mountains-only: sparse pines
        public static readonly string[] MountainTrees = {
            "Vegetation/Trees/Pine_Tree_1A", "Vegetation/Trees/Pine_Tree_2A",
        };

        // ─── Bushes ─────────────────────────────────────────────

        public static readonly string[] Bushes = {
            "Vegetation/Plants/Bush_1A", "Vegetation/Plants/Bush_1B", "Vegetation/Plants/Bush_1C",
            "Vegetation/Plants/Bush_2A", "Vegetation/Plants/Bush_2B", "Vegetation/Plants/Bush_2C", "Vegetation/Plants/Bush_2D",
            "Vegetation/Plants/Bush_3A_Corner", "Vegetation/Plants/Bush_3B_Corner",
            "Vegetation/Plants/Bush_4A", "Vegetation/Plants/Bush_4B", "Vegetation/Plants/Bush_4C",
            "Vegetation/Plants/Bush_5A", "Vegetation/Plants/Bush_5B", "Vegetation/Plants/Bush_5C",
            "Vegetation/Plants/Bush_6A", "Vegetation/Plants/Bush_6B",
            "Vegetation/Plants/Bush_7A", "Vegetation/Plants/Bush_7B", "Vegetation/Plants/Bush_7C",
            "Vegetation/Plants/Bush_8A", "Vegetation/Plants/Bush_8B", "Vegetation/Plants/Bush_8C",
            "Vegetation/Plants/Bush_9A", "Vegetation/Plants/Bush_9B",
            "Vegetation/Plants/Bush_10A", "Vegetation/Plants/Bush_10B",
            "Vegetation/Plants/Bush_11A", "Vegetation/Plants/Bush_11B", "Vegetation/Plants/Bush_11C",
            "Vegetation/Plants/Bush_12A", "Vegetation/Plants/Bush_12B",
            "Vegetation/Plants/Bush_13A", "Vegetation/Plants/Bush_13B",
        };

        public static readonly string[] CoastBushes = {
            "Vegetation/Plants/Bush_1A", "Vegetation/Plants/Bush_2A",
            "Vegetation/Plants/Bush_3A_Corner", "Vegetation/Plants/Bush_4A",
            "Vegetation/Plants/Bush_5A",
        };

        // ─── Ferns ──────────────────────────────────────────────

        public static readonly string[] Ferns = {
            "Vegetation/Plants/Fern_1A", "Vegetation/Plants/Fern_1B",
            "Vegetation/Plants/Fern_2A", "Vegetation/Plants/Fern_2B",
        };

        // ─── Flowers ────────────────────────────────────────────

        public static readonly string[] Flowers = {
            "Vegetation/Plants/Flower_1A", "Vegetation/Plants/Flower_1B",
            "Vegetation/Plants/Flower_1C", "Vegetation/Plants/Flower_1D",
        };

        // ─── Mushrooms (GATHERABLE - Food) ──────────────────────

        public static readonly string[] Mushrooms = {
            "Vegetation/Plants/Mushroom_1A", "Vegetation/Plants/Mushroom_1B",
            "Vegetation/Plants/Mushroom_2A", "Vegetation/Plants/Mushroom_2B",
            "Vegetation/Plants/Mushroom_3A", "Vegetation/Plants/Mushroom_3B",
            "Vegetation/Plants/Mushroom_4A", "Vegetation/Plants/Mushroom_4B",
            "Vegetation/Plants/Mushroom_5A", "Vegetation/Plants/Mushroom_5B",
            "Vegetation/Plants/Mushroom_6A", "Vegetation/Plants/Mushroom_6B",
        };

        // ─── Tree Logs / Deadwood (GATHERABLE - Wood) ───────────

        public static readonly string[] TreeLogs = {
            "Vegetation/Trees/Tree_Log_1A", "Vegetation/Trees/Tree_Log_1B",
            "Vegetation/Trees/Tree_Log_1C",
            "Vegetation/Trees/Tree_Log_2A", "Vegetation/Trees/Tree_Log_2B",
            "Vegetation/Trees/Tree_Log_2C", "Vegetation/Trees/Tree_Log_2D",
            "Vegetation/Trees/Tree_Log_2E", "Vegetation/Trees/Tree_Log_2F",
            "Vegetation/Trees/Tree_Log_2G",
        };

        public static readonly string[] Twigs = {
            "Props/Twigs_1A", "Props/Twigs_1B",
        };

        // ─── Tree Trunks (Decoration) ───────────────────────────

        public static readonly string[] TreeTrunks = {
            "Vegetation/Trees/Tree_Trunk_1A", "Vegetation/Trees/Tree_Trunk_1B",
        };

        // ─── Rocks: Small (GATHERABLE - Stone) ─────────────────

        public static readonly string[] RockSmall = {
            "Rocks/Rock_Small_1A", "Rocks/Rock_Small_1B", "Rocks/Rock_Small_1C",
            "Rocks/Rock_Small_1D", "Rocks/Rock_Small_1E", "Rocks/Rock_Small_1F",
        };

        // ─── Rocks: Large / Medium (Decoration) ────────────────

        public static readonly string[] RockLarge = {
            "Rocks/Rock_Large_1A", "Rocks/Rock_Large_1B",
            "Rocks/Rock_Large_2A", "Rocks/Rock_Large_2B",
            "Rocks/Rock_Large_3A", "Rocks/Rock_Large_3B",
        };

        public static readonly string[] RockMedium = {
            "Rocks/Rock_Medium_1A", "Rocks/Rock_Medium_1B",
            "Rocks/Rock_Medium_2A", "Rocks/Rock_Medium_2B",
            "Rocks/Rock_Medium_3A", "Rocks/Rock_Medium_3B",
            "Rocks/Rock_Medium_3C", "Rocks/Rock_Medium_3D",
            "Rocks/Rock_Medium_3E", "Rocks/Rock_Medium_3F",
        };

        // ─── Rocks: Mountain-specific ──────────────────────────

        public static readonly string[] CliffFormations = {
            "Rocks/Cliff_Formation_1A", "Rocks/Cliff_Formation_2A",
            "Rocks/Cliff_Formation_3A", "Rocks/Cliff_Formation_4A",
            "Rocks/Cliff_Formation_5A", "Rocks/Cliff_Formation_5B",
            "Rocks/Cliff_Formation_6A", "Rocks/Cliff_Formation_6B", "Rocks/Cliff_Formation_6C",
            "Rocks/Cliff_Formation_7A", "Rocks/Cliff_Formation_7B", "Rocks/Cliff_Formation_7C",
            "Rocks/Cliff_Formation_7D", "Rocks/Cliff_Formation_7E", "Rocks/Cliff_Formation_7F",
            "Rocks/Cliff_Formation_8A",
        };

        public static readonly string[] CanyonWalls = {
            "Rocks/Canyon_Wall_1A", "Rocks/Canyon_Wall_1B", "Rocks/Canyon_Wall_1C",
            "Rocks/Canyon_Wall_2A", "Rocks/Canyon_Wall_2B", "Rocks/Canyon_Wall_2C",
            "Rocks/Canyon_Wall_3A", "Rocks/Canyon_Wall_3B", "Rocks/Canyon_Wall_3C",
            "Rocks/Canyon_Wall_4A", "Rocks/Canyon_Wall_4B", "Rocks/Canyon_Wall_4C",
        };

        public static readonly string[] RockFormations = {
            "Rocks/Rock_Formation_1A", "Rocks/Rock_Formation_2A",
        };

        public static readonly string[] RockSharp = {
            "Rocks/Rock_Sharp_1A", "Rocks/Rock_Sharp_1B",
            "Rocks/Rock_Sharp_2A", "Rocks/Rock_Sharp_2B",
            "Rocks/Rock_Sharp_3A", "Rocks/Rock_Sharp_3B",
        };

        // ─── Rocks: Coast-specific ─────────────────────────────

        public static readonly string[] RockSlabs = {
            "Rocks/Rock_Slab_1A", "Rocks/Rock_Slab_2A", "Rocks/Rock_Slab_3A",
            "Rocks/Rock_Slab_4A", "Rocks/Rock_Slab_5A", "Rocks/Rock_Slab_6A",
        };

        public static readonly string[] RockPavements = {
            "Rocks/Rock_Pavement_1A", "Rocks/Rock_Pavement_1B", "Rocks/Rock_Pavement_1C",
        };

        // ─── Shelters ──────────────────────────────────────────

        public static readonly string[] CaveEntrance = {
            "Rocks/Cave_Entrance_1A", "Rocks/Cave_Entrance_2A",
        };

        public static readonly string[] CanyonOverpass = {
            "Rocks/Canyon_Overpass_1A", "Rocks/Canyon_Overpass_1B",
            "Rocks/Canyon_Overpass_1C",
        };

        public static readonly string[] RockClusters = {
            "Rocks/Rock_Cluster_1A", "Rocks/Rock_Cluster_2A",
            "Rocks/Rock_Cluster_3A", "Rocks/Rock_Cluster_4A",
            "Rocks/Rock_Cluster_5A", "Rocks/Rock_Cluster_6A",
            "Rocks/Rock_Cluster_7A", "Rocks/Rock_Cluster_8A",
        };

        // ─── Campfire & Props ───────────────────────────────────

        public static readonly string[] CampFires = {
            "Props/Camp_Fire_1A", "Props/Camp_Fire_1B",
        };

        public static readonly string[] Bones = {
            "Props/Bone_1A", "Props/Bone_1B", "Props/Bone_2A",
        };

        public static readonly string[] AnimalCarcasses = {
            "Props/Animal_Carcass_1A", "Props/Animal_Carcass_1B",
            "Props/Animal_Carcass_2A",
        };

        // ─── Particles ─────────────────────────────────────────

        public static readonly string FireParticle = "Assets/EXPLORER - Stone Age/Particles/Fire_1A";
        public static readonly string FirefliesParticle = "Assets/EXPLORER - Stone Age/Particles/Fireflies_1A";
        public static readonly string SunShaftParticle = "Assets/EXPLORER - Stone Age/Particles/Sun_Shaft_1A";
        public static readonly string FogParticle = "Assets/EXPLORER - Stone Age/Particles/Fog_1A";

        // ─── Settlers (Avatars) ────────────────────────────────

        public static readonly string[] MaleAvatars = {
            "Avatars/Prehistoric_Male_Avatar_V1",
            "Avatars/Prehistoric_Male_Avatar_V2",
            "Avatars/Prehistoric_Male_Avatar_V3",
            "Avatars/Prehistoric_Male_Avatar_V4",
        };

        public static readonly string[] FemaleAvatars = {
            "Avatars/Prehistoric_Female_Avatar_V1",
            "Avatars/Prehistoric_Female_Avatar_V2",
            "Avatars/Prehistoric_Female_Avatar_V3",
            "Avatars/Prehistoric_Female_Avatar_V4",
        };

        // ─── Prefab cache ──────────────────────────────────────

        private static readonly Dictionary<string, GameObject> _prefabCache = new();
        private static int _loadSuccessCount;
        private static int _loadFailCount;
        private static bool _validated;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _prefabCache.Clear();
            _loadSuccessCount = 0;
            _loadFailCount = 0;
            _validated = false;
        }

        /// <summary>
        /// One-time validation: attempt to load a known prefab and log diagnostic info.
        /// Called automatically on first LoadPrefab call.
        /// </summary>
        private static void ValidateAssetLoading()
        {
            _validated = true;

            string testPath = "Vegetation/Trees/Pine_Tree_1A";
            string fullTestPath = BASE + testPath + ".prefab";

            Debug.Log($"[AssetPrefabRegistry] ═══ PREFAB LOADING VALIDATION ═══");
#if UNITY_EDITOR
            Debug.Log($"[AssetPrefabRegistry] Mode: UNITY_EDITOR (AssetDatabase)");
            Debug.Log($"[AssetPrefabRegistry] Test path: {fullTestPath}");
            var testPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(fullTestPath);
            if (testPrefab != null)
            {
                Debug.Log($"[AssetPrefabRegistry] ✓ Validation PASSED — '{testPrefab.name}' loaded successfully");
                // Check if it has renderers (would be invisible without them)
                int rendererCount = testPrefab.GetComponentsInChildren<Renderer>(true).Length;
                Debug.Log($"[AssetPrefabRegistry]   Renderers in prefab: {rendererCount}");
            }
            else
            {
                Debug.LogError($"[AssetPrefabRegistry] ✗ Validation FAILED — AssetDatabase returned null for: {fullTestPath}");
                // Try to find the asset by name as diagnostic
                string[] guids = UnityEditor.AssetDatabase.FindAssets("Pine_Tree_1A t:Prefab");
                if (guids.Length > 0)
                {
                    string foundPath = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
                    Debug.LogError($"[AssetPrefabRegistry]   FindAssets found it at: {foundPath}");
                    Debug.LogError($"[AssetPrefabRegistry]   Expected path was:      {fullTestPath}");
                    Debug.LogError($"[AssetPrefabRegistry]   → PATH MISMATCH! The asset exists but at a different location.");
                }
                else
                {
                    Debug.LogError($"[AssetPrefabRegistry]   FindAssets found NO prefab named 'Pine_Tree_1A' in the entire project!");
                    Debug.LogError($"[AssetPrefabRegistry]   → The Explorer Stone Age asset pack may not be imported correctly.");
                }
            }
#else
            Debug.Log($"[AssetPrefabRegistry] Mode: RUNTIME (Resources.Load)");
            Debug.Log($"[AssetPrefabRegistry] Test subPath: {testPath}");
            var testPrefab = UnityEngine.Resources.Load<GameObject>(testPath);
            if (testPrefab != null)
            {
                Debug.Log($"[AssetPrefabRegistry] ✓ Validation PASSED — '{testPrefab.name}' loaded from Resources");
            }
            else
            {
                Debug.LogError($"[AssetPrefabRegistry] ✗ Validation FAILED — Resources.Load returned null for: {testPath}");
                Debug.LogError($"[AssetPrefabRegistry]   Prefabs must be in a 'Resources/' folder for runtime builds.");
                Debug.LogError($"[AssetPrefabRegistry]   Expected: Assets/Resources/{testPath}.prefab");
                Debug.LogError($"[AssetPrefabRegistry]   The Explorer Stone Age prefabs are NOT in a Resources folder.");
                Debug.LogError($"[AssetPrefabRegistry]   → ALL prefab loading will fail. Props, trees, settlers will be missing.");
            }
#endif
            Debug.Log($"[AssetPrefabRegistry] ═══════════════════════════════════");
        }

        /// <summary>
        /// Load a prefab from the Explorer Stoneage asset pack.
        /// Uses the Prefabs/ subfolder path (e.g. "Vegetation/Trees/Pine_Tree_1A").
        /// Caches loaded prefabs for reuse.
        /// </summary>
        public static GameObject LoadPrefab(string subPath)
        {
            if (!_validated) ValidateAssetLoading();

            if (_prefabCache.TryGetValue(subPath, out var cached) && cached != null)
                return cached;

            string fullPath = subPath.StartsWith("Assets/")
                ? subPath
                : BASE + subPath;

            string assetPath = fullPath + ".prefab";

#if UNITY_EDITOR
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
#else
            // Runtime: try Resources.Load (requires assets in a Resources/ folder)
            var prefab = UnityEngine.Resources.Load<GameObject>(subPath);
            // Fallback: try just the filename
            if (prefab == null)
            {
                string fileName = System.IO.Path.GetFileName(subPath);
                prefab = UnityEngine.Resources.Load<GameObject>(fileName);
            }
#endif

            if (prefab != null)
            {
                _prefabCache[subPath] = prefab;
                _loadSuccessCount++;
            }
            else
            {
                _loadFailCount++;
                // Log first 5 failures as errors, then suppress to avoid console flood
                if (_loadFailCount <= 5)
                    Debug.LogError($"[AssetPrefabRegistry] LOAD FAILED ({_loadFailCount}): {assetPath}");
                else if (_loadFailCount == 6)
                    Debug.LogError($"[AssetPrefabRegistry] Suppressing further load errors. Total failures so far: {_loadFailCount}");
            }

            return prefab;
        }

        /// <summary>Log summary of load attempts. Call after bulk loading is done.</summary>
        public static void LogLoadSummary()
        {
            int total = _loadSuccessCount + _loadFailCount;
            if (_loadFailCount > 0)
                Debug.LogError($"[AssetPrefabRegistry] LOAD SUMMARY: {_loadSuccessCount}/{total} succeeded, {_loadFailCount} FAILED");
            else
                Debug.Log($"[AssetPrefabRegistry] LOAD SUMMARY: {_loadSuccessCount}/{total} succeeded — all OK");
        }

        /// <summary>
        /// Instantiate a random prefab from the given array at the specified position.
        /// Returns null if no prefab could be loaded.
        /// </summary>
        public static GameObject InstantiateRandom(string[] prefabPaths, Vector3 position,
            System.Random rng, Transform parent, float minScale = 0.8f, float maxScale = 1.2f)
        {
            if (prefabPaths == null || prefabPaths.Length == 0) return null;

            string chosen = prefabPaths[rng.Next(prefabPaths.Length)];
            var prefab = LoadPrefab(chosen);
            if (prefab == null) return null;

            var instance = Object.Instantiate(prefab, position, Quaternion.Euler(0f, (float)(rng.NextDouble() * 360.0), 0f), parent);
            float scale = minScale + (float)(rng.NextDouble() * (maxScale - minScale));
            instance.transform.localScale = prefab.transform.localScale * scale;

            return instance;
        }

        /// <summary>
        /// Instantiate a specific prefab at the specified position.
        /// Returns null if the prefab could not be loaded.
        /// </summary>
        public static GameObject InstantiateSpecific(string prefabPath, Vector3 position,
            Quaternion rotation, Transform parent, float scale = 1f)
        {
            var prefab = LoadPrefab(prefabPath);
            if (prefab == null) return null;

            var instance = Object.Instantiate(prefab, position, rotation, parent);
            if (!Mathf.Approximately(scale, 1f))
                instance.transform.localScale = prefab.transform.localScale * scale;

            return instance;
        }
    }
}
