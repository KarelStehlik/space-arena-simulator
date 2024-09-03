using CommandLine;
using Microsoft.Xna.Framework;
using SaSimulator;
using System.Runtime.InteropServices;
using System;

public class Options
{
    [Option('G', "graphics", Required = false, Default = false, HelpText = "Use graphics window to view the battle.")]
    public bool Graphics { get; set; }
}

namespace SaSimulator
{
    internal class Program
    {
        static void Main(Options o)
        {
            if (o.Graphics)
            {
                Console.WriteLine("begin");
                MonoGameWindow window = new();
                window.Run();
                Console.WriteLine("end");
            }
            else
            {
                Console.WriteLine("Performing battle.");
            }
        }

        static void Main(string[] args)
        {
            Parser.Default.ParseArguments<Options>(args).WithParsed<Options>(Main);
        }
    }
}