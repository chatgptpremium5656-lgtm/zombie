using UnityEngine;
using Zombie.Core;

namespace Zombie.Building
{
    /// <summary>Wooden spikes: hurt zombies that walk over them and wear out doing it.</summary>
    public sealed class Spikes : MonoBehaviour, IDamageable
    {
        public const float Radius = 1.3f;
        public const float DamagePerTick = 14f;
        public const float WearPerHit = 3f;

        public float HealthValue { get; private set; } = 150f;
        public bool IsAlive => HealthValue > 0f;

        public void TakeDamage(float amount, GameObject source)
        {
            if (!IsAlive) return;
            HealthValue -= amount;
            if (HealthValue <= 0f)
            {
                if (Game.Horde != null) Game.Horde.UnregisterSpikes(this);
                Destroy(gameObject);
            }
        }

        void OnDestroy()
        {
            if (Game.Horde != null) Game.Horde.UnregisterSpikes(this);
        }
    }
}

namespace Zombie.Player
{
    /// <summary>Respawn point. One per player: placing a new one removes the old.</summary>
    public sealed class SleepingBag : MonoBehaviour
    {
        public PlayerAvatar Owner { get; set; }

        void OnDestroy()
        {
            if (Owner != null && Owner.Bag == this) Owner.Bag = null;
        }
    }
}
