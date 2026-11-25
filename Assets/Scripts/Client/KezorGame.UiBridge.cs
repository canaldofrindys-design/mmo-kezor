using System.Collections.Generic;
using UnityEngine;
using Kezor.Core.Combat;
using Kezor.Core.Items;
using Kezor.Core.Net;
using Kezor.Core.World;

namespace Kezor.Client
{
    /// <summary>
    /// Ponte entre a lógica do cliente e a apresentação (Kezor.Client.UI): as views leem o estado por aqui e pedem ações
    /// por aqui. Nada nesta ponte desenha; nada nas views fala com o servidor diretamente.
    /// </summary>
    public sealed partial class KezorGame
    {
        /// <summary>Janelas que a barra de menu e os atalhos abrem.</summary>
        public enum UiPanel { Character, Inventory, Party, Guild, Ranking, Map, Options, Help, Shop, Forge, MyShop, Ascension }

        public Camera Cam => _cam;
        public MapDefinition Map => _map;
        public Effects Fx => _fx;
        public IReadOnlyDictionary<int, EntityView> Views => _views;
        public IReadOnlyList<(NpcDefinition def, EntityView view)> Npcs => _npcs;
        public int SelectedTarget => _selectedTarget;
        public int HoverId => _hoverId;
        public int SkillSlot => _skillSlot;
        public bool InSafeZone => _map.InSafeZone(State.Me.X, State.Me.Z);

        /// <summary>A interface nova (UI Toolkit) desenha o HUD; o IMGUI antigo só desenha o que ainda não migrou.</summary>
        public bool ToolkitHud { get; set; }

        /// <summary>O chat da interface nova está com o foco: os atalhos do jogo ficam desligados.</summary>
        public bool ChatFocused { get => _chatOpen; set => _chatOpen = value; }

        public void SelectSkill(int slot) { if (slot >= 0 && slot < ClassSkills().Count) _skillSlot = slot; }

        public void UsePotionOf(PotionType t) => UsePotion(t);

        /// <summary>Usa o consumível do atalho Q/W/E/R (o servidor escolhe a pilha e valida).</summary>
        public void UseQuick(int q)
        {
            if (q < 0 || q >= State.Me.Quick.Length) return;
            int id = State.Me.Quick[q];
            if (id == 0) { State.AddLog("err", "Atalho vazio: arraste uma poção da mochila para ele."); return; }
            if (ItemCount(id) == 0) { State.AddLog("err", $"Sem {Kezor.Core.Items.ItemDatabase.Get(id)?.Name ?? "item"} na mochila."); return; }
            Send($"USEQ {q}");
        }

        /// <summary>Total do item na mochila (somando as pilhas).</summary>
        public int ItemCount(int itemId)
        {
            int n = 0;
            foreach (var it in State.Inventory) if (it != null && it.Def.Id == itemId) n += it.Quantity;
            return n;
        }

        public int PotionCount(PotionType t)
        {
            int n = 0;
            foreach (var it in State.Inventory) if (it != null && it.Def.Potion == t) n += it.Quantity;
            return n;
        }

        public bool IsOpen(UiPanel p) => p switch
        {
            UiPanel.Character => _showChar, UiPanel.Inventory => _showInv, UiPanel.Party => _showParty, UiPanel.Guild => _showGuild,
            UiPanel.Ranking => _showRanking, UiPanel.Help => _showHelp, UiPanel.Ascension => _showAscension, UiPanel.Shop => _showShop, UiPanel.Forge => _showForge,
            UiPanel.MyShop => _showMyShop, UiPanel.Map => _showWorldMap, UiPanel.Options => _showOptions,
            _ => false,
        };

        public void Toggle(UiPanel p)
        {
            switch (p)
            {
                case UiPanel.Character: _showChar = !_showChar; break;
                case UiPanel.Inventory: _showInv = !_showInv; break;
                case UiPanel.Party: _showParty = !_showParty; break;
                case UiPanel.Guild: _showGuild = !_showGuild; if (_showGuild) Send("GUILD INFO"); break;
                case UiPanel.Ranking: _showRanking = !_showRanking; if (_showRanking) OpenRanking(_rankTab); break;
                case UiPanel.Help: _showHelp = !_showHelp; break;
                case UiPanel.Map: _showWorldMap = !_showWorldMap; break;
                case UiPanel.Options: _showOptions = !_showOptions; break;
                case UiPanel.Ascension: _showAscension = !_showAscension; if (_showAscension) Send("ASC INFO"); break;
                case UiPanel.MyShop: _showMyShop = !_showMyShop; if (_showMyShop) { _showInv = true; Send("SHOP MINE"); } break;
            }
        }

        internal bool _showWorldMap, _showOptions, _showAscension;

        public void CloseAllPanels() => CloseAll();
        public bool Connected => _conn != null;

        public bool MinimapOn { get => _showMinimap; set => _showMinimap = value; }
        public string WarEnemyGuild => WarEnemy;

        /// <summary>Envia uma linha do chat: comandos com "/" vão crus; o canal decide SAY, grupo (/p) ou guilda.</summary>
        public void SendChat(string text, string channel)
        {
            var t = text.Trim();
            if (t.Length == 0) return;
            if (t.StartsWith("/g ")) { Send("GSAY " + t.Substring(3)); return; }
            if (t.StartsWith("/p ")) { Send("SAY " + t); return; }
            if (t.StartsWith("/")) { Send(t.Substring(1)); return; }
            switch (channel)
            {
                case "party": Send("SAY /p " + t); break;
                case "guild": Send("GSAY " + t); break;
                default: Send("SAY " + t); break;
            }
        }

        /// <summary>Nome da área onde o herói está (vila, bosque ou o mapa).</summary>
        public string ZoneName => InSafeZone ? "Vila de Brasalume" : _map.Id == MapDefinition.ValeId ? "Bosque Cinzento" : _map.Name;
    }
}
