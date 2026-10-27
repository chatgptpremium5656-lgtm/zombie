using UnityEngine;
using Zombie.Core;
using Zombie.Logic;

namespace Zombie.Player
{
    /// <summary>
    /// One player in the world: ties together movement, health, inventory and tools,
    /// and owns the death rule (respawn at the sleeping bag, all carried loot lost).
    /// </summary>
    public sealed class PlayerAvatar : MonoBehaviour
    {
        public Inventory Inventory { get; } = new Inventory();
        public Health Health { get; private set; }
        public PlayerMotor Motor { get; private set; }
        public PlayerTools Tools { get; private set; }
        public Camera Camera { get; private set; }

        public bool IsDead { get; private set; }
        public float RespawnTimeLeft { get; private set; }
        public int DeathCount { get; private set; }

        /// <summary>Feet position; zombies compare heights with it to tell whether the player is reachable.</summary>
        public Vector3 Position => transform.position;

        public SleepingBag Bag { get; set; }

        readonly string[] _messages = new string[4];
        readonly float[] _messageTimes = new float[4];
        int _nextMessage;

        Vector3 _spawnPoint;
        GameConfig _config;

        public void Init(GameConfig config, Camera cam, Vector3 spawnPoint)
        {
            _config = config;
            _spawnPoint = spawnPoint;
            Camera = cam;

            Health = gameObject.AddComponent<Health>();
            Health.Configure(config.maxHealth, config.regenDelay, config.regenPerSecond);
            Health.Died += OnDied;

            Motor = gameObject.AddComponent<PlayerMotor>();
            Motor.Init(config, cam);

            Tools = gameObject.AddComponent<PlayerTools>();
            Tools.Init(this);

            Motor.Teleport(spawnPoint, 0f);
        }

        // ------------------------------------------------------------ messages for the HUD

        public void Notify(string message)
        {
            _messages[_nextMessage] = message;
            _messageTimes[_nextMessage] = Time.time;
            _nextMessage = (_nextMessage + 1) % _messages.Length;
        }

        /// <summary>Recent messages, newest first. Returns null when the slot is empty or expired.</summary>
        public string GetMessage(int age, out float alpha)
        {
            int index = (_nextMessage - 1 - age + _messages.Length * 2) % _messages.Length;
            float t = Time.time - _messageTimes[index];
            alpha = Mathf.Clamp01(3f - t);
            return _messages[index] != null && alpha > 0f ? _messages[index] : null;
        }

        // ------------------------------------------------------------ death and respawn

        void OnDied()
        {
            IsDead = true;
            DeathCount++;
            RespawnTimeLeft = _config.respawnSeconds;
            Motor.InputEnabled = false;
            Tools.OnDeath();
            Inventory.ClearOnDeath();
        }

        void Update()
        {
            if (!IsDead) return;
            RespawnTimeLeft -= Time.deltaTime;
            if (RespawnTimeLeft > 0f) return;

            IsDead = false;
            Health.Revive();
            Motor.InputEnabled = true;

            if (Bag != null)
            {
                var t = Bag.transform;
                Motor.Teleport(t.position + Vector3.up * 0.3f + t.right * 0.9f, t.eulerAngles.y);
                Notify("Вы очнулись в спальнике");
            }
            else
            {
                Motor.Teleport(_spawnPoint, 0f);
                Notify("Нет спальника — вы на старте");
            }
        }
    }
}
