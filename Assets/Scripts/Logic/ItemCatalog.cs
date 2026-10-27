namespace Zombie.Logic
{
    public enum ItemId
    {
        Rock = 0,
        StoneHatchet,
        StonePickaxe,
        WoodenSpear,
        Machete,
        BuildingPlan,
        Hammer,
        SleepingBag,
        WoodenSpikes,
    }

    public enum ItemKind
    {
        /// <summary>Melee tool or weapon; one per inventory.</summary>
        Tool,
        /// <summary>Building plan / hammer; one per inventory.</summary>
        BuildTool,
        /// <summary>Placed into the world; stacks.</summary>
        Placeable,
    }

    public sealed class ItemDef
    {
        public ItemId Id;
        public string Name;
        public ItemKind Kind;
        public Cost Recipe;
        /// <summary>Amount produced by one craft.</summary>
        public int CraftAmount = 1;

        // Melee stats (tools only).
        public float Damage;
        public int WoodPerHit;
        public int StonePerHit;
        public int MetalPerHit;
        public float Cooldown = 0.7f;
        public float Reach = 2.4f;
    }

    /// <summary>
    /// All items of the prototype. Plain data so it can be unit tested and later moved to ScriptableObjects
    /// without changing callers.
    /// </summary>
    public static class ItemCatalog
    {
        static readonly ItemDef[] Defs =
        {
            new ItemDef { Id = ItemId.Rock, Name = "Камень", Kind = ItemKind.Tool, Recipe = new Cost(0),
                Damage = 8, WoodPerHit = 2, StonePerHit = 2, MetalPerHit = 0, Cooldown = 0.75f, Reach = 2.2f },
            new ItemDef { Id = ItemId.StoneHatchet, Name = "Каменный топор", Kind = ItemKind.Tool, Recipe = new Cost(30, 15),
                Damage = 14, WoodPerHit = 6, StonePerHit = 1, MetalPerHit = 0, Cooldown = 0.6f, Reach = 2.4f },
            new ItemDef { Id = ItemId.StonePickaxe, Name = "Каменная кирка", Kind = ItemKind.Tool, Recipe = new Cost(30, 20),
                Damage = 12, WoodPerHit = 1, StonePerHit = 6, MetalPerHit = 3, Cooldown = 0.7f, Reach = 2.4f },
            new ItemDef { Id = ItemId.WoodenSpear, Name = "Деревянное копьё", Kind = ItemKind.Tool, Recipe = new Cost(40),
                Damage = 26, WoodPerHit = 1, StonePerHit = 0, MetalPerHit = 0, Cooldown = 0.65f, Reach = 3.0f },
            new ItemDef { Id = ItemId.Machete, Name = "Мачете", Kind = ItemKind.Tool, Recipe = new Cost(20, 0, 25),
                Damage = 42, WoodPerHit = 3, StonePerHit = 0, MetalPerHit = 0, Cooldown = 0.45f, Reach = 2.5f },
            new ItemDef { Id = ItemId.BuildingPlan, Name = "План постройки", Kind = ItemKind.BuildTool, Recipe = new Cost(10) },
            new ItemDef { Id = ItemId.Hammer, Name = "Молоток", Kind = ItemKind.BuildTool, Recipe = new Cost(15) },
            new ItemDef { Id = ItemId.SleepingBag, Name = "Спальник", Kind = ItemKind.Placeable, Recipe = new Cost(30) },
            new ItemDef { Id = ItemId.WoodenSpikes, Name = "Колья", Kind = ItemKind.Placeable, Recipe = new Cost(40), CraftAmount = 1 },
        };

        public static int Count => Defs.Length;

        public static ItemDef Get(ItemId id) => Defs[(int)id];

        public static ItemDef GetByIndex(int index) => Defs[index];

        /// <summary>Items that can be crafted from the craft menu (the rock is free and always owned).</summary>
        public static bool IsCraftable(ItemId id) => id != ItemId.Rock;
    }
}
