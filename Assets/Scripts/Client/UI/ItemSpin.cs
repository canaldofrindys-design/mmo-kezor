using Kezor.Core.Items;
using UnityEngine;
using UnityEngine.UIElements;

namespace Kezor.Client.UI
{
    /// <summary>
    /// Item girando em 360° no lugar do ícone, como no inventário do MU: ao passar o mouse (ou clicar) numa peça da
    /// mochila, do equipamento ou do baú, o modelo 3D da peça é montado num palco fora do mapa, numa camada que só a
    /// câmera do giro enxerga, e a imagem dessa câmera vira o fundo do ícone até o mouse sair.
    /// Um item por vez; sem modelo (poções, runas, joias) o ícone fica parado.
    /// </summary>
    public sealed class ItemSpin : MonoBehaviour
    {
        private const int Layer = 31;
        private const float DegPerSec = 110f, Tilt = 14f;
        private static readonly Vector3 Stage = new Vector3(0f, 900f, 0f);   // longe do terreno e da névoa baixa

        private static ItemSpin? _inst;
        private Camera _cam = null!;
        private Transform _turn = null!;
        private RenderTexture? _rt;
        private GameObject? _model;
        private VisualElement? _el;
        private StyleBackground _old;
        private int _defId = -1;

        private static ItemSpin Inst
        {
            get
            {
                if (_inst != null) return _inst;
                var go = new GameObject("ItemSpin");
                DontDestroyOnLoad(go);
                _inst = go.AddComponent<ItemSpin>();
                _inst.Build();
                return _inst;
            }
        }

        private void Build()
        {
            transform.position = Stage;
            _turn = new GameObject("Turn").transform;
            _turn.SetParent(transform, false);
            var cg = new GameObject("SpinCam");
            cg.transform.SetParent(transform, false);
            _cam = cg.AddComponent<Camera>();
            _cam.orthographic = true;
            _cam.clearFlags = CameraClearFlags.SolidColor;
            _cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
            _cam.cullingMask = 1 << Layer;
            _cam.nearClipPlane = 0.01f; _cam.farClipPlane = 50f;
            _cam.allowHDR = false; _cam.allowMSAA = true;
            _cam.depth = -50;
            _cam.enabled = false;
        }

        /// <summary>Começa a girar o item <paramref name="d"/> dentro de <paramref name="icon"/> (troca o fundo dele).</summary>
        public static void Show(VisualElement icon, ItemDefinition d, Vector2? size = null)
        {
            var s = Inst;
            if (s._el == icon && s._defId == d.Id) return;
            s.Stop();
            var paths = Gear.ModelPathsFor(d);
            if (paths.Count == 0) return;
            // pares (calças, luvas, botas, manoplas): uma peça só, como no ícone
            bool pair = d.Kind == ItemKind.Gauntlet || d.Slot == EquipSlot.Pants || d.Slot == EquipSlot.Gloves || d.Slot == EquipSlot.Boots;
            string path = pair ? paths[paths.Count - 1] : paths[0];
            var holder = new GameObject("Model");
            holder.transform.SetParent(s._turn, false);
            foreach (var p in pair ? new[] { path } : paths.ToArray())
            {
                var go = ModelLibrary.Instantiate(p, p, holder.transform);
                if (go != null) Gear.ApplyItemMaps(go, p);
            }
            var rs = holder.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) { Destroy(holder); return; }
            foreach (var t in holder.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = Layer;
            foreach (var ps in holder.GetComponentsInChildren<ParticleSystem>(true)) ps.gameObject.SetActive(false);
            foreach (var r in rs) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            if (d.IsWeapon || d.Kind == ItemKind.Shield) Orient(holder.transform, rs, d.Kind == ItemKind.Shield);

            // centraliza no eixo do giro
            s._turn.localRotation = Quaternion.identity;
            var b = Bounds(rs);
            holder.transform.position += s._turn.position - b.center;
            b = Bounds(rs);

            float w = size?.x ?? icon.resolvedStyle.width, h = size?.y ?? icon.resolvedStyle.height;
            if (float.IsNaN(w) || w < 8f) w = 80f;
            if (float.IsNaN(h) || h < 8f) h = 80f;
            float aspect = w / h;
            float radius = Mathf.Sqrt(b.extents.x * b.extents.x + b.extents.z * b.extents.z);   // largura em qualquer ângulo do giro
            s._cam.transform.rotation = Quaternion.Euler(Tilt, 180f, 0f);
            s._cam.transform.position = b.center - s._cam.transform.forward * (b.extents.magnitude * 2f + 2f);
            s._cam.orthographicSize = Mathf.Max(b.extents.y + radius * Mathf.Sin(Tilt * Mathf.Deg2Rad), radius / aspect) * 1.08f;

            int rw = Mathf.Clamp(Mathf.RoundToInt(w * 2f), 64, 512), rh = Mathf.Clamp(Mathf.RoundToInt(h * 2f), 64, 512);
            if (s._rt == null || s._rt.width != rw || s._rt.height != rh)
            {
                if (s._rt != null) { s._rt.Release(); Destroy(s._rt); }
                s._rt = new RenderTexture(rw, rh, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4, name = "ItemSpinRT" };
            }
            s._cam.targetTexture = s._rt;
            s._cam.aspect = aspect;
            s._cam.enabled = true;
            HideFromOtherCameras(s._cam);

            s._model = holder;
            s._el = icon;
            s._defId = d.Id;
            s._old = icon.style.backgroundImage;
            icon.style.backgroundImage = new StyleBackground(Background.FromRenderTexture(s._rt));
        }

        /// <summary>Para o giro se ele estiver em <paramref name="icon"/> (ou em qualquer um, se null) e devolve o ícone.</summary>
        public static void Hide(VisualElement? icon = null)
        {
            if (_inst == null || _inst._el == null) return;
            if (icon != null && _inst._el != icon) return;
            _inst.Stop();
        }

        private void Stop()
        {
            if (_el != null) _el.style.backgroundImage = _old;
            _el = null; _defId = -1;
            if (_model != null) Destroy(_model);
            _model = null;
            _cam.enabled = false;
        }

        private void Update()
        {
            if (_el == null) return;
            // a janela fechou ou o slot sumiu com o mouse em cima
            if (_el.panel == null || _el.resolvedStyle.display == DisplayStyle.None) { Stop(); return; }
            _turn.Rotate(0f, DegPerSec * Time.unscaledDeltaTime, 0f, Space.World);
            _el.MarkDirtyRepaint();     // a interface não sabe que a imagem da câmera mudou
        }

        private void OnDestroy()
        {
            if (_rt != null) { _rt.Release(); Destroy(_rt); }
            if (_inst == this) _inst = null;
        }

        private static void HideFromOtherCameras(Camera mine)
        {
            foreach (var c in Camera.allCameras)
                if (c != mine) c.cullingMask &= ~(1 << Layer);
        }

        private static Bounds Bounds(Renderer[] rs)
        {
            var b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            return b;
        }

        /// <summary>Armas em pé (eixo mais longo na vertical); escudos de frente — igual ao gerador de ícones.</summary>
        private static void Orient(Transform root, Renderer[] rs, bool shield)
        {
            var sz = Bounds(rs).size;
            if (shield)
            {
                if (sz.x <= sz.y && sz.x <= sz.z) root.localRotation = Quaternion.Euler(0, 90, 0);
                else if (sz.y <= sz.x && sz.y <= sz.z) root.localRotation = Quaternion.Euler(90, 0, 0);
                return;
            }
            if (sz.z >= sz.x && sz.z >= sz.y) root.localRotation = Quaternion.Euler(-90, 0, 0);
            else if (sz.x >= sz.y && sz.x >= sz.z) root.localRotation = Quaternion.Euler(0, 0, 90);
        }
    }
}
