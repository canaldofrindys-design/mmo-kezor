using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using static Kezor.Core.Server.Protocol;

namespace Kezor.Core.Server
{
    /// <summary>
    /// Entrada robusta: aperto de mão com a versão do jogo (HELLO), volta automática depois de uma queda (RESUME com um
    /// código de uso único, sem pedir a senha de novo) e freio para quem erra a senha muitas vezes do mesmo IP.
    /// </summary>
    public sealed partial class GameServer
    {
        /// <summary>Quanto tempo depois da queda o código de volta (RESUME) ainda vale.</summary>
        public const double ResumeWindowSeconds = 600;
        public const int MaxLoginFailures = 5;
        public const double LoginFailureWindow = 60, LoginLockSeconds = 30;

        private sealed class ResumeToken { public string Value = ""; public double ExpiresAt = double.MaxValue; }
        private readonly Dictionary<string, ResumeToken> _tokens = new Dictionary<string, ResumeToken>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, List<double>> _loginFailures = new Dictionary<string, List<double>>();

        /// <summary>HELLO versão: confere se o jogo é da mesma versão do servidor.</summary>
        private string Hello(Session s, string[] p)
        {
            var v = p.Length > 1 ? p[1] : "";
            if (v != GameVersion)
                return $"ERR versão do jogo diferente (servidor {GameVersion}, seu jogo {Clean(v)}). Atualize o jogo.";
            return $"OK hello {GameVersion} {Clean(_opt.ServerName)};{_opt.Channel}";
        }

        /// <summary>Gera um código novo de volta para a conta da sessão e manda para o cliente ("@TOKEN código").</summary>
        private void IssueToken(Session s)
        {
            if (s.Account == null) return;
            var bytes = new byte[16];
            using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(bytes);
            var t = new ResumeToken { Value = BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant() };
            _tokens[s.Account.Name] = t;
            Push(s, "@TOKEN " + t.Value);
        }

        /// <summary>Caiu: o código continua valendo por alguns minutos para o jogo voltar sozinho.</summary>
        private void TokenAfterDisconnect(Session s)
        {
            if (s.Account != null && _tokens.TryGetValue(s.Account.Name, out var t) && t.ExpiresAt == double.MaxValue)
                t.ExpiresAt = _time + ResumeWindowSeconds;
        }

        private static bool SameSecret(string a, string b)
        {
            if (a.Length != b.Length) return false;
            int diff = 0;
            for (int i = 0; i < a.Length; i++) diff |= a[i] ^ b[i];
            return diff == 0;
        }

        /// <summary>
        /// RESUME conta código: volta para a conta depois de uma queda, sem a senha. Se a conexão antiga ainda não tiver
        /// caído do lado do servidor (cabo puxado, Wi-Fi trocado), ela é fechada e o herói é salvo antes.
        /// </summary>
        private string Resume(Session s, string[] p)
        {
            if (s.Character != null || s.Account != null) return "ERR já conectado";
            if (p.Length < 3) return "ERR use RESUME <conta> <código>";
            if (LoginBlocked(s) is string wait) return wait;
            if (!_tokens.TryGetValue(p[1], out var t) || _time >= t.ExpiresAt || !SameSecret(t.Value, p[2]))
            {
                LoginFailed(s, p[1]);
                return "ERR sessão expirada, entre de novo com a senha";
            }
            var old = _sessions.Values.FirstOrDefault(o => o != s && o.Account != null && string.Equals(o.Account.Name, p[1], StringComparison.OrdinalIgnoreCase));
            Account? a;
            if (old != null)
            {
                a = old.Account;
                Kick(old, "Sua conta entrou de novo em outra conexão.", "conta conectada em outra conexão");
                Disconnect(old.Id);   // salva o herói e libera a conta agora; a queda do socket antigo depois não faz nada
            }
            else a = _accounts.Load(p[1]);
            if (a == null) return "ERR conta não encontrada";
            if (a.IsBanned(DateTime.UtcNow)) return $"ERR esta conta está banida {Until(a.BannedUntil)}" + (a.BanReason.Length > 0 ? ": " + a.BanReason : "");
            s.Account = a;
            IssueToken(s);   // o código é de uso único: cada volta ganha outro
            LogLine($"voltou: {a.Name} (sessão {s.Id}{(s.Address != "" ? ", " + s.Address : "")})");
            return "OK " + CharList(a);
        }

        /// <summary>Muitas senhas erradas do mesmo IP (ou da mesma sessão, sem rede): espera um pouco.</summary>
        private string? LoginBlocked(Session s)
        {
            var key = s.Address != "" ? s.Address : "sessão " + s.Id;
            if (!_loginFailures.TryGetValue(key, out var list)) return null;
            list.RemoveAll(t => _time - t > LoginFailureWindow);
            if (list.Count < MaxLoginFailures) return null;
            double left = list[list.Count - 1] + LoginLockSeconds - _time;
            if (left <= 0) { list.Clear(); return null; }
            return $"ERR muitas tentativas erradas, espere {Math.Ceiling(left)} s";
        }

        private void LoginFailed(Session s, string account)
        {
            var key = s.Address != "" ? s.Address : "sessão " + s.Id;
            if (!_loginFailures.TryGetValue(key, out var list)) _loginFailures[key] = list = new List<double>();
            list.Add(_time);
            if (list.Count == MaxLoginFailures) LogLine($"login bloqueado por {LoginLockSeconds:0} s: {key} errou {MaxLoginFailures} vezes (última conta: {Clean(account)})");
        }
    }
}
