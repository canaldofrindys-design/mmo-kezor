using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using Kezor.Core.Characters;
using Kezor.Core.Combat;
using Kezor.Core.Items;
using Kezor.Core.Net;
using Kezor.Core.Server;
using Kezor.Core.World;

namespace Kezor.Client.UI
{
    /// <summary>Janela do jogo ligada a um estado do <see cref="KezorGame"/>: abre quando o estado pede, fecha pelo X ou Esc.</summary>
    public abstract class GameWindow : KzWindow
    {
        protected readonly UiRoot Ui;
        protected KezorGame G => Ui.Game;
        protected ClientState S => Ui.Game.State;

        protected GameWindow(UiRoot ui, string key, string title, float width, Vector2 pos) : base(key, title, width, pos)
        {
            Ui = ui;
            OnClose = Close;
        }

        public abstract bool Wanted { get; }
        public abstract void Close();
        public virtual void Refresh() { }
        public virtual void Opened() { }

        // ---------- ajudantes de layout
        protected static VisualElement Row(VisualElement parent, string cls = "") { var r = Kz.El("win-row " + cls, parent, pick: true); return r; }
        protected static TextField Field(VisualElement parent, int max, float width, string placeholder = "")
        {
            var f = new TextField { maxLength = max };
            f.AddToClassList("kz-field");
            f.style.width = width;
            if (placeholder != "") f.textEdition.placeholder = placeholder;
            f.RegisterCallback<FocusInEvent>(_ => KezorGame.Instance!.ChatFocused = true);
            f.RegisterCallback<FocusOutEvent>(_ => KezorGame.Instance!.ChatFocused = false);
            parent.Add(f);
            return f;
        }

        /// <summary>Slot de item com tooltip (e comparação com o equipado quando <paramref name="compare"/>).</summary>
        protected KzSlot ItemSlot(VisualElement parent, Func<Item?> item, bool compare, string extra = "")
        {
            var s = parent.Put(new KzSlot(extra));
            s.RegisterCallback<PointerEnterEvent>(e => { var it = item(); if (it != null && !Ui.Drag.Dragging) Ui.ShowItemTip(it, e.position, compare); });
            s.RegisterCallback<PointerMoveEvent>(e => { var it = item(); if (it != null && !Ui.Drag.Dragging) Ui.ShowItemTip(it, e.position, compare); });
            s.RegisterCallback<PointerLeaveEvent>(_ => Ui.HideItemTip());
            return s;
        }
    }

    /// <summary>Gerencia as janelas: mostra/esconde conforme o estado e atualiza só as abertas.</summary>
    public sealed class WindowManager
    {
        private readonly List<GameWindow> _all = new List<GameWindow>();
        private readonly Dictionary<GameWindow, bool> _shown = new Dictionary<GameWindow, bool>();

        public WindowManager(UiRoot ui)
        {
            void Add(GameWindow w) { ui.WindowLayer.Add(w); w.Show(false); _all.Add(w); _shown[w] = false; }
            Add(new CharacterWindow(ui));
            Add(new InventoryWindow(ui));
            Add(new VaultWindow(ui));
            Add(new TradeWindow(ui));
            Add(new NpcShopWindow(ui));
            Add(new ForgeWindow(ui));
            Add(new PartyWindow(ui));
            Add(new GuildWindow(ui));
            Add(new RankingWindow(ui));
            Add(new MyShopWindow(ui));
            Add(new ShopViewWindow(ui));
            Add(new MapWindow(ui));
            Add(new OptionsWindow(ui));
            Add(new HelpWindow(ui));
            Add(new AscensionWindow(ui));
        }

        public void Tick()
        {
            foreach (var w in _all)
            {
                bool want = w.Wanted;
                if (want != _shown[w]) { _shown[w] = want; w.Show(want); if (want) { w.Opened(); w.BringToFront(); } }
                if (want) w.Refresh();
            }
        }
    }

    /// <summary>Faixas de convite (grupo, troca, guilda, guerra) com Aceitar/Recusar e o placar da guerra de guildas.</summary>
    public sealed class PromptsView
    {
        private readonly UiRoot _ui;
        private readonly VisualElement _box, _war;
        private readonly Label _warText;
        private string _sig = "";

        private static (string text, Action yes)? _ask;
        /// <summary>Pergunta de sim/não na faixa de avisos (ex.: largar item valioso no chão).</summary>
        public static void Ask(string text, Action yes) => _ask = (text, yes);

        public PromptsView(UiRoot ui)
        {
            _ui = ui;
            _box = Kz.El("hud-prompts", ui.HudLayer);
            _war = Kz.El("kz-panel-dark hud-war", ui.HudLayer);
            _warText = Kz.Text("", "kz-text kz-num", _war);
        }

        public void Tick()
        {
            var g = _ui.Game; var s = g.State;
            var items = new List<(string text, Action yes, Action no)>();
            if (!string.IsNullOrEmpty(s.PendingInvite)) items.Add(($"<color=#8fe08f>{s.PendingInvite}</color> convidou você para o grupo", () => { g.Send("PARTY ACCEPT"); s.PendingInvite = ""; }, () => { s.PendingInvite = ""; }));
            if (!string.IsNullOrEmpty(s.TradeInviteFrom) && !s.TradeOpen) items.Add(($"<color=#ffd36a>{s.TradeInviteFrom}</color> quer trocar com você", () => { g.Send("TRADE ACCEPT"); s.TradeInviteFrom = ""; }, () => { g.Send("TRADE DECLINE"); s.TradeInviteFrom = ""; }));
            if (!string.IsNullOrEmpty(s.GuildInviteName) && s.GuildName == "") items.Add(($"<color=#8fd0ff>{s.GuildInviteFrom}</color> convidou você para a guilda <b>{s.GuildInviteName}</b>", () => { g.Send("GUILD ACCEPT"); s.GuildInviteName = ""; }, () => { g.Send("GUILD DECLINE"); s.GuildInviteName = ""; }));
            if (s.WarInviteFrom != "" && s.GuildRank == 2 && !s.AtWar) items.Add(($"<color=#ff8a7a>{s.WarInviteFrom}</color> declarou guerra à sua guilda!", () => { g.Send("GUILD WARACCEPT"); s.WarInviteFrom = ""; }, () => { g.Send("GUILD WARDECLINE"); s.WarInviteFrom = ""; }));
            if (_ask is var (askText, askYes)) items.Insert(0, (askText, () => { _ask = null; askYes(); }, () => { _ask = null; }));
            string sig = string.Join("|", items.Select(i => i.text));
            if (sig != _sig)
            {
                _sig = sig;
                _box.Clear();
                foreach (var (text, yes, no) in items)
                {
                    var p = Kz.El("kz-panel hud-prompt", _box, pick: true);
                    Kz.Text(text, "kz-text hud-prompt__text", p);
                    Kz.Btn("Aceitar", yes, "kz-btn kz-btn--primary kz-btn--small", p);
                    Kz.Btn("Recusar", no, "kz-btn kz-btn--small", p);
                }
            }
            _war.Show(s.AtWar);
            if (s.AtWar)
            {
                double left = Math.Max(0, s.WarEndsLocal - s.LocalTime);
                _warText.Set($"⚔ Guerra  <color=#8fd0ff>{s.WarA}</color>  <b>{s.WarScoreA}  ×  {s.WarScoreB}</b>  <color=#ff8a7a>{s.WarB}</color>    {(int)left / 60:00}:{(int)left % 60:00}");
            }
        }
    }

    // ====================================================================== personagem
    public sealed class CharacterWindow : GameWindow
    {
        private readonly Label _head, _sub, _free;
        private readonly Dictionary<string, Label> _stat = new Dictionary<string, Label>();
        private readonly List<Button> _plus = new List<Button>();
        private readonly Label _derived, _note;
        private readonly VisualElement _skills;
        private readonly Button _rebirth;
        private CharacterClass? _skillsFor;

        public CharacterWindow(UiRoot ui) : base(ui, "char", "Personagem", 360, new Vector2(20, 130))
        {
            _head = Kz.Text("", "kz-h2", Body);
            _sub = Kz.Text("", "kz-text-2", Body);
            Kz.El("kz-divider", Body);
            _free = Kz.Text("", "kz-text", Body);
            foreach (var (label, code) in new[] { ("Força", "STR"), ("Agilidade", "AGI"), ("Vitalidade", "VIT"), ("Energia", "ENE") })
            {
                var r = Row(Body, "win-row--stat");
                Kz.Text(label, "kz-text win-stat__label", r);
                _stat[code] = Kz.Text("", "kz-text kz-num win-stat__value", r);
                foreach (int n in new[] { 1, 10, 100 })
                {
                    int amount = n; string c = code;
                    _plus.Add(Kz.Btn("+" + n, () => G.Send($"ADD {c} {Mathf.Min(amount, S.Me.FreePoints)}"), "kz-btn kz-btn--small win-plus", r));
                }
            }
            Kz.El("kz-divider", Body);
            _derived = Kz.Text("", "kz-text win-derived", Body);
            Kz.El("kz-divider", Body);
            Kz.Text("Skills  <color=#7d7366>(1–6 escolhem · botão direito usa)</color>", "kz-text", Body);
            _skills = Kz.El("win-skills", Body);
            _note = Kz.Text("", "kz-text-2", Body);
            _rebirth = Kz.Btn("", () => G.Send("REBIRTH"), "kz-btn", Body);
        }

        public override bool Wanted => G._showChar;
        public override void Close() => G._showChar = false;

        public override void Refresh()
        {
            var m = S.Me;
            var def = ClassDefinition.Get(m.Class);
            _head.Set(m.Name);
            _sub.Set($"{def.DisplayName}  ·  Nível {m.Level}  ·  Renascimentos {m.Rebirths}" + (m.Infamy > 0 ? $"  ·  <color=#ff6a5a>Infâmia {m.Infamy}</color>" : ""));
            _free.Set(m.FreePoints > 0 ? $"Pontos livres: <color=#ffb347><b>{m.FreePoints}</b></color>" : "Pontos livres: <color=#7d7366>0</color>");
            _stat["STR"].Set(m.Str.ToString()); _stat["AGI"].Set(m.Agi.ToString()); _stat["VIT"].Set(m.Vit.ToString()); _stat["ENE"].Set(m.Ene.ToString());
            foreach (var b in _plus) b.SetEnabled(m.FreePoints > 0);
            _derived.Set($"Vida <b>{m.Hp}/{m.MaxHp}</b>     Mana <b>{m.Mp}/{m.MaxMp}</b>\n" +
                         $"Dano físico <b>{m.MinDmg}~{m.MaxDmg}</b>" + (m.MaxMag > 0 ? $"     Dano mágico <b>{m.MinMag}~{m.MaxMag}</b>" : "") + "\n" +
                         $"Defesa <b>{m.Defense}</b>     Taxa de ataque <b>{m.AttackRate}</b>     Taxa de defesa <b>{m.DefenseRate}</b>");
            if (_skillsFor != m.Class)
            {
                _skillsFor = m.Class;
                _skills.Clear();
                foreach (var sk in G.ClassSkills())
                {
                    var r = Row(_skills, "win-skill");
                    var slot = r.Put(new KzSlot("win-skill__icon"));
                    slot.SetIcon(SkillIcons.Get(sk));
                    var sp = ItemIcons.Skill(sk.Id); if (sp != null) slot.SetSprite(sp);
                    var col = Kz.El("win-skill__text", r);
                    Kz.Text($"<b>{sk.Name}</b>  <color=#8fa8ff>{sk.ManaCost} MP</color>  <color=#7d7366>nível {sk.ReqLevel}{(sk.ReqEnergy > 0 ? $" · energia {sk.ReqEnergy}" : "")}</color>", "kz-text", col);
                    Kz.Text(sk.Description, "kz-text-2", col);
                }
                _note.Set(m.Class switch
                {
                    CharacterClass.Warden => "<color=#ffb347>Combo (nível 10+): 3 skills seguidas, com 2 diferentes, em até 3 s.</color>",
                    CharacterClass.EmberFist => "<color=#ff8a3a>Fluxo de Brasa (nível 10+): 5 golpes no mesmo alvo, até 1,5 s entre eles, soltam uma explosão (FOR+AGI).</color>",
                    CharacterClass.Commander => "<color=#ffd36a>Estandartes: um de cada tipo; a aura vale para você e o grupo no raio. Energia fortalece a aura.</color>",
                    CharacterClass.Invoker => "<color=#b8f0ff>Ecos: até 2 ao mesmo tempo; lutam pelo seu alvo e somem se você trocar de mapa.</color>",
                    _ => "",
                });
            }
            _rebirth.text = $"Renascer (nível {Character.MaxLevel}, {Character.RebirthGoldCost(m.Rebirths):N0} ouro)";
            _rebirth.SetEnabled(m.Level >= Character.MaxLevel);
        }
    }

    // ====================================================================== baú
    public sealed class VaultWindow : GameWindow
    {
        private readonly GridView _grid;
        private readonly Label _gold;
        private readonly TextField _amount;

        public VaultWindow(UiRoot ui) : base(ui, "vault", "Ilsa, a Guardiã — Baú", 400, new Vector2(790, 110))
        {
            Kz.Text("Arraste itens entre o baú e a mochila (ou duplo clique). O baú é o mesmo em qualquer cidade.", "kz-text-2", Body);
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.AddToClassList("win-scroll"); scroll.style.height = 300;
            Body.Add(scroll);
            _grid = scroll.contentContainer.Put(new GridView(ui, DragZone.Vault, Vault.Columns, Vault.Rows, () => S.Vault) { Compare = true });
            _grid.OnClick = (slot, it, clicks) => { if (clicks >= 2) G.Send($"VTAKE {slot}"); };
            _gold = Kz.Text("", "kz-text", Body);
            var r = Row(Body);
            _amount = Field(r, 12, 130, "quantia");
            Kz.Btn("Guardar ouro", () => { if (long.TryParse(_amount.value, out var v) && v > 0) { G.Send($"VGOLDIN {v}"); _amount.value = ""; } }, "kz-btn", r);
            Kz.Btn("Retirar ouro", () => { if (long.TryParse(_amount.value, out var v) && v > 0) { G.Send($"VGOLDOUT {v}"); _amount.value = ""; } }, "kz-btn", r);
            userData = new DropTag(DragZone.Vault);
        }

        public override bool Wanted => S.VaultOpen;
        public override void Close() { G.Send("VCLOSE"); S.VaultOpen = false; }
        public override void Opened() => G._showInv = true;

        public override void Refresh()
        {
            _grid.Refresh();
            _gold.Set($"<color=#e8c27a>Ouro no baú: <b>{S.VaultGold:N0}</b></color>   ·   na mochila: {S.Me.Gold:N0}");
        }
    }

    // ====================================================================== troca
    public sealed class TradeWindow : GameWindow
    {
        private readonly KzSlot[] _mine = new KzSlot[12], _theirs = new KzSlot[12];
        private readonly Label _myGold, _theirGold, _myState, _theirState, _theirTitle;
        private readonly TextField _gold;
        private readonly Button _lock, _confirm;

        public TradeWindow(UiRoot ui) : base(ui, "trade", "Troca", 540, new Vector2(380, 300))
        {
            var cols = Row(Body, "win-row--top");
            VisualElement Col(out Label title, string t) { var c = Kz.El("win-col", cols, pick: true); title = Kz.Text(t, "kz-h2", c); return c; }
            var mine = Col(out _, "Você oferece");
            mine.userData = new DropTag(DragZone.TradeMine);
            var gm = Kz.El("win-grid win-grid--4", mine, pick: true);
            for (int i = 0; i < 12; i++)
            {
                int idx = i;
                Item? It() => idx < S.MyTradeItems.Count ? S.MyTradeItems[idx].item : null;
                var s = ItemSlot(gm, It, false, "win-cell");
                s.RegisterCallback<ClickEvent>(_ => { if (idx < S.MyTradeItems.Count) G.Send($"TRADE REMOVE {S.MyTradeItems[idx].slot}"); });
                _mine[i] = s;
            }
            _myGold = Kz.Text("", "kz-text", mine);
            _myState = Kz.Text("", "kz-text-2", mine);
            var theirs = Col(out _theirTitle, "");
            var gt = Kz.El("win-grid win-grid--4", theirs, pick: true);
            for (int i = 0; i < 12; i++) { int idx = i; _theirs[i] = ItemSlot(gt, () => idx < S.TheirTradeItems.Count ? S.TheirTradeItems[idx] : null, true, "win-cell"); }
            _theirGold = Kz.Text("", "kz-text", theirs);
            _theirState = Kz.Text("", "kz-text-2", theirs);

            var r = Row(Body);
            _gold = Field(r, 12, 130, "ouro");
            Kz.Btn("Ofertar ouro", () => { if (long.TryParse(_gold.value, out var v) && v >= 0) G.Send($"TRADE GOLD {v}"); }, "kz-btn", r);
            var b = Row(Body);
            _lock = Kz.Btn("1. Travar oferta", () => G.Send("TRADE LOCK"), "kz-btn", b);
            _confirm = Kz.Btn("2. Confirmar troca", () => G.Send("TRADE CONFIRM"), "kz-btn kz-btn--primary", b);
            Kz.Btn("Cancelar", () => G.Send("TRADE CANCEL"), "kz-btn", b);
            Kz.Text("Arraste da mochila (ou duplo clique) para oferecer. Qualquer mudança destrava os dois lados.", "kz-text-3", Body);
        }

        public override bool Wanted => S.TradeOpen;
        public override void Close() => G.Send("TRADE CANCEL");
        public override void Opened() => G._showInv = true;

        private static string St(bool locked, bool confirmed) => confirmed ? "<color=#6dff9e>✔ confirmou</color>" : locked ? "<color=#ffd36a>travou</color>" : "<color=#7d7366>montando a oferta</color>";

        public override void Refresh()
        {
            Title.Set($"Troca com {S.TradePartner}");
            _theirTitle.Set($"{S.TradePartner} oferece");
            for (int i = 0; i < 12; i++)
            {
                ItemIcons.Fill(_mine[i], i < S.MyTradeItems.Count ? S.MyTradeItems[i].item : null, G);
                ItemIcons.Fill(_theirs[i], i < S.TheirTradeItems.Count ? S.TheirTradeItems[i] : null, G);
            }
            _myGold.Set($"<color=#e8c27a>Ouro: {S.MyTradeGold:N0}</color>");
            _theirGold.Set($"<color=#e8c27a>Ouro: {S.TheirTradeGold:N0}</color>");
            _myState.Set("Você: " + St(S.MyTradeLocked, S.MyTradeConfirmed));
            _theirState.Set($"{S.TradePartner}: " + St(S.TheirTradeLocked, S.TheirTradeConfirmed));
            _lock.SetEnabled(!S.MyTradeLocked);
            _confirm.SetEnabled(S.MyTradeLocked && S.TheirTradeLocked && !S.MyTradeConfirmed);
        }
    }

    // ====================================================================== loja do NPC
    public sealed class NpcShopWindow : GameWindow
    {
        private readonly Button _sell;

        public NpcShopWindow(UiRoot ui) : base(ui, "npcshop", "Mercadora Tália — Loja", 400, new Vector2(790, 110))
        {
            Kz.Text("Compre com ouro. Para vender, selecione um item na mochila.", "kz-text-2", Body);
            var scroll = new ScrollView(ScrollViewMode.Vertical); scroll.AddToClassList("win-scroll"); scroll.style.height = 430; Body.Add(scroll);
            foreach (var d in ItemDatabase.All.Where(d => d.ShopPrice > 0))
            {
                var r = Row(scroll.contentContainer, "win-list-row");
                var it = new Item(d);
                var s = ItemSlot(r, () => it, true, "win-cell win-cell--small");
                ItemIcons.Fill(s, it, ui.Game);
                Kz.Text($"{d.Name}\n<color=#e8c27a>{d.ShopPrice:N0} ouro</color>", "kz-text win-grow", r);
                var def = d;
                Kz.Btn("×1", () => G.Send($"BUY {def.Id} 1"), "kz-btn kz-btn--small", r);
                if (d.IsPotion) Kz.Btn("×10", () => G.Send($"BUY {def.Id} 10"), "kz-btn kz-btn--small", r);
            }
            Kz.El("kz-divider", Body);
            _sell = Kz.Btn("", () => { if (G._selSlot >= 0 && S.Inventory[G._selSlot] != null) { G.Send($"SELL {G._selSlot}"); G._selSlot = -1; } }, "kz-btn", Body);
        }

        public override bool Wanted => G._showShop;
        public override void Close() => G._showShop = false;

        public override void Refresh()
        {
            var it = G._selSlot >= 0 ? S.Inventory[G._selSlot] : null;
            _sell.text = it != null ? $"Vender {it.DisplayName} por {GameServer.SellPrice(it):N0}" : "Selecione um item da mochila para vender";
            _sell.SetEnabled(it != null);
        }
    }

    // ====================================================================== forja
    public sealed class ForgeWindow : GameWindow
    {
        private readonly VisualElement _sel;
        private readonly Label _preview;
        private readonly Button _forge;
        private string _sig = "";

        public ForgeWindow(UiRoot ui) : base(ui, "forge", "Ferreiro Bram — Forja Arcana", 380, new Vector2(810, 200))
        {
            Kz.Text("<b>Refino Superior</b> (+9 → +15): item + N Runas da Aurora + N Runas do Eclipse + 1 Cristal do Vórtice (N = nível alvo − 9). Falha destrói o item.", "kz-text-2", Body);
            Kz.Text("<b>Arma do Vórtice</b>: equipamentos +4~+8 + 1 Cristal do Vórtice (Aurora e Eclipse aumentam a chance).", "kz-text-2", Body);
            Kz.El("kz-divider", Body);
            Kz.Text("Clique nos itens da mochila para incluir ou tirar:", "kz-text", Body);
            _sel = Kz.El("win-grid", Body, pick: true);
            _preview = Kz.Text("", "kz-text win-preview", Body);
            _forge = Kz.Btn("Forjar", () => G.Send("FORGE " + string.Join(",", G._forgeSel), r => { G._forgePreview = r; G._forgeSel.Clear(); }), "kz-btn kz-btn--primary", Body);
            var r2 = Row(Body);
            Kz.Btn("Reparar equipamento", () => G.Send("REPAIR"), "kz-btn", r2);
        }

        public override bool Wanted => G._showForge;
        public override void Close() { G._showForge = false; G._forgeSel.Clear(); }
        public override void Opened() { G._showInv = true; G._forgeSel.Clear(); G._forgePreview = "Selecione os itens na mochila."; }

        public override void Refresh()
        {
            string sig = string.Join(",", G._forgeSel.Select(i => i + ":" + (S.Inventory[i]?.Def.Id ?? 0)));
            if (sig != _sig)
            {
                _sig = sig;
                _sel.Clear();
                foreach (var i in G._forgeSel.ToList())
                {
                    var it = S.Inventory[i];
                    if (it == null) { G._forgeSel.Remove(i); continue; }
                    var s = ItemSlot(_sel, () => it, false, "win-cell");
                    ItemIcons.Fill(s, it, G);
                }
            }
            _preview.Set(G._forgePreview);
            _forge.SetEnabled(G._forgeSel.Count > 0);
        }
    }

    // ====================================================================== grupo
    public sealed class PartyWindow : GameWindow
    {
        private readonly VisualElement _list;
        private readonly TextField _name;
        private readonly Button _leave;
        private string _sig = "";

        public PartyWindow(UiRoot ui) : base(ui, "party", "Grupo", 320, new Vector2(20, 130))
        {
            Kz.Text("Grupos dividem a experiência (+15% por membro) e o loot reservado.", "kz-text-2", Body);
            _list = Kz.El("", Body);
            var r = Row(Body);
            _name = Field(r, 12, 160, "nome do herói");
            Kz.Btn("Convidar", () => { if (_name.value.Trim() != "") G.Send($"PARTY INVITE {_name.value.Trim()}"); }, "kz-btn", r);
            _leave = Kz.Btn("Sair do grupo", () => G.Send("PARTY LEAVE"), "kz-btn", Body);
        }

        public override bool Wanted => G._showParty;
        public override void Close() => G._showParty = false;

        public override void Refresh()
        {
            string sig = string.Join(",", S.PartyMembers.Select(p => p.name)) + S.PartyLeaderId;
            if (sig != _sig)
            {
                _sig = sig;
                _list.Clear();
                if (S.PartyMembers.Count == 0) Kz.Text("<color=#7d7366>Você não está em um grupo.</color>", "kz-text", _list);
                foreach (var (id, name) in S.PartyMembers)
                    Kz.Text((id == S.PartyLeaderId ? "<color=#ffb347>★</color> " : "• ") + name + (id == S.MyId ? " <color=#7d7366>(você)</color>" : ""), "kz-text", _list);
            }
            _leave.SetEnabled(S.PartyMembers.Count > 0);
            if (G.HoverId != 0 && S.Entities.TryGetValue(G.HoverId, out var he) && he.Kind == EntityKind.Player && he.Id != S.MyId && _name.focusController?.focusedElement != _name) _name.value = he.Name;
        }
    }

    // ====================================================================== guilda
    public sealed class GuildWindow : GameWindow
    {
        private readonly VisualElement _content;
        private string _sig = "";

        public GuildWindow(UiRoot ui) : base(ui, "guild", "Guilda", 400, new Vector2(20, 130))
        {
            _content = Kz.El("", Body, pick: true);
        }

        public override bool Wanted => G._showGuild;
        public override void Close() => G._showGuild = false;

        public override void Refresh()
        {
            string sig = S.GuildName + S.GuildRank + string.Join(",", S.GuildMembers.Select(m => m.name + m.rank + m.online));
            if (sig == _sig) return;
            _sig = sig;
            _content.Clear();
            Title.Set(S.GuildName == "" ? "Guilda" : $"Guilda {S.GuildName}");
            if (S.GuildName == "")
            {
                Kz.Text("Você não está numa guilda.", "kz-text", _content);
                Kz.Text($"<b>Fundar uma guilda</b>: fale com o Hadrik, o Arauto das Guildas, na praça. Requer nível {Guild.CreateMinLevel} e {Guild.CreateCost:N0} de ouro.", "kz-text-2", _content);
                var r = Row(_content);
                var f = Field(r, 14, 200, "nome da guilda");
                Kz.Btn("Fundar", () => G.Send($"GUILD CREATE {f.value.Trim()}"), "kz-btn kz-btn--primary", r);
                return;
            }
            int my = S.GuildRank;
            Kz.Text($"Seu cargo: <color=#ffb347>{KezorGame.RankNames[Mathf.Clamp(my, 0, 2)]}</color>   ·   {S.GuildMembers.Count} membros   ·   chat: <color=#8fd0ff>/g mensagem</color>", "kz-text-2", _content);
            var scroll = new ScrollView(); scroll.AddToClassList("win-scroll"); scroll.style.height = 240; _content.Add(scroll);
            foreach (var (name, rank, online) in S.GuildMembers)
            {
                var r = Row(scroll.contentContainer, "win-list-row");
                Kz.Text($"{(online ? "<color=#6dff9e>●</color>" : "<color=#55504a>●</color>")} <b>{name}</b>  <color=#c9b27a>{KezorGame.RankNames[Mathf.Clamp(rank, 0, 2)]}</color>", "kz-text win-grow", r);
                bool self = name == S.Me.Name; string n = name;
                if (!self && my == 2 && rank == 0) Kz.Btn("Promover", () => G.Send($"GUILD PROMOTE {n}"), "kz-btn kz-btn--small", r);
                if (!self && my == 2 && rank == 1) Kz.Btn("Rebaixar", () => G.Send($"GUILD DEMOTE {n}"), "kz-btn kz-btn--small", r);
                if (!self && my > rank && my >= 1) Kz.Btn("Expulsar", () => G.Send($"GUILD KICK {n}"), "kz-btn kz-btn--small", r);
            }
            if (my >= 1)
            {
                var r = Row(_content);
                var f = Field(r, 12, 180, "convidar herói");
                Kz.Btn("Convidar", () => { G.Send($"GUILD INVITE {f.value.Trim()}"); f.value = ""; }, "kz-btn", r);
                var w = Row(_content);
                var fw = Field(w, 14, 180, "guilda rival");
                if (my == 2) Kz.Btn("Declarar guerra", () => G.Send($"GUILD WAR {fw.value.Trim()}"), "kz-btn", w);
            }
            var b = Row(_content);
            Kz.Btn("Sair da guilda", () => G.Send("GUILD LEAVE"), "kz-btn", b);
            if (my == 2) Kz.Btn("Dissolver", () => G.Send("GUILD DISBAND"), "kz-btn", b);
        }
    }

    // ====================================================================== ranking
    public sealed class RankingWindow : GameWindow
    {
        private readonly Dictionary<string, Button> _tabs = new Dictionary<string, Button>();
        private readonly VisualElement _table;
        private readonly Label _mine;
        private int _seen = -1;

        public RankingWindow(UiRoot ui) : base(ui, "ranking", "Ranking de Brasalume", 640, new Vector2(420, 120))
        {
            var tabs = new List<(string, string)> { ("GERAL", "Geral"), ("GUILDAS", "Guildas") };
            foreach (var d in ClassDefinition.All) tabs.Add((d.Code, d.DisplayName.Split(' ')[0]));
            for (int row = 0; row * 5 < tabs.Count; row++)
            {
                var r = Row(Body, "win-tabs");
                foreach (var (code, label) in tabs.Skip(row * 5).Take(5))
                {
                    string c = code;
                    _tabs[code] = Kz.Btn(label, () => G.OpenRanking(c), "kz-tab", r);
                }
            }
            _table = Kz.El("win-table", Body, pick: true);
            _mine = Kz.Text("", "kz-text kz-amber", Body);
        }

        public override bool Wanted => G._showRanking;
        public override void Close() => G._showRanking = false;

        public override void Refresh()
        {
            foreach (var kv in _tabs) kv.Value.SetClass("kz-tab--on", kv.Key == G._rankTab);
            _mine.Set(G._rankMine);
            if (S.RankVersion == _seen) return;
            _seen = S.RankVersion;
            _table.Clear();
            bool guilds = S.RankKind == "GUILDAS";
            void Line(string[] cells, bool head, bool me = false)
            {
                var r = Kz.El("win-trow" + (head ? " win-trow--head" : "") + (me ? " win-trow--me" : ""), _table);
                float[] w = guilds ? new float[] { 40, 200, 100, 90, 120 } : new float[] { 40, 230, 170, 70, 70 };
                for (int i = 0; i < cells.Length && i < w.Length; i++) { var l = Kz.Text(cells[i], "kz-text win-tcell", r); l.style.width = w[i]; }
            }
            if (guilds) Line(new[] { "#", "Guilda", "Pontos", "Membros", "V / D / E" }, true);
            else Line(new[] { "#", "Herói", "Classe", "Nível", "Renasc." }, true);
            foreach (var row in S.RankRows)
            {
                if (guilds && row.Length >= 7) Line(new[] { row[0], $"<color=#8fd0ff>{row[1]}</color>", row[2], row[3], $"{row[4]} / {row[5]} / {row[6]}" }, false, row[1] == S.GuildName);
                else if (row.Length >= 6)
                {
                    int.TryParse(row[2], out var cls);
                    string guild = row[5] != "" ? $"  <size=11><color=#8fd0ff>‹{row[5]}›</color></size>" : "";
                    Line(new[] { row[0], row[1] + guild, $"<color=#c9b27a>{ClassDefinition.Get((CharacterClass)cls).DisplayName}</color>", row[3], row[4] }, false, row[1] == S.Me.Name);
                }
            }
            if (S.RankRows.Count == 0) Kz.Text("<color=#7d7366>Ninguém neste ranking ainda.</color>", "kz-text", _table);
        }
    }

    // ====================================================================== lojas pessoais
    public sealed class MyShopWindow : GameWindow
    {
        private readonly TextField _title, _price;
        private readonly VisualElement _list;
        private readonly Button _open, _close;
        private string _sig = "";

        public MyShopWindow(UiRoot ui) : base(ui, "myshop", "Minha loja", 380, new Vector2(810, 200))
        {
            Kz.Text("Só dentro da vila; com a loja aberta você fica parado. Arraste itens da mochila para cá (ou duplo clique) para vender pelo preço abaixo.", "kz-text-2", Body);
            var r1 = Row(Body); Kz.Text("Nome", "kz-text win-label", r1); _title = Field(r1, 24, 250, "nome da barraca");
            var r2 = Row(Body); Kz.Text("Preço", "kz-text win-label", r2); _price = Field(r2, 12, 140); Kz.Text("ouro", "kz-text-2", r2);
            _title.RegisterValueChangedCallback(e => G._shopTitleText = e.newValue);
            _price.RegisterValueChangedCallback(e => G._shopPriceText = e.newValue);
            _list = Kz.El("win-droparea", Body, pick: true);
            _list.userData = new DropTag(DragZone.MyShop);
            userData = new DropTag(DragZone.MyShop);
            var b = Row(Body);
            _open = Kz.Btn("Abrir loja", () => { G.Send($"SHOP OPEN {_title.value.Trim()}"); G.Send("SHOP MINE"); }, "kz-btn kz-btn--primary", b);
            _close = Kz.Btn("Fechar loja", () => { G.Send("SHOP CLOSE"); G.Send("SHOP MINE"); }, "kz-btn", b);
        }

        public override bool Wanted => G._showMyShop;
        public override void Close() => G._showMyShop = false;
        public override void Opened() { _title.value = G._shopTitleText; _price.value = G._shopPriceText; }

        public override void Refresh()
        {
            bool mine = S.ShopOwner == S.Me.Name;
            bool open = mine && S.ShopIsOpen;
            _open.Show(!open); _close.Show(open);
            string sig = mine ? string.Join(",", S.ShopListings.Select(l => l.slot + ":" + l.price)) : "";
            if (sig == _sig) return;
            _sig = sig;
            _list.Clear();
            if (!mine || S.ShopListings.Count == 0) Kz.Text("<color=#7d7366>Nenhum item à venda.</color>", "kz-text", _list);
            if (mine)
                foreach (var (slot, price, item) in S.ShopListings)
                {
                    var r = Row(_list, "win-list-row");
                    var it = item; int sl = slot;
                    var s = ItemSlot(r, () => it, false, "win-cell win-cell--small"); ItemIcons.Fill(s, it, G);
                    Kz.Text($"<color={G.ItemColor(it)}>{it.DisplayName}</color>\n<color=#e8c27a>{price:N0} ouro</color>", "kz-text win-grow", r);
                    Kz.Btn("Tirar", () => { G.Send($"SHOP REMOVE {sl}"); G.Send("SHOP MINE"); }, "kz-btn kz-btn--small", r);
                }
        }
    }

    public sealed class ShopViewWindow : GameWindow
    {
        private readonly Label _title, _gold;
        private readonly VisualElement _list;
        private string _sig = "";

        public ShopViewWindow(UiRoot ui) : base(ui, "shopview", "Loja", 400, new Vector2(500, 300))
        {
            _title = Kz.Text("", "kz-h2", Body);
            _list = Kz.El("", Body, pick: true);
            _gold = Kz.Text("", "kz-text-2", Body);
        }

        public override bool Wanted => S.ShopVisible && S.ShopOwner != S.Me.Name;
        public override void Close() => S.ShopVisible = false;

        public override void Refresh()
        {
            Title.Set($"Loja de {S.ShopOwner}");
            _title.Set(S.ShopIsOpen ? S.ShopTitle : $"{S.ShopTitle}  <color=#ff6a5a>(fechada)</color>");
            _gold.Set($"Seu ouro: {S.Me.Gold:N0}");
            string sig = S.ShopOwner + S.ShopIsOpen + S.Me.Gold + string.Join(",", S.ShopListings.Select(l => l.slot + ":" + l.price));
            if (sig == _sig) return;
            _sig = sig;
            _list.Clear();
            foreach (var (slot, price, item) in S.ShopListings)
            {
                var r = Row(_list, "win-list-row");
                var it = item; int sl = slot; long pr = price;
                var s = ItemSlot(r, () => it, true, "win-cell win-cell--small"); ItemIcons.Fill(s, it, G);
                Kz.Text($"<color={G.ItemColor(it)}>{it.DisplayName}</color>\n<color=#e8c27a>{price:N0} ouro</color>", "kz-text win-grow", r);
                var b = Kz.Btn("Comprar", () => G.Send($"SHOP BUY {S.ShopOwner} {sl} {pr}"), "kz-btn kz-btn--small", r);
                b.SetEnabled(S.ShopIsOpen && S.Me.Gold >= price);
            }
            if (S.ShopListings.Count == 0) Kz.Text("<color=#7d7366>Nada à venda.</color>", "kz-text", _list);
        }
    }

    // ====================================================================== mapa e viagem
    public sealed class MapWindow : GameWindow
    {
        private readonly VisualElement _map, _markers;
        private readonly VisualElement _warps;
        private readonly Label _here;
        private int _for = -1;
        private const float Px = 320;

        public MapWindow(UiRoot ui) : base(ui, "map", "Mapa e viagem", 640, new Vector2(480, 120))
        {
            var row = Row(Body, "win-row--top");
            var left = Kz.El("win-mapbox", row, pick: true);
            _map = Kz.El("win-map", left);
            _markers = Kz.El("win-map", left);
            var right = Kz.El("win-col", row, pick: true);
            _here = Kz.Text("", "kz-h2", right);
            Kz.Text("Viagem rápida (custa ouro; não funciona logo depois de lutar):", "kz-text-2", right);
            _warps = Kz.El("", right, pick: true);
            foreach (var m in MapDefinition.AllMaps.Where(m => m.WarpCode != ""))
            {
                var r = Row(_warps, "win-list-row");
                Kz.Text($"<b>{m.Name}</b>\n<color=#7d7366>nível {m.MinLevel}{(m.WarpCost > 0 ? $" · {m.WarpCost:N0} ouro" : " · grátis")}</color>", "kz-text win-grow", r);
                string code = m.WarpCode;
                Kz.Btn("Viajar", () => G.Send($"WARP {code}"), "kz-btn kz-btn--small", r);
            }
            Kz.El("kz-divider", right);
            Kz.Text("<color=#ffd36a>●</color> NPC   <color=#ff6a1a>◆</color> portal   <color=#ffffff>●</color> você   <color=#e33a2a>●</color> monstros perto", "kz-text-3", right);
        }

        public override bool Wanted => G._showWorldMap;
        public override void Close() => G._showWorldMap = false;

        public override void Refresh()
        {
            var map = G.Map;
            if (_for != map.Id) { _for = map.Id; _map.style.backgroundImage = new StyleBackground(MinimapTexture.Build(map)); }
            _here.Set(map.Name);
            _markers.Clear();
            float k = Px / map.Size;
            void M(float x, float z, Color c, float s, bool diamond = false)
            {
                var e = Kz.El("win-marker", _markers);
                e.style.left = x * k - s / 2; e.style.top = (map.Size - z) * k - s / 2; e.style.width = s; e.style.height = s;
                e.style.backgroundColor = c;
                if (diamond) e.style.rotate = new Rotate(45); else { e.style.borderTopLeftRadius = e.style.borderTopRightRadius = e.style.borderBottomLeftRadius = e.style.borderBottomRightRadius = s / 2; }
            }
            foreach (var p in map.Portals) M(p.X, p.Z, WorldBuilder.PortalColor(p.TargetMap), 10, true);
            foreach (var n in map.Npcs) M(n.X, n.Z, new Color(1f, 0.83f, 0.35f), 6);
            foreach (var e in S.Entities.Values) if (e.Kind == EntityKind.Monster && e.OwnerId == 0) M(e.X, e.Z, new Color(0.9f, 0.23f, 0.17f), 4);
            M(S.Me.X, S.Me.Z, Color.white, 9);
        }
    }

    // ====================================================================== opções
    public sealed class OptionsWindow : GameWindow
    {
        public const string VolumePref = "kz.volume", QualityPref = "kz.quality", FpsPref = "kz.fps", VsyncPref = "kz.vsync";
        private readonly Label _scaleLabel, _volLabel, _bloomLabel;
        private readonly Toggle _bloom;
        private readonly List<Button> _quality = new List<Button>(), _fps = new List<Button>();
        private readonly Toggle _minimap, _vsync, _full;
        private readonly VisualElement _conn;
        private readonly Label _connLabel;

        public OptionsWindow(UiRoot ui) : base(ui, "options", "Opções", 440, new Vector2(580, 120))
        {
            _conn = Kz.El("", Body, pick: true);
            Kz.Text("Conexão", "kz-h2", _conn);
            _connLabel = Kz.Text("", "kz-text", _conn);
            var cr = Row(_conn);
            Kz.Btn("Trocar personagem", () => { G.BackToCharacterSelect(); Close(); }, "kz-btn kz-btn--small", cr);
            Kz.Btn("Trocar de servidor", () => { G.BackToServerList(); Close(); }, "kz-btn kz-btn--small", cr);
            Kz.El("kz-divider", _conn);

            Kz.Text("Interface", "kz-h2", Body);
            _scaleLabel = Kz.Text("", "kz-text", Body);
            var scale = new Slider(0.8f, 1.4f) { value = ui.UserScale }; scale.AddToClassList("kz-slider"); Body.Add(scale);
            scale.RegisterValueChangedCallback(e => ui.UserScale = Mathf.Round(e.newValue * 20f) / 20f);
            _minimap = new Toggle("Mostrar minimapa") { value = true }; _minimap.AddToClassList("kz-toggle"); Body.Add(_minimap);
            _minimap.RegisterValueChangedCallback(e => G.MinimapOn = e.newValue);
            Kz.El("kz-divider", Body);

            Kz.Text("Áudio", "kz-h2", Body);
            _volLabel = Kz.Text("", "kz-text", Body);
            var vol = new Slider(0f, 1f) { value = PlayerPrefs.GetFloat(VolumePref, 1f) }; vol.AddToClassList("kz-slider"); Body.Add(vol);
            vol.RegisterValueChangedCallback(e => { AudioListener.volume = e.newValue; PlayerPrefs.SetFloat(VolumePref, e.newValue); });
            Kz.El("kz-divider", Body);

            Kz.Text("Gráficos", "kz-h2", Body);
            var q = Row(Body);
            var names = QualitySettings.names;
            for (int i = 0; i < names.Length; i++) { int lvl = i; _quality.Add(Kz.Btn(QualityName(names[i]), () => { QualitySettings.SetQualityLevel(lvl, true); PlayerPrefs.SetInt(QualityPref, lvl); }, "kz-btn kz-btn--small", q)); }
            var f = Row(Body);
            var fl = Kz.Text("Limite de FPS", "kz-text", f); fl.style.width = 110;
            foreach (int fps in new[] { 30, 60, 120, 144 }) { int v = fps; _fps.Add(Kz.Btn(fps.ToString(), () => { Application.targetFrameRate = v; PlayerPrefs.SetInt(FpsPref, v); }, "kz-btn kz-btn--small", f)); }
            _vsync = new Toggle("Sincronização vertical (VSync)") { value = QualitySettings.vSyncCount > 0 }; _vsync.AddToClassList("kz-toggle"); Body.Add(_vsync);
            _vsync.RegisterValueChangedCallback(e => { QualitySettings.vSyncCount = e.newValue ? 1 : 0; PlayerPrefs.SetInt(VsyncPref, e.newValue ? 1 : 0); });
            _full = new Toggle("Tela cheia") { value = Screen.fullScreen }; _full.AddToClassList("kz-toggle"); Body.Add(_full);
            _full.RegisterValueChangedCallback(e => Screen.fullScreen = e.newValue);
            _bloom = new Toggle("Brilho (bloom) em refino alto, épicas e fogo") { value = KezorBloom.On }; _bloom.AddToClassList("kz-toggle"); Body.Add(_bloom);
            _bloom.RegisterValueChangedCallback(e => { KezorBloom.On = e.newValue; KezorBloom.Save(); });
            _bloomLabel = Kz.Text("", "kz-text", Body);
            var bl = new Slider(0f, 2f) { value = KezorBloom.Intensity }; bl.AddToClassList("kz-slider"); Body.Add(bl);
            bl.RegisterValueChangedCallback(e => { KezorBloom.Intensity = Mathf.Round(e.newValue * 20f) / 20f; KezorBloom.Save(); });
            Kz.El("kz-divider", Body);

            Kz.Text("Teclas", "kz-h2", Body);
            Kz.Text("Andar/atacar: botão esquerdo · Skill: botão direito · 1–6 escolhem a skill · Q W E R atalhos de poção (arraste da mochila) · Espaço pega item · X montaria · A Árvore da Ascensão · T troca (mouse sobre o jogador)\n" +
                    "C personagem · I inventário · P grupo · G guilda · K ranking · B loja pessoal · M mapa · O opções · H ajuda · Enter chat · Esc fecha janelas", "kz-text-2", Body);
        }

        private static string QualityName(string n) => n switch
        {
            "Very Low" => "Muito baixa", "Low" => "Baixa", "Medium" => "Média", "High" => "Alta", "Very High" => "Muito alta", "Ultra" => "Ultra", _ => n,
        };

        public override bool Wanted => G._showOptions;
        public override void Close() => G._showOptions = false;

        public override void Refresh()
        {
            _conn.Show(G.InWorld);
            if (G.InWorld)
                _connLabel.Set($"<b>{(G._serverLabel != "" ? G._serverLabel : "Servidor")}</b>  ·  conta {G._account}  ·  " +
                               (G.PingMs >= 0 ? $"{G.PingMs} ms" : "medindo...") + (G.Hosting ? $"  ·  você hospeda ({G.HostPlayers} online)" : ""));
            _scaleLabel.Set($"Escala da interface: <b>{Ui.UserScale * 100f:0}%</b>");
            _volLabel.Set($"Volume geral: <b>{AudioListener.volume * 100f:0}%</b>");
            _bloomLabel.Set(KezorBloom.On ? $"Intensidade do brilho: <b>{KezorBloom.Intensity * 100f:0}%</b>" : "Intensidade do brilho: <b>desligado</b>");
            _bloom.SetValueWithoutNotify(KezorBloom.On);
            int ql = QualitySettings.GetQualityLevel();
            for (int i = 0; i < _quality.Count; i++) _quality[i].SetClass("kz-btn--on", i == ql);
            int[] fpsVals = { 30, 60, 120, 144 };
            for (int i = 0; i < _fps.Count; i++) _fps[i].SetClass("kz-btn--on", Application.targetFrameRate == fpsVals[i]);
            _minimap.SetValueWithoutNotify(G.MinimapOn);
        }

        /// <summary>Aplica as preferências salvas ao iniciar o jogo.</summary>
        public static void ApplySaved()
        {
            AudioListener.volume = PlayerPrefs.GetFloat(VolumePref, 1f);
            if (PlayerPrefs.HasKey(QualityPref)) QualitySettings.SetQualityLevel(PlayerPrefs.GetInt(QualityPref), true);
            if (PlayerPrefs.HasKey(FpsPref)) Application.targetFrameRate = PlayerPrefs.GetInt(FpsPref);
            if (PlayerPrefs.HasKey(VsyncPref)) QualitySettings.vSyncCount = PlayerPrefs.GetInt(VsyncPref);
        }
    }

    // ====================================================================== ajuda
    public sealed class HelpWindow : GameWindow
    {
        public HelpWindow(UiRoot ui) : base(ui, "help", "Como jogar", 580, new Vector2(510, 120))
        {
            void Section(string title, string text) { Kz.Text(title, "kz-h2", Body); Kz.Text(text, "kz-text-2 win-help", Body); }
            Section("Movimento e combate",
                "• Clique (ou segure) no chão para andar. Clique num monstro para atacar sem parar.\n" +
                "• Teclas 1–6 escolhem a skill; botão direito usa no alvo (buffs, estandartes e ecos não precisam de alvo).\n" +
                "• Q / W: poção de vida / mana (ou clique nas orbes). Espaço pega o item mais próximo.\n" +
                "• R monta e desmonta. Asas, montaria e mascote têm slot próprio no inventário.\n" +
                "• Roda do mouse: zoom. Botão do meio arrastando: girar a câmera.");
            Section("Progressão",
                "• Cada nível dá 5 pontos de atributo (C). No nível 400, renasça para recomeçar mais forte.\n" +
                "• Runa da Aurora: +0→+6. Runa do Eclipse: +6→+9 (pode falhar). Runa Vital: opção adicional.\n" +
                "• +10 a +15 e a Arma do Vórtice: Forja Arcana do Ferreiro Bram, na vila.\n" +
                "• Itens Ascendentes têm opções especiais; [Sorte] aumenta o crítico e o refino.");
            Section("Mundo",
                "• A vila de Brasalume é área segura. Fora dela há PvP (nível 6+) e monstros mais fortes longe da vila.\n" +
                "• A Invasão das Cinzas ataca o portão sul de tempos em tempos: derrote o Arauto para ganhar runas.\n" +
                "• Grupo (P) divide experiência. Chat: Enter. /p texto = grupo, /g texto = guilda, /w Nome texto = sussurro, /online = quem está jogando.\n" +
                "• Opções (O) ▸ Conexão: trocar de personagem ou de servidor. No servidor deste PC, a sua conta tem comandos GM (/gm level 50).");
            Kz.Btn("Entendi", Close, "kz-btn kz-btn--primary", Body);
        }

        public override bool Wanted => G._showHelp;
        public override void Close() => G._showHelp = false;
    }
}
