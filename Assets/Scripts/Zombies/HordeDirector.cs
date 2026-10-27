using System.Collections.Generic;
using UnityEngine;
using Zombie.Building;
using Zombie.Core;
using Zombie.Logic;
using Zombie.Player;

namespace Zombie.Zombies
{
    /// <summary>
    /// Spawns a growing horde every night, keeps a few wanderers around by day and ticks every zombie
    /// in a single loop. All zombies are pre-allocated (object pool): no Instantiate/Destroy during play.
    /// </summary>
    public sealed class HordeDirector : MonoBehaviour
    {
        public int Alive { get; private set; }
        public int Night { get; private set; }
        public int RemainingToSpawn => _spawnQueue.Count - _spawnIndex;
        public int Kills { get; private set; }
        public int BestNight { get; private set; }
        public WavePlan CurrentPlan { get; private set; }

        const string BestNightKey = "zombie.bestNight";
        const float GridCell = 2f;
        const int GridSize = 96; // covers 192 m, the whole test map
        static readonly int[] NeighbourOffsets = { -GridSize - 1, -GridSize, -GridSize + 1, -1, 0, 1, GridSize - 1, GridSize, GridSize + 1 };

        GameConfig _config;
        readonly List<ZombieAgent>[] _pools = { new List<ZombieAgent>(), new List<ZombieAgent>(), new List<ZombieAgent>() };
        readonly List<ZombieAgent> _active = new List<ZombieAgent>(256);
        readonly List<ZombieKind> _spawnQueue = new List<ZombieKind>(256);
        readonly List<Spikes> _spikes = new List<Spikes>(32);
        readonly List<ZombieAgent> _pendingRelease = new List<ZombieAgent>(32);
        readonly List<float> _spawnAngles = new List<float>(3);
        int _spawnIndex;
        float _spawnTimer;
        float _spawnInterval;
        float _healthMultiplier = 1f;
        float _spikeTimer;
        float _wanderTimer;

        // Spatial hash for separation and spike hits, rebuilt every frame without allocations.
        readonly int[] _cellHead = new int[GridSize * GridSize];
        int[] _next = new int[256];
        Vector3[] _positions = new Vector3[256];

        public void Init(GameConfig config)
        {
            _config = config;
            BestNight = PlayerPrefs.GetInt(BestNightKey, 0);

            // Pool sizes follow the cap: mostly shamblers, fewer runners, a handful of brutes.
            int cap = Mathf.Max(10, config.maxZombiesAlive);
            Prewarm(ZombieKind.Shambler, cap);
            Prewarm(ZombieKind.Runner, Mathf.CeilToInt(cap * 0.4f));
            Prewarm(ZombieKind.Brute, Mathf.Max(4, cap / 8));

            if (Game.DayNight != null)
            {
                Game.DayNight.NightStarted += OnNightStarted;
                Game.DayNight.DayStarted += OnDayStarted;
            }
        }

        void Prewarm(ZombieKind kind, int count)
        {
            var stats = ZombieStats.All[(int)kind];
            var parent = new GameObject("Pool_" + kind).transform;
            parent.SetParent(transform, false);
            for (int i = 0; i < count; i++)
            {
                var go = new GameObject(kind.ToString());
                go.transform.SetParent(parent, false);
                var agent = go.AddComponent<ZombieAgent>();
                agent.Create(this, stats);
                _pools[(int)kind].Add(agent);
            }
        }

        // ------------------------------------------------------------------ waves

        void OnNightStarted(int night)
        {
            Night = night;
            var plan = WavePlanner.Plan(night, Mathf.Max(1, Game.Players.Count), _config.maxZombiesAlive);
            CurrentPlan = plan;
            _healthMultiplier = plan.HealthMultiplier;

            _spawnQueue.Clear();
            _spawnIndex = 0;
            for (int i = 0; i < plan.Brutes; i++) _spawnQueue.Add(ZombieKind.Brute);
            for (int i = 0; i < plan.Runners; i++) _spawnQueue.Add(ZombieKind.Runner);
            for (int i = 0; i < plan.Shamblers; i++) _spawnQueue.Add(ZombieKind.Shambler);
            // Shuffle so the different kinds arrive mixed.
            for (int i = _spawnQueue.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                var t = _spawnQueue[i];
                _spawnQueue[i] = _spawnQueue[j];
                _spawnQueue[j] = t;
            }

            _spawnAngles.Clear();
            float start = Random.Range(0f, 360f);
            for (int i = 0; i < plan.Directions; i++) _spawnAngles.Add(start + i * (360f / plan.Directions) + Random.Range(-25f, 25f));

            _spawnInterval = Mathf.Max(0.05f, _config.spawnWindowSeconds / Mathf.Max(1, _spawnQueue.Count));
            _spawnTimer = 0f;

            for (int i = 0; i < Game.Players.Count; i++)
                Game.Players[i].Notify("Ночь " + night + ": орда идёт! (" + plan.Total + ")");
        }

        void OnDayStarted(int day)
        {
            // Survived the night: record it, and the leftover horde burns in the sunlight.
            if (Night > 0)
            {
                bool someoneAlive = false;
                for (int i = 0; i < Game.Players.Count; i++) someoneAlive |= !Game.Players[i].IsDead;
                if (someoneAlive && Night > BestNight)
                {
                    BestNight = Night;
                    PlayerPrefs.SetInt(BestNightKey, BestNight);
                    PlayerPrefs.Save();
                }
                for (int i = 0; i < Game.Players.Count; i++)
                    Game.Players[i].Notify("Вы пережили ночь " + Night + "!");
            }

            _spawnQueue.Clear();
            _spawnIndex = 0;
            for (int i = _active.Count - 1; i >= 0; i--) _active[i].Burn();
        }

        void SpawnNext()
        {
            if (_spawnIndex >= _spawnQueue.Count) return;
            if (Alive >= _config.maxZombiesAlive) return;

            var kind = _spawnQueue[_spawnIndex];
            float angle = _spawnAngles.Count > 0 ? _spawnAngles[_spawnIndex % _spawnAngles.Count] : 0f;
            angle += Random.Range(-12f, 12f);
            float radius = _config.mapHalfSize - Random.Range(4f, 8f);
            var pos = Quaternion.Euler(0, angle, 0) * new Vector3(0, 0, radius);
            pos.x = Mathf.Clamp(pos.x, -_config.mapHalfSize + 3f, _config.mapHalfSize - 3f);
            pos.z = Mathf.Clamp(pos.z, -_config.mapHalfSize + 3f, _config.mapHalfSize - 3f);

            if (TrySpawn(kind, pos, _healthMultiplier, false)) _spawnIndex++;
        }

        bool TrySpawn(ZombieKind kind, Vector3 pos, float healthMultiplier, bool wandering)
        {
            var pool = _pools[(int)kind];
            for (int i = 0; i < pool.Count; i++)
            {
                if (pool[i].IsActive) continue;
                pool[i].Spawn(pos, healthMultiplier, wandering);
                _active.Add(pool[i]);
                Alive++;
                return true;
            }
            return false; // pool exhausted: the cap is doing its job
        }

        public void OnZombieKilled(ZombieAgent zombie, GameObject killer)
        {
            Alive = Mathf.Max(0, Alive - 1);
            if (killer == null) return;
            Kills++;
            var player = killer.GetComponentInParent<PlayerAvatar>();
            if (player != null && zombie.Stats.MetalDrop > 0)
            {
                player.Inventory.Add(ResourceType.Metal, zombie.Stats.MetalDrop);
                player.Notify("+" + zombie.Stats.MetalDrop + " металл");
            }
        }

        /// <summary>Returns a zombie to the pool after its death animation. Applied after the tick loop.</summary>
        public void Release(ZombieAgent zombie)
        {
            _pendingRelease.Add(zombie);
        }

        void FlushReleases()
        {
            for (int i = 0; i < _pendingRelease.Count; i++)
            {
                _pendingRelease[i].Despawn();
                _active.Remove(_pendingRelease[i]);
            }
            _pendingRelease.Clear();
        }

        public void RegisterSpikes(Spikes spikes)
        {
            if (!_spikes.Contains(spikes)) _spikes.Add(spikes);
        }

        public void UnregisterSpikes(Spikes spikes)
        {
            _spikes.Remove(spikes);
        }

        // ------------------------------------------------------------------ per-frame

        void Update()
        {
            if (Game.Paused) return;
            float dt = Time.deltaTime;

            if (Game.DayNight != null && Game.DayNight.IsNight && _spawnIndex < _spawnQueue.Count)
            {
                _spawnTimer -= dt;
                while (_spawnTimer <= 0f && _spawnIndex < _spawnQueue.Count && Alive < _config.maxZombiesAlive)
                {
                    SpawnNext();
                    _spawnTimer += _spawnInterval;
                }
            }

            if (Game.DayNight != null && !Game.DayNight.IsNight)
            {
                _wanderTimer -= dt;
                if (_wanderTimer <= 0f)
                {
                    _wanderTimer = 20f;
                    if (Alive < _config.dayWanderers) SpawnWanderer();
                }
            }

            BuildGrid();

            for (int i = 0; i < _active.Count; i++)
            {
                var zombie = _active[i];
                var separation = zombie.IsAlive ? Separation(i) : Vector3.zero;
                zombie.Tick(dt, separation);
            }

            _spikeTimer -= dt;
            if (_spikeTimer <= 0f)
            {
                _spikeTimer = 0.4f;
                TickSpikes();
            }

            FlushReleases();
        }

        void SpawnWanderer()
        {
            float angle = Random.Range(0f, 360f);
            float radius = Random.Range(_config.mapHalfSize * 0.55f, _config.mapHalfSize - 6f);
            TrySpawn(ZombieKind.Shambler, Quaternion.Euler(0, angle, 0) * new Vector3(0, 0, radius), 1f, true);
        }

        void BuildGrid()
        {
            int count = _active.Count;
            if (_next.Length < count)
            {
                _next = new int[count * 2];
                _positions = new Vector3[count * 2];
            }

            for (int i = 0; i < _cellHead.Length; i++) _cellHead[i] = -1;
            for (int i = 0; i < count; i++)
            {
                var p = _active[i].Position;
                _positions[i] = p;
                int cell = CellIndex(p);
                _next[i] = _cellHead[cell];
                _cellHead[cell] = i;
            }
        }

        static int CellIndex(Vector3 p)
        {
            int x = Mathf.Clamp((int)((p.x / GridCell) + GridSize * 0.5f), 1, GridSize - 2);
            int z = Mathf.Clamp((int)((p.z / GridCell) + GridSize * 0.5f), 1, GridSize - 2);
            return z * GridSize + x;
        }

        Vector3 Separation(int index)
        {
            var self = _positions[index];
            float radius = _active[index].Radius;
            int cell = CellIndex(self);
            var push = Vector3.zero;
            for (int n = 0; n < NeighbourOffsets.Length; n++)
            {
                for (int j = _cellHead[cell + NeighbourOffsets[n]]; j >= 0; j = _next[j])
                {
                    if (j == index || !_active[j].IsAlive) continue;
                    var d = self - _positions[j];
                    d.y = 0f;
                    float minDist = radius + _active[j].Radius + 0.15f;
                    float sq = d.sqrMagnitude;
                    if (sq >= minDist * minDist || sq < 1e-6f) continue;
                    float dist = Mathf.Sqrt(sq);
                    push += d / dist * ((minDist - dist) / minDist) * 3f;
                }
            }
            return push;
        }

        void TickSpikes()
        {
            for (int s = _spikes.Count - 1; s >= 0; s--)
            {
                var spikes = _spikes[s];
                if (spikes == null)
                {
                    _spikes.RemoveAt(s);
                    continue;
                }

                var pos = spikes.transform.position;
                int cell = CellIndex(pos);
                for (int n = 0; n < NeighbourOffsets.Length && spikes != null && spikes.IsAlive; n++)
                {
                    for (int j = _cellHead[cell + NeighbourOffsets[n]]; j >= 0; j = _next[j])
                    {
                        var zombie = _active[j];
                        if (!zombie.IsAlive) continue;
                        var d = _positions[j] - pos;
                        d.y = 0f;
                        float r = Spikes.Radius + zombie.Radius;
                        if (d.sqrMagnitude > r * r) continue;
                        zombie.TakeDamage(Spikes.DamagePerTick, null);
                        spikes.TakeDamage(Spikes.WearPerHit, zombie.gameObject);
                        if (!spikes.IsAlive) break;
                    }
                }
            }
        }

        void OnDestroy()
        {
            if (Game.DayNight != null)
            {
                Game.DayNight.NightStarted -= OnNightStarted;
                Game.DayNight.DayStarted -= OnDayStarted;
            }
        }
    }
}
