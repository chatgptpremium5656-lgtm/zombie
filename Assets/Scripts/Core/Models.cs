using System.Collections.Generic;
using UnityEngine;
using Zombie.Logic;

namespace Zombie.Core
{
    /// <summary>
    /// Procedural low-poly models, built once and shared. Simple, readable silhouettes with big shapes,
    /// no tiny detail (CLAUDE.md art rules). Triangle budgets are far under the limits in CLAUDE.md.
    /// </summary>
    public static class Models
    {
        static readonly MeshBuilder Builder = new MeshBuilder();
        static readonly Dictionary<int, Mesh> Cache = new Dictionary<int, Mesh>();

        const int TreeVariants = 6;
        const int RockVariants = 5;

        static Mesh Cached(int key, System.Func<Mesh> build)
        {
            if (Cache.TryGetValue(key, out var mesh) && mesh != null) return mesh;
            mesh = build();
            Cache[key] = mesh;
            return mesh;
        }

        // ---------------------------------------------------------------- nature

        public static Mesh Tree(int variant)
        {
            variant = Mathf.Abs(variant) % TreeVariants;
            return Cached(1000 + variant, () => BuildTree(variant));
        }

        static Mesh BuildTree(int variant)
        {
            var b = Builder;
            b.Clear();
            var rng = new System.Random(variant * 7919 + 11);
            System.Func<float> R = () => (float)rng.NextDouble();

            switch (variant % 3)
            {
                case 0: // pine: stacked cones
                {
                    float trunkH = 1.4f + R() * 0.4f;
                    b.Prism(Vector3.zero, Vector3.up, 0.32f, 0.22f, trunkH, 6, PaletteColor.Bark);
                    float y = trunkH * 0.7f;
                    float radius = 1.9f + R() * 0.3f;
                    for (int i = 0; i < 3; i++)
                    {
                        float h = 2.0f - i * 0.25f;
                        b.Prism(new Vector3(0, y, 0), Vector3.up, radius, 0f, h, 7,
                            i == 1 ? PaletteColor.LeafLight : PaletteColor.LeafDark, R() * 3f);
                        y += h * 0.55f;
                        radius *= 0.72f;
                    }
                    break;
                }
                case 1: // round crown, a bit sickly
                {
                    float trunkH = 2.2f + R() * 0.5f;
                    b.Prism(Vector3.zero, Vector3.up, 0.34f, 0.22f, trunkH, 6, PaletteColor.Bark);
                    b.Prism(new Vector3(0, trunkH * 0.6f, 0), new Vector3(0.6f, 1f, 0.1f), 0.14f, 0.08f, 1.1f, 5, PaletteColor.Bark);
                    b.Blob(new Vector3(0, trunkH + 1.0f, 0), new Vector3(1.7f, 1.4f, 1.7f), 0.18f, variant * 31 + 3,
                        PaletteColor.LeafDark, PaletteColor.DeadLeaf);
                    b.Blob(new Vector3(0.8f, trunkH + 0.4f, 0.3f), new Vector3(0.9f, 0.8f, 0.9f), 0.2f, variant * 31 + 4,
                        PaletteColor.LeafLight);
                    break;
                }
                default: // dead tree: bare trunk and a few branches
                {
                    float trunkH = 3.2f + R() * 0.8f;
                    b.Prism(Vector3.zero, Vector3.up, 0.36f, 0.12f, trunkH, 5, PaletteColor.Bark);
                    for (int i = 0; i < 3; i++)
                    {
                        float a = R() * Mathf.PI * 2f;
                        var dir = new Vector3(Mathf.Cos(a), 0.9f + R() * 0.5f, Mathf.Sin(a));
                        b.Prism(new Vector3(0, trunkH * (0.45f + i * 0.17f), 0), dir, 0.12f, 0.04f, 1.2f + R() * 0.5f, 4,
                            PaletteColor.Bark);
                    }
                    break;
                }
            }

            return b.ToMesh("Tree" + variant);
        }

        public static Mesh Rock(int variant, bool metal)
        {
            variant = Mathf.Abs(variant) % RockVariants;
            return Cached((metal ? 2100 : 2000) + variant, () => BuildRock(variant, metal));
        }

        static Mesh BuildRock(int variant, bool metal)
        {
            var b = Builder;
            b.Clear();
            int seed = variant * 104729 + (metal ? 7 : 1);
            var main = metal ? PaletteColor.StoneDark : PaletteColor.Stone;
            var accent = metal ? PaletteColor.MetalOre : PaletteColor.StoneLight;
            b.Blob(new Vector3(0, 0.55f, 0), new Vector3(1.25f, 0.95f, 1.05f), 0.22f, seed, main, accent);
            b.Blob(new Vector3(0.75f, 0.3f, 0.45f), new Vector3(0.55f, 0.45f, 0.5f), 0.25f, seed + 1, main, accent);
            return b.ToMesh(metal ? "MetalRock" + variant : "Rock" + variant);
        }

        public static Mesh Ground(float halfSize)
        {
            return Cached(3000 + (int)halfSize, () => BuildGround(halfSize));
        }

        static Mesh BuildGround(float halfSize)
        {
            var b = Builder;
            b.Clear();
            const float tile = 8f;
            int n = Mathf.CeilToInt(halfSize * 2f / tile) + 4; // a margin outside the playable area
            float start = -n * tile * 0.5f;
            var below = new Vector3(0, -10f, 0);
            for (int x = 0; x < n; x++)
            {
                for (int z = 0; z < n; z++)
                {
                    float x0 = start + x * tile, z0 = start + z * tile;
                    var p00 = new Vector3(x0, 0, z0);
                    var p10 = new Vector3(x0 + tile, 0, z0);
                    var p01 = new Vector3(x0, 0, z0 + tile);
                    var p11 = new Vector3(x0 + tile, 0, z0 + tile);
                    int hash = (x * 73856093) ^ (z * 19349663);
                    var c1 = (hash & 3) == 0 ? PaletteColor.GroundLight : PaletteColor.GroundDark;
                    var c2 = (hash & 12) == 4 ? PaletteColor.Dirt : c1;
                    b.Tri(p00, p01, p11, c1, p00 + below);
                    b.Tri(p00, p11, p10, c2, p00 + below);
                }
            }
            return b.ToMesh("Ground");
        }

        // ---------------------------------------------------------------- zombies

        public static Mesh Zombie(ZombieKind kind)
        {
            return Cached(4000 + (int)kind, () => BuildZombie(kind));
        }

        static Mesh BuildZombie(ZombieKind kind)
        {
            var b = Builder;
            b.Clear();
            var skin = kind == ZombieKind.Brute ? PaletteColor.BruteSkin : PaletteColor.ZombieSkin;
            var cloth = kind == ZombieKind.Runner ? PaletteColor.ZombieCloth2 : PaletteColor.ZombieCloth;

            // Unit-height model (feet at 0, top of head ~1.8). Size differences come from transform scale.
            float shoulders = kind == ZombieKind.Brute ? 0.75f : 0.5f;
            b.Box(new Vector3(-0.14f, 0.42f, 0), new Vector3(0.18f, 0.84f, 0.2f), PaletteColor.ZombieSkinDark);       // leg L
            b.Box(new Vector3(0.14f, 0.42f, 0.04f), new Vector3(0.18f, 0.84f, 0.2f), PaletteColor.ZombieSkinDark);    // leg R
            b.Box(new Vector3(0, 0.8f, 0), new Vector3(0.44f, 0.22f, 0.26f), cloth);                                 // hips
            b.Box(new Vector3(0, 1.17f, 0.04f), new Vector3(shoulders, 0.56f, 0.3f), Quaternion.Euler(10, 0, 0), cloth); // torso
            b.Box(new Vector3(0.06f, 1.0f, 0.2f), new Vector3(0.14f, 0.12f, 0.02f), PaletteColor.Blood);              // stain

            // Arms reach forward, the classic zombie pose.
            float armX = shoulders * 0.5f + 0.08f;
            b.Box(new Vector3(-armX, 1.32f, 0.3f), new Vector3(0.14f, 0.14f, 0.62f), Quaternion.Euler(-8, 4, 0), skin);
            b.Box(new Vector3(armX, 1.28f, 0.32f), new Vector3(0.14f, 0.14f, 0.62f), Quaternion.Euler(4, -6, 0), skin);

            // Big head, small glowing eyes for readability at night.
            b.Box(new Vector3(0.02f, 1.62f, 0.08f), new Vector3(0.36f, 0.36f, 0.34f), Quaternion.Euler(0, 0, 8), skin);
            b.Box(new Vector3(-0.07f, 1.65f, 0.26f), new Vector3(0.07f, 0.05f, 0.02f), PaletteColor.Eye);
            b.Box(new Vector3(0.1f, 1.66f, 0.26f), new Vector3(0.07f, 0.05f, 0.02f), PaletteColor.Eye);
            b.Box(new Vector3(0.02f, 1.52f, 0.25f), new Vector3(0.16f, 0.04f, 0.02f), PaletteColor.Blood);          // mouth

            if (kind == ZombieKind.Brute)
            {
                b.Box(new Vector3(0, 1.38f, -0.12f), new Vector3(0.6f, 0.26f, 0.28f), PaletteColor.ZombieSkinDark); // hunch
            }

            return b.ToMesh("Zombie_" + kind);
        }

        // ---------------------------------------------------------------- building

        public static PaletteColor TierColor(BuildTier tier)
        {
            switch (tier)
            {
                case BuildTier.Twig: return PaletteColor.Twig;
                case BuildTier.Wood: return PaletteColor.Wood;
                case BuildTier.Stone: return PaletteColor.Stone;
                default: return PaletteColor.Metal;
            }
        }

        static PaletteColor TierTrim(BuildTier tier)
        {
            switch (tier)
            {
                case BuildTier.Twig: return PaletteColor.Twig;
                case BuildTier.Wood: return PaletteColor.WoodDark;
                case BuildTier.Stone: return PaletteColor.StoneDark;
                default: return PaletteColor.Rust;
            }
        }

        public static Mesh Piece(PieceKind kind, BuildTier tier)
        {
            return Cached(5000 + (int)kind * 10 + (int)tier, () => BuildPiece(kind, tier));
        }

        public const float WallThickness = 0.3f;
        public const float DoorWidth = 1.2f;
        public const float DoorHeight = 2.2f;

        /// <summary>Piece meshes are centred on the slot centre (see BuildingSystem.SlotPose).</summary>
        static Mesh BuildPiece(PieceKind kind, BuildTier tier)
        {
            var b = Builder;
            b.Clear();
            var main = TierColor(tier);
            var trim = TierTrim(tier);
            const float s = BuildGrid.CellSize;
            const float h = BuildGrid.LevelHeight;
            const float t = WallThickness;

            switch (kind)
            {
                case PieceKind.Foundation:
                {
                    const float fh = BuildGrid.FoundationHeight;
                    b.Box(new Vector3(0, 0, 0), new Vector3(s, fh, s), main);
                    b.Box(new Vector3(0, -fh * 0.5f + 0.08f, 0), new Vector3(s + 0.08f, 0.16f, s + 0.08f), trim);
                    if (tier != BuildTier.Twig) AddPlanks(b, tier, s);
                    break;
                }
                case PieceKind.Floor:
                    b.Box(Vector3.zero, new Vector3(s, 0.2f, s), main);
                    if (tier != BuildTier.Twig) AddPlanks(b, tier, s, 0.1f);
                    break;
                case PieceKind.Wall:
                    if (tier == BuildTier.Twig) TwigFrame(b, s, h, t);
                    else
                    {
                        b.Box(Vector3.zero, new Vector3(s, h, t), main);
                        WallTrim(b, tier, trim, s, h, t);
                    }
                    break;
                case PieceKind.Doorway:
                {
                    float side = (s - DoorWidth) * 0.5f;
                    float lintel = h - DoorHeight;
                    var color = tier == BuildTier.Twig ? PaletteColor.Twig : main;
                    b.Box(new Vector3(-(DoorWidth + side) * 0.5f, 0, 0), new Vector3(side, h, t), color);
                    b.Box(new Vector3((DoorWidth + side) * 0.5f, 0, 0), new Vector3(side, h, t), color);
                    b.Box(new Vector3(0, h * 0.5f - lintel * 0.5f, 0), new Vector3(DoorWidth, lintel, t), color);
                    b.Box(new Vector3(0, h * 0.5f - lintel, 0), new Vector3(DoorWidth + 0.2f, 0.14f, t + 0.06f), trim);
                    break;
                }
                case PieceKind.Stairs:
                {
                    // Straight flight from the floor of this level to the floor above (local pivot = cell centre at floor height).
                    const int steps = 6;
                    float run = s / steps, rise = h / steps;
                    for (int i = 0; i < steps; i++)
                    {
                        float z = -s * 0.5f + run * (i + 0.5f);
                        float top = rise * (i + 1);
                        b.Box(new Vector3(0, top * 0.5f, z), new Vector3(s * 0.62f, top, run), i % 2 == 0 ? main : trim);
                    }
                    break;
                }
            }

            return b.ToMesh(kind + "_" + tier);
        }

        static void AddPlanks(MeshBuilder b, BuildTier tier, float size, float yTop = 0.3f)
        {
            var trim = TierTrim(tier);
            if (tier == BuildTier.Wood)
            {
                for (int i = 0; i < 3; i++)
                    b.Box(new Vector3(-size * 0.33f + i * size * 0.33f, yTop, 0), new Vector3(0.12f, 0.02f, size * 0.96f), trim);
            }
            else if (tier == BuildTier.Stone)
            {
                b.Box(new Vector3(0, yTop, 0), new Vector3(size * 0.96f, 0.02f, 0.1f), trim);
                b.Box(new Vector3(0, yTop, 0), new Vector3(0.1f, 0.02f, size * 0.96f), trim);
            }
            else
            {
                b.Box(new Vector3(0, yTop, 0), new Vector3(size * 0.7f, 0.02f, size * 0.7f), trim);
            }
        }

        static void WallTrim(MeshBuilder b, BuildTier tier, PaletteColor trim, float s, float h, float t)
        {
            float face = t * 0.5f + 0.01f;
            if (tier == BuildTier.Wood)
            {
                // Vertical boards + two horizontal battens.
                for (int i = 0; i < 2; i++)
                {
                    float y = i == 0 ? -h * 0.25f : h * 0.25f;
                    b.Box(new Vector3(0, y, 0), new Vector3(s * 0.96f, 0.16f, t + 0.06f), trim);
                }
            }
            else if (tier == BuildTier.Stone)
            {
                // Two staggered block lines on each face.
                b.Box(new Vector3(0, -h * 0.17f, 0), new Vector3(s * 0.98f, 0.06f, t + 0.04f), trim);
                b.Box(new Vector3(0, h * 0.17f, 0), new Vector3(s * 0.98f, 0.06f, t + 0.04f), trim);
                b.Box(new Vector3(-s * 0.2f, 0, face), new Vector3(0.06f, h * 0.3f, 0.02f), trim);
                b.Box(new Vector3(s * 0.25f, 0, -face), new Vector3(0.06f, h * 0.3f, 0.02f), trim);
            }
            else
            {
                // Sheet metal: frame and a rusty patch.
                b.Box(new Vector3(0, h * 0.5f - 0.1f, 0), new Vector3(s, 0.2f, t + 0.06f), PaletteColor.StoneDark);
                b.Box(new Vector3(0, -h * 0.5f + 0.1f, 0), new Vector3(s, 0.2f, t + 0.06f), PaletteColor.StoneDark);
                b.Box(new Vector3(s * 0.2f, -h * 0.1f, face), new Vector3(0.6f, 0.5f, 0.02f), trim);
            }
        }

        static void TwigFrame(MeshBuilder b, float s, float h, float t)
        {
            // Twig walls read as a loose frame of sticks; the collider is still a full wall.
            float stick = 0.12f;
            b.Box(new Vector3(-s * 0.5f + stick, 0, 0), new Vector3(stick, h, stick), PaletteColor.Twig);
            b.Box(new Vector3(s * 0.5f - stick, 0, 0), new Vector3(stick, h, stick), PaletteColor.Twig);
            b.Box(new Vector3(0, h * 0.5f - stick, 0), new Vector3(s, stick, stick), PaletteColor.Twig);
            b.Box(new Vector3(0, -h * 0.5f + stick, 0), new Vector3(s, stick, stick), PaletteColor.Twig);
            b.Box(new Vector3(0, 0, 0), new Vector3(Mathf.Sqrt(s * s + h * h) * 0.92f, stick, stick),
                Quaternion.Euler(0, 0, Mathf.Atan2(h, s) * Mathf.Rad2Deg), PaletteColor.Twig);
            b.Box(new Vector3(0, 0, 0), new Vector3(s * 0.96f, h * 0.96f, t * 0.25f), PaletteColor.WoodDark);
        }

        public static Mesh Door(BuildTier tier)
        {
            return Cached(6000 + (int)tier, () =>
            {
                var b = Builder;
                b.Clear();
                var main = tier == BuildTier.Metal ? PaletteColor.Metal : PaletteColor.Wood;
                var trim = tier == BuildTier.Metal ? PaletteColor.Rust : PaletteColor.WoodDark;
                // Pivot at the hinge (left edge, bottom).
                const float w = DoorWidth - 0.06f, dh = DoorHeight - 0.04f, th = 0.1f;
                b.Box(new Vector3(w * 0.5f, dh * 0.5f, 0), new Vector3(w, dh, th), main);
                b.Box(new Vector3(w * 0.5f, dh * 0.3f, 0), new Vector3(w * 0.96f, 0.12f, th + 0.04f), trim);
                b.Box(new Vector3(w * 0.5f, dh * 0.72f, 0), new Vector3(w * 0.96f, 0.12f, th + 0.04f), trim);
                b.Box(new Vector3(w - 0.14f, dh * 0.5f, 0), new Vector3(0.06f, 0.06f, th + 0.12f), PaletteColor.StoneDark);
                return b.ToMesh("Door_" + tier);
            });
        }

        // ---------------------------------------------------------------- placeables

        public static Mesh SleepingBag()
        {
            return Cached(7000, () =>
            {
                var b = Builder;
                b.Clear();
                b.Box(new Vector3(0, 0.08f, 0), new Vector3(0.85f, 0.16f, 2.0f), PaletteColor.Cloth);
                b.Prism(new Vector3(-0.42f, 0.2f, 0.78f), Vector3.right, 0.18f, 0.18f, 0.84f, 7, PaletteColor.Sleeve);
                b.Box(new Vector3(0, 0.18f, -0.15f), new Vector3(0.8f, 0.06f, 1.3f), PaletteColor.Sleeve);
                return b.ToMesh("SleepingBag");
            });
        }

        public static Mesh Spikes()
        {
            return Cached(7100, () =>
            {
                var b = Builder;
                b.Clear();
                b.Box(new Vector3(0, 0.08f, 0), new Vector3(2.2f, 0.16f, 0.25f), PaletteColor.WoodDark);
                for (int i = 0; i < 5; i++)
                {
                    float x = -0.9f + i * 0.45f;
                    float lean = i % 2 == 0 ? 25f : -25f;
                    b.Prism(new Vector3(x, 0.1f, 0), Quaternion.Euler(lean, 0, 0) * Vector3.up, 0.08f, 0f, 0.9f, 4, PaletteColor.Spike);
                }
                return b.ToMesh("Spikes");
            });
        }

        // ---------------------------------------------------------------- first person view models

        /// <summary>Held item model; pivot at the grip, pointing up/forward.</summary>
        public static Mesh ViewModel(ItemId item)
        {
            return Cached(8000 + (int)item, () => BuildViewModel(item));
        }

        static Mesh BuildViewModel(ItemId item)
        {
            var b = Builder;
            b.Clear();
            // Hand + sleeve always present.
            b.Box(new Vector3(0, -0.02f, -0.02f), new Vector3(0.09f, 0.1f, 0.11f), PaletteColor.PlayerSkin);
            b.Box(new Vector3(0, -0.06f, -0.22f), new Vector3(0.12f, 0.12f, 0.32f), PaletteColor.Sleeve);

            switch (item)
            {
                case ItemId.Rock:
                    b.Blob(new Vector3(0, 0.06f, 0.04f), new Vector3(0.09f, 0.08f, 0.09f), 0.2f, 5, PaletteColor.Stone, PaletteColor.StoneLight);
                    break;
                case ItemId.StoneHatchet:
                    b.Box(new Vector3(0, 0.18f, 0), new Vector3(0.035f, 0.5f, 0.035f), PaletteColor.Wood);
                    b.Box(new Vector3(0, 0.38f, 0.06f), new Vector3(0.05f, 0.11f, 0.16f), PaletteColor.Stone);
                    break;
                case ItemId.StonePickaxe:
                    b.Box(new Vector3(0, 0.18f, 0), new Vector3(0.035f, 0.5f, 0.035f), PaletteColor.Wood);
                    b.Box(new Vector3(0, 0.4f, 0), new Vector3(0.05f, 0.06f, 0.36f), Quaternion.Euler(-8, 0, 0), PaletteColor.Stone);
                    break;
                case ItemId.WoodenSpear:
                    b.Box(new Vector3(0, 0.1f, 0.25f), new Vector3(0.03f, 0.03f, 1.1f), PaletteColor.Wood);
                    b.Prism(new Vector3(0, 0.1f, 0.8f), Vector3.forward, 0.03f, 0f, 0.16f, 4, PaletteColor.WoodDark);
                    break;
                case ItemId.Machete:
                    b.Box(new Vector3(0, 0.05f, 0), new Vector3(0.04f, 0.13f, 0.04f), PaletteColor.WoodDark);
                    b.Box(new Vector3(0, 0.33f, 0.015f), new Vector3(0.01f, 0.45f, 0.07f), PaletteColor.Metal);
                    break;
                case ItemId.BuildingPlan:
                    b.Box(new Vector3(0, 0.1f, 0.03f), new Vector3(0.22f, 0.28f, 0.01f), Quaternion.Euler(-25, 0, 0), PaletteColor.Paper);
                    break;
                case ItemId.Hammer:
                    b.Box(new Vector3(0, 0.15f, 0), new Vector3(0.03f, 0.36f, 0.03f), PaletteColor.Wood);
                    b.Box(new Vector3(0, 0.33f, 0.02f), new Vector3(0.06f, 0.06f, 0.16f), PaletteColor.Metal);
                    break;
                case ItemId.SleepingBag:
                    b.Prism(new Vector3(-0.12f, 0.06f, 0.04f), Vector3.right, 0.08f, 0.08f, 0.24f, 7, PaletteColor.Cloth);
                    break;
                case ItemId.WoodenSpikes:
                    b.Prism(new Vector3(0, 0.0f, 0.04f), Vector3.up, 0.03f, 0f, 0.3f, 4, PaletteColor.Spike);
                    break;
            }

            return b.ToMesh("VM_" + item);
        }
    }
}
