using System;
using System.Linq;
using Kezor.Core.Characters;
using static Kezor.Core.Server.Protocol;

namespace Kezor.Core.Server
{
    /// <summary>
    /// Partes do multiplayer que não são regra de combate: sair do mundo (desconectar ou voltar à seleção de personagem),
    /// sussurro entre jogadores, lista de quem está online, PING de "ainda estou aqui" e permissão de GM por conta.
    /// </summary>
    public sealed partial class GameServer
    {
        /// <summary>GM liberado para esta conexão: servidor de teste (AllowGm) ou conta na lista de GMs.</summary>
        private bool IsGm(Session s) => _opt.AllowGm || (s.Account != null && _opt.GmAccounts.Contains(s.Account.Name));

        /// <summary>Dá comandos GM a uma conta (o servidor deste PC usa para a conta do dono). Vale para quem já está conectado.</summary>
        public void GrantGm(string account) { if (!string.IsNullOrWhiteSpace(account)) _opt.GmAccounts.Add(account.Trim()); }

        /// <summary>
        /// Tira o herói do mundo: sai do grupo, cancela troca e loja, salva, avisa a guilda e quem está por perto.
        /// A conexão (e a conta) continuam; usado ao desconectar e no SELECT.
        /// </summary>
        private void LeaveWorld(Session s)
        {
            var gone = s.Character;
            if (gone == null) return;
            LeaveParty(gone);
            if (_tradeOf.TryGetValue(s.Id, out var t)) EndTrade(t, $"Troca cancelada: {gone.Name} saiu.");
            _tradeInvite.Remove(s.Id);
            foreach (var k in _tradeInvite.Where(kv => kv.Value == s.Id).Select(kv => kv.Key).ToList()) _tradeInvite.Remove(k);
            _shops.Remove(s.Id);
            SaveCharacter(s);
            s.Character = null;
            s.ClearIntent();
            s.PendingUtility = -1;
            s.VaultOpen = false;
            s.PendingInviteFrom = 0;
            s.SentInvVersion = -1; s.LastMe = "";
            BroadcastExcept(s, $"@EV SYS;{Clean(gone.Name)} saiu do mundo.");
            var g = GuildOf(gone.Name);
            if (g != null) SendGuild(g);   // atualiza quem está online
        }

        /// <summary>SELECT: volta para a tela de personagens sem desconectar (como "trocar personagem").</summary>
        private string BackToSelect(Session s)
        {
            if (s.Account == null) return "ERR entre na sua conta primeiro";
            if (s.Character != null && s.Character.IsDead) return "ERR espere renascer";
            LeaveWorld(s);
            return "OK " + CharList(s.Account);
        }

        /// <summary>W nome texto: mensagem privada para um jogador online (em qualquer mapa).</summary>
        private string Whisper(Session s, Character c, string[] p)
        {
            if (p.Length < 3) return "ERR use /w <nome> <mensagem>";
            var target = CharByName(p[1]);
            if (target == null) return $"ERR {Clean(p[1])} não está online";
            if (target == c) return "ERR você não pode sussurrar para si mesmo";
            if (_time - s.LastChatTime < 0.7) return "ERR aguarde para falar de novo";
            s.LastChatTime = _time;
            var text = Clean(string.Join(" ", p.Skip(2)));
            if (text.Length > 120) text = text.Substring(0, 120);
            Push(target, $"@EV CHAT;whisper;de {Clean(c.Name)};{text}");
            Push(s, $"@EV CHAT;whisper;para {Clean(target.Name)};{text}");
            return "OK";
        }

        /// <summary>ONLINE: quantos jogadores estão no mundo e os nomes (até 40).</summary>
        private string OnlineList()
        {
            var names = Online.Select(o => $"{o.Character!.Name} (nv {o.Character.Level})").OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList();
            var shown = string.Join(", ", names.Take(40));
            return $"OK online {names.Count}: {shown}{(names.Count > 40 ? ", ..." : "")}";
        }
    }
}
