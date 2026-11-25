using MuProto.Core;
using MuProto.Core.Server;
using UnityEngine;

namespace MuProto.UnityHost
{
    /// <summary>
    /// Coloque em um GameObject numa cena vazia. Rode em batchmode/headless:
    ///   Unity -batchmode -nographics -projectPath . -executeMethod ... (ou build "Dedicated Server").
    /// O loop de jogo usa Update(); a rede roda em threads e é drenada por Pump().
    /// </summary>
    public sealed class ServerBootstrap : MonoBehaviour
    {
        [SerializeField] private int port = 55901;
        [SerializeField] private int tickRate = 20;
        [SerializeField] private int seed = 0; // 0 = aleatório

        private GameServer _game;
        private TcpServerHost _host;
        private float _acc;

        private void Awake()
        {
            Application.targetFrameRate = tickRate;
            Application.runInBackground = true;
            _game = new GameServer(seed == 0 ? new SystemRandom() : new SystemRandom(seed));
            _host = new TcpServerHost(_game, port);
            _host.Start();
            Debug.Log($"[MuProto] servidor ouvindo na porta {port}");
        }

        private void Update()
        {
            _host.Pump();
            _acc += Time.deltaTime;
            float step = 1f / tickRate;
            while (_acc >= step) { _game.Tick(step); _acc -= step; }
        }

        private void OnDestroy() => _host?.Dispose();
    }
}
