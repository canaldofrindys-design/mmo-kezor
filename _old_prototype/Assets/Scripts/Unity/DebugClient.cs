using System.IO;
using System.Net.Sockets;
using System.Text;
using UnityEngine;

namespace MuProto.UnityHost
{
    /// <summary>Cliente de depuração com IMGUI: digita comandos e mostra respostas.</summary>
    public sealed class DebugClient : MonoBehaviour
    {
        [SerializeField] private string host = "127.0.0.1";
        [SerializeField] private int port = 55901;
        private TcpClient _tcp; private StreamReader _r; private StreamWriter _w;
        private string _input = "HELP"; private readonly StringBuilder _log = new StringBuilder();
        private Vector2 _scroll;

        private void Start()
        {
            _tcp = new TcpClient(host, port);
            var s = _tcp.GetStream();
            _r = new StreamReader(s, Encoding.UTF8); _w = new StreamWriter(s) { AutoFlush = true };
        }

        private void Update()
        {
            while (_tcp != null && _tcp.Available > 0) _log.AppendLine(_r.ReadLine()?.Replace(" | ", "\n   "));
        }

        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(10, 10, Screen.width - 20, Screen.height - 20));
            _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.Height(Screen.height - 70));
            GUILayout.Label(_log.ToString());
            GUILayout.EndScrollView();
            GUILayout.BeginHorizontal();
            _input = GUILayout.TextField(_input);
            if (GUILayout.Button("Enviar", GUILayout.Width(100))) { _log.AppendLine("> " + _input); _w.WriteLine(_input); }
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        private void OnDestroy() => _tcp?.Close();
    }
}
