using UnityEngine;

namespace Zombie.World
{
    /// <summary>Shared pooled particle bursts for tool hits (chips, dust, leaves). Emits only, never instantiates.</summary>
    public sealed class HitEffects : MonoBehaviour
    {
        [SerializeField] ParticleSystem _chips;
        [SerializeField] ParticleSystem _dust;
        [SerializeField] ParticleSystem _leaves;
        [SerializeField] Color _woodColor = new Color(0.9f, 0.74f, 0.5f);
        [SerializeField] Color _stoneColor = new Color(0.7f, 0.69f, 0.72f);
        [SerializeField] Color _oreColor = new Color(0.95f, 0.6f, 0.25f);
        [SerializeField] Color _leafA = new Color(0.36f, 0.66f, 0.22f);
        [SerializeField] Color _leafB = new Color(0.66f, 0.82f, 0.3f);

        public static HitEffects Instance { get; private set; }

        public void Setup(ParticleSystem chips, ParticleSystem dust, ParticleSystem leaves)
        {
            _chips = chips;
            _dust = dust;
            _leaves = leaves;
        }

        void OnEnable() => Instance = this;

        void OnDisable()
        {
            if (Instance == this)
                Instance = null;
        }

        public void PlayImpact(HitMaterial material, Vector3 point, Vector3 normal)
        {
            Color color = material == HitMaterial.Wood ? _woodColor : material == HitMaterial.Ore ? _oreColor : _stoneColor;
            var p = new ParticleSystem.EmitParams { applyShapeToPosition = false };
            int chips = material == HitMaterial.Wood ? 9 : 7;
            for (int i = 0; i < chips; i++)
            {
                p.position = point + normal * 0.05f;
                p.velocity = normal * Random.Range(1.2f, 2.6f) + Random.insideUnitSphere * 1.4f + Vector3.up * Random.Range(1.5f, 2.8f);
                float size = Random.Range(0.025f, 0.055f);
                p.startSize3D = new Vector3(size * 1.4f, size * 0.55f, size);
                p.rotation3D = new Vector3(Random.Range(0f, 360f), Random.Range(0f, 360f), Random.Range(0f, 360f));
                p.angularVelocity3D = Random.insideUnitSphere * 720f;
                Color c = material == HitMaterial.Ore && i % 2 == 0 ? _stoneColor : color;
                p.startColor = Color.Lerp(c, Color.white, Random.Range(0f, 0.15f));
                p.startLifetime = Random.Range(0.9f, 1.4f);
                _chips.Emit(p, 1);
            }

            var d = new ParticleSystem.EmitParams { applyShapeToPosition = false };
            for (int i = 0; i < 4; i++)
            {
                d.position = point + normal * 0.1f + Random.insideUnitSphere * 0.1f;
                d.velocity = normal * Random.Range(0.4f, 0.9f) + Random.insideUnitSphere * 0.4f + Vector3.up * 0.3f;
                d.startSize = Random.Range(0.25f, 0.45f);
                d.startColor = Color.Lerp(color, Color.white, 0.55f);
                _dust.Emit(d, 1);
            }
        }

        public void PlayLeaves(Vector3 center, float radius)
        {
            var p = new ParticleSystem.EmitParams { applyShapeToPosition = false };
            for (int i = 0; i < 16; i++)
            {
                Vector3 offset = Random.insideUnitSphere * radius * 0.85f;
                offset.y *= 0.6f;
                p.position = center + offset;
                p.velocity = offset.normalized * Random.Range(0.4f, 1.4f) + Vector3.down * 0.2f;
                p.startSize = Random.Range(0.14f, 0.24f);
                p.rotation = Random.Range(0f, 360f);
                p.angularVelocity = Random.Range(-220f, 220f);
                p.startColor = Color.Lerp(_leafA, _leafB, Random.value);
                p.startLifetime = Random.Range(2.2f, 3.4f);
                _leaves.Emit(p, 1);
            }
        }
    }
}
