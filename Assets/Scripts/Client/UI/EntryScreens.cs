using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using Kezor.Core.Characters;
using Kezor.Core.Combat;
using Kezor.Core.World;

namespace Kezor.Client.UI
{
    /// <summary>
    /// Telas de entrada: logotipo, modo (local, LAN, servidor dedicado), conta e senha, seleção e criação de personagem
    /// com o herói da classe girando num pedestal (RenderTexture do <see cref="HeroPreview"/>), descrição e atributos base.
    /// A lógica de conexão continua no <see cref="KezorGame"/>.
    /// </summary>
    public sealed class EntryScreen
    {
        private readonly UiRoot _ui;
        private KezorGame G => _ui.Game;
        public readonly VisualElement Root;
        private readonly HeroPreview _preview;
        private readonly VisualElement _left, _modeTabs, _formLocal, _formOnline, _formChars, _classList, _charList, _createBox;
        private readonly List<(VisualElement row, int idx)> _classRows = new List<(VisualElement, int)>();
        private readonly TextField _name, _port, _host, _portOnline, _account, _password, _newName;
        private readonly Toggle _register, _showcase;
        private readonly Label _error, _heroName, _heroRole, _heroLore, _hostHint;
        private readonly KzBar[] _statBars = new KzBar[4];
        private readonly Label[] _statVals = new Label[4];
        private readonly Button _enter, _play, _back;
        private readonly VisualElement _formCreate, _stageEl;
        private bool Creating => G.Connected && !G._connecting && G._loginStage == KezorGame.LoginStage.Create;

        /// <summary>Cria o herói com o nome e a classe escolhidos e volta para a seleção.</summary>
        private void CreateFromForm()
        {
            var code = ClassDefinition.All[Mathf.Clamp(G._classIndex, 0, ClassDefinition.All.Length - 1)].Code;
            G.Send($"CREATE {G._name.Trim()} {code}", rep =>
            {
                if (rep.StartsWith("ERR")) { G._loginError = rep.Substring(4); return; }
                G._loginError = "";
                G._loginStage = KezorGame.LoginStage.Characters;
                G.RequestCharList();
            });
        }
        private int _charSel;
        private string _charSig = "";
        private bool _dragging; private float _lastX;

        public EntryScreen(UiRoot ui)
        {
            _ui = ui;
            _preview = HeroPreview.Create(ui.Game.Cam);
            Root = Kz.El("entry", ui.Root, pick: true);
            Kz.El("entry__vignette", Root);
            Kz.El("entry__shade", Root);

            // ---------------- logotipo
            var logo = Kz.El("entry__logo", Root);
            Kz.Text("KEZOR", "kz-logo", logo);
            Kz.Text("Crônicas de Brasalume", "entry__tagline", logo);

            // ---------------- coluna da esquerda
            _left = Kz.El("kz-panel entry__left", Root, pick: true);
            _modeTabs = Kz.El("win-tabs", _left, pick: true);

            // local e LAN: nome + classe
            _formLocal = Kz.El("entry__form", _left, pick: true);
            _hostHint = Kz.Text("", "kz-text-3", _formLocal);
            var pr = Kz.El("win-row", _formLocal, pick: true);
            Kz.Text("Porta", "entry__label entry__label--inline", pr);
            _port = Field(pr, 6, "7777"); _port.style.width = 100;
            _port.RegisterValueChangedCallback(e => G._port = e.newValue);
            // criação (aberta pela tela de seleção): nome + classe + herói em destaque
            _formCreate = Kz.El("entry__form", _left, pick: true);
            Kz.Text("Novo herói", "kz-h2", _formCreate);
            Kz.Text("Nome do herói", "entry__label", _formCreate);
            _name = Field(_formCreate, 12, "Nome");
            _name.RegisterValueChangedCallback(e => G._name = e.newValue);
            Kz.Text("Classe", "entry__label", _formCreate);
            _classList = Kz.El("entry__classes", _formCreate, pick: true);
            for (int i = 0; i < ClassDefinition.All.Length; i++) _classRows.Add((ClassRow(_classList, i, () => G._classIndex, v => G._classIndex = v), i));

            // servidor dedicado: conta
            _formOnline = Kz.El("entry__form", _left, pick: true);
            Kz.Text("Endereço do servidor", "entry__label", _formOnline);
            var hr = Kz.El("win-row", _formOnline, pick: true);
            _host = Field(hr, 64, "127.0.0.1"); _host.style.width = 230;
            _host.RegisterValueChangedCallback(e => G._host = e.newValue);
            _portOnline = Field(hr, 6, "7777"); _portOnline.style.width = 90;
            _portOnline.RegisterValueChangedCallback(e => G._port = e.newValue);
            Kz.Text("Conta", "entry__label", _formOnline);
            _account = Field(_formOnline, 16, "conta");
            _account.RegisterValueChangedCallback(e => G._account = e.newValue);
            Kz.Text("Senha", "entry__label", _formOnline);
            _password = Field(_formOnline, 64, "senha");
            _password.isPasswordField = true; _password.maskChar = '•';
            _password.RegisterValueChangedCallback(e => G._password = e.newValue);
            _register = new Toggle("Criar conta nova (senha de 6+ caracteres)"); _register.AddToClassList("kz-toggle");
            _register.RegisterValueChangedCallback(e => G._registering = e.newValue);
            _formOnline.Add(_register);
            Kz.Text("A senha só vai para o servidor, que guarda apenas o hash.", "kz-text-3", _formOnline);

            // servidor dedicado: personagens
            _formChars = Kz.El("entry__form", _left, pick: true);
            Kz.Text("Seus heróis", "kz-h2", _formChars);
            _charList = Kz.El("entry__chars", _formChars, pick: true);
            _play = Kz.Btn("Jogar com este herói", PlaySelected, "kz-btn kz-btn--primary entry__wide", _formChars);
            Kz.El("kz-divider", _formChars);
            _createBox = Kz.El("", _formChars, pick: true);
            Kz.Text("Criar herói", "kz-h2", _createBox);
            _newName = Field(_createBox, 12, "Nome do novo herói");
            _newName.RegisterValueChangedCallback(e => G._newCharName = e.newValue);
            var cl2 = Kz.El("entry__classes entry__classes--compact", _createBox, pick: true);
            for (int i = 0; i < ClassDefinition.All.Length; i++) _classRows.Add((ClassRow(cl2, i, () => G._newCharClass, v => G._newCharClass = v), i));
            Kz.Btn("Criar", CreateChar, "kz-btn", _createBox);
            Kz.Btn("Sair da conta", () => { G.Disconnect(); G._loginStage = KezorGame.LoginStage.Form; }, "kz-btn kz-btn--small", _formChars);

            _error = Kz.Text("", "entry__error", _left);
            _back = Kz.Btn("Voltar para a seleção", () => { G._loginError = ""; G._loginStage = KezorGame.LoginStage.Characters; }, "kz-btn", _left);
            _enter = Kz.Btn("Entrar", () => { if (Creating) CreateFromForm(); }, "kz-btn kz-btn--primary entry__enter", _left);

            // ---------------- herói em destaque
            var stage = Kz.El("entry__stage", Root, pick: true);
            _stageEl = stage;
            var img = Kz.El("entry__preview", stage, pick: true);
            img.style.backgroundImage = new StyleBackground(Background.FromRenderTexture(_preview.Texture));
            img.RegisterCallback<PointerDownEvent>(e => { _dragging = true; _lastX = e.position.x; img.CapturePointer(e.pointerId); });
            img.RegisterCallback<PointerMoveEvent>(e => { if (_dragging) { _preview.DragYaw -= (e.position.x - _lastX) * 0.6f; _lastX = e.position.x; } });
            img.RegisterCallback<PointerUpEvent>(e => { _dragging = false; img.ReleasePointer(e.pointerId); });
            var info = Kz.El("kz-panel-dark entry__info", stage, pick: true);
            _heroName = Kz.Text("", "kz-title entry__hero-name", info);
            _heroRole = Kz.Text("", "kz-gold entry__hero-role", info);
            _heroLore = Kz.Text("", "kz-text-2 entry__lore", info);
            string[] statNames = { "Força", "Agilidade", "Vitalidade", "Energia" };
            for (int i = 0; i < 4; i++)
            {
                var r = Kz.El("entry__stat", info);
                Kz.Text(statNames[i], "kz-text-2 entry__stat-name", r);
                _statBars[i] = r.Put(new KzBar("kz-bar--exp"));
                _statBars[i].AddToClassList("entry__stat-bar");
                _statVals[i] = Kz.Text("", "kz-text kz-num entry__stat-val", r);
            }
            _showcase = new Toggle("Ver com o equipamento da classe"); _showcase.AddToClassList("kz-toggle");
            info.Add(_showcase);
            Kz.Text("Arraste o herói para girar.", "kz-text-3", info);

            Kz.Text("Kezor · fantasia sombria low poly · servidor autoritativo", "entry__footer", Root);
            _name.value = G._name; _port.value = G._port; _portOnline.value = G._port; _host.value = G._host;
            Root.RegisterCallback<KeyDownEvent>(e => { if ((e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter) && !G._connecting) { if (Creating) CreateFromForm(); else if (G._loginStage == KezorGame.LoginStage.Characters) PlaySelected(); } }, TrickleDown.TrickleDown);
        }

        private TextField Field(VisualElement parent, int max, string placeholder)
        {
            var f = new TextField { maxLength = max };
            f.AddToClassList("kz-field");
            f.textEdition.placeholder = placeholder;
            parent.Add(f);
            return f;
        }

        private VisualElement ClassRow(VisualElement parent, int idx, System.Func<int> get, System.Action<int> set)
        {
            var def = ClassDefinition.All[idx];
            var row = Kz.El("entry__class", parent, pick: true);
            var first = SkillCatalog.All.FirstOrDefault(s => s.Id != 0 && (s.Classes & ClassDefinition.MaskOf(def.Class)) != 0);
            var icon = Kz.El("entry__class-icon", row);
            var sp = first != null ? ItemIcons.Skill(first.Id) : null;
            if (sp != null) icon.style.backgroundImage = new StyleBackground(sp);
            else if (first != null) icon.style.backgroundImage = new StyleBackground(SkillIcons.Get(first));
            var col = Kz.El("entry__class-text", row);
            Kz.Text(def.DisplayName, "entry__class-name", col);
            Kz.Text(def.Role, "entry__class-role", col);
            row.RegisterCallback<ClickEvent>(_ => set(idx));
            row.userData = get;
            return row;
        }

        private void PlaySelected()
        {
            if (_charSel < 0 || _charSel >= G._charList.Count) return;
            var name = G._charList[_charSel].name;
            G.Send($"PLAY {name}", rep => { if (rep.StartsWith("ERR")) G._loginError = rep.Substring(4); else G._loginStage = KezorGame.LoginStage.Form; });
        }

        private void CreateChar()
        {
            var code = ClassDefinition.All[G._newCharClass].Code;
            G.Send($"CREATE {G._newCharName.Trim()} {code}", rep =>
            {
                if (rep.StartsWith("ERR")) { G._loginError = rep.Substring(4); return; }
                G._newCharName = ""; _newName.value = "";
                G.RequestCharList();
            });
        }

        public void Tick()
        {
            bool connected = G.Connected && !G._connecting;
            bool creating = Creating;
            bool show = !G.InWorld && creating;   // o login (lista de servidores e conta) fica no LoginScreen
            Root.Show(show);
            _preview.SetActive(show && creating);
            if (!show) return;
            _modeTabs.Show(false);
            _formLocal.Show(false);
            _formOnline.Show(false);
            _formChars.Show(false);
            _formCreate.Show(creating);
            _back.Show(creating);
            _stageEl.Show(creating);
            _enter.text = "Criar herói";
            _enter.SetEnabled(!G._connecting);
            _error.Set(G._loginError != "" ? $"<color=#ff7a6a>{G._loginError}</color>" : "");
            foreach (var (row, idx) in _classRows)
            {
                var get = (System.Func<int>)row.userData;
                row.SetClass("entry__class--on", get() == idx);
            }
            bool chars = false;

            // herói em destaque
            CharacterClass shown;
            if (chars && G._charList.Count > 0 && _charSel < G._charList.Count) shown = (CharacterClass)G._charList[_charSel].cls;
            else if (chars) shown = ClassDefinition.All[G._newCharClass].Class;
            else shown = ClassDefinition.All[Mathf.Clamp(G._classIndex, 0, ClassDefinition.All.Length - 1)].Class;
            _preview.Show(shown, _showcase.value);
            var def = ClassDefinition.Get(shown);
            _heroName.Set(def.DisplayName);
            _heroRole.Set(def.Role);
            _heroLore.Set(def.Lore);
            var s = def.BaseStats;
            int[] v = { s.Strength, s.Agility, s.Vitality, s.Energy };
            for (int i = 0; i < 4; i++) { _statBars[i].Set(v[i] / 35f); _statVals[i].Set(v[i].ToString()); }
        }
    }

    /// <summary>Tela de carregamento entre mapas: nome do mapa, mapa ao fundo, dica e barra; fica pelo menos ~1 s e some suave.</summary>
    public sealed class LoadingScreen
    {
        private static readonly string[] Tips =
        {
            "Runas da Aurora levam um item até +6 sem risco. Do +6 ao +9, a Runa do Eclipse pode falhar.",
            "O baú da Ilsa é o mesmo em qualquer cidade — guarde ali o que não quer perder.",
            "Grupos dividem a experiência e ganham +15% por membro.",
            "O Comandante finca estandartes: fique dentro do anel para receber a aura.",
            "A Invocadora mantém até dois ecos; eles somem se ela trocar de mapa.",
            "Cinco golpes seguidos no mesmo alvo soltam o Fluxo de Brasa do Punho.",
            "Itens Ascendentes brilham em verde e trazem opções especiais.",
            "Fora da vila há PvP a partir do nível 6. Matar inocentes dá infâmia.",
            "A Invasão das Cinzas ataca o portão sul de tempos em tempos.",
            "Passe o mouse sobre um item para comparar com o que você está usando.",
            "Pressione M para ver o mapa e viajar entre as regiões.",
        };

        private readonly UiRoot _ui;
        private readonly VisualElement _root, _map;
        private readonly Label _name, _sub, _tip;
        private readonly KzBar _bar;
        private float _shownAt = -10, _doneAt = -1;
        private bool _active;

        public LoadingScreen(UiRoot ui)
        {
            _ui = ui;
            _root = Kz.El("loading", ui.Root, pick: true);
            _map = Kz.El("loading__map", _root);
            Kz.El("entry__vignette", _root);
            var box = Kz.El("loading__box", _root);
            _name = Kz.Text("", "kz-logo loading__name", box);
            _sub = Kz.Text("", "entry__tagline", box);
            Kz.El("kz-divider loading__divider", box);
            _bar = box.Put(new KzBar("kz-bar--exp"));
            _bar.AddToClassList("loading__bar");
            _tip = Kz.Text("", "kz-text-2 loading__tip", box);
            _root.Show(false);
        }

        public void Show(MapDefinition map)
        {
            _active = true; _shownAt = Time.unscaledTime; _doneAt = -1;
            _root.Show(true); _root.style.opacity = 1;
            _root.BringToFront();
            _map.style.backgroundImage = new StyleBackground(MinimapTexture.Build(map));
            _name.Set(map.Name);
            _sub.Set(map.MinLevel > 1 ? $"Região para nível {map.MinLevel}+" : "Terras de Brasalume");
            _tip.Set("Dica: " + Tips[Random.Range(0, Tips.Length)]);
            _bar.Set(0.1f);
        }

        public void Done() { if (_active) _doneAt = Time.unscaledTime; }

        public void Tick()
        {
            if (!_active) return;
            float t = Time.unscaledTime - _shownAt;
            _bar.Set(_doneAt < 0 ? Mathf.Min(0.85f, 0.1f + t * 0.8f) : Mathf.Min(1f, 0.85f + (Time.unscaledTime - _doneAt) * 1.5f));
            if (_doneAt >= 0 && t > 1.1f)
            {
                float fade = Mathf.Clamp01((Time.unscaledTime - Mathf.Max(_doneAt, _shownAt + 1.1f)) / 0.35f);
                _root.style.opacity = 1f - fade;
                if (fade >= 1f) { _active = false; _root.Show(false); }
            }
        }
    }
}
