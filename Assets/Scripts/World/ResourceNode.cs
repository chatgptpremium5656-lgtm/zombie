using UnityEngine;
using Zombie.Core;
using Zombie.Logic;

namespace Zombie.World
{
    public enum NodeType
    {
        Tree,
        Rock,
        MetalRock,
    }

    /// <summary>
    /// A tree or rock that gives resources when hit. Depleted nodes hide and regrow later,
    /// so the small test map never runs dry.
    /// </summary>
    public sealed class ResourceNode : MonoBehaviour
    {
        public NodeType Type { get; private set; }
        public int Remaining { get; private set; }
        public int Capacity { get; private set; }
        public bool Depleted => Remaining <= 0;

        Renderer _renderer;
        Collider _collider;
        Transform _visual;
        float _respawnAt;
        float _respawnSeconds;
        float _punch;
        Vector3 _baseScale;

        public void Init(NodeType type, int capacity, float respawnSeconds, Transform visual, Collider col)
        {
            Type = type;
            Capacity = capacity;
            Remaining = capacity;
            _respawnSeconds = respawnSeconds;
            _visual = visual;
            _renderer = visual.GetComponent<Renderer>();
            _collider = col;
            _baseScale = visual.localScale;
            enabled = false; // Update only runs while animating or waiting to respawn
        }

        public string DisplayName
        {
            get
            {
                switch (Type)
                {
                    case NodeType.Tree: return "Дерево";
                    case NodeType.Rock: return "Камень";
                    default: return "Металлическая руда";
                }
            }
        }

        /// <summary>Applies one hit with the given tool. Returns the amount gathered (0 if the tool can't gather this).</summary>
        public int Gather(ItemDef tool, out ResourceType resource, out int bonusStone)
        {
            bonusStone = 0;
            int perHit;
            switch (Type)
            {
                case NodeType.Tree:
                    resource = ResourceType.Wood;
                    perHit = tool.WoodPerHit;
                    break;
                case NodeType.Rock:
                    resource = ResourceType.Stone;
                    perHit = tool.StonePerHit;
                    break;
                default:
                    resource = ResourceType.Metal;
                    perHit = tool.MetalPerHit;
                    if (perHit > 0) bonusStone = Mathf.Max(1, tool.StonePerHit / 3);
                    break;
            }

            _punch = 1f;
            enabled = true;
            if (perHit <= 0 || Depleted) return 0;

            int amount = Mathf.Min(perHit, Remaining);
            Remaining -= amount;
            if (Depleted) Deplete();
            return amount;
        }

        void Deplete()
        {
            _renderer.enabled = false;
            _collider.enabled = false;
            _respawnAt = Time.time + _respawnSeconds;
            enabled = true;
        }

        void Update()
        {
            if (Depleted)
            {
                if (Time.time < _respawnAt) return;
                // Don't pop back inside a player or a building.
                if (Physics.CheckSphere(transform.position + Vector3.up * 1.4f, 1.1f, ~0, QueryTriggerInteraction.Ignore))
                {
                    _respawnAt = Time.time + 20f;
                    return;
                }
                Remaining = Capacity;
                _renderer.enabled = true;
                _collider.enabled = true;
                _visual.localScale = _baseScale;
                enabled = false;
                return;
            }

            if (_punch > 0f)
            {
                _punch = Mathf.Max(0f, _punch - Time.deltaTime * 6f);
                float s = 1f + Mathf.Sin(_punch * Mathf.PI) * 0.06f;
                _visual.localScale = new Vector3(_baseScale.x * s, _baseScale.y * (2f - s), _baseScale.z * s);
                if (_punch <= 0f)
                {
                    _visual.localScale = _baseScale;
                    enabled = false;
                }
            }
        }
    }
}
