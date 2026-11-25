using System;
using System.IO;
using System.Net.Sockets;
using UnityEngine;
using Kezor.Core.Server;

namespace Kezor.Client
{
    /// <summary>
    /// "Servidor deste PC": o jogo abre um servidor de verdade (o mesmo do dedicado: contas, saves em disco, TCP) e entra
    /// nele pela rede como qualquer outro jogador. Amigos entram pelo seu IP; uma segunda janela do jogo neste PC também.
    /// Se já houver um servidor na porta (outra janela do jogo ou o servidor dedicado), o jogo só conecta nele.
    /// O servidor continua ligado ao voltar para o login e só para ao fechar o jogo (os heróis são salvos).
    /// </summary>
    public sealed partial class KezorGame
    {
        private GameServer? _hostServer;
        private TcpServerHost? _hostTcp;
        private int _hostPort;
        /// <summary>Este jogo está hospedando um servidor agora.</summary>
        public bool Hosting => _hostTcp != null;
        public int HostPort => _hostPort;
        public int HostPlayers => _hostServer?.OnlineCount ?? 0;

        /// <summary>Pasta dos saves do servidor deste PC (contas, heróis, guildas, ranking).</summary>
        public static string HostDataFolder => Path.Combine(Application.persistentDataPath, "kezor", "servidor");

        /// <summary>
        /// Garante um servidor neste PC na porta. Devolve false (com o motivo) só se não deu para abrir nem há outro servidor
        /// ouvindo nela. "alreadyRunning" = outra janela/processo já serve essa porta (o jogo só conecta).
        /// </summary>
        internal bool EnsureHost(int port, out bool alreadyRunning, out string error)
        {
            alreadyRunning = false; error = "";
            if (_hostTcp != null && _hostPort == port) return true;
            StopHost();
            GameServer? game = null;
            try
            {
                MigrateOldSaves();
                game = new GameServer(repo: new JsonFileRepository(HostDataFolder), options: new ServerOptions
                {
                    RequireAccounts = true, ServerName = "Servidor de " + Environment.UserName, Channel = 1, MaxPlayers = 50,
                });
                var tcp = new TcpServerHost(game, port);
                tcp.Start();
                _hostServer = game; _hostTcp = tcp; _hostPort = port;
                Debug.Log($"[Kezor] servidor deste PC ouvindo na porta {port}. Saves em {HostDataFolder}");
                return true;
            }
            catch (SocketException e) when (e.SocketErrorCode == SocketError.AddressAlreadyInUse)
            {
                alreadyRunning = true;   // outra janela do jogo (ou o servidor dedicado) já está nesta porta
                return true;
            }
            catch (Exception e)
            {
                error = "Não foi possível abrir o servidor na porta " + port + ": " + e.Message;
                return false;
            }
        }

        /// <summary>A conta que o dono do PC usa no próprio servidor ganha comandos GM (/gm level 50 etc.).</summary>
        internal void GrantHostGm(string account)
        {
            if (_hostServer != null && _host.Trim() == "127.0.0.1" && _port == _hostPort.ToString()) _hostServer.GrantGm(account);
        }

        private void PumpHost()
        {
            if (_hostServer == null || _hostTcp == null) return;
            _hostServer.Tick(Time.unscaledDeltaTime);
            _hostTcp.Pump();
        }

        internal void StopHost()
        {
            if (_hostServer == null) return;
            try { _hostServer.SaveAll(); } catch (Exception e) { Debug.LogWarning("[Kezor] salvar ao fechar o servidor: " + e.Message); }
            _hostTcp?.Dispose();
            _hostTcp = null; _hostServer = null; _hostPort = 0;
        }

        /// <summary>
        /// Primeira vez: copia os heróis do antigo modo local (ou do antigo "Hospedar LAN") para o servidor deste PC.
        /// Eles ficam na conta "jogadorlocal" (senha "kezor-local"). Nada é apagado das pastas antigas.
        /// </summary>
        private static void MigrateOldSaves()
        {
            var dest = HostDataFolder;
            if (Directory.Exists(dest)) return;
            var root = Path.Combine(Application.persistentDataPath, "kezor");
            string? src = null;
            foreach (var old in new[] { "local", "lan" })
            {
                var d = Path.Combine(root, old);
                if (Directory.Exists(d) && Directory.GetFiles(d, "*", SearchOption.AllDirectories).Length > 0) { src = d; break; }
            }
            Directory.CreateDirectory(dest);
            if (src == null) return;
            foreach (var f in Directory.GetFiles(src, "*", SearchOption.AllDirectories))
            {
                var rel = f.Substring(src.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                var to = Path.Combine(dest, rel);
                Directory.CreateDirectory(Path.GetDirectoryName(to)!);
                File.Copy(f, to, false);
            }
            Debug.Log($"[Kezor] heróis antigos copiados de {src} para o servidor deste PC (conta jogadorlocal).");
        }
    }
}
