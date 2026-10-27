using System;
using UnityEngine;
using Zombie.Core;

namespace Zombie.World
{
    /// <summary>
    /// ~7 minute cycle: day for gathering and building, night for the horde.
    /// Drives sun/moon, ambient light, fog and sky. Raises events other systems listen to.
    /// </summary>
    public sealed class DayNightCycle : MonoBehaviour
    {
        public int Day { get; private set; } = 1;
        public bool IsNight { get; private set; }
        /// <summary>Seconds until the current phase (day or night) ends.</summary>
        public float PhaseTimeLeft { get; private set; }
        /// <summary>1 at noon, 0 at night, blends at dusk/dawn.</summary>
        public float Daylight { get; private set; } = 1f;

        public event Action<int> NightStarted;
        public event Action<int> DayStarted;

        GameConfig _config;
        Light _sun;
        Light _moon;
        Material _sky;
        Camera _camera;
        float _time; // seconds into the current cycle

        static readonly Color DayAmbient = new Color(0.46f, 0.47f, 0.44f);
        static readonly Color NightAmbient = new Color(0.11f, 0.13f, 0.19f);
        static readonly Color DayFog = new Color(0.62f, 0.64f, 0.58f);
        static readonly Color DuskFog = new Color(0.55f, 0.42f, 0.34f);
        static readonly Color NightFog = new Color(0.06f, 0.08f, 0.11f);
        static readonly Color SunColor = new Color(1f, 0.92f, 0.78f);
        static readonly Color DuskSunColor = new Color(1f, 0.6f, 0.38f);

        float DayLength => _config.cycleSeconds * _config.dayFraction;
        float NightLength => _config.cycleSeconds - DayLength;

        public void Init(GameConfig config, Camera cam)
        {
            _config = config;
            _camera = cam;
            _time = DayLength * 0.08f; // start in the morning

            _sun = CreateLight("Sun", SunColor, 1.15f, LightShadows.Soft);
            _moon = CreateLight("Moon", new Color(0.55f, 0.65f, 0.9f), 0.32f, LightShadows.None);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.sun = _sun;

            if (RenderSettings.skybox != null)
            {
                _sky = new Material(RenderSettings.skybox);
                RenderSettings.skybox = _sky;
            }
            else if (_camera != null)
            {
                _camera.clearFlags = CameraClearFlags.SolidColor;
            }

            Apply();
        }

        Light CreateLight(string name, Color color, float intensity, LightShadows shadows)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = color;
            light.intensity = intensity;
            light.shadows = shadows;
            light.shadowStrength = 0.75f;
            return light;
        }

        void Update()
        {
            _time += Time.deltaTime;
            if (_time >= _config.cycleSeconds)
            {
                _time -= _config.cycleSeconds;
                Day++;
            }
            Apply();
        }

        void Apply()
        {
            bool night = _time >= DayLength;
            if (night != IsNight)
            {
                IsNight = night;
                if (night) NightStarted?.Invoke(Day);
                else DayStarted?.Invoke(Day);
            }

            PhaseTimeLeft = night ? _config.cycleSeconds - _time : DayLength - _time;

            // Daylight blends over 12% of the day at both ends.
            float dayT = Mathf.Clamp01(_time / DayLength);
            float edge = 0.12f;
            float daylight = night ? 0f : Mathf.Clamp01(Mathf.Min(dayT, 1f - dayT) / edge);
            Daylight = daylight;

            // Sun arcs east to west during the day; moon hangs fixed and fades in at night.
            float sunAngle = Mathf.Lerp(8f, 172f, dayT);
            _sun.transform.rotation = Quaternion.Euler(sunAngle, -35f, 0f);
            _sun.intensity = 1.15f * daylight;
            _sun.color = Color.Lerp(DuskSunColor, SunColor, daylight);
            _sun.enabled = daylight > 0.01f;

            float nightAmount = 1f - daylight;
            _moon.transform.rotation = Quaternion.Euler(50f, 140f, 0f);
            _moon.intensity = 0.32f * nightAmount;
            _moon.enabled = nightAmount > 0.01f;

            RenderSettings.ambientLight = Color.Lerp(NightAmbient, DayAmbient, daylight);
            var fog = daylight > 0.5f
                ? Color.Lerp(DuskFog, DayFog, (daylight - 0.5f) * 2f)
                : Color.Lerp(NightFog, DuskFog, daylight * 2f);
            RenderSettings.fogColor = fog;
            RenderSettings.fogStartDistance = Mathf.Lerp(12f, 30f, daylight);
            RenderSettings.fogEndDistance = Mathf.Lerp(65f, 150f, daylight);

            if (_sky != null)
            {
                _sky.SetFloat("_Exposure", Mathf.Lerp(0.08f, 1.0f, daylight));
                _sky.SetColor("_SkyTint", Color.Lerp(new Color(0.3f, 0.32f, 0.4f), new Color(0.55f, 0.55f, 0.5f), daylight));
            }
            else if (_camera != null)
            {
                _camera.backgroundColor = fog;
            }
        }
    }
}
