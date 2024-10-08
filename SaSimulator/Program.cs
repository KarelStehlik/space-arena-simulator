﻿using CommandLine;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace SaSimulator
{
    public class Options
    {
        [Option('F', "file", Required = true, Default = false, HelpText = "Battle file to load ships and global bonuses from")]
        public string File { get; set; } = "";

        [Option('M', "modules0", Required = false, Default = "modules.txt", HelpText = "Module file to load player 0 available modules from.")]
        public string ModulesFile0 { get; set; } = "modules.txt";

        [Option('m', "modules1", Required = false, Default = "", HelpText = "File to load player 1 available modules from. Same as player 0 if not set.")]
        public string ModulesFile1 { get; set; } = "";

        [Option('G', "graphics", Required = false, Default = false, HelpText = "Use graphics window to view the battle.")]
        public bool Graphics { get; set; }

        [Option('S', "speed", Required = false, Default = 1, HelpText = "Game speed used when graphics are on. This does not affect the game result.")]
        public float GameSpeed { get; set; }

        [Option('D', "deltatime", Required = false, Default = 1 / 30f, HelpText = "Delta time used to simulate lag. default non-laggy value is 1/30.")]
        public float DeltaTime { get; set; }

        [Option('T', "timeout", Required = false, Default = 180, HelpText = "Time after which the battle is declared a draw to avoid long simulations. 0 to disable.")]
        public float Timeout { get; set; }

        [Option('N', "number", Required = false, Default = 180, HelpText = "Number of simulations to perform. Locked to 1 if graphics are on.")]
        public int NumberSims { get; set; }
    }

    internal class Program
    {
        /// <summary>
        /// Loads battle data from input files. If -G is set, opens a graphics window and runs a battle there.
        /// If not, runs the requested number of battles as quickly as possible in a thread pool.
        /// </summary>
        /// <param name="o">Parsed command line arguments</param>
        static void RunMain(Options o)
        {
            var file = FileLoading.ReadPlayersAndShips(o.File);
            var modules0 = FileLoading.ReadModules(o.ModulesFile0);
            var modules1 = o.ModulesFile1 == "" ? modules0 : FileLoading.ReadModules(o.ModulesFile1);
            Random rng = new();

            // graphics mode
            if (o.Graphics)
            {
                Game game = new(file, modules0, modules1, o.Graphics, (o.Timeout == 0 ? float.PositiveInfinity : o.Timeout).Seconds(), rng.Next(), o.DeltaTime.Seconds());
                Console.WriteLine("begin");
                MonoGameWindow.Init(game, (float)o.GameSpeed);
                MonoGameWindow window = MonoGameWindow.Instance;
                window.Run();
                Console.WriteLine("end");
                switch (game.Result)
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

            // speed mode
            Console.WriteLine("simulating...");
            Stopwatch stopwatch = Stopwatch.StartNew();
            SimulationResults results = new(o.NumberSims);

            for (int i = 0; i < o.NumberSims; i++)
            {
                ThreadPool.QueueUserWorkItem(Simulate, new SimulationInfo(file, modules0, modules1, o, rng.Next(), results));
            }
            results.done.WaitOne();

            Console.WriteLine($"Blue player (p0) has {results.win0} wins, {results.draw} draws, {results.win1} losses.");
            Console.WriteLine($"Win rate {results.win0 / (float)(results.win0 + results.win1) * 100:0.00}%");
            stopwatch.Stop();
            Console.WriteLine($"time elapsed: {stopwatch.ElapsedMilliseconds} ms");
        }

        /// <summary>
        /// Contains all information necessary to perform one battle simulation.
        /// </summary>
        /// <param name="ships"></param>
        /// <param name="modules0">modules available to player 0</param>
        /// <param name="modules1">modules available to player 1</param>
        /// <param name="o">command line options</param>
        /// <param name="randomSeed">the game should be deterministic based on this seed</param>
        /// <param name="results">object to store the result into</param>
        class SimulationInfo(ShipLists ships, IReadOnlyDictionary<string, ModuleCreation.ModuleInfo> modules0, IReadOnlyDictionary<string, ModuleCreation.ModuleInfo> modules1, Options o, int randomSeed, SimulationResults results)
        {
            public readonly ShipLists ships = ships;
            public readonly IReadOnlyDictionary<string, ModuleCreation.ModuleInfo> modules0= modules0, modules1= modules1;
            public readonly Options options = o;
            public readonly int randomSeed = randomSeed;
            public readonly SimulationResults results = results;
        }

        /// <summary>
        /// contains a lock which will release after a given number of battles has completed and called CompleteGame.
        /// also tracks the result of these games.
        /// </summary>
        /// <param name="count">Number of CopleteGame() calls to wait for.</param>
        class SimulationResults(int count)
        {
            public int win0 = 0, win1 = 0, draw = 0;
            public int remaining = count;
            public readonly AutoResetEvent done = new(false);

            public void CompleteGame(Game.GameResult result)
            {
                switch (result)
                {
                    case Game.GameResult.win_0:
                        Interlocked.Increment(ref win0);
                        break;
                    case Game.GameResult.win_1:
                        Interlocked.Increment(ref win1);
                        break;
                    case Game.GameResult.draw:
                        Interlocked.Increment(ref draw);
                        break;
                }
                if (Interlocked.Decrement(ref remaining) == 0)
                {
                    done.Set();

                }
            }
        }

        /// <summary>
        /// simulates a game, then calls CompleteGame on the provided SimulationInfo.SimulationResults
        /// </summary>
        /// <param name="simulationInfo">should be of type SimulationInfo</param>
        static void Simulate(object? simulationInfo)
        {
            if (simulationInfo == null || simulationInfo is not SimulationInfo info)
            {
                throw new ArgumentException($"Expected a SimulationInfo, got {simulationInfo?.GetType()}");
            }
            Game game = new(info.ships, info.modules0, info.modules1, info.options.Graphics,
                (info.options.Timeout == 0 ? float.PositiveInfinity : info.options.Timeout).Seconds(),
                info.randomSeed, info.options.DeltaTime.Seconds());
            game.Load();
            while (game.Result == Game.GameResult.unfinished)
            {
                game.Tick();
            }
            info.results.CompleteGame(game.Result);
        }

        static void Main(string[] args)
        {
            Parser.Default.ParseArguments<Options>(args).WithParsed<Options>(RunMain);
        }
    }
}