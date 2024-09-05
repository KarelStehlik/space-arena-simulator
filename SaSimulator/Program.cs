using CommandLine;
using System;

namespace SaSimulator
{
    public class Options
    {
        [Option('G', "graphics", Required = false, Default = false, HelpText = "Use graphics window to view the battle.")]
        public bool Graphics { get; set; }
    }

    internal class Program
    {
        static void Main(Options o)
        {
            ShipInfo a = new();
            a.modules.Add(new ModulePlacement("Test", 0, 0));
            a.modules.Add(new ModulePlacement("Gun", 1, 3));
            ShipInfo b = new();
            b.modules.Add(new ModulePlacement("Test", 0, 0));
            b.modules.Add(new ModulePlacement("Test", 2, 0));

            Game? game = new([a], [b], o.Graphics, 60.Seconds());
            if (o.Graphics)
            {
                Console.WriteLine("begin");
                MonoGameWindow.Init(game);
                MonoGameWindow window = MonoGameWindow.Instance;
                window.Run();
                Console.WriteLine("end");
            }
            else
            {
                Console.WriteLine("Performing battle.");
                game.Load();
                while (game.result == Game.GameResult.unfinished)
                {
                    game.Tick(1.Seconds() / 30);
                }
            }
            if (game != null)
            {
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
            }
        }

        static void Main(string[] args)
        {
            Parser.Default.ParseArguments<Options>(args).WithParsed<Options>(Main);
        }
    }
}