using System;
using System.Collections.Generic;
using System.Linq;

namespace Kezor.Core.Characters
{
    /// <summary>Efeito de um nó da Árvore da Ascensão (valor por rank).</summary>
    public enum AscEffect { HpPct, MpPct, Defense, ReductionPct, DamagePct, CritPct, AttackRatePct, DefenseRatePct, Vigor, ShieldPct, SpeedPct, LifeOnKill, MagicPct }

    public sealed class AscNode
    {
        public int Id;
        public int Branch;          // 0 = Bastião, 1 = Fúria, 2 = ramo da classe
        public int Tier, Col;       // linha (0..4) e coluna (0..2) no desenho
        public string Name = "";
        public int MaxRank;
        public AscEffect Effect;
        public int ValuePerRank;
        public int Prereq;          // nó logo acima (0 = nenhum)
        public ClassMask Classes = ClassMask.All;

        /// <summary>Pontos já gastos no ramo para liberar a linha.</summary>
        public int TierPoints => Tier * AscensionTree.PointsPerTier;

        public string Describe(int rank)
        {
            int v = ValuePerRank * Math.Max(1, rank);
            return Effect switch
            {
                AscEffect.HpPct => $"+{v}% de vida máxima",
                AscEffect.MpPct => $"+{v}% de mana máxima",
                AscEffect.Defense => $"+{v} de defesa",
                AscEffect.ReductionPct => $"-{v}% de dano recebido",
                AscEffect.DamagePct => $"+{v}% de dano",
                AscEffect.CritPct => $"+{v}% de chance de crítico",
                AscEffect.AttackRatePct => $"+{v}% de taxa de ataque",
                AscEffect.DefenseRatePct => $"+{v}% de taxa de defesa",
                AscEffect.Vigor => $"+{v} de vigor máximo",
                AscEffect.ShieldPct => $"Escudo +{v}% da vida máxima",
                AscEffect.SpeedPct => $"+{v}% de velocidade de movimento",
                AscEffect.LifeOnKill => $"recupera {v} de vida por abate (× nível/10)",
                AscEffect.MagicPct => $"+{v}% de poder mágico",
                _ => "",
            };
        }
    }

    /// <summary>
    /// Árvore da Ascensão (nível 400): três ramos por classe — Bastião (defesa), Fúria (ataque) e o ramo próprio da
    /// classe. Cada ramo tem 15 nós em 5 linhas × 3 colunas; cada nó pede o de cima e a linha pede pontos gastos no ramo.
    /// </summary>
    public static class AscensionTree
    {
        public const int UnlockLevel = Character.MaxLevel;   // 400
        public const int MaxLevel = 200;
        public const int PointsPerTier = 5;
        public const int Tiers = 5, Cols = 3;
        /// <summary>Custo de redistribuir os pontos: 500 mil de ouro por ponto gasto (mínimo 1 milhão).</summary>
        public static long ResetCost(int spent) => Math.Max(1_000_000L, spent * 500_000L);
        /// <summary>Experiência para o próximo nível de Ascensão (cresce em linha reta: 50 M + 5 M por nível).</summary>
        public static long ExpToNext(int ascLevel) => 50_000_000L + ascLevel * 5_000_000L;

        public static readonly string[] BranchNames = { "Bastião", "Fúria", "" };

        private static readonly Dictionary<int, AscNode> _byId = new Dictionary<int, AscNode>();
        public static IReadOnlyDictionary<int, AscNode> ById => _byId;
        public static IEnumerable<AscNode> All => _byId.Values;

        public static string ClassBranchName(CharacterClass c) => c switch
        {
            CharacterClass.Warden => "Juramento Rúnico", CharacterClass.Arcanist => "Profundezas do Véu",
            CharacterClass.Ranger => "Lei da Mata", CharacterClass.Bladeweaver => "Trama da Lâmina",
            CharacterClass.Commander => "Legião", CharacterClass.Invoker => "Coro dos Ecos",
            CharacterClass.EmberFist => "Forja Viva", _ => "Classe",
        };

        public static string BranchName(int branch, CharacterClass c) => branch == 2 ? ClassBranchName(c) : BranchNames[branch];

        /// <summary>Nós que a classe enxerga (os dois ramos comuns e o dela), na ordem ramo/linha/coluna.</summary>
        public static List<AscNode> For(CharacterClass c)
        {
            var mask = ClassDefinition.MaskOf(c);
            return _byId.Values.Where(n => (n.Classes & mask) != 0).OrderBy(n => n.Branch).ThenBy(n => n.Tier).ThenBy(n => n.Col).ToList();
        }

        public static AscNode? Get(int id) => _byId.TryGetValue(id, out var n) ? n : null;

        private static void Branch(int baseId, int branch, ClassMask classes, (string name, AscEffect e, int v)[] nodes)
        {
            for (int i = 0; i < nodes.Length; i++)
            {
                int tier = i / Cols, col = i % Cols;
                var n = new AscNode
                {
                    Id = baseId + i, Branch = branch, Tier = tier, Col = col, Name = nodes[i].name, Effect = nodes[i].e, ValuePerRank = nodes[i].v,
                    MaxRank = tier == Tiers - 1 ? 3 : 5, Prereq = tier == 0 ? 0 : baseId + i - Cols, Classes = classes,
                };
                _byId[n.Id] = n;
            }
        }

        private static (string, AscEffect, int)[] Pattern(string[] names, bool magic, bool hybrid)
        {
            AscEffect Dmg(int i) => hybrid ? (i % 2 == 0 ? AscEffect.DamagePct : AscEffect.MagicPct) : magic ? AscEffect.MagicPct : AscEffect.DamagePct;
            int Dv(int baseV) => magic ? baseV + 1 : baseV;
            var e = new (AscEffect, int)[]
            {
                (Dmg(0), Dv(1)), (magic ? AscEffect.MpPct : AscEffect.HpPct, magic ? 2 : 1), (AscEffect.Vigor, 6),
                (AscEffect.CritPct, 1), (magic ? AscEffect.ShieldPct : AscEffect.Defense, magic ? 2 : 5), (AscEffect.AttackRatePct, 2),
                (Dmg(1), Dv(2)), (AscEffect.ShieldPct, 2), (AscEffect.SpeedPct, 1),
                (Dmg(2), Dv(2)), (AscEffect.ReductionPct, 1), (magic ? AscEffect.MpPct : AscEffect.LifeOnKill, magic ? 2 : 1),
                (Dmg(3), Dv(3)), (magic ? AscEffect.ShieldPct : AscEffect.HpPct, magic ? 4 : 3), (AscEffect.CritPct, 2),
            };
            return names.Select((n, i) => (n, e[i].Item1, e[i].Item2)).ToArray();
        }

        static AscensionTree()
        {
            Branch(1000, 0, ClassMask.All, new[]
            {
                ("Pele de Basalto", AscEffect.HpPct, 1), ("Postura Firme", AscEffect.Defense, 5), ("Olhar Atento", AscEffect.DefenseRatePct, 2),
                ("Couraça de Cinza", AscEffect.ReductionPct, 1), ("Sangue Teimoso", AscEffect.HpPct, 1), ("Passo Seguro", AscEffect.Vigor, 6),
                ("Brasa Interior", AscEffect.ShieldPct, 2), ("Muralha Viva", AscEffect.Defense, 8), ("Reflexo de Guarda", AscEffect.DefenseRatePct, 3),
                ("Juramento de Ferro", AscEffect.ReductionPct, 1), ("Coração de Forja", AscEffect.HpPct, 2), ("Fôlego de Pedra", AscEffect.Vigor, 8),
                ("Bastião Inabalável", AscEffect.ReductionPct, 2), ("Escudo de Lume", AscEffect.ShieldPct, 4), ("Raízes de Basalto", AscEffect.HpPct, 3),
            });
            Branch(1100, 1, ClassMask.All, new[]
            {
                ("Gume Faminto", AscEffect.DamagePct, 1), ("Mão Certeira", AscEffect.AttackRatePct, 2), ("Olho de Brasa", AscEffect.CritPct, 1),
                ("Sede de Cinza", AscEffect.DamagePct, 1), ("Ritmo de Guerra", AscEffect.AttackRatePct, 2), ("Golpe Vivo", AscEffect.LifeOnKill, 1),
                ("Chama Crescente", AscEffect.DamagePct, 2), ("Ponto Fraco", AscEffect.CritPct, 1), ("Passo Ligeiro", AscEffect.SpeedPct, 1),
                ("Fúria Rubra", AscEffect.DamagePct, 2), ("Instinto Caçador", AscEffect.AttackRatePct, 3), ("Lâmina Sedenta", AscEffect.LifeOnKill, 2),
                ("Tempestade de Brasas", AscEffect.DamagePct, 3), ("Execução", AscEffect.CritPct, 2), ("Avanço Implacável", AscEffect.SpeedPct, 2),
            });
            Branch(2000, 2, ClassMask.Warden, Pattern(new[]
            {
                "Runa do Juramento", "Placa Gravada", "Vigília Rúnica", "Selo de Pedra", "Marca do Escudo", "Ritmo do Martelo",
                "Runa Ardente", "Égide Rúnica", "Marcha Rúnica", "Golpe do Juramento", "Pacto de Ferro", "Runa Sedenta",
                "Sentença Rúnica", "Guardião Eterno", "Olho do Juramento",
            }, false, false));
            Branch(2100, 2, ClassMask.Arcanist, Pattern(new[]
            {
                "Véu Desperto", "Poço Arcano", "Respiração Calma", "Fio do Véu", "Manto de Névoa", "Olhar Além",
                "Maré Arcana", "Reserva Profunda", "Passo Etéreo", "Eco do Véu", "Véu Protetor", "Mente Serena",
                "Ruptura do Véu", "Muralha de Névoa", "Abismo Arcano",
            }, true, false));
            Branch(2200, 2, ClassMask.Ranger, Pattern(new[]
            {
                "Pena Afiada", "Couro Curtido", "Trilha Longa", "Olho de Falcão", "Pele de Lobo", "Corda Tensa",
                "Flecha Funda", "Camuflagem", "Pés de Raposa", "Ponta Serrilhada", "Casca de Carvalho", "Caçada Viva",
                "Chuva de Penas", "Espírito da Mata", "Tiro do Predador",
            }, false, false));
            Branch(2300, 2, ClassMask.Bladeweaver, Pattern(new[]
            {
                "Fio Rúnico", "Trama Arcana", "Dança Leve", "Corte Marcado", "Malha do Véu", "Ritmo da Lâmina",
                "Gume Encantado", "Bainha de Mana", "Passo Cortante", "Lâmina Dupla", "Guarda Tecida", "Sede do Fio",
                "Corte do Véu", "Trama Viva", "Tempestade de Fios",
            }, false, true));
            Branch(2400, 2, ClassMask.Commander, Pattern(new[]
            {
                "Voz de Comando", "Estandarte Firme", "Marcha Longa", "Ordem de Ataque", "Formação Cerrada", "Tambor de Guerra",
                "Carga do Estandarte", "Bandeira de Lume", "Avanço da Tropa", "Golpe de Honra", "Linha de Escudos", "Saque de Guerra",
                "Brado Rubro", "Legião de Ferro", "Comando Absoluto",
            }, false, false));
            Branch(2500, 2, ClassMask.Invoker, Pattern(new[]
            {
                "Eco Primeiro", "Lago de Ecos", "Sopro Antigo", "Voz Dupla", "Véu dos Mortos", "Sussurro Certeiro",
                "Coro de Ecos", "Memória Funda", "Guarda Espectral", "Lamento Agudo", "Selo do Silêncio", "Fôlego dos Ecos",
                "Sinfonia Morta", "Abrigo Fantasma", "Grande Coro",
            }, true, false));
            Branch(2600, 2, ClassMask.EmberFist, Pattern(new[]
            {
                "Nó dos Dedos", "Pele Endurecida", "Fôlego de Forja", "Brasa no Punho", "Calo de Ferro", "Golpe em Cadeia",
                "Punho Rubro", "Couraça Viva", "Passo de Brasa", "Gancho Fundo", "Postura de Forja", "Punho Sedento",
                "Fluxo Ardente", "Corpo de Bigorna", "Punho do Vulcão",
            }, false, false));
        }
    }

    /// <summary>
    /// Progresso de Ascensão de um herói. Libera ao chegar ao nível 400 pela primeira vez e é permanente: renascer não
    /// apaga nível, pontos nem nós (e os bônus continuam valendo). A experiência só entra enquanto o herói está no 400.
    /// </summary>
    public sealed class AscensionState
    {
        public bool Unlocked;
        public int Level;
        public long Exp;
        public readonly Dictionary<int, int> Ranks = new Dictionary<int, int>();
        public int Version { get; private set; }

        public int Spent => Ranks.Values.Sum();
        public int Points => Math.Max(0, Level - Spent);
        public void Touch() => Version++;

        public int RankOf(int id) => Ranks.TryGetValue(id, out var r) ? r : 0;
        public int SpentIn(int branch) => Ranks.Where(kv => AscensionTree.Get(kv.Key)?.Branch == branch).Sum(kv => kv.Value);

        /// <summary>Ganha experiência de Ascensão; retorna níveis ganhos.</summary>
        public int Gain(long amount)
        {
            if (!Unlocked || amount <= 0 || Level >= AscensionTree.MaxLevel) return 0;
            Exp += amount;
            int gained = 0;
            while (Level < AscensionTree.MaxLevel && Exp >= AscensionTree.ExpToNext(Level)) { Exp -= AscensionTree.ExpToNext(Level); Level++; gained++; }
            if (Level >= AscensionTree.MaxLevel) Exp = 0;
            Touch();
            return gained;
        }

        public bool CanLearn(CharacterClass cls, AscNode n, out string error)
        {
            error = "";
            if (!Unlocked) { error = $"a Ascensão libera no nível {AscensionTree.UnlockLevel}"; return false; }
            if ((n.Classes & ClassDefinition.MaskOf(cls)) == 0) { error = "esse ramo é de outra classe"; return false; }
            if (Points <= 0) { error = "sem pontos de Ascensão"; return false; }
            if (RankOf(n.Id) >= n.MaxRank) { error = "nó no máximo"; return false; }
            if (n.Prereq != 0 && RankOf(n.Prereq) <= 0) { error = $"aprenda antes {AscensionTree.Get(n.Prereq)?.Name}"; return false; }
            if (SpentIn(n.Branch) < n.TierPoints) { error = $"gaste {n.TierPoints} pontos no ramo para liberar esta linha"; return false; }
            return true;
        }

        public bool Learn(CharacterClass cls, int id, out string error)
        {
            var n = AscensionTree.Get(id);
            if (n == null) { error = "nó inexistente"; return false; }
            if (!CanLearn(cls, n, out error)) return false;
            Ranks[id] = RankOf(id) + 1;
            Touch();
            return true;
        }

        public void ResetNodes() { Ranks.Clear(); Touch(); }

        /// <summary>Soma do efeito em todos os nós aprendidos.</summary>
        public int Total(AscEffect e)
        {
            int t = 0;
            foreach (var kv in Ranks) { var n = AscensionTree.Get(kv.Key); if (n != null && n.Effect == e) t += n.ValuePerRank * kv.Value; }
            return t;
        }
    }
}
