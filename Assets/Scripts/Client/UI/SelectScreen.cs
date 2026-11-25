using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Kezor.Core.Characters;
using Kezor.Core.Items;

namespace Kezor.Client.UI
{
    /// <summary>
    /// Palco 3D da tela de seleção: pátio noturno do Kezor (Resources/Models/Environment/SelecaoPatio) longe do mundo,
    /// câmera fixa frontal levemente de cima, braseiros com fogo tremulando e faíscas, até 5 heróis da conta em fila com
    /// o modelo real (equipamento, asas, brilho de refino) e respiração; o selecionado ganha anel de brasa e a câmera
    /// chega um pouco mais perto dele.
    /// </summary>
    public sealed class SelectStage : MonoBehaviour
    {
        public static readonly Vector3 Origin = new Vector3(0, -900f, 0);
        public Camera Cam { get; private set; } = null!;
        private readonly Vector3[] _spots = new Vector3[5];
        private readonly List<(Transform t, float phase)> _flames = new List<(Transform, float)>();
        private readonly Rig?[] _heroes = new Rig?[5];
        private readonly string[] _keys = new string[5];
        private Transform _ring = null!;
        private int _selected = -1;
        private bool _active;
        private Camera? _main;

        public static SelectStage Create(Camera main)
        {
            var go = new GameObject("PalcoSelecao");
            DontDestroyOnLoad(go);
            var s = go.AddComponent<SelectStage>();
            s._main = main;
            s.Build();
            return s;
        }

        /// <summary>Topo dos braseiros em pilares (kezor_select.py BRAZIERS; Blender x,y,z → Unity x,z,-y).</summary>
        private static readonly Vector3[] FirePos =
        {
            new Vector3(-7.6f, 1.5f, 0.6f), new Vector3(7.6f, 1.5f, 0.6f), new Vector3(-3.9f, 2.7f, -2.6f),
            new Vector3(3.9f, 2.7f, -2.6f), new Vector3(-1.6f, 3.7f, -4.4f), new Vector3(1.6f, 3.7f, -4.4f),
        };
        /// <summary>Passo entre os heróis na fila (igual aos discos de bronze da cena).</summary>
        public const float Spacing = 2.6f;
        /// <summary>Heróis um pouco menores que no jogo: cabem inteiros, com asas, no plano aberto (~18% da altura).</summary>
        public const float HeroScale = 0.86f;

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
            // anel de brasa do selecionado
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
            var ring = KezorLook.MakeObject("AnelSelecao", mb.ToMesh(), transform);
            ring.AddComponent<Spin>().Speed = 30f;
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
            PlaceCamera(0f, true);
            Cam.enabled = false;
        }

        private void PlaceCamera(float focusX, bool snap)
        {
            // plano aberto: heróis pequenos (~20% da altura) na faixa de baixo, cenário grande em volta
            var want = Origin + new Vector3(focusX * 0.15f, 3.3f, 20.5f - (_selected >= 0 ? 0.8f : 0f));
            var look = Origin + new Vector3(focusX * 0.15f, 2.7f, -2f);
            Cam.transform.position = snap ? want : Vector3.Lerp(Cam.transform.position, want, 1f - Mathf.Exp(-4f * Time.deltaTime));
            Cam.transform.rotation = Quaternion.Slerp(Cam.transform.rotation, Quaternion.LookRotation(look - Cam.transform.position), snap ? 1f : 1f - Mathf.Exp(-4f * Time.deltaTime));
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

        public void SetActive(bool on, int mapId)
        {
            if (on == _active) return;
            _active = on;
            Cam.enabled = on;
            if (_main != null) _main.enabled = !on;
            if (on) NightLight(Cam);
            else if (_main != null) KezorLook.ApplyEnvironment(_main, mapId);
        }

        /// <summary>Monta (ou mantém) os heróis da conta nos 5 lugares.</summary>
        internal void Show(IReadOnlyList<KezorGame.CharSummary> rows, int selected)
        {
            for (int i = 0; i < 5; i++)
            {
                var row = i < rows.Count ? rows[i] : null;
                string key = row == null ? "" : row.Name + "|" + row.Cls + "|" + row.Look;
                if (key == _keys[i]) continue;
                _keys[i] = key;
                if (_heroes[i] != null) Destroy(_heroes[i]!.Root.gameObject);
                _heroes[i] = null;
                if (row == null) continue;
                var look = row.Look != "" ? Appearance.Parse(row.Look) : new Appearance();
                var rig = Gear.Player(row.Name, (CharacterClass)row.Cls, look);
                rig.Root.SetParent(transform, true);
                rig.Root.position = _spots[i];
                rig.Root.rotation = Quaternion.identity;               // de frente para a câmera
                _heroes[i] = rig;
            }
            _selected = selected >= 0 && selected < 5 && _heroes[selected] != null ? selected : -1;
            _ring.gameObject.SetActive(_selected >= 0);
            if (_selected >= 0) _ring.position = _spots[_selected];
        }

        /// <summary>Retângulo do herói no painel (para a placa e a área de clique).</summary>
        public Rect? HeroRect(int i, IPanel panel)
        {
            var bottom = _spots[i]; var top = _spots[i] + Vector3.up * (2.35f * HeroScale);
            var a = Cam.WorldToScreenPoint(bottom); var b = Cam.WorldToScreenPoint(top);
            if (a.z < 0) return null;
            var pa = RuntimePanelUtils.ScreenToPanel(panel, new Vector2(a.x, Screen.height - a.y));
            var pb = RuntimePanelUtils.ScreenToPanel(panel, new Vector2(b.x, Screen.height - b.y));
            float h = pa.y - pb.y, w = h * 0.55f;
            return new Rect(pa.x - w / 2, pb.y, w, h);
        }

        private readonly List<(Vector3 pos, Color col, float range)> _fireLights = new List<(Vector3, Color, float)>();

        private void LateUpdate()
        {
            if (!_active) return;
            LocalLights.UploadSet(_fireLights);
            float t = Time.time;
            foreach (var (f, ph) in _flames)
            {   // fogo tremulando
                float k = 1f + Mathf.Sin(t * 9f + ph) * 0.08f + Mathf.Sin(t * 23f + ph * 2f) * 0.05f;
                f.localScale = new Vector3(1f + Mathf.Sin(t * 7f + ph) * 0.06f, k, 1f + Mathf.Cos(t * 6f + ph) * 0.06f);
            }
            for (int i = 0; i < 5; i++)
            {
                var r = _heroes[i];
                if (r == null) continue;
                float p = t * 1.5f + i * 1.3f;
                if (r.Body != null) r.Body.localPosition = r.BodyBase + new Vector3(0, Mathf.Sin(p) * 0.012f, 0);
                if (r.ArmL != null) r.ArmL.localRotation = Quaternion.Euler(Mathf.Sin(p) * 2.5f, 0, 4f);
                if (r.ArmR != null) r.ArmR.localRotation = Quaternion.Euler(-Mathf.Sin(p) * 2.5f, 0, -4f);
                if (r.WingL != null && r.WingR != null)
                {
                    float f = Mathf.Sin(t * 2.2f + i) * 9f;
                    r.WingL.localRotation = Quaternion.Euler(0, -18f, f + 6f);
                    r.WingR.localRotation = Quaternion.Euler(0, 18f, -f - 6f);
                }
                float s = HeroScale * (i == _selected ? 1.05f : 1f);
                r.Root.localScale = Vector3.Lerp(r.Root.localScale, Vector3.one * s, 1f - Mathf.Exp(-8f * Time.deltaTime));
            }
            PlaceCamera(_selected >= 0 ? _spots[_selected].x - Origin.x : 0f, false);
        }
    }

    /// <summary>
    /// Interface da seleção: placa acima de cada herói (nome, guilda e cargo, classe, nível, renascimentos; "Vazio" nos
    /// lugares livres), clique seleciona e duplo clique entra; Criar e Menu à esquerda, Entrar e Apagar à direita
    /// (apagar pede o nome digitado). Funciona igual nos modos local, LAN e servidor.
    /// </summary>
    public sealed class SelectScreen
    {
        private readonly UiRoot _ui;
        private KezorGame G => _ui.Game;
        private readonly SelectStage _stage;
        public readonly VisualElement Root;
        private readonly VisualElement[] _zones = new VisualElement[5];
        private readonly VisualElement[] _plates = new VisualElement[5];
        private readonly Label[] _pName = new Label[5], _pSub = new Label[5], _pInfo = new Label[5];
        private readonly Label _title, _error;
        private readonly Button _enter, _delete, _create;
        private readonly VisualElement _modal, _menu;
        private readonly Label _modalText;
        private readonly TextField _modalField;
        private int _sel;

        private static readonly string[] Ranks = { "Membro", "Oficial", "Líder" };

        public SelectScreen(UiRoot ui)
        {
            _ui = ui;
            _stage = SelectStage.Create(ui.Game.Cam);
            Root = Kz.El("sel", ui.Root, pick: false);
            Kz.El("sel__vignette", Root);
            _title = Kz.Text("Escolha seu herói", "kz-title sel__title", Root);
            for (int i = 0; i < 5; i++)
            {
                int idx = i;
                var z = Kz.El("sel__zone", Root, pick: true);
                z.RegisterCallback<ClickEvent>(e => { if (idx < G._charRows.Count) { _sel = idx; if (e.clickCount >= 2) Enter(); } else G._loginStage = KezorGame.LoginStage.Create; });
                _zones[i] = z;
                var p = Kz.El("kz-panel-dark sel__plate", Root, pick: true);
                p.RegisterCallback<ClickEvent>(e => { if (idx < G._charRows.Count) { _sel = idx; if (e.clickCount >= 2) Enter(); } else G._loginStage = KezorGame.LoginStage.Create; });
                _pName[i] = Kz.Text("", "sel__name", p);
                _pSub[i] = Kz.Text("", "sel__sub", p);
                _pInfo[i] = Kz.Text("", "sel__info", p);
                _plates[i] = p;
            }
            Kz.El("sel__bar", Root);   // faixa escura fina na base, com os botões pequenos nos cantos
            var left = Kz.El("sel__corner sel__corner--left", Root, pick: true);
            _create = Kz.Btn("Criar", () => G._loginStage = KezorGame.LoginStage.Create, "kz-btn sel__btn", left);
            Kz.Btn("Menu", () => _menu.Show(!_menu.style.display.Equals(DisplayStyle.Flex) || _menu.resolvedStyle.display == DisplayStyle.None), "kz-btn sel__btn", left);
            var right = Kz.El("sel__corner sel__corner--right", Root, pick: true);
            _delete = Kz.Btn("Apagar", OpenDelete, "kz-btn sel__btn", right);
            _enter = Kz.Btn("Entrar", Enter, "kz-btn kz-btn--primary sel__btn sel__btn--big", right);
            _error = Kz.Text("", "sel__error", Root);

            _menu = Kz.El("kz-panel sel__menu", Root, pick: true);
            Kz.Text("Menu", "kz-h2", _menu);
            Kz.Btn("Opções", () => { _menu.Show(false); ToggleOptionsOverlay(); }, "kz-btn", _menu);
            Kz.Btn("Sair da conta", () => { _menu.Show(false); G.Disconnect(); G._loginStage = KezorGame.LoginStage.Form; }, "kz-btn", _menu);
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

        public bool Visible => !G.InWorld && G.Connected && G._loginStage == KezorGame.LoginStage.Characters;

        public void Tick()
        {
            bool on = Visible;
            Root.Show(on);
            _stage.SetActive(on, G.Map.Id);
            if (!on) return;
            var rows = G._charRows;
            _sel = rows.Count == 0 ? -1 : Mathf.Clamp(_sel < 0 ? 0 : _sel, 0, rows.Count - 1);
            _stage.Show(rows, _sel);
            var panel = _ui.Root.panel;
            for (int i = 0; i < 5; i++)
            {
                var rect = panel != null ? _stage.HeroRect(i, panel) : null;
                bool has = i < rows.Count;
                _zones[i].Show(rect != null);
                _plates[i].Show(rect != null);
                if (rect == null) continue;
                var r = rect.Value;
                _zones[i].style.left = r.x; _zones[i].style.top = r.y; _zones[i].style.width = r.width; _zones[i].style.height = r.height;
                _plates[i].style.left = r.center.x - 78; _plates[i].style.top = r.y - 82;   // placa compacta logo acima da cabeça
                _plates[i].SetClass("sel__plate--on", i == _sel && has);
                _plates[i].SetClass("sel__plate--empty", !has);
                if (has)
                {
                    var c = rows[i];
                    _pName[i].Set(c.Name);
                    _pSub[i].Set(c.Guild != "" ? $"({c.Guild} · {Ranks[Mathf.Clamp(c.Rank, 0, 2)]})" : "(sem guilda)");
                    _pInfo[i].Set($"<color=#ffc04a>{ClassDefinition.Get((CharacterClass)c.Cls).DisplayName} {c.Level}\nRenascimentos: {c.Rebirths}</color>");
                }
                else
                {
                    _pName[i].Set("Vazio");
                    _pSub[i].Set("");
                    _pInfo[i].Set("<color=#7d7366>clique para criar</color>");
                }
            }
            _enter.SetEnabled(_sel >= 0);
            _delete.SetEnabled(_sel >= 0);
            _create.SetEnabled(rows.Count < 5);
            _title.Set(G._mode == ConnectMode.Online ? $"Conta {G._account}  ·  escolha seu herói" : "Escolha seu herói");
            _error.Set(G._loginError != "" ? $"<color=#ff7a6a>{G._loginError}</color>" : (G._awaitingList ? "Carregando..." : ""));
            if (Event.current == null && InputK.KeyDown(KeyCode.Return) && !_modal.visible) Enter();
            if (InputK.KeyDown(KeyCode.LeftArrow)) _sel = Mathf.Max(0, _sel - 1);
            if (InputK.KeyDown(KeyCode.RightArrow)) _sel = Mathf.Min(rows.Count - 1, _sel + 1);
        }
    }
}
