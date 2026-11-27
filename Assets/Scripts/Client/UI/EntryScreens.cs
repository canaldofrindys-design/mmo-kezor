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
    /// Tela de criação de herói (aberta pela seleção): o herói de amostra da classe em pé no pátio noturno, grande no
    /// centro (palco do <see cref="SelectStage"/>, arrastar gira), as 7 classes numa coluna à esquerda, a ficha da classe
    /// (papel, história e atributos base) à direita e, embaixo, o nome e o botão Criar. Trocar de classe faz o herói novo
    /// entrar com uma pose de golpe. O login (servidores e conta) fica no <see cref="LoginScreen"/>.
    /// </summary>
    public sealed class EntryScreen
    {
        private readonly UiRoot _ui;
        private KezorGame G => _ui.Game;
        public readonly VisualElement Root;
        private readonly SelectStage _stage;
        private readonly List<(VisualElement row, int idx)> _classRows = new List<(VisualElement, int)>();
        private readonly TextField _name;
        private readonly Toggle _showcase;
        private readonly Label _error, _heroName, _heroRole, _heroLore, _count;
        private readonly KzBar[] _statBars = new KzBar[4];
        private readonly Label[] _statVals = new Label[4];
        private readonly Button _enter;
        private bool _dragging; private float _lastX;
        private bool Creating => G.Connected && !G._connecting && G._loginStage == KezorGame.LoginStage.Create;

        public EntryScreen(UiRoot ui)
        {
            _ui = ui;
            _stage = SelectStage.Get(ui.Game.Cam);
            Root = Kz.El("cr", ui.Root, pick: false);
            Kz.El("sel__vignette", Root);
            var drag = Kz.El("sel__drag", Root, pick: true);
            drag.RegisterCallback<PointerDownEvent>(e => { if (e.button != 0) return; _dragging = true; _lastX = e.position.x; drag.CapturePointer(e.pointerId); });
            drag.RegisterCallback<PointerMoveEvent>(e => { if (_dragging && drag.HasPointerCapture(e.pointerId)) { _stage.Drag(-(e.position.x - _lastX) * 0.6f); _lastX = e.position.x; } });
            drag.RegisterCallback<PointerUpEvent>(e => { _dragging = false; if (drag.HasPointerCapture(e.pointerId)) drag.ReleasePointer(e.pointerId); });

            // cabeçalho
            var head = Kz.El("sel__head", Root);
            Kz.Text("Criar herói", "kz-title sel__title", head);
            Kz.El("kz-divider sel__head-div", head);
            Kz.Text("Escolha a classe e dê um nome ao seu herói", "sel__subtitle", head);

            // classes
            var left = Kz.El("kz-panel cr__classes", Root, pick: true);
            Kz.Text("Classe", "kz-h2 cr__h", left);
            for (int i = 0; i < ClassDefinition.All.Length; i++) _classRows.Add((ClassRow(left, i), i));

            // ficha da classe
            var info = Kz.El("kz-panel cr__info", Root, pick: true);
            _heroName = Kz.Text("", "kz-title cr__hero-name", info);
            _heroRole = Kz.Text("", "kz-gold cr__hero-role", info);
            Kz.El("kz-divider", info);
            _heroLore = Kz.Text("", "kz-text-2 cr__lore", info);
            Kz.Text("Atributos iniciais", "cr__sub", info);
            string[] statNames = { "Força", "Agilidade", "Vitalidade", "Energia" };
            for (int i = 0; i < 4; i++)
            {
                var r = Kz.El("entry__stat", info);
                Kz.Text(statNames[i], "kz-text-2 entry__stat-name", r);
                _statBars[i] = r.Put(new KzBar("kz-bar--exp"));
                _statBars[i].AddToClassList("entry__stat-bar");
                _statVals[i] = Kz.Text("", "kz-text kz-num entry__stat-val", r);
            }
            Kz.El("kz-divider", info);
            _showcase = new Toggle("Ver com o equipamento da classe (+9)"); _showcase.AddToClassList("kz-toggle");
            info.Add(_showcase);
            Kz.Text("Arraste o herói para girar.", "kz-text-3", info);

            // faixa de baixo: voltar, nome e criar
            Kz.El("sel__bar", Root);
            var back = Kz.El("sel__corner sel__corner--left", Root, pick: true);
            Kz.Btn("Voltar", () => { G._loginError = ""; G._loginStage = KezorGame.LoginStage.Characters; }, "kz-btn sel__btn", back);
            var bar = Kz.El("cr__namebar", Root, pick: true);
            Kz.Text("Nome", "cr__name-label", bar);
            _name = new TextField { maxLength = 12 };
            _name.AddToClassList("kz-field"); _name.AddToClassList("cr__name");
            _name.textEdition.placeholder = "nome do herói";
            _name.RegisterValueChangedCallback(e => G._name = e.newValue);
            bar.Add(_name);
            _enter = Kz.Btn("Criar herói", CreateFromForm, "kz-btn kz-btn--primary sel__btn sel__btn--big", bar);
            var right = Kz.El("sel__corner sel__corner--right", Root);
            _count = Kz.Text("", "cr__count", right);
            _error = Kz.Text("", "sel__error", Root);

            _name.value = G._name;
            Root.RegisterCallback<KeyDownEvent>(e => { if ((e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter) && Creating) CreateFromForm(); }, TrickleDown.TrickleDown);
        }

        private VisualElement ClassRow(VisualElement parent, int idx)
        {
            var def = ClassDefinition.All[idx];
            var row = Kz.El("cr__class", parent, pick: true);
            SelectScreen.ClassIcon(Kz.El("cr__class-icon", row), def.Class);
            var col = Kz.El("entry__class-text", row);
            Kz.Text(def.DisplayName, "cr__class-name", col);
            Kz.Text(def.Role, "cr__class-role", col);
            row.RegisterCallback<ClickEvent>(_ => G._classIndex = idx);
            return row;
        }

        /// <summary>Cria o herói com o nome e a classe escolhidos e volta para a seleção.</summary>
        private void CreateFromForm()
        {
            if (!Creating) return;
            if (G._name.Trim() == "") { G._loginError = "Digite um nome para o herói."; return; }
            var code = ClassDefinition.All[Mathf.Clamp(G._classIndex, 0, ClassDefinition.All.Length - 1)].Code;
            G.Send($"CREATE {G._name.Trim()} {code}", rep =>
            {
                if (rep.StartsWith("ERR")) { G._loginError = rep.Substring(4); return; }
                G._loginError = "";
                G._loginStage = KezorGame.LoginStage.Characters;
                G.RequestCharList();
            });
        }

        public void Tick()
        {
            bool show = !G.InWorld && Creating;
            Root.Show(show);
            if (!show) return;
            int n = ClassDefinition.All.Length;
            if (_name.focusController?.focusedElement != _name)
            {   // ↑/↓ trocam de classe (fora da caixa do nome)
                if (InputK.KeyDown(KeyCode.UpArrow)) G._classIndex = (G._classIndex - 1 + n) % n;
                if (InputK.KeyDown(KeyCode.DownArrow)) G._classIndex = (G._classIndex + 1) % n;
            }
            G._classIndex = Mathf.Clamp(G._classIndex, 0, n - 1);
            _enter.SetEnabled(!G._connecting);
            _error.Set(G._loginError != "" ? $"<color=#ff7a6a>{G._loginError}</color>" : "");
            _count.Set($"{G._charRows.Count} de 5 heróis");
            foreach (var (row, idx) in _classRows) row.SetClass("cr__class--on", G._classIndex == idx);

            var def = ClassDefinition.All[G._classIndex];
            _stage.ShowSample(def.Class, _showcase.value);
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
