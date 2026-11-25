using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using Kezor.Core.Items;

namespace Kezor.Core.Server
{
    /// <summary>Conta de jogador: login, senha (hash PBKDF2), até 5 personagens e o baú compartilhado entre eles.</summary>
    public sealed class Account
    {
        public const int MaxCharacters = 5;
        public string Name = "";
        public string PasswordHash = "";
        public readonly List<string> Characters = new List<string>();
        public Vault Vault = new Vault();
        public DateTime Created = DateTime.UtcNow;
        /// <summary>Banimento "para sempre" (uma data bem distante, que cabe em qualquer banco e fuso).</summary>
        public static readonly DateTime PermanentBan = new DateTime(9000, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        /// <summary>Banida até esta data (UTC). <see cref="PermanentBan"/> = para sempre. Null = livre.</summary>
        public DateTime? BannedUntil;
        public string BanReason = "";
        /// <summary>Chat bloqueado até esta data (UTC).</summary>
        public DateTime? MutedUntil;
        public bool IsBanned(DateTime nowUtc) => BannedUntil.HasValue && BannedUntil.Value > nowUtc;
        public bool IsMuted(DateTime nowUtc) => MutedUntil.HasValue && MutedUntil.Value > nowUtc;
    }

    /// <summary>
    /// Senhas com PBKDF2-SHA256 (sal aleatório de 16 bytes, 32 bytes de hash). Formato guardado:
    /// "pbkdf2-sha256$iterações$salBase64$hashBase64". A comparação é em tempo constante.
    /// </summary>
    public static class PasswordHasher
    {
        public const int DefaultIterations = 120_000;
        private const int SaltBytes = 16, HashBytes = 32;

        public static string Hash(string password, int iterations = DefaultIterations)
        {
            var salt = new byte[SaltBytes];
            using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(salt);
            var hash = Derive(password, salt, iterations);
            return $"pbkdf2-sha256${iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
        }

        public static bool Verify(string password, string stored)
        {
            var parts = stored.Split('$');
            if (parts.Length != 4 || parts[0] != "pbkdf2-sha256" || !int.TryParse(parts[1], out var it) || it < 1000) return false;
            byte[] salt, expected;
            try { salt = Convert.FromBase64String(parts[2]); expected = Convert.FromBase64String(parts[3]); }
            catch (FormatException) { return false; }
            var actual = Derive(password, salt, it);
            if (actual.Length != expected.Length) return false;
            int diff = 0;
            for (int i = 0; i < actual.Length; i++) diff |= actual[i] ^ expected[i];
            return diff == 0;
        }

        private static byte[] Derive(string password, byte[] salt, int iterations)
        {
            using var kdf = new Rfc2898DeriveBytes(password, salt, iterations, HashAlgorithmName.SHA256);
            return kdf.GetBytes(HashBytes);
        }
    }

    public interface IAccountRepository
    {
        Account? Load(string name);
        void Save(Account account);
    }

    public sealed class InMemoryAccountRepository : IAccountRepository
    {
        private readonly Dictionary<string, Account> _db = new Dictionary<string, Account>(StringComparer.OrdinalIgnoreCase);
        public Account? Load(string name) => _db.TryGetValue(name, out var a) ? a : null;
        public void Save(Account a) => _db[a.Name] = a;
    }
}
