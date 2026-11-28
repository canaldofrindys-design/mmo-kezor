using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Kezor.Core.Characters;
using Kezor.Core.Items;

namespace Kezor.Client
{
    /// <summary>
    /// Equipamento visível no herói: corpo base da classe (sem equipamento) + peças do Blender penduradas nas partes do rig
    /// (Resources/Models/Equip, Wings, Mounts, Pets). Cada peça tem uma malha "Piece" com o pivô na articulação, então
    /// basta pendurar com posição local zero. Também liga o brilho de runa (+7..+15), a aura Ascendente, o rastro das
    /// armas épicas, as asas, a montaria e o mascote.
    /// </summary>
    public static class Gear
    {
        public static string BaseModel(CharacterClass c) => Models.ClassModel(c) + "Base";

        internal static readonly Dictionary<int, string> WeaponModels = new Dictionary<int, string>
        {
            { 1, "Arma_LaminaFerro" }, { 3, "Arma_MachadoGuerra" }, { 2, "Arma_MontanteTempestade" }, { 30, "Arma_LaminaVortice" }, { 40, "Arma_FendenteRubro" },
            { 10, "Arma_CajadoAprendiz" }, { 12, "Arma_VarinhaAmbar" }, { 11, "Arma_CajadoOraculo" }, { 41, "Arma_CetroAuroraVelada" },
            { 20, "Arma_ArcoCaca" }, { 21, "Arma_ArcoComposto" }, { 22, "Arma_ArcoVentoNorte" }, { 42, "Arma_ArcoSussurroBrasa" },
            { 120, "Escudo_Bronze" }, { 121, "Escudo_PavesJuramento" },
            { 50, "Arma_LaminaRunicaCurta" }, { 51, "Arma_SabreCrepusculo" }, { 52, "Arma_EspadaMarePrismatica" }, { 43, "Arma_LaminaAstralPartida" },
            { 53, "Arma_LancaRecruta" }, { 54, "Arma_LancaVanguarda" }, { 55, "Arma_AlabardaBastiao" }, { 44, "Arma_HasteJuramentoSolar" },
            { 56, "Arma_TomoEcoMenor" }, { 57, "Arma_SinoPrataVelada" }, { 58, "Arma_GrimorioMilEcos" }, { 45, "Arma_SinoSilencioEterno" },
            { 59, "Arma_ManoplasCouroCru" }, { 60, "Arma_ManoplasFerroFundido" }, { 61, "Arma_PunhosMagma" }, { 46, "Arma_NucleoBrasaViva" },
            // Geada Rubra (Colosso da Escarpa): modelos existentes, recoloridos em vermelho-gelo (WeaponTints)
            { 70, "Arma_MontanteTempestade" }, { 71, "Arma_CajadoOraculo" }, { 72, "Arma_ArcoVentoNorte" }, { 73, "Arma_EspadaMarePrismatica" },
            { 74, "Arma_AlabardaBastiao" }, { 75, "Arma_GrimorioMilEcos" }, { 76, "Arma_PunhosMagma" },
        };

        internal static readonly Dictionary<int, string> ExtraModels = new Dictionary<int, string>
        {
            { 300, "Wings/Asas_Cinza" }, { 301, "Wings/Asas_Crepusculo" }, { 302, "Wings/Asas_Brasalume" },
            { 400, "Mounts/Montaria_LagartoBasalto" }, { 401, "Mounts/Montaria_LoboBrasas" },
            { 500, "Pets/Mascote_DiabreteCinza" }, { 501, "Pets/Mascote_LumeGuardiao" },
        };

        /// <summary>Altura do assento de cada montaria (topo da sela, em metros, medido no Blender).</summary>
        private static readonly Dictionary<int, float> SeatHeight = new Dictionary<int, float> { { 400, 0.78f }, { 401, 1.35f } };
        private const float HipHeight = 0.92f;

        /// <summary>Modelos (Resources/Models/...) que representam o item, para gerar o ícone. Vazio se não houver modelo.</summary>
        public static List<string> ModelPathsFor(ItemDefinition d)
        {
            var list = new List<string>();
            if (WeaponModels.TryGetValue(d.Id, out var w))
            {
                list.Add("Equip/" + w);
                if (d.Kind == ItemKind.Gauntlet) list.Add("Equip/" + w + "_L");
            }
            else if (ExtraModels.TryGetValue(d.Id, out var x)) list.Add(x);
            else if (SetOf(d) is string set)
            {
                switch (d.Slot)
                {
                    case EquipSlot.Helm: list.Add($"Equip/{set}_Elmo"); break;
                    case EquipSlot.Armor: list.Add($"Equip/{set}_Armadura"); break;
                    case EquipSlot.Pants: list.Add($"Equip/{set}_CalcaL"); list.Add($"Equip/{set}_CalcaR"); break;
                    case EquipSlot.Gloves: list.Add($"Equip/{set}_LuvaL"); list.Add($"Equip/{set}_LuvaR"); break;
                    case EquipSlot.Boots: list.Add($"Equip/{set}_BotaL"); list.Add($"Equip/{set}_BotaR"); break;
                }
            }
            return list;
        }

        /// <summary>Cor do brilho de conjunto completo (as 5 peças do mesmo set equipadas).</summary>
        internal static readonly Dictionary<string, Color> SetColors = new Dictionary<string, Color>
        {
            { "Couro", KezorLook.Hex("#d9a066") }, { "Escamas", KezorLook.Hex("#7fd18b") }, { "Veu", KezorLook.Hex("#9fb8ff") },
            { "Runica", KezorLook.Hex("#7af0ff") }, { "Trama", KezorLook.Hex("#c9b8ff") }, { "Bastiao", KezorLook.Hex("#ffd36a") },
            { "Ecos", KezorLook.Hex("#b6ffd9") }, { "Igneo", KezorLook.Hex("#ff7a2a") },
            // <kz-class-sets colors>
            { "Cavaleiro", KezorLook.Hex("#c9d2dc") }, { "Templario", KezorLook.Hex("#9fd4ff") }, { "Soberano", KezorLook.Hex("#ffcf4a") },
            // </kz-class-sets colors>
        };

        /// <summary>Nome do set quando elmo, armadura, calças, luvas e botas são todos do mesmo conjunto.</summary>
        internal static string? FullSetOf(Appearance a)
        {
            string? set = null;
            foreach (var slot in new[] { EquipSlot.Helm, EquipSlot.Armor, EquipSlot.Pants, EquipSlot.Gloves, EquipSlot.Boots })
            {
                if (!(a[slot].Def is ItemDefinition d) || !(SetOf(d) is string s)) return null;
                if (set == null) set = s;
                else if (set != s) return null;
            }
            return set;
        }

        /// <summary>Conjunto (prefixo do modelo) de cada peça de armadura.</summary>
        internal static string? SetOf(ItemDefinition d)
        {
            if (d.Id >= 100 && d.Id <= 104) return "Couro";
            if (d.Id >= 110 && d.Id <= 114) return "Escamas";
            if (d.Id >= 130 && d.Id <= 134) return "Veu";
            if (d.Id >= 140 && d.Id <= 144) return "Runica";
            if (d.Id >= 150 && d.Id <= 154) return "Trama";
            if (d.Id >= 160 && d.Id <= 164) return "Bastiao";
            if (d.Id >= 170 && d.Id <= 174) return "Ecos";
            if (d.Id >= 180 && d.Id <= 184) return "Igneo";
            // <kz-class-sets setof>
            if (d.Id >= 700 && d.Id <= 704) return "Cavaleiro";
            if (d.Id >= 710 && d.Id <= 714) return "Templario";
            if (d.Id >= 720 && d.Id <= 724) return "Soberano";
            // </kz-class-sets setof>
            return null;
        }

        private static readonly Dictionary<int, Color> EpicColors = new Dictionary<int, Color>
        {
            { 40, KezorLook.Hex("#ff4a1a") }, { 41, KezorLook.Hex("#ffd36a") }, { 42, KezorLook.Hex("#ff8a2a") },
            { 43, KezorLook.Hex("#b98bff") }, { 44, KezorLook.Hex("#ffb347") }, { 45, KezorLook.Hex("#e0b8ff") }, { 46, KezorLook.Hex("#ff6a1a") },
        };

        /// <summary>Brilho por nível de runa: intensidade no shader, cor e partículas por segundo.</summary>
        public static (float glow, Color color, float rate) RuneTier(int level)
        {   // estilo heroico: brilho e partículas bem mais fortes a partir do +11
            if (level >= 15) return (0.62f, KezorLook.Hex("#7af0ff"), 60f);
            if (level >= 13) return (0.52f, KezorLook.Hex("#a77bff"), 40f);
            if (level >= 11) return (0.42f, KezorLook.Hex("#ff8a2a"), 24f);
            if (level >= 9) return (0.34f, KezorLook.Hex("#ffd36a"), 9f);
            if (level >= 7) return (0.2f, KezorLook.Hex("#bfe3ff"), 2f);
            return (0f, Color.black, 0f);
        }

        /// <summary>Monta o herói com tudo o que a aparência traz. Sem FBX, cai no modelo antigo ou no procedural.</summary>
        public static Rig Player(string name, CharacterClass cls, Appearance a)
        {
            var rig = ModelLibrary.Rig(BaseModel(cls), name);
            bool legacy = false;
            if (rig == null) { rig = ModelLibrary.Rig(Models.ClassModel(cls), name); legacy = true; } // modelo antigo já vem armado
            if (rig == null) return Models.Humanoid(name, Models.OutfitFor(cls, a[EquipSlot.RightHand].Id));

            var fx = rig.Root.gameObject.AddComponent<GearFx>();
            fx.Rig = rig;
            if (!legacy)
            {
                var baseFilters = rig.Root.GetComponentsInChildren<MeshFilter>(true);   // corpo e cabeça (antes das peças)
                var hide = new List<string>();
                var w = a[EquipSlot.RightHand];
                if (w.Def != null && WeaponModels.TryGetValue(w.Id, out var wm))
                {
                    bool bow = w.Def.Kind == ItemKind.Bow;                       // arco fica na mão esquerda
                    var rs = Attach(rig, "Equip/" + wm, bow ? rig.ArmL : rig.ArmR, out var piece);
                    TintWeapon(rs, w.Id);
                    fx.Decorate(rs, w.Level, w.Ascended);
                    if (w.Def.Epic && piece != null) fx.Epic(piece, rs, EpicColors.TryGetValue(w.Id, out var ec) ? ec : Color.white, (bow ? rig.ArmL : rig.ArmR) ?? rig.Body);
                    if (w.Def.Kind == ItemKind.Gauntlet)
                    {   // manoplas: o par vai nas duas mãos (modelo _L na esquerda)
                        var ls = Attach(rig, "Equip/" + wm + "_L", rig.ArmL, out var lp);
                        TintWeapon(ls, w.Id);
                        fx.Decorate(ls, w.Level, w.Ascended);
                        if (w.Def.Epic && lp != null) fx.Epic(lp, ls, EpicColors.TryGetValue(w.Id, out var ec2) ? ec2 : Color.white, rig.ArmL ?? rig.Body);
                    }
                }
                var sh = a[EquipSlot.LeftHand];
                if (sh.Def != null && WeaponModels.TryGetValue(sh.Id, out var sm)) fx.Decorate(Attach(rig, "Equip/" + sm, rig.ArmL, out _), sh.Level, sh.Ascended);

                var helm = PieceFor(rig, fx, a[EquipSlot.Helm], "Elmo", rig.Head, hide: hide);
                if (a[EquipSlot.Helm].Def is ItemDefinition hd && SetOf(hd) is string hs && ClosedHelms.Contains(hs)) HideHeadUnderHelm(rig, helm);
                PieceFor(rig, fx, a[EquipSlot.Armor], "Armadura", rig.Body, hide: hide);
                PieceFor(rig, fx, a[EquipSlot.Pants], "CalcaL", rig.LegL, "CalcaR", rig.LegR, hide);
                PieceFor(rig, fx, a[EquipSlot.Gloves], "LuvaL", rig.ArmL, "LuvaR", rig.ArmR, hide);
                PieceFor(rig, fx, a[EquipSlot.Boots], "BotaL", rig.LegL, "BotaR", rig.LegR, hide);
                if (hide.Count > 0)
                {   // nunca derruba o herói: se falhar, só fica sem esconder a pele
                    try { HideCovered(rig, baseFilters, BaseModel(cls), hide); }
                    catch (System.Exception e) { Debug.LogWarning("[Kezor] esconder o corpo sob a armadura falhou: " + e); }
                }
                int armorLevel = a.MaxArmorLevel;
                if (armorLevel >= 11) { fx.BodySparks(RuneTier(armorLevel)); fx.GroundAura(RuneTier(armorLevel), armorLevel); }
                if (FullSetOf(a) is string full && SetColors.TryGetValue(full, out var setColor)) fx.SetAura(setColor);
            }

            bool asc = false;
            foreach (var p in a.Pieces) asc |= p.Ascended;
            if (asc) fx.AscendedAura();

            var wings = a[EquipSlot.Wings];
            if (wings.Def != null && ExtraModels.TryGetValue(wings.Id, out var wp)) AttachWings(rig, fx, wp, wings.Level);

            var mount = a[EquipSlot.Mount];
            if (a.Mounted && mount.Def != null && ExtraModels.TryGetValue(mount.Id, out var mp)) AttachMount(rig, mp, SeatHeight.TryGetValue(mount.Id, out var seat) ? seat : 1f);

            var pet = a[EquipSlot.Pet];
            if (pet.Def != null && ExtraModels.TryGetValue(pet.Id, out var pp)) PetFollower.Spawn(rig, pp, pet.Def.Name);
            return rig;
        }

        private static List<Renderer>? PieceFor(Rig rig, GearFx fx, Appearance.Piece p, string suffix, Transform? part, string? suffix2 = null, Transform? part2 = null,
                                                List<string>? hide = null)
        {
            if (p.Def == null || part == null) return null;
            var set = SetOf(p.Def);
            if (set == null) return null;
            var rs = Attach(rig, $"Equip/{set}_{suffix}", part, out _);
            ApplyMaps(rs, $"{set}_{suffix}");
            if (rs.Count > 0) hide?.Add($"{set}_{suffix}");
            if (suffix2 != null && part2 != null)
            {
                var rs2 = Attach(rig, $"Equip/{set}_{suffix2}", part2, out _);
                ApplyMaps(rs2, $"{set}_{suffix2}");
                if (rs2.Count > 0) hide?.Add($"{set}_{suffix2}");
                rs.AddRange(rs2);
            }
            fx.Decorate(rs, p.Level, p.Ascended, sparks: false);
            return rs;
        }

        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap"), BumpMapId = Shader.PropertyToID("_BumpMap"),
                                    MaskMapId = Shader.PropertyToID("_MaskMap"), UseMapsId = Shader.PropertyToID("_UseMaps");
        private static readonly Dictionary<string, (Texture2D? b, Texture2D? n, Texture2D? m)> _maps = new Dictionary<string, (Texture2D?, Texture2D?, Texture2D?)>();

        /// <summary>
        /// Peças com texturas assadas no Blender (Resources/Textures/Equip/&lt;Set&gt;_&lt;Peça&gt;_Base/_Normal/_Mask):
        /// liga o modo de mapas do Kezor/LowPoly (cor pintada, relevo, metal e brilho). Sem textura, fica a cor de vértice.
        /// </summary>
        private static void ApplyMaps(List<Renderer> rs, string name)
        {
            if (rs.Count == 0) return;
            if (!_maps.TryGetValue(name, out var t))
            {
                // a peça usa a própria textura ou a do par: Runica_LuvaL -> Runica_Luva; calças e botas -> Runica_Pernas
                foreach (var n in MapNames(name))
                {
                    t = (Resources.Load<Texture2D>($"Textures/Equip/{n}_Base"), Resources.Load<Texture2D>($"Textures/Equip/{n}_Normal"),
                         Resources.Load<Texture2D>($"Textures/Equip/{n}_Mask"));
                    if (t.b != null) break;
                }
                _maps[name] = t;
            }
            if (t.b == null) return;
            var mpb = new MaterialPropertyBlock();
            foreach (var r in rs)
            {
                r.GetPropertyBlock(mpb);
                mpb.SetTexture(BaseMapId, t.b);
                if (t.n != null) mpb.SetTexture(BumpMapId, t.n);
                if (t.m != null) mpb.SetTexture(MaskMapId, t.m);
                mpb.SetFloat(UseMapsId, 1f);
                r.SetPropertyBlock(mpb);
            }
        }

        private static IEnumerable<string> MapNames(string name)
        {
            yield return name;
            if (name.EndsWith("L") || name.EndsWith("R")) yield return name.Substring(0, name.Length - 1);
            int u = name.IndexOf('_');
            if (u > 0 && (name.Contains("_Calca") || name.Contains("_Bota"))) yield return name.Substring(0, u) + "_Pernas";
        }

        private static readonly Dictionary<string, HashSet<long>?> _hideCache = new Dictionary<string, HashSet<long>?>();

        private static long HideKey(int x, int y, int z) => ((long)(x + 4096) << 26) | ((long)(y + 4096) << 13) | (long)(z + 4096);

        /// <summary>Vértices do corpo cobertos pela peça (Resources/Equip/Hide/&lt;Set&gt;_&lt;Peça&gt;_&lt;Modelo&gt;.txt, mm no Blender).</summary>
        private static HashSet<long>? HideList(string piece, string model)
        {
            string key = piece + "_" + model;
            if (_hideCache.TryGetValue(key, out var hs)) return hs;
            hs = null;
            var ta = Resources.Load<TextAsset>("Equip/Hide/" + key);
            if (ta != null)
            {
                hs = new HashSet<long>();
                foreach (var line in ta.text.Split('\n'))
                {
                    var p = line.Trim().Split(' ');
                    if (p.Length == 3 && int.TryParse(p[0], out var x) && int.TryParse(p[1], out var y) && int.TryParse(p[2], out var z))
                        hs.Add(HideKey(x, y, z));
                }
            }
            _hideCache[key] = hs;
            return hs;
        }

        /// <summary>
        /// Esconde a pele que fica por baixo das peças equipadas (sets com lista no Resources/Equip/Hide): tira da malha do
        /// corpo e da cabeça os triângulos com os três vértices cobertos, para nada atravessar a armadura ao animar.
        /// </summary>
        private static void HideCovered(Rig rig, MeshFilter[] filters, string model, List<string> pieces)
        {
            var all = new HashSet<long>();
            foreach (var pc in pieces)
                if (HideList(pc, model) is HashSet<long> h) all.UnionWith(h);
            if (all.Count == 0 || rig.LegL == null || rig.ArmL == null) return;
            var body = rig.Body;
            float s = body.InverseTransformPoint(rig.LegL.position).y / 0.92f;
            if (s < 0.3f) return;
            float sx = body.InverseTransformPoint(rig.ArmL.position).x < 0 ? -1f : 1f;
            var verts = new List<Vector3>();
            foreach (var mf in filters)
            {
                if (mf == null || mf.sharedMesh == null || !mf.sharedMesh.isReadable) continue;
                var src = mf.sharedMesh;
                src.GetVertices(verts);
                var toBody = body.worldToLocalMatrix * mf.transform.localToWorldMatrix;
                var hit = new bool[2][];
                int best = -1, bestN = 0;
                for (int fz = 0; fz < 2; fz++)
                {   // o eixo da frente do FBX pode vir trocado: fica o sinal que casar mais vértices
                    hit[fz] = new bool[verts.Count];
                    int n = 0;
                    for (int i = 0; i < verts.Count; i++)
                    {
                        var q = toBody.MultiplyPoint3x4(verts[i]) / s;
                        int bx = Mathf.RoundToInt(q.x * sx * 1000f), by = Mathf.RoundToInt((fz == 0 ? q.z : -q.z) * 1000f), bz = Mathf.RoundToInt(q.y * 1000f);
                        bool c = false;
                        for (int dx = -1; dx <= 1 && !c; dx++)
                            for (int dy = -1; dy <= 1 && !c; dy++)
                                for (int dz = -1; dz <= 1 && !c; dz++)
                                    c = all.Contains(HideKey(bx + dx, by + dy, bz + dz));
                        if (c) { hit[fz][i] = true; n++; }
                    }
                    if (n > bestN) { bestN = n; best = fz; }
                }
                if (best < 0) continue;
                var h = hit[best];
                var mesh = Object.Instantiate(src);
                mesh.name = src.name + "_coberto";
                // Unity 6: malha com LODs automáticos não aceita trocar os triângulos; fica só o LOD0
                var lodProp = typeof(Mesh).GetProperty("lodCount");
                if (lodProp != null && lodProp.CanWrite && lodProp.GetValue(mesh) is int lods && lods > 1) lodProp.SetValue(mesh, 1);
                for (int sub = 0; sub < mesh.subMeshCount; sub++)
                {
                    var tris = mesh.GetTriangles(sub);
                    var keep = new List<int>(tris.Length);
                    for (int t = 0; t + 2 < tris.Length; t += 3)
                        if (!(h[tris[t]] && h[tris[t + 1]] && h[tris[t + 2]])) { keep.Add(tris[t]); keep.Add(tris[t + 1]); keep.Add(tris[t + 2]); }
                    mesh.SetTriangles(keep, sub);
                }
                mf.sharedMesh = mesh;
            }
        }

        /// <summary>
        /// Elmo fechado (cobre quase toda a cabeça): esconde a malha da cabeça para o rosto arredondado não atravessar
        /// a viseira. Capuzes e elmos abertos (que não cobrem a cabeça) deixam o rosto à mostra.
        /// </summary>
        /// <summary>Conjuntos de elmo fechado (viseira): os outros (capuz, faixa, gorro) mostram o rosto.</summary>
        private static readonly HashSet<string> ClosedHelms = new HashSet<string> { "Bastiao" };   // Rúnica (v5), Escamas e Trama têm o rosto aberto

        private static void HideHeadUnderHelm(Rig rig, List<Renderer>? helm)
        {
            if (helm == null || helm.Count == 0 || rig.Head == null) return;
            var head = rig.Head.GetComponent<Renderer>();
            if (head == null) return;
            var hb = helm[0].bounds;
            foreach (var r in helm) hb.Encapsulate(r.bounds);
            var b = head.bounds;
            bool covers = hb.min.x <= b.min.x + b.size.x * 0.08f && hb.max.x >= b.max.x - b.size.x * 0.08f
                       && hb.min.z <= b.min.z + b.size.z * 0.15f && hb.max.z >= b.max.z - b.size.z * 0.15f && hb.max.y >= b.max.y - b.size.y * 0.05f
                       && hb.min.y <= b.min.y + b.size.y * 0.35f;
            if (covers) head.enabled = false;
        }

        /// <summary>Armas que reaproveitam um modelo com outra cor (Geada Rubra: vermelho com reflexo de gelo).</summary>
        internal static readonly Dictionary<int, Color> WeaponTints = new Dictionary<int, Color>
        {
            { 70, new Color(1.25f, 0.62f, 0.7f) }, { 71, new Color(1.25f, 0.62f, 0.7f) }, { 72, new Color(1.25f, 0.62f, 0.7f) }, { 73, new Color(1.25f, 0.62f, 0.7f) },
            { 74, new Color(1.25f, 0.62f, 0.7f) }, { 75, new Color(1.25f, 0.62f, 0.7f) }, { 76, new Color(1.25f, 0.62f, 0.7f) },
        };

        private static void TintWeapon(List<Renderer> rs, int itemId)
        {
            if (!WeaponTints.TryGetValue(itemId, out var tint)) return;
            var mpb = new MaterialPropertyBlock();
            foreach (var r in rs)
            {
                r.GetPropertyBlock(mpb);
                mpb.SetColor(KezorLook.TintId, tint);
                r.SetPropertyBlock(mpb);
            }
        }

        /// <summary>Pendura a malha "Piece" do modelo na parte (posição local zero = pivô da articulação).</summary>
        public static List<Renderer> Attach(Rig rig, string path, Transform? part, out Transform? piece)
        {
            piece = null;
            var list = new List<Renderer>();
            if (part == null) return list;
            var go = ModelLibrary.Instantiate(path, path, null);
            if (go == null) return list;
            piece = go.transform.Find("Piece");
            if (piece == null)
                foreach (var t in go.GetComponentsInChildren<Transform>(true))   // FBX com um vazio por cima ("__a5_root/Piece")
                    if (t.name == "Piece") { piece = t; break; }
            if (piece == null) { Debug.LogWarning($"[Kezor] '{path}' sem a malha 'Piece'"); Object.Destroy(go); return list; }
            piece.SetParent(part, false);
            piece.localPosition = Vector3.zero;
            piece.localRotation = Quaternion.identity;
            piece.localScale = Vector3.one;
            piece.name = path.Substring(path.LastIndexOf('/') + 1);
            Object.Destroy(go);
            foreach (var r in piece.GetComponentsInChildren<Renderer>()) { list.Add(r); rig.Renderers.Add(r); }
            return list;
        }

        private static void AttachWings(Rig rig, GearFx fx, string path, int level)
        {
            var go = ModelLibrary.Instantiate(path, "Asas", null);
            if (go == null) return;
            var hub = go.transform.Find("Wings");
            if (hub == null) { Object.Destroy(go); return; }
            hub.SetParent(rig.Body, false);
            hub.localPosition = Vector3.zero; hub.localRotation = Quaternion.identity;
            hub.localScale = Vector3.one * 1.3f;                                    // asas mais imponentes
            Object.Destroy(go);
            rig.WingL = hub.Find("WingL"); rig.WingR = hub.Find("WingR");
            rig.Flying = !rig.Seated;
            var rs = new List<Renderer>(hub.GetComponentsInChildren<Renderer>());
            rig.Renderers.AddRange(rs);
            fx.Decorate(rs, level, false, sparks: false);
            if (WingTier.TryGetValue(path, out var tier) && tier >= 3) GearFx.SetHdr(rs, 1.2f);   // asas de 3º nível
        }

        /// <summary>Nível das asas (1 = Cinza, 2 = Crepúsculo, 3 = Brasalume).</summary>
        private static readonly Dictionary<string, int> WingTier = new Dictionary<string, int>
        {
            { "Wings/Asas_Cinza", 1 }, { "Wings/Asas_Crepusculo", 2 }, { "Wings/Asas_Brasalume", 3 },
        };

        private static void AttachMount(Rig rig, string path, float seat)
        {
            var m = ModelLibrary.Rig(path, "Montaria");
            if (m == null) return;
            m.Root.SetParent(rig.Root, false);
            m.Root.localPosition = Vector3.zero; m.Root.localRotation = Quaternion.identity;
            rig.ExtraLegs.AddRange(m.ExtraLegs);              // a montaria anda; o EntityView balança as pernas
            rig.Renderers.AddRange(m.Renderers);
            rig.Seated = true;
            rig.BodyBase = new Vector3(0, seat - HipHeight + 0.04f, 0);
            rig.Height += rig.BodyBase.y;
        }
    }

    /// <summary>Efeitos do equipamento de um herói: brilho de runa, partículas, aura Ascendente e rastro épico.</summary>
    public sealed class GearFx : MonoBehaviour
    {
        [System.NonSerialized] public Rig Rig = null!;
        private static Material? _fxMat;
        private readonly List<TrailRenderer> _trails = new List<TrailRenderer>();
        private float _trailOff;

        /// <summary>Material das partículas e rastros: o mesmo shader, com cor emissiva vinda do alfa 0 da cor do vértice.</summary>
        public static Material FxMaterial
        {
            get
            {
                if (_fxMat != null) return _fxMat;
                _fxMat = new Material(KezorLook.Material) { name = "KezorFx" };
                _fxMat.SetFloat(HdrId, 1.0f);                 // faíscas e rastros passam de 1 no HDR (o bloom pega)
                return _fxMat;
            }
        }
        public static readonly int HdrId = Shader.PropertyToID("_Hdr");

        /// <summary>Brilho HDR extra pelo refino: +11 = 0,5; +13 = 1,0; +15 = 1,5 (abaixo de +11 não passa de 1).</summary>
        public static float HdrFor(int level) => level >= 11 ? (level - 9) * 0.25f : 0f;

        /// <summary>Soma brilho HDR nas malhas (mantém o que já houver no bloco de propriedades).</summary>
        public static void SetHdr(List<Renderer> rs, float hdr)
        {
            if (hdr <= 0f) return;
            var mpb = new MaterialPropertyBlock();
            foreach (var r in rs)
            {
                r.GetPropertyBlock(mpb);
                mpb.SetFloat(HdrId, Mathf.Max(hdr, mpb.GetFloat(HdrId)));
                r.SetPropertyBlock(mpb);
            }
        }

        private static Color Emissive(Color c) { c.a = 0f; return c; }

        public void Decorate(List<Renderer> rs, int level, bool ascended, bool sparks = true)
        {
            if (rs.Count == 0) return;
            var (glow, color, rate) = Gear.RuneTier(level);
            if (!sparks) glow *= 0.5f;   // armaduras e asas: brilho mais discreto que o da arma
            if (glow > 0f)
            {
                var mpb = new MaterialPropertyBlock();
                foreach (var r in rs)
                {
                    r.GetPropertyBlock(mpb);
                    var (flow, hdr) = RefineFlow(level);
                    // +13/+15: cor HDR no fluxo; o pulso de runa usa a mesma cor, então diminui na mesma proporção
                    float k = hdr.maxColorComponent > 1f ? 1f / hdr.maxColorComponent : 1f;
                    mpb.SetFloat("_Glow", flow > 0f ? glow * Mathf.Max(k, 0.35f) : glow);
                    mpb.SetColor("_GlowColor", flow > 0f ? hdr : color);
                    mpb.SetFloat(RefineFlowId, flow * (sparks ? 1f : 0.85f));
                    mpb.SetFloat(HdrId, Mathf.Max(mpb.GetFloat(HdrId), HdrFor(level) * (sparks ? 1f : 0.6f)));
                    r.SetPropertyBlock(mpb);
                }
            }
            if (sparks && rate > 0f)
            {
                var b = rs[0].bounds;
                foreach (var r in rs) b.Encapsulate(r.bounds);
                var host = rs[0].transform;
                Sparks(host, host.InverseTransformPoint(b.center), b.size * 0.8f, color, rate, 0.07f, -0.12f);
            }
        }

        public static readonly int RefineFlowId = Shader.PropertyToID("_RefineFlow");

        /// <summary>Fluxo de energia do refino (Fresnel + ruído subindo): +11 fraco (brasa), +13 violeta, +15 ciano (HDR).</summary>
        public static (float flow, Color color) RefineFlow(int level)
        {
            if (level >= 15) return (1.2f, new Color(0.25f, 1.6f, 4.0f));
            if (level >= 13) return (1.0f, new Color(0.9f, 0.35f, 3.2f));
            if (level >= 11) return (0.35f, new Color(2.2f, 0.9f, 0.25f));
            return (0f, Color.black);
        }

        /// <summary>Aura no chão para armaduras +11 ou mais: anel de brasa girando e faíscas subindo; +13 dobra; +15 cresce.</summary>
        public void GroundAura((float glow, Color color, float rate) tier, int level)
        {
            int rings = level >= 13 ? 2 : 1;
            for (int k = 0; k < rings; k++)
            {
                var mb = new MeshBuilder();
                float r = 0.62f + k * 0.2f + (level >= 15 ? 0.12f : 0f);
                const int n = 18;
                for (int i = 0; i < n; i++)
                {
                    if (i % 3 == 2) continue;                                       // anel tracejado (runas)
                    float a0 = i * Mathf.PI * 2f / n, a1 = (i + 0.7f) * Mathf.PI * 2f / n;
                    var p0 = new Vector3(Mathf.Cos(a0) * r, 0.03f, Mathf.Sin(a0) * r);
                    var p1 = new Vector3(Mathf.Cos(a1) * r, 0.03f, Mathf.Sin(a1) * r);
                    var mid = (p0 + p1) * 0.5f;
                    mb.Box(mid, new Vector3((p1 - p0).magnitude, 0.015f, 0.05f), KezorLook.Glow(tier.color));
                }
                var go = KezorLook.MakeObject("AuraRunica", mb.ToMesh(), Rig.Root);
                SetHdr(new List<Renderer> { go.GetComponent<Renderer>() }, HdrFor(level) + 0.4f);
                go.AddComponent<Spin>().Speed = k == 0 ? 25f : -40f;
                foreach (var rr in go.GetComponentsInChildren<Renderer>()) Rig.Renderers.Add(rr);
            }
            var ps = Sparks(Rig.Root, new Vector3(0, 0.05f, 0), Vector3.one, tier.color, tier.rate * 0.8f, 0.07f, -0.5f);
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.7f;
            shape.rotation = new Vector3(-90f, 0, 0);
        }

        public void BodySparks((float glow, Color color, float rate) tier)
        {
            Sparks(Rig.Body, new Vector3(0, 1.1f, 0), new Vector3(0.5f, 0.9f, 0.35f), tier.color, tier.rate * 0.6f, 0.06f, -0.2f);
        }

        /// <summary>Conjunto completo: partículas suaves na cor do set subindo pelo corpo e um anel fino nos pés.</summary>
        public void SetAura(Color color)
        {
            Sparks(Rig.Body, new Vector3(0, 0.95f, 0), new Vector3(0.45f, 1.1f, 0.3f), color, 7f, 0.045f, -0.12f);
            var mb = new MeshBuilder();
            const int n = 32; const float r = 0.5f;
            for (int i = 0; i < n; i++)
            {
                float a0 = i * Mathf.PI * 2f / n, a1 = (i + 1) * Mathf.PI * 2f / n;
                var p0 = new Vector3(Mathf.Cos(a0) * r, 0.02f, Mathf.Sin(a0) * r);
                var p1 = new Vector3(Mathf.Cos(a1) * r, 0.02f, Mathf.Sin(a1) * r);
                mb.Box((p0 + p1) * 0.5f, new Vector3((p1 - p0).magnitude, 0.008f, 0.025f), KezorLook.Glow(color));
            }
            var go = KezorLook.MakeObject("AuraConjunto", mb.ToMesh(), Rig.Root);
            SetHdr(new List<Renderer> { go.GetComponent<Renderer>() }, 0.35f);
            go.AddComponent<Spin>().Speed = 12f;
            foreach (var rr in go.GetComponentsInChildren<Renderer>()) Rig.Renderers.Add(rr);
        }

        public void AscendedAura()
        {
            var ps = Sparks(Rig.Root, new Vector3(0, 0.05f, 0), Vector3.one, KezorLook.Hex("#6dff9e"), 14f, 0.08f, -0.35f);
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.65f;
            shape.rotation = new Vector3(-90f, 0, 0);
        }

        /// <summary>Arma épica: partículas próprias e rastro no golpe.</summary>
        public void Epic(Transform piece, List<Renderer> rs, Color color, Transform hand)
        {
            var b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            // ponta = canto dos limites mais longe da mão
            Vector3 handPos = hand.position, tip = b.center;
            float best = -1;
            for (int i = 0; i < 8; i++)
            {
                var c = new Vector3((i & 1) == 0 ? b.min.x : b.max.x, (i & 2) == 0 ? b.min.y : b.max.y, (i & 4) == 0 ? b.min.z : b.max.z);
                float d = (c - handPos).sqrMagnitude;
                if (d > best) { best = d; tip = c; }
            }
            tip = Vector3.Lerp(b.center, tip, 0.8f);
            SetHdr(rs, 1.4f);                                   // a arma épica brilha acima de 1 (bloom forte)
            Sparks(piece, piece.InverseTransformPoint(b.center), b.size * 0.6f, color, 26f, 0.07f, -0.25f);
            Sparks(piece, piece.InverseTransformPoint(b.center), b.size * 0.9f, Color.Lerp(color, Color.white, 0.4f), 8f, 0.12f, -0.1f);   // brilhos grandes

            var t = new GameObject("Rastro").transform;
            t.SetParent(piece, false);
            t.position = tip;
            var tr = t.gameObject.AddComponent<TrailRenderer>();
            tr.sharedMaterial = FxMaterial;
            tr.time = 0.28f;
            tr.minVertexDistance = 0.04f;
            tr.widthMultiplier = 0.32f;
            tr.widthCurve = AnimationCurve.Linear(0, 1, 1, 0);
            tr.startColor = tr.endColor = Emissive(color);
            tr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            tr.receiveShadows = false;
            tr.emitting = false;
            _trails.Add(tr);
            Rig.OnAttack += () => { foreach (var x in _trails) if (x != null) x.emitting = true; _trailOff = Time.time + 0.45f; };
        }

        private void Update()
        {
            if (_trails.Count > 0 && Time.time > _trailOff)
                foreach (var x in _trails) if (x != null && x.emitting) x.emitting = false;
        }

        /// <summary>Faíscas que sobem (gravidade negativa) numa caixa em volta do ponto local dado.</summary>
        public ParticleSystem Sparks(Transform parent, Vector3 localPos, Vector3 boxSize, Color color, float rate, float size, float gravity)
        {
            var go = new GameObject("Faiscas");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.duration = 1f;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 1.1f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.35f);
            main.startSize = new ParticleSystem.MinMaxCurve(size * 0.6f, size * 1.3f);
            main.startColor = Emissive(color);
            main.gravityModifier = gravity;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 80;
            main.playOnAwake = true;
            var em = ps.emission;
            em.rateOverTime = rate;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(Mathf.Max(0.05f, boxSize.x), Mathf.Max(0.05f, boxSize.y), Mathf.Max(0.05f, boxSize.z));
            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 1, 1, 0));
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = FxMaterial;
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            ps.Play();
            return ps;
        }
    }

    /// <summary>Gira o objeto no eixo Y (anéis de aura).</summary>
    public sealed class Spin : MonoBehaviour
    {
        public float Speed = 30f;
        private void Update() => transform.Rotate(0, Speed * Time.deltaTime, 0, Space.Self);
    }

    /// <summary>Mascote: segue o herói por cima do ombro, flutuando; o diabrete bate as asas e o lume gira as orbes.</summary>
    public sealed class PetFollower : MonoBehaviour
    {
        private Transform _target = null!;
        private Rig _pet = null!;
        private Transform? _wingL, _wingR;
        private float _phase;

        public static void Spawn(Rig owner, string path, string name)
        {
            var pet = ModelLibrary.Rig(path, name);
            if (pet == null) return;
            var f = pet.Root.gameObject.AddComponent<PetFollower>();
            f._target = owner.Root;
            f._pet = pet;
            f._wingL = pet.Body.Find("WingL"); f._wingR = pet.Body.Find("WingR");
            f._phase = Random.value * 6f;
            pet.Root.position = owner.Root.position + Vector3.up * 1.6f;
        }

        private void LateUpdate()
        {
            if (_target == null) { Destroy(gameObject); return; }
            float t = Time.time + _phase;
            var want = _target.position + _target.right * 0.85f - _target.forward * 0.45f + Vector3.up * (1.55f + Mathf.Sin(t * 2.2f) * 0.12f);
            transform.position = Vector3.Lerp(transform.position, want, 1f - Mathf.Exp(-5f * Time.deltaTime));
            transform.rotation = Quaternion.Slerp(transform.rotation, _target.rotation, 1f - Mathf.Exp(-4f * Time.deltaTime));
            if (_wingL != null && _wingR != null)
            {
                float f = Mathf.Sin(t * 16f) * 35f;
                _wingL.localRotation = Quaternion.Euler(0, 0, f);
                _wingR.localRotation = Quaternion.Euler(0, 0, -f);
            }
            for (int i = 0; i < _pet.ExtraLegs.Count; i++)
            {   // orbes do lume em volta do núcleo (pivô da orbe está a 0,3 m do centro)
                float a = t * 2f + i * 2.094f;
                _pet.ExtraLegs[i].localPosition = new Vector3(Mathf.Cos(a) * 0.3f, 0.45f + Mathf.Sin(t * 3f + i) * 0.06f, Mathf.Sin(a) * 0.3f);
            }
        }
    }
}
