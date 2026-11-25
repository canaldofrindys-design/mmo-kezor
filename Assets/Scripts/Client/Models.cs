using System.Collections.Generic;
using UnityEngine;
using Kezor.Core.Characters;
using Kezor.Core.Items;

namespace Kezor.Client
{
    /// <summary>Partes animáveis de um modelo (pivôs nas articulações).</summary>
    public sealed class Rig
    {
        public Transform Root = null!, Body = null!;
        public Transform? Head, ArmL, ArmR, LegL, LegR;
        public readonly List<Transform> ExtraLegs = new List<Transform>();
        public readonly List<Renderer> Renderers = new List<Renderer>();
        public float Height = 2f;
        public bool Bouncy;     // lodos
        public bool Hover;      // arauto
        /// <summary>Posição local base do Body (montado: o cavaleiro sobe até a sela).</summary>
        public Vector3 BodyBase;
        /// <summary>Montado: pernas do cavaleiro dobradas e paradas; quem anda é a montaria (ExtraLegs).</summary>
        public bool Seated;
        public Transform? WingL, WingR;
        /// <summary>Chamado quando o personagem golpeia (rastro da arma épica).</summary>
        public System.Action? OnAttack;
    }

    /// <summary>Fábrica de modelos low poly montados de primitivas (personagens, monstros, NPCs, itens).</summary>
    public static class Models
    {
        private static readonly Color Skin = KezorLook.Hex("#d7a982");
        private static readonly Color Steel = KezorLook.Hex("#8e98a3");
        private static readonly Color Leather = KezorLook.Hex("#5a3d2a");
        private static readonly Color Wood = KezorLook.Hex("#6b4a2e");

        private static Transform Part(Rig rig, string name, Transform parent, Vector3 localPos, System.Action<MeshBuilder> build)
        {
            var b = new MeshBuilder(); build(b);
            var go = KezorLook.MakeObject(name, b.ToMesh(name), parent);
            go.transform.localPosition = localPos;
            rig.Renderers.Add(go.GetComponent<Renderer>());
            return go.transform;
        }

        private static Rig NewRig(string name, out Transform body)
        {
            var rig = new Rig();
            rig.Root = new GameObject(name).transform;
            body = new GameObject("Body").transform;
            body.SetParent(rig.Root, false);
            rig.Body = body;
            return rig;
        }

        // ------------------------------------------------------------------ humanoides
        public struct Outfit
        {
            public Color Main, Accent, Pants, Head;
            public bool Robe, Helmet, Hood, Cap;
            public ItemKind Weapon; public bool Shield;
        }

        public static Outfit OutfitFor(CharacterClass c, int weaponId)
        {
            var w = ItemDatabase.Get(weaponId);
            var o = c switch
            {
                CharacterClass.Warden => new Outfit { Main = KezorLook.Hex("#3d5a80"), Accent = KezorLook.Hex("#9b2c2c"), Pants = KezorLook.Hex("#2b2f3a"), Head = Steel, Helmet = true, Weapon = ItemKind.Sword, Shield = true },
                CharacterClass.Arcanist => new Outfit { Main = KezorLook.Hex("#4b2d6b"), Accent = KezorLook.Hex("#d4a94a"), Pants = KezorLook.Hex("#2a1d3a"), Head = KezorLook.Hex("#3a2252"), Robe = true, Hood = true, Weapon = ItemKind.Staff },
                CharacterClass.Bladeweaver => new Outfit { Main = KezorLook.Hex("#3d6f7a"), Accent = KezorLook.Hex("#8fd0ff"), Pants = KezorLook.Hex("#2a2f3a"), Head = KezorLook.Hex("#2a4f5c"), Weapon = ItemKind.Sword },
                CharacterClass.Commander => new Outfit { Main = KezorLook.Hex("#5a3a2a"), Accent = KezorLook.Hex("#8a2a24"), Pants = KezorLook.Hex("#2e2a28"), Head = Steel, Helmet = true, Weapon = ItemKind.Lance },
                CharacterClass.Invoker => new Outfit { Main = KezorLook.Hex("#2f2f5a"), Accent = KezorLook.Hex("#c8ccd8"), Pants = KezorLook.Hex("#1a1f3d"), Head = KezorLook.Hex("#a8b4d0"), Robe = true, Weapon = ItemKind.Tome },
                CharacterClass.EmberFist => new Outfit { Main = KezorLook.Hex("#6a3a22"), Accent = KezorLook.Hex("#c4421a"), Pants = KezorLook.Hex("#3a2a22"), Head = Skin, Weapon = ItemKind.Gauntlet },
                _ => new Outfit { Main = KezorLook.Hex("#3f6b3a"), Accent = KezorLook.Hex("#8a6a3a"), Pants = KezorLook.Hex("#4a3a28"), Head = KezorLook.Hex("#2f4f2c"), Cap = true, Weapon = ItemKind.Bow },
            };
            if (w != null) o.Weapon = w.Kind;
            return o;
        }

        public static Rig Humanoid(string name, Outfit o, float scale = 1f)
        {
            var rig = NewRig(name, out var body);
            rig.Height = 2.1f * scale;
            body.localScale = Vector3.one * scale;
            Part(rig, "Torso", body, Vector3.zero, b =>
            {
                b.Box(new Vector3(0, 1.22f, 0), new Vector3(0.62f, 0.72f, 0.36f), o.Main);
                b.Box(new Vector3(0, 0.9f, 0), new Vector3(0.66f, 0.14f, 0.4f), o.Accent);           // cinto
                b.Box(new Vector3(0, 1.45f, 0.19f), new Vector3(0.2f, 0.2f, 0.02f), o.Accent);       // brasão
                if (o.Robe) b.Cylinder(new Vector3(0, 0.05f, 0), 0.5f, 0.34f, 0.9f, 7, o.Main * 0.9f + new Color(0, 0, 0, 0.1f));
                if (o.Helmet) { b.Box(new Vector3(-0.38f, 1.55f, 0), new Vector3(0.24f, 0.18f, 0.42f), Steel); b.Box(new Vector3(0.38f, 1.55f, 0), new Vector3(0.24f, 0.18f, 0.42f), Steel); }
            });
            rig.Head = Part(rig, "Head", body, new Vector3(0, 1.6f, 0), b =>
            {
                b.Box(new Vector3(0, 0.2f, 0), new Vector3(0.34f, 0.36f, 0.34f), Skin);
                b.Box(new Vector3(-0.08f, 0.22f, 0.171f), new Vector3(0.06f, 0.05f, 0.01f), KezorLook.Hex("#1a1a1a"));
                b.Box(new Vector3(0.08f, 0.22f, 0.171f), new Vector3(0.06f, 0.05f, 0.01f), KezorLook.Hex("#1a1a1a"));
                if (o.Helmet)
                {
                    b.Box(new Vector3(0, 0.3f, -0.02f), new Vector3(0.4f, 0.26f, 0.4f), o.Head);
                    b.Box(new Vector3(0, 0.5f, -0.02f), new Vector3(0.06f, 0.16f, 0.42f), o.Accent);  // crista
                }
                if (o.Hood) b.Cone(new Vector3(0, 0.1f, -0.03f), 0.3f, 0.65f, 6, o.Head);
                if (o.Cap) { b.Box(new Vector3(0, 0.4f, 0), new Vector3(0.38f, 0.1f, 0.38f), o.Head); b.Box(new Vector3(0.12f, 0.55f, -0.1f), new Vector3(0.03f, 0.3f, 0.08f), KezorLook.Hex("#c94f2a")); }
            });
            rig.ArmL = Part(rig, "ArmL", body, new Vector3(-0.42f, 1.52f, 0), b =>
            {
                b.Box(new Vector3(0, -0.3f, 0), new Vector3(0.2f, 0.66f, 0.22f), o.Main);
                b.Box(new Vector3(0, -0.66f, 0), new Vector3(0.18f, 0.14f, 0.2f), Skin);
                if (o.Shield) { b.Box(new Vector3(-0.14f, -0.45f, 0.05f), new Vector3(0.07f, 0.7f, 0.55f), o.Accent); b.Box(new Vector3(-0.18f, -0.45f, 0.05f), new Vector3(0.02f, 0.3f, 0.25f), KezorLook.Hex("#d4a94a")); }
                if (o.Weapon == ItemKind.Bow)
                {
                    for (int i = -3; i <= 3; i++)
                        b.Box(new Vector3(-0.05f, -0.66f + i * 0.17f, 0.12f + 0.18f - Mathf.Abs(i) * 0.06f), new Vector3(0.05f, 0.18f, 0.05f), Wood);
                    b.Box(new Vector3(-0.05f, -0.66f, 0.12f), new Vector3(0.01f, 1.1f, 0.01f), KezorLook.Hex("#ddd"));
                }
            });
            rig.ArmR = Part(rig, "ArmR", body, new Vector3(0.42f, 1.52f, 0), b =>
            {
                b.Box(new Vector3(0, -0.3f, 0), new Vector3(0.2f, 0.66f, 0.22f), o.Main);
                b.Box(new Vector3(0, -0.66f, 0), new Vector3(0.18f, 0.14f, 0.2f), Skin);
                switch (o.Weapon)
                {
                    case ItemKind.Sword:
                        b.Box(new Vector3(0, -0.7f, 0.5f), new Vector3(0.06f, 0.08f, 0.9f), KezorLook.Hex("#c7cfd8"));
                        b.Box(new Vector3(0, -0.7f, 0.06f), new Vector3(0.3f, 0.08f, 0.06f), KezorLook.Hex("#d4a94a"));
                        break;
                    case ItemKind.Axe:
                        b.Box(new Vector3(0, -0.7f, 0.35f), new Vector3(0.06f, 0.06f, 0.8f), Wood);
                        b.Box(new Vector3(0, -0.6f, 0.65f), new Vector3(0.05f, 0.35f, 0.25f), Steel);
                        break;
                    case ItemKind.Staff:
                        b.Box(new Vector3(0, -0.4f, 0.08f), new Vector3(0.06f, 1.6f, 0.06f), Wood);
                        b.Gem(new Vector3(0, 0.5f, 0.08f), 0.12f, 0.16f, KezorLook.Glow(KezorLook.Hex("#9f7bff")));
                        break;
                }
            });
            rig.LegL = Part(rig, "LegL", body, new Vector3(-0.15f, 0.85f, 0), b =>
            {
                b.Box(new Vector3(0, -0.4f, 0), new Vector3(0.24f, 0.8f, 0.26f), o.Pants);
                b.Box(new Vector3(0, -0.78f, 0.04f), new Vector3(0.26f, 0.12f, 0.34f), Leather);
            });
            rig.LegR = Part(rig, "LegR", body, new Vector3(0.15f, 0.85f, 0), b =>
            {
                b.Box(new Vector3(0, -0.4f, 0), new Vector3(0.24f, 0.8f, 0.26f), o.Pants);
                b.Box(new Vector3(0, -0.78f, 0.04f), new Vector3(0.26f, 0.12f, 0.34f), Leather);
            });
            return rig;
        }

        // ------------------------------------------------------------------ modelos do Blender (Resources/Models)
        public static string ClassModel(CharacterClass c) => c switch
        {
            CharacterClass.Warden => "Classes/GuardiaoRunico",
            CharacterClass.Arcanist => "Classes/ArcanistaDoVeu",
            CharacterClass.Bladeweaver => "Classes/LaminaArcana",
            CharacterClass.Commander => "Classes/ComandanteEstandarte",
            CharacterClass.Invoker => "Classes/InvocadoraEcos",
            CharacterClass.EmberFist => "Classes/PunhoBrasa",
            _ => "Classes/PatrulheiroSelvagem",
        };

        public static string NpcModel(string role) => role switch
        {
            "shop" => "Npcs/MercadoraTalia",
            "forge" => "Npcs/FerreiroBram",
            "vault" => "Npcs/IlsaGuardia",
            "guild" => "Npcs/HadrikGuildas",
            _ => "Npcs/SentinelaOrvo",
        };

        /// <summary>Id do MonsterDefinition → FBX em Resources/Models/Monsters.</summary>
        public static readonly Dictionary<int, string> MonsterModels = new Dictionary<int, string>
        {
            { 1, "Monsters/LodoMusgoso" }, { 2, "Monsters/LoboCinzento" }, { 3, "Monsters/AranhaTecela" },
            { 4, "Monsters/OssadaErrante" }, { 5, "Monsters/GolemDeBasalto" }, { 50, "Monsters/CriaDasCinzas" }, { 51, "Monsters/ArautoDasCinzas" },
            { 6, "Monsters/RastejanteTurfa" }, { 7, "Monsters/BruxaJunco" }, { 8, "Monsters/CarnicalAfogado" }, { 9, "Monsters/MariposaFatua" },
            { 10, "Monsters/SentinelaVidro" }, { 11, "Monsters/AranhaObsidiana" }, { 12, "Monsters/EspectroVitreo" }, { 52, "Monsters/SoberanoVidroNegro" },
            { 60, "Monsters/EcoSombrio" }, { 61, "Monsters/EcoLuminoso" },
        };

        private static readonly Dictionary<int, string> MonsterNames = new Dictionary<int, string>
        {
            { 1, "Lodo Musgoso" }, { 2, "Lobo Cinzento" }, { 3, "Aranha Tecelã" }, { 4, "Ossada Errante" },
            { 5, "Golem de Basalto" }, { 50, "Cria das Cinzas" }, { 51, "Arauto das Cinzas" },
        };

        /// <summary>Jogador: corpo base da classe + equipamento visível (ver <see cref="Gear"/>); procedural se não houver FBX.</summary>
        public static Rig Player(string name, CharacterClass c, Appearance look) => Gear.Player(name, c, look);

        public static Rig Npc(string name, string role)
        {
            if (role == "deco")
            {   // moradores: corpo base polido de um herói (sem armadura), escolhido pelo nome
                var classes = Kezor.Core.Characters.ClassDefinition.All;
                int h = 0; foreach (var ch in name) h = h * 31 + ch;
                var cls = classes[(h & 0x7fffffff) % classes.Length].Class;
                return Gear.Player(name, cls, new Kezor.Core.Items.Appearance());
            }
            var fbx = ModelLibrary.Rig(NpcModel(role), name);
            if (fbx != null) return fbx;
            var o = role switch
            {
                "shop" => new Outfit { Main = KezorLook.Hex("#8c5a2b"), Accent = KezorLook.Hex("#e0c070"), Pants = KezorLook.Hex("#4a3424"), Head = KezorLook.Hex("#6b3a5a"), Hood = true, Weapon = ItemKind.Potion },
                "forge" => new Outfit { Main = KezorLook.Hex("#5a4a3a"), Accent = KezorLook.Hex("#2a2a2a"), Pants = KezorLook.Hex("#3a3028"), Head = Skin, Weapon = ItemKind.Axe },
                _ => new Outfit { Main = KezorLook.Hex("#6b6b70"), Accent = KezorLook.Hex("#ffae42"), Pants = KezorLook.Hex("#2b2f3a"), Head = Steel, Helmet = true, Shield = true, Weapon = ItemKind.Sword },
            };
            return Humanoid(name, o, role == "forge" ? 1.1f : 1f);
        }

        // ------------------------------------------------------------------ estandarte do Comandante
        public static Color BannerColor(int kind) => kind switch
        {
            1 => KezorLook.Hex("#ff5a3a"),   // fúria (dano)
            2 => KezorLook.Hex("#6ab0ff"),   // muralha (defesa)
            _ => KezorLook.Hex("#7aff9e"),   // vento (velocidade)
        };

        /// <summary>Mastro com bandeira da cor da aura e um anel brilhante no chão marcando o raio.</summary>
        public static Rig Banner(int kind, float radius)
        {
            var rig = NewRig("Estandarte", out var body);
            rig.Height = 3.2f;
            var col = BannerColor(kind);
            Part(rig, "Mastro", body, Vector3.zero, b =>
            {
                b.Cylinder(new Vector3(0, 0, 0), 0.22f, 0.16f, 0.25f, 6, KezorLook.Hex("#5a4a3a"));
                b.Cylinder(new Vector3(0, 0.2f, 0), 0.05f, 0.04f, 2.9f, 6, Wood);
                b.Gem(new Vector3(0, 3.15f, 0), 0.1f, 0.22f, KezorLook.Glow(col));
                b.Box(new Vector3(0.36f, 2.6f, 0), new Vector3(0.66f, 0.62f, 0.03f), col * 0.85f + new Color(0, 0, 0, 0.15f));
                b.Box(new Vector3(0.36f, 2.6f, -0.02f), new Vector3(0.22f, 0.22f, 0.01f), KezorLook.Hex("#d4a94a"));
                const int n = 28;
                for (int i = 0; i < n; i++)
                {
                    float a = i * Mathf.PI * 2f / n;
                    b.Box(new Vector3(Mathf.Cos(a) * radius, 0.04f, Mathf.Sin(a) * radius), new Vector3(0.5f, 0.05f, 0.12f), KezorLook.Glow(col));
                }
            });
            // os segmentos do anel giram junto com o corpo; alinha cada um tangente ao círculo não é preciso no low poly
            return rig;
        }

        // ------------------------------------------------------------------ monstros
        public static Rig Monster(int defId, float scale)
        {
            Rig? rig = null;
            if (MonsterModels.TryGetValue(defId, out var path))
                rig = ModelLibrary.Rig(path, Kezor.Core.World.MonsterCatalog.Get(defId)?.Name ?? path, bouncy: defId == 1,
                                       hover: defId == 51 || (Kezor.Core.World.MonsterCatalog.Get(defId)?.Hover ?? false));
            if (rig == null) switch (defId)
            {
                case 1: rig = Slime(); break;
                case 2: rig = Wolf(); break;
                case 3: rig = Spider(); break;
                case 4: rig = Skeleton(); break;
                case 5: rig = Golem(); break;
                case 50: rig = Ashling(); break;
                case 51: rig = Herald(); break;
                default: rig = Slime(); break;
            }
            rig.Body.localScale *= scale;
            rig.Height *= scale;
            return rig;
        }

        private static Rig Slime()
        {
            var rig = NewRig("Lodo Musgoso", out var body);
            rig.Height = 1.1f; rig.Bouncy = true;
            Part(rig, "Gel", body, Vector3.zero, b =>
            {
                b.Ico(new Vector3(0, 0.42f, 0), new Vector3(0.62f, 0.45f, 0.62f), KezorLook.Hex("#6fa83a"), 0.12f, 3, KezorLook.Hex("#5a9030"));
                b.Ico(new Vector3(0.2f, 0.75f, -0.1f), new Vector3(0.15f, 0.08f, 0.15f), KezorLook.Hex("#3f6b2a"), 0.2f, 5);
                b.Box(new Vector3(-0.17f, 0.55f, 0.5f), new Vector3(0.1f, 0.14f, 0.05f), KezorLook.Glow(KezorLook.Hex("#f4ff9a")));
                b.Box(new Vector3(0.17f, 0.55f, 0.5f), new Vector3(0.1f, 0.14f, 0.05f), KezorLook.Glow(KezorLook.Hex("#f4ff9a")));
            });
            return rig;
        }

        private static Rig Wolf()
        {
            var rig = NewRig("Lobo Cinzento", out var body);
            rig.Height = 1.3f;
            var fur = KezorLook.Hex("#6b6966"); var dark = KezorLook.Hex("#45433f");
            Part(rig, "Torso", body, Vector3.zero, b =>
            {
                b.Box(new Vector3(0, 0.75f, 0), new Vector3(0.5f, 0.5f, 1.15f), fur, dark);
                b.Box(new Vector3(0, 0.9f, 0.35f), new Vector3(0.58f, 0.5f, 0.5f), dark);
                b.SetTRS(new Vector3(0, 0.85f, -0.6f), Quaternion.Euler(-35, 0, 0), Vector3.one);
                b.Box(new Vector3(0, 0, -0.25f), new Vector3(0.14f, 0.14f, 0.55f), dark);
            });
            rig.Head = Part(rig, "Head", body, new Vector3(0, 1.0f, 0.65f), b =>
            {
                b.Box(new Vector3(0, 0, 0.1f), new Vector3(0.38f, 0.34f, 0.38f), fur);
                b.Box(new Vector3(0, -0.06f, 0.38f), new Vector3(0.2f, 0.18f, 0.3f), dark);
                b.Cone(new Vector3(-0.12f, 0.16f, 0.02f), 0.08f, 0.2f, 4, dark);
                b.Cone(new Vector3(0.12f, 0.16f, 0.02f), 0.08f, 0.2f, 4, dark);
                b.Box(new Vector3(-0.1f, 0.05f, 0.3f), new Vector3(0.06f, 0.04f, 0.02f), KezorLook.Glow(KezorLook.Hex("#ff4a2a")));
                b.Box(new Vector3(0.1f, 0.05f, 0.3f), new Vector3(0.06f, 0.04f, 0.02f), KezorLook.Glow(KezorLook.Hex("#ff4a2a")));
            });
            Vector3[] legs = { new Vector3(-0.18f, 0.55f, 0.38f), new Vector3(0.18f, 0.55f, 0.38f), new Vector3(-0.18f, 0.55f, -0.4f), new Vector3(0.18f, 0.55f, -0.4f) };
            for (int i = 0; i < 4; i++)
                rig.ExtraLegs.Add(Part(rig, "Leg" + i, body, legs[i], b => b.Box(new Vector3(0, -0.27f, 0), new Vector3(0.13f, 0.56f, 0.15f), dark)));
            return rig;
        }

        private static Rig Spider()
        {
            var rig = NewRig("Aranha Tecelã", out var body);
            rig.Height = 1.2f;
            var shell = KezorLook.Hex("#2e2433"); var mark = KezorLook.Hex("#7a3b8f");
            Part(rig, "Corpo", body, Vector3.zero, b =>
            {
                b.Ico(new Vector3(0, 0.75f, -0.45f), new Vector3(0.55f, 0.45f, 0.65f), shell, 0.1f, 9, mark);
                b.Ico(new Vector3(0, 0.6f, 0.25f), new Vector3(0.32f, 0.25f, 0.35f), shell, 0.1f, 11);
                for (int i = 0; i < 4; i++) b.Box(new Vector3(-0.09f + (i % 2) * 0.18f, 0.7f + (i / 2) * 0.08f, 0.56f), new Vector3(0.06f, 0.06f, 0.04f), KezorLook.Glow(KezorLook.Hex("#8dff6a")));
            });
            for (int i = 0; i < 8; i++)
            {
                int side = i < 4 ? -1 : 1; int k = i % 4;
                var leg = Part(rig, "Leg" + i, body, new Vector3(side * 0.25f, 0.62f, 0.35f - k * 0.2f), b =>
                {
                    b.SetTRS(Vector3.zero, Quaternion.Euler(0, 0, side * -55f), Vector3.one);
                    b.Box(new Vector3(0, -0.3f, 0), new Vector3(0.07f, 0.6f, 0.07f), shell);
                    b.SetTRS(new Vector3(side * 0.5f, -0.33f, 0), Quaternion.Euler(0, 0, side * 20f), Vector3.one);
                    b.Box(new Vector3(0, -0.3f, 0), new Vector3(0.06f, 0.6f, 0.06f), mark);
                });
                leg.localRotation = Quaternion.Euler(0, (k - 1.5f) * -20f * side, 0);
                rig.ExtraLegs.Add(leg);
            }
            return rig;
        }

        private static Rig Skeleton()
        {
            var bone = KezorLook.Hex("#d9d2bf"); var rust = KezorLook.Hex("#7a5034");
            var o = new Outfit { Main = bone, Accent = rust, Pants = bone, Head = bone, Weapon = ItemKind.Sword };
            var rig = Humanoid("Ossada Errante", o, 1.05f);
            // costelas e olhos brilhantes por cima do corpo base
            Part(rig, "Detalhes", rig.Body, Vector3.zero, b =>
            {
                for (int i = 0; i < 4; i++) b.Box(new Vector3(0, 1.05f + i * 0.13f, 0.19f), new Vector3(0.66f, 0.05f, 0.03f), KezorLook.Hex("#3a3328"));
                b.Box(new Vector3(-0.08f, 1.83f, 0.18f), new Vector3(0.07f, 0.06f, 0.02f), KezorLook.Glow(KezorLook.Hex("#6ad7ff")));
                b.Box(new Vector3(0.08f, 1.83f, 0.18f), new Vector3(0.07f, 0.06f, 0.02f), KezorLook.Glow(KezorLook.Hex("#6ad7ff")));
            });
            return rig;
        }

        private static Rig Golem()
        {
            var rig = NewRig("Golem de Basalto", out var body);
            rig.Height = 2.6f;
            var rock = KezorLook.Hex("#3d3a3a"); var rock2 = KezorLook.Hex("#2c2a2a"); var lava = KezorLook.Glow(KezorLook.Hex("#ff6a1a"));
            Part(rig, "Torso", body, Vector3.zero, b =>
            {
                b.Ico(new Vector3(0, 1.45f, 0), new Vector3(0.8f, 0.7f, 0.6f), rock, 0.2f, 21, rock2);
                b.Box(new Vector3(0, 1.5f, 0.52f), new Vector3(0.25f, 0.4f, 0.1f), lava);
                b.Ico(new Vector3(0, 0.75f, 0), new Vector3(0.55f, 0.35f, 0.45f), rock2, 0.2f, 22);
            });
            rig.Head = Part(rig, "Head", body, new Vector3(0, 2.1f, 0.1f), b =>
            {
                b.Box(new Vector3(0, 0.15f, 0), new Vector3(0.45f, 0.35f, 0.4f), rock);
                b.Box(new Vector3(-0.1f, 0.17f, 0.2f), new Vector3(0.1f, 0.06f, 0.03f), lava);
                b.Box(new Vector3(0.1f, 0.17f, 0.2f), new Vector3(0.1f, 0.06f, 0.03f), lava);
            });
            rig.ArmL = Part(rig, "ArmL", body, new Vector3(-0.85f, 1.8f, 0), b => { b.Ico(new Vector3(0, -0.5f, 0), new Vector3(0.32f, 0.6f, 0.32f), rock, 0.2f, 23, rock2); b.Ico(new Vector3(0, -1.15f, 0.05f), new Vector3(0.38f, 0.3f, 0.38f), rock2, 0.2f, 24); });
            rig.ArmR = Part(rig, "ArmR", body, new Vector3(0.85f, 1.8f, 0), b => { b.Ico(new Vector3(0, -0.5f, 0), new Vector3(0.32f, 0.6f, 0.32f), rock, 0.2f, 25, rock2); b.Ico(new Vector3(0, -1.15f, 0.05f), new Vector3(0.38f, 0.3f, 0.38f), rock2, 0.2f, 26); });
            rig.LegL = Part(rig, "LegL", body, new Vector3(-0.3f, 0.7f, 0), b => b.Box(new Vector3(0, -0.35f, 0), new Vector3(0.38f, 0.7f, 0.42f), rock2));
            rig.LegR = Part(rig, "LegR", body, new Vector3(0.3f, 0.7f, 0), b => b.Box(new Vector3(0, -0.35f, 0), new Vector3(0.38f, 0.7f, 0.42f), rock2));
            return rig;
        }

        private static Rig Ashling()
        {
            var o = new Outfit { Main = KezorLook.Hex("#2a2422"), Accent = KezorLook.Glow(KezorLook.Hex("#ff7a2a"), 0.8f), Pants = KezorLook.Hex("#1e1a19"), Head = KezorLook.Hex("#2a2422"), Hood = true, Weapon = ItemKind.Axe };
            var rig = Humanoid("Cria das Cinzas", o, 0.85f);
            return rig;
        }

        private static Rig Herald()
        {
            var rig = NewRig("Arauto das Cinzas", out var body);
            rig.Height = 3f; rig.Hover = true;
            var robe = KezorLook.Hex("#1f1a1a"); var ember = KezorLook.Glow(KezorLook.Hex("#ff5a1a")); var gold = KezorLook.Hex("#b58a3a");
            Part(rig, "Manto", body, Vector3.zero, b =>
            {
                b.Cylinder(new Vector3(0, 0.2f, 0), 0.75f, 0.35f, 1.7f, 7, robe);
                b.Box(new Vector3(0, 1.75f, 0), new Vector3(0.8f, 0.35f, 0.45f), robe);
                b.Box(new Vector3(0, 1.2f, 0.34f), new Vector3(0.15f, 0.7f, 0.05f), ember);
            });
            rig.Head = Part(rig, "Cabeça", body, new Vector3(0, 2.0f, 0), b =>
            {
                b.Box(new Vector3(0, 0.2f, 0), new Vector3(0.38f, 0.42f, 0.38f), KezorLook.Hex("#3a2f2a"));
                b.Box(new Vector3(-0.09f, 0.22f, 0.2f), new Vector3(0.08f, 0.05f, 0.02f), ember);
                b.Box(new Vector3(0.09f, 0.22f, 0.2f), new Vector3(0.08f, 0.05f, 0.02f), ember);
                for (int i = 0; i < 5; i++)
                {
                    float a = (i / 5f) * Mathf.PI * 2;
                    b.Cone(new Vector3(Mathf.Cos(a) * 0.2f, 0.42f, Mathf.Sin(a) * 0.2f), 0.06f, 0.35f, 4, i % 2 == 0 ? gold : ember);
                }
            });
            rig.ArmL = Part(rig, "ArmL", body, new Vector3(-0.55f, 1.8f, 0), b => b.Box(new Vector3(0, -0.45f, 0), new Vector3(0.22f, 0.9f, 0.24f), robe));
            rig.ArmR = Part(rig, "ArmR", body, new Vector3(0.55f, 1.8f, 0), b =>
            {
                b.Box(new Vector3(0, -0.45f, 0), new Vector3(0.22f, 0.9f, 0.24f), robe);
                b.Box(new Vector3(0, -0.6f, 0.1f), new Vector3(0.07f, 2.2f, 0.07f), gold);
                b.Gem(new Vector3(0, 0.55f, 0.1f), 0.2f, 0.3f, ember);
            });
            for (int i = 0; i < 3; i++)
            {
                float a = i / 3f * Mathf.PI * 2;
                rig.ExtraLegs.Add(Part(rig, "Orbe" + i, body, new Vector3(Mathf.Cos(a) * 1.1f, 1.6f, Mathf.Sin(a) * 1.1f), b => b.Ico(Vector3.zero, Vector3.one * 0.16f, ember)));
            }
            return rig;
        }

        // ------------------------------------------------------------------ itens no chão
        public static Color RuneColor(RuneType t) => t switch
        {
            RuneType.Dawn => KezorLook.Hex("#ffd36a"),
            RuneType.Eclipse => KezorLook.Hex("#a77bff"),
            RuneType.Vital => KezorLook.Hex("#5cff8a"),
            RuneType.Vortex => KezorLook.Hex("#4fe3ff"),
            _ => Color.white
        };

        public static Rig Drop(ItemDefinition def, ItemRarity rarity)
        {
            var rig = NewRig("Drop " + def.Name, out var body);
            rig.Height = 0.8f;
            Part(rig, "Item", body, Vector3.zero, b =>
            {
                if (def.IsRune) b.Gem(new Vector3(0, 0.45f, 0), 0.18f, 0.28f, KezorLook.Glow(RuneColor(def.Rune)));
                else if (def.IsPotion)
                {
                    var c = def.Potion == PotionType.Health ? KezorLook.Hex("#c0262e") : KezorLook.Hex("#2c56c9");
                    b.Cylinder(new Vector3(0, 0.2f, 0), 0.14f, 0.12f, 0.25f, 6, KezorLook.Glow(c, 0.5f));
                    b.Cylinder(new Vector3(0, 0.45f, 0), 0.05f, 0.05f, 0.1f, 5, KezorLook.Hex("#8a6a3a"));
                }
                else if (def.Kind == ItemKind.Material)          // Pena de Brasa
                {
                    b.SetTRS(new Vector3(0, 0.4f, 0), Quaternion.Euler(0, 0, 25), Vector3.one);
                    b.Box(Vector3.zero, new Vector3(0.12f, 0.5f, 0.03f), KezorLook.Glow(KezorLook.Hex("#ff8a2a"), 0.8f));
                    b.Box(new Vector3(0, -0.3f, 0), new Vector3(0.02f, 0.2f, 0.02f), KezorLook.Hex("#4a3424"));
                }
                else if (def.IsWings)
                {
                    for (int s = -1; s <= 1; s += 2)
                    {
                        b.SetTRS(new Vector3(s * 0.2f, 0.4f, 0), Quaternion.Euler(0, 0, s * -30f), Vector3.one);
                        b.Box(Vector3.zero, new Vector3(0.4f, 0.25f, 0.04f), KezorLook.Glow(KezorLook.Hex("#c4421a"), 0.4f));
                    }
                }
                else if (def.IsWeapon)
                {
                    b.SetTRS(new Vector3(0, 0.35f, 0), Quaternion.Euler(0, 0, 70), Vector3.one);
                    b.Box(new Vector3(0, 0.15f, 0), new Vector3(0.07f, 0.8f, 0.05f), def.Kind == ItemKind.Staff || def.Kind == ItemKind.Bow ? Wood : KezorLook.Hex("#c7cfd8"));
                    b.Box(new Vector3(0, -0.22f, 0), new Vector3(0.25f, 0.06f, 0.06f), KezorLook.Hex("#d4a94a"));
                }
                else
                {
                    b.Box(new Vector3(0, 0.25f, 0), new Vector3(0.45f, 0.35f, 0.3f), def.Kind == ItemKind.Shield ? KezorLook.Hex("#9b2c2c") : KezorLook.Hex("#7d6a55"));
                }
                if (rarity == ItemRarity.Ascended)
                {
                    b.ResetMatrix();
                    b.Cylinder(Vector3.zero, 0.05f, 0.02f, 2.2f, 4, KezorLook.Glow(KezorLook.Hex("#6dff9e")));
                    b.Ring(new Vector3(0, 0.03f, 0), 0.35f, 0.45f, 10, KezorLook.Glow(KezorLook.Hex("#6dff9e"), 0.8f));
                }
            });
            return rig;
        }
    }
}
