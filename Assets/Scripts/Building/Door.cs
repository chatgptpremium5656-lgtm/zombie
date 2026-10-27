using UnityEngine;
using Zombie.Core;
using Zombie.Logic;

namespace Zombie.Building
{
    /// <summary>Door inside a doorway. Players open it with E; zombies can only break it.</summary>
    public sealed class Door : MonoBehaviour, IBuildingPart
    {
        public BuildTier Tier { get; private set; } = BuildTier.Wood;
        public float HealthValue { get; private set; }
        public float MaxHealth => Tier == BuildTier.Metal ? 1500f : 350f;
        public bool IsAlive => HealthValue > 0f;
        public bool IsOpen { get; private set; }

        Transform _hinge;
        MeshFilter _filter;
        BoxCollider _collider;
        float _angle;

        public void Init(Transform hinge)
        {
            _hinge = hinge;
            _filter = hinge.gameObject.AddComponent<MeshFilter>();
            hinge.gameObject.AddComponent<MeshRenderer>().sharedMaterial = Palette.Opaque;
            _collider = hinge.gameObject.AddComponent<BoxCollider>();
            const float w = Models.DoorWidth - 0.06f, h = Models.DoorHeight - 0.04f;
            _collider.center = new Vector3(w * 0.5f, h * 0.5f, 0);
            _collider.size = new Vector3(w, h, 0.12f);
            SetTier(BuildTier.Wood);
            enabled = false;
        }

        public void SetTier(BuildTier tier)
        {
            Tier = tier;
            HealthValue = MaxHealth;
            _filter.sharedMesh = Models.Door(tier);
        }

        public void Repair(float amount)
        {
            HealthValue = Mathf.Min(MaxHealth, HealthValue + amount);
        }

        public void Toggle()
        {
            IsOpen = !IsOpen;
            enabled = true;
        }

        public void TakeDamage(float amount, GameObject source)
        {
            if (!IsAlive || amount <= 0f) return;
            HealthValue -= amount * (Tier == BuildTier.Metal ? 0.2f : 0.6f);
            if (HealthValue <= 0f)
            {
                var piece = GetComponentInParent<BuildingPiece>();
                if (piece != null) piece.Door = null;
                Destroy(gameObject);
            }
        }

        public Vector3 ClosestPoint(Vector3 from) => _collider.ClosestPoint(from);

        void Update()
        {
            float target = IsOpen ? -100f : 0f;
            _angle = Mathf.MoveTowards(_angle, target, 300f * Time.deltaTime);
            _hinge.localRotation = Quaternion.Euler(0, _angle, 0);
            if (Mathf.Approximately(_angle, target)) enabled = false;
        }
    }
}
