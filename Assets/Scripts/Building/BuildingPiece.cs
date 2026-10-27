using UnityEngine;
using Zombie.Core;
using Zombie.Logic;

namespace Zombie.Building
{
    /// <summary>Part of a player base that zombies can attack (pieces and doors).</summary>
    public interface IBuildingPart : IDamageable
    {
        Vector3 ClosestPoint(Vector3 from);
    }

    /// <summary>Static rules for building pieces: costs, health and how hard zombies hit each tier.</summary>
    public static class BuildingRules
    {
        public static Cost BuildCost(PieceKind kind)
        {
            switch (kind)
            {
                case PieceKind.Foundation: return new Cost(20);
                case PieceKind.Wall: return new Cost(15);
                case PieceKind.Doorway: return new Cost(12);
                case PieceKind.Floor: return new Cost(10);
                default: return new Cost(25); // stairs
            }
        }

        static float SizeFactor(PieceKind kind)
        {
            switch (kind)
            {
                case PieceKind.Doorway: return 0.8f;
                case PieceKind.Floor: return 0.5f;
                default: return 1f;
            }
        }

        public static Cost UpgradeCost(PieceKind kind, BuildTier to)
        {
            Cost baseCost;
            switch (to)
            {
                case BuildTier.Wood: baseCost = new Cost(40); break;
                case BuildTier.Stone: baseCost = new Cost(0, 60); break;
                case BuildTier.Metal: baseCost = new Cost(0, 0, 30); break;
                default: return Cost.Free;
            }
            return baseCost.Scaled(SizeFactor(kind));
        }

        /// <summary>Resource spent per hammer repair hit.</summary>
        public static Cost RepairCost(BuildTier tier)
        {
            switch (tier)
            {
                case BuildTier.Twig: return new Cost(1);
                case BuildTier.Wood: return new Cost(3);
                case BuildTier.Stone: return new Cost(0, 4);
                default: return new Cost(0, 0, 2);
            }
        }

        public static float MaxHealth(BuildTier tier)
        {
            switch (tier)
            {
                case BuildTier.Twig: return 40f;
                case BuildTier.Wood: return 400f;
                case BuildTier.Stone: return 900f;
                default: return 2000f;
            }
        }

        /// <summary>Stronger materials shrug off zombie hits; only brutes really hurt stone and metal.</summary>
        public static float ZombieDamageMultiplier(BuildTier tier)
        {
            switch (tier)
            {
                case BuildTier.Twig: return 1f;
                case BuildTier.Wood: return 0.6f;
                case BuildTier.Stone: return 0.3f;
                default: return 0.15f;
            }
        }

        public static string KindName(PieceKind kind)
        {
            switch (kind)
            {
                case PieceKind.Foundation: return "Фундамент";
                case PieceKind.Wall: return "Стена";
                case PieceKind.Doorway: return "Дверной проём";
                case PieceKind.Floor: return "Перекрытие";
                default: return "Лестница";
            }
        }

        public static string TierName(BuildTier tier)
        {
            switch (tier)
            {
                case BuildTier.Twig: return "солома";
                case BuildTier.Wood: return "дерево";
                case BuildTier.Stone: return "камень";
                default: return "металл";
            }
        }
    }

    public sealed class BuildingPiece : MonoBehaviour, IBuildingPart
    {
        public PieceKind Kind { get; private set; }
        public BuildTier Tier { get; private set; }
        public SlotKey Slot { get; private set; }
        public int Rotation { get; private set; }
        public float HealthValue { get; private set; }
        public float MaxHealth => BuildingRules.MaxHealth(Tier);
        public bool IsAlive => HealthValue > 0f;
        public Door Door { get; set; }

        MeshFilter _filter;
        BuildingSystem _system;

        public void Init(BuildingSystem system, PieceKind kind, SlotKey slot, int rotation)
        {
            _system = system;
            Kind = kind;
            Slot = slot;
            Rotation = rotation;
            _filter = gameObject.AddComponent<MeshFilter>();
            gameObject.AddComponent<MeshRenderer>().sharedMaterial = Palette.Opaque;
            SetTier(BuildTier.Twig);
        }

        public void SetTier(BuildTier tier)
        {
            Tier = tier;
            HealthValue = MaxHealth;
            _filter.sharedMesh = Models.Piece(Kind, tier);
        }

        public void Repair(float amount)
        {
            HealthValue = Mathf.Min(MaxHealth, HealthValue + amount);
        }

        public void TakeDamage(float amount, GameObject source)
        {
            if (!IsAlive || amount <= 0f) return;
            HealthValue -= amount * BuildingRules.ZombieDamageMultiplier(Tier);
            if (HealthValue <= 0f) _system.DestroyPiece(this);
        }

        Collider[] _colliders;

        /// <summary>Nearest point over all of the piece's colliders (doorways have three, stairs keep theirs on a child).</summary>
        public Vector3 ClosestPoint(Vector3 from)
        {
            if (_colliders == null || _colliders.Length == 0) _colliders = GetComponentsInChildren<Collider>();
            var best = transform.position;
            float bestSq = float.MaxValue;
            for (int i = 0; i < _colliders.Length; i++)
            {
                var c = _colliders[i];
                if (c == null || !c.enabled || c.GetComponentInParent<Door>() != null) continue;
                var p = c.ClosestPoint(from);
                float sq = (p - from).sqrMagnitude;
                if (sq < bestSq)
                {
                    bestSq = sq;
                    best = p;
                }
            }
            return best;
        }
    }
}
