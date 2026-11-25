using System;
using System.Text;
using Kezor.Core.Characters;

namespace Kezor.Core.Items
{
    /// <summary>
    /// Aparência de um jogador para os outros clientes: o que está equipado (id, nível de runa, Ascendente) em cada slot
    /// visível, mais se está montado. Vai no snapshot (@S) num campo só, sem ';' nem '|':
    /// "id.nível.asc,id.nível.asc,...(um por slot de <see cref="Slots"/>)/montado" — slot vazio = "0".
    /// </summary>
    public sealed class Appearance
    {
        public struct Piece
        {
            public int Id, Level;
            public bool Ascended;
            public ItemDefinition? Def => Id == 0 ? null : ItemDatabase.Get(Id);
            public bool Empty => Id == 0;
        }

        /// <summary>Ordem dos slots no campo do protocolo.</summary>
        public static readonly EquipSlot[] Slots =
        {
            EquipSlot.RightHand, EquipSlot.LeftHand, EquipSlot.Helm, EquipSlot.Armor, EquipSlot.Pants,
            EquipSlot.Gloves, EquipSlot.Boots, EquipSlot.Wings, EquipSlot.Mount, EquipSlot.Pet
        };

        public readonly Piece[] Pieces = new Piece[Slots.Length];
        public bool Mounted;

        public Piece this[EquipSlot slot]
        {
            get { int i = Array.IndexOf(Slots, slot); return i < 0 ? default : Pieces[i]; }
        }

        /// <summary>Maior nível de runa entre arma e armaduras (para o brilho do corpo).</summary>
        public int MaxArmorLevel
        {
            get
            {
                int m = 0;
                foreach (var s in new[] { EquipSlot.Helm, EquipSlot.Armor, EquipSlot.Pants, EquipSlot.Gloves, EquipSlot.Boots })
                    m = Math.Max(m, this[s].Level);
                return m;
            }
        }

        public static Appearance Of(Character c)
        {
            var a = new Appearance { Mounted = c.Mounted };
            for (int i = 0; i < Slots.Length; i++)
                if (c.Equipment.TryGetValue(Slots[i], out var it))
                    a.Pieces[i] = new Piece { Id = it.Def.Id, Level = it.Level, Ascended = it.Rarity == ItemRarity.Ascended };
            return a;
        }

        public static string Encode(Character c) => Of(c).Encode();

        public string Encode()
        {
            var sb = new StringBuilder();
            for (int i = 0; i < Pieces.Length; i++)
            {
                if (i > 0) sb.Append(',');
                var p = Pieces[i];
                if (p.Id == 0) sb.Append('0');
                else sb.Append(p.Id).Append('.').Append(p.Level).Append('.').Append(p.Ascended ? 1 : 0);
            }
            return sb.Append('/').Append(Mounted ? 1 : 0).ToString();
        }

        /// <summary>Lê o campo do protocolo; tolera texto vazio/antigo (devolve aparência vazia).</summary>
        public static Appearance Parse(string? s)
        {
            var a = new Appearance();
            if (string.IsNullOrEmpty(s)) return a;
            var halves = s!.Split('/');
            var parts = halves[0].Split(',');
            for (int i = 0; i < parts.Length && i < Slots.Length; i++)
            {
                var f = parts[i].Split('.');
                int.TryParse(f[0], out var id);
                var p = new Piece { Id = id };
                if (f.Length > 1) int.TryParse(f[1], out p.Level);
                if (f.Length > 2) p.Ascended = f[2] == "1";
                if (p.Id != 0 && ItemDatabase.Get(p.Id) == null) p = default; // id desconhecido: ignora
                a.Pieces[i] = p;
            }
            if (halves.Length > 1) a.Mounted = halves[1] == "1";
            return a;
        }
    }
}
