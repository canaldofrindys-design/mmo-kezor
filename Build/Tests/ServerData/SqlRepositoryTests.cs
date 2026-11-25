using System;
using System.IO;
using System.Linq;
using Kezor.Core;
using Kezor.Core.Characters;
using Kezor.Core.Items;
using Kezor.Core.Server;
using Kezor.Server.Data;
using NUnit.Framework;

namespace Kezor.Tests
{
    /// <summary>
    /// Banco SQL do servidor dedicado. Roda sempre com SQLite; com MySQL/MariaDB quando a variável KEZOR_TEST_MYSQL tem a
    /// conexão (ex.: Server=localhost;Database=kezor_test;User ID=kezor;Password=...). O banco de teste é zerado.
    /// </summary>
    [TestFixture("sqlite")]
    [TestFixture("mysql")]
    public class SqlRepositoryTests
    {
        private readonly string _kind;
        private string _dir = "";
        public SqlRepositoryTests(string kind) { _kind = kind; }

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "kezor-sql-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
            if (_kind == "mysql")
            {
                var cs = Environment.GetEnvironmentVariable("KEZOR_TEST_MYSQL");
                if (string.IsNullOrWhiteSpace(cs)) Assert.Ignore("sem KEZOR_TEST_MYSQL: teste do MySQL pulado");
                using var c = new MySqlConnector.MySqlConnection(cs); c.Open();
                foreach (var t in new[] { "accounts", "characters", "guilds", "ranking" })
                { using var cmd = c.CreateCommand(); cmd.CommandText = "DROP TABLE IF EXISTS " + t; cmd.ExecuteNonQuery(); }
            }
        }

        [TearDown]
        public void TearDown() { Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools(); try { Directory.Delete(_dir, true); } catch { } }

        private SqlRepository Open() => _kind == "mysql"
            ? SqlRepository.Open("mysql:" + Environment.GetEnvironmentVariable("KEZOR_TEST_MYSQL"))
            : SqlRepository.Open("sqlite:" + Path.Combine(_dir, "kezor.db"));

        private static ServerOptions Opt() => new ServerOptions { RequireAccounts = true, EventFirstDelay = 99999, PasswordIterations = 1000, AutosaveSeconds = 0 };

        [Test]
        public void AccountCharacterGuildAndRankingRoundTrip()
        {
            var db = Open();
            var a = new Account { Name = "Conta1", PasswordHash = PasswordHasher.Hash("segredo1", 1000) };
            a.Characters.Add("Heroi");
            a.Vault.Items.TryAdd(new Item(ItemDatabase.HealthPotion) { Quantity = 7 }, out _);
            a.BannedUntil = DateTime.UtcNow.AddDays(2); a.BanReason = "teste";
            db.Save(a);

            var c = new Character(1, "Heroi", CharacterClass.Arcanist) { Gold = 12345, AccountName = "Conta1" };
            c.Inventory.TryAdd(new Item(ItemDatabase.ApprenticeStaff, 7), out var slot); c.Equip(slot, out _);
            db.Save(c);
            c.Gold = 999; db.Save(c);   // atualizar a mesma linha

            var again = Open();   // outra instância, mesmo banco
            var a2 = ((IAccountRepository)again).Load("conta1")!;
            Assert.AreEqual("Conta1", a2.Name);
            Assert.IsTrue(PasswordHasher.Verify("segredo1", a2.PasswordHash));
            CollectionAssert.AreEqual(new[] { "Heroi" }, a2.Characters);
            Assert.AreEqual(7, Enumerable.Range(0, a2.Vault.Items.Capacity).Select(a2.Vault.Items.Get).Where(x => x != null).Sum(x => x!.Quantity));
            Assert.IsTrue(a2.IsBanned(DateTime.UtcNow)); Assert.AreEqual("teste", a2.BanReason);

            var c2 = again.Load("HEROI")!;
            Assert.AreEqual(999, c2.Gold);
            Assert.AreEqual(CharacterClass.Arcanist, c2.Class);
            Assert.AreEqual(7, c2.Equipment[EquipSlot.RightHand].Level);
            Assert.AreEqual(1, again.CountCharacters());

            var g = new Guild { Name = "Brasas", Wins = 3 }; g.Members["Heroi"] = GuildRank.Leader;
            again.SaveGuild(g);
            Assert.AreEqual(3, again.LoadAllGuilds().Single().Wins);
            again.SaveRanking(new[] { new RankingEntry { Name = "Heroi", Level = 50, Rebirths = 1 } });
            Assert.AreEqual(50, again.LoadRanking().Single().Level);

            again.Delete("Heroi"); again.DeleteGuild("Brasas");
            Assert.IsNull(again.Load("Heroi"));
            Assert.IsEmpty(again.LoadAllGuilds());
            Assert.IsNull(((IAccountRepository)again).Load("nao existe"), "nome inválido não consulta o banco");
        }

        [Test]
        public void ServerKeepsProgressAcrossRestartWithDatabase()
        {
            var gs = new GameServer(new SystemRandom(1), repo: Open(), options: Opt());
            StringAssert.StartsWith("OK", gs.Handle(1, "REGISTER jogador segredo1"));
            StringAssert.StartsWith("OK", gs.Handle(1, "CREATE Duradouro LAM"));
            StringAssert.StartsWith("OK", gs.Handle(1, "PLAY Duradouro"));
            gs.GetCharacter(1)!.Gold = 777_777;
            gs.Disconnect(1);   // salva no banco

            var restarted = new GameServer(new SystemRandom(2), repo: Open(), options: Opt());
            StringAssert.StartsWith("OK chars Duradouro:", restarted.Handle(5, "AUTH jogador segredo1"));
            StringAssert.StartsWith("OK", restarted.Handle(5, "PLAY Duradouro"));
            Assert.AreEqual(777_777, restarted.GetCharacter(5)!.Gold);
            StringAssert.StartsWith("ERR", restarted.Handle(6, "AUTH jogador errada1"));
        }

        [Test]
        public void SqliteBackupIsAUsableCopy()
        {
            var db = Open();
            db.Save(new Account { Name = "Guardada", PasswordHash = PasswordHasher.Hash("segredo1", 1000) });
            var file = db.Backup(Path.Combine(_dir, "backups"));
            if (_kind == "mysql") { Assert.IsNull(file, "MySQL usa mysqldump"); return; }
            Assert.IsTrue(File.Exists(file));
            var copy = SqlRepository.Open("sqlite:" + file);
            Assert.IsNotNull(((IAccountRepository)copy).Load("Guardada"));
        }

        [Test]
        public void ImportsJsonSaves()
        {
            var jsonDir = Path.Combine(_dir, "json");
            var gs = new GameServer(new SystemRandom(1), repo: new JsonFileRepository(jsonDir), options: Opt());
            gs.Handle(1, "REGISTER antigo segredo1");
            gs.Handle(1, "CREATE Veterano GUA");
            gs.Handle(1, "CREATE Segundo PAT");
            gs.Handle(1, "PLAY Veterano");
            gs.GetCharacter(1)!.Gold = 4242;
            gs.Disconnect(1);

            var db = Open();
            var (na, nc, _) = db.ImportJson(jsonDir);
            Assert.AreEqual(1, na); Assert.AreEqual(2, nc);
            var fromDb = new GameServer(new SystemRandom(3), repo: db, options: Opt());
            var chars = fromDb.Handle(2, "AUTH antigo segredo1");
            StringAssert.Contains("Veterano:", chars); StringAssert.Contains("Segundo:", chars);
            fromDb.Handle(2, "PLAY Veterano");
            Assert.AreEqual(4242, fromDb.GetCharacter(2)!.Gold);
            Assert.IsTrue(File.Exists(Path.Combine(jsonDir, "accounts", "antigo.json")), "a importação não apaga os arquivos");
        }
    }
}
