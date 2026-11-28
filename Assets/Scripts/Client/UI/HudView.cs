using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using Kezor.Core.Characters;
using Kezor.Core.Combat;
using Kezor.Core.Items;
using Kezor.Core.Net;
using Kezor.Core.World;

namespace Kezor.Client.UI
{
    /// <summary>
    /// HUD em UI Toolkit: quadro do herói, buffs, grupo, quadro do alvo, minimapa redondo, orbes animadas, barra de skills
    /// com recarga radial, experiência segmentada, menu, chat com abas e toasts. Só lê o <see cref="ClientState"/> e chama
    /// ações do <see cref="KezorGame"/>.
    /// </summary>
    public sealed class HudView
    {
        private readonly UiRoot _ui;
        private KezorGame G => _ui.Game;
        private ClientState S => _ui.Game.State;

        // quadro do herói
        private readonly VisualElement _portraitGlyph;
        private readonly Label _lvl, _name, _class, _zone, _gold, _points;
        // buffs, grupo, alvo
        private readonly VisualElement _buffs, _party, _target;
        private readonly Label _targetName, _targetSub;
        private readonly KzBar _targetBar;
        private readonly List<(VisualElement root, VisualElement icon, Label time)> _buffPool = new List<(VisualElement, VisualElement, Label)>();
        private readonly List<(VisualElement root, Label name, KzBar bar)> _partyPool = new List<(VisualElement, Label, KzBar)>();
        // minimapa
        private readonly VisualElement _minimap, _mapImg, _mapClip;
        private readonly Label _mapName, _coords;
        private readonly List<VisualElement> _dots = new List<VisualElement>();
        private int _mapTexFor = -1;
        private const float MapView = 55f, MapPx = 172f;
        // barra de baixo
        public const int SkillSlots = 5;
        private readonly KzSlot[] _skills = new KzSlot[SkillSlots];
        private readonly KzSlot _active;
        private readonly KzSlot[] _quick = new KzSlot[4];
        private readonly KzBar[] _expSeg = new KzBar[20];
        private readonly Label _expText;
        private readonly HudCrystal _life, _mana;
        private readonly HudVBar _vigor, _shield;
        private readonly Label _hpText, _mpText, _vigorText, _shieldText;
        private readonly Dictionary<KezorGame.UiPanel, VisualElement> _menuBtns = new Dictionary<KezorGame.UiPanel, VisualElement>();
        // chat
        private readonly VisualElement _chatLog;
        private readonly TextField _chatInput;
        private readonly List<Label> _chatLines = new List<Label>();
        private readonly Dictionary<string, Button> _chatTabs = new Dictionary<string, Button>();
        private string _chatTab = "all";
        private int _chatSeen = -1; private string _chatTabSeen = "";
        // toasts e tooltip
        private readonly VisualElement _toasts, _tip;
        private readonly Label _tipText;
        private VisualElement? _tipOwner;
        private readonly VisualElement _eventRibbon;
        private readonly Label _eventText;
        private int _lastLevel = -1, _lastMap = -1; private bool _lastEvent; private string _lastInvite = "", _lastWarInvite = "";

        public static readonly Dictionary<CharacterClass, Color> ClassTint = new Dictionary<CharacterClass, Color>
        {
            { CharacterClass.Warden, KezorLook.Hex("#7fa6d8") }, { CharacterClass.Arcanist, KezorLook.Hex("#b48bff") },
            { CharacterClass.Ranger, KezorLook.Hex("#8fcf7a") }, { CharacterClass.Bladeweaver, KezorLook.Hex("#7fd8e8") },
            { CharacterClass.Commander, KezorLook.Hex("#ff8a6a") }, { CharacterClass.Invoker, KezorLook.Hex("#c9b0ff") },
            { CharacterClass.EmberFist, KezorLook.Hex("#ffa04a") },
        };

        public HudView(UiRoot ui)
        {
            _ui = ui;
            var hud = ui.HudLayer;

            // ---------------- herói
            var player = Kz.El("kz-panel hud-player", hud, pick: true);
            var portrait = Kz.El("hud-portrait", player);
            _portraitGlyph = Kz.Icon(Kz.Art("g_char"), "hud-portrait__glyph", portrait);
            _lvl = Kz.Text("1", "hud-portrait__lvl", portrait);
            var info = Kz.El("", player);
            _name = Kz.Text("", "hud-player__name", info);
            _class = Kz.Text("", "hud-player__class", info);
            _zone = Kz.Text("", "hud-player__zone", info);
            var row = Kz.El("hud-player__row", info);
            Kz.Icon(Kz.Art("g_coin"), "hud-coin", row);
            _gold = Kz.Text("", "kz-text-2 kz-gold", row);
            _points = Kz.Text("", "hud-points", row);
            Tip(player, () => S.Me.FreePoints > 0 ? $"<b>{S.Me.FreePoints} pontos livres</b>\nAbra a janela do personagem (C) para distribuir." : "Seu herói. Janela do personagem: C");

            _buffs = Kz.El("hud-buffs", hud);
            _party = Kz.El("hud-party", hud);

            // ---------------- alvo
            _target = Kz.El("kz-panel hud-target", hud, pick: true);
            _targetName = Kz.Text("", "hud-target__name", _target);
            _targetSub = Kz.Text("", "hud-target__sub", _target);
            _targetBar = _target.Put(new KzBar("kz-bar--hp", true));

            _eventRibbon = Kz.El("hud-toast hud-toast--event", hud);
            _eventRibbon.style.position = Position.Absolute; _eventRibbon.style.top = 96; _eventRibbon.style.left = Length.Percent(50); _eventRibbon.style.translate = new Translate(Length.Percent(-50), 0);
            _eventText = Kz.Text("", "hud-toast__text", _eventRibbon);

            // ---------------- minimapa
            _minimap = Kz.El("hud-minimap", hud, pick: true);
            _mapClip = Kz.El("hud-minimap__clip", _minimap);
            _mapImg = Kz.El("hud-minimap__map", _mapClip);
            Kz.El("hud-minimap__frame", _minimap);
            _mapName = Kz.Text("", "hud-minimap__name", _minimap);
            _coords = Kz.Text("", "hud-minimap__coords", _minimap);
            Tip(_minimap, () => $"<b>{G.Map.Name}</b>\n{G.ZoneName}\nClique ou M: mapa e viagem");
            _minimap.RegisterCallback<ClickEvent>(_ => G.Toggle(KezorGame.UiPanel.Map));

            // ---------------- HUD v4 (base escura e discreta): cristal de Vida · Vigor · Q W E R · skills 1–5 · magia ativa ·
            //                  menu quadrado · Escudo · cristal de Mana; experiência em 20 segmentos por cima
            var bottom = Kz.El("hud4", hud);
            var exp = Kz.El("hud4-exp", bottom, pick: true);
            for (int i = 0; i < _expSeg.Length; i++) { _expSeg[i] = exp.Put(new KzBar("kz-bar--exp")); _expSeg[i].AddToClassList("hud4-exp__seg"); }
            _expText = Kz.Text("", "hud4-exp__text", exp);
            Tip(exp, ExpTip);

            var bar = Kz.El("hud4-bar", bottom, pick: true);
            // vida
            var lifeBox = Kz.El("hud4-crystal hud4-crystal--hp", bar, pick: true);
            _life = lifeBox.Put(new HudCrystal(KezorLook.Hex("#4a0805"), KezorLook.Hex("#b01c10"), KezorLook.Hex("#ff7a4a")));
            _hpText = Kz.Text("", "hud4-crystal__text", lifeBox);
            Kz.Text("VIDA", "hud4-crystal__cap", lifeBox);
            Tip(lifeBox, () => $"<b>Cristal de Vida</b>  {S.Me.Hp}/{S.Me.MaxHp}\nPoções nos atalhos Q W E R.");
            _vigor = bar.Put(new HudVBar(KezorLook.Hex("#c9a23a"), KezorLook.Hex("#ffe08a")));
            _vigor.AddToClassList("hud4-vbar--vigor");
            _vigorText = Kz.Text("", "hud4-vbar__text", _vigor);
            Tip(_vigor, () => $"<b>Vigor</b>  {S.Me.Vigor}/{S.Me.MaxVigor}\nAs skills iniciais não gastam; as outras gastam {Kezor.Core.Characters.Character.SkillVigorCost}. O ataque básico não gasta.\nVolta a cada {Kezor.Core.Characters.Character.RecoveryInterval:0} s (mais rápido na zona segura).");

            // atalhos de consumível
            var quick = Kz.El("hud4-group hud4-quick", bar, pick: true);
            string[] qk = { "Q", "W", "E", "R" };
            for (int i = 0; i < 4; i++)
            {
                int q = i;
                var k = quick.Put(new KzSlot("hud4-slot hud4-slot--quick"));
                k.Key.text = qk[i];
                k.userData = new DropTag(DragZone.Quick, q);
                k.RegisterCallback<PointerDownEvent>(e => { if (e.button == 1 && !_ui.Drag.Holding && S.Me.Quick[q] != 0) G.Send($"QUICK {q} 0"); });   // clique direito limpa
                k.RegisterCallback<ClickEvent>(_ => G.UseQuick(q));
                Tip(k, () => QuickTip(q));
                _quick[i] = k;
            }
            // skills 1–5 e magia ativa (botão direito)
            var skills = Kz.El("hud4-group hud4-skills", bar, pick: true);
            for (int i = 0; i < SkillSlots; i++)
            {
                int slot = i;
                var k = skills.Put(new KzSlot("hud4-slot hud-skill", cooldown: true));
                k.Key.text = (i + 1).ToString();
                k.RegisterCallback<ClickEvent>(_ => G.SelectSkill(slot));
                Tip(k, () => SkillTip(slot));
                _skills[i] = k;
            }
            _active = bar.Put(new KzSlot("hud4-slot hud4-slot--active", cooldown: true));
            _active.Key.text = "";
            Kz.Icon(Kz.Art("g_rmb"), "hud4-rmb", _active);
            Tip(_active, () => "<b>Magia ativa</b> (botão direito do mouse)\n" + SkillTip(G.SkillSlot));

            // menu quadrado (2 × 4)
            var menu = Kz.El("hud4-menu", bar, pick: true);
            foreach (var (panel, glyph, key, label) in new[]
            {
                // uma fileira de 5 botões quadrados; ranking (K), mapa (M), grupo (P) e ajuda (H) seguem nas teclas
                (KezorGame.UiPanel.Inventory, "bag", "I", "Inventário"), (KezorGame.UiPanel.Character, "char", "C", "Personagem"),
                (KezorGame.UiPanel.Guild, "guild", "G", "Guilda"), (KezorGame.UiPanel.Ascension, "a_mag", "A", "Árvore da Ascensão"),
                (KezorGame.UiPanel.Options, "options", "O", "Opções"),
            })
            {
                var b = Kz.El("hud4-menu__btn", menu, pick: true);
                Kz.Icon(Kz.Art("g_" + glyph), "hud4-menu__glyph", b);
                var p = panel;
                b.RegisterCallback<ClickEvent>(_ => G.Toggle(p));
                Tip(b, () => $"<b>{label}</b>  ({key})");
                _menuBtns[panel] = b;
            }

            // escudo e mana
            _shield = bar.Put(new HudVBar(KezorLook.Hex("#3aa8c8"), KezorLook.Hex("#a8f0ff")));
            _shield.AddToClassList("hud4-vbar--shield");
            _shieldText = Kz.Text("", "hud4-vbar__text", _shield);
            Tip(_shield, () => $"<b>Escudo</b>  {S.Me.Shield}/{S.Me.MaxShield}\nSó segura golpes de jogadores: {Kezor.Core.Characters.Character.ShieldRatioPercent}% vai no escudo e o resto na vida.\nRecarrega na zona segura, {Kezor.Core.Characters.Character.ShieldDelaySeconds:0} s depois de levar dano.");
            var manaBox = Kz.El("hud4-crystal hud4-crystal--mp", bar, pick: true);
            _mana = manaBox.Put(new HudCrystal(KezorLook.Hex("#081650"), KezorLook.Hex("#1c48c0"), KezorLook.Hex("#7ab8ff")));
            _mpText = Kz.Text("", "hud4-crystal__text", manaBox);
            Kz.Text("MANA", "hud4-crystal__cap", manaBox);
            Tip(manaBox, () => $"<b>Cristal de Mana</b>  {S.Me.Mp}/{S.Me.MaxMp}");

            // ordem da barra (HUD v5): Q W E R · Vida · Vigor · skills 1–5 · magia ativa · Escudo · Mana · menu; experiência embaixo
            foreach (var el in new VisualElement[] { quick, lifeBox, _vigor, skills, _active, _shield, manaBox, menu }) { el.RemoveFromHierarchy(); bar.Add(el); }
            exp.RemoveFromHierarchy(); bottom.Add(exp);

            // ---------------- chat
            var chat = Kz.El("hud-chat", hud);
            var tabs = Kz.El("hud-chat__tabs", chat, pick: true);
            foreach (var (id, label) in new[] { ("all", "Geral"), ("party", "Grupo"), ("guild", "Guilda"), ("sys", "Sistema") })
            {
                var tab = Kz.Btn(label, () => { _chatTab = id; }, "kz-tab", tabs);
                _chatTabs[id] = tab;
            }
            _chatLog = Kz.El("hud-chat__log", chat, pick: true);
            for (int i = 0; i < 11; i++) _chatLines.Add(Kz.Text("", "hud-chat__line", _chatLog));
            _chatInput = new TextField { maxLength = 120 };
            _chatInput.AddToClassList("kz-field"); _chatInput.AddToClassList("hud-chat__input");
            _chatInput.textEdition.placeholder = "Enter para conversar  ·  /comando";
            chat.Add(_chatInput);
            _chatInput.RegisterCallback<FocusInEvent>(_ => G.ChatFocused = true);
            _chatInput.RegisterCallback<FocusOutEvent>(_ => G.ChatFocused = false);
            _chatInput.RegisterCallback<KeyDownEvent>(e =>
            {
                if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)
                {
                    G.SendChat(_chatInput.value, _chatTab);
                    _chatInput.value = "";
                    _chatInput.Blur();
                    e.StopPropagation();
                }
                else if (e.keyCode == KeyCode.Escape) { _chatInput.value = ""; _chatInput.Blur(); e.StopPropagation(); }
            }, TrickleDown.TrickleDown);

            // ---------------- sobreposição
            _toasts = Kz.El("hud-toasts", ui.OverlayLayer);
            _tip = Kz.El("hud-tip", ui.OverlayLayer);
            _tipText = Kz.Text("", "kz-text", _tip);
            _tip.Show(false);
        }

        private Item? QuickItem(int q)
        {
            int id = S.Me.Quick[q];
            var def = id != 0 ? ItemDatabase.Get(id) : null;
            return def != null ? new Item(def) : null;
        }

        private string QuickTip(int q)
        {
            string[] keys = { "Q", "W", "E", "R" };
            int id = S.Me.Quick[q];
            var def = id != 0 ? ItemDatabase.Get(id) : null;
            if (def == null) return $"<b>Atalho {keys[q]}</b> (vazio)\nPegue uma poção na mochila e clique aqui.";
            return $"<b>{def.Name}</b>  ({keys[q]})\nRecupera {def.PotionRecover(def.Potion == PotionType.Health ? S.Me.MaxHp : S.Me.MaxMp, S.Me.Level)} de {(def.Potion == PotionType.Health ? "vida" : "mana")} ({def.PotionDescription}).\nNa mochila: <b>{G.ItemCount(id)}</b>\n<color=#7d7366>Clique direito limpa o atalho.</color>";
        }

        private string ExpTip()
        {
            var me = S.Me;
            float frac = me.ExpNext > 0 ? Mathf.Clamp01(me.Exp / (float)me.ExpNext) : 0f;
            return $"<b>Experiência</b>  nível {me.Level}\n{me.Exp:N0} / {me.ExpNext:N0}  ({frac * 100f:0.00}%)\nFaltam {System.Math.Max(0, me.ExpNext - me.Exp):N0}\n<color=#7d7366>Cada segmento é 5%.</color>";
        }

        // ------------------------------------------------------------------ tooltip simples
        public void Tip(VisualElement owner, Func<string> text)
        {
            owner.RegisterCallback<PointerEnterEvent>(e => { _tipOwner = owner; ShowTip(text(), e.position); });
            owner.RegisterCallback<PointerMoveEvent>(e => { if (_tipOwner == owner) ShowTip(text(), e.position); });
            owner.RegisterCallback<PointerLeaveEvent>(_ => { if (_tipOwner == owner) { _tipOwner = null; _tip.Show(false); } });
        }

        public void ShowTip(string text, Vector2 at)
        {
            _tipText.Set(text);
            _tip.Show(true);
            var root = _ui.Root.contentRect;
            float w = _tip.resolvedStyle.width > 1 ? _tip.resolvedStyle.width : 260, h = _tip.resolvedStyle.height > 1 ? _tip.resolvedStyle.height : 60;
            float x = at.x + 18, y = at.y - h - 10;
            if (x + w > root.width - 4) x = at.x - w - 18;
            if (y < 4) y = at.y + 22;
            _tip.style.left = x; _tip.style.top = y;
        }

        public void HideTip() { _tipOwner = null; _tip.Show(false); }

        private string SkillTip(int slot)
        {
            var list = G.ClassSkills();
            if (slot >= list.Count) return "Slot vazio";
            var sk = list[slot];
            var me = S.Me;
            var lines = new List<string> { $"<b><color=#ffb347>{sk.Name}</color></b>   <color=#8fa8ff>{sk.ManaCost} MP</color>" };
            string req = $"Nível {sk.ReqLevel}" + (sk.ReqEnergy > 0 ? $" · Energia {sk.ReqEnergy}" : "");
            lines.Add(me.Level >= sk.ReqLevel && me.Ene >= sk.ReqEnergy ? $"<color=#7d7366>{req}</color>" : $"<color=#ff6a5a>Requer {req}</color>");
            lines.Add(sk.Description);
            var tags = new List<string>();
            if (sk.AreaRadius > 0) tags.Add($"área {sk.AreaRadius:0.#} m");
            if (sk.Cooldown > 0) tags.Add($"recarga {sk.Cooldown:0.#} s");
            if (sk.Duration > 0) tags.Add($"dura {sk.Duration:0} s");
            if (!sk.NeedsTarget) tags.Add("sem alvo");
            if (tags.Count > 0) lines.Add($"<color=#b9ad97>{string.Join(" · ", tags)}</color>");
            lines.Add($"<color=#7d7366>Tecla {slot + 1} escolhe · botão direito usa · {sk.VigorCost} de vigor</color>");
            return string.Join("\n", lines);
        }

        // ------------------------------------------------------------------ toasts
        public void Toast(string text, string kind = "", float seconds = 3.2f)
        {
            while (_toasts.childCount >= 3) _toasts.RemoveAt(0);
            var t = Kz.El("hud-toast hud-toast--hidden" + (kind != "" ? " hud-toast--" + kind : ""), _toasts);
            Kz.Text(text, "hud-toast__text", t);
            t.schedule.Execute(() => t.RemoveFromClassList("hud-toast--hidden")).StartingIn(20);
            t.schedule.Execute(() => t.AddToClassList("hud-toast--hidden")).StartingIn((long)(seconds * 1000));
            t.schedule.Execute(() => t.RemoveFromHierarchy()).StartingIn((long)(seconds * 1000) + 400);
        }

        // ------------------------------------------------------------------ atualização
        public void Tick()
        {
            var me = S.Me;
            // herói
            var cls = ClassDefinition.Get(me.Class);
            _name.Set(me.Name);
            _class.Set(cls.DisplayName + (me.Rebirths > 0 ? $"  ·  Renasc. {me.Rebirths}" : ""));
            _lvl.Set(me.Level.ToString());
            _portraitGlyph.style.unityBackgroundImageTintColor = ClassTint.TryGetValue(me.Class, out var tint) ? tint : Color.white;
            bool safe = G.InSafeZone;
            _zone.Set(safe ? $"<color=#8fe08f>{G.ZoneName}</color>  <color=#7d7366>área segura</color>" : $"<color=#ff9a6a>{G.ZoneName}</color>");
            _gold.Set($"{me.Gold:N0}");
            _points.Set(me.FreePoints > 0 ? $"+{me.FreePoints} pontos (C)" : "");

            TickBuffs();
            TickParty();
            TickTarget();
            TickMinimap();
            TickBottom();
            TickChat();
            TickToasts();
            foreach (var kv in _menuBtns) kv.Value.SetClass("hud-menu__btn--on", G.IsOpen(kv.Key));
            _minimap.Show(G.MinimapOn);
            _eventRibbon.Show(S.EventRunning);
            if (S.EventRunning) _eventText.Set($"⚔  {S.EventName} em andamento — portão sul!");
        }

        private void TickBuffs()
        {
            int n = 0;
            float now = S.ServerTimeNow;
            var skills = SkillCatalog.All;
            foreach (var e in S.Effects)
            {
                if (n >= _buffPool.Count)
                {
                    var root = Kz.El("hud-buff", _buffs, pick: true);
                    var icon = Kz.El("hud-buff__icon", root);
                    var time = Kz.Text("", "hud-buff__time", root);
                    int idx = _buffPool.Count;
                    Tip(root, () => BuffTip(idx));
                    _buffPool.Add((root, icon, time));
                }
                var (r, ic, tx) = _buffPool[n++];
                r.Show(true);
                r.SetClass("hud-buff--aura", e.Aura);
                var sk = BuffSkill(e);
                ic.style.backgroundImage = sk == null ? new StyleBackground(Kz.Art("g_char")) : ItemIcons.Skill(sk.Id) is Sprite bsp ? new StyleBackground(bsp) : new StyleBackground(SkillIcons.Get(sk));
                tx.Set(e.Aura ? "aura" : Kz.Time(Mathf.Max(0, e.Expires - now)));
            }
            for (int i = n; i < _buffPool.Count; i++) _buffPool[i].root.Show(false);
        }

        private SkillDefinition? BuffSkill(ActiveEffect e)
        {
            var mask = ClassDefinition.MaskOf(S.Me.Class);
            if (e.Aura) return SkillCatalog.All.FirstOrDefault(s => s.Kind == SkillKind.Banner && s.Buff == e.Kind);
            return SkillCatalog.All.FirstOrDefault(s => s.Kind == SkillKind.SelfBuff && (s.Buff == e.Kind || (e.Kind == BuffKind.Damage && s.ExtraDamageBuff > 0)) && (s.Classes & mask) != 0)
                   ?? SkillCatalog.All.FirstOrDefault(s => s.Kind == SkillKind.SelfBuff && s.Buff == e.Kind);
        }

        private string BuffTip(int idx)
        {
            if (idx >= S.Effects.Count) return "";
            var e = S.Effects[idx];
            string what = e.Kind switch { BuffKind.Damage => $"+{e.Value}% de dano", BuffKind.Defense => $"+{e.Value} de defesa", BuffKind.Speed => $"+{e.Value}% de velocidade", _ => "" };
            var sk = BuffSkill(e);
            return e.Aura ? $"<b><color=#9fe39f>{sk?.Name ?? "Aura"}</color></b>\n{what}\nVale enquanto estiver perto do estandarte."
                          : $"<b><color=#ffb347>{sk?.Name ?? "Buff"}</color></b>\n{what}\nRestam {Kz.Time(Mathf.Max(0, e.Expires - S.ServerTimeNow))} s";
        }

        private void TickParty()
        {
            int n = 0;
            foreach (var (id, name) in S.PartyMembers)
            {
                if (id == S.MyId) continue;
                if (n >= _partyPool.Count)
                {
                    var root = Kz.El("kz-panel-dark hud-party__member", _party, pick: true);
                    var lbl = Kz.Text("", "kz-text-2", root);
                    var bar = root.Put(new KzBar("kz-bar--ally"));
                    _partyPool.Add((root, lbl, bar));
                }
                var (r, l, b) = _partyPool[n++];
                r.Show(true);
                bool near = S.Entities.TryGetValue(id, out var pe);
                l.Set((id == S.PartyLeaderId ? "<color=#ffb347>★</color> " : "") + name + (near ? $"  <color=#7d7366>Nv {pe!.Level}</color>" : "  <color=#7d7366>longe</color>"));
                b.Set(near ? pe!.Hp / (float)Mathf.Max(1, pe.MaxHp) : 0f);
            }
            for (int i = n; i < _partyPool.Count; i++) _partyPool[i].root.Show(false);
            _party.style.top = S.Effects.Count > 0 ? 214 : 166;
        }

        private void TickTarget()
        {
            int id = G.SelectedTarget != 0 ? G.SelectedTarget : G.HoverId;
            if (id == 0 || id == S.MyId || !S.Entities.TryGetValue(id, out var e) || (e.Kind != EntityKind.Monster && e.Kind != EntityKind.Player))
            { _target.Show(false); return; }
            _target.Show(true);
            bool boss = false; string sub;
            if (e.Kind == EntityKind.Monster)
            {
                var md = MonsterCatalog.Get(e.TypeId);
                boss = md?.Boss ?? false;
                string owner = e.OwnerId != 0 && S.Entities.TryGetValue(e.OwnerId, out var oe) ? $"Eco de {oe.Name}" : "";
                string danger = md != null && md.Level > S.Me.Level + 10 ? "  <color=#ff6a5a>perigoso</color>" : "";
                sub = owner != "" ? $"Nível {e.Level} · {owner}" : $"Nível {e.Level}" + (boss ? " · <color=#ffb347>Chefe</color>" : "") + danger;
                _targetBar.RemoveFromClassList("kz-bar--ally"); _targetBar.SetClass("kz-bar--echo", e.OwnerId != 0);
            }
            else
            {
                var cls = ClassDefinition.Get((CharacterClass)e.TypeId).DisplayName;
                bool party = S.Me.PartyId != 0 && e.PartyId == S.Me.PartyId;
                sub = $"Nível {e.Level} · {cls}" + (e.Guild != "" ? $" · ‹{e.Guild}›" : "") + (e.Infamy >= 2 ? " · <color=#ff6a5a>assassino</color>" : e.Infamy == 1 ? " · <color=#ff9a3c>aviso de PK</color>" : "");
                _targetBar.SetClass("kz-bar--ally", party); _targetBar.RemoveFromClassList("kz-bar--echo");
            }
            _target.SetClass("hud-target--boss", boss);
            _targetName.Set(e.Name);
            _targetSub.Set(sub);
            _targetBar.Set(e.Hp / (float)Mathf.Max(1, e.MaxHp), $"{e.Hp:N0} / {e.MaxHp:N0}");
        }

        // ------------------------------------------------------------------ minimapa
        private void TickMinimap()
        {
            var map = G.Map;
            if (_mapTexFor != map.Id)
            {
                _mapTexFor = map.Id;
                var tex = MinimapTexture.Build(map);
                _mapImg.style.backgroundImage = new StyleBackground(tex);
                float sizePx = map.Size * MapPx / (2f * MapView);
                _mapImg.style.width = sizePx; _mapImg.style.height = sizePx;
            }
            var me = S.Me;
            float k = MapPx / (2f * MapView);
            _mapImg.style.left = MapPx / 2f - me.X * k;
            _mapImg.style.top = MapPx / 2f - (map.Size - me.Z) * k;
            _mapName.Set(map.Name);
            _coords.Set($"X {me.X:0}  ·  Z {me.Z:0}");

            int n = 0;
            void Dot(float x, float z, Color c, float size, bool ring = false)
            {
                float dx = (x - me.X) * k, dz = (me.Z - z) * k;
                if (dx * dx + dz * dz > (MapPx / 2f - 4) * (MapPx / 2f - 4)) return;
                if (n >= _dots.Count) { var d = Kz.El("hud-dot", _mapClip); _dots.Add(d); }
                var e = _dots[n++];
                e.Show(true);
                e.style.width = size; e.style.height = size; e.style.borderTopLeftRadius = e.style.borderTopRightRadius = e.style.borderBottomLeftRadius = e.style.borderBottomRightRadius = size / 2f;
                e.style.left = MapPx / 2f + dx - size / 2f; e.style.top = MapPx / 2f + dz - size / 2f;
                e.style.backgroundColor = c;
                float bw = ring ? 1.5f : 0.8f;
                e.style.borderLeftWidth = e.style.borderRightWidth = e.style.borderTopWidth = e.style.borderBottomWidth = bw;
                var bc = ring ? new Color(1f, 0.7f, 0.28f) : new Color(0, 0, 0, 0.8f);
                e.style.borderLeftColor = e.style.borderRightColor = e.style.borderTopColor = e.style.borderBottomColor = bc;
            }
            foreach (var p in map.Portals) Dot(p.X, p.Z, WorldBuilder.PortalColor(p.TargetMap), 8, true);
            foreach (var (def, _) in G.Npcs) Dot(def.X, def.Z, new Color(1f, 0.83f, 0.35f), 6);
            foreach (var e in S.Entities.Values)
            {
                if (e.Id == S.MyId) continue;
                switch (e.Kind)
                {
                    case EntityKind.Monster:
                        if (e.OwnerId != 0) Dot(e.X, e.Z, new Color(0.45f, 0.85f, 1f), 4);
                        else Dot(e.X, e.Z, MonsterCatalog.Get(e.TypeId)?.Boss == true ? new Color(1f, 0.55f, 0.1f) : new Color(0.92f, 0.25f, 0.2f), MonsterCatalog.Get(e.TypeId)?.Boss == true ? 7 : 4);
                        break;
                    case EntityKind.Drop: Dot(e.X, e.Z, new Color(1f, 0.85f, 0.3f), 3); break;
                    case EntityKind.Banner: Dot(e.X, e.Z, Models.BannerColor(e.TypeId), 5); break;
                    default:
                        bool party = S.Me.PartyId != 0 && e.PartyId == S.Me.PartyId;
                        Dot(e.X, e.Z, party ? new Color(0.45f, 0.95f, 0.45f) : e.Infamy > 0 ? new Color(1f, 0.35f, 0.3f) : new Color(0.5f, 0.75f, 1f), 5);
                        break;
                }
            }
            Dot(me.X, me.Z, Color.white, 8, true);
            for (int i = n; i < _dots.Count; i++) _dots[i].Show(false);
        }

        // ------------------------------------------------------------------ orbes, skills, exp
        private void TickBottom()
        {
            var me = S.Me;
            _life.Fill = me.Hp / (float)Mathf.Max(1, me.MaxHp);
            _life.Warn = me.MaxHp > 0 && me.Hp * 4 < me.MaxHp;
            _mana.Fill = me.Mp / (float)Mathf.Max(1, me.MaxMp);
            _hpText.Set($"{me.Hp}");
            _mpText.Set($"{me.Mp}");
            _vigor.Fill = me.MaxVigor > 0 ? me.Vigor / (float)me.MaxVigor : 0f;
            _shield.Fill = me.MaxShield > 0 ? me.Shield / (float)me.MaxShield : 0f;
            _vigorText.Set(me.Vigor.ToString());
            _shieldText.Set(me.Shield.ToString());

            var skills = G.ClassSkills();
            for (int i = 0; i < SkillSlots; i++) FillSkill(_skills[i], i < skills.Count ? skills[i] : null, i == G.SkillSlot, me);
            FillSkill(_active, G.SkillSlot < skills.Count ? skills[G.SkillSlot] : null, false, me);

            for (int q = 0; q < 4; q++)
            {
                var k = _quick[q];
                int id = me.Quick[q];
                var def = id != 0 ? ItemDatabase.Get(id) : null;
                if (def == null) { k.SetSprite(null); k.SetIcon(null); k.Count.Set(""); k.SetClass("hud4-slot--empty", true); continue; }
                k.SetClass("hud4-slot--empty", false);
                var sp = ItemIcons.Item(id);
                if (sp != null) k.SetSprite(sp); else k.SetIcon(Kz.Art(def.Potion == PotionType.Mana ? "g_potion_mp" : "g_potion_hp"));
                int n = G.ItemCount(id);
                k.Count.Set(n.ToString());
                k.SetClass("kz-slot--nomana", n == 0);
            }

            float frac = me.ExpNext > 0 ? Mathf.Clamp01(me.Exp / (float)me.ExpNext) : 0f;
            int segs = _expSeg.Length;
            for (int i = 0; i < segs; i++) _expSeg[i].Set(Mathf.Clamp01(frac * segs - i));
            _expText.Set($"{frac * 100f:0}%");   // número curto no fim da barra; o detalhe fica na dica
        }

        private void FillSkill(KzSlot k, SkillDefinition? sk, bool selected, PlayerInfo me)
        {
            if (sk == null) { k.SetSprite(null); k.SetIcon(null); k.Caption.Set(""); k.Count.Set(""); k.Cd!.Frac = 0; k.CdText!.Set(""); k.SetClass("hud-skill--active", false); k.SetClass("kz-slot--locked", true); return; }
            var skSprite = ItemIcons.Skill(sk.Id);
            if (skSprite != null) k.SetSprite(skSprite); else k.SetIcon(SkillIcons.Get(sk));
            bool ok = me.Level >= sk.ReqLevel && me.Ene >= sk.ReqEnergy;
            k.SetClass("kz-slot--locked", !ok);
            k.SetClass("kz-slot--nomana", ok && (me.Mp < sk.ManaCost || me.Vigor < sk.VigorCost));
            k.SetClass("hud-skill--active", selected);
            k.Count.Set(ok ? "" : $"Nv{sk.ReqLevel}");
            float left = S.CooldownLeft(sk.Id);
            k.Cd!.Frac = sk.Cooldown > 0 ? Mathf.Clamp01(left / sk.Cooldown) : 0f;
            k.CdText!.Set(left > 0.05f ? Kz.Time(left) : "");
        }

        // ------------------------------------------------------------------ chat
        private static string ChannelColor(string ch) => ch switch
        {
            "err" => "#ff7a7a", "fail" => "#ffb36a", "sys" => "#ffd36a", "party" => "#8fe08f", "guild" => "#8fd0ff", "whisper" => "#f0a0e0", "notice" => "#ffcf5a", "ok" => "#bcd4ff", _ => "#f0eadc",
        };

        private bool InTab(string ch) => _chatTab switch
        {
            "party" => ch == "party",
            "guild" => ch == "guild",
            "sys" => ch == "sys" || ch == "err" || ch == "fail" || ch == "ok",
            _ => true,
        };

        private void TickChat()
        {
            foreach (var kv in _chatTabs) kv.Value.SetClass("kz-tab--on", kv.Key == _chatTab);
            if (InputK.KeyDown(KeyCode.Return) && !G.ChatFocused && _ui.Root.focusController?.focusedElement == null)
                _chatInput.schedule.Execute(() => _chatInput.Focus());
            if (S.Log.Count == _chatSeen && _chatTab == _chatTabSeen) return;
            _chatSeen = S.Log.Count; _chatTabSeen = _chatTab;
            var lines = new List<string>();
            for (int i = S.Log.Count - 1; i >= 0 && lines.Count < _chatLines.Count; i--)
            {
                var l = S.Log[i];
                if (!InTab(l.Channel)) continue;
                lines.Add($"<color={ChannelColor(l.Channel)}>{l.Text.Replace("<", "‹").Replace(">", "›")}</color>");
            }
            lines.Reverse();
            for (int i = 0; i < _chatLines.Count; i++) { var lbl = _chatLines[i]; bool on = i < lines.Count; lbl.Show(on); if (on) lbl.Set(lines[i]); }
        }

        private void TickToasts()
        {
            var me = S.Me;
            if (_lastLevel >= 0 && me.Level > _lastLevel) Toast($"Nível {me.Level}!", "level");
            _lastLevel = me.Level;
            if (_lastMap >= 0 && S.MapId != _lastMap) Toast(G.Map.Name, "level", 2.6f);
            _lastMap = S.MapId;
            if (S.EventRunning && !_lastEvent) Toast($"⚔ {S.EventName}: defenda o portão sul!", "event", 4f);
            if (!S.EventRunning && _lastEvent) Toast("A invasão terminou.", "", 3f);
            _lastEvent = S.EventRunning;
            if (!string.IsNullOrEmpty(S.PendingInvite) && S.PendingInvite != _lastInvite) Toast($"{S.PendingInvite} convidou você para um grupo  (P)", "", 5f);
            _lastInvite = S.PendingInvite ?? "";
            if (!string.IsNullOrEmpty(S.WarInviteFrom) && S.WarInviteFrom != _lastWarInvite) Toast($"{S.WarInviteFrom} declarou guerra!", "bad", 5f);
            _lastWarInvite = S.WarInviteFrom ?? "";
        }
    }
}
