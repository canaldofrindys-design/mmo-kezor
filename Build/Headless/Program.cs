using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using Kezor.Core.Server;

// Servidor dedicado de Kezor sem Unity (mesmo Core).
// Uso: dotnet run -- [porta] [--data <pasta>] [--gm-account <conta>] [--gm] [--name <nome>] [--channel <n>] [--max <jogadores>]
//   --data        pasta dos saves JSON (contas e personagens). Padrão: ./data
//   --gm-account  conta com comandos GM (pode repetir). --gm libera GM para todo mundo (só para teste).
//   Todo jogador entra com conta (REGISTER/AUTH), escolhe o herói (CHARS/PLAY) e joga no mesmo mundo.
//   --name/--channel/--max: nome e canal mostrados na lista de servidores do login (comando STATUS) e limite de jogadores.
int port = args.Length > 0 && int.TryParse(args[0], out var p) ? p : Protocol.DefaultPort;
bool gm = Array.IndexOf(args, "--gm") >= 0;
int di = Array.IndexOf(args, "--data");
string data = di >= 0 && di + 1 < args.Length ? args[di + 1] : Path.Combine(Environment.CurrentDirectory, "data");
string Arg(string flag, string def) { int i = Array.IndexOf(args, flag); return i >= 0 && i + 1 < args.Length ? args[i + 1] : def; }
string name = Arg("--name", "Kezor");
int channel = int.TryParse(Arg("--channel", "1"), out var ch) ? ch : 1;
int max = int.TryParse(Arg("--max", "500"), out var mx) ? mx : 500;

var repo = new JsonFileRepository(data);
var options = new ServerOptions { AllowGm = gm, RequireAccounts = true, ServerName = name, Channel = channel, MaxPlayers = max };
for (int i = 0; i + 1 < args.Length; i++) if (args[i] == "--gm-account") options.GmAccounts.Add(args[i + 1]);
var game = new GameServer(repo: repo, options: options);
using var host = new TcpServerHost(game, port);
host.Start();
Console.WriteLine($"Kezor: servidor \"{name}\" canal {channel} ouvindo na porta {port}{(gm ? " (comandos GM para todos)" : options.GmAccounts.Count > 0 ? " (GM: " + string.Join(", ", options.GmAccounts) + ")" : "")}. Saves em {data}. Ctrl+C para sair.");

bool running = true;
Console.CancelKeyPress += (_, e) => { e.Cancel = true; running = false; };
var sw = Stopwatch.StartNew(); double last = 0;
while (running)
{
    double now = sw.Elapsed.TotalSeconds;
    game.Tick((float)(now - last)); last = now;
    host.Pump();
    Thread.Sleep(33); // ~30 ticks/s
}
game.SaveAll();
Console.WriteLine("Kezor: todos os personagens salvos. Até mais!");
