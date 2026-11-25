using System.Collections.Generic;
using UnityEngine;
using Kezor.Core.Characters;

namespace Kezor.Client
{
    /// <summary>
    /// Login com conta no servidor dedicado: entrar/registrar (senha só vai ao servidor, que guarda o hash PBKDF2),
    /// depois a tela de personagens (até 3 por conta, criar escolhendo a classe, e jogar). Os modos local e LAN
    /// continuam entrando direto como convidado.
    /// </summary>
    public sealed partial class KezorGame
    {
        internal enum LoginStage { Form, Characters, Create }
        internal LoginStage _loginStage = LoginStage.Form;
        internal string _account = "", _password = "";
        internal bool _registering;
        internal readonly List<(string name, int cls, int level)> _charList = new List<(string, int, int)>();
        internal string _newCharName = "";
        internal int _newCharClass;
        internal bool _awaitingList;

        /// <summary>Primeira mensagem ao conectar no modo online: AUTH ou REGISTER (e depois CHARS).</summary>
        internal void SendAccountLogin()
        {
            var acc = _account.Trim();
            if (_registering)
                Send($"REGISTER {acc} {_password}", r =>
                {
                    if (r.StartsWith("ERR")) { _loginError = r.Substring(4); Disconnect(); return; }
                    RequestCharList();
                });
            else
                Send($"AUTH {acc} {_password}", r =>
                {
                    if (r.StartsWith("ERR")) { _loginError = r.Substring(4); Disconnect(); return; }
                    ParseCharList(r);
                });
            _password = "";   // não fica guardada no cliente
        }

        /// <summary>Resumo de um personagem da conta para a tela de seleção.</summary>
        internal sealed class CharSummary
        {
            public string Name = "", Guild = ""; public int Cls, Level, Rebirths, Rank = -1; public string Look = "";
        }
        internal readonly List<CharSummary> _charRows = new List<CharSummary>();

        /// <summary>Saves dos modos local e LAN ficam neste computador (persistentDataPath/kezor/&lt;modo&gt;).</summary>
        private static Kezor.Core.Server.JsonFileRepository LocalRepo(string mode)
        {
            var dir = System.IO.Path.Combine(Application.persistentDataPath, "kezor", mode);
            System.IO.Directory.CreateDirectory(dir);
            return new Kezor.Core.Server.JsonFileRepository(dir);
        }

        /// <summary>Local/LAN: entra (ou cria) a conta automática deste computador e abre a seleção.</summary>
        private void SendLocalAccountLogin()
        {
            const string acc = "jogadorlocal", pass = "kezor-local";
            Send($"AUTH {acc} {pass}", r =>
            {
                if (r.StartsWith("OK")) { ParseCharList(r); return; }
                Send($"REGISTER {acc} {pass}", r2 =>
                {
                    if (r2.StartsWith("ERR")) { _loginError = r2.Substring(4); Disconnect(); return; }
                    RequestCharList();
                });
            });
        }

        internal void RequestCharList()
        {
            _awaitingList = true;
            Send("CHARS", ParseCharList);
        }

        private void ParseCharList(string reply)
        {
            _awaitingList = false;
            _charList.Clear(); _charRows.Clear();
            int i = reply.IndexOf("chars", System.StringComparison.Ordinal);
            if (i >= 0)
            {
                var body = reply.Substring(i + 5).Trim();
                bool rich = body.Contains("|") || body.Split(':').Length > 3;
                foreach (var e in body.Split(rich ? '|' : ','))
                {
                    var f = e.Split(':');
                    if (f.Length < 3 || !int.TryParse(f[1], out var cls) || !int.TryParse(f[2], out var lvl)) continue;
                    _charList.Add((f[0], cls, lvl));
                    var row = new CharSummary { Name = f[0], Cls = cls, Level = lvl };
                    if (f.Length >= 7) { int.TryParse(f[3], out row.Rebirths); row.Guild = f[4]; int.TryParse(f[5], out row.Rank); row.Look = f[6]; }
                    _charRows.Add(row);
                }
            }
            _loginStage = LoginStage.Characters;
            _loginError = "";
        }
    }
}
