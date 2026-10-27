using System;

namespace Zombie.Logic
{
    /// <summary>
    /// What a player carries: resources, owned tools and placeable stacks.
    /// Pure C# so the rules (crafting, spending, losing everything on death) are unit tested.
    /// </summary>
    public sealed class Inventory
    {
        const int ResourceTypeCount = 3;

        readonly int[] _resources = new int[ResourceTypeCount];
        readonly int[] _items = new int[ItemCatalog.Count];

        /// <summary>Raised after any change. Listeners refresh cached UI text here instead of every frame.</summary>
        public event Action Changed;

        /// <summary>Incremented on every change; cheap dirty check for UI.</summary>
        public int Version { get; private set; }

        public Inventory()
        {
            _items[(int)ItemId.Rock] = 1;
        }

        public int Get(ResourceType type) => _resources[(int)type];

        public int Count(ItemId id) => _items[(int)id];

        public bool Has(ItemId id) => _items[(int)id] > 0;

        public void Add(ResourceType type, int amount)
        {
            if (amount <= 0) return;
            _resources[(int)type] += amount;
            Notify();
        }

        public bool CanAfford(Cost cost)
        {
            return _resources[0] >= cost.Wood && _resources[1] >= cost.Stone && _resources[2] >= cost.Metal;
        }

        public bool TrySpend(Cost cost)
        {
            if (!CanAfford(cost)) return false;
            _resources[0] -= Math.Max(0, cost.Wood);
            _resources[1] -= Math.Max(0, cost.Stone);
            _resources[2] -= Math.Max(0, cost.Metal);
            Notify();
            return true;
        }

        public void AddItem(ItemId id, int amount = 1)
        {
            if (amount <= 0) return;
            var def = ItemCatalog.Get(id);
            if (def.Kind == ItemKind.Placeable) _items[(int)id] += amount;
            else _items[(int)id] = 1;
            Notify();
        }

        public bool TryConsume(ItemId id)
        {
            if (_items[(int)id] <= 0) return false;
            _items[(int)id]--;
            Notify();
            return true;
        }

        /// <summary>Tools are unique, so crafting one you already own is refused.</summary>
        public bool CanCraft(ItemId id)
        {
            if (!ItemCatalog.IsCraftable(id)) return false;
            var def = ItemCatalog.Get(id);
            if (def.Kind != ItemKind.Placeable && Has(id)) return false;
            return CanAfford(def.Recipe);
        }

        public bool TryCraft(ItemId id)
        {
            if (!CanCraft(id)) return false;
            var def = ItemCatalog.Get(id);
            TrySpend(def.Recipe);
            AddItem(id, def.CraftAmount);
            return true;
        }

        /// <summary>Death rule: all carried loot is lost, only the starting rock remains.</summary>
        public void ClearOnDeath()
        {
            Array.Clear(_resources, 0, _resources.Length);
            Array.Clear(_items, 0, _items.Length);
            _items[(int)ItemId.Rock] = 1;
            Notify();
        }

        void Notify()
        {
            Version++;
            Changed?.Invoke();
        }
    }
}
