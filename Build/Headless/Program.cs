using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using Kezor.Core.Server;

// Servidor dedicado de Kezor sem Unity (mesmo Core). Feito para ficar ligado 24h num VPS (Linux ou Windows).
// Uso: Kezor.Server [porta] [--data <pasta>] [--gm-account <conta>] [--gm] [--name <nome>] [--channel <n>] [--max <jogadores>]
//   --data        pasta dos saves (contas e personagens). Padrão: ./data
//   --gm-account  conta com comandos GM (pode repetir). --gm libera GM para todo mundo (só para teste).
//   Também lê variáveis de ambiente: KEZOR_PORT, KEZOR_DATA, KEZOR_NAME, KEZOR_GM_ACCOUNTS (separadas por vírgula).
//   Todo jogador entra com conta (REGISTER/AUTH), escolhe o herói (CHARS/PLAY) e joga no mesmo mundo.
// No console: ajuda | online | salvar | aviso <texto> | sair  (num serviço do Linux o console não é usado).

string Arg(string flag, string def) { int i = Array.IndexOf(args, flag); return i >= 0 && i + 1 < args.Length ? args[i + 1] : def; }
string Env(string key, string def) { var v = Environment.GetEnvironmentVariable(key); return string.IsNullOrWhiteSpace(v) ? def : v.Trim(); }
void Log(string text) => Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {text}");

int port = args.Length > 0 && int.TryParse(args[0], out var p) ? p : int.TryParse(Env("KEZOR_PORT", ""), out var ep) ? ep : Protocol.DefaultPort;
bool gm = Array.IndexOf(args, "--gm") >= 0;
string data = Arg("--data", Env("KEZOR_DATA", Path.Combine(AppContext.BaseDirectory, "data")));
string name = Arg("--name", Env("KEZOR_NAME", "Kezor"));
int channel = int.TryParse(Arg("--channel", "1"), out var ch) ? ch : 1;
int max = int.TryParse(Arg("--max", "500"), out var mx) ? mx : 500;

var options = new ServerOptions { AllowGm = gm, RequireAccounts = true, ServerName = name, Channel = channel, MaxPlayers = max, Log = Log };
for (int i = 0; i + 1 < args.Length; i++) if (args[i] == "--gm-account") options.GmAccounts.Add(args[i + 1]);
foreach (var a in Env("KEZOR_GM_ACCOUNTS", "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)) options.GmAccounts.Add(a);

Directory.CreateDirectory(data);
var repo = new JsonFileRepository(data);
var game = new GameServer(repo: repo, options: options);
using var host = new TcpServerHost(game, port);
try { host.Start(); }
catch (Exception e) { Log($"ERRO: não foi possível abrir a porta {port}: {e.Message}"); return 1; }
Log($"Kezor {Protocol.GameVersion}: servidor \"{name}\" canal {channel} ouvindo na porta {port}" +
    (gm ? " (comandos GM para todos)" : options.GmAccounts.Count > 0 ? " (GM: " + string.Join(", ", options.GmAccounts) + ")" : "") + $". Saves em {data}.");

// Desligar com segurança: Ctrl+C, "sair" no console ou o sistema parando o serviço (SIGTERM) salvam todo mundo antes.
var stop = new ManualResetEventSlim(false);
bool running = true;
void RequestStop(string why) { if (!running) return; running = false; Log($"Desligando ({why})..."); }
Console.CancelKeyPress += (_, e) => { e.Cancel = true; RequestStop("Ctrl+C"); };
using var sigterm = PosixSignalRegistration.Create(PosixSignal.SIGTERM, ctx => { ctx.Cancel = true; RequestStop("serviço parado"); stop.Wait(15000); });

// Comandos digitados no console (lidos numa thread; executados na thread do jogo).
var console = new ConcurrentQueue<string>();
if (!Console.IsInputRedirected)
    new Thread(() => { string? l; while ((l = Console.ReadLine()) != null) console.Enqueue(l.Trim()); }) { IsBackground = true, Name = "Kezor-Console" }.Start();

var sw = Stopwatch.StartNew(); double last = 0, nextStatus = 300;
while (running)
{
    double now = sw.Elapsed.TotalSeconds;
    game.Tick((float)Math.Min(now - last, 0.5)); last = now;
    host.Pump();
    while (console.TryDequeue(out var cmd)) if (cmd.Length > 0) Log(ConsoleCommand(cmd));
    if (now >= nextStatus) { nextStatus = now + 300; Log($"{game.OnlineCount} jogador(es) online, {host.ConnectionCount} conexão(ões)."); }
    Thread.Sleep(33); // ~30 ticks/s
}
game.SaveAll();
host.Flush();
Log("Todos os personagens salvos. Até mais!");
stop.Set();
return 0;

string ConsoleCommand(string line)
{
    var sp = line.IndexOf(' ');
    var cmd = (sp < 0 ? line : line.Substring(0, sp)).ToLowerInvariant();
    var rest = sp < 0 ? "" : line.Substring(sp + 1).Trim();
    switch (cmd)
    {
        case "ajuda": case "help": return "Comandos: online | salvar | aviso <texto> | sair";
        case "online": return game.AdminOnline();
        case "salvar": case "save": game.SaveAll(); return "Tudo salvo.";
        case "aviso": case "announce": return rest.Length == 0 ? "Use: aviso <texto>" : game.Announce(rest);
        case "sair": case "exit": case "quit": RequestStop("console"); return "Saindo...";
        default: return "Comando desconhecido. Digite ajuda.";
    }
}
