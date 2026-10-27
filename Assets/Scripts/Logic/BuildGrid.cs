using System;
using System.Collections.Generic;

namespace Zombie.Logic
{
    public enum PieceKind
    {
        Foundation = 0,
        Wall,
        Doorway,
        Floor,
        Stairs,
    }

    public enum BuildTier
    {
        Twig = 0,
        Wood,
        Stone,
        Metal,
    }

    /// <summary>Which socket of the grid a piece occupies.</summary>
    public enum SlotType : byte
    {
        /// <summary>Horizontal plate at the base of a level: foundation (level 0) or floor (level 1+).</summary>
        CellFloor = 0,
        /// <summary>The air volume of a cell: stairs.</summary>
        CellVolume = 1,
        /// <summary>Wall on the south edge of cell (x, z), running along X.</summary>
        EdgeSouth = 2,
        /// <summary>Wall on the west edge of cell (x, z), running along Z.</summary>
        EdgeWest = 3,
    }

    public readonly struct SlotKey : IEquatable<SlotKey>
    {
        public readonly int X;
        public readonly int Level;
        public readonly int Z;
        public readonly SlotType Type;

        public SlotKey(int x, int level, int z, SlotType type)
        {
            X = x;
            Level = level;
            Z = z;
            Type = type;
        }

        public bool IsEdge => Type == SlotType.EdgeSouth || Type == SlotType.EdgeWest;

        public SlotKey WithLevel(int level) => new SlotKey(X, level, Z, Type);

        public bool Equals(SlotKey other) => X == other.X && Level == other.Level && Z == other.Z && Type == other.Type;

        public override bool Equals(object obj) => obj is SlotKey other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int h = X * 73856093;
                h ^= Level * 19349663;
                h ^= Z * 83492791;
                return h * 31 + (int)Type;
            }
        }

        public override string ToString() => $"{Type}({X},{Level},{Z})";

        public static SlotKey Cell(int x, int level, int z) => new SlotKey(x, level, z, SlotType.CellFloor);
        public static SlotKey Volume(int x, int level, int z) => new SlotKey(x, level, z, SlotType.CellVolume);
        public static SlotKey South(int x, int level, int z) => new SlotKey(x, level, z, SlotType.EdgeSouth);
        public static SlotKey West(int x, int level, int z) => new SlotKey(x, level, z, SlotType.EdgeWest);
    }

    public enum PlaceResult
    {
        Ok = 0,
        Occupied,
        NoSupport,
        WrongSlot,
        BlockedByStairs,
    }

    /// <summary>
    /// Rust-like socket grid. Cells are 3 x 3 m, levels are 3 m high, foundations sit on level 0.
    /// Knows nothing about Unity: it only decides what may be placed where and what collapses
    /// when a piece is destroyed. BuildingSystem maps slots to GameObjects.
    /// </summary>
    public sealed class BuildGrid
    {
        public const float CellSize = 3f;
        public const float FoundationHeight = 0.6f;
        public const float LevelHeight = 3f;
        public const int MaxLevel = 4;

        readonly Dictionary<SlotKey, PieceKind> _pieces = new Dictionary<SlotKey, PieceKind>();
        readonly Stack<SlotKey> _collapseStack = new Stack<SlotKey>();

        public int Count => _pieces.Count;

        public static float LevelBaseY(int level) => FoundationHeight + level * LevelHeight;

        public static SlotType SlotTypeFor(PieceKind kind, bool westEdge)
        {
            switch (kind)
            {
                case PieceKind.Foundation:
                case PieceKind.Floor:
                    return SlotType.CellFloor;
                case PieceKind.Stairs:
                    return SlotType.CellVolume;
                default:
                    return westEdge ? SlotType.EdgeWest : SlotType.EdgeSouth;
            }
        }

        public bool TryGet(SlotKey key, out PieceKind kind) => _pieces.TryGetValue(key, out kind);

        public bool IsOccupied(SlotKey key) => _pieces.ContainsKey(key);

        public PlaceResult CanPlace(PieceKind kind, SlotKey key)
        {
            if (key.Level < 0 || key.Level > MaxLevel) return PlaceResult.WrongSlot;

            switch (kind)
            {
                case PieceKind.Foundation:
                    if (key.Type != SlotType.CellFloor || key.Level != 0) return PlaceResult.WrongSlot;
                    break;
                case PieceKind.Floor:
                    if (key.Type != SlotType.CellFloor || key.Level < 1) return PlaceResult.WrongSlot;
                    break;
                case PieceKind.Stairs:
                    if (key.Type != SlotType.CellVolume) return PlaceResult.WrongSlot;
                    break;
                case PieceKind.Wall:
                case PieceKind.Doorway:
                    if (!key.IsEdge) return PlaceResult.WrongSlot;
                    break;
            }

            if (_pieces.ContainsKey(key)) return PlaceResult.Occupied;

            // Stairs run through the floor of the level above, so the two exclude each other.
            if (kind == PieceKind.Floor && _pieces.ContainsKey(SlotKey.Volume(key.X, key.Level - 1, key.Z)))
                return PlaceResult.BlockedByStairs;
            if (kind == PieceKind.Stairs && _pieces.ContainsKey(SlotKey.Cell(key.X, key.Level + 1, key.Z)))
                return PlaceResult.BlockedByStairs;

            return HasSupport(kind, key) ? PlaceResult.Ok : PlaceResult.NoSupport;
        }

        public PlaceResult Place(PieceKind kind, SlotKey key)
        {
            var result = CanPlace(kind, key);
            if (result == PlaceResult.Ok) _pieces[key] = kind;
            return result;
        }

        /// <summary>
        /// Removes a piece and everything that loses its support because of it.
        /// Every removed slot (the piece itself first) is appended to <paramref name="removed"/>.
        /// </summary>
        public void Remove(SlotKey key, List<SlotKey> removed)
        {
            if (!_pieces.Remove(key)) return;
            removed.Add(key);

            _collapseStack.Clear();
            PushDependents(key);
            while (_collapseStack.Count > 0)
            {
                var candidate = _collapseStack.Pop();
                if (!_pieces.TryGetValue(candidate, out var kind)) continue;
                if (HasSupport(kind, candidate)) continue;
                _pieces.Remove(candidate);
                removed.Add(candidate);
                PushDependents(candidate);
            }
        }

        public bool HasSupport(PieceKind kind, SlotKey key)
        {
            switch (kind)
            {
                case PieceKind.Foundation:
                    return true; // sits on the ground

                case PieceKind.Floor:
                    // A floor rests on any wall under one of its four edges.
                    return IsWallLike(SlotKey.South(key.X, key.Level - 1, key.Z))
                        || IsWallLike(SlotKey.South(key.X, key.Level - 1, key.Z + 1))
                        || IsWallLike(SlotKey.West(key.X, key.Level - 1, key.Z))
                        || IsWallLike(SlotKey.West(key.X + 1, key.Level - 1, key.Z));

                case PieceKind.Stairs:
                    return _pieces.ContainsKey(SlotKey.Cell(key.X, key.Level, key.Z));

                default:
                    // A wall stands on a foundation/floor of either neighbouring cell, or on the wall below.
                    if (IsWallLike(key.WithLevel(key.Level - 1))) return true;
                    if (_pieces.ContainsKey(SlotKey.Cell(key.X, key.Level, key.Z))) return true;
                    if (key.Type == SlotType.EdgeSouth)
                        return _pieces.ContainsKey(SlotKey.Cell(key.X, key.Level, key.Z - 1));
                    return _pieces.ContainsKey(SlotKey.Cell(key.X - 1, key.Level, key.Z));
            }
        }

        bool IsWallLike(SlotKey key)
        {
            return _pieces.TryGetValue(key, out var kind) && (kind == PieceKind.Wall || kind == PieceKind.Doorway);
        }

        void PushDependents(SlotKey key)
        {
            int x = key.X, l = key.Level, z = key.Z;
            switch (key.Type)
            {
                case SlotType.CellFloor:
                    _collapseStack.Push(SlotKey.South(x, l, z));
                    _collapseStack.Push(SlotKey.South(x, l, z + 1));
                    _collapseStack.Push(SlotKey.West(x, l, z));
                    _collapseStack.Push(SlotKey.West(x + 1, l, z));
                    _collapseStack.Push(SlotKey.Volume(x, l, z));
                    break;
                case SlotType.EdgeSouth:
                    _collapseStack.Push(key.WithLevel(l + 1));
                    _collapseStack.Push(SlotKey.Cell(x, l + 1, z));
                    _collapseStack.Push(SlotKey.Cell(x, l + 1, z - 1));
                    break;
                case SlotType.EdgeWest:
                    _collapseStack.Push(key.WithLevel(l + 1));
                    _collapseStack.Push(SlotKey.Cell(x, l + 1, z));
                    _collapseStack.Push(SlotKey.Cell(x - 1, l + 1, z));
                    break;
            }
        }
    }
}
