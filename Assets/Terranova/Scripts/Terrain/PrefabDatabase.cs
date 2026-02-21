using System;
using System.Collections.Generic;
using UnityEngine;

namespace Terranova.Terrain
{
    /// <summary>
    /// ScriptableObject that holds direct references to Explorer Stone Age prefabs.
    /// Unity serializes these references into the build, so they load on iOS/standalone
    /// without needing AssetDatabase or a Resources/ folder for each prefab.
    ///
    /// The asset lives at Assets/Terranova/Resources/PrefabDatabase.asset and is
    /// loaded via Resources.Load at runtime.
    ///
    /// Populated automatically by the Editor menu:
    ///   Terranova > Rebuild Prefab Database
    /// </summary>
    [CreateAssetMenu(fileName = "PrefabDatabase", menuName = "Terranova/Prefab Database")]
    public class PrefabDatabase : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public string key;
            public GameObject prefab;
        }

        [SerializeField] private Entry[] _entries = Array.Empty<Entry>();

        // Runtime lookup built on first access
        private Dictionary<string, GameObject> _lookup;

        /// <summary>
        /// Find a prefab by its registry key (e.g. "Vegetation/Trees/Pine_Tree_1A"
        /// or "Assets/EXPLORER - Stone Age/Particles/Fire_1A").
        /// </summary>
        public GameObject Get(string key)
        {
            if (_lookup == null) BuildLookup();
            _lookup.TryGetValue(key, out var prefab);
            return prefab;
        }

        private void BuildLookup()
        {
            _lookup = new Dictionary<string, GameObject>(_entries.Length);
            foreach (var e in _entries)
            {
                if (!string.IsNullOrEmpty(e.key) && e.prefab != null)
                    _lookup[e.key] = e.prefab;
            }
        }

        /// <summary>Number of entries in the database.</summary>
        public int Count => _entries.Length;

        // Called by the editor rebuild script
        public void SetEntries(Entry[] entries)
        {
            _entries = entries;
            _lookup = null;
        }

        private static PrefabDatabase _instance;

        /// <summary>
        /// Load the singleton PrefabDatabase from Resources.
        /// Returns null if the asset doesn't exist (editor-only workflow).
        /// </summary>
        public static PrefabDatabase Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = Resources.Load<PrefabDatabase>("PrefabDatabase");
                    if (_instance == null)
                        Debug.LogError("[PrefabDatabase] Not found at Resources/PrefabDatabase! " +
                                       "Run 'Terranova > Rebuild Prefab Database' in the Unity Editor.");
                }
                return _instance;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatic() { _instance = null; }
    }
}
