using System.Collections.Generic;
using UnityEngine;
using Terranova.Core;

namespace Terranova.Terrain
{
    /// <summary>
    /// v0.5.1: Terrain Deformation system.
    ///
    /// Visual terrain changes from settler activity:
    ///   - Tree felling leaves stump (when wood gathered from tree)
    ///   - Campfire area gradually clears (handled by TrampledPaths)
    ///   - Deformations persist across tribe death
    ///
    /// Future (Q4): digging creates pits, advanced terrain modification.
    /// </summary>
    public class TerrainDeformation : MonoBehaviour
    {
        public static TerrainDeformation Instance { get; private set; }

        private Material _stumpMat;
        private GameObject _stumpContainer;
        private readonly List<Vector3> _stumpPositions = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            _stumpContainer = new GameObject("TreeStumps");
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void OnEnable()
        {
            EventBus.Subscribe<ResourceDepletedEvent>(OnResourceDepleted);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<ResourceDepletedEvent>(OnResourceDepleted);
        }

        private void OnResourceDepleted(ResourceDepletedEvent evt)
        {
            // Create stump for wood resources
            if (evt.Type == ResourceType.Wood)
                CreateStump(evt.Position);
        }

        /// <summary>
        /// Create a small tree stump at the position where wood was gathered.
        /// Stump is a short brown cylinder that persists permanently.
        /// </summary>
        private void CreateStump(Vector3 position)
        {
            if (_stumpMat == null)
                _stumpMat = TerrainShaderLibrary.CreateStumpMaterial();

            var stump = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            stump.name = "Stump";
            stump.transform.SetParent(_stumpContainer.transform, false);
            stump.transform.position = new Vector3(position.x, position.y, position.z);

            // Short, wide cylinder
            float radius = Random.Range(0.12f, 0.20f);
            float height = Random.Range(0.08f, 0.15f);
            stump.transform.localScale = new Vector3(radius * 2f, height, radius * 2f);
            stump.transform.localPosition = new Vector3(position.x, position.y + height, position.z);

            if (_stumpMat != null)
                stump.GetComponent<MeshRenderer>().sharedMaterial = _stumpMat;

            // Disable shadow casting for small object
            stump.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var col = stump.GetComponent<Collider>();
            if (col != null) Object.Destroy(col);

            _stumpPositions.Add(position);
        }

        /// <summary>
        /// Stumps persist across tribe death — no reset needed.
        /// </summary>
        public void OnTribeDeath()
        {
            // Stumps remain — slowly decaying structures would be future work
            Debug.Log($"[TerrainDeformation] Tribe died — {_stumpPositions.Count} stumps persist.");
        }
    }
}
