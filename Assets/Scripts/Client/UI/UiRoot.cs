using UnityEngine;
using UnityEngine.UIElements;

namespace Kezor.Client.UI
{
    /// <summary>
    /// Raiz da interface em UI Toolkit: cria o painel (escala pela altura com referência de 1600×900, vezes a escala
    /// escolhida nas opções), carrega o tema e as fontes e separa as camadas: rótulos do mundo, HUD, janelas e sobreposição
    /// (tooltip, toasts). Só apresentação: lê o estado e chama ações pelo <see cref="KezorGame"/>.
    /// </summary>
    public sealed class UiRoot : MonoBehaviour
    {
        public const float RefWidth = 1600f, RefHeight = 900f;
        public const string ScalePref = "kz.uiScale";

        public static UiRoot? Instance { get; private set; }

        public KezorGame Game { get; private set; } = null!;
        public VisualElement Root { get; private set; } = null!;
        public VisualElement WorldLayer { get; private set; } = null!;
        public VisualElement HudLayer { get; private set; } = null!;
        public VisualElement WindowLayer { get; private set; } = null!;
        public VisualElement OverlayLayer { get; private set; } = null!;
        public HudView Hud { get; private set; } = null!;
        public WorldLabels Labels { get; private set; } = null!;
        public DragDrop Drag { get; private set; } = null!;
        public WindowManager Windows { get; private set; } = null!;
        public PromptsView Prompts { get; private set; } = null!;
        public EntryScreen Entry { get; private set; } = null!;
        public LoginScreen Login { get; private set; } = null!;
        public LoadingScreen Loading { get; private set; } = null!;
        public SelectScreen Select { get; private set; } = null!;
        private bool _wasInWorld;
        private VisualElement _itemTip = null!;
        private Label _itemTipText = null!;

        private UIDocument _doc = null!;
        private PanelSettings _settings = null!;

        public float UserScale
        {
            get => PlayerPrefs.GetFloat(ScalePref, 1f);
            set { PlayerPrefs.SetFloat(ScalePref, Mathf.Clamp(value, 0.8f, 1.4f)); ApplyScale(); }
        }

        public static UiRoot Create(KezorGame game)
        {
            var go = new GameObject("Interface");
            DontDestroyOnLoad(go);
            var ui = go.AddComponent<UiRoot>();
            ui.Init(game);
            return ui;
        }

        private void Init(KezorGame game)
        {
            Instance = this;
            Game = game;
            _settings = ScriptableObject.CreateInstance<PanelSettings>();
            _settings.name = "KezorPanel";
            _settings.themeStyleSheet = Resources.Load<ThemeStyleSheet>("UI/KezorTheme");
            _settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            _settings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            _settings.match = 1f;
            _settings.sortingOrder = 10;
            ApplyScale();

            _doc = gameObject.AddComponent<UIDocument>();
            _doc.panelSettings = _settings;
            Root = _doc.rootVisualElement;
            var sheet = Resources.Load<StyleSheet>("UI/Kezor");
            if (sheet != null) Root.styleSheets.Add(sheet);
            Root.AddToClassList("kz-root");
            Root.pickingMode = PickingMode.Ignore;

            WorldLayer = Kz.El("hud-layer", Root);
            HudLayer = Kz.El("hud-layer", Root);
            WindowLayer = Kz.El("hud-layer", Root);
            OverlayLayer = Kz.El("hud-layer", Root);

            Labels = new WorldLabels(this);
            Drag = new DragDrop(this);
            Root.RegisterCallback<PointerDownEvent>(e => { if (e.target is VisualElement ve && ve != Root) LastUiPointerFrame = Time.frameCount; }, TrickleDown.TrickleDown);
            Root.RegisterCallback<PointerUpEvent>(e => { if (e.target is VisualElement ve && ve != Root) LastUiPointerFrame = Time.frameCount; }, TrickleDown.TrickleDown);          // antes da HUD: os atalhos Q W E R recebem itens arrastados
            Hud = new HudView(this);
            Prompts = new PromptsView(this);
            _itemTip = Kz.El("hud-tip hud-tip--item", OverlayLayer);
            _itemTipText = Kz.Text("", "kz-text", _itemTip);
            _itemTip.Show(false);
            Windows = new WindowManager(this);
            OptionsWindow.ApplySaved();
            Entry = new EntryScreen(this);
            Login = new LoginScreen(this);
            Select = new SelectScreen(this);
            Loading = new LoadingScreen(this);
        }

        private void ApplyScale()
        {
            if (_settings == null) return;
            float s = Mathf.Clamp(PlayerPrefs.GetFloat(ScalePref, 1f), 0.8f, 1.4f);
            _settings.referenceResolution = new Vector2Int(Mathf.RoundToInt(RefWidth / s), Mathf.RoundToInt(RefHeight / s));
        }

        /// <summary>O ponteiro está sobre algo clicável da interface (o clique não vai para o mundo).</summary>
        /// <summary>Quadro do último clique que caiu em algum elemento da interface (bloqueia comandos de mundo).</summary>
        public int LastUiPointerFrame = -10;

        public bool PointerOverUi(Vector2 screenPos)
        {
            var panel = Root?.panel;
            if (panel == null) return false;
            var p = RuntimePanelUtils.ScreenToPanel(panel, new Vector2(screenPos.x, Screen.height - screenPos.y));
            var picked = panel.Pick(p);
            return picked != null && picked != Root;
        }

        /// <summary>Posição do ponto do mundo no painel (null atrás da câmera).</summary>
        public Vector2? WorldToPanel(Vector3 world)
        {
            var cam = Game.Cam;
            if (cam == null || Root.panel == null) return null;
            var sp = cam.WorldToScreenPoint(world);
            if (sp.z < 0) return null;
            return RuntimePanelUtils.ScreenToPanel(Root.panel, new Vector2(sp.x, Screen.height - sp.y));
        }

        private void Update()
        {
            if (Game == null) return;
            bool inWorld = Game.InWorld;
            HudLayer.Show(inWorld);
            WorldLayer.Show(inWorld);
            WindowLayer.Show(inWorld);
            if (!inWorld) _itemTip.Show(false);
            Game.ToolkitHud = true;
            Entry.Tick();
            Login.Tick();
            Select.Tick();
            if (inWorld && !_wasInWorld) { Loading.Show(Game.Map); Loading.Done(); }   // entrando no mundo
            _wasInWorld = inWorld;
            Loading.Tick();
            if (!inWorld) return;
            Hud.Tick();
            Drag.Tick();
            Labels.Tick();
            Prompts.Tick();
            Windows.Tick();
        }

        public void ShowLoading(Kezor.Core.World.MapDefinition map) => Loading.Show(map);
        public void LoadingDone() => Loading.Done();

        /// <summary>Tooltip rico de item perto do ponteiro (fica dentro da tela).</summary>
        public void ShowItemTip(Kezor.Core.Items.Item it, Vector2 at, bool compare)
        {
            _itemTipText.Set(ItemTooltip.Build(it, Game, compare));
            _itemTip.Show(true);
            _itemTip.BringToFront();
            var root = Root.contentRect;
            float w = _itemTip.resolvedStyle.width > 1 ? _itemTip.resolvedStyle.width : 300, h = _itemTip.resolvedStyle.height > 1 ? _itemTip.resolvedStyle.height : 200;
            float x = at.x + 20, y = at.y + 12;
            if (x + w > root.width - 6) x = at.x - w - 20;
            if (y + h > root.height - 6) y = root.height - h - 6;
            _itemTip.style.left = Mathf.Max(4, x); _itemTip.style.top = Mathf.Max(4, y);
        }

        public void HideItemTip() => _itemTip.Show(false);

        private void OnDestroy() { if (Instance == this) Instance = null; }
    }
}
