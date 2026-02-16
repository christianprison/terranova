using UnityEngine;
using Terranova.Core;

namespace Terranova.Terrain
{
    /// <summary>
    /// Visual day/night cycle driven by a smooth sun arc.
    /// MS4 Feature 1.5: Day-Night Cycle.
    /// ~3 game-minutes per day (180 seconds of game time).
    ///
    /// The directional light rotates 360° over one full day:
    ///   Dawn (east, warm orange) → Noon (overhead, bright white)
    ///   → Dusk (west, warm red) → Night (below horizon, dark blue ambient only)
    ///
    /// Sun altitude (0° horizon, 90° zenith, negative = below horizon) drives
    /// all lighting smoothly — no hard-coded phase thresholds.
    /// </summary>
    public class DayNightCycle : MonoBehaviour
    {
        public static DayNightCycle Instance { get; private set; }

        // 180 game-seconds = 1 full day cycle (3 minutes at 1x speed)
        public const float SECONDS_PER_DAY = 180f;

        private Light _sunLight;
        private float _timeOfDay; // 0-1, where 0.25=sunrise, 0.5=noon, 0.75=sunset
        private int _dayCount = 1;
        private float _temperature = 20f; // Celsius, simplified

        // Sun arc color stops
        private static readonly Color SUN_NOON    = new Color(1f, 0.96f, 0.84f);     // Warm white
        private static readonly Color SUN_HORIZON = new Color(1f, 0.45f, 0.15f);     // Deep orange
        private static readonly Color SUN_NIGHT   = new Color(0.05f, 0.05f, 0.12f);  // Near-black

        // Ambient color stops
        private static readonly Color AMB_DAY     = new Color(0.75f, 0.78f, 0.82f);
        private static readonly Color AMB_HORIZON = new Color(0.45f, 0.28f, 0.20f);  // Warm orange-brown
        private static readonly Color AMB_NIGHT   = new Color(0.06f, 0.06f, 0.14f);  // Dark blue

        public int DayCount => _dayCount;
        public float TimeOfDay => _timeOfDay;
        public float Temperature => _temperature;

        /// <summary>Sun altitude: 0 at horizon, positive above, negative below.</summary>
        public float SunAltitude => Mathf.Sin((_timeOfDay - 0.25f) * Mathf.PI * 2f) * 90f;

        public bool IsNight => SunAltitude < -5f;
        public bool IsDawn => _timeOfDay >= 0.20f && _timeOfDay < 0.30f;
        public bool IsDusk => _timeOfDay >= 0.70f && _timeOfDay < 0.80f;

        /// <summary>Visibility range multiplier (smooth).</summary>
        public float VisibilityMultiplier => Mathf.Clamp01(Mathf.InverseLerp(-10f, 10f, SunAltitude));

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            _timeOfDay = 0.30f; // Start at early morning
        }

        private void Start()
        {
            CreateSunLight();
        }

        private void Update()
        {
            _timeOfDay += Time.deltaTime / SECONDS_PER_DAY;
            if (_timeOfDay >= 1f)
            {
                _timeOfDay -= 1f;
                _dayCount++;
                GameState.DayCount = _dayCount;
                EventBus.Publish(new DayChangedEvent { DayCount = _dayCount });
            }

            GameState.GameTimeSeconds += Time.deltaTime;
            UpdateLighting();
            UpdateTemperature();
        }

        private void CreateSunLight()
        {
            var existing = FindFirstObjectByType<Light>();
            if (existing != null && existing.type == LightType.Directional)
            {
                _sunLight = existing;
            }
            else
            {
                var sunGo = new GameObject("Sun");
                _sunLight = sunGo.AddComponent<Light>();
                _sunLight.type = LightType.Directional;
                _sunLight.shadows = LightShadows.Soft;
            }
            _sunLight.intensity = 1.2f;
        }

        private void UpdateLighting()
        {
            if (_sunLight == null) return;

            // ── Sun rotation: full 360° arc ──────────────────────────
            // timeOfDay 0.25 = sunrise (east), 0.5 = noon (overhead),
            // 0.75 = sunset (west), 0.0/1.0 = midnight (below horizon).
            float sunAngle = (_timeOfDay - 0.25f) * 360f;
            _sunLight.transform.rotation = Quaternion.Euler(sunAngle, -30f, 0f);

            // ── Sun altitude drives everything smoothly ──────────────
            // altitude: -90 (midnight) → 0 (sunrise/sunset) → +90 (noon)
            float altitude = SunAltitude;

            // Normalized altitude: 0 = horizon, 1 = zenith
            float altNorm = Mathf.Clamp01(altitude / 90f);

            // How far below horizon (0 = at horizon, 1 = deep night)
            float belowHorizon = Mathf.Clamp01(-altitude / 30f);

            // ── Sun color: horizon→orange, zenith→white, below→dark ──
            Color sunColor;
            float intensity;
            if (altitude > 0f)
            {
                // Above horizon: orange at low angles, white at high
                sunColor = Color.Lerp(SUN_HORIZON, SUN_NOON, altNorm);
                intensity = Mathf.Lerp(0.4f, 1.3f, altNorm);
            }
            else
            {
                // Below horizon: fade to dark
                sunColor = Color.Lerp(SUN_HORIZON, SUN_NIGHT, belowHorizon);
                intensity = Mathf.Lerp(0.4f, 0f, belowHorizon);
            }

            // ── Ambient: smooth blend night→horizon→day ──────────────
            Color ambient;
            if (altitude > 0f)
            {
                ambient = Color.Lerp(AMB_HORIZON, AMB_DAY, altNorm);
            }
            else
            {
                ambient = Color.Lerp(AMB_HORIZON, AMB_NIGHT, belowHorizon);
            }

            _sunLight.color = sunColor;
            _sunLight.intensity = intensity;
            RenderSettings.ambientLight = ambient;

            // ── Fog for night visibility ─────────────────────────────
            bool fogActive = altitude < 5f;
            RenderSettings.fog = fogActive;
            if (fogActive)
            {
                float fogStrength = Mathf.Clamp01(Mathf.InverseLerp(5f, -15f, altitude));
                RenderSettings.fogColor = Color.Lerp(AMB_HORIZON, AMB_NIGHT, fogStrength);
                RenderSettings.fogMode = FogMode.Linear;
                RenderSettings.fogStartDistance = Mathf.Lerp(80f, 20f, fogStrength);
                RenderSettings.fogEndDistance = Mathf.Lerp(200f, 80f, fogStrength);
            }
        }

        private void UpdateTemperature()
        {
            // Temperature follows sun altitude smoothly
            float altitude = SunAltitude;
            float altNorm = Mathf.Clamp01((altitude + 90f) / 180f); // 0=midnight, 1=noon
            _temperature = Mathf.Lerp(8f, 24f, altNorm);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
