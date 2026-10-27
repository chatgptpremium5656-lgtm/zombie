using System.Collections.Generic;
using UnityEngine;
using Zombie.Core;
using Zombie.Logic;
using Zombie.Player;

namespace Zombie.Building
{
    /// <summary>What the building plan can place, in the order the right mouse button cycles through.</summary>
    public enum PlanMode
    {
        Foundation,
        Wall,
        Doorway,
        Floor,
        Stairs,
        Door,
    }

    public struct Placement
    {
        public bool Valid;
        /// <summary>True once Position/Orientation point at a real slot (the ghost can be shown).</summary>
        public bool HasPose;
        public string Reason;
        public Cost Cost;
        public PlanMode Mode;
        public SlotKey Slot;
        public int Rotation;
        public Vector3 Position;
        public Quaternion Orientation;
        public BuildingPiece Doorway;
    }

    /// <summary>
    /// Rust-style modular building: preview ghost, socket snapping, support rules (via BuildGrid),
    /// hammer upgrades/repairs and collapse when supports are destroyed. Also places spikes and sleeping bags.
    /// </summary>
    public sealed class BuildingSystem : MonoBehaviour
    {
        static readonly Cost DoorCost = new Cost(30);
        static readonly Cost MetalDoorCost = new Cost(0, 0, 25);

        readonly BuildGrid _grid = new BuildGrid();
        readonly Dictionary<SlotKey, BuildingPiece> _pieces = new Dictionary<SlotKey, BuildingPiece>();
        readonly List<SlotKey> _removed = new List<SlotKey>(16);
        readonly Collider[] _overlap = new Collider[16];

        Transform _root;
        GameObject _ghost;
        MeshFilter _ghostFilter;
        MeshRenderer _ghostRenderer;
        Material _ghostMaterial;
        Collider _groundCollider;
        GameConfig _config;

        static readonly Color GhostOk = new Color(0.45f, 1f, 0.45f, 0.38f);
        static readonly Color GhostBad = new Color(1f, 0.35f, 0.3f, 0.38f);

        public int PieceCount => _pieces.Count;

        public void Init(GameConfig config, Collider groundCollider)
        {
            _config = config;
            _groundCollider = groundCollider;
            _root = new GameObject("Base").transform;
            _root.SetParent(transform, false);

            _ghost = new GameObject("BuildGhost");
            _ghost.transform.SetParent(transform, false);
            _ghostFilter = _ghost.AddComponent<MeshFilter>();
            _ghostRenderer = _ghost.AddComponent<MeshRenderer>();
            _ghostMaterial = new Material(Palette.Ghost);
            _ghostRenderer.sharedMaterial = _ghostMaterial;
            _ghostRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _ghost.SetActive(false);
        }

        // ------------------------------------------------------------------ slot geometry

        public static void SlotPose(SlotKey slot, int rotation, out Vector3 position, out Quaternion orientation)
        {
            const float s = BuildGrid.CellSize;
            float baseY = BuildGrid.LevelBaseY(slot.Level);
            switch (slot.Type)
            {
                case SlotType.CellFloor:
                    position = new Vector3((slot.X + 0.5f) * s, slot.Level == 0 ? BuildGrid.FoundationHeight * 0.5f : baseY - 0.1f, (slot.Z + 0.5f) * s);
                    orientation = Quaternion.identity;
                    break;
                case SlotType.CellVolume:
                    position = new Vector3((slot.X + 0.5f) * s, baseY, (slot.Z + 0.5f) * s);
                    orientation = Quaternion.Euler(0, rotation * 90f, 0);
                    break;
                case SlotType.EdgeSouth:
                    position = new Vector3((slot.X + 0.5f) * s, baseY + BuildGrid.LevelHeight * 0.5f, slot.Z * s);
                    orientation = Quaternion.identity;
                    break;
                default:
                    // West walls are 1 mm lower so their tops never z-fight with south walls at corners.
                    position = new Vector3(slot.X * s, baseY + BuildGrid.LevelHeight * 0.5f - 0.001f, (slot.Z + 0.5f) * s);
                    orientation = Quaternion.Euler(0, 90f, 0);
                    break;
            }
        }

        static PieceKind ToKind(PlanMode mode)
        {
            switch (mode)
            {
                case PlanMode.Foundation: return PieceKind.Foundation;
                case PlanMode.Wall: return PieceKind.Wall;
                case PlanMode.Doorway: return PieceKind.Doorway;
                case PlanMode.Floor: return PieceKind.Floor;
                default: return PieceKind.Stairs;
            }
        }

        public static string ModeName(PlanMode mode)
        {
            return mode == PlanMode.Door ? "Дверь" : BuildingRules.KindName(ToKind(mode));
        }

        public static Cost ModeCost(PlanMode mode)
        {
            return mode == PlanMode.Door ? DoorCost : BuildingRules.BuildCost(ToKind(mode));
        }

        // ------------------------------------------------------------------ building plan

        /// <summary>Works out where the current plan piece would go and shows the ghost there.</summary>
        public Placement PreviewPlan(PlanMode mode, Ray ray, int rotation, Inventory inventory)
        {
            var p = Evaluate(mode, ray, rotation);
            if (p.Valid && !inventory.CanAfford(p.Cost))
            {
                p.Valid = false;
                p.Reason = "Не хватает ресурсов";
            }

            if (p.HasPose)
            {
                var mesh = mode == PlanMode.Door ? Models.Door(BuildTier.Wood) : Models.Piece(ToKind(mode), BuildTier.Twig);
                ShowGhost(mesh, p.Position, p.Orientation, p.Valid);
            }
            else
            {
                HideGhost();
            }
            return p;
        }

        public bool TryBuild(Placement p, PlayerAvatar player)
        {
            if (!p.Valid || !player.Inventory.CanAfford(p.Cost)) return false;

            if (p.Mode == PlanMode.Door)
            {
                if (p.Doorway == null || p.Doorway.Door != null) return false;
                player.Inventory.TrySpend(p.Cost);
                AttachDoor(p.Doorway);
                return true;
            }

            var kind = ToKind(p.Mode);
            if (_grid.Place(kind, p.Slot) != PlaceResult.Ok) return false;
            player.Inventory.TrySpend(p.Cost);

            var go = new GameObject(kind + " " + p.Slot);
            go.transform.SetParent(_root, false);
            go.transform.SetPositionAndRotation(p.Position, p.Orientation);
            var piece = go.AddComponent<BuildingPiece>();
            piece.Init(this, kind, p.Slot, p.Rotation);
            AddColliders(go, kind);
            _pieces[p.Slot] = piece;
            return true;
        }

        Placement Evaluate(PlanMode mode, Ray ray, int rotation)
        {
            var p = new Placement { Mode = mode, Rotation = rotation, Cost = ModeCost(mode) };
            float range = _config.buildRange;

            bool hit = Physics.Raycast(ray, out var info, range, ~0, QueryTriggerInteraction.Ignore);

            if (mode == PlanMode.Door)
            {
                var doorway = hit ? info.collider.GetComponentInParent<BuildingPiece>() : null;
                if (doorway == null || doorway.Kind != PieceKind.Doorway)
                {
                    p.Reason = "Наведите на дверной проём";
                    return p;
                }
                var hinge = doorway.transform.TransformPoint(DoorHingeLocal);
                p.Position = hinge;
                p.Orientation = doorway.transform.rotation;
                p.Doorway = doorway;
                p.HasPose = true;
                if (doorway.Door != null)
                {
                    p.Reason = "Дверь уже стоит";
                    return p;
                }
                p.Valid = true;
                return p;
            }

            Vector3 point;
            if (hit)
            {
                point = info.point + info.normal * 0.05f;
            }
            else
            {
                // Aiming at the sky: floors still snap to the level above the player.
                point = ray.origin + ray.direction * Mathf.Min(range, 4f);
            }

            var kind = ToKind(mode);
            const float s = BuildGrid.CellSize;
            int cx = Mathf.FloorToInt(point.x / s);
            int cz = Mathf.FloorToInt(point.z / s);
            float rel = point.y - BuildGrid.FoundationHeight;
            SlotKey slot;
            switch (kind)
            {
                case PieceKind.Foundation:
                    if (!hit && point.y > 0.5f)
                    {
                        // Project onto the ground in front of the player.
                        if (ray.direction.y >= -0.05f) { p.Reason = "Наведите на землю"; return p; }
                        float t = -ray.origin.y / ray.direction.y;
                        if (t > range) { p.Reason = "Слишком далеко"; return p; }
                        point = ray.origin + ray.direction * t;
                        cx = Mathf.FloorToInt(point.x / s);
                        cz = Mathf.FloorToInt(point.z / s);
                    }
                    slot = SlotKey.Cell(cx, 0, cz);
                    break;
                case PieceKind.Floor:
                    slot = SlotKey.Cell(cx, Mathf.Max(1, Mathf.RoundToInt(rel / BuildGrid.LevelHeight)), cz);
                    break;
                case PieceKind.Stairs:
                    slot = SlotKey.Volume(cx, Mathf.Max(0, Mathf.FloorToInt((rel + 0.3f) / BuildGrid.LevelHeight)), cz);
                    break;
                default:
                {
                    int level = Mathf.Max(0, Mathf.FloorToInt((rel + 0.3f) / BuildGrid.LevelHeight));
                    float fx = point.x / s - cx;
                    float fz = point.z / s - cz;
                    float south = fz, north = 1f - fz, west = fx, east = 1f - fx;
                    float min = Mathf.Min(Mathf.Min(south, north), Mathf.Min(west, east));
                    if (min == south) slot = SlotKey.South(cx, level, cz);
                    else if (min == north) slot = SlotKey.South(cx, level, cz + 1);
                    else if (min == west) slot = SlotKey.West(cx, level, cz);
                    else slot = SlotKey.West(cx + 1, level, cz);
                    break;
                }
            }

            p.Slot = slot;
            SlotPose(slot, rotation, out p.Position, out p.Orientation);
            p.HasPose = true;

            if (Vector3.Distance(ray.origin, p.Position) > range + 2f)
            {
                p.Reason = "Слишком далеко";
                return p;
            }

            switch (_grid.CanPlace(kind, slot))
            {
                case PlaceResult.Occupied:
                    p.Reason = "Место занято";
                    return p;
                case PlaceResult.NoSupport:
                    p.Reason = kind == PieceKind.Floor ? "Нужна стена снизу" : kind == PieceKind.Stairs ? "Нужен пол или фундамент" : "Нужен фундамент рядом";
                    return p;
                case PlaceResult.BlockedByStairs:
                    p.Reason = "Мешает лестница";
                    return p;
                case PlaceResult.WrongSlot:
                    p.Reason = "Сюда нельзя";
                    return p;
            }

            if (IsObstructed(kind, p.Position, p.Orientation))
            {
                p.Reason = "Мешает объект";
                return p;
            }

            p.Valid = true;
            return p;
        }

        bool IsObstructed(PieceKind kind, Vector3 position, Quaternion orientation)
        {
            Vector3 half;
            Vector3 center = position;
            switch (kind)
            {
                case PieceKind.Foundation: half = new Vector3(1.45f, 0.29f, 1.45f); break;
                case PieceKind.Floor: half = new Vector3(1.4f, 0.09f, 1.4f); break;
                case PieceKind.Stairs:
                    half = new Vector3(0.9f, 1.4f, 1.4f);
                    center += Vector3.up * 1.5f;
                    break;
                default: half = new Vector3(1.4f, 1.4f, 0.14f); break;
            }

            int count = Physics.OverlapBoxNonAlloc(center, half, _overlap, orientation, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var col = _overlap[i];
                if (col == _groundCollider) continue;
                if (col.GetComponentInParent<BuildingPiece>() != null) continue;
                return true;
            }
            return false;
        }

        // ------------------------------------------------------------------ pieces

        static readonly Vector3 DoorHingeLocal = new Vector3(-Models.DoorWidth * 0.5f + 0.03f, -BuildGrid.LevelHeight * 0.5f + 0.02f, 0f);

        static void AddColliders(GameObject go, PieceKind kind)
        {
            const float s = BuildGrid.CellSize;
            const float h = BuildGrid.LevelHeight;
            const float t = Models.WallThickness;
            switch (kind)
            {
                case PieceKind.Foundation:
                    AddBox(go, Vector3.zero, new Vector3(s, BuildGrid.FoundationHeight, s), Quaternion.identity);
                    break;
                case PieceKind.Floor:
                    AddBox(go, Vector3.zero, new Vector3(s, 0.2f, s), Quaternion.identity);
                    break;
                case PieceKind.Wall:
                    AddBox(go, Vector3.zero, new Vector3(s, h, t), Quaternion.identity);
                    break;
                case PieceKind.Doorway:
                {
                    float side = (s - Models.DoorWidth) * 0.5f;
                    float lintel = h - Models.DoorHeight;
                    AddBox(go, new Vector3(-(Models.DoorWidth + side) * 0.5f, 0, 0), new Vector3(side, h, t), Quaternion.identity);
                    AddBox(go, new Vector3((Models.DoorWidth + side) * 0.5f, 0, 0), new Vector3(side, h, t), Quaternion.identity);
                    AddBox(go, new Vector3(0, h * 0.5f - lintel * 0.5f, 0), new Vector3(Models.DoorWidth, lintel, t), Quaternion.identity);
                    break;
                }
                case PieceKind.Stairs:
                {
                    // A 45 degree ramp: smooth to walk on, the steps are only visual.
                    float length = Mathf.Sqrt(s * s + h * h);
                    AddBox(go, new Vector3(0, h * 0.5f, 0), new Vector3(s * 0.62f, 0.1f, length), Quaternion.Euler(-45f, 0, 0));
                    break;
                }
            }
        }

        static void AddBox(GameObject parent, Vector3 center, Vector3 size, Quaternion rotation)
        {
            if (rotation == Quaternion.identity)
            {
                var box = parent.AddComponent<BoxCollider>();
                box.center = center;
                box.size = size;
                return;
            }

            // Rotated boxes need their own transform.
            var child = new GameObject("Collider");
            child.transform.SetParent(parent.transform, false);
            child.transform.localPosition = center;
            child.transform.localRotation = rotation;
            child.AddComponent<BoxCollider>().size = size;
        }

        void AttachDoor(BuildingPiece doorway)
        {
            var go = new GameObject("Door");
            go.transform.SetParent(doorway.transform, false);
            go.transform.localPosition = DoorHingeLocal;
            go.transform.localRotation = Quaternion.identity;
            var door = go.AddComponent<Door>();
            door.Init(go.transform);
            doorway.Door = door;
        }

        public void DestroyPiece(BuildingPiece piece)
        {
            if (piece == null) return;
            _removed.Clear();
            _grid.Remove(piece.Slot, _removed);
            if (_removed.Count == 0) _removed.Add(piece.Slot);

            for (int i = 0; i < _removed.Count; i++)
            {
                if (_pieces.TryGetValue(_removed[i], out var p) && p != null)
                {
                    _pieces.Remove(_removed[i]);
                    Destroy(p.gameObject);
                }
            }
        }

        // ------------------------------------------------------------------ hammer

        public bool TryGetTarget(Ray ray, float reach, out BuildingPiece piece, out Door door)
        {
            piece = null;
            door = null;
            if (!Physics.Raycast(ray, out var hit, reach, ~0, QueryTriggerInteraction.Ignore)) return false;
            door = hit.collider.GetComponentInParent<Door>();
            piece = hit.collider.GetComponentInParent<BuildingPiece>();
            return piece != null || door != null;
        }

        /// <summary>Upgrades the aimed piece one tier (twig → wood → stone → metal), or a wooden door to metal.</summary>
        public string TryUpgrade(Ray ray, float reach, Inventory inventory)
        {
            if (!TryGetTarget(ray, reach, out var piece, out var door)) return null;

            if (door != null)
            {
                if (door.Tier == BuildTier.Metal) return "Дверь уже металлическая";
                if (!inventory.TrySpend(MetalDoorCost)) return "Нужно 25 металла";
                door.SetTier(BuildTier.Metal);
                return "Дверь: металл";
            }

            if (piece.Tier == BuildTier.Metal) return "Максимальный уровень";
            var next = piece.Tier + 1;
            var cost = BuildingRules.UpgradeCost(piece.Kind, next);
            if (!inventory.TrySpend(cost)) return "Нужно: " + CostText(cost);
            piece.SetTier(next);
            return BuildingRules.KindName(piece.Kind) + ": " + BuildingRules.TierName(next);
        }

        public string TryRepair(Ray ray, float reach, Inventory inventory)
        {
            if (!TryGetTarget(ray, reach, out var piece, out var door)) return null;

            if (door != null)
            {
                if (door.HealthValue >= door.MaxHealth) return null;
                var doorCost = door.Tier == BuildTier.Metal ? new Cost(0, 0, 2) : new Cost(3);
                if (!inventory.TrySpend(doorCost)) return "Нечем чинить";
                door.Repair(door.MaxHealth * 0.15f);
                return null;
            }

            if (piece.HealthValue >= piece.MaxHealth) return null;
            if (!inventory.TrySpend(BuildingRules.RepairCost(piece.Tier))) return "Нечем чинить";
            piece.Repair(piece.MaxHealth * 0.15f);
            return null;
        }

        public static string CostText(Cost cost)
        {
            var text = "";
            if (cost.Wood > 0) text += cost.Wood + " дерева ";
            if (cost.Stone > 0) text += cost.Stone + " камня ";
            if (cost.Metal > 0) text += cost.Metal + " металла";
            return text.Trim();
        }

        // ------------------------------------------------------------------ placeables

        public Placement PreviewPlaceable(ItemId item, Ray ray, float yaw)
        {
            var p = new Placement { Orientation = Quaternion.Euler(0, yaw, 0) };
            if (!Physics.Raycast(ray, out var hit, _config.placeRange, ~0, QueryTriggerInteraction.Ignore) || hit.normal.y < 0.7f)
            {
                p.Reason = "Наведите на землю или пол";
                HideGhost();
                return p;
            }

            p.Position = hit.point;
            p.HasPose = true;
            var half = item == ItemId.WoodenSpikes ? new Vector3(1.1f, 0.3f, 0.3f) : new Vector3(0.45f, 0.2f, 1f);
            int count = Physics.OverlapBoxNonAlloc(hit.point + Vector3.up * (half.y + 0.05f), half, _overlap, p.Orientation, ~0,
                QueryTriggerInteraction.Collide);
            p.Valid = true;
            for (int i = 0; i < count; i++)
            {
                var col = _overlap[i];
                if (col == _groundCollider || col.GetComponentInParent<BuildingPiece>() != null) continue;
                p.Valid = false;
                p.Reason = "Мешает объект";
                break;
            }

            ShowGhost(item == ItemId.WoodenSpikes ? Models.Spikes() : Models.SleepingBag(), p.Position, p.Orientation, p.Valid);
            return p;
        }

        public void Place(ItemId item, Placement p, PlayerAvatar player)
        {
            if (!p.Valid || !player.Inventory.TryConsume(item)) return;

            var go = new GameObject(item.ToString());
            go.transform.SetParent(_root, false);
            go.transform.SetPositionAndRotation(p.Position, p.Orientation);
            go.AddComponent<MeshRenderer>().sharedMaterial = Palette.Opaque;

            if (item == ItemId.WoodenSpikes)
            {
                go.AddComponent<MeshFilter>().sharedMesh = Models.Spikes();
                var trigger = go.AddComponent<BoxCollider>();
                trigger.isTrigger = true;
                trigger.center = new Vector3(0, 0.4f, 0);
                trigger.size = new Vector3(2.2f, 0.8f, 0.6f);
                var spikes = go.AddComponent<Spikes>();
                if (Game.Horde != null) Game.Horde.RegisterSpikes(spikes);
            }
            else
            {
                go.AddComponent<MeshFilter>().sharedMesh = Models.SleepingBag();
                var box = go.AddComponent<BoxCollider>();
                box.center = new Vector3(0, 0.1f, 0);
                box.size = new Vector3(0.85f, 0.2f, 2f);
                var bag = go.AddComponent<SleepingBag>();
                if (player.Bag != null) Destroy(player.Bag.gameObject);
                bag.Owner = player;
                player.Bag = bag;
                player.Notify("Спальник установлен — здесь вы возродитесь");
            }
        }

        // ------------------------------------------------------------------ ghost

        void ShowGhost(Mesh mesh, Vector3 position, Quaternion orientation, bool valid)
        {
            if (!_ghost.activeSelf) _ghost.SetActive(true);
            _ghostFilter.sharedMesh = mesh;
            _ghost.transform.SetPositionAndRotation(position, orientation);
            _ghostMaterial.color = valid ? GhostOk : GhostBad;
        }

        public void HideGhost()
        {
            if (_ghost != null && _ghost.activeSelf) _ghost.SetActive(false);
        }
    }
}
