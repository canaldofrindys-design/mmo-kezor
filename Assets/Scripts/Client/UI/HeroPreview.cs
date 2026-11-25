using System.Linq;
using UnityEngine;
using Kezor.Core.Characters;
using Kezor.Core.Items;
using Kezor.Core.Net;

namespace Kezor.Client.UI
{
    /// <summary>
    /// Palco do herói nas telas de entrada: um pedestal facetado com anel de brasa e faíscas, longe do mundo (camada 30),
    /// filmado por uma câmera própria numa RenderTexture com fundo transparente. O herói gira devagar; arrastar gira à mão.
    /// </summary>
    public sealed class HeroPreview : MonoBehaviour
    {
        public const int StageLayer = 30;
        private static readonly Vector3 Origin = new Vector3(0, -400f, 0);

        public RenderTexture Texture { get; private set; } = null!;
        private Camera _cam = null!;
        private Transform _stage = null!;
        private Rig? _rig;
        private string _shownKey = "";
        private float _yaw = 200f;
        public float DragYaw;

        public static HeroPreview Create(Camera main)
        {
            var go = new GameObject("PalcoDoHeroi");
            DontDestroyOnLoad(go);
            var p = go.AddComponent<HeroPreview>();
            p.Build(main);
            return p;
        }

        private void Build(Camera main)
        {
            main.cullingMask &= ~(1 << StageLayer);
            Texture = new RenderTexture(720, 900, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4, name = "HeroPreview" };
            _stage = new GameObject("Palco").transform;
            _stage.SetParent(transform, false);
            _stage.position = Origin;

            // pedestal: octógono de pedra com aro de bronze e anel de brasa
            var mb = new MeshBuilder();
            mb.Cylinder(new Vector3(0, -0.32f, 0), 1.25f, 1.15f, 0.22f, 8, KezorLook.Hex("#3a332c"), KezorLook.Hex("#4a4038"));
            mb.Cylinder(new Vector3(0, -0.12f, 0), 1.08f, 1.02f, 0.12f, 8, KezorLook.Hex("#8a6a3e"), KezorLook.Hex("#6b5232"));
            mb.Cylinder(new Vector3(0, 0.0f, 0), 0.96f, 0.96f, 0.02f, 8, KezorLook.Glow(KezorLook.Hex("#ff6a1a")));
            mb.Cylinder(new Vector3(0, 0.01f, 0), 0.9f, 0.9f, 0.02f, 8, KezorLook.Hex("#241c16"));
            var ped = KezorLook.MakeObject("Pedestal", mb.ToMesh(), _stage);
            ped.layer = StageLayer;
            var fx = gameObject.AddComponent<GearFx>();
            var ps = fx.Sparks(_stage, new Vector3(0, 0.1f, 0), new Vector3(2.0f, 0.2f, 2.0f), KezorLook.Hex("#ff8a3a"), 9f, 0.05f, -0.18f);
            ps.gameObject.layer = StageLayer;

            var cg = new GameObject("CameraDoPalco");
            cg.transform.SetParent(transform, false);
            _cam = cg.AddComponent<Camera>();
            _cam.clearFlags = CameraClearFlags.SolidColor;
            _cam.backgroundColor = new Color(0, 0, 0, 0);
            _cam.cullingMask = 1 << StageLayer;
            _cam.fieldOfView = 26f;
            _cam.nearClipPlane = 0.1f; _cam.farClipPlane = 30f;
            _cam.targetTexture = Texture;
            _cam.transform.position = Origin + new Vector3(0, 1.7f, 9.6f);
            _cam.transform.LookAt(Origin + new Vector3(0, 1.05f, 0));
            _cam.depth = -5;
        }

        /// <summary>Mostra o herói da classe. Com <paramref name="showcase"/>, veste o conjunto e a arma épica da classe (+9).</summary>
        public void Show(CharacterClass cls, bool showcase)
        {
            string key = cls + ":" + showcase;
            if (key == _shownKey && _rig != null) return;
            _shownKey = key;
            if (_rig != null) Destroy(_rig.Root.gameObject);
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

            _rig = Gear.Player("preview", cls, app);
            _rig.Root.SetParent(_stage, false);
            _rig.Root.localPosition = Vector3.zero;
            foreach (var t in _rig.Root.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = StageLayer;
        }

        private void LateUpdate()
        {
            if (_rig == null) return;
            if (Mathf.Abs(DragYaw) > 0.01f) { _yaw += DragYaw; DragYaw = 0; }
            else _yaw += Time.deltaTime * 14f;
            _rig.Root.localRotation = Quaternion.Euler(0, _yaw, 0);
            float t = Time.time;
            if (_rig.Body != null) _rig.Body.localPosition = _rig.BodyBase + new Vector3(0, Mathf.Sin(t * 1.6f) * 0.012f, 0);   // respiração
            if (_rig.ArmL != null) _rig.ArmL.localRotation = Quaternion.Euler(Mathf.Sin(t * 1.6f) * 2.5f, 0, 4f);
            if (_rig.ArmR != null) _rig.ArmR.localRotation = Quaternion.Euler(-Mathf.Sin(t * 1.6f) * 2.5f, 0, -4f);
            if (_rig.WingL != null && _rig.WingR != null)
            {
                float f = Mathf.Sin(t * 2.2f) * 9f;
                _rig.WingL.localRotation = Quaternion.Euler(0, -18f, f + 6f);
                _rig.WingR.localRotation = Quaternion.Euler(0, 18f, -f - 6f);
            }
        }

        public void SetActive(bool on) { if (_cam != null && _cam.enabled != on) _cam.enabled = on; }
    }
}
