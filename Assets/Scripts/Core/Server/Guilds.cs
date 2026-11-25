using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Kezor.Core.Server
{
    public enum GuildRank { Member = 0, Officer = 1, Leader = 2 }

    /// <summary>Guilda: nome, membros por nome de personagem com cargo (líder, oficial, membro).</summary>
    public sealed class Guild
    {
        public const int MaxMembers = 30;
        public const long CreateCost = 500_000;
        public const int CreateMinLevel = 50;
        public string Name = "";
        public readonly Dictionary<string, GuildRank> Members = new Dictionary<string, GuildRank>(StringComparer.OrdinalIgnoreCase);
        public DateTime Created = DateTime.UtcNow;
        /// <summary>Resultado das guerras de guilda.</summary>
        public int Wins, Losses, Draws;
        public string Leader => Members.FirstOrDefault(kv => kv.Value == GuildRank.Leader).Key ?? "";
        public GuildRank RankOf(string name) => Members.TryGetValue(name, out var r) ? r : GuildRank.Member;

        public static bool ValidName(string n) => n.Length >= 3 && n.Length <= 14 && n.All(char.IsLetterOrDigit);
        public static string RankName(GuildRank r) => r switch { GuildRank.Leader => "Líder", GuildRank.Officer => "Oficial", _ => "Membro" };
    }

    public interface IGuildRepository
    {
        IEnumerable<Guild> LoadAllGuilds();
        void SaveGuild(Guild g);
        void DeleteGuild(string name);
    }

    public sealed class InMemoryGuildRepository : IGuildRepository
    {
        private readonly Dictionary<string, Guild> _db = new Dictionary<string, Guild>(StringComparer.OrdinalIgnoreCase);
        public IEnumerable<Guild> LoadAllGuilds() => _db.Values.ToList();
        public void SaveGuild(Guild g) => _db[g.Name] = g;
        public void DeleteGuild(string name) => _db.Remove(name);
    }

    public static class GuildSave
    {
        public static Dictionary<string, object?> ToJson(Guild g) => new Dictionary<string, object?>
        {
            ["v"] = 1, ["name"] = g.Name, ["created"] = g.Created.ToString("o"), ["wins"] = g.Wins, ["losses"] = g.Losses, ["draws"] = g.Draws,
            ["members"] = g.Members.Select(kv => (object?)new Dictionary<string, object?> { ["name"] = kv.Key, ["rank"] = kv.Value.ToString() }).ToList(),
        };

        public static Guild FromJson(IDictionary<string, object?> o)
        {
            var g = new Guild { Name = o.S("name"), Wins = o.I("wins"), Losses = o.I("losses"), Draws = o.I("draws") };
            if (DateTime.TryParse(o.S("created"), null, System.Globalization.DateTimeStyles.RoundtripKind, out var dt)) g.Created = dt;
            foreach (var m in o.A("members").OfType<IDictionary<string, object?>>())
                if (Enum.TryParse<GuildRank>(m.S("rank"), out var r)) g.Members[m.S("name")] = r;
            return g;
        }
    }

    public sealed partial class JsonFileRepository : IGuildRepository
    {
        private string GuildDir { get { var d = Path.Combine(Path.GetDirectoryName(_chars)!, "guilds"); Directory.CreateDirectory(d); return d; } }

        public IEnumerable<Guild> LoadAllGuilds()
        {
            var list = new List<Guild>();
            foreach (var f in Directory.GetFiles(GuildDir, "*.json"))
                if (MiniJson.Read(File.ReadAllText(f)) is IDictionary<string, object?> o) list.Add(GuildSave.FromJson(o));
            return list;
        }

        public void SaveGuild(Guild g)
        {
            var path = Path.Combine(GuildDir, g.Name.ToLowerInvariant() + ".json");
            var tmp = path + ".tmp";
            File.WriteAllText(tmp, MiniJson.Write(GuildSave.ToJson(g)), new System.Text.UTF8Encoding(false));
            if (File.Exists(path)) File.Replace(tmp, path, null); else File.Move(tmp, path);
        }

        public void DeleteGuild(string name)
        {
            var path = Path.Combine(GuildDir, name.ToLowerInvariant() + ".json");
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
