using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Kezor.Core.Characters;

namespace Kezor.Core.Server
{
    /// <summary>Linha do ranking: o servidor guarda um resumo de cada personagem (atualizado ao salvar e ao subir de nível).</summary>
    public sealed class RankingEntry
    {
        public string Name = "";
        public CharacterClass Class;
        public int Level, Rebirths;
        public long Experience;
        /// <summary>Pontuação: 400 por renascimento + nível (renascer vale mais que qualquer nível).</summary>
        public long Score => Rebirths * 400L + Level;
    }

    public sealed class GuildRankingEntry
    {
        public string Name = "";
        public int Members, Wins, Losses, Draws;
        /// <summary>Soma da pontuação dos membros + 1000 por vitória em guerra.</summary>
        public long Score;
    }

    /// <summary>Ranking calculado no servidor: top por pontuação, por classe e de guildas.</summary>
    public sealed class Ranking
    {
        private readonly Dictionary<string, RankingEntry> _entries = new Dictionary<string, RankingEntry>(StringComparer.OrdinalIgnoreCase);
        public IReadOnlyCollection<RankingEntry> Entries => _entries.Values;
        public bool Dirty { get; private set; }

        public void Update(Character c)
        {
            if (!_entries.TryGetValue(c.Name, out var e)) _entries[c.Name] = e = new RankingEntry { Name = c.Name };
            if (e.Class == c.Class && e.Level == c.Level && e.Rebirths == c.Rebirths && e.Experience == c.Experience) return;
            e.Class = c.Class; e.Level = c.Level; e.Rebirths = c.Rebirths; e.Experience = c.Experience;
            Dirty = true;
        }

        public void Load(IEnumerable<RankingEntry> entries) { foreach (var e in entries) _entries[e.Name] = e; }
        public void MarkSaved() => Dirty = false;
        public RankingEntry? Get(string name) => _entries.TryGetValue(name, out var e) ? e : null;

        private static IEnumerable<RankingEntry> Ordered(IEnumerable<RankingEntry> src) =>
            src.OrderByDescending(e => e.Score).ThenByDescending(e => e.Experience).ThenBy(e => e.Name, StringComparer.OrdinalIgnoreCase);

        public List<RankingEntry> Top(int n = 10, CharacterClass? cls = null) =>
            Ordered(cls == null ? _entries.Values : _entries.Values.Where(e => e.Class == cls.Value)).Take(n).ToList();

        /// <summary>Posição (1 = primeiro) no ranking geral ou da classe; 0 se não está no ranking.</summary>
        public int PositionOf(string name, CharacterClass? cls = null)
        {
            int i = 1;
            foreach (var e in Ordered(cls == null ? _entries.Values : _entries.Values.Where(x => x.Class == cls.Value)))
            {
                if (string.Equals(e.Name, name, StringComparison.OrdinalIgnoreCase)) return i;
                i++;
            }
            return 0;
        }

        public List<GuildRankingEntry> Guilds(IEnumerable<Guild> guilds, int n = 10) => guilds
            .Select(g => new GuildRankingEntry
            {
                Name = g.Name, Members = g.Members.Count, Wins = g.Wins, Losses = g.Losses, Draws = g.Draws,
                Score = g.Members.Keys.Sum(m => Get(m)?.Score ?? 0) + g.Wins * 1000L,
            })
            .OrderByDescending(x => x.Score).ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase).Take(n).ToList();

        public static Dictionary<string, object?> ToJson(RankingEntry e) => new Dictionary<string, object?>
        { ["name"] = e.Name, ["class"] = e.Class.ToString(), ["level"] = e.Level, ["rebirths"] = e.Rebirths, ["exp"] = e.Experience };

        public static RankingEntry FromJson(IDictionary<string, object?> o)
        {
            Enum.TryParse<CharacterClass>(o.S("class"), out var cls);
            return new RankingEntry { Name = o.S("name"), Class = cls, Level = o.I("level", 1), Rebirths = o.I("rebirths"), Experience = o.L("exp") };
        }
    }

    public interface IRankingRepository
    {
        IEnumerable<RankingEntry> LoadRanking();
        void SaveRanking(IEnumerable<RankingEntry> entries);
    }

    public sealed partial class JsonFileRepository : IRankingRepository
    {
        private string RankingFile => Path.Combine(Path.GetDirectoryName(_chars)!, "ranking.json");

        /// <summary>Lê ranking.json; se não existir, monta a partir dos personagens salvos.</summary>
        public IEnumerable<RankingEntry> LoadRanking()
        {
            if (File.Exists(RankingFile) && MiniJson.Read(File.ReadAllText(RankingFile)) is IDictionary<string, object?> root)
                return root.A("entries").OfType<IDictionary<string, object?>>().Select(Ranking.FromJson).ToList();
            var list = new List<RankingEntry>();
            foreach (var f in Directory.GetFiles(_chars, "*.json"))
                if (MiniJson.Read(File.ReadAllText(f)) is IDictionary<string, object?> o)
                {
                    Enum.TryParse<CharacterClass>(o.S("class"), out var cls);
                    list.Add(new RankingEntry { Name = o.S("name"), Class = cls, Level = o.I("level", 1), Rebirths = o.I("rebirths"), Experience = o.L("exp") });
                }
            return list;
        }

        public void SaveRanking(IEnumerable<RankingEntry> entries)
        {
            var root = new Dictionary<string, object?> { ["v"] = 1, ["entries"] = entries.Select(e => (object?)Ranking.ToJson(e)).ToList() };
            var tmp = RankingFile + ".tmp";
            File.WriteAllText(tmp, MiniJson.Write(root, pretty: false), new System.Text.UTF8Encoding(false));
            if (File.Exists(RankingFile)) File.Replace(tmp, RankingFile, null); else File.Move(tmp, RankingFile);
        }
    }
}
