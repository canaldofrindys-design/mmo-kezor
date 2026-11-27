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

    /// <summary>Obstáculo retangular girado (prédio, torre do portão, barraca). HX/HZ = meias medidas no espaço local.</summary>
    public struct ObstacleBox
    {
        public float X, Z, HX, HZ, Yaw;
        private readonly float _cs, _sn;
        public ObstacleBox(float x, float z, float hx, float hz, float yaw)
        {
            X = x; Z = z; HX = hx; HZ = hz; Yaw = yaw;
            double r = yaw * Math.PI / 180.0;
            _cs = (float)Math.Cos(r); _sn = (float)Math.Sin(r);
        }
        /// <summary>True se um círculo de raio `pad` em (x, z) encosta no retângulo.</summary>
        public bool Contains(float x, float z, float pad)
        {
            float dx = x - X, dz = z - Z;
            // mundo -> local (inverso do giro do Unity em Y: local = (dx cos - dz sin, dx sin + dz cos))
            float lx = dx * _cs - dz * _sn, lz = dx * _sn + dz * _cs;
            float ox = Math.Max(0f, Math.Abs(lx) - HX), oz = Math.Max(0f, Math.Abs(lz) - HZ);
            return ox * ox + oz * oz < pad * pad;
        }
    }

    /// <summary>Prédio da cidade: posição, giro (graus do Unity: a frente +Z do modelo vira para a rua), escala e modelo.</summary>
    public struct TownHouse
    {
        public float X, Z, Yaw, Scale;
        /// <summary>Modelo (Casa4A..D, Loja4, Taverna4, Ferraria4, Capela4, Guilda4, Estabulo4).</summary>
        public string Kind;
        /// <summary>Variação da cor do telhado de telha (0 = original).</summary>
        public int Tint;
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

        // Muralha quadrada com 4 portões (L, N, O, S) e obstáculos. O servidor usa para bloquear o movimento;
        // o cliente (WorldBuilder) usa os mesmos dados para posicionar muralha, torres, casas e peças.
        public bool HasWall;
        /// <summary>Metade do lado da muralha quadrada (do centro até a linha da muralha), meia espessura e meia largura do vão andável do portão.</summary>
        public float WallRadius, WallHalfThickness, GateHalfWidth;
        public readonly List<Obstacle> Obstacles = new List<Obstacle>();
        /// <summary>Obstáculos retangulares (prédios, torres do portão, barracas).</summary>
        public readonly List<ObstacleBox> Boxes = new List<ObstacleBox>();
        public readonly List<TownHouse> Houses = new List<TownHouse>();
        /// <summary>Torres da muralha (cantos e meio dos lados): posição, giro (frente para fora) e escala.</summary>
        public readonly List<TownProp> Towers = new List<TownProp>();
        /// <summary>Raio do corpo de personagens e monstros para a colisão.</summary>
        public const float BodyRadius = 0.35f;
        /// <summary>
        /// Cidade de Brasalume v4: cidade murada quadrada (104 m de lado) no estilo clássico dos MMOs isométricos — praça
        /// calçada com a Fonte da Brasa no centro, quatro avenidas em cruz até os portões fortificados, uma rua em quadrado
        /// ligando os bairros, rua junto à muralha e quarteirões fechados de casas de pedra e enxaimel. Bairros: Mercado
        /// (nordeste), Capela (noroeste), Guilda (sudeste) e Taverna/Estábulo (sudoeste); ferraria no bairro oeste.
        /// </summary>
        public const float TownScale = 1.0f;
        /// <summary>Raio da praça calçada em volta da fonte (piso de lajes Praca4).</summary>
        public const float PlazaRadius = 15f;
        /// <summary>Raio da bacia da Fonte da Brasa (obstáculo).</summary>
        public const float FountainRadius = 4.0f;
        /// <summary>Rua em quadrado que liga os bairros (distância do centro ao eixo) e começo da rua junto à muralha.</summary>
        public const float RingRoadRadius = 30f, LaneRadius = 47.5f;
        /// <summary>Meia largura das quatro avenidas (do centro aos portões) e da rua em quadrado.</summary>
        public const float AvenueHalfWidth = 4f, RingHalfWidth = 2.5f;
        /// <summary>Centro do Vale (o mapa tem 340 m).</summary>
        public const float ValeCenter = 170f;
        /// <summary>Centro do Vale na planta v2 (mapa de 260 m), para trazer posições salvas antigas para a planta nova.</summary>
        public const float ValeCenterV2 = 130f;
        /// <summary>Metade do lado da muralha quadrada da cidade.</summary>
        public const float TownWallRadius = 52f;
        /// <summary>Torres do portão (Portao4): centro a 5,9 m do eixo, meia largura 2,7 m e meia profundidade 3,2 m.</summary>
        public const float GateTowerOffset = 5.9f, GateTowerHalfW = 2.7f, GateTowerHalfD = 3.2f;

        /// <summary>Pracinhas calçadas dos bairros (centro e raio). O cliente pinta o calçamento.</summary>
        public readonly List<(float X, float Z, float R)> Squares = new List<(float, float, float)>();
        /// <summary>Áreas retangulares calçadas além das ruas (mercado, adro da capela). O cliente pinta o calçamento.</summary>
        public readonly List<FloorRect> Paved = new List<FloorRect>();

        /// <summary>Peças de cenário da vila (o cliente desenha; as com R &gt; 0 também bloqueiam).</summary>
        public readonly List<TownProp> Props = new List<TownProp>();

        /// <summary>Distância "quadrada" (Chebyshev) ao centro da cidade: dentro da muralha quando menor que WallRadius.</summary>
        public float TownDistance(float x, float z) => Math.Max(Math.Abs(x - SafeX), Math.Abs(z - SafeZ));

        public bool InSafeZone(float x, float z)
        {
            if (SafeRadius <= 0) return false;
            if (HasWall) return TownDistance(x, z) <= WallRadius + WallHalfThickness;
            return MathK.Dist(x, z, SafeX, SafeZ) <= SafeRadius;
        }
        public float ClampCoord(float v) => MathK.Clamp(v, 1f, Size - 1f);

        /// <summary>True se o ponto (sem raio) é rua ou praça calçada da cidade.</summary>
        public bool IsTownStreet(float x, float z, float pad = 0f)
        {
            if (!HasWall) return false;
            float dx = Math.Abs(x - SafeX), dz = Math.Abs(z - SafeZ), d = Math.Max(dx, dz);
            if (d > WallRadius) return false;
            if (dx < AvenueHalfWidth + pad || dz < AvenueHalfWidth + pad) return true;
            if (Math.Abs(d - RingRoadRadius) < RingHalfWidth + pad) return true;
            if (d > LaneRadius - pad) return true;
            if (MathK.Dist(x, z, SafeX, SafeZ) < PlazaRadius + pad) return true;
            foreach (var sq in Squares) if (MathK.Dist(x, z, sq.X, sq.Z) < sq.R + pad) return true;
            foreach (var p in Paved) if (x > p.X0 - pad && x < p.X1 + pad && z > p.Z0 - pad && z < p.Z1 + pad) return true;
            return false;
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
                float dx = Math.Abs(x - SafeX), dz = Math.Abs(z - SafeZ), t = WallHalfThickness + BodyRadius;
                if (Math.Abs(dx - WallRadius) < t && dz < WallRadius + t && dz > GateHalfWidth) return true;   // muralhas leste/oeste
                if (Math.Abs(dz - WallRadius) < t && dx < WallRadius + t && dx > GateHalfWidth) return true;   // muralhas norte/sul
            }
            foreach (var o in Obstacles)
                if (MathK.Dist(x, z, o.X, o.Z) < o.R + BodyRadius) return true;
            foreach (var b in Boxes)
                if (b.Contains(x, z, BodyRadius)) return true;
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

        /// <summary>Giro (graus, eixo Y do Unity) que vira a frente (+Z) de um modelo para a direção (fx, fz).</summary>
        public static float YawTo(float fx, float fz) => (float)(Math.Atan2(fx, fz) * 180.0 / Math.PI);

        /// <summary>Converte um ponto local de um modelo (x à direita, z para a frente) em mundo, com giro e escala.</summary>
        public static (float X, float Z) LocalToWorld(float px, float pz, float yaw, float scale, float lx, float lz)
        {
            double r = yaw * Math.PI / 180.0, cs = Math.Cos(r), sn = Math.Sin(r);
            lx *= scale; lz *= scale;
            return (px + (float)(lx * cs + lz * sn), pz + (float)(-lx * sn + lz * cs));
        }

        /// <summary>
        /// Planta de cada prédio da cidade v4 em retângulos (centro x, z e meias medidas), no espaço local do modelo
        /// (x à direita, z para a frente = rua), já em metros na escala 1. Medidas tiradas dos modelos do kezor_city4.py.
        /// </summary>
        public static (float X, float Z, float HX, float HZ)[] HouseFootprint(string kind) => kind switch
        {
            "Casa4A" => new[] { (0f, 0f, 3.65f, 3.2f), (-2.2f, 3.65f, 0.85f, 0.45f) },
            "Casa4B" => new[] { (0f, 0f, 2.9f, 3.45f) },
            "Casa4C" => new[] { (0f, 0f, 4.1f, 3.3f) },
            "Casa4D" => new[] { (0f, 0f, 3.3f, 3.15f), (4.4f, -0.4f, 1.15f, 2.05f), (3.9f, 3.2f, 0.85f, 0.3f) },
            "Taverna4" => new[] { (0f, 0f, 5.65f, 3.95f), (0f, 4.95f, 5.5f, 1.2f), (6.0f, 2.85f, 0.45f, 0.6f) },
            "Loja4" => new[] { (0f, 0f, 3.6f, 3.3f), (-0.3f, 3.85f, 3.1f, 0.5f) },
            "Ferraria4" => new[] { (0f, 0f, 3.6f, 3.3f), (5.8f, 0f, 2.45f, 3.3f) },
            "Capela4" => new[] { (0f, 0f, 4.6f, 7.3f), (0f, -0.9f, 4.95f, 5.9f), (-2.6f, 8.4f, 0.5f, 0.5f), (2.6f, 8.4f, 0.5f, 0.5f) },
            "Guilda4" => new[] { (0f, 0f, 6.8f, 4.8f), (0f, 6.1f, 3.0f, 0.45f), (-6.7f, 4.7f, 1.6f, 1.6f), (6.7f, 4.7f, 1.6f, 1.6f), (-4.2f, 6.9f, 0.5f, 0.5f), (4.2f, 6.9f, 0.5f, 0.5f) },
            "Estabulo4" => new[] { (0f, 0f, 5.65f, 3.65f), (-6.4f, -0.3f, 0.6f, 1.2f) },
            _ => new[] { (0f, 0f, 3f, 3f) },
        };

        /// <summary>Raio que cobre o prédio inteiro a partir do centro (grama, flores e minimapa evitam essa área).</summary>
        public static float HouseExtent(string kind)
        {
            float m = 0;
            foreach (var (x, z, hx, hz) in HouseFootprint(kind)) m = Math.Max(m, (float)Math.Sqrt((Math.Abs(x) + hx) * (Math.Abs(x) + hx) + (Math.Abs(z) + hz) * (Math.Abs(z) + hz)));
            return m + 0.6f;
        }

        /// <summary>Retângulos de colisão de uma casa já em mundo (o servidor guarda todos em Boxes).</summary>
        public static IEnumerable<ObstacleBox> HouseBoxes(TownHouse h)
        {
            foreach (var (x, z, hx, hz) in HouseFootprint(h.Kind))
            {
                var (wx, wz) = LocalToWorld(h.X, h.Z, h.Yaw, h.Scale, x, z);
                yield return new ObstacleBox(wx, wz, hx * h.Scale, hz * h.Scale, h.Yaw);
            }
        }

        /// <summary>Casa com a frente virada para (fx, fz). Retorna a casa (para conferir a planta).</summary>
        private TownHouse House(string kind, float x, float z, float fx, float fz, float scale = 1f, int tint = 0)
        {
            var h = new TownHouse { X = x, Z = z, Yaw = YawTo(fx, fz), Scale = scale, Kind = kind, Tint = tint };
            Houses.Add(h);
            foreach (var b in HouseBoxes(h)) Boxes.Add(b);
            return h;
        }

        /// <summary>Peça solta (yaw em graus do Unity). block = raio do obstáculo (0 = passa por cima/ao lado).</summary>
        private void PropAt(string kind, float x, float z, float yaw, float scale = 1f, float block = 0f)
        {
            Props.Add(new TownProp { Kind = kind, X = x, Z = z, Yaw = yaw, Scale = scale, R = block * scale });
            if (block > 0) Obstacles.Add(new Obstacle(x, z, block * scale));
        }

        /// <summary>Peça com colisão retangular (barraca, carroça).</summary>
        private void PropBox(string kind, float x, float z, float yaw, float hx, float hz, float scale = 1f)
        {
            Props.Add(new TownProp { Kind = kind, X = x, Z = z, Yaw = yaw, Scale = scale, R = 0f });
            Boxes.Add(new ObstacleBox(x, z, hx * scale, hz * scale, yaw));
        }

        private void Npc(int id, string name, string role, float x, float z, string greeting = "")
            => Npcs.Add(new NpcDefinition { Id = id, Name = name, Role = role, X = x, Z = z, Greeting = greeting });

        /// <summary>Extensão do prédio ao longo do x local (esquerda negativa, direita positiva), pela planta.</summary>
        private static (float Left, float Right) HouseSpan(string kind)
        {
            float l = 0, r = 0;
            foreach (var (x, z, hx, hz) in HouseFootprint(kind)) { l = Math.Min(l, x - hx); r = Math.Max(r, x + hx); }
            return (l, r);
        }

        /// <summary>
        /// Fileira de casas ao longo de um lado da cidade. side = 0 L, 1 N, 2 O, 3 S; t0..t1 = trecho ao longo do lado
        /// (com sinal); front = distância do centro até a frente das casas; faceIn = frente para o centro (rua em quadrado)
        /// ou para fora (rua da muralha). Casas encostadas, com 0,6 m entre uma e outra, como quarteirão fechado;
        /// a cada `skipEvery` casas fica um beco com árvore e barris.
        /// </summary>
        private void Row(int side, float t0, float t1, float front, bool faceIn, string[] kinds, ref int k, int skipEvery = 0)
        {
            float nx = side == 0 ? 1 : side == 2 ? -1 : 0, nz = side == 1 ? 1 : side == 3 ? -1 : 0;   // normal para fora
            float tx = -nz, tz = nx;                                                                   // ao longo do lado
            float dir = t1 > t0 ? 1f : -1f;
            float fx = faceIn ? -nx : nx, fz = faceIn ? -nz : nz;
            float yaw = YawTo(fx, fz);
            double yr = yaw * Math.PI / 180.0;
            float lxx = (float)Math.Cos(yr), lxz = -(float)Math.Sin(yr);                               // x local em mundo
            bool plus = (lxx * tx + lxz * tz) * dir > 0;                                               // x local avança com a fileira?
            float t = t0;
            int n = 0;
            while (true)
            {
                string kind = kinds[k % kinds.Length];
                var (l, r) = HouseSpan(kind);
                float back = plus ? -l : r, fwd = plus ? r : -l;
                if (dir * (t1 - (t + dir * (back + fwd))) < 0) break;
                k++;
                n++;
                float tc = t + dir * back;
                float hz = HouseFootprint(kind)[0].HZ;
                float rr = faceIn ? front + hz : front - hz;               // centro da casa (distância do centro da cidade)
                float x = SafeX + nx * rr + tx * tc, z = SafeZ + nz * rr + tz * tc;
                if (skipEvery > 0 && n % skipEvery == 0)
                {   // beco/quintal: árvore e barris no vão
                    PropAt(n % 2 == 0 ? "Arvore2A" : "Arvore2B", x - fx * 0.8f, z - fz * 0.8f, n * 47f, 0.9f, 0.6f);
                    PropAt("Barris4", x + tx * 1.6f * dir + fx * 1.8f, z + tz * 1.6f * dir + fz * 1.8f, yaw, 1f, 0.8f);
                }
                else House(kind, x, z, fx, fz, 1f, (k * 7 + side) % 3);
                t += dir * (back + fwd + 0.6f);
            }
        }

        private void AddTown()
        {
            HasWall = true;
            float W = TownWallRadius;
            WallRadius = W; WallHalfThickness = 1.3f;          // Muralha4: 2,6 m de espessura
            GateHalfWidth = 2.8f;                              // vão do Portao4: 6,4 m entre as torres (o corpo cabe com folga)
            float C = SafeX;
            // torres do portão (retângulos) e torres da muralha (cantos e meio de cada trecho)
            for (int g = 0; g < 4; g++)
            {
                float nx = g == 0 ? 1 : g == 2 ? -1 : 0, nz = g == 1 ? 1 : g == 3 ? -1 : 0;
                float tx = -nz, tz = nx;
                foreach (var s in new[] { -1f, 1f })
                {
                    float x = C + nx * W + tx * s * GateTowerOffset, z = C + nz * W + tz * s * GateTowerOffset;
                    Boxes.Add(new ObstacleBox(x, z, GateTowerHalfW, GateTowerHalfD, YawTo(nx, nz)));
                    // torres no meio de cada trecho da muralha
                    // (meio para fora da muralha, para não fechar a rua de dentro)
                    float mx = C + nx * (W + 1.2f) + tx * s * 29f, mz = C + nz * (W + 1.2f) + tz * s * 29f;
                    Towers.Add(new TownProp { Kind = "Torre4", X = mx, Z = mz, Yaw = YawTo(nx, nz), Scale = 0.82f, R = 3.6f });
                    Obstacles.Add(new Obstacle(mx, mz, 3.6f));
                }
                // torres de canto (no canto entre este lado e o seguinte)
                float cx2 = C + (g == 0 || g == 1 ? W : -W), cz2 = C + (g == 0 || g == 3 ? W : -W);
                if (g == 0) { cx2 = C + W; cz2 = C + W; }
                else if (g == 1) { cx2 = C - W; cz2 = C + W; }
                else if (g == 2) { cx2 = C - W; cz2 = C - W; }
                else { cx2 = C + W; cz2 = C - W; }
                Towers.Add(new TownProp { Kind = "Torre4", X = cx2, Z = cz2, Yaw = YawTo(cx2 - C, cz2 - C), Scale = 1f, R = 4.4f });
                Obstacles.Add(new Obstacle(cx2, cz2, 4.4f));
                // braseiros por dentro do portão e guarita
                foreach (var s in new[] { -1f, 1f })
                {   // braseiros na boca do portão (por dentro) e postes nas bordas da avenida
                    PropAt("Braseiro4", C + nx * (W - 4.8f) + tx * s * 3.4f, C + nz * (W - 4.8f) + tz * s * 3.4f, 0f, 1f, 0.6f);
                    foreach (var r in new[] { 21f, 40f })
                        PropAt("Poste4", C + nx * r + tx * s * 3.6f, C + nz * r + tz * s * 3.6f, YawTo(-tx * s, -tz * s), 1f, 0.3f);
                }
            }
            Obstacles.Add(new Obstacle(SafeX, SafeZ, FountainRadius));   // Fonte da Brasa na praça (ponto de renascer)

            // ---- praça: canteiros, bancos, estandartes e postes em volta do piso; baú da Ilsa
            foreach (var deg in new[] { 45f, 135f, 225f, 315f })
            {
                double a = deg * Math.PI / 180;
                float ux = (float)Math.Cos(a), uz = (float)Math.Sin(a);
                PropAt("Canteiro4", C + ux * 11.2f, C + uz * 11.2f, YawTo(-uz, ux), 1f, 0f);
                Boxes.Add(new ObstacleBox(C + ux * 11.2f, C + uz * 11.2f, 1.25f, 0.65f, YawTo(-uz, ux)));
                PropAt("Banco4", C + ux * 9.3f, C + uz * 9.3f, YawTo(-ux, -uz), 1f, 0f);
                foreach (var s in new[] { -1f, 1f })
                {
                    double b = (deg + s * 19) * Math.PI / 180;
                    PropAt("Poste4", C + (float)Math.Cos(b) * 13.6f, C + (float)Math.Sin(b) * 13.6f, YawTo(-(float)Math.Cos(b), -(float)Math.Sin(b)), 1f, 0.3f);
                }
            }
            foreach (var deg in new[] { 70f, 110f })
            {
                double a = deg * Math.PI / 180;
                PropAt("Estandarte4", C + (float)Math.Cos(a) * 13.5f, C + (float)Math.Sin(a) * 13.5f, 0f, 1f, 0.35f);
            }
            Obstacles.Add(new Obstacle(VaultChestX, VaultChestZ, 0.7f));   // baú da Ilsa (o cliente desenha em BuildVaultChest)

            string[] mix = { "Casa4A", "Casa4B", "Casa4D", "Casa4C", "Casa4B", "Casa4A", "Casa4D", "Casa4B", "Casa4C" };
            int k = 0;
            float inner = RingRoadRadius + RingHalfWidth + 0.3f;          // frente das casas da rua em quadrado (lado de fora)
            float lane = LaneRadius - 0.3f;                               // frente das casas da rua da muralha
            float ringIn = RingRoadRadius - RingHalfWidth - 0.3f;         // frente das casas viradas para a rua em quadrado (lado de dentro)
            float av = AvenueHalfWidth + 0.5f;                            // começo das fileiras junto às avenidas

            // ---- quarteirões de fora (entre a rua em quadrado e a rua da muralha): duas fileiras costas com costas.
            // A fileira da rua da muralha nos lados leste/oeste vai até o canto; o resto para antes do canto (os cantos
            // ficam como quintais com árvore).
            for (int side = 0; side < 4; side++)
                foreach (var s in new[] { -1f, 1f })
                {
                    float laneEnd = (side == 0 || side == 2) ? LaneRadius - 0.2f : inner - 0.5f;
                    if (side == 2 && s > 0) continue;        // oeste-sul (no lado oeste, t positivo = sul): ferraria e estábulo (abaixo)
                    Row(side, s * av, s * (inner - 0.5f), inner, true, side == 0 && s > 0 ? new[] { "Loja4", "Casa4C", "Casa4B", "Casa4A" } : mix, ref k, 5);
                    Row(side, s * (GateTowerOffset + GateTowerHalfW + 0.6f), s * laneEnd, lane, false, mix, ref k, 4);
                }
            {   // quarteirão oeste-sul: Ferraria do Bram e uma casa viradas para a rua em quadrado; Estábulo na rua da muralha
                House("Ferraria4", C - (inner + 3.35f), C - 11f, 1f, 0f);
                House("Casa4D", C - (inner + 3.2f), C - 23.2f, 1f, 0f);
                House("Estabulo4", C - (lane - 3.7f), C - 12.4f, -1f, 0f);
                Row(2, 20.2f, LaneRadius - 0.2f, lane, false, mix, ref k);
                PropAt("Caixotes4", C - (inner + 1.4f), C - 5.8f, 90f, 1f, 0.9f);
                PropAt("Arvore2B", C - (inner + 2.5f), C - 30.5f, 40f, 0.9f, 0.6f);
            }
            // quintais dos cantos: árvore, poço ou caixotes
            foreach (var (qx, qz) in new[] { (1f, 1f), (-1f, 1f), (-1f, -1f), (1f, -1f) })
            {
                PropAt(qx * qz > 0 ? "Arvore2A" : "Arvore2B", C + qx * 36.5f, C + qz * 37.5f, qx * 40f, 1.05f, 0.6f);
                PropAt("Caixotes4", C + qx * 36.0f, C + qz * 43.0f, qz * 90f, 1f, 0.9f);
            }

            // ---- quarteirões de dentro (entre as avenidas e a rua em quadrado), um bairro em cada canto
            // Mercado (nordeste): largo calçado com barracas em volta do poço e do obelisco; lojas de frente para o largo
            Paved.Add(new FloorRect(C + av - 0.5f, C + av - 0.5f, C + ringIn + 0.3f, C + ringIn + 0.3f));
            Squares.Add((C + 17f, C + 17f, 6f));                              // o primeiro da lista é o mercado (som ambiente)
            PropAt("Poco4", C + 17f, C + 17f, 20f, 1f, 1.3f);
            PropAt("Obelisco4", C + 24f, C + 24f, 45f, 1f, 1.4f);
            var stalls = new (float x, float z)[] { (11.5f, 22f), (16.5f, 23.5f), (22f, 18.5f), (23.5f, 13.5f), (11f, 12f) };
            for (int i = 0; i < stalls.Length; i++)
            {
                var (sx, sz) = stalls[i];
                float yaw = YawTo(17f - sx, 17f - sz);
                PropBox(i % 3 == 0 ? "Barraca4R" : i % 3 == 1 ? "Barraca4B" : "Barraca4Y", C + sx, C + sz, yaw, 1.75f, 1.25f);
            }
            PropAt("Caixotes4", C + 13.5f, C + 25.5f, 10f, 1f, 1.0f); PropAt("Barris4", C + 25.5f, C + 15.5f, 80f, 1f, 0.9f);
            PropBox("Carroca", C + 8.5f, C + 25f, 75f, 1.0f, 1.9f);

            // Capela (noroeste): Capela do Lume com o portal para a avenida leste-oeste, jardim com árvores e bancos
            House("Capela4", C - 18.5f, C + 15.8f, 0f, -1f);
            Paved.Add(new FloorRect(C - 23f, C + av - 0.5f, C - 14f, C + 8.2f));
            PropAt("Arvore2B", C - 25.3f, C + 25.3f, 100f, 1.05f, 0.6f);
            PropAt("Obelisco4", C - 25.3f, C + 6.9f, 0f, 0.8f, 1.1f);
            PropAt("Banco4", C - 9.6f, C + 17f, 90f); PropAt("Banco4", C - 9.6f, C + 12.5f, 90f);
            PropAt("Canteiro4", C - 15f, C + 25.5f, 0f); Boxes.Add(new ObstacleBox(C - 15f, C + 25.5f, 1.25f, 0.65f, 0f));
            House("Casa4A", C - 7.9f, C + 23.5f, 1f, 0f);       // junto à avenida norte

            // Guilda (sudeste): Salão das Guildas de frente para a avenida leste-oeste, braseiros e estandartes
            House("Guilda4", C + 17.5f, C - 17.5f, 0f, 1f);
            PropAt("Estandarte4", C + 8.5f, C - 8.6f, 0f, 1f, 0.35f); PropAt("Estandarte4", C + 26f, C - 8.6f, 0f, 1f, 0.35f);
            PropAt("Canteiro4", C + 9f, C - 25f, 90f); Boxes.Add(new ObstacleBox(C + 9f, C - 25f, 1.25f, 0.65f, 90f));
            PropAt("Arvore2B", C + 25.5f, C - 26f, 10f, 0.95f, 0.6f);

            // Taverna (sudoeste): Taverna do Lume Velho de frente para a avenida norte-sul, quintal com barris e carroça
            House("Taverna4", C - 13.5f, C - 20.5f, 0f, 1f);
            House("Casa4C", C - 23.3f, C - 8.6f, 0f, 1f);
            PropAt("Barris4", C - 22f, C - 25.5f, 0f, 1f, 0.9f); PropAt("Caixotes4", C - 6.5f, C - 25.8f, 90f, 1f, 0.9f);
            PropBox("Carroca", C - 6.2f, C - 13f, 0f, 1.0f, 1.9f);

        }

        /// <summary>
        /// Vale de Brasalume: a vila de Brasalume no centro (área segura) e o Bosque Cinzento em volta,
        /// com monstros mais fortes quanto mais longe da vila. Layout original.
        /// </summary>
        public static MapDefinition Brasalume()
        {
            const float C = ValeCenter;
            var m = new MapDefinition { Id = 1, Name = "Vale de Brasalume", Size = 2 * C, SafeX = C, SafeZ = C, SafeRadius = TownWallRadius * 1.42f, RespawnX = C, RespawnZ = C - 7f };
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
            float GD = TownWallRadius - 7f;   // guardas por dentro de cada portão, ao lado da avenida
            m.Npc(1, "Mercadora Tália", "shop", C + 15f, C + 20.6f);                                          // no largo do mercado
            m.Npc(2, "Ferreiro Bram", "forge", C - 31.6f, C - 16.8f);                                          // na frente do galpão da forja
            m.Npc(3, "Sentinela Orvo", "guard", C + 2.2f, C - GD, "As cinzas rondam o portão sul. Fique atento aos avisos de invasão!");
            m.Npc(4, "Ilsa, a Guardiã", "vault", C + (float)Math.Cos(293 * Math.PI / 180) * 12.8f, C + (float)Math.Sin(293 * Math.PI / 180) * 12.8f);
            m.Npc(5, "Hadrik, o Arauto das Guildas", "guild", C + 17.5f, C - 8.2f);                           // na escadaria do Salão das Guildas
            m.Npc(6, "Sentinela Brena", "guard", C - 2.2f, C + GD, "O portão norte dá para a mata dos lodos. Nada de pressa por lá.");
            m.Npc(7, "Sentinela Caio", "guard", C + GD, C + 2.2f, "Os portais ficam logo depois do portão leste: verde para o pântano, violeta para a cripta.");
            m.Npc(8, "Sentinela Mírian", "guard", C - GD, C - 2.2f, "Do portão oeste se vê a fumaça das tecelãs. Vá em grupo.");
            m.Npc(9, "Irmã Vesna", "deco", C - 15.8f, C + 5.9f, "O lume da capela nunca se apaga. Que ele guie seus passos, viajante.");
            m.Npc(10, "Cavalariço Doran", "deco", C - 46.0f, C - 4.2f, "Lagartos de basalto aguentam qualquer estrada. A Tália vende os filhotes.");
            m.Npc(11, "Velha Moira", "deco", C - 10.5f, C + 3.8f, "Na minha época a fonte era só uma brasa num buraco. Veja como cresceu!");
            m.Npc(12, "Mestre Talo, o Escriba", "deco", C - 12.0f, C + 9.5f, "Cada runa tem seu humor. A da Aurora é gentil; a do Eclipse, nem tanto.");
            m.Npc(13, "Pequeno Bento", "deco", C - 3.8f, C - 10.3f, "Você já viu um golem de basalto? Dizem que moram depois das torres!");
            // Portais fora do portão leste, longe da muralha (o cliente desenha o arco de pedra com o vórtice da cor do destino)
            m.Portals.Add(new Portal { X = C + 92, Z = C - 12, TargetMap = SwampId, TargetX = 20, TargetZ = 70, Label = "Pântano de Turfa Negra (nível 20+)" });
            m.Portals.Add(new Portal { X = C + 92, Z = C + 12, TargetMap = CryptId, TargetX = 20, TargetZ = 60, Label = "Cripta de Vidro Negro (nível 45+)" });
            m.WarpCode = "VALE";
            m.LayoutVersion = 4;   // v4: cidade murada quadrada (104 m) num Vale de 340 m; posições salvas dentro de prédios vão para o lugar livre mais perto
            return m;
        }

        public const int ValeId = 1, SwampId = 2, CryptId = 3, EscarpId = 4;
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
            m.Portals.Add(new Portal { X = 127, Z = 70, TargetMap = EscarpId, TargetX = 22, TargetZ = 75, Label = "Escarpa da Geada Rubra (nível 80+)" });
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

        /// <summary>Onde a Fenda da Geada Rubra se abre (evento): no planalto central da Escarpa.</summary>
        public const float RiftX = 92f, RiftZ = 75f;

        /// <summary>
        /// Escarpa da Geada Rubra (nível 80-120): planalto gelado e aberto, com agulhas de gelo (obstáculos) e fendas
        /// fundas (não dá para atravessar). Chega-se pelo portal leste do Pântano (e volta-se por ele) ou por /warp. No centro abre-se a Fenda.
        /// </summary>
        public static MapDefinition Escarpment()
        {
            var m = new MapDefinition { Id = EscarpId, Name = "Escarpa da Geada Rubra", Size = 150, SafeRadius = -1, RespawnX = 18, RespawnZ = 75,
                                        MinLevel = 80, WarpCost = 250_000, WarpCode = "ESCARPA" };
            void G(MonsterDefinition d, float x, float z, float r, int n) => m.Spawns.Add(new SpawnGroup { Monster = d, X = x, Z = z, Radius = r, Count = n });
            G(MonsterCatalog.FrostLynx, 44, 44, 9, 6);
            G(MonsterCatalog.FrostLynx, 46, 108, 9, 6);
            G(MonsterCatalog.RimeWarden, 70, 30, 8, 4);
            G(MonsterCatalog.RimeWarden, 72, 122, 8, 4);
            G(MonsterCatalog.FrozenRevenant, 118, 46, 9, 6);
            G(MonsterCatalog.FrozenRevenant, 118, 106, 9, 6);
            G(MonsterCatalog.RimeSerpent, 134, 76, 8, 5);
            // Fendas fundas (círculos grandes) e agulhas de gelo (pequenas): posições fixas, o cliente desenha as mesmas
            foreach (var (x, z, r) in new (float, float, float)[] { (60, 76, 6f), (80, 52, 5f), (82, 100, 5.5f), (104, 76, 4f), (34, 92, 4f), (30, 56, 3.5f), (100, 132, 5f), (100, 20, 5f) })
                m.Obstacles.Add(new Obstacle(x, z, r));
            foreach (var (x, z) in new (float, float)[] { (52, 60), (54, 92), (66, 46), (66, 104), (88, 64), (88, 88), (110, 62), (110, 90), (126, 60), (126, 92), (38, 30), (38, 120) })
                m.Obstacles.Add(new Obstacle(x, z, 1.2f));
            m.Portals.Add(new Portal { X = 10, Z = 75, TargetMap = SwampId, TargetX = 119, TargetZ = 70, Label = "Pântano" });
            return m;
        }

        private static Dictionary<int, MapDefinition>? _all;
        /// <summary>Todos os mapas do jogo por id (instâncias novas a cada servidor via <see cref="CreateAll"/>).</summary>
        public static Dictionary<int, MapDefinition> CreateAll() => new Dictionary<int, MapDefinition>
        {
            { ValeId, Brasalume() }, { SwampId, Swamp() }, { CryptId, Crypt() }, { EscarpId, Escarpment() },
        };

        /// <summary>Catálogo compartilhado (somente leitura) para o cliente desenhar e consultar.</summary>
        public static MapDefinition Get(int id) => (_all ??= CreateAll()).TryGetValue(id, out var m) ? m : (_all[ValeId]);
        public static IEnumerable<MapDefinition> AllMaps => (_all ??= CreateAll()).Values;
    }
}
