using UnityEngine;

namespace Zombie.Rendering
{
    /// <summary>
    /// Pushes the global look values used by Zombie/Toon and Zombie/SkyGradient:
    /// hemispheric ambient (sky/ground), wind, grass fade distance and sun direction.
    /// One per scene. Runs in edit mode so the scene looks right without Play.
    /// </summary>
    [ExecuteAlways]
    public sealed class StylizedAtmosphere : MonoBehaviour
    {
        static readonly int AmbientSkyId = Shader.PropertyToID("_ZAmbientSky");
        static readonly int AmbientGroundId = Shader.PropertyToID("_ZAmbientGround");
        static readonly int WindId = Shader.PropertyToID("_ZWind");
        static readonly int GrassFadeId = Shader.PropertyToID("_ZGrassFade");
        static readonly int SunDirId = Shader.PropertyToID("_ZSunDir");

        [SerializeField] Light _sun;
        [SerializeField, ColorUsage(false, true)] Color _ambientSky = new Color(0.55f, 0.62f, 0.78f);
        [SerializeField, ColorUsage(false, true)] Color _ambientGround = new Color(0.42f, 0.38f, 0.33f);
        [SerializeField] Vector2 _windDirection = new Vector2(1f, 0.35f);
        [SerializeField, Range(0f, 4f)] float _windSpeed = 1.3f;
        [SerializeField, Range(0f, 1f)] float _gustStrength = 0.35f;
        [SerializeField] float _grassFadeStart = 22f;
        [SerializeField] float _grassFadeEnd = 32f;

        public float GrassFadeEnd => _grassFadeEnd;

        void OnEnable() => Apply();

        void OnValidate() => Apply();

        void Update()
        {
            // Sun direction can change at runtime (day/night later); the rest is cheap enough to keep in sync.
            if (_sun != null)
                Shader.SetGlobalVector(SunDirId, -_sun.transform.forward);
        }

        void Apply()
        {
            Shader.SetGlobalColor(AmbientSkyId, _ambientSky.linear);
            Shader.SetGlobalColor(AmbientGroundId, _ambientGround.linear);
            Vector2 dir = _windDirection.sqrMagnitude > 0f ? _windDirection.normalized : Vector2.right;
            Shader.SetGlobalVector(WindId, new Vector4(dir.x, dir.y, _windSpeed, _gustStrength));
            Shader.SetGlobalVector(GrassFadeId, new Vector4(_grassFadeStart, _grassFadeEnd, 0f, 0f));
            if (_sun != null)
                Shader.SetGlobalVector(SunDirId, -_sun.transform.forward);
        }
    }
}
