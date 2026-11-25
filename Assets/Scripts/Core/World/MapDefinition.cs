using System;
using System.Collections.Generic;

namespace Kezor.Core.World
{
    public sealed class SpawnGroup
    {
        public MonsterDefinition Monster = null!;
        public float X, Z, Radius;
        public int Count;
    }

    public sealed class NpcDefinition
    {
        public int Id;
        public string Name = "";
        public string Role = "";   // "shop" | "forge" | "guard" | "vault" | "guild" | "deco" (morador)
        public float X, Z;
        /// <summary>Fala ao clicar (moradores e guardas).</summary>
        public string Greeting = "";
    }

    /// <summary>Obstáculo circular no chão (torre, casa, fonte). R já é o raio do objeto, sem o corpo de quem anda.</summary>
    public struct Obstacle
    {
        public float X, Z, R;
        public Obstacle(float x, float z, float r) { X = x; Z = z; R = r; }
    }

    /// <summary>Casa da vila: posição, ângulo em volta da praça (graus, 0 = +X) e escala. O cliente usa para desenhar.</summary>
    public struct TownHouse
    {
        public float X, Z, Angle, Scale;
        /// <summary>Modelo (Casa2A, Casa2B, Casa2C, Sobrado2).</summary>
        public string Kind;
    }

    /// <summary>Peça de cenário da vila (barraca, forja, templo, poste, banco...). R &gt; 0 bloqueia o passo.</summary>
    public struct TownProp
    {
        public string Kind;
        public float X, Z, Yaw, Scale, R;
    }

    /// <summary>Portal: pisar no círculo leva a outro mapa (se o nível permitir). Grátis, ao contrário do /warp.</summary>
    public sealed class Portal
    {
        public float X, Z, Radius = 2.0f;   // arco do portal em escala 1,8
        public int TargetMap;
        public float TargetX, TargetZ;
        public string Label = "";
    }

    /// <summary>Retângulo de chão andável (masmorras): fora de todos eles é parede.</summary>
    public struct FloorRect
    {
        public float X0, Z0, X1, Z1;
        public FloorRect(float x0, float z0, float x1, float z1) { X0 = x0; Z0 = z0; X1 = x1; Z1 = z1; }
        public bool Contains(float x, float z, float margin) => x >= X0 + margin && x <= X1 - margin && z >= Z0 + margin && z <= Z1 - margin;
    }

    /// <summary>Mapa: área segura (cidade), ponto de renascer, grupos de spawn e NPCs.</summary>
    public sealed class MapDefinition
    {
        public int Id;
        public string Name = "";
        public float Size;
        public float SafeX, SafeZ, SafeRadius;
        public float RespawnX, RespawnZ;
        public readonly List<SpawnGroup> Spawns = new List<SpawnGroup>();
        public readonly List<NpcDefinition> Npcs = new List<NpcDefinition>();
        public readonly List<Portal> Portals = new List<Portal>();
        /// <summary>Masmorra: só se anda dentro destes retângulos. Vazio = mapa aberto.</summary>
        public readonly List<FloorRect> Floors = new List<FloorRect>();
        /// <summary>Nível mínimo para entrar (portal ou /warp) e custo do /warp em ouro.</summary>
        public int MinLevel = 1;
        /// <summary>Versão da planta do mapa. Mudou a planta = posições salvas antigas viram o ponto de renascer.</summary>
        public int LayoutVersion = 1;
        public long WarpCost;
        public string WarpCode = "";

        // Muralha circular com 4 portões (N, S, L, O) e obstáculos. O servidor usa para bloquear o movimento;
        // o cliente (WorldBuilder) usa os mesmos dados para posicionar muralha, torres e casas.
        public bool HasWall;
        public float WallRadius, WallHalfThickness, GateHalfAngle, TowerAngle, TowerRadius;
        public readonly List<Obstacle> Obstacles = new List<Obstacle>();
        public readonly List<TownHouse> Houses = new List<TownHouse>();
        /// <summary>Raio do corpo de personagens e monstros para a colisão.</summary>
        public const float BodyRadius = 0.35f;
        /// <summary>
        /// Vila de Brasalume v2: muralha de raio 45 m (área ~3,5× a anterior) com praça da fonte (renascer), anel de ruas,
        /// distritos (mercado, forja, templo, estábulo), dois anéis de casas e postes ao longo das ruas. Prédios em
        /// tamanho real (o herói tem 1,8 m); colisão e posições vêm daqui para servidor e cliente.
        /// </summary>
        public const float TownScale = 1.0f;
        /// <summary>Raio da praça calçada em volta da fonte.</summary>
        public const float PlazaRadius = 11f;
        /// <summary>Raio da bacia da Fonte de Brasas (obstáculo).</summary>
        public const float FountainRadius = 4.0f;
        /// <summary>Rua circular que liga os distritos.</summary>
        public const float RingRoadRadius = 20f;
        /// <summary>Braseiros ladeando os portões, por dentro da muralha: recuo da muralha e afastamento do eixo da rua.</summary>
        public const float BrazierInset = 4f, BrazierSide = 5.5f, BrazierRadius = 0.5f;
        /// <summary>Centro do Vale (o mapa tem 260 m).</summary>
        public const float ValeCenter = 130f;

        /// <summary>Peças de cenário da vila (o cliente desenha; as com R &gt; 0 também bloqueiam).</summary>
        public readonly List<TownProp> Props = new List<TownProp>();

        public bool InSafeZone(float x, float z) => SafeRadius > 0 && MathK.Dist(x, z, SafeX, SafeZ) <= SafeRadius;
        public float ClampCoord(float v) => MathK.Clamp(v, 1f, Size - 1f);

        /// <summary>Ângulo (graus) até o portão mais próximo, visto do centro da vila.</summary>
        public float GateDistance(float x, float z)
        {
            float ang = (float)(Math.Atan2(z - SafeZ, x - SafeX) * 180.0 / Math.PI);
            float rel = ang + 45f;
            rel -= (float)Math.Floor(rel / 90f) * 90f;
            return Math.Abs(rel - 45f);
        }

        /// <summary>True se um corpo em (x, z) atravessaria a muralha ou um obstáculo.</summary>
        public bool IsBlocked(float x, float z)
        {
            if (Floors.Count > 0)
            {
                bool inside = false;
                foreach (var f in Floors) if (f.Contains(x, z, BodyRadius)) { inside = true; break; }
                if (!inside) return true;
            }
            if (HasWall)
            {
                float d = MathK.Dist(x, z, SafeX, SafeZ);
                if (Math.Abs(d - WallRadius) < WallHalfThickness + BodyRadius && GateDistance(x, z) > GateHalfAngle) return true;
            }
            foreach (var o in Obstacles)
                if (MathK.Dist(x, z, o.X, o.Z) < o.R + BodyRadius) return true;
            return false;
        }

        private static readonly float[] DetourAngles = { 25f, -25f, 50f, -50f, 75f, -75f, 100f, -100f };

        /// <summary>
        /// Anda `step` metros na direção (dirX, dirZ) respeitando a colisão. Se o passo direto bater, tenta desviar
        /// girando a direção (±25°, ±50°...), o que contorna círculos e desliza na muralha. Quem já está dentro de algo
        /// bloqueado anda livre para poder sair. Retorna false se não conseguiu andar.
        /// </summary>
        public bool TryStep(ref float x, ref float z, float dirX, float dirZ, float step)
        {
            float nx = ClampCoord(x + dirX * step), nz = ClampCoord(z + dirZ * step);
            if (!IsBlocked(nx, nz) || IsBlocked(x, z)) { x = nx; z = nz; return true; }
            foreach (var deg in DetourAngles)
            {
                double r = deg * Math.PI / 180.0;
                float cs = (float)Math.Cos(r), sn = (float)Math.Sin(r);
                float rx = dirX * cs - dirZ * sn, rz = dirX * sn + dirZ * cs;
                nx = ClampCoord(x + rx * step); nz = ClampCoord(z + rz * step);
                if (!IsBlocked(nx, nz)) { x = nx; z = nz; return true; }
            }
            return false;
        }

        /// <summary>True se dá para andar em linha reta de A a B (amostra a cada 0,4 m).</summary>
        public bool LineClear(float ax, float az, float bx, float bz)
        {
            float d = MathK.Dist(ax, az, bx, bz);
            int n = Math.Max(1, (int)(d / 0.4f));
            for (int i = 1; i <= n; i++)
            {
                float t = i / (float)n;
                if (IsBlocked(ax + (bx - ax) * t, az + (bz - az) * t)) return false;
            }
            return true;
        }

        private (float x, float z) Polar(float deg, float r) { double a = deg * Math.PI / 180.0; return (SafeX + (float)Math.Cos(a) * r, SafeZ + (float)Math.Sin(a) * r); }

        /// <summary>Ângulo de frente para a praça (o mesmo que o cliente usa para girar casas e barracas).</summary>
        private static float FaceCenter(float deg) => -deg - 90f;

        private void Prop(string kind, float deg, float r, float scale = 1f, float block = 0f, float? yaw = null)
        {
            var (x, z) = Polar(deg, r);
            Props.Add(new TownProp { Kind = kind, X = x, Z = z, Yaw = yaw ?? FaceCenter(deg), Scale = scale, R = block * scale });
            if (block > 0) Obstacles.Add(new Obstacle(x, z, block * scale));
        }

        private void House(string kind, float deg, float r, float scale = 1f)
        {
            var (x, z) = Polar(deg, r);
            Houses.Add(new TownHouse { X = x, Z = z, Angle = deg, Scale = scale, Kind = kind });
            Obstacles.Add(new Obstacle(x, z, 2.7f * scale));
        }

        private void Npc(int id, string name, string role, float deg, float r, string greeting = "")
        {
            var (x, z) = Polar(deg, r);
            Npcs.Add(new NpcDefinition { Id = id, Name = name, Role = role, X = x, Z = z, Greeting = greeting });
        }

        private void AddTown()
        {
            HasWall = true;
            WallRadius = 45f; WallHalfThickness = 0.95f;   // Muralha2: 1,8 m de espessura
            GateHalfAngle = 5.1f;                          // vão do portão: ~8 m sob o arco
            TowerAngle = 9.3f; TowerRadius = 2.3f;         // Torre2 com escala 1,1 de cada lado do arco
            for (int g = 0; g < 4; g++)
                for (int s = -1; s <= 1; s += 2)
                {
                    var (tx, tz) = Polar(g * 90f + s * TowerAngle, WallRadius);
                    Obstacles.Add(new Obstacle(tx, tz, TowerRadius));
                }
            Obstacles.Add(new Obstacle(SafeX, SafeZ, FountainRadius));   // Fonte de Brasas na praça (ponto de renascer)

            // ---- casas: anel interno (entre a praça e a rua circular) e anel externo (perto da muralha), fora das ruas
            string[] kinds = { "Casa2A", "Casa2B", "Casa2C", "Sobrado2" };
            int k = 0;
            foreach (var deg in new[] { 18f, 72f, 108f, 162f, 198f, 252f, 288f, 342f })
                House(kinds[k++ % 4], deg, 15.5f, 0.95f);
            foreach (var (deg, r) in new (float, float)[]
            {
                (20, 34), (37, 38.5f), (55, 37), (71, 34), (109, 34), (126, 38.5f), (152, 37.5f), (165, 33.5f),
                (196, 34), (212, 38.5f), (240, 38), (256, 34), (287, 34), (302, 38.5f), (328, 37.5f), (344, 34),
            })
                House(kinds[k++ % 4], deg, r, r > 36 ? 1.1f : 1f);

            // ---- distritos
            // mercado (nordeste): barracas de feira, caixotes e barris
            Prop("BarracaR", 45, 15.2f, 1f, 1.3f);
            Prop("BarracaB", 32, 25f, 1f, 1.3f);
            Prop("BarracaY", 58, 25f, 1f, 1.3f);
            Prop("BarracaR", 45, 29f, 1f, 1.3f);
            Prop("Caixote", 40, 17.5f, 1f, 0.45f); Prop("Barril", 50, 17.5f, 1f, 0.4f); Prop("Caixote", 28, 28.5f, 1f, 0.45f);
            Prop("Barril", 62, 28.5f, 1f, 0.4f); Prop("Carroca", 45, 33f, 1f, 1.4f, FaceCenter(45) + 90f);
            // forja (sudeste)
            Prop("Forja2", 315, 21f, 1.1f, 2.7f);
            Prop("Barril", 305, 18f, 1f, 0.4f); Prop("Caixote", 325, 18f, 1f, 0.45f); Prop("Braseiro2", 330, 24.5f, 1f, 0.5f);
            // templo (noroeste) com jardim
            Prop("Templo", 135, 28f, 1f, 4.4f);
            Prop("Arvore2A", 122, 29f, 1f, 0.6f); Prop("Arvore2B", 148, 29f, 1f, 0.6f); Prop("Vaso", 128, 23.5f); Prop("Vaso", 142, 23.5f);
            Prop("Banco", 135, 21.5f, 1f, 0f);
            // estábulo (sudoeste), cercado, carroça e poço
            Prop("Estabulo", 225, 28f, 1f, 3.3f);
            Prop("Cerca", 214, 24.5f, 1f, 0f, FaceCenter(214) + 90f); Prop("Cerca", 236, 24.5f, 1f, 0f, FaceCenter(236) + 90f);
            Prop("Carroca", 245, 24f, 1f, 1.4f, FaceCenter(245) + 60f); Prop("Poco", 205, 25f, 1f, 1.2f);
            Prop("Caixote", 232, 22f, 1f, 0.45f); Prop("Barril", 218, 22f, 1f, 0.4f);

            // ---- praça: bancos, vasos, estandartes e o baú da Ilsa
            foreach (var deg in new[] { 45f, 135f, 225f, 315f }) { Prop("Banco", deg, 8.2f); Prop("Vaso", deg + 12, 8.6f); Prop("Vaso", deg - 12, 8.6f); }
            Prop("Estandarte", 104, 10f, 1f, 0.3f); Prop("Estandarte", 76, 10f, 1f, 0.3f);
            Obstacles.Add(new Obstacle(VaultChestX, VaultChestZ, 0.7f));   // baú da Ilsa (o cliente desenha em BuildVaultChest)

            // ---- postes de lanterna ao longo das quatro ruas e em volta da praça; braseiros nos portões
            for (int g = 0; g < 4; g++)
            {
                int side = 1;
                foreach (var r in new[] { 13f, 21f, 28f, 35f })
                {
                    double a = g * Math.PI / 2;
                    float ux = (float)Math.Cos(a), uz = (float)Math.Sin(a);
                    float x = SafeX + ux * r - uz * 3.4f * side, z = SafeZ + uz * r + ux * 3.4f * side;
                    float dx = side * uz, dz = -side * ux;   // braço da lanterna virado para a rua
                    Props.Add(new TownProp { Kind = "Poste2", X = x, Z = z, Yaw = (float)(Math.Atan2(dx, dz) * 180.0 / Math.PI), Scale = 1f, R = 0.3f });
                    Obstacles.Add(new Obstacle(x, z, 0.3f));
                    side = -side;
                }
                foreach (var s2 in new[] { -1, 1 })
                {
                    double a = g * Math.PI / 2;
                    float ux = (float)Math.Cos(a), uz = (float)Math.Sin(a), r = WallRadius - BrazierInset;
                    float x = SafeX + ux * r - uz * BrazierSide * s2, z = SafeZ + uz * r + ux * BrazierSide * s2;
                    Props.Add(new TownProp { Kind = "Braseiro2", X = x, Z = z, Yaw = 0, Scale = 1.1f, R = BrazierRadius });
                    Obstacles.Add(new Obstacle(x, z, BrazierRadius));
                }
            }
            // árvores nos cantos dos jardins perto da muralha
            foreach (var (deg, r) in new (float, float)[] { (28, 42), (62, 42), (118, 42), (152, 42.5f), (208, 42), (242, 42.5f), (298, 42), (332, 42) })
                Prop((int)deg % 2 == 0 ? "Arvore2A" : "Arvore2B", deg, r, 1.1f, 0.6f);
        }

        /// <summary>
        /// Vale de Brasalume: a vila de Brasalume no centro (área segura) e o Bosque Cinzento em volta,
        /// com monstros mais fortes quanto mais longe da vila. Layout original.
        /// </summary>
        public static MapDefinition Brasalume()
        {
            const float C = ValeCenter;
            var m = new MapDefinition { Id = 1, Name = "Vale de Brasalume", Size = 2 * C, SafeX = C, SafeZ = C, SafeRadius = 44f, RespawnX = C, RespawnZ = C - 6.5f };
            m.AddTown();
            void G(MonsterDefinition d, float x, float z, float r, int n) => m.Spawns.Add(new SpawnGroup { Monster = d, X = C + x, Z = C + z, Radius = r, Count = n });
            // Anel próximo (fora dos quatro portões)
            G(MonsterCatalog.MossSlime, 0, 64, 7, 6);
            G(MonsterCatalog.MossSlime, 66, 0, 7, 6);
            G(MonsterCatalog.MossSlime, -64, 0, 7, 5);
            G(MonsterCatalog.AshWolf, 0, -64, 8, 5);
            G(MonsterCatalog.AshWolf, 52, 52, 8, 5);
            // Anel médio
            G(MonsterCatalog.Weaver, -58, 58, 9, 6);
            G(MonsterCatalog.Weaver, 58, -58, 9, 6);
            G(MonsterCatalog.Wanderer, -62, -62, 10, 6);
            G(MonsterCatalog.Wanderer, 78, 78, 10, 5);
            // Fundo do bosque
            G(MonsterCatalog.BasaltGolem, -92, 92, 8, 3);
            G(MonsterCatalog.BasaltGolem, 92, -92, 8, 3);
            // NPCs: serviços perto da praça e nos distritos; guardas nos portões; moradores
            m.Npc(1, "Mercadora Tália", "shop", 45, 15.9f);   // atrás do balcão da barraca
            m.Npc(2, "Ferreiro Bram", "forge", 315, 16.8f);
            m.Npc(3, "Sentinela Orvo", "guard", 265, 40f, "As cinzas rondam o portão sul. Fique atento aos avisos de invasão!");
            m.Npc(4, "Ilsa, a Guardiã", "vault", 293, 10.2f);
            m.Npc(5, "Hadrik, o Arauto das Guildas", "guild", 90, 10.4f);
            m.Npc(6, "Sentinela Brena", "guard", 85, 40f, "O portão norte dá para a mata dos lodos. Nada de pressa por lá.");
            m.Npc(7, "Sentinela Caio", "guard", 355, 40f, "Os portais ficam logo depois do portão leste: verde para o pântano, violeta para a cripta.");
            m.Npc(8, "Sentinela Mírian", "guard", 175, 40f, "Do portão oeste se vê a fumaça das tecelãs. Vá em grupo.");
            m.Npc(9, "Irmã Vesna", "deco", 135, 22.2f, "O lume do templo nunca se apaga. Que ele guie seus passos, viajante.");
            m.Npc(10, "Cavalariço Doran", "deco", 225, 23.2f, "Lagartos de basalto aguentam qualquer estrada. A Tália vende os filhotes.");
            m.Npc(11, "Velha Moira", "deco", 160, 9.6f, "Na minha época a fonte era só uma brasa num buraco. Veja como cresceu!");
            m.Npc(12, "Mestre Talo, o Escriba", "deco", 20, 22.5f, "Cada runa tem seu humor. A da Aurora é gentil; a do Eclipse, nem tanto.");
            m.Npc(13, "Pequeno Bento", "deco", 250, 9.2f, "Você já viu um golem de basalto? Dizem que moram depois das torres!");
            // Portais fora do portão leste, longe da muralha (o cliente desenha o arco de pedra com o vórtice da cor do destino)
            m.Portals.Add(new Portal { X = C + 58, Z = C - 12, TargetMap = SwampId, TargetX = 20, TargetZ = 70, Label = "Pântano de Turfa Negra (nível 20+)" });
            m.Portals.Add(new Portal { X = C + 58, Z = C + 12, TargetMap = CryptId, TargetX = 20, TargetZ = 60, Label = "Cripta de Vidro Negro (nível 45+)" });
            m.WarpCode = "VALE";
            m.LayoutVersion = 2;   // v2: vila de raio 45 num Vale de 260 m
            return m;
        }

        public const int ValeId = 1, SwampId = 2, CryptId = 3;
        /// <summary>Baú visível na praça do Vale, ao lado da Ilsa (polar 300°, 10 m do centro).</summary>
        public static readonly float VaultChestX = ValeCenter + (float)Math.Cos(300 * Math.PI / 180) * 10f, VaultChestZ = ValeCenter + (float)Math.Sin(300 * Math.PI / 180) * 10f;

        /// <summary>
        /// Pântano de Turfa Negra (nível 20-45): charco plano, poças fundas de turfa (não dá para atravessar),
        /// árvores mortas e névoa baixa. Chega-se pelo portal sudeste do Vale ou por /warp.
        /// </summary>
        public static MapDefinition Swamp()
        {
            var m = new MapDefinition { Id = SwampId, Name = "Pântano de Turfa Negra", Size = 140, SafeRadius = -1, RespawnX = 20, RespawnZ = 70,
                                        MinLevel = 20, WarpCost = 30_000, WarpCode = "PANTANO" };
            void G(MonsterDefinition d, float x, float z, float r, int n) => m.Spawns.Add(new SpawnGroup { Monster = d, X = x, Z = z, Radius = r, Count = n });
            G(MonsterCatalog.PeatCrawler, 42, 52, 8, 6);
            G(MonsterCatalog.PeatCrawler, 44, 94, 8, 6);
            G(MonsterCatalog.ReedHag, 72, 40, 9, 6);
            G(MonsterCatalog.ReedHag, 74, 102, 9, 5);
            G(MonsterCatalog.DrownedGhoul, 100, 66, 9, 6);
            G(MonsterCatalog.DrownedGhoul, 96, 118, 8, 4);
            G(MonsterCatalog.WispMoth, 120, 30, 8, 5);
            G(MonsterCatalog.WispMoth, 122, 108, 8, 5);
            // Poças fundas de turfa (posições fixas: o cliente desenha as mesmas)
            var pools = new (float x, float z, float r)[]
            {
                (34, 72, 4.5f), (58, 66, 6f), (60, 92, 5f), (82, 56, 5.5f), (86, 84, 7f), (108, 92, 6f), (110, 48, 5f), (50, 30, 4f), (92, 30, 4.5f), (64, 118, 5f),
            };
            foreach (var (x, z, r) in pools) m.Obstacles.Add(new Obstacle(x, z, r));
            m.Portals.Add(new Portal { X = 12, Z = 70, TargetMap = ValeId, TargetX = ValeCenter + 54, TargetZ = ValeCenter - 17, Label = "Vale de Brasalume" });
            return m;
        }

        /// <summary>
        /// Cripta de Vidro Negro (nível 45-80): masmorra de salões ligados por corredores. Só se anda dentro dos
        /// retângulos de chão (os corredores entram 1 m nos salões para não haver costura). Chefe no salão leste.
        /// </summary>
        public static MapDefinition Crypt()
        {
            var m = new MapDefinition { Id = CryptId, Name = "Cripta de Vidro Negro", Size = 120, SafeRadius = -1, RespawnX = 20, RespawnZ = 60,
                                        MinLevel = 45, WarpCost = 120_000, WarpCode = "CRIPTA" };
            m.Floors.Add(new FloorRect(10, 50, 30, 70));     // átrio (chegada)
            m.Floors.Add(new FloorRect(29, 56, 51, 64));     // corredor leste
            m.Floors.Add(new FloorRect(50, 44, 74, 76));     // salão das sentinelas
            m.Floors.Add(new FloorRect(58, 75, 66, 97));     // corredor norte
            m.Floors.Add(new FloorRect(48, 96, 76, 112));    // ninho das aranhas
            m.Floors.Add(new FloorRect(58, 23, 66, 45));     // corredor sul
            m.Floors.Add(new FloorRect(48, 8, 76, 24));      // galeria dos espectros
            m.Floors.Add(new FloorRect(73, 56, 93, 64));     // corredor do trono
            m.Floors.Add(new FloorRect(92, 42, 114, 78));    // salão do trono
            void G(MonsterDefinition d, float x, float z, float r, int n) => m.Spawns.Add(new SpawnGroup { Monster = d, X = x, Z = z, Radius = r, Count = n });
            G(MonsterCatalog.GlassSentinel, 62, 60, 8, 6);
            G(MonsterCatalog.ObsidianSpider, 62, 104, 9, 6);
            G(MonsterCatalog.GlassWraith, 62, 16, 9, 6);
            G(MonsterCatalog.BlackGlassSovereign, 106, 60, 2, 1);
            // Pilares de cristal (obstáculos)
            foreach (var (x, z) in new (float, float)[] { (55, 49), (69, 49), (55, 71), (69, 71), (97, 47), (109, 47), (97, 73), (109, 73) })
                m.Obstacles.Add(new Obstacle(x, z, 0.9f));
            m.Portals.Add(new Portal { X = 13, Z = 60, TargetMap = ValeId, TargetX = ValeCenter + 54, TargetZ = ValeCenter + 17, Label = "Vale de Brasalume" });
            return m;
        }

        private static Dictionary<int, MapDefinition>? _all;
        /// <summary>Todos os mapas do jogo por id (instâncias novas a cada servidor via <see cref="CreateAll"/>).</summary>
        public static Dictionary<int, MapDefinition> CreateAll() => new Dictionary<int, MapDefinition>
        {
            { ValeId, Brasalume() }, { SwampId, Swamp() }, { CryptId, Crypt() },
        };

        /// <summary>Catálogo compartilhado (somente leitura) para o cliente desenhar e consultar.</summary>
        public static MapDefinition Get(int id) => (_all ??= CreateAll()).TryGetValue(id, out var m) ? m : (_all[ValeId]);
        public static IEnumerable<MapDefinition> AllMaps => (_all ??= CreateAll()).Values;
    }
}
