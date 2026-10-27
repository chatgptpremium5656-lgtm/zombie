using System;
using UnityEngine;

namespace Zombie.Core
{
    /// <summary>Anything that can be hit: players, zombies, building pieces, spikes.</summary>
    public interface IDamageable
    {
        bool IsAlive { get; }
        void TakeDamage(float amount, GameObject source);
    }

    /// <summary>Player health. Health is the only survival stat (no hunger/thirst for now).</summary>
    public sealed class Health : MonoBehaviour, IDamageable
    {
        public float Max { get; private set; } = 100f;
        public float Current { get; private set; } = 100f;
        public bool IsAlive => Current > 0f;
        public float Normalized => Max > 0f ? Current / Max : 0f;

        public event Action<float> Damaged;
        public event Action Died;

        float _regenDelay = 8f;
        float _regenPerSecond = 1.5f;
        float _lastDamageTime = -999f;

        public void Configure(float max, float regenDelay, float regenPerSecond)
        {
            Max = max;
            Current = max;
            _regenDelay = regenDelay;
            _regenPerSecond = regenPerSecond;
        }

        public void TakeDamage(float amount, GameObject source)
        {
            if (!IsAlive || amount <= 0f) return;
            Current = Mathf.Max(0f, Current - amount);
            _lastDamageTime = Time.time;
            Damaged?.Invoke(amount);
            if (Current <= 0f) Died?.Invoke();
        }

        public void Heal(float amount)
        {
            if (!IsAlive) return;
            Current = Mathf.Min(Max, Current + amount);
        }

        public void Revive()
        {
            Current = Max;
            _lastDamageTime = -999f;
        }

        void Update()
        {
            if (IsAlive && Current < Max && Time.time - _lastDamageTime > _regenDelay)
            {
                Current = Mathf.Min(Max, Current + _regenPerSecond * Time.deltaTime);
            }
        }
    }
}
