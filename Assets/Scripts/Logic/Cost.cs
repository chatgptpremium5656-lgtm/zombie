using System;

namespace Zombie.Logic
{
    public enum ResourceType
    {
        Wood = 0,
        Stone = 1,
        Metal = 2,
    }

    /// <summary>Resource price of a recipe, a building piece or an upgrade.</summary>
    [Serializable]
    public struct Cost
    {
        public int Wood;
        public int Stone;
        public int Metal;

        public Cost(int wood, int stone = 0, int metal = 0)
        {
            Wood = wood;
            Stone = stone;
            Metal = metal;
        }

        public static readonly Cost Free = new Cost(0);

        public bool IsFree => Wood <= 0 && Stone <= 0 && Metal <= 0;

        public int Get(ResourceType type)
        {
            switch (type)
            {
                case ResourceType.Wood: return Wood;
                case ResourceType.Stone: return Stone;
                default: return Metal;
            }
        }

        /// <summary>Scales every component and rounds up, so a non-zero cost never becomes free.</summary>
        public Cost Scaled(float factor)
        {
            return new Cost(Scale(Wood, factor), Scale(Stone, factor), Scale(Metal, factor));
        }

        static int Scale(int value, float factor)
        {
            if (value <= 0) return 0;
            int scaled = (int)Math.Ceiling(value * factor);
            return scaled < 1 ? 1 : scaled;
        }

        public override string ToString()
        {
            return $"wood {Wood}, stone {Stone}, metal {Metal}";
        }
    }
}
