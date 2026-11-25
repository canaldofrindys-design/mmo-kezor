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
        /// Vila de Brasalume v3: cidade murada de raio 72 m (área ~2,6× a v2) com praça da fonte (renascer), quatro avenidas
        /// largas, duas ruas circulares, quatro bairros com praça própria (mercado, forja, templo, estábulo), casas espaçadas
        /// com quintal e postes ao longo das ruas. Prédios em tamanho real (o herói tem 1,8 m).
        /// </summary>
        public const float TownScale = 1.0f;
        /// <summary>Raio da praça calçada em volta da fonte.</summary>
        public const float PlazaRadius = 16f;
        /// <summary>Raio da bacia da Fonte de Brasas (obstáculo).</summary>
        public const float FountainRadius = 4.0f;
        /// <summary>Rua circular interna (liga as avenidas perto do centro) e externa (passa pelos bairros).</summary>
        public const float RingRoadRadius = 32f, OuterRingRadius = 54f;
        /// <summary>Meia largura das quatro avenidas (do centro aos portões) e das ruas circulares.</summary>
        public const float AvenueHalfWidth = 3.5f, RingHalfWidth = 2.6f;
        /// <summary>Braseiros ladeando os portões, por dentro da muralha: recuo da muralha e afastamento do eixo da rua.</summary>
        public const float BrazierInset = 4.5f, BrazierSide = 6.5f, BrazierRadius = 0.5f;
        /// <summary>Centro do Vale (o mapa tem 340 m).</summary>
        public const float ValeCenter = 170f;
        /// <summary>Centro do Vale na planta v2 (mapa de 260 m), para trazer posições salvas antigas para a planta nova.</summary>
        public const float ValeCenterV2 = 130f;
        /// <summary>Raio da muralha da vila.</summary>
        public const float TownWallRadius = 72f;

        /// <summary>Pracinhas calçadas dos bairros (centro e raio). O cliente pinta o calçamento.</summary>
        public readonly List<(float X, float Z, float R)> Squares = new List<(float, float, float)>();

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

        /// <summary>
        /// Planta de cada modelo de casa em círculos (x, y do Blender: frente = -Y; raio), já em metros na escala 1.
        /// Prédios compostos (casa em L, taverna, casarão) usam mais de um círculo para a colisão seguir a forma.
        /// </summary>
        public static (float X, float Y, float R)[] HouseFootprint(string kind) => kind switch
        {
            "Casa3A" => new[] { (0f, 0f, 3.3f), (4.6f, 1.6f, 2.3f) },
            "Casa3B" => new[] { (0f, 0f, 3.0f), (3.6f, 0f, 1.6f) },
            "Casa3C" => new[] { (-1.6f, -0.6f, 3.0f), (1.6f, -0.6f, 3.0f) },
            "Taverna3" => new[] { (-2.1f, -0.6f, 3.6f), (2.1f, -0.6f, 3.6f) },
            "Casarao3" => new[] { (-2.3f, -0.6f, 3.8f), (2.3f, -0.6f, 3.8f), (6.6f, 1.0f, 2.4f) },
            _ => new[] { (0f, 0f, 2.7f) },
        };

        /// <summary>Raio que cobre o modelo inteiro a partir do centro (grama, flores e minimapa evitam essa área).</summary>
        public static float HouseExtent(string kind)
        {
            float m = 0;
            foreach (var (x, y, r) in HouseFootprint(kind)) m = Math.Max(m, (float)Math.Sqrt(x * x + y * y) + r);
            return m + 0.6f;
        }

        private void House(string kind, float deg, float r, float scale = 1f, float? facing = null)
        {
            var (x, z) = Polar(deg, r);
            // Angle = direção polar que olha para o centro; com `facing` (direção da frente, graus) a casa olha para a rua
            float angle = facing.HasValue ? facing.Value - 180f : deg;
            Houses.Add(new TownHouse { X = x, Z = z, Angle = angle, Scale = scale, Kind = kind });
            // o cliente gira o modelo em Y por (-Angle - 90); Blender (x, y) vira Unity local (x, -y)
            double yaw = (-angle - 90f) * Math.PI / 180.0, cs = Math.Cos(yaw), sn = Math.Sin(yaw);
            foreach (var (bx, by, br) in HouseFootprint(kind))
            {
                float lx = bx * scale, lz = -by * scale;
                Obstacles.Add(new Obstacle(x + (float)(lx * cs + lz * sn), z + (float)(-lx * sn + lz * cs), br * scale));
            }
        }

        private void Npc(int id, string name, string role, float deg, float r, string greeting = "")
        {
            var (x, z) = Polar(deg, r);
            Npcs.Add(new NpcDefinition { Id = id, Name = name, Role = role, X = x, Z = z, Greeting = greeting });
        }

        private void Square(float deg, float r, float radius) { var (x, z) = Polar(deg, r); Squares.Add((x, z, radius)); }

        /// <summary>Peça solta num ponto absoluto (deslocado do centro do bairro).</summary>
        private void PropAt(string kind, float x, float z, float yaw, float scale = 1f, float block = 0f)
        {
            Props.Add(new TownProp { Kind = kind, X = x, Z = z, Yaw = yaw, Scale = scale, R = block * scale });
            if (block > 0) Obstacles.Add(new Obstacle(x, z, block * scale));
        }

        private void AddTown()
        {
            HasWall = true;
            WallRadius = TownWallRadius; WallHalfThickness = 0.95f;   // Muralha2: 1,8 m de espessura
            GateHalfAngle = 3.3f;                                      // vão do portão: ~8 m sob o arco
            TowerAngle = 5.9f; TowerRadius = 2.3f;                     // Torre2 com escala 1,1 de cada lado do arco
            for (int g = 0; g < 4; g++)
                for (int s = -1; s <= 1; s += 2)
                {
                    var (tx, tz) = Polar(g * 90f + s * TowerAngle, WallRadius);
                    Obstacles.Add(new Obstacle(tx, tz, TowerRadius));
                }
            Obstacles.Add(new Obstacle(SafeX, SafeZ, FountainRadius));   // Fonte de Brasas na praça (ponto de renascer)

            string[] kinds = { "Casa3A", "Casa2A", "Casa3C", "Casa2B", "Casa3B", "Casa2C", "Sobrado2" };
            int k = 0;

            // ---- praça central: bancos e vasos em volta da fonte, estandartes do arauto, postes, baú da Ilsa
            foreach (var deg in new[] { 45f, 135f, 225f, 315f })
            {
                Prop("Banco", deg, 9.5f); Prop("Vaso", deg + 9, 10f); Prop("Vaso", deg - 9, 10f);
                Prop("Poste2", deg, PlazaRadius - 1.2f, 1f, 0.3f, FaceCenter(deg) + 180f);
            }
            Prop("Estandarte", 101, 12.5f, 1f, 0.3f); Prop("Estandarte", 79, 12.5f, 1f, 0.3f);
            Obstacles.Add(new Obstacle(VaultChestX, VaultChestZ, 0.7f));   // baú da Ilsa (o cliente desenha em BuildVaultChest)

            // ---- casas ao longo das avenidas, entre a praça e a rua interna (frente para a avenida)
            for (int g = 0; g < 4; g++)
            {
                float ga = g * 90f;
                foreach (var side in new[] { -1f, 1f })
                {
                    float off = (float)(Math.Atan2(10.5, 22.0) * 180.0 / Math.PI);   // 10,5 m ao lado do eixo
                    // a taverna fica na avenida leste, a primeira coisa que se vê ao chegar dos portais
                    string kind = g == 0 && side > 0 ? "Taverna3" : new[] { "Casa3C", "Casa2A", "Casa3B", "Casa2C", "Sobrado2", "Casa2B", "Casa3C" }[k++ % 7];
                    House(kind, ga + side * off, 24.4f, 1f, ga + 90f + (side > 0 ? 180f : 0f));
                }
            }

            // ---- bairros (cada um com pracinha calçada no meio da rua externa)
            // Mercado (nordeste): seis barracas em volta da pracinha, caixotes, barris e carroças
            float mR = OuterRingRadius - 12f; Square(45, mR, 9f);
            var (mx, mz) = Polar(45, mR);
            for (int i = 0; i < 6; i++)
            {
                double a = (i * 60 + 15) * Math.PI / 180;
                float bx = mx + (float)Math.Cos(a) * 8.5f, bz = mz + (float)Math.Sin(a) * 8.5f;
                float yaw = (float)(Math.Atan2(mx - bx, mz - bz) * 180.0 / Math.PI);           // balcão virado para a pracinha
                PropAt(i % 3 == 0 ? "BarracaR" : i % 3 == 1 ? "BarracaB" : "BarracaY", bx, bz, yaw, 1f, 1.3f);
                double a2 = a + 0.42;
                PropAt(i % 2 == 0 ? "Caixote" : "Barril", mx + (float)Math.Cos(a2) * 9.6f, mz + (float)Math.Sin(a2) * 9.6f, yaw, 1f, 0.45f);
            }
            Prop("Carroca", 36, mR + 13f, 1f, 1.4f, FaceCenter(36) + 90f); Prop("Carroca", 56, mR + 12f, 1f, 1.4f, FaceCenter(56) + 70f);
            Prop("Poco", 45, mR, 1f, 1.2f);
            Prop("Braseiro2", 30, mR - 4f, 1f, 0.5f); Prop("Braseiro2", 60, mR - 4f, 1f, 0.5f);

            // Forja (sudeste): forja grande, braseiros, pilhas de caixotes e barris, carroça de minério
            float fR = OuterRingRadius - 10f; Square(315, fR, 8f);
            Prop("Forja2", 315, fR + 3.5f, 1.25f, 2.9f);
            Prop("Braseiro2", 305, fR - 2f, 1f, 0.5f); Prop("Braseiro2", 325, fR - 2f, 1f, 0.5f);
            Prop("Barril", 300, fR + 5f, 1f, 0.4f); Prop("Barril", 302, fR + 6.5f, 1f, 0.4f); Prop("Caixote", 330, fR + 5f, 1f, 0.45f);
            Prop("Caixote", 328, fR + 6.7f, 1f, 0.45f);
            Prop("Estandarte", 308, fR - 7.5f, 1f, 0.3f); Prop("Estandarte", 322, fR - 7.5f, 1f, 0.3f);

            // Templo (noroeste): templo ao fundo, jardim com árvores, vasos, bancos e o poço sagrado
            float tR = OuterRingRadius - 8f; Square(135, tR, 8f);
            Prop("Templo", 135, tR + 7f, 1.15f, 5.0f);
            Prop("Arvore2A", 121, tR + 4f, 1.1f, 0.6f); Prop("Arvore2B", 149, tR + 4f, 1.1f, 0.6f);
            Prop("Vaso", 129, tR + 1f); Prop("Vaso", 141, tR + 1f); Prop("Vaso", 126, tR - 3f); Prop("Vaso", 144, tR - 3f);
            Prop("Banco", 128, tR - 5f); Prop("Banco", 142, tR - 5f);
            Prop("Braseiro2", 131, tR + 3.5f, 1f, 0.5f); Prop("Braseiro2", 139, tR + 3.5f, 1f, 0.5f);

            // Estábulo (sudoeste): estábulo, cercado do pasto, carroças, poço, feno (caixotes) e cochos (barris)
            float sR = OuterRingRadius - 8f; Square(225, sR, 8f);
            Prop("Estabulo", 225, sR + 7f, 1.1f, 3.6f);
            foreach (var dd in new[] { 209f, 213f, 217f, 233f, 237f, 241f }) Prop("Cerca", dd, sR + 2f, 1f, 0f, FaceCenter(dd) + 90f);
            Prop("Carroca", 240, sR - 4f, 1f, 1.4f, FaceCenter(240) - 60f);
            Prop("Poco", 225, sR - 3f, 1f, 1.2f);
            Prop("Caixote", 214, sR + 5f, 1f, 0.45f); Prop("Caixote", 215.5f, sR + 6.2f, 1f, 0.45f); Prop("Barril", 236, sR + 5f, 1f, 0.4f);

            // ---- bairro residencial: anel de casas perto da muralha (fora dos bairros), cada uma com quintal
            foreach (var g in new[] { 0f, 90f, 180f, 270f })
                foreach (var d in new[] { 14f, 27f, 63f, 76f })
                {
                    float deg = g + d;
                    House(kinds[k++ % kinds.Length], deg, 63f, 1f);
                    Prop(k % 2 == 0 ? "Arvore2A" : "Arvore2B", deg, 69.2f, 1.05f, 0.6f);           // árvore do quintal, atrás
                    Prop("Vaso", deg - 3.6f, 59.2f); Prop("Cerca", deg + 4.2f, 58.8f, 1f, 0f, FaceCenter(deg + 4.2f) + 90f);
                }
            // casas entre a rua interna e a externa, nas diagonais livres dos bairros (frente para a rua interna)
            foreach (var g in new[] { 0f, 90f, 180f, 270f })
                foreach (var d in new[] { 20f, 70f })
                    House(g + d == 200f ? "Casarao3" : kinds[k++ % kinds.Length], g + d, 41f, 1f);

            // ---- postes ao longo das avenidas (lados alternados) e da rua externa; braseiros nos portões
            for (int g = 0; g < 4; g++)
            {
                int side = 1;
                foreach (var r in new[] { 20f, 29f, 38f, 47f, 56f, 65f })
                {
                    double a = g * Math.PI / 2;
                    float ux = (float)Math.Cos(a), uz = (float)Math.Sin(a);
                    float x = SafeX + ux * r - uz * (AvenueHalfWidth + 1.1f) * side, z = SafeZ + uz * r + ux * (AvenueHalfWidth + 1.1f) * side;
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
            foreach (var deg in new[] { 20f, 70f, 110f, 160f, 200f, 250f, 290f, 340f })
                Prop("Poste2", deg, OuterRingRadius + RingHalfWidth + 1.1f, 1f, 0.3f, FaceCenter(deg));
            // ---- vida: placas nos cruzamentos, canteiros na praça, mesas da taverna, feno, lenha, bigorna, varais, arbustos
            foreach (var deg in new[] { 0f, 90f, 180f, 270f })
                Prop("Placa", deg + 14f, PlazaRadius + 1.6f, 1f, 0.25f, FaceCenter(deg));
            foreach (var deg in new[] { 22f, 158f, 202f, 338f }) Prop("Canteiro", deg, 13.2f, 1f, 0.6f, FaceCenter(deg) + 90f);
            Prop("MesaTaverna", 33.9f, 26.5f, 1f, 0.8f, 0f); Prop("MesaTaverna", 39.4f, 23.3f, 1f, 0.8f, 12f);   // quintal da taverna
            Prop("Bigorna", 309, fR + 0.5f, 1f, 0.5f); Prop("Lenha", 333, fR + 1.5f, 1f, 0.9f, FaceCenter(333) + 90f);
            Prop("CaixotePilha", 50, mR + 11.5f, 1f, 0.9f); Prop("CaixotePilha", 40, mR - 10.5f, 1f, 0.9f);
            Prop("Feno", 212, sR + 8.5f, 1f, 1.0f); Prop("Feno", 238, sR + 8.5f, 1f, 1.0f); Prop("Feno", 241, sR + 6f, 0.8f, 0.8f);
            Prop("Lenha", 230, sR - 6.5f, 1f, 0.9f, FaceCenter(230) + 90f);
            foreach (var g in new[] { 0f, 90f, 180f, 270f })
            {
                Prop("Varal", g + 45f, 66f, 1f, 0f, FaceCenter(g + 45f) + 90f);
                foreach (var d in new[] { 20.5f, 69.5f }) Prop("Arbusto", g + d, 59f, 1f, 0.5f);
            }
            // bancos à beira da rua interna, para dar vida às esquinas
            foreach (var deg in new[] { 30f, 60f, 120f, 150f, 210f, 240f, 300f, 330f })
                Prop("Banco", deg, RingRoadRadius + RingHalfWidth + 1.4f);
        }

        /// <summary>
        /// Vale de Brasalume: a vila de Brasalume no centro (área segura) e o Bosque Cinzento em volta,
        /// com monstros mais fortes quanto mais longe da vila. Layout original.
        /// </summary>
        public static MapDefinition Brasalume()
        {
            const float C = ValeCenter;
            var m = new MapDefinition { Id = 1, Name = "Vale de Brasalume", Size = 2 * C, SafeX = C, SafeZ = C, SafeRadius = TownWallRadius - 1f, RespawnX = C, RespawnZ = C - 7f };
            m.AddTown();
            void G(MonsterDefinition d, float x, float z, float r, int n) => m.Spawns.Add(new SpawnGroup { Monster = d, X = C + x, Z = C + z, Radius = r, Count = n });
            // Anel próximo (fora dos quatro portões)
            G(MonsterCatalog.MossSlime, 0, 100, 8, 6);
            G(MonsterCatalog.MossSlime, 100, -42, 8, 6);
            G(MonsterCatalog.MossSlime, -100, 0, 8, 5);
            G(MonsterCatalog.AshWolf, 0, -100, 9, 5);
            G(MonsterCatalog.AshWolf, 76, 76, 9, 5);
            // Anel médio
            G(MonsterCatalog.Weaver, -84, 84, 10, 6);
            G(MonsterCatalog.Weaver, 84, -84, 10, 6);
            G(MonsterCatalog.Wanderer, -90, -90, 11, 6);
            G(MonsterCatalog.Wanderer, 108, 108, 11, 5);
            // Fundo do bosque
            G(MonsterCatalog.BasaltGolem, -120, 120, 9, 3);
            G(MonsterCatalog.BasaltGolem, 120, -120, 9, 3);
            // NPCs: cada um no seu lugar (serviços na praça e nos bairros, guardas nos portões, moradores pela cidade)
            float mR = OuterRingRadius - 12f, fR = OuterRingRadius - 10f, tR = OuterRingRadius - 8f, sR = OuterRingRadius - 8f;
            {   // no balcão da barraca sudoeste do mercado, de frente para quem vem da praça
                var (mx, mz) = m.Polar(45, mR); double a = 195 * Math.PI / 180;
                m.Npcs.Add(new NpcDefinition { Id = 1, Name = "Mercadora Tália", Role = "shop", X = mx + (float)Math.Cos(a) * 6f, Z = mz + (float)Math.Sin(a) * 6f });
            }
            m.Npc(2, "Ferreiro Bram", "forge", 315, fR - 0.5f);                 // na frente da forja
            m.Npc(3, "Sentinela Orvo", "guard", 270 - 7.5f, TownWallRadius - 6f, "As cinzas rondam o portão sul. Fique atento aos avisos de invasão!");
            m.Npc(4, "Ilsa, a Guardiã", "vault", 293, 12.8f);
            m.Npc(5, "Hadrik, o Arauto das Guildas", "guild", 90, 12.6f);
            m.Npc(6, "Sentinela Brena", "guard", 90 - 7.5f, TownWallRadius - 6f, "O portão norte dá para a mata dos lodos. Nada de pressa por lá.");
            m.Npc(7, "Sentinela Caio", "guard", 360 - 7.5f, TownWallRadius - 6f, "Os portais ficam logo depois do portão leste: verde para o pântano, violeta para a cripta.");
            m.Npc(8, "Sentinela Mírian", "guard", 180 - 7.5f, TownWallRadius - 6f, "Do portão oeste se vê a fumaça das tecelãs. Vá em grupo.");
            m.Npc(9, "Irmã Vesna", "deco", 135, tR - 0.5f, "O lume do templo nunca se apaga. Que ele guie seus passos, viajante.");
            m.Npc(10, "Cavalariço Doran", "deco", 220, sR + 0.5f, "Lagartos de basalto aguentam qualquer estrada. A Tália vende os filhotes.");
            m.Npc(11, "Velha Moira", "deco", 160, 11.2f, "Na minha época a fonte era só uma brasa num buraco. Veja como cresceu!");
            m.Npc(12, "Mestre Talo, o Escriba", "deco", 127, tR - 2.5f, "Cada runa tem seu humor. A da Aurora é gentil; a do Eclipse, nem tanto.");
            m.Npc(13, "Pequeno Bento", "deco", 250, 11f, "Você já viu um golem de basalto? Dizem que moram depois das torres!");
            // Portais fora do portão leste, longe da muralha (o cliente desenha o arco de pedra com o vórtice da cor do destino)
            m.Portals.Add(new Portal { X = C + 92, Z = C - 12, TargetMap = SwampId, TargetX = 20, TargetZ = 70, Label = "Pântano de Turfa Negra (nível 20+)" });
            m.Portals.Add(new Portal { X = C + 92, Z = C + 12, TargetMap = CryptId, TargetX = 20, TargetZ = 60, Label = "Cripta de Vidro Negro (nível 45+)" });
            m.WarpCode = "VALE";
            m.LayoutVersion = 3;   // v3: cidade de raio 72 num Vale de 340 m (posições v2 são deslocadas pelo centro novo)
            return m;
        }

        public const int ValeId = 1, SwampId = 2, CryptId = 3;
        /// <summary>Baú visível na praça do Vale, ao lado da Ilsa (polar 300°, 12,5 m do centro).</summary>
        public static readonly float VaultChestX = ValeCenter + (float)Math.Cos(300 * Math.PI / 180) * 12.5f, VaultChestZ = ValeCenter + (float)Math.Sin(300 * Math.PI / 180) * 12.5f;

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
            m.Portals.Add(new Portal { X = 12, Z = 70, TargetMap = ValeId, TargetX = ValeCenter + 86, TargetZ = ValeCenter - 20, Label = "Vale de Brasalume" });
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
            m.Portals.Add(new Portal { X = 13, Z = 60, TargetMap = ValeId, TargetX = ValeCenter + 86, TargetZ = ValeCenter + 20, Label = "Vale de Brasalume" });
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
