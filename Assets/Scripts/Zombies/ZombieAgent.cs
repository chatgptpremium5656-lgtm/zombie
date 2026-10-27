using UnityEngine;
using Zombie.Building;
using Zombie.Core;
using Zombie.Logic;
using Zombie.Player;

namespace Zombie.Zombies
{
    public sealed class ZombieStats
    {
        public ZombieKind Kind;
        public float Scale;
        public float Speed;
        public float Health;
        public float PlayerDamage;
        public float BuildingDamage;
        public float AttackInterval;
        public float Radius;
        public int MetalDrop;

        public static readonly ZombieStats[] All =
        {
            new ZombieStats { Kind = ZombieKind.Shambler, Scale = 1f, Speed = 1.7f, Health = 60f, PlayerDamage = 10f,
                BuildingDamage = 4f, AttackInterval = 1.2f, Radius = 0.32f, MetalDrop = 1 },
            new ZombieStats { Kind = ZombieKind.Runner, Scale = 0.82f, Speed = 4.3f, Health = 35f, PlayerDamage = 6f,
                BuildingDamage = 2f, AttackInterval = 0.8f, Radius = 0.26f, MetalDrop = 1 },
            new ZombieStats { Kind = ZombieKind.Brute, Scale = 1.9f, Speed = 1.25f, Health = 420f, PlayerDamage = 30f,
                BuildingDamage = 45f, AttackInterval = 2.0f, Radius = 0.6f, MetalDrop = 8 },
        };
    }

    /// <summary>
    /// One pooled zombie. No Update(): HordeDirector ticks every zombie in one loop (cheaper than
    /// 150 MonoBehaviour updates). Moves straight at its target and breaks whatever building is in the way.
    /// </summary>
    public sealed class ZombieAgent : MonoBehaviour, IDamageable
    {
        enum State
        {
            Inactive,
            Chasing,
            Dying,
        }

        public ZombieStats Stats { get; private set; }
        public bool IsAlive => _state == State.Chasing;
        public bool IsActive => _state != State.Inactive;
        public Vector3 Position => _transform.position;
        public float Radius => Stats.Radius;

        static readonly RaycastHit[] HitBuffer = new RaycastHit[4];
        static readonly Collider[] OverlapBuffer = new Collider[8];

        Transform _transform;
        Transform _body;
        Collider _collider;
        HordeDirector _director;
        State _state;
        float _health;
        float _attackTimer;
        float _senseTimer;
        float _targetTimer;
        float _stateTimer;
        float _animPhase;
        float _lunge;
        float _flash;
        float _sidestep; // -1/0/+1 while walking around a tree or rock
        float _sidestepTimer;
        IBuildingPart _blocker;
        float _blockerSince;
        PlayerAvatar _target;
        Vector3 _wanderPoint;
        bool _wandering;

        public void Create(HordeDirector director, ZombieStats stats)
        {
            _director = director;
            Stats = stats;
            _transform = transform;

            var body = new GameObject("Body");
            body.transform.SetParent(_transform, false);
            body.transform.localScale = Vector3.one * stats.Scale;
            body.AddComponent<MeshFilter>().sharedMesh = Models.Zombie(stats.Kind);
            body.AddComponent<MeshRenderer>().sharedMaterial = Palette.Opaque;
            _body = body.transform;

            var capsule = gameObject.AddComponent<CapsuleCollider>();
            capsule.radius = stats.Radius;
            capsule.height = 1.8f * stats.Scale;
            capsule.center = new Vector3(0, 0.9f * stats.Scale, 0);
            _collider = capsule;

            var rb = gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            _state = State.Inactive;
            gameObject.SetActive(false);
        }

        public void Spawn(Vector3 position, float healthMultiplier, bool wandering)
        {
            _transform.SetPositionAndRotation(position, Quaternion.Euler(0, Random.Range(0f, 360f), 0));
            _health = Stats.Health * healthMultiplier;
            _state = State.Chasing;
            _attackTimer = Random.Range(0f, Stats.AttackInterval);
            _senseTimer = Random.Range(0f, 0.1f);
            _targetTimer = 0f;
            _animPhase = Random.Range(0f, 10f);
            _blocker = null;
            _target = null;
            _sidestep = 0f;
            _wandering = wandering;
            _wanderPoint = position;
            _body.localPosition = Vector3.zero;
            _body.localRotation = Quaternion.identity;
            _collider.enabled = true;
            gameObject.SetActive(true);
        }

        public void Despawn()
        {
            _state = State.Inactive;
            gameObject.SetActive(false);
        }

        /// <summary>At sunrise the night's horde burns away.</summary>
        public void Burn()
        {
            if (_state == State.Chasing) Die(null);
        }

        public void TakeDamage(float amount, GameObject source)
        {
            if (_state != State.Chasing || amount <= 0f) return;
            _health -= amount;
            _flash = 1f;
            if (source != null)
            {
                // Small knockback so hits feel solid.
                var away = _transform.position - source.transform.position;
                away.y = 0f;
                if (away.sqrMagnitude > 0.001f) _transform.position += away.normalized * (0.25f / Stats.Scale);
                var player = source.GetComponentInParent<PlayerAvatar>();
                if (player != null) _target = player;
                _wandering = false;
            }
            if (_health <= 0f) Die(source);
        }

        void Die(GameObject killer)
        {
            _state = State.Dying;
            _stateTimer = 1.4f;
            _collider.enabled = false;
            _director.OnZombieKilled(this, killer);
        }

        /// <summary>Called by HordeDirector once per frame with the push-away from nearby zombies.</summary>
        public void Tick(float dt, Vector3 separation)
        {
            if (_state == State.Dying)
            {
                _stateTimer -= dt;
                // Topple, then sink into the ground and go back to the pool.
                float fall = Mathf.Clamp01((1.4f - _stateTimer) * 3f);
                _body.localRotation = Quaternion.Euler(-85f * fall, 0, 0);
                if (_stateTimer < 0.6f) _body.localPosition = new Vector3(0, -(0.6f - _stateTimer) * 1.5f, 0);
                if (_stateTimer <= 0f) _director.Release(this);
                return;
            }

            // Obstacles are checked 10x per second with a cast long enough to cover the distance walked
            // meanwhile, so even runners can't tunnel through a wall. Targets are re-picked 2x per second.
            _senseTimer -= dt;
            if (_senseTimer <= 0f)
            {
                _senseTimer = 0.1f;
                _targetTimer -= 0.1f;
                if (_targetTimer <= 0f)
                {
                    _targetTimer = 0.5f;
                    PickTarget();
                }
                SenseObstacles();
            }

            var pos = _transform.position;
            Vector3 goal = _target != null ? _target.Position : _wanderPoint;
            var toGoal = goal - pos;
            float dy = toGoal.y;
            toGoal.y = 0f;
            float dist = toGoal.magnitude;

            bool attacking = false;
            _attackTimer -= dt;

            if (_target != null && !_target.IsDead)
            {
                float reach = 1.0f + Stats.Radius;
                if (dist < reach && dy > -0.5f && dy < 1.9f * Stats.Scale)
                {
                    attacking = true;
                    if (_attackTimer <= 0f)
                    {
                        _attackTimer = Stats.AttackInterval;
                        _lunge = 1f;
                        _target.Health.TakeDamage(Stats.PlayerDamage, gameObject);
                    }
                }
            }

            if (!attacking && _blocker != null)
            {
                if (!_blocker.IsAlive || (_blocker as Component) == null)
                {
                    _blocker = null;
                }
                else
                {
                    attacking = true;
                    var point = _blocker.ClosestPoint(pos);
                    point.y = pos.y;
                    toGoal = point - pos;
                    dist = toGoal.magnitude;
                    if (_attackTimer <= 0f)
                    {
                        _attackTimer = Stats.AttackInterval;
                        _lunge = 1f;
                        _blocker.TakeDamage(Stats.BuildingDamage, gameObject);
                    }
                }
            }

            Vector3 dir = dist > 0.001f ? toGoal / dist : _transform.forward;
            if (!attacking)
            {
                if (_sidestep != 0f)
                {
                    _sidestepTimer -= dt;
                    dir = Quaternion.Euler(0, 75f * _sidestep, 0) * dir;
                    if (_sidestepTimer <= 0f) _sidestep = 0f;
                }

                var move = dir * Stats.Speed + separation;
                if (_wandering && dist < 1.5f) move = separation;
                pos += move * dt;
                pos.y = 0f;
                _transform.position = pos;
            }

            if (dist > 0.05f)
            {
                var look = Quaternion.LookRotation(new Vector3(dir.x, 0, dir.z));
                _transform.rotation = Quaternion.RotateTowards(_transform.rotation, look, 240f * dt);
            }

            Animate(dt, attacking);
        }

        void PickTarget()
        {
            // Target: nearest living player (co-op ready), unless wandering by day and nobody is close.
            PlayerAvatar best = null;
            float bestSq = float.MaxValue;
            var pos = _transform.position;
            for (int i = 0; i < Game.Players.Count; i++)
            {
                var p = Game.Players[i];
                if (p == null || p.IsDead) continue;
                float sq = (p.Position - pos).sqrMagnitude;
                if (sq < bestSq)
                {
                    bestSq = sq;
                    best = p;
                }
            }

            if (_wandering)
            {
                if (best != null && bestSq < 18f * 18f) _wandering = false;
                else
                {
                    if ((_wanderPoint - pos).sqrMagnitude < 4f)
                        _wanderPoint = pos + new Vector3(Random.Range(-15f, 15f), 0, Random.Range(-15f, 15f));
                    best = null;
                }
            }
            _target = best;
        }

        void SenseObstacles()
        {
            var pos = _transform.position;
            // Keep hitting the same piece for a while, then look again (the player may have moved on).
            if (_blocker != null && _blocker.IsAlive && (_blocker as Component) != null && Time.time - _blockerSince < 2f) return;
            _blocker = null;

            Vector3 goal = _target != null ? _target.Position : _wanderPoint;
            var toGoal = goal - pos;
            float dy = toGoal.y;
            toGoal.y = 0f;
            float dist = toGoal.magnitude;
            if (dist < 0.01f) return;
            var dir = toGoal / dist;

            // Player out of reach above us (on a foundation edge, a floor or a roof): smash the base.
            if (_target != null && dist < 4f && dy > 1.9f * Stats.Scale)
            {
                _blocker = FindNearbyBuilding(pos);
                if (_blocker != null)
                {
                    _blockerSince = Time.time;
                    return;
                }
            }

            // Anything in the way? Buildings get attacked, trees and rocks get walked around.
            var origin = pos + Vector3.up * 0.45f;
            float castDist = Stats.Speed * 0.1f + Stats.Radius + 0.3f;
            int count = Physics.SphereCastNonAlloc(origin, Stats.Radius * 0.9f, dir, HitBuffer, castDist, ~0,
                QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var col = HitBuffer[i].collider;
                if (col == _collider || col.attachedRigidbody != null && col.attachedRigidbody.gameObject.GetComponent<ZombieAgent>() != null)
                    continue;
                var part = col.GetComponentInParent<IBuildingPart>();
                if (part != null && part.IsAlive)
                {
                    _blocker = part;
                    _blockerSince = Time.time;
                    return;
                }
                if (col.GetComponentInParent<PlayerAvatar>() != null) continue;
                if (HitBuffer[i].point.y < 0.05f) continue; // the ground itself
                if (_sidestep == 0f)
                {
                    var local = _transform.InverseTransformPoint(HitBuffer[i].point);
                    _sidestep = local.x > 0f ? -1f : 1f;
                    _sidestepTimer = 0.9f;
                }
                return;
            }
        }

        static IBuildingPart FindNearbyBuilding(Vector3 pos)
        {
            int count = Physics.OverlapSphereNonAlloc(pos + Vector3.up * 0.5f, 2.2f, OverlapBuffer, ~0, QueryTriggerInteraction.Ignore);
            IBuildingPart best = null;
            float bestSq = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                var part = OverlapBuffer[i].GetComponentInParent<IBuildingPart>();
                if (part == null || !part.IsAlive) continue;
                float sq = (part.ClosestPoint(pos) - pos).sqrMagnitude;
                if (sq < bestSq)
                {
                    bestSq = sq;
                    best = part;
                }
            }
            return best;
        }

        void Animate(float dt, bool attacking)
        {
            // Cheap procedural waddle on the single body mesh: sway, bob and an attack lunge.
            float speedFactor = Stats.Speed / 1.7f;
            _animPhase += dt * (attacking ? 3f : 5f * Mathf.Sqrt(speedFactor));
            _lunge = Mathf.Max(0f, _lunge - dt * 3.5f);
            _flash = Mathf.Max(0f, _flash - dt * 6f);
            float sway = Mathf.Sin(_animPhase) * (attacking ? 4f : 9f);
            float bob = Mathf.Abs(Mathf.Sin(_animPhase)) * 0.06f * Stats.Scale;
            float lean = 10f + Mathf.Sin(_lunge * Mathf.PI) * 28f + _flash * -12f;
            _body.localRotation = Quaternion.Euler(lean, 0, sway);
            _body.localPosition = new Vector3(0, bob, Mathf.Sin(_lunge * Mathf.PI) * 0.25f);
        }
    }
}
