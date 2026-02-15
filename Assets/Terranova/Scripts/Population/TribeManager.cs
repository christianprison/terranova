using UnityEngine;
using Terranova.Core;
using Terranova.Buildings;
using Terranova.Discovery;
using Terranova.Terrain;

namespace Terranova.Population
{
    /// <summary>
    /// Manages tribe death and restart mechanics.
    /// Feature 6.4: Tribe Death &amp; Restart.
    ///
    /// When all settlers die:
    ///   - Short pause ("The tribe has perished...")
    ///   - After 5 seconds: 5 new settlers arrive at same terrain
    ///   - Terrain preserved (paths, clearings, pits, fireplaces)
    ///   - Structures preserved (slowly decaying)
    ///   - Tools on ground: pickable by new settlers
    ///   - Discoveries LOST - must rediscover everything
    ///   - Food partially spoiled
    ///   - Chronicle: new chapter
    /// </summary>
    public class TribeManager : MonoBehaviour
    {
        public static TribeManager Instance { get; private set; }

        private const float RESTART_DELAY = 5f;

        private int _tribeNumber = 1;
        private bool _isRestarting;
        private float _restartTimer;
        private bool _hasFired;

        /// <summary>Current tribe number (increments on restart).</summary>
        public int TribeNumber => _tribeNumber;

        /// <summary>Whether a restart is in progress.</summary>
        public bool IsRestarting => _isRestarting;

        /// <summary>Time remaining before new tribe arrives.</summary>
        public float RestartTimeRemaining => _restartTimer;

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
            EventBus.Subscribe<PopulationChangedEvent>(OnPopulationChanged);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<PopulationChangedEvent>(OnPopulationChanged);
        }

        private void Update()
        {
            if (!_isRestarting) return;

            _restartTimer -= Time.unscaledDeltaTime;
            if (_restartTimer <= 0f)
            {
                _isRestarting = false;
                SpawnNewTribe();
            }
        }

        private void OnPopulationChanged(PopulationChangedEvent evt)
        {
            if (evt.CurrentPopulation <= 0 && !_isRestarting && _hasFired)
            {
                StartRestart();
            }

            // Track that we've had settlers (don't trigger restart before first spawn)
            if (evt.CurrentPopulation > 0)
                _hasFired = true;
        }

        private void StartRestart()
        {
            _isRestarting = true;
            _restartTimer = RESTART_DELAY;

            int discoveriesLost = 0;
            var dsm = DiscoveryStateManager.Instance;
            if (dsm != null)
                discoveriesLost = dsm.CompletedCount;

            EventBus.Publish(new TribePerishedEvent
            {
                DayCount = GameState.DayCount,
                DiscoveriesLost = discoveriesLost
            });

            Debug.Log($"[TribeManager] Tribe {_tribeNumber} has perished. New tribe in {RESTART_DELAY}s.");
        }

        private void SpawnNewTribe()
        {
            _tribeNumber++;

            // Reset discoveries (must rediscover everything)
            ResetDiscoveries();

            // Partially spoil food
            var rm = ResourceManager.Instance;
            if (rm != null)
                rm.SpoilFood(0.6f); // 60% of food spoils

            // Spawn new settlers at campfire
            SpawnSettlersAtCampfire();

            // Resume time
            Time.timeScale = 1f;

            EventBus.Publish(new NewTribeArrivedEvent
            {
                TribeNumber = _tribeNumber
            });

            Debug.Log($"[TribeManager] New tribe #{_tribeNumber} has arrived!");
        }

        private void ResetDiscoveries()
        {
            // Destroy and recreate discovery system
            var dsm = DiscoveryStateManager.Instance;
            if (dsm != null)
            {
                Object.Destroy(dsm.gameObject);
            }

            // Re-create discovery system (will be fresh)
            var go = new GameObject("DiscoverySystem");
            go.AddComponent<ActivityTracker>();
            go.AddComponent<DiscoveryStateManager>();
            go.AddComponent<DiscoveryEngine>();
            go.AddComponent<DiscoveryRegistry>();
            go.AddComponent<DiscoveryEffectsManager>();

            Debug.Log("[TribeManager] Discoveries reset. Must rediscover everything.");
        }

        private void SpawnSettlersAtCampfire()
        {
            var world = WorldManager.Instance;
            if (world == null) return;

            // Find the campfire
            Vector3 campfirePos = Vector3.zero;
            var buildings = Object.FindObjectsByType<Building>(FindObjectsSortMode.None);
            foreach (var b in buildings)
            {
                if (b.Definition != null && b.Definition.Type == BuildingType.Campfire)
                {
                    campfirePos = b.transform.position;
                    break;
                }
            }

            if (campfirePos == Vector3.zero)
            {
                // Fallback: world center
                int cx = world.WorldBlocksX / 2;
                int cz = world.WorldBlocksZ / 2;
                float y = world.GetSmoothedHeightAtWorldPos(cx + 0.5f, cz + 0.5f);
                campfirePos = new Vector3(cx + 0.5f, y, cz + 0.5f);
            }

            int count = 5;
            float spawnRadius = 3f;
            float angleStep = 360f / count;

            // Assign traits without duplicates
            var availableTraits = new System.Collections.Generic.List<SettlerTrait>(
                (SettlerTrait[])System.Enum.GetValues(typeof(SettlerTrait)));

            for (int i = 0; i < count; i++)
            {
                float angle = i * angleStep * Mathf.Deg2Rad;
                float x = campfirePos.x + Mathf.Cos(angle) * spawnRadius;
                float z = campfirePos.z + Mathf.Sin(angle) * spawnRadius;
                float sy = world.GetSmoothedHeightAtWorldPos(x, z);

                var settlerObj = new GameObject($"Settler_{i}");
                settlerObj.transform.position = new Vector3(x, sy, z);

                var settler = settlerObj.AddComponent<Settler>();
                settler.Initialize(i, campfirePos);

                // Assign unique trait
                if (availableTraits.Count > 0)
                {
                    int traitIdx = Random.Range(0, availableTraits.Count);
                    settler.SetTrait(availableTraits[traitIdx]);
                    availableTraits.RemoveAt(traitIdx);
                }
            }

            EventBus.Publish(new PopulationChangedEvent
            {
                CurrentPopulation = count
            });
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }
    }
}
