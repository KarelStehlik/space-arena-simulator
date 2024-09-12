using CommandLine;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace SaSimulator
{
    public class Options
    {
        [Option('F', "file", Required = true, Default = false, HelpText = "File to load ships from")]
        public string File { get; set; } = "";
        [Option('G', "graphics", Required = false, Default = false, HelpText = "Use graphics window to view the battle.")]
        public bool Graphics { get; set; }
        [Option('S', "speed", Required = false, Default = 1, HelpText = "Game speed used when graphics are on. This does not affect the game result.")]
        public float Gamespeed { get; set; }
        [Option('D', "deltatime", Required = false, Default = 1 / 30f, HelpText = "Deltatime used to simulate lag. default non-laggy value is 1/30.")]
        public float Deltatime { get; set; }
        [Option('T', "timeout", Required = false, Default = 180, HelpText = "Time after which the battle is declared a draw to avoid long simulations. 0 to disable.")]
        public float Timeout { get; set; }
        [Option('N', "number", Required = false, Default = 180, HelpText = "Number of simulations to perform. Locked to 1 if graphics are on.")]
        public int NumberSims { get; set; }
    }

    internal class Program
    {
        static void RunMain(Options o)
        {
            var file = FileLoading.Read(o.File);
            List<ShipInfo> a = file.player0;
            List<ShipInfo> b = file.player1;
            Random rng = new();

            if (o.Graphics)
            {
                Game game = new(a, b, o.Graphics, (o.Timeout == 0 ? float.PositiveInfinity : o.Timeout).Seconds(), rng.Next(), o.Deltatime.Seconds());
                Console.WriteLine("begin");
                MonoGameWindow.Init(game, (float)o.Gamespeed);
                MonoGameWindow window = MonoGameWindow.Instance;
                window.Run();
                Console.WriteLine("end");
                switch (game.result)
                {
                    case Game.GameResult.unfinished:
                        break;
                    case Game.GameResult.win_0:
                        Console.WriteLine("blue player wins");
                        break;
                    case Game.GameResult.win_1:
                        Console.WriteLine("red player wins");
                        break;
                    case Game.GameResult.draw:
                        Console.WriteLine("draw");
                        break;
                }
                return;
            }

            for (int t = 0; t < 100; t++)
            {
                Console.WriteLine("simulating...");
                Stopwatch stopwatch = Stopwatch.StartNew();
                SimulationResults results = new(o.NumberSims);

                for (int i = 0; i < o.NumberSims; i++)
                {
                    ThreadPool.QueueUserWorkItem(Simulate, new SimulationInfo(a, b, o, rng.Next(), results));
                }
                results.done.WaitOne();

                Console.WriteLine($"Blue player (p0) has {results.win0} wins, {results.draw} draws, {results.win1} losses.");
                Console.WriteLine($"Win rate {results.win0 / (float)(results.win0 + results.win1) * 100:0.00}%");
                stopwatch.Stop();
                Console.WriteLine($"elapsed: {stopwatch.ElapsedMilliseconds} ms");
            }
        }

        class SimulationResults(int count)
        {
            public int win0 = 0, win1 = 0, draw = 0;
            public int remaining = count;
            public readonly AutoResetEvent done = new(false);
        }

        class SimulationInfo(List<ShipInfo> a, List<ShipInfo> b, Options o, int randomSeed, SimulationResults results)
        {
            public readonly List<ShipInfo> shipsA = a, shipsB = b;
            public readonly Options options = o;
            public readonly int randomSeed = randomSeed;
            public readonly SimulationResults results = results;
        }

        static void Simulate(object? simulationInfo)
        {
            if (simulationInfo == null || simulationInfo is not SimulationInfo info)
            {
                return;
            }
            Game game = new(info.shipsA, info.shipsB, info.options.Graphics,
                (info.options.Timeout == 0 ? float.PositiveInfinity : info.options.Timeout).Seconds(),
                info.randomSeed, info.options.Deltatime.Seconds());
            game.Load();
            while (game.result == Game.GameResult.unfinished)
            {
                game.Tick();
            }
            switch (game.result)
            {
                case Game.GameResult.win_0:
                    System.Threading.Interlocked.Increment(ref info.results.win0);
                    break;
                case Game.GameResult.win_1:
                    System.Threading.Interlocked.Increment(ref info.results.win1);
                    break;
                case Game.GameResult.draw:
                    System.Threading.Interlocked.Increment(ref info.results.draw);
                    break;
            }
            if (System.Threading.Interlocked.Decrement(ref info.results.remaining) == 0)
            {
                info.results.done.Set();
            }
        }

        static void Main(string[] args)
        {
            Parser.Default.ParseArguments<Options>(args).WithParsed<Options>(RunMain);
        }
    }
}