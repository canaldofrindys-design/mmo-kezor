using System;
using System.Collections.Generic;
using System.Data.Common;
using System.IO;
using System.Linq;
using Kezor.Core.Characters;
using Kezor.Core.Server;
using Microsoft.Data.Sqlite;
using MySqlConnector;

namespace Kezor.Server.Data
{
    /// <summary>
    /// Banco de dados SQL do servidor dedicado: SQLite (um arquivo, sem instalar nada; padrão) ou MySQL/MariaDB.
    /// Tabelas: accounts, characters, guilds, ranking. Cada linha tem as colunas para consulta (conta, classe, nível,
    /// renascimentos, ban...) e o registro completo em JSON na coluna data (o mesmo formato dos saves em arquivo).
    /// Abre uma conexão por operação (o pool do driver reaproveita); chamado só pela thread do jogo.
    /// </summary>
    public sealed class SqlRepository : ICharacterRepository, IAccountRepository, IGuildRepository, IRankingRepository
    {
        public enum Kind { Sqlite, MySql }
        public Kind Provider { get; }
        private readonly string _cs;
        private int _nextId = 1_000_000;

        /// <summary>"sqlite:caminho/kezor.db" ou "mysql:Server=..;Database=..;User ID=..;Password=..".</summary>
        public static SqlRepository Open(string spec)
        {
            var i = spec.IndexOf(':');
            if (i <= 0) throw new ArgumentException("use sqlite:<arquivo> ou mysql:<conexão>");
            var kind = spec.Substring(0, i).Trim().ToLowerInvariant();
            var rest = spec.Substring(i + 1).Trim();
            return kind switch
            {
                "sqlite" => new SqlRepository(Kind.Sqlite, new SqliteConnectionStringBuilder { DataSource = rest, Cache = SqliteCacheMode.Shared, Pooling = true }.ToString()),
                "mysql" or "mariadb" => new SqlRepository(Kind.MySql, rest),
                _ => throw new ArgumentException($"banco desconhecido: {kind} (use sqlite ou mysql)"),
            };
        }

        public SqlRepository(Kind provider, string connectionString)
        {
            Provider = provider; _cs = connectionString;
            if (provider == Kind.Sqlite)
            {
                var file = new SqliteConnectionStringBuilder(connectionString).DataSource;
                var dir = Path.GetDirectoryName(Path.GetFullPath(file));
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            }
            else EnsureMySqlDatabase();
            CreateSchema();
        }

        /// <summary>Descrição sem senha, para o registro.</summary>
        public string Describe()
        {
            if (Provider == Kind.Sqlite) return "SQLite " + new SqliteConnectionStringBuilder(_cs).DataSource;
            var b = new MySqlConnectionStringBuilder(_cs);
            return $"MySQL {b.Server}:{b.Port}/{b.Database}";
        }

        /// <summary>Cria o banco (database) no MySQL se ainda não existir (o usuário precisa ter permissão).</summary>
        private void EnsureMySqlDatabase()
        {
            var b = new MySqlConnectionStringBuilder(_cs);
            var db = b.Database;
            if (string.IsNullOrEmpty(db)) throw new ArgumentException("informe Database=... na conexão do MySQL");
            if (!db.All(ch => char.IsLetterOrDigit(ch) || ch == '_')) throw new ArgumentException("nome do banco: só letras, números e _");
            try { using var test = new MySqlConnection(_cs); test.Open(); return; }
            catch (MySqlException e) when (e.ErrorCode == MySqlErrorCode.UnknownDatabase) { }
            b.Database = "";
            using var c = new MySqlConnection(b.ConnectionString);
            c.Open();
            Exec(c, $"CREATE DATABASE IF NOT EXISTS `{db}` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci");
        }

        private DbConnection Connect()
        {
            DbConnection c = Provider == Kind.Sqlite ? new SqliteConnection(_cs) : new MySqlConnection(_cs);
            c.Open();
            if (Provider == Kind.Sqlite) Exec(c, "PRAGMA journal_mode=WAL; PRAGMA synchronous=NORMAL; PRAGMA busy_timeout=5000;");
            return c;
        }

        private static int Exec(DbConnection c, string sql, params (string, object?)[] ps)
        {
            using var cmd = c.CreateCommand();
            cmd.CommandText = sql;
            foreach (var (n, v) in ps) { var p = cmd.CreateParameter(); p.ParameterName = n; p.Value = v ?? DBNull.Value; cmd.Parameters.Add(p); }
            return cmd.ExecuteNonQuery();
        }

        private static List<object?[]> Query(DbConnection c, string sql, params (string, object?)[] ps)
        {
            using var cmd = c.CreateCommand();
            cmd.CommandText = sql;
            foreach (var (n, v) in ps) { var p = cmd.CreateParameter(); p.ParameterName = n; p.Value = v ?? DBNull.Value; cmd.Parameters.Add(p); }
            using var r = cmd.ExecuteReader();
            var rows = new List<object?[]>();
            while (r.Read())
            {
                var row = new object?[r.FieldCount];
                for (int i = 0; i < r.FieldCount; i++) row[i] = r.IsDBNull(i) ? null : r.GetValue(i);
                rows.Add(row);
            }
            return rows;
        }

        private string Text => Provider == Kind.MySql ? "LONGTEXT" : "TEXT";

        private void CreateSchema()
        {
            using var c = Connect();
            string tail = Provider == Kind.MySql ? " ENGINE=InnoDB DEFAULT CHARSET=utf8mb4" : "";
            Exec(c, $@"CREATE TABLE IF NOT EXISTS accounts (
                name VARCHAR(16) NOT NULL PRIMARY KEY,
                password_hash VARCHAR(200) NOT NULL,
                created VARCHAR(32) NOT NULL,
                banned_until VARCHAR(32) NULL,
                data {Text} NOT NULL){tail}");
            Exec(c, $@"CREATE TABLE IF NOT EXISTS characters (
                name VARCHAR(16) NOT NULL PRIMARY KEY,
                account VARCHAR(16) NOT NULL,
                class INT NOT NULL,
                level INT NOT NULL,
                rebirths INT NOT NULL,
                updated VARCHAR(32) NOT NULL,
                data {Text} NOT NULL){tail}");
            Exec(c, $@"CREATE TABLE IF NOT EXISTS guilds (
                name VARCHAR(32) NOT NULL PRIMARY KEY,
                data {Text} NOT NULL){tail}");
            Exec(c, $@"CREATE TABLE IF NOT EXISTS ranking (
                id INT NOT NULL PRIMARY KEY,
                data {Text} NOT NULL){tail}");
            if (Provider == Kind.Sqlite) Exec(c, "CREATE INDEX IF NOT EXISTS ix_characters_account ON characters(account)");
            else if (Query(c, "SELECT 1 FROM information_schema.statistics WHERE table_schema = DATABASE() AND table_name = 'characters' AND index_name = 'ix_characters_account'").Count == 0)
                Exec(c, "CREATE INDEX ix_characters_account ON characters(account)");
        }

        /// <summary>INSERT ou UPDATE pela chave (SQLite: ON CONFLICT; MySQL: ON DUPLICATE KEY).</summary>
        private string Upsert(string table, string key, params string[] cols)
        {
            var all = new[] { key }.Concat(cols).ToArray();
            var ins = $"INSERT INTO {table} ({string.Join(", ", all)}) VALUES ({string.Join(", ", all.Select(x => "@" + x))})";
            return Provider == Kind.MySql
                ? ins + " ON DUPLICATE KEY UPDATE " + string.Join(", ", cols.Select(x => $"{x} = VALUES({x})"))
                : ins + $" ON CONFLICT({key}) DO UPDATE SET " + string.Join(", ", cols.Select(x => $"{x} = excluded.{x}"));
        }

        private static string Key(string name) => name.ToLowerInvariant();
        private static string Now() => DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");

        // ------------------------------------------------------------ personagens
        public Character? Load(string name)
        {
            if (string.IsNullOrEmpty(name) || !name.All(char.IsLetterOrDigit)) return null;
            using var c = Connect();
            var rows = Query(c, "SELECT data FROM characters WHERE name = @name", ("@name", Key(name)));
            if (rows.Count == 0 || !(MiniJson.Read((string)rows[0][0]!) is IDictionary<string, object?> o)) return null;
            return SaveData.CharacterFromJson(o, _nextId++);
        }

        public void Save(Character ch)
        {
            using var c = Connect();
            Exec(c, Upsert("characters", "name", "account", "class", "level", "rebirths", "updated", "data"),
                ("@name", Key(ch.Name)), ("@account", Key(ch.AccountName)), ("@class", (int)ch.Class), ("@level", ch.Level), ("@rebirths", ch.Rebirths),
                ("@updated", Now()), ("@data", MiniJson.Write(SaveData.CharacterToJson(ch, includeVault: ch.AccountName == ""), pretty: false)));
        }

        public void Delete(string name)
        {
            using var c = Connect();
            Exec(c, "DELETE FROM characters WHERE name = @name", ("@name", Key(name)));
        }

        // ------------------------------------------------------------ contas
        Account? IAccountRepository.Load(string name)
        {
            if (string.IsNullOrEmpty(name) || !name.All(char.IsLetterOrDigit)) return null;
            using var c = Connect();
            var rows = Query(c, "SELECT data FROM accounts WHERE name = @name", ("@name", Key(name)));
            if (rows.Count == 0 || !(MiniJson.Read((string)rows[0][0]!) is IDictionary<string, object?> o)) return null;
            return SaveData.AccountFromJson(o);
        }

        public void Save(Account a)
        {
            using var c = Connect();
            Exec(c, Upsert("accounts", "name", "password_hash", "created", "banned_until", "data"),
                ("@name", Key(a.Name)), ("@password_hash", a.PasswordHash), ("@created", a.Created.ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss")),
                ("@banned_until", a.BannedUntil.HasValue ? a.BannedUntil.Value.ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss") : null),
                ("@data", MiniJson.Write(SaveData.AccountToJson(a), pretty: false)));
        }

        // ------------------------------------------------------------ guildas
        public IEnumerable<Guild> LoadAllGuilds()
        {
            using var c = Connect();
            return Query(c, "SELECT data FROM guilds").Select(r => MiniJson.Read((string)r[0]!)).OfType<IDictionary<string, object?>>().Select(GuildSave.FromJson).ToList();
        }

        public void SaveGuild(Guild g)
        {
            using var c = Connect();
            Exec(c, Upsert("guilds", "name", "data"), ("@name", g.Name.ToLowerInvariant()), ("@data", MiniJson.Write(GuildSave.ToJson(g), pretty: false)));
        }

        public void DeleteGuild(string name)
        {
            using var c = Connect();
            Exec(c, "DELETE FROM guilds WHERE name = @name", ("@name", name.ToLowerInvariant()));
        }

        // ------------------------------------------------------------ ranking
        public IEnumerable<RankingEntry> LoadRanking()
        {
            using var c = Connect();
            var rows = Query(c, "SELECT data FROM ranking WHERE id = 1");
            if (rows.Count > 0 && MiniJson.Read((string)rows[0][0]!) is IDictionary<string, object?> root)
                return root.A("entries").OfType<IDictionary<string, object?>>().Select(Ranking.FromJson).ToList();
            // sem ranking salvo: monta a partir dos personagens
            return Query(c, "SELECT data FROM characters").Select(r => MiniJson.Read((string)r[0]!)).OfType<IDictionary<string, object?>>()
                .Select(o => { Enum.TryParse<CharacterClass>(o.S("class"), out var cls); return new RankingEntry { Name = o.S("name"), Class = cls, Level = o.I("level", 1), Rebirths = o.I("rebirths"), Experience = o.L("exp") }; })
                .ToList();
        }

        public void SaveRanking(IEnumerable<RankingEntry> entries)
        {
            var root = new Dictionary<string, object?> { ["v"] = 1, ["entries"] = entries.Select(e => (object?)Ranking.ToJson(e)).ToList() };
            using var c = Connect();
            Exec(c, Upsert("ranking", "id", "data"), ("@id", 1), ("@data", MiniJson.Write(root, pretty: false)));
        }

        /// <summary>
        /// SQLite: cópia consistente do banco (mesmo com o servidor rodando) em pasta/kezor-AAAAMMDD-HHmm.db; guarda as
        /// últimas "keep". MySQL: não faz nada (use mysqldump). Devolve o arquivo criado ou null.
        /// </summary>
        public string? Backup(string folder, int keep = 14)
        {
            if (Provider != Kind.Sqlite) return null;
            Directory.CreateDirectory(folder);
            var file = Path.Combine(folder, $"kezor-{DateTime.Now:yyyyMMdd-HHmm}.db");
            if (File.Exists(file)) File.Delete(file);
            using (var c = Connect()) Exec(c, "VACUUM INTO @f", ("@f", file));
            foreach (var old in Directory.GetFiles(folder, "kezor-*.db").OrderByDescending(f => f).Skip(keep)) File.Delete(old);
            return file;
        }

        // ------------------------------------------------------------ números e migração
        public int CountAccounts() { using var c = Connect(); return Convert.ToInt32(Query(c, "SELECT COUNT(*) FROM accounts")[0][0]); }
        public int CountCharacters() { using var c = Connect(); return Convert.ToInt32(Query(c, "SELECT COUNT(*) FROM characters")[0][0]); }

        /// <summary>
        /// Copia os saves em arquivos JSON (pasta com accounts/, characters/, guilds/ e ranking.json) para o banco.
        /// Não apaga nada da pasta. Registros que já existem no banco com o mesmo nome são substituídos.
        /// </summary>
        public (int accounts, int characters, int guilds) ImportJson(string folder, Action<string>? log = null)
        {
            int na = 0, nc = 0, ng = 0;
            var accDir = Path.Combine(folder, "accounts");
            if (Directory.Exists(accDir))
                foreach (var f in Directory.GetFiles(accDir, "*.json"))
                    try { if (MiniJson.Read(File.ReadAllText(f)) is IDictionary<string, object?> o) { Save(SaveData.AccountFromJson(o)); na++; } }
                    catch (Exception e) { log?.Invoke($"conta ignorada ({Path.GetFileName(f)}): {e.Message}"); }
            var chDir = Path.Combine(folder, "characters");
            if (Directory.Exists(chDir))
                foreach (var f in Directory.GetFiles(chDir, "*.json"))
                    try { if (MiniJson.Read(File.ReadAllText(f)) is IDictionary<string, object?> o) { Save(SaveData.CharacterFromJson(o, _nextId++)); nc++; } }
                    catch (Exception e) { log?.Invoke($"personagem ignorado ({Path.GetFileName(f)}): {e.Message}"); }
            if (Directory.Exists(Path.Combine(folder, "guilds")) || File.Exists(Path.Combine(folder, "ranking.json")))
            {
                var json = new JsonFileRepository(folder);
                foreach (var g in json.LoadAllGuilds()) { SaveGuild(g); ng++; }
                if (File.Exists(Path.Combine(folder, "ranking.json"))) SaveRanking(json.LoadRanking());
            }
            return (na, nc, ng);
        }
    }
}
