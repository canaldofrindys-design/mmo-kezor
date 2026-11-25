using System.Collections.Generic;

namespace Kezor.Core.Items
{
    /// <summary>Tamanho de cada item na grade (largura × altura em casas). Sem rotação.</summary>
    public static class ItemSize
    {
        public static (int w, int h) Of(ItemDefinition d)
        {
            if (d.IsPotion || d.IsRune) return (1, 1);
            if (d.IsWings) return (3, 2);
            if (d.IsMount) return (2, 2);
            if (d.IsPet) return (1, 1);
            switch (d.Kind)
            {
                case ItemKind.Sword: return d.MaxDamage >= 40 || d.Epic ? (1, 4) : (1, 3);
                case ItemKind.Axe: return (2, 3);
                case ItemKind.Staff: return (1, 4);
                case ItemKind.Lance: return (1, 4);
                case ItemKind.Bow: return (2, 4);
                case ItemKind.Gauntlet: return (2, 2);
                case ItemKind.Tome: return (2, 2);
                case ItemKind.Shield: return (2, 2);
                case ItemKind.Helm: return (2, 2);
                case ItemKind.Armor: return (2, 3);
                case ItemKind.Pants: return (2, 2);
                case ItemKind.Gloves: return (2, 2);
                case ItemKind.Boots: return (2, 2);
            }
            return (1, 1);
        }
    }

    /// <summary>
    /// Inventário em grade (colunas × linhas). Cada item ocupa w × h casas a partir do canto superior esquerdo
    /// ("slot" = índice dessa casa). Consumíveis empilham. Sem rotação. Usado pela mochila (8×8) e pelo baú (8×15).
    /// </summary>
    public sealed class Inventory
    {
        public readonly int Columns, Rows;
        private readonly Item?[] _slots;   // item no canto de cada um
        private readonly int[] _owner;     // casa -> canto que a ocupa (-1 livre)

        public Inventory(int capacity, int columns = 8)
        {
            Columns = columns;
            Rows = (capacity + columns - 1) / columns;
            _slots = new Item?[Columns * Rows];
            _owner = new int[Columns * Rows];
            for (int i = 0; i < _owner.Length; i++) _owner[i] = -1;
        }

        public int Capacity => _slots.Length;
        /// <summary>Incrementado a cada alteração (o servidor usa para reenviar o inventário).</summary>
        public int Version { get; private set; }

        public Item? Get(int slot) => slot >= 0 && slot < _slots.Length ? _slots[slot] : null;

        /// <summary>Canto do item que ocupa a casa (ou -1).</summary>
        public int AnchorAt(int cell) => cell >= 0 && cell < _owner.Length ? _owner[cell] : -1;

        /// <summary>O item cabe com o canto em <paramref name="slot"/>? Casas do próprio <paramref name="ignore"/> contam como livres.</summary>
        public bool Fits(ItemDefinition d, int slot, int ignore = -1)
        {
            if (slot < 0 || slot >= _slots.Length) return false;
            var (w, h) = ItemSize.Of(d);
            int x0 = slot % Columns, y0 = slot / Columns;
            if (x0 + w > Columns || y0 + h > Rows) return false;
            for (int y = y0; y < y0 + h; y++)
                for (int x = x0; x < x0 + w; x++)
                {
                    int o = _owner[y * Columns + x];
                    if (o >= 0 && o != ignore) return false;
                }
            return true;
        }

        /// <summary>Primeiro lugar onde o item cabe (de cima para baixo, da esquerda para a direita) ou -1.</summary>
        public int FindSpot(ItemDefinition d)
        {
            for (int i = 0; i < _slots.Length; i++) if (Fits(d, i)) return i;
            return -1;
        }

        private void Place(int slot, Item item)
        {
            var (w, h) = ItemSize.Of(item.Def);
            int x0 = slot % Columns, y0 = slot / Columns;
            for (int y = y0; y < y0 + h; y++)
                for (int x = x0; x < x0 + w; x++) _owner[y * Columns + x] = slot;
            _slots[slot] = item;
        }

        private void Clear(int slot)
        {
            var it = _slots[slot];
            if (it == null) return;
            for (int i = 0; i < _owner.Length; i++) if (_owner[i] == slot) _owner[i] = -1;
            _slots[slot] = null;
        }

        public bool TryAdd(Item item, out int slot)
        {
            if (item.Def.MaxStack > 1)
            {
                for (slot = 0; slot < _slots.Length; slot++)
                {
                    var s = _slots[slot];
                    if (s != null && s.Def == item.Def && s.Quantity + item.Quantity <= s.Def.MaxStack)
                    { s.Quantity += item.Quantity; Version++; return true; }
                }
            }
            slot = FindSpot(item.Def);
            if (slot < 0) return false;
            Place(slot, item); Version++;
            return true;
        }

        /// <summary>Põe o item com o canto em <paramref name="slot"/> se couber (retorna false se não couber).</summary>
        public bool Put(int slot, Item item)
        {
            if (!Fits(item.Def, slot)) return false;
            Place(slot, item); Version++;
            return true;
        }

        public Item? Remove(int slot)
        {
            var it = Get(slot);
            if (it != null) { Clear(slot); Version++; }
            return it;
        }

        /// <summary>Move o item para outro canto dentro da grade (sem rotação). Retorna false se não couber.</summary>
        public bool Move(int from, int to)
        {
            var it = Get(from);
            if (it == null || from == to) return it != null;
            if (!Fits(it.Def, to, ignore: from)) return false;
            Clear(from); Place(to, it); Version++;
            return true;
        }

        /// <summary>
        /// Move para a casa <paramref name="to"/>; se ela estiver ocupada por um item do mesmo tamanho (e o que está
        /// sendo movido não couber), os dois trocam de lugar. Devolve o canto onde ficou o item que estava no destino
        /// (-1 se não houve troca). Falha se não couber nem der para trocar.
        /// </summary>
        public bool MoveOrSwap(int from, int to, out int swappedTo)
        {
            swappedTo = -1;
            var it = Get(from);
            if (it == null) return false;
            if (Move(from, to)) return true;
            int other = AnchorAt(to);
            var ot = other >= 0 ? Get(other) : null;
            if (ot == null || other == from || ItemSize.Of(ot.Def) != ItemSize.Of(it.Def)) return false;
            Clear(from); Clear(other);
            Place(other, it); Place(from, ot); Version++;
            swappedTo = from;
            return true;
        }

        /// <summary>Consome uma unidade do slot (remove se acabar).</summary>
        public bool ConsumeOne(int slot)
        {
            var it = Get(slot);
            if (it == null) return false;
            if (--it.Quantity <= 0) Clear(slot);
            Version++;
            return true;
        }

        public bool RemoveItem(Item item)
        {
            for (int i = 0; i < _slots.Length; i++)
                if (ReferenceEquals(_slots[i], item)) { Clear(i); Version++; return true; }
            return false;
        }

        public int IndexOf(Item item) => System.Array.IndexOf(_slots, item);
        public bool Contains(Item item) => IndexOf(item) >= 0;
        public void Touch() => Version++;

        /// <summary>Casas livres (não garante que um item grande caiba; use <see cref="FindSpot"/>).</summary>
        public int FreeSlots { get { int n = 0; foreach (var o in _owner) if (o < 0) n++; return n; } }

        /// <summary>Itens com o canto de cada um.</summary>
        public IEnumerable<(int slot, Item item)> Items()
        {
            for (int i = 0; i < _slots.Length; i++) if (_slots[i] != null) yield return (i, _slots[i]!);
        }

        /// <summary>Simula: tirando os itens dos slots dados, todos os itens novos cabem (empilhando quando der)?</summary>
        public bool CanReceive(IEnumerable<int> removing, IEnumerable<Item> adding)
        {
            var sim = new Inventory(Capacity, Columns);
            var gone = new HashSet<int>(removing);
            foreach (var (s, it) in Items()) if (!gone.Contains(s)) sim.Place(s, new Item(it.Def) { Quantity = it.Quantity });
            foreach (var it in adding) if (!sim.TryAdd(new Item(it.Def) { Quantity = it.Quantity }, out _)) return false;
            return true;
        }
    }
}
