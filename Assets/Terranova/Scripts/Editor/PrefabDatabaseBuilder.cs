using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Terranova.Terrain;

namespace Terranova.EditorTools
{
    /// <summary>
    /// Editor tool: collects all prefab paths referenced by AssetPrefabRegistry
    /// and stores direct GameObject references in a PrefabDatabase ScriptableObject.
    ///
    /// This makes prefabs available in iOS/standalone builds where
    /// AssetDatabase is not available.
    ///
    /// Usage: Unity menu → Terranova → Rebuild Prefab Database
    /// Also runs automatically before every build (via IPreprocessBuildWithReport).
    /// </summary>
    public static class PrefabDatabaseBuilder
    {
        private const string DB_PATH = "Assets/Terranova/Resources/PrefabDatabase.asset";
        private const string BASE = "Assets/EXPLORER - Stone Age/Prefabs/";

        [MenuItem("Terranova/Rebuild Prefab Database")]
        public static void RebuildDatabase()
        {
            var entries = new List<PrefabDatabase.Entry>();
            int missing = 0;

            // Collect all string[] arrays from AssetPrefabRegistry via reflection-free list
            var allPools = new Dictionary<string, string[]>
            {
                { "PineTrees",        AssetPrefabRegistry.PineTrees },
                { "DeciduousTrees",   AssetPrefabRegistry.DeciduousTrees },
                { "CoastTrees",       AssetPrefabRegistry.CoastTrees },
                { "MountainTrees",    AssetPrefabRegistry.MountainTrees },
                { "Bushes",           AssetPrefabRegistry.Bushes },
                { "CoastBushes",      AssetPrefabRegistry.CoastBushes },
                { "Ferns",            AssetPrefabRegistry.Ferns },
                { "Flowers",          AssetPrefabRegistry.Flowers },
                { "Mushrooms",        AssetPrefabRegistry.Mushrooms },
                { "TreeLogs",         AssetPrefabRegistry.TreeLogs },
                { "Twigs",            AssetPrefabRegistry.Twigs },
                { "TreeTrunks",       AssetPrefabRegistry.TreeTrunks },
                { "RockSmall",        AssetPrefabRegistry.RockSmall },
                { "RockLarge",        AssetPrefabRegistry.RockLarge },
                { "RockMedium",       AssetPrefabRegistry.RockMedium },
                { "CliffFormations",  AssetPrefabRegistry.CliffFormations },
                { "CanyonWalls",      AssetPrefabRegistry.CanyonWalls },
                { "RockFormations",   AssetPrefabRegistry.RockFormations },
                { "RockSharp",        AssetPrefabRegistry.RockSharp },
                { "RockSlabs",        AssetPrefabRegistry.RockSlabs },
                { "RockPavements",    AssetPrefabRegistry.RockPavements },
                { "CaveEntrance",     AssetPrefabRegistry.CaveEntrance },
                { "CanyonOverpass",   AssetPrefabRegistry.CanyonOverpass },
                { "RockClusters",     AssetPrefabRegistry.RockClusters },
                { "CampFires",        AssetPrefabRegistry.CampFires },
                { "Bones",            AssetPrefabRegistry.Bones },
                { "AnimalCarcasses",  AssetPrefabRegistry.AnimalCarcasses },
                { "MaleAvatars",      AssetPrefabRegistry.MaleAvatars },
                { "FemaleAvatars",    AssetPrefabRegistry.FemaleAvatars },
            };

            var seen = new HashSet<string>();

            // Process pool entries (relative paths under Prefabs/)
            foreach (var kvp in allPools)
            {
                foreach (string subPath in kvp.Value)
                {
                    if (!seen.Add(subPath)) continue;

                    string assetPath = BASE + subPath + ".prefab";
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
                    if (prefab != null)
                    {
                        entries.Add(new PrefabDatabase.Entry { key = subPath, prefab = prefab });
                    }
                    else
                    {
                        Debug.LogWarning($"[PrefabDatabaseBuilder] Missing: {assetPath}");
                        missing++;
                    }
                }
            }

            // Process particle entries (full paths starting with Assets/)
            string[] particlePaths = {
                AssetPrefabRegistry.FireParticle,
                AssetPrefabRegistry.FirefliesParticle,
                AssetPrefabRegistry.SunShaftParticle,
                AssetPrefabRegistry.FogParticle,
            };

            foreach (string fullPath in particlePaths)
            {
                if (!seen.Add(fullPath)) continue;

                string assetPath = fullPath + ".prefab";
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
                if (prefab != null)
                {
                    entries.Add(new PrefabDatabase.Entry { key = fullPath, prefab = prefab });
                }
                else
                {
                    Debug.LogWarning($"[PrefabDatabaseBuilder] Missing particle: {assetPath}");
                    missing++;
                }
            }

            // Create or update the ScriptableObject
            var db = AssetDatabase.LoadAssetAtPath<PrefabDatabase>(DB_PATH);
            if (db == null)
            {
                db = ScriptableObject.CreateInstance<PrefabDatabase>();
                // Ensure directory exists
                if (!AssetDatabase.IsValidFolder("Assets/Terranova/Resources"))
                    AssetDatabase.CreateFolder("Assets/Terranova", "Resources");
                AssetDatabase.CreateAsset(db, DB_PATH);
            }

            db.SetEntries(entries.ToArray());
            EditorUtility.SetDirty(db);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[PrefabDatabaseBuilder] ✓ Built PrefabDatabase: {entries.Count} prefabs registered, {missing} missing.");
            if (missing > 0)
                Debug.LogWarning($"[PrefabDatabaseBuilder] {missing} prefabs could not be found. Check the Explorer Stone Age asset pack.");
        }

        /// <summary>
        /// Auto-rebuild before every build so iOS builds always have the latest prefab refs.
        /// </summary>
        private class PreBuildStep : UnityEditor.Build.IPreprocessBuildWithReport
        {
            public int callbackOrder => 0;

            public void OnPreprocessBuild(UnityEditor.Build.Reporting.BuildReport report)
            {
                Debug.Log("[PrefabDatabaseBuilder] Auto-rebuilding PrefabDatabase before build...");
                RebuildDatabase();
            }
        }
    }
}
