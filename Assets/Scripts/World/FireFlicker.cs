using UnityEngine;

namespace Zombie.World
{
    /// <summary>Organic campfire light: layered noise on intensity and a small position wander.</summary>
    [RequireComponent(typeof(Light))]
    public sealed class FireFlicker : MonoBehaviour
    {
        [SerializeField] float _baseIntensity = 1f;
        [SerializeField] float _flicker = 0.28f;
        [SerializeField] float _speed = 7f;
        [SerializeField] float _wander = 0.06f;

        Light _light;
        Vector3 _basePosition;
        float _seed;

        void Awake()
        {
            _light = GetComponent<Light>();
            _basePosition = transform.localPosition;
            _seed = Random.value * 100f;
        }

        void Update()
        {
            float t = Time.time * _speed + _seed;
            float n = Mathf.PerlinNoise(t, 0.37f) * 0.7f + Mathf.PerlinNoise(t * 2.3f, 4.1f) * 0.3f;
            _light.intensity = _baseIntensity * (1f + (n - 0.5f) * 2f * _flicker);
            transform.localPosition = _basePosition + new Vector3(
                (Mathf.PerlinNoise(t * 0.5f, 9.2f) - 0.5f) * _wander,
                (Mathf.PerlinNoise(t * 0.5f, 3.3f) - 0.5f) * _wander,
                (Mathf.PerlinNoise(t * 0.5f, 6.7f) - 0.5f) * _wander);
        }
    }
}
