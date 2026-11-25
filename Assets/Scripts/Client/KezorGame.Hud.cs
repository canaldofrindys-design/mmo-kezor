using System.Collections.Generic;
using UnityEngine;
using Kezor.Core.Items;

namespace Kezor.Client
{
    /// <summary>
    /// Estado das janelas e a lógica de interação com itens (seleção na mochila, modo runa, seleção da forja). O desenho
    /// fica todo em Kezor.Client.UI (UI Toolkit); aqui só há regras de cliente e envio de comandos.
    /// </summary>
    public sealed partial class KezorGame
    {
        internal bool _showChar, _showInv, _showShop, _showForge, _showParty, _showHelp, _showMinimap = true;
        private bool _chatOpen;
        internal int _selSlot = -1, _runeMode = -1;
        internal readonly List<int> _forgeSel = new List<int>();
        internal string _forgePreview = "";
        private bool _helpShownOnce;

        /// <summary>Cor do nome do item (Ascendente, runa, refinado, poção).</summary>
        internal string ItemColor(Item it)
        {
            if (it.Rarity == ItemRarity.Ascended) return "#6dff9e";
            if (it.Def.IsRune) return "#" + ColorUtility.ToHtmlStringRGB(Models.RuneColor(it.Def.Rune));
            if (it.Level >= 7) return "#ffd36a";
            if (it.Def.IsPotion) return it.Def.Potion == PotionType.Health ? "#ff8a8a" : "#8aa8ff";
            return "#e8e2d0";
        }

        /// <summary>Nome curto para slots sem ícone.</summary>
        internal static string Short(Item it)
        {
            var words = it.Def.Name.Split(' ');
            string n = words.Length > 2 ? words[0] + " " + words[words.Length - 1] : it.Def.Name;
            if (it.Level > 0) n += $" +{it.Level}";
            if (it.Quantity > 1) n += $"\nx{it.Quantity}";
            return n;
        }

        /// <summary>Clique num espaço da mochila: aplica runa, marca para a forja, seleciona ou (duplo clique) usa/transfere.</summary>
        internal void ClickSlot(int i, Item? it, bool dbl)
        {
            if (_runeMode >= 0)
            {
                if (it != null && i != _runeMode) Send($"RUNE {_runeMode} {i}");
                _runeMode = -1; _selSlot = -1;
                return;
            }
            if (_showForge)
            {
                if (it == null) return;
                if (_forgeSel.Contains(i)) _forgeSel.Remove(i); else _forgeSel.Add(i);
                RefreshForgePreview();
                return;
            }
            if (it == null) { _selSlot = -1; return; }
            if (dbl && State.TradeOpen) { Send($"TRADE ADD {i}"); _selSlot = -1; return; }
            if (dbl && MyShopEditing) { Send($"SHOP ADD {i} {_shopPriceText.Trim()}"); Send("SHOP MINE"); _selSlot = -1; return; }
            if (dbl && VaultVisible) { Send($"VPUT {i}"); _selSlot = -1; return; }
            if (dbl)
            {
                if (it.Def.Slot != EquipSlot.None) Send($"EQUIP {i}");
                else if (it.Def.IsPotion) Send($"USE {i}");
                _selSlot = -1;
                return;
            }
            _selSlot = i;
        }

        internal void RefreshForgePreview()
        {
            if (_forgeSel.Count == 0) { _forgePreview = "Selecione os itens na mochila."; return; }
            Send("FORGEPREVIEW " + string.Join(",", _forgeSel), r => _forgePreview = r.StartsWith("OK") ? r.Substring(3) : "<color=#ff7a7a>" + r.Substring(r.IndexOf(' ') + 1) + "</color>");
        }

        /// <summary>O clique cai na interface (e não no mundo) quando o ponteiro está sobre algo clicável dela.</summary>
        private void UpdatePointer()
        {
            if (!_helpShownOnce && InWorld) { _helpShownOnce = true; _showHelp = true; }
            PointerOverUi = !InWorld || (UI.UiRoot.Instance != null && UI.UiRoot.Instance.PointerOverUi(InputK.MousePosition));
        }
    }
}
