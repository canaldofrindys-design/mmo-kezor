using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using Kezor.Core.Server;
using Kezor.Server.Data;

// Servidor dedicado de Kezor sem Unity (mesmo Core). Feito para ficar ligado 24h num VPS (Linux ou Windows).
// Uso: Kezor.Server [porta] [--data <pasta>] [--db <banco>] [--gm-account <conta>] [--gm] [--name <nome>] [--channel <n>] [--max <jogadores>]
//   --data        pasta do servidor. Padrão: ./data
//   --db          banco de dados: sqlite:<arquivo> (padrão: <data>/kezor.db), mysql:<conexão> (ex.: mysql:Server=localhost;Database=kezor;User ID=kezor;Password=...)
//                 ou json (arquivos .json na pasta data, como o servidor do PC). Na primeira vez num banco vazio, os saves
//                 .json da pasta data são copiados para o banco automaticamente (nada é apagado).
//   --gm-account  conta com comandos GM (pode repetir). --gm libera GM para todo mundo (só para teste).
//   Também lê variáveis de ambiente: KEZOR_PORT, KEZOR_DATA, KEZOR_DB, KEZOR_NAME, KEZOR_GM_ACCOUNTS (separadas por vírgula).
//   Todo jogador entra com conta (REGISTER/AUTH), escolhe o herói (CHARS/PLAY) e joga no mesmo mundo.
// No console: ajuda | online | salvar | backup | aviso <texto> | sair  (num serviço do Linux o console não é usado).

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
string dbSpec = Arg("--db", Env("KEZOR_DB", "sqlite:" + Path.Combine(data, "kezor.db")));
ICharacterRepository repo;
if (dbSpec.Trim().Equals("json", StringComparison.OrdinalIgnoreCase))
{
    repo = new JsonFileRepository(data);
    Log($"Banco: arquivos JSON em {data}");
}
else
{
    SqlRepository sql;
    try { sql = SqlRepository.Open(dbSpec); }
    catch (Exception e) { Log($"ERRO: não foi possível abrir o banco de dados ({e.Message})."); return 1; }
    int accounts = sql.CountAccounts();
    if (accounts == 0 && Directory.Exists(Path.Combine(data, "accounts")) && Directory.GetFiles(Path.Combine(data, "accounts"), "*.json").Length > 0)
    {   // primeira vez com banco: traz os saves em arquivo
        var (na, nc, ng) = sql.ImportJson(data, Log);
        Log($"Saves em arquivo copiados para o banco: {na} conta(s), {nc} personagem(ns), {ng} guilda(s). Os arquivos continuam na pasta.");
        accounts = na;
    }
    repo = sql;
    Log($"Banco: {sql.Describe()} · {accounts} conta(s), {sql.CountCharacters()} personagem(ns)");
}
var game = new GameServer(repo: repo, options: options);
void Backup()
{
    if (!(repo is SqlRepository sq)) return;
    try { var f = sq.Backup(Path.Combine(data, "backups")); if (f != null) Log("Backup do banco: " + f); }
    catch (Exception e) { Log("Falha no backup do banco: " + e.Message); }
}
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

var sw = Stopwatch.StartNew(); double last = 0, nextStatus = 300, nextBackup = 60;
while (running)
{
    double now = sw.Elapsed.TotalSeconds;
    game.Tick((float)Math.Min(now - last, 0.5)); last = now;
    host.Pump();
    while (console.TryDequeue(out var cmd)) if (cmd.Length > 0) Log(ConsoleCommand(cmd));
    if (now >= nextBackup) { nextBackup = now + 6 * 3600; game.SaveAll(); Backup(); }   // cópia do banco a cada 6 h
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
        case "ajuda": case "help": return "Comandos: online | salvar | backup | aviso <texto> | sair";
        case "online": return game.AdminOnline();
        case "salvar": case "save": game.SaveAll(); return "Tudo salvo.";
        case "backup": game.SaveAll(); Backup(); return "Backup feito.";
        case "aviso": case "announce": return rest.Length == 0 ? "Use: aviso <texto>" : game.Announce(rest);
        case "sair": case "exit": case "quit": RequestStop("console"); return "Saindo...";
        default: return "Comando desconhecido. Digite ajuda.";
    }
}
