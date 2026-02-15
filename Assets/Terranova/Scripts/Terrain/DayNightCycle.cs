using UnityEngine;
using Terranova.Core;

namespace Terranova.Terrain
{
    /// <summary>
    /// Visual day/night cycle with lighting transitions.
    /// MS4 Feature 1.5: Day-Night Cycle.
    /// ~3 game-minutes per day (180 seconds of game time).
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

        // Lighting colors
        private static readonly Color DAY_AMBIENT = new Color(0.75f, 0.78f, 0.82f);
        private static readonly Color NIGHT_AMBIENT = new Color(0.08f, 0.08f, 0.15f);
        private static readonly Color DAWN_AMBIENT = new Color(0.6f, 0.4f, 0.3f);
        private static readonly Color DUSK_AMBIENT = new Color(0.55f, 0.3f, 0.25f);
        private static readonly Color DAY_SUN = new Color(1f, 0.96f, 0.84f);
        private static readonly Color NIGHT_SUN = new Color(0.15f, 0.15f, 0.3f);
        private static readonly Color DAWN_SUN = new Color(1f, 0.6f, 0.3f);

        public int DayCount => _dayCount;
        public float TimeOfDay => _timeOfDay;
        public float Temperature => _temperature;
        public bool IsNight => _timeOfDay < 0.22f || _timeOfDay > 0.78f;
        public bool IsDawn => _timeOfDay >= 0.22f && _timeOfDay < 0.30f;
        public bool IsDusk => _timeOfDay >= 0.70f && _timeOfDay < 0.78f;

        /// <summary>Visibility range multiplier (1.0 day, 0.3 night).</summary>
        public float VisibilityMultiplier => IsNight ? 0.3f : 1f;

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

            // Sun angle: 0 at midnight, 90 at noon
            float sunAngle = (_timeOfDay - 0.25f) * 360f;
            _sunLight.transform.rotation = Quaternion.Euler(sunAngle, -30f, 0f);

            // Sun color and intensity
            Color sunColor;
            float intensity;
            Color ambient;

            if (IsNight)
            {
                sunColor = NIGHT_SUN;
                intensity = 0.1f;
                ambient = NIGHT_AMBIENT;
            }
            else if (IsDawn)
            {
                float t = (_timeOfDay - 0.22f) / 0.08f;
                sunColor = Color.Lerp(NIGHT_SUN, DAWN_SUN, t);
                intensity = Mathf.Lerp(0.1f, 0.8f, t);
                ambient = Color.Lerp(NIGHT_AMBIENT, DAWN_AMBIENT, t);
            }
            else if (IsDusk)
            {
                float t = (_timeOfDay - 0.70f) / 0.08f;
                sunColor = Color.Lerp(DAY_SUN, DUSK_AMBIENT, t);
                intensity = Mathf.Lerp(1.2f, 0.3f, t);
                ambient = Color.Lerp(DAY_AMBIENT, DUSK_AMBIENT, t);
            }
            else
            {
                // Daytime
                float dawnEnd = 0.30f;
                float duskStart = 0.70f;
                if (_timeOfDay < 0.35f)
                {
                    float t = (_timeOfDay - dawnEnd) / 0.05f;
                    sunColor = Color.Lerp(DAWN_SUN, DAY_SUN, t);
                    intensity = Mathf.Lerp(0.8f, 1.2f, t);
                    ambient = Color.Lerp(DAWN_AMBIENT, DAY_AMBIENT, t);
                }
                else if (_timeOfDay > 0.65f)
                {
                    float t = (_timeOfDay - 0.65f) / 0.05f;
                    sunColor = Color.Lerp(DAY_SUN, DUSK_AMBIENT, t);
                    intensity = Mathf.Lerp(1.2f, 0.8f, t);
                    ambient = Color.Lerp(DAY_AMBIENT, DUSK_AMBIENT, t);
                }
                else
                {
                    sunColor = DAY_SUN;
                    intensity = 1.2f;
                    ambient = DAY_AMBIENT;
                }
            }

            _sunLight.color = sunColor;
            _sunLight.intensity = intensity;
            RenderSettings.ambientLight = ambient;

            // Fog for night visibility reduction
            RenderSettings.fog = IsNight;
            if (IsNight)
            {
                RenderSettings.fogColor = NIGHT_AMBIENT;
                RenderSettings.fogMode = FogMode.Linear;
                RenderSettings.fogStartDistance = 20f;
                RenderSettings.fogEndDistance = 80f;
            }
        }

        private void UpdateTemperature()
        {
            // Simplified temperature: warmer midday, cooler at night
            float basetemp = 18f;
            if (IsNight) _temperature = basetemp - 8f;
            else if (IsDawn || IsDusk) _temperature = basetemp - 3f;
            else _temperature = basetemp + 4f;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
