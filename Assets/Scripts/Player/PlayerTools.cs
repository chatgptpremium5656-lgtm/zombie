using System.Collections.Generic;
using UnityEngine;
using Zombie.Building;
using Zombie.Core;
using Zombie.Logic;
using Zombie.World;

namespace Zombie.Player
{
    /// <summary>
    /// Hotbar and everything the player does with the item in hand: gathering, fighting,
    /// building plan, hammer, placing spikes/sleeping bag, opening doors, flashlight.
    /// </summary>
    public sealed class PlayerTools : MonoBehaviour
    {
        public ItemId Selected { get; private set; } = ItemId.Rock;
        public PlanMode PlanMode { get; private set; } = PlanMode.Foundation;
        public int PlanRotation { get; private set; }
        public Placement LastPlacement { get; private set; }
        public string AimText { get; private set; }

        /// <summary>Owned items in hotbar order (rebuilt only when the inventory changes).</summary>
        public readonly List<ItemId> Hotbar = new List<ItemId>(ItemCatalog.Count);

        PlayerAvatar _player;
        Transform _viewModel;
        MeshFilter _viewFilter;
        Light _flashlight;
        float _cooldown;
        float _swing;
        int _hotbarVersion = -1;
        float _aimTimer;

        static readonly Vector3 ViewPos = new Vector3(0.3f, -0.3f, 0.5f);
        static readonly Quaternion ViewRot = Quaternion.Euler(-12f, -8f, 0f);

        public void Init(PlayerAvatar player)
        {
            _player = player;
            var cam = player.Camera.transform;

            var vm = new GameObject("ViewModel");
            vm.transform.SetParent(cam, false);
            vm.transform.localPosition = ViewPos;
            vm.transform.localRotation = ViewRot;
            _viewFilter = vm.AddComponent<MeshFilter>();
            var renderer = vm.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = Palette.Opaque;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _viewModel = vm.transform;

            var lightGo = new GameObject("Flashlight");
            lightGo.transform.SetParent(cam, false);
            lightGo.transform.localPosition = new Vector3(0.2f, -0.1f, 0.1f);
            _flashlight = lightGo.AddComponent<Light>();
            _flashlight.type = LightType.Spot;
            _flashlight.range = 28f;
            _flashlight.spotAngle = 55f;
            _flashlight.intensity = 2.2f;
            _flashlight.color = new Color(1f, 0.93f, 0.8f);
            _flashlight.shadows = LightShadows.None;
            _flashlight.enabled = false;

            Select(ItemId.Rock);
        }

        public bool FlashlightOn => _flashlight != null && _flashlight.enabled;

        public void OnDeath()
        {
            Game.Building?.HideGhost();
            Select(ItemId.Rock);
        }

        void Select(ItemId item)
        {
            Selected = item;
            _viewFilter.sharedMesh = Models.ViewModel(item);
            _swing = 0f;
            if (item != ItemId.BuildingPlan && item != ItemId.SleepingBag && item != ItemId.WoodenSpikes)
                Game.Building?.HideGhost();
        }

        void RefreshHotbar()
        {
            var inv = _player.Inventory;
            if (_hotbarVersion == inv.Version) return;
            _hotbarVersion = inv.Version;
            Hotbar.Clear();
            for (int i = 0; i < ItemCatalog.Count; i++)
            {
                var id = (ItemId)i;
                if (inv.Has(id)) Hotbar.Add(id);
            }
            if (!inv.Has(Selected)) Select(ItemId.Rock);
        }

        void Update()
        {
            RefreshHotbar();
            AnimateViewModel();
            if (Game.Paused || Game.UiOpen || _player.IsDead)
            {
                if (_player.IsDead || Game.UiOpen) Game.Building?.HideGhost();
                return;
            }

            HandleHotbarInput();

            if (GameInput.Pressed(GameInput.Button.Flashlight)) _flashlight.enabled = !_flashlight.enabled;

            var ray = new Ray(_player.Camera.transform.position, _player.Camera.transform.forward);
            if (GameInput.Pressed(GameInput.Button.Interact)) Interact(ray);

            _cooldown -= Time.deltaTime;
            var def = ItemCatalog.Get(Selected);
            switch (def.Kind)
            {
                case ItemKind.Tool:
                    if (GameInput.PrimaryHeld && _cooldown <= 0f) Swing(def, ray);
                    break;
                case ItemKind.BuildTool:
                    if (Selected == ItemId.BuildingPlan) UsePlan(ray);
                    else UseHammer(ray);
                    break;
                case ItemKind.Placeable:
                    UsePlaceable(ray);
                    break;
            }

            UpdateAimText(ray);
        }

        void HandleHotbarInput()
        {
            int key = GameInput.HotbarKeyPressed();
            if (key >= 0 && key < Hotbar.Count) Select(Hotbar[key]);

            // The wheel cycles the hotbar, except with the building plan where the right button cycles pieces.
            int scroll = GameInput.Scroll;
            if (scroll != 0 && Hotbar.Count > 0)
            {
                int index = Mathf.Max(0, Hotbar.IndexOf(Selected));
                index = (index - scroll + Hotbar.Count) % Hotbar.Count;
                Select(Hotbar[index]);
            }
        }

        // ------------------------------------------------------------------ melee

        void Swing(ItemDef tool, Ray ray)
        {
            _cooldown = tool.Cooldown;
            _swing = 1f;

            if (!Physics.Raycast(ray, out var hit, tool.Reach, ~0, QueryTriggerInteraction.Ignore)) return;

            var node = hit.collider.GetComponentInParent<ResourceNode>();
            if (node != null)
            {
                int amount = node.Gather(tool, out var resource, out int bonusStone);
                if (amount > 0)
                {
                    _player.Inventory.Add(resource, amount);
                    if (bonusStone > 0) _player.Inventory.Add(ResourceType.Stone, bonusStone);
                    _player.Notify("+" + amount + " " + ResourceName(resource));
                }
                else if (node.Type == NodeType.MetalRock && tool.MetalPerHit == 0)
                {
                    _player.Notify("Нужна кирка");
                }
                return;
            }

            if (hit.collider.GetComponentInParent<IBuildingPart>() != null) return; // don't wreck your own base

            var target = hit.collider.GetComponentInParent<IDamageable>();
            if (target != null && target.IsAlive) target.TakeDamage(tool.Damage, gameObject);
        }

        public static string ResourceName(ResourceType type)
        {
            switch (type)
            {
                case ResourceType.Wood: return "дерево";
                case ResourceType.Stone: return "камень";
                default: return "металл";
            }
        }

        // ------------------------------------------------------------------ building

        void UsePlan(Ray ray)
        {
            if (GameInput.SecondaryPressed)
            {
                PlanMode = (PlanMode)(((int)PlanMode + 1) % 6);
            }
            if (GameInput.Pressed(GameInput.Button.Rotate)) PlanRotation = (PlanRotation + 1) % 4;

            var placement = Game.Building.PreviewPlan(PlanMode, ray, PlanRotation, _player.Inventory);
            LastPlacement = placement;
            if (GameInput.PrimaryPressed && placement.Valid)
            {
                if (Game.Building.TryBuild(placement, _player)) _swing = 0.6f;
            }
        }

        void UseHammer(Ray ray)
        {
            if (_cooldown > 0f) return;
            string message = null;
            if (GameInput.PrimaryHeld)
            {
                message = Game.Building.TryRepair(ray, 4f, _player.Inventory);
                _cooldown = 0.5f;
                _swing = 1f;
            }
            else if (GameInput.SecondaryPressed)
            {
                message = Game.Building.TryUpgrade(ray, 4f, _player.Inventory);
                _cooldown = 0.3f;
                _swing = 1f;
            }
            if (message != null) _player.Notify(message);
        }

        void UsePlaceable(Ray ray)
        {
            var placement = Game.Building.PreviewPlaceable(Selected, ray, transform.eulerAngles.y);
            LastPlacement = placement;
            if (GameInput.PrimaryPressed && placement.Valid)
            {
                Game.Building.Place(Selected, placement, _player);
                _swing = 0.6f;
            }
        }

        void Interact(Ray ray)
        {
            if (!Physics.Raycast(ray, out var hit, 3f, ~0, QueryTriggerInteraction.Ignore)) return;
            var door = hit.collider.GetComponentInParent<Door>();
            if (door != null) door.Toggle();
        }

        // ------------------------------------------------------------------ feedback

        void UpdateAimText(Ray ray)
        {
            _aimTimer -= Time.deltaTime;
            if (_aimTimer > 0f) return;
            _aimTimer = 0.15f;
            AimText = null;

            if (!Physics.Raycast(ray, out var hit, 4f, ~0, QueryTriggerInteraction.Collide)) return;
            var node = hit.collider.GetComponentInParent<ResourceNode>();
            if (node != null)
            {
                AimText = node.DisplayName + "  " + node.Remaining + "/" + node.Capacity;
                return;
            }
            var door = hit.collider.GetComponentInParent<Door>();
            if (door != null)
            {
                AimText = "Дверь (" + BuildingRules.TierName(door.Tier) + ")  " + Mathf.CeilToInt(door.HealthValue) + "/" +
                          Mathf.CeilToInt(door.MaxHealth) + "   [E] открыть";
                return;
            }
            var piece = hit.collider.GetComponentInParent<BuildingPiece>();
            if (piece != null)
            {
                AimText = BuildingRules.KindName(piece.Kind) + " (" + BuildingRules.TierName(piece.Tier) + ")  " +
                          Mathf.CeilToInt(piece.HealthValue) + "/" + Mathf.CeilToInt(piece.MaxHealth);
                if (Selected == ItemId.Hammer && piece.Tier != BuildTier.Metal)
                    AimText += "   [ПКМ] улучшить: " + BuildingSystem.CostText(BuildingRules.UpgradeCost(piece.Kind, piece.Tier + 1));
            }
        }

        void AnimateViewModel()
        {
            _swing = Mathf.Max(0f, _swing - Time.deltaTime * 4f);
            float s = Mathf.Sin(_swing * Mathf.PI);
            _viewModel.localPosition = ViewPos + new Vector3(-0.05f * s, 0.04f * s, 0.12f * s);
            _viewModel.localRotation = ViewRot * Quaternion.Euler(-55f * s, 0f, 10f * s);
            bool hidden = _player.IsDead;
            if (_viewModel.gameObject.activeSelf == hidden) _viewModel.gameObject.SetActive(!hidden);
        }
    }
}
