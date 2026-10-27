using UnityEngine;
using Zombie.Core;

namespace Zombie.World
{
    public enum HitMaterial
    {
        Wood,
        Stone,
        Ore,
    }

    /// <summary>
    /// Springy reaction of a resource node to a tool hit: it wobbles away from the hit around its
    /// base, squashes for a moment and emits chips (plus leaves for trees). Sleeps when settled.
    /// </summary>
    public sealed class HitReaction : MonoBehaviour
    {
        [SerializeField] HitMaterial _material = HitMaterial.Wood;
        [SerializeField] float _wobbleImpulse = 55f;
        [SerializeField] float _frequency = 1.9f;
        [SerializeField] float _damping = 0.16f;
        [SerializeField] float _squashImpulse = 1.1f;
        [SerializeField] float _canopyHeight;
        [SerializeField] float _canopyRadius;

        Spring _angle;
        Spring _squash;
        Vector3 _axis = Vector3.right;
        Quaternion _baseRotation;
        Vector3 _baseScale;

        public void Configure(HitMaterial material, float wobbleImpulse, float canopyHeight, float canopyRadius)
        {
            _material = material;
            _wobbleImpulse = wobbleImpulse;
            _canopyHeight = canopyHeight;
            _canopyRadius = canopyRadius;
        }

        void Awake()
        {
            _baseRotation = transform.rotation;
            _baseScale = transform.localScale;
            enabled = false;
        }

        public void Hit(Vector3 point, Vector3 direction)
        {
            Vector3 flat = Vector3.ProjectOnPlane(direction, Vector3.up);
            if (flat.sqrMagnitude < 0.0001f)
                flat = transform.forward;
            _axis = Vector3.Cross(Vector3.up, flat.normalized);
            _angle.Velocity += _wobbleImpulse;
            _squash.Velocity += _squashImpulse;

            if (HitEffects.Instance != null)
            {
                Vector3 normal = -flat.normalized;
                HitEffects.Instance.PlayImpact(_material, point, normal);
                if (_canopyRadius > 0f)
                    HitEffects.Instance.PlayLeaves(transform.position + Vector3.up * (_canopyHeight * _baseScale.y), _canopyRadius * _baseScale.x);
            }
            enabled = true;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            _angle.Step(0f, _frequency, _damping, dt);
            _squash.Step(0f, _frequency * 2.2f, 0.3f, dt);

            transform.rotation = Quaternion.AngleAxis(_angle.Value, _axis) * _baseRotation;
            float sq = _squash.Value * 0.05f;
            transform.localScale = new Vector3(_baseScale.x * (1f + sq), _baseScale.y * (1f - sq), _baseScale.z * (1f + sq));

            if (_angle.IsSettled(0f, 0.02f) && _squash.IsSettled(0f, 0.002f))
            {
                transform.rotation = _baseRotation;
                transform.localScale = _baseScale;
                _angle = default;
                _squash = default;
                enabled = false;
            }
        }
    }
}
