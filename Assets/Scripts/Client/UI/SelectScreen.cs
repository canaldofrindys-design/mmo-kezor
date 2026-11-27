using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using Kezor.Core.Characters;
using Kezor.Core.Combat;
using Kezor.Core.Items;
using Kezor.Core.Net;

namespace Kezor.Client.UI
{
    /// <summary>
    /// Palco 3D das telas de seleção e de criação: pátio noturno do Kezor (Resources/Models/Environment/SelecaoPatio)
    /// longe do mundo, braseiros com fogo tremulando e faíscas.
    /// <para>Seleção: os até 5 heróis da conta ficam nos discos de bronze; o escolhido dá um passo à frente, faz uma pose
    /// de golpe e a câmera chega perto dele (herói grande, ~60% da altura da tela), deslizando pela fila ao trocar.
    /// Os vizinhos aparecem menores nas laterais.</para>
    /// <para>Criação: só o herói de amostra da classe, no centro, no mesmo enquadramento.</para>
    /// Os heróis usam o <see cref="EntityView"/> do jogo, então respiram, andam e golpeiam com as animações do Blender.
    /// </summary>
    public sealed class SelectStage : MonoBehaviour
    {
        public enum Mode { Off, Select, Create }

        public static readonly Vector3 Origin = new Vector3(0, -900f, 0);
        private static SelectStage? _instance;
        public Camera Cam { get; private set; } = null!;

        /// <summary>Palco único, criado pela primeira tela que pedir (criação ou seleção).</summary>
        public static SelectStage Get(Camera main)
        {
            if (_instance != null) return _instance;
            var go = new GameObject("PalcoSelecao");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<SelectStage>();
            _instance._main = main;
            _instance.Build();
            return _instance;
        }

        private sealed class Hero
        {
            public Rig Rig = null!;
            public EntityView View = null!;
            public Vector3 Pos;
            public bool Posed;
        }

        private readonly Vector3[] _spots = new Vector3[5];
        private readonly List<(Transform t, float phase)> _flames = new List<(Transform, float)>();
        private readonly Hero?[] _heroes = new Hero?[5];
        private readonly string[] _keys = new string[5];
        private Hero? _sample;
        private string _sampleKey = "";
        private Transform _ring = null!;
        private int _selected = -1;
        private Mode _mode;
        private Camera? _main;
        private Vector3 _focus;

        /// <summary>Topo dos braseiros em pilares (kezor_select.py BRAZIERS; Blender x,y,z → Unity x,z,-y).</summary>
        private static readonly Vector3[] FirePos =
        {
            new Vector3(-7.6f, 1.5f, 0.6f), new Vector3(7.6f, 1.5f, 0.6f), new Vector3(-3.9f, 2.7f, -2.6f),
            new Vector3(3.9f, 2.7f, -2.6f), new Vector3(-1.6f, 3.7f, -4.4f), new Vector3(1.6f, 3.7f, -4.4f),
        };
        /// <summary>Passo entre os heróis na fila (igual aos discos de bronze da cena).</summary>
        public const float Spacing = 2.6f;
        /// <summary>Quanto o herói escolhido avança para a frente do disco.</summary>
        public const float StepForward = 1.15f;
        /// <summary>Velocidade do passo (m/s): devagar o bastante para o EntityView tocar a caminhada.</summary>
        private const float WalkSpeed = 1.5f;
        /// <summary>Altura aproximada do herói para a placa e a área de clique.</summary>
        public const float HeroHeight = 2.1f;

        /// <summary>Giro extra que o jogador deu arrastando (graus); volta devagar para a frente quando solta.</summary>
        public float DragYaw;
        private float _lastDrag = -10f;
        public void Drag(float degrees) { DragYaw = Mathf.Repeat(DragYaw + degrees + 180f, 360f) - 180f; _lastDrag = Time.unscaledTime; }

        private void Build()
        {
            var scene = ModelLibrary.Instantiate("Environment/SelecaoPatio", "SelecaoPatio", transform);
            if (scene != null) scene.transform.position = Origin;
            for (int i = 0; i < 5; i++) _spots[i] = Origin + new Vector3(2 * Spacing - i * Spacing, 0.04f, 0);   // fila da esquerda para a direita na tela (a câmera olha para -Z)
            var fx = gameObject.AddComponent<GearFx>();
            for (int i = 0; i < FirePos.Length; i++)
            {
                var f = scene != null ? scene.transform.Find("Fogo" + i) : null;
                if (f == null) continue;
                // o pivô da chama não sobrevive à importação: põe cada fogo de volta no seu braseiro (Blender x,y → Unity x,-y)
                f.position = Origin + FirePos[i];
                _flames.Add((f, Random.value * 10f));
                _fireLights.Add((f.position + Vector3.up * 0.6f, new Color(1f, 0.5f, 0.18f) * 1.1f, 7f));
                var ps = fx.Sparks(f, new Vector3(0, 0.25f, 0), new Vector3(0.4f, 0.3f, 0.4f), KezorLook.Hex("#ff8a2a"), 22f, 0.06f, -0.9f);
                var main = ps.main; main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.6f);
            }
            // anel de brasa sob o escolhido, com brasas subindo
            var mb = new MeshBuilder();
            const int n = 24;
            for (int i = 0; i < n; i++)
            {
                if (i % 4 == 3) continue;
                float a0 = i * Mathf.PI * 2f / n, a1 = (i + 0.8f) * Mathf.PI * 2f / n;
                var p0 = new Vector3(Mathf.Cos(a0) * 0.82f, 0.08f, Mathf.Sin(a0) * 0.82f);
                var p1 = new Vector3(Mathf.Cos(a1) * 0.82f, 0.08f, Mathf.Sin(a1) * 0.82f);
                mb.SetTRS((p0 + p1) * 0.5f, Quaternion.LookRotation(p1 - p0), Vector3.one);
                mb.Box(Vector3.zero, new Vector3(0.11f, 0.03f, (p1 - p0).magnitude), KezorLook.Glow(KezorLook.Hex("#ff8a2a")));
                mb.ResetMatrix();
            }
            mb.Ring(new Vector3(0, 0.07f, 0), 0.62f, 0.66f, 32, KezorLook.Glow(KezorLook.Hex("#ffd36a"), 0.8f));
            mb.Ring(new Vector3(0, 0.06f, 0), 0.98f, 1.0f, 40, KezorLook.Glow(KezorLook.Hex("#ff6a1a"), 0.55f));
            var ring = KezorLook.MakeObject("AnelSelecao", mb.ToMesh(), transform);
            ring.AddComponent<Spin>().Speed = 30f;
            var embers = fx.Sparks(ring.transform, new Vector3(0, 0.15f, 0), new Vector3(1.5f, 0.1f, 1.5f), KezorLook.Hex("#ffa040"), 12f, 0.045f, -0.35f);
            var em = embers.main; em.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2.4f);
            _ring = ring.transform;
            ring.SetActive(false);

            var cg = new GameObject("CameraSelecao");
            cg.transform.SetParent(transform, false);
            Cam = cg.AddComponent<Camera>();
            Cam.fieldOfView = 30f; Cam.nearClipPlane = 0.2f; Cam.farClipPlane = 70f;
            Cam.clearFlags = CameraClearFlags.SolidColor;
            Cam.backgroundColor = new Color(0.035f, 0.045f, 0.08f);
            Cam.depth = 5;
            var bloom = KezorBloom.Attach(Cam);
            bloom.Scale = 0.85f; bloom.Threshold = 1.1f;    // palco escuro: refino alto, épicas e asas brilham forte
            _focus = Origin + new Vector3(0, 0, StepForward);
            PlaceCamera(true);
            Cam.enabled = false;
        }

        /// <summary>Lugar da frente do disco <paramref name="i"/> (onde o escolhido para).</summary>
        private Vector3 Front(int i) => _spots[i] + new Vector3(0, 0, StepForward);
        private static Vector3 CenterFront => Origin + new Vector3(0, 0.04f, StepForward);

        private void PlaceCamera(bool snap)
        {
            // close no herói: ~60% da altura da tela; os vizinhos ficam nas laterais, mais atrás
            var want = _focus + new Vector3(0, 1.75f, 6.5f);
            var look = _focus + new Vector3(0, 1.12f, 0);
            float k = snap ? 1f : 1f - Mathf.Exp(-3.2f * Time.deltaTime);
            Cam.transform.position = Vector3.Lerp(Cam.transform.position, want, k);
            Cam.transform.rotation = Quaternion.Slerp(Cam.transform.rotation, Quaternion.LookRotation(look - Cam.transform.position), k);
        }

        /// <summary>Luz noturna: luar azulado no céu, luz quente vinda dos braseiros da frente, névoa azul escura.</summary>
        private static void NightLight(Camera cam)
        {
            DetailTextures.Ensure();
            Shader.SetGlobalVector("_KezorLowFog", Vector4.zero);
            Shader.SetGlobalVector("_KezorSunDir", (Quaternion.Euler(28f, 160f, 0) * Vector3.forward).normalized);
            Shader.SetGlobalColor("_KezorSunColor", new Color(0.55f, 0.62f, 0.9f) * 0.42f);   // luar fraco; o calor vem dos braseiros
            Shader.SetGlobalColor("_KezorSkyColor", new Color(0.12f, 0.15f, 0.26f));
            Shader.SetGlobalColor("_KezorGroundColor", new Color(0.06f, 0.05f, 0.05f));
            Shader.SetGlobalColor("_KezorFogColor", new Color(0.02f, 0.025f, 0.05f));
            Shader.SetGlobalVector("_KezorFogParams", new Vector4(20f, 46f, 0.9f, 0));
            KezorUrp.SetSun(Quaternion.Euler(28f, 160f, 0) * Vector3.forward, new Color(0.55f, 0.62f, 0.9f) * 0.42f, 0.6f);
            KezorUrp.EnsurePost(cam);
            cam.backgroundColor = new Color(0.02f, 0.025f, 0.05f);
        }

        /// <summary>Liga o palco no modo pedido (seleção ou criação) ou devolve a câmera do mundo.</summary>
        public void SetMode(Mode mode, int mapId)
        {
            if (mode == _mode) return;
            var was = _mode;
            _mode = mode;
            bool on = mode != Mode.Off;
            Cam.enabled = on;
            if (_main != null) _main.enabled = !on;
            if (on && was == Mode.Off) NightLight(Cam);
            else if (!on && _main != null) KezorLook.ApplyEnvironment(_main, mapId);
            foreach (var h in _heroes) if (h != null) h.Rig.Root.gameObject.SetActive(mode == Mode.Select);
            if (_sample != null) _sample.Rig.Root.gameObject.SetActive(mode == Mode.Create);
            DragYaw = 0;
            if (mode == Mode.Create) { _focus = CenterFront; if (_sample != null) _sample.Posed = false; }
            else if (mode == Mode.Select && _selected >= 0 && _heroes[_selected] != null) _heroes[_selected]!.Posed = false;
            if (on) PlaceCamera(true);
        }

        private Hero MakeHero(string name, CharacterClass cls, Appearance look, Vector3 pos)
        {
            var rig = Gear.Player(name, cls, look);
            rig.Root.SetParent(transform, true);
            rig.Root.SetPositionAndRotation(pos, Quaternion.identity);   // de frente para a câmera
            var view = EntityView.Create(rig, -1, EntityKind.Player, pos);
            view.WeaponId = look[EquipSlot.RightHand].Id;
            return new Hero { Rig = rig, View = view, Pos = pos };
        }

        /// <summary>Monta (ou mantém) os heróis da conta nos 5 lugares e marca o escolhido.</summary>
        internal void Show(IReadOnlyList<KezorGame.CharSummary> rows, int selected)
        {
            for (int i = 0; i < 5; i++)
            {
                var row = i < rows.Count ? rows[i] : null;
                string key = row == null ? "" : row.Name + "|" + row.Cls + "|" + row.Look;
                if (key == _keys[i]) continue;
                _keys[i] = key;
                if (_heroes[i] != null) Destroy(_heroes[i]!.Rig.Root.gameObject);
                _heroes[i] = null;
                if (row == null) continue;
                var look = row.Look != "" ? Appearance.Parse(row.Look) : new Appearance();
                _heroes[i] = MakeHero(row.Name, (CharacterClass)row.Cls, look, _spots[i]);
                _heroes[i]!.Rig.Root.gameObject.SetActive(_mode == Mode.Select);
            }
            int sel = selected >= 0 && selected < 5 && _heroes[selected] != null ? selected : -1;
            if (sel != _selected)
            {
                _selected = sel;
                DragYaw = 0;
                if (sel >= 0) _heroes[sel]!.Posed = false;
            }
        }

        /// <summary>Herói de amostra da criação. Com <paramref name="showcase"/>, veste o conjunto e a arma épica da classe (+9).</summary>
        public void ShowSample(CharacterClass cls, bool showcase)
        {
            string key = cls + ":" + showcase;
            if (key == _sampleKey && _sample != null) return;
            _sampleKey = key;
            if (_sample != null) Destroy(_sample.Rig.Root.gameObject);
            _sample = MakeHero("amostra", cls, SampleLook(cls, showcase), CenterFront);
            _sample.Rig.Root.gameObject.SetActive(_mode == Mode.Create);
            DragYaw = 0;
        }

        private static Appearance SampleLook(CharacterClass cls, bool showcase)
        {
            var app = new Appearance();
            var mask = ClassDefinition.MaskOf(cls);
            void Put(EquipSlot slot, ItemDefinition? d, int level)
            {
                if (d == null) return;
                int i = System.Array.IndexOf(Appearance.Slots, slot);
                if (i >= 0) app.Pieces[i] = new Appearance.Piece { Id = d.Id, Level = level };
            }
            var weapons = ItemDatabase.All.Where(d => d.Slot == EquipSlot.RightHand && (d.AllowedClasses & mask) != 0 && d.AllowedClasses != ClassMask.All).ToList();
            if (weapons.Count == 0) weapons = ItemDatabase.All.Where(d => d.Slot == EquipSlot.RightHand && (d.AllowedClasses & mask) != 0).ToList();
            if (showcase)
            {
                Put(EquipSlot.RightHand, weapons.FirstOrDefault(d => d.Epic) ?? weapons.OrderByDescending(d => d.MaxDamage).FirstOrDefault(), 9);
                var set = ItemDatabase.All.Where(d => d.Set != ArmorSet.None && (d.AllowedClasses & mask) != 0 && d.AllowedClasses != ClassMask.All)
                                          .GroupBy(d => d.Set).OrderByDescending(g => g.Max(x => x.Defense)).FirstOrDefault();
                if (set != null) foreach (var d in set) Put(d.Slot, d, 9);
                Put(EquipSlot.Wings, ItemDatabase.All.FirstOrDefault(d => d.IsWings && d.Id == 301), 7);
            }
            else Put(EquipSlot.RightHand, weapons.OrderBy(d => d.ReqLevel).ThenBy(d => d.MaxDamage).FirstOrDefault(), 0);
            return app;
        }

        /// <summary>Herói <paramref name="i"/> existe na fila.</summary>
        public bool Has(int i) => i >= 0 && i < 5 && _heroes[i] != null;

        /// <summary>Retângulo do herói (ou do disco vazio) no painel, para a placa e a área de clique.</summary>
        public Rect? HeroRect(int i, IPanel panel)
        {
            var bottom = _heroes[i]?.Pos ?? _spots[i];
            var top = bottom + Vector3.up * HeroHeight;
            var a = Cam.WorldToScreenPoint(bottom); var b = Cam.WorldToScreenPoint(top);
            if (a.z < 0) return null;
            var pa = RuntimePanelUtils.ScreenToPanel(panel, new Vector2(a.x, Screen.height - a.y));
            var pb = RuntimePanelUtils.ScreenToPanel(panel, new Vector2(b.x, Screen.height - b.y));
            float h = pa.y - pb.y, w = h * 0.5f;
            return new Rect(pa.x - w / 2, pb.y, w, h);
        }

        private readonly List<(Vector3 pos, Color col, float range)> _fireLights = new List<(Vector3, Color, float)>();
        private readonly List<(Vector3 pos, Color col, float range)> _lights = new List<(Vector3, Color, float)>();

        private void LateUpdate()
        {
            if (_mode == Mode.Off) return;
            float t = Time.time, dt = Time.deltaTime;
            foreach (var (f, ph) in _flames)
            {   // fogo tremulando
                float k = 1f + Mathf.Sin(t * 9f + ph) * 0.08f + Mathf.Sin(t * 23f + ph * 2f) * 0.05f;
                f.localScale = new Vector3(1f + Mathf.Sin(t * 7f + ph) * 0.06f, k, 1f + Mathf.Cos(t * 6f + ph) * 0.06f);
            }
            if (Time.unscaledTime - _lastDrag > 2.5f) DragYaw = Mathf.MoveTowardsAngle(DragYaw, 0f, 40f * dt);   // volta a olhar para a câmera

            Hero? star = null;
            if (_mode == Mode.Select)
            {
                for (int i = 0; i < 5; i++)
                {
                    var h = _heroes[i];
                    if (h == null) continue;
                    Walk(h, i == _selected ? Front(i) : _spots[i], i == _selected, dt);
                }
                if (_selected >= 0) star = _heroes[_selected];
                _focus = Vector3.Lerp(_focus, _selected >= 0 ? Front(_selected) : CenterFront, 1f - Mathf.Exp(-5f * dt));
            }
            else if (_sample != null) { Walk(_sample, CenterFront, true, dt); star = _sample; _focus = CenterFront; }

            _ring.gameObject.SetActive(star != null);
            if (star != null) _ring.position = star.Pos;
            // luz de destaque no escolhido: quente, de frente e um pouco acima
            _lights.Clear();
            _lights.AddRange(_fireLights);
            if (star != null) _lights.Add((star.Pos + new Vector3(0, 2.4f, 2.2f), new Color(1f, 0.78f, 0.55f) * 1.25f, 6.5f));
            LocalLights.UploadSet(_lights);
            PlaceCamera(false);
        }

        /// <summary>
        /// Leva o herói até o lugar dele andando (o EntityView toca a caminhada), virado para onde anda; parado, olha para a
        /// câmera (o escolhido com o giro do arraste). Ao chegar na frente, o escolhido faz uma pose de golpe.
        /// </summary>
        private void Walk(Hero h, Vector3 target, bool star, float dt)
        {
            var prev = h.Pos;
            h.Pos = Vector3.MoveTowards(h.Pos, target, WalkSpeed * dt);
            h.View.Target = h.Pos;
            var move = h.Pos - prev; move.y = 0;
            var root = h.Rig.Root;
            Quaternion want;
            if (move.sqrMagnitude > 1e-7f) want = Quaternion.LookRotation(move);
            else
            {
                var toCam = Cam.transform.position - h.Pos; toCam.y = 0;
                want = Quaternion.LookRotation(toCam.sqrMagnitude > 0.01f ? toCam : Vector3.forward) * Quaternion.Euler(0, star ? DragYaw : 0f, 0);
            }
            root.rotation = Quaternion.Slerp(root.rotation, want, 1f - Mathf.Exp(-7f * dt));
            float s = star ? 1.04f : 0.96f;
            root.localScale = Vector3.Lerp(root.localScale, Vector3.one * s, 1f - Mathf.Exp(-6f * dt));
            if (star && !h.Posed && (h.Pos - target).sqrMagnitude < 0.0004f && Quaternion.Angle(root.rotation, want) < 12f)
            {
                h.Posed = true;
                h.View.PlayAttack(Cam.transform.position);   // pose de entrada: golpe (ou magia) virado para a câmera
            }
        }
    }

    /// <summary>
    /// Interface da seleção: o herói escolhido grande no centro com a ficha ao lado (nome, classe, nível, renascimentos,
    /// guilda e cargo), setas e ←/→ para trocar, os 5 lugares numa faixa embaixo ("Vazio" abre a criação), placas pequenas
    /// sobre os vizinhos; clique escolhe, duplo clique ou Enter entra, arrastar gira o herói. Criar e Menu à esquerda,
    /// Apagar e Entrar à direita (apagar pede o nome digitado). Funciona igual nos modos local, LAN e servidor.
    /// </summary>
    public sealed class SelectScreen
    {
        private readonly UiRoot _ui;
        private KezorGame G => _ui.Game;
        private readonly SelectStage _stage;
        public readonly VisualElement Root;
        private readonly VisualElement[] _zones = new VisualElement[5];
        private readonly VisualElement[] _plates = new VisualElement[5];
        private readonly Label[] _pName = new Label[5], _pInfo = new Label[5];
        private readonly VisualElement[] _slots = new VisualElement[5], _slotIcons = new VisualElement[5];
        private readonly Label[] _sName = new Label[5], _sInfo = new Label[5];
        private readonly int[] _slotIconCls = { -1, -1, -1, -1, -1 };
        private readonly VisualElement _card, _arrowL, _arrowR;
        private readonly Label _cName, _cClass, _cLevel, _cRebirth, _cGuild, _cLore, _cSlot;
        private readonly Label _title, _sub, _error;
        private readonly Button _enter, _delete, _create;
        private readonly VisualElement _modal, _menu;
        private readonly Label _modalText;
        private readonly TextField _modalField;
        private int _sel, _lastCount = -1;
        private bool _dragging; private float _lastX, _dragDist;

        private static readonly string[] Ranks = { "Membro", "Oficial", "Líder" };

        public SelectScreen(UiRoot ui)
        {
            _ui = ui;
            _stage = SelectStage.Get(ui.Game.Cam);
            Root = Kz.El("sel", ui.Root, pick: false);
            Kz.El("sel__vignette", Root);
            var drag = Kz.El("sel__drag", Root, pick: true);
            HookDrag(drag);

            // cabeçalho
            var head = Kz.El("sel__head", Root);
            _title = Kz.Text("Escolha seu herói", "kz-title sel__title", head);
            Kz.El("kz-divider sel__head-div", head);
            _sub = Kz.Text("", "sel__subtitle", head);

            // placas e áreas de clique sobre os heróis da fila
            for (int i = 0; i < 5; i++)
            {
                int idx = i;
                var z = Kz.El("sel__zone", Root, pick: true);
                z.RegisterCallback<ClickEvent>(e => { if (_dragDist > 8f) return; Pick(idx, e.clickCount); });   // arraste não conta como clique
                HookDrag(z);
                _zones[i] = z;
                var p = Kz.El("sel__plate", Root, pick: true);
                p.RegisterCallback<ClickEvent>(e => Pick(idx, e.clickCount));
                _pName[i] = Kz.Text("", "sel__name", p);
                _pInfo[i] = Kz.Text("", "sel__info", p);
                _plates[i] = p;
            }

            // setas para alternar
            _arrowL = Kz.El("sel__arrow sel__arrow--l", Root, pick: true);
            Kz.El("sel__chev sel__chev--l", _arrowL);
            _arrowL.RegisterCallback<ClickEvent>(_ => Cycle(-1));
            _arrowR = Kz.El("sel__arrow sel__arrow--r", Root, pick: true);
            Kz.El("sel__chev sel__chev--r", _arrowR);
            _arrowR.RegisterCallback<ClickEvent>(_ => Cycle(1));

            // ficha do herói escolhido
            _card = Kz.El("kz-panel sel__card", Root, pick: true);
            _cSlot = Kz.Text("", "sel__card-slot", _card);
            _cName = Kz.Text("", "kz-title sel__card-name", _card);
            _cClass = Kz.Text("", "kz-gold sel__card-class", _card);
            Kz.El("kz-divider", _card);
            _cLevel = Row("Nível");
            _cRebirth = Row("Renascimentos");
            _cGuild = Row("Guilda");
            Kz.El("kz-divider", _card);
            _cLore = Kz.Text("", "kz-text-2 sel__card-lore", _card);
            Label Row(string name)
            {
                var r = Kz.El("sel__card-row", _card);
                Kz.Text(name, "kz-text-2 sel__card-key", r);
                return Kz.Text("", "kz-text sel__card-val", r);
            }

            // faixa de baixo: 5 lugares + botões nos cantos
            Kz.El("sel__bar", Root);
            var slots = Kz.El("sel__slots", Root);
            for (int i = 0; i < 5; i++)
            {
                int idx = i;
                var s = Kz.El("sel__slot", slots, pick: true);
                _slotIcons[i] = Kz.El("sel__slot-icon", s);
                var col = Kz.El("sel__slot-text", s);
                _sName[i] = Kz.Text("", "sel__slot-name", col);
                _sInfo[i] = Kz.Text("", "sel__slot-info", col);
                s.RegisterCallback<ClickEvent>(e => Pick(idx, e.clickCount));
                _slots[i] = s;
            }
            var left = Kz.El("sel__corner sel__corner--left", Root, pick: true);
            _create = Kz.Btn("Criar herói", OpenCreate, "kz-btn sel__btn", left);
            Kz.Btn("Menu", () => _menu.Show(_menu.resolvedStyle.display == DisplayStyle.None), "kz-btn sel__btn sel__btn--small", left);
            var right = Kz.El("sel__corner sel__corner--right", Root, pick: true);
            _delete = Kz.Btn("Apagar", OpenDelete, "kz-btn sel__btn sel__btn--small", right);
            _enter = Kz.Btn("Entrar", Enter, "kz-btn kz-btn--primary sel__btn sel__btn--big", right);
            Kz.Text("← → trocam de herói  ·  arraste para girar  ·  duplo clique ou Enter para entrar", "sel__hint", Root);
            _error = Kz.Text("", "sel__error", Root);

            _menu = Kz.El("kz-panel sel__menu", Root, pick: true);
            Kz.Text("Menu", "kz-h2", _menu);
            Kz.Btn("Opções", () => { _menu.Show(false); ToggleOptionsOverlay(); }, "kz-btn", _menu);
            Kz.Btn("Sair da conta", () => { _menu.Show(false); G.BackToServerList(); }, "kz-btn", _menu);
            Kz.Btn("Fechar o jogo", Application.Quit, "kz-btn", _menu);
            _menu.Show(false);

            _modal = Kz.El("sel__modalbg", Root, pick: true);
            var box = Kz.El("kz-panel sel__modal", _modal, pick: true);
            Kz.Text("Apagar herói", "kz-title", box);
            _modalText = Kz.Text("", "kz-text", box);
            _modalField = new TextField { maxLength = 12 };
            _modalField.AddToClassList("kz-field");
            _modalField.textEdition.placeholder = "digite o nome exato";
            box.Add(_modalField);
            var row = Kz.El("win-row", box, pick: true);
            Kz.Btn("Cancelar", () => _modal.Show(false), "kz-btn", row);
            Kz.Btn("Apagar para sempre", ConfirmDelete, "kz-btn kz-btn--primary", row);
            _modal.Show(false);
        }

        /// <summary>Arrastar na horizontal gira o herói escolhido (um clique curto continua sendo clique).</summary>
        private void HookDrag(VisualElement el)
        {
            el.RegisterCallback<PointerDownEvent>(e => { if (e.button != 0) return; _dragging = true; _dragDist = 0; _lastX = e.position.x; el.CapturePointer(e.pointerId); });
            el.RegisterCallback<PointerMoveEvent>(e =>
            {
                if (!_dragging || !el.HasPointerCapture(e.pointerId)) return;
                float dx = e.position.x - _lastX; _lastX = e.position.x; _dragDist += Mathf.Abs(dx);
                _stage.Drag(-dx * 0.6f);
            });
            el.RegisterCallback<PointerUpEvent>(e => { _dragging = false; if (el.HasPointerCapture(e.pointerId)) el.ReleasePointer(e.pointerId); });
        }

        /// <summary>Clique num herói ou lugar: escolhe (duplo clique entra); lugar vazio abre a criação.</summary>
        private void Pick(int idx, int clicks)
        {
            if (idx < G._charRows.Count) { _sel = idx; if (clicks >= 2) Enter(); }
            else OpenCreate();
        }

        private void Cycle(int dir)
        {
            int n = G._charRows.Count;
            if (n <= 1) return;
            _sel = ((_sel < 0 ? 0 : _sel) + dir + n) % n;   // dá a volta: do último volta ao primeiro
        }

        private void OpenCreate()
        {
            if (G._charRows.Count >= 5) return;
            G._loginError = "";
            G._loginStage = KezorGame.LoginStage.Create;
        }

        private void ToggleOptionsOverlay() => G.Toggle(KezorGame.UiPanel.Options);

        private void Enter()
        {
            if (_sel < 0 || _sel >= G._charRows.Count) return;
            var name = G._charRows[_sel].Name;
            G._loginError = "";
            G.Send($"PLAY {name}", rep => { if (rep.StartsWith("ERR")) G._loginError = rep.Substring(4); else G._loginStage = KezorGame.LoginStage.Form; });
        }

        private void OpenDelete()
        {
            if (_sel < 0 || _sel >= G._charRows.Count) return;
            _modalText.Set($"Isto apaga <b>{G._charRows[_sel].Name}</b> para sempre, com todo o equipamento e a mochila.\nPara confirmar, digite o nome do herói:");
            _modalField.value = "";
            _modal.Show(true);
            _modalField.schedule.Execute(() => _modalField.Focus());
        }

        private void ConfirmDelete()
        {
            if (_sel < 0 || _sel >= G._charRows.Count) return;
            var name = G._charRows[_sel].Name;
            G.Send($"DELCHAR {name} {_modalField.value.Trim()}", rep =>
            {
                if (rep.StartsWith("ERR")) { G._loginError = rep.Substring(4); return; }
                _modal.Show(false); _sel = 0; G._loginError = "";
                G.RequestCharList();
            });
        }

        /// <summary>Ícone da classe: a primeira skill dela (arte gerada ou o ícone desenhado em código).</summary>
        internal static void ClassIcon(VisualElement icon, CharacterClass cls)
        {
            var first = SkillCatalog.All.FirstOrDefault(s => s.Id != 0 && (s.Classes & ClassDefinition.MaskOf(cls)) != 0);
            if (first == null) { icon.style.backgroundImage = StyleKeyword.None; return; }
            var sp = ItemIcons.Skill(first.Id);
            icon.style.backgroundImage = sp != null ? new StyleBackground(sp) : new StyleBackground(SkillIcons.Get(first));
        }

        public bool Visible => !G.InWorld && G.Connected && G._loginStage == KezorGame.LoginStage.Characters;
        private bool Creating => !G.InWorld && G.Connected && !G._connecting && G._loginStage == KezorGame.LoginStage.Create;

        public void Tick()
        {
            bool on = Visible;
            Root.Show(on);
            _stage.SetMode(on ? SelectStage.Mode.Select : Creating ? SelectStage.Mode.Create : SelectStage.Mode.Off, G.Map.Id);
            if (!on) return;
            var rows = G._charRows;
            if (!_modal.visible || _modal.resolvedStyle.display == DisplayStyle.None)
            {
                if (InputK.KeyDown(KeyCode.LeftArrow) || InputK.KeyDown(KeyCode.A)) Cycle(-1);
                if (InputK.KeyDown(KeyCode.RightArrow) || InputK.KeyDown(KeyCode.D)) Cycle(1);
                if (Event.current == null && (InputK.KeyDown(KeyCode.Return) || InputK.KeyDown(KeyCode.KeypadEnter))) Enter();
            }
            if (_lastCount >= 0 && rows.Count == _lastCount + 1) _sel = rows.Count - 1;   // herói recém-criado já vem escolhido
            _lastCount = rows.Count;
            _sel = rows.Count == 0 ? -1 : Mathf.Clamp(_sel < 0 ? 0 : _sel, 0, rows.Count - 1);
            _stage.Show(rows, _sel);
            var panel = _ui.Root.panel;
            for (int i = 0; i < 5; i++)
            {
                bool has = i < rows.Count;
                var rect = panel != null ? _stage.HeroRect(i, panel) : null;
                bool vis = rect != null && rect.Value.xMax > 0 && rect.Value.xMin < Root.resolvedStyle.width;
                _zones[i].Show(vis);
                _plates[i].Show(vis && i != _sel);   // o escolhido tem a ficha ao lado; os vizinhos, uma plaquinha
                if (vis)
                {
                    var r = rect!.Value;
                    _zones[i].style.left = r.x; _zones[i].style.top = r.y; _zones[i].style.width = r.width; _zones[i].style.height = r.height;
                    _plates[i].style.left = r.center.x - 70; _plates[i].style.top = r.y - 40;
                    _plates[i].SetClass("sel__plate--empty", !has);
                }

                // faixa dos lugares
                _slots[i].SetClass("sel__slot--on", i == _sel && has);
                _slots[i].SetClass("sel__slot--empty", !has);
                if (has)
                {
                    var c = rows[i];
                    var def = ClassDefinition.Get((CharacterClass)c.Cls);
                    _pName[i].Set(c.Name);
                    _pInfo[i].Set($"{def.DisplayName} · Nv {c.Level}");
                    _sName[i].Set(c.Name);
                    _sInfo[i].Set($"{def.DisplayName}  Nv {c.Level}");
                    if (_slotIconCls[i] != c.Cls) { _slotIconCls[i] = c.Cls; ClassIcon(_slotIcons[i], (CharacterClass)c.Cls); }
                }
                else
                {
                    _pName[i].Set("Vazio");
                    _pInfo[i].Set("clique para criar");
                    _sName[i].Set("Vazio");
                    _sInfo[i].Set(rows.Count < 5 ? "Criar herói" : "");
                    if (_slotIconCls[i] != -1) { _slotIconCls[i] = -1; _slotIcons[i].style.backgroundImage = StyleKeyword.None; }
                }
            }

            // ficha
            _card.Show(_sel >= 0);
            if (_sel >= 0)
            {
                var c = rows[_sel];
                var def = ClassDefinition.Get((CharacterClass)c.Cls);
                _cSlot.Set($"Herói {_sel + 1} de {rows.Count}");
                _cName.Set(c.Name);
                _cClass.Set(def.DisplayName + (def.Role != "" ? $"  ·  {def.Role}" : ""));
                _cLevel.Set(c.Level.ToString());
                _cRebirth.Set(c.Rebirths.ToString());
                _cGuild.Set(c.Guild != "" ? $"{c.Guild} ({Ranks[Mathf.Clamp(c.Rank, 0, 2)]})" : "<color=#7d7366>sem guilda</color>");
                _cLore.Set(def.Lore);
            }
            _arrowL.Show(rows.Count > 1);
            _arrowR.Show(rows.Count > 1);
            _enter.SetEnabled(_sel >= 0);
            _delete.SetEnabled(_sel >= 0);
            _create.SetEnabled(rows.Count < 5);
            _title.Set("Escolha seu herói");
            _sub.Set($"Conta {G._account}{(G._serverLabel != "" ? "  ·  " + G._serverLabel : "")}  ·  {rows.Count} de 5 heróis");
            _error.Set(G._loginError != "" ? $"<color=#ff7a6a>{G._loginError}</color>" : (G._awaitingList ? "Carregando..." : rows.Count == 0 ? "Nenhum herói ainda: clique em <color=#ffb347>Criar herói</color>." : ""));
        }
    }
}
