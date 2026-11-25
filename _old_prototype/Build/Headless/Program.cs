using System;
using System.Diagnostics;
using System.Threading;
using MuProto.Core;
using MuProto.Core.Server;

// Servidor headless sem Unity (mesmo Core). Uso: dotnet run -- [porta]
int port = args.Length > 0 ? int.Parse(args[0]) : 55901;
var game = new GameServer(new SystemRandom());
using var host = new TcpServerHost(game, port);
host.Start();
Console.WriteLine($"MuProto headless ouvindo em {port} (Ctrl+C para sair)");
var sw = Stopwatch.StartNew(); double last = 0;
while (true)
{
    host.Pump();
    double now = sw.Elapsed.TotalSeconds;
    game.Tick((float)(now - last)); last = now;
    Thread.Sleep(50); // 20 ticks/s
}
