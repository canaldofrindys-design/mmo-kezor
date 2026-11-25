namespace MuProto.Core.Items
{
    /// <summary>Inventário por slots (protótipo; grade WxH fica para iteração futura).</summary>
    public sealed class Inventory
    {
        private readonly Item?[] _slots;
        public Inventory(int capacity) { _slots = new Item?[capacity]; }
        public int Capacity => _slots.Length;

        public Item? Get(int slot) => slot >= 0 && slot < _slots.Length ? _slots[slot] : null;

        public bool TryAdd(Item item, out int slot)
        {
            for (slot = 0; slot < _slots.Length; slot++)
                if (_slots[slot] == null) { _slots[slot] = item; return true; }
            slot = -1; return false;
        }

        public Item? Remove(int slot)
        {
            var it = Get(slot);
            if (it != null) _slots[slot] = null;
            return it;
        }

        public bool RemoveItem(Item item)
        {
            for (int i = 0; i < _slots.Length; i++)
                if (ReferenceEquals(_slots[i], item)) { _slots[i] = null; return true; }
            return false;
        }

        public bool Contains(Item item) => System.Array.IndexOf(_slots, item) >= 0;

        public int FreeSlots { get { int n = 0; foreach (var s in _slots) if (s == null) n++; return n; } }
    }
}
