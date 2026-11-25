using UnityEngine;
using Kezor.Core.Server;

namespace Kezor.Client
{
    /// <summary>
    /// Servidor dedicado dentro do Unity (build "Dedicated Server" ou batchmode). Coloque numa cena vazia.
    /// Alternativa sem Unity: Build/Headless (dotnet run). As threads de rede só enfileiram; Pump() roda aqui.
    /// </summary>
    public sealed class ServerBootstrap : MonoBehaviour
    {
        [SerializeField] private int port = Protocol.DefaultPort;
        [SerializeField] private int tickRate = 30;
        [SerializeField] private bool allowGm;
        [SerializeField] private bool requireAccounts = true;
        [Tooltip("Nome e canal mostrados na lista de servidores do login (comando STATUS)")]
        [SerializeField] private string serverName = "Kezor";
        [SerializeField] private int channel = 1;
        [SerializeField] private int maxPlayers = 500;
        [Tooltip("Pasta dos saves JSON; vazio = Application.persistentDataPath/kezor-data")]
        [SerializeField] private string dataFolder = "";

        private GameServer? _game;
        private TcpServerHost? _host;

        private void Awake()
        {
            Application.targetFrameRate = tickRate;
            Application.runInBackground = true;
            var folder = string.IsNullOrEmpty(dataFolder) ? System.IO.Path.Combine(Application.persistentDataPath, "kezor-data") : dataFolder;
            _game = new GameServer(repo: new JsonFileRepository(folder), options: new ServerOptions { AllowGm = allowGm, RequireAccounts = requireAccounts, ServerName = serverName, Channel = channel, MaxPlayers = maxPlayers });
            _host = new TcpServerHost(_game, port);
            _host.Start();
            Debug.Log($"[Kezor] servidor dedicado \"{serverName}\" canal {channel} ouvindo na porta {port}");
        }

        private void Update()
        {
            _game!.Tick(Time.deltaTime);
            _host!.Pump();
        }

        private void OnApplicationQuit() => _game?.SaveAll();

        private void OnDestroy() { _game?.SaveAll(); _host?.Dispose(); }
    }
}
