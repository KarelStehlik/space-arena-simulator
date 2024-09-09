using CommandLine;
using System;

namespace SaSimulator
{
    public class Options
    {
        [Option('F', "file", Required = true, Default = false, HelpText = "File to lod ships from")]
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
            var a = file.player0;
            var b = file.player1;
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
                        Console.WriteLine("player 0 wins");
                        break;
                    case Game.GameResult.win_1:
                        Console.WriteLine("player 1 wins");
                        break;
                    case Game.GameResult.draw:
                        Console.WriteLine("draw");
                        break;
                }
                return;
            }

            int win0 = 0, win1 = 0, draw = 0;
            Console.WriteLine("simulating...");
            for (int i = 0; i < o.NumberSims; i++)
            {
                Game game = new(a, b, o.Graphics, (o.Timeout == 0 ? float.PositiveInfinity : o.Timeout).Seconds(), rng.Next(), o.Deltatime.Seconds());
                game.Load();
                while (game.result == Game.GameResult.unfinished)
                {
                    game.Tick();
                }
                switch (game.result)
                {
                    case Game.GameResult.win_0:
                        win0++;
                        break;
                    case Game.GameResult.win_1:
                        win1++;
                        break;
                    case Game.GameResult.draw:
                        draw++;
                        break;
                }
            }
            Console.WriteLine($"Player 0 has {win0} wins, {draw} draws, {win1} losses.");
            Console.WriteLine($"Winrate {win0 / (float)(win0 + win1) * 100:0.00}%");
        }

        static void Main(string[] args)
        {
            Parser.Default.ParseArguments<Options>(args).WithParsed<Options>(RunMain);
        }
    }
}