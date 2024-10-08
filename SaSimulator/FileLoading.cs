﻿using System;
using System.Collections.Generic;
using System.IO;

namespace SaSimulator
{
    class ShipLists()
    {
        public readonly List<ModuleBuff> globalBuffs = [], player0Buffs = [], player1Buffs = [];
        public readonly List<ShipInfo> player0 = [], player1 = [];
    }
    internal class FileLoading
    {
        private static readonly StringSplitOptions splitDefault = StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries;
        private static ModuleBuff ReadModuleBuff(string line)
        {
            var ModuleStatAmount = line.Split(' ');
            if (!Enum.TryParse(ModuleStatAmount[0], true, out ModuleTag module))
            {
                throw new ArgumentException($"No such module tag: {ModuleStatAmount[0]}");
            }
            if (!Enum.TryParse(ModuleStatAmount[1], true, out StatType stat))
            {
                throw new ArgumentException($"No such module stat: {ModuleStatAmount[1]}");
            }
            if (!float.TryParse(ModuleStatAmount[2], out float amount))
            {
                throw new ArgumentException($"Invalid stat multiplier: {ModuleStatAmount[1]}");
            }
            return new(amount, stat, module);
        }

        private static Dictionary<string, float> ReadDict(string line)
        {
            Dictionary<string, float> result = [];
            foreach(string element in line.Split(",", splitDefault))
            {
                string[] nameAndValue = element.Split('=', splitDefault);
                result[nameAndValue[0]] = float.Parse(nameAndValue[1]);
            }
            return result;
        }

        private static void ReadModuleBuffsInto(string[] lines, List<ModuleBuff> buffs)
        {
            foreach (var line in lines)
            {
                buffs.Add(ReadModuleBuff(line));
            }
        }

        public static ShipLists ReadPlayersAndShips(string filename)
        {
            ShipLists result = new();

            var Players = File.ReadAllText(filename).Split("--- new player ---", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

            string[] globalArgs = Players[0].Split('\n'); // global modifiers, such as space arena anomalies module levels
            ReadModuleBuffsInto(globalArgs, result.globalBuffs);

            for (int i = 1; i < Players.Length; i++)  // process the player
            {
                var ships = Players[i].Split("--- new ship ---", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

                string[] playerArgs = ships[0].Split('\n'); // player specific modifiers, such as ship bonus
                ReadModuleBuffsInto(playerArgs, i == 1 ? result.player0Buffs : result.player1Buffs);

                for (int s = 1; s < ships.Length; s++)   // process the ship
                {
                    ShipInfo shipInfo = new(); // ship speed, turning
                    var lines = ships[s].Split('\n');
                    var shipArgs = lines[0].Split(' ');
                    shipInfo.speed = float.Parse(shipArgs[0]).CellsPerSecond();
                    shipInfo.turnSpeed = float.Parse(shipArgs[1]);
                    foreach (var line in lines)
                    {
                        var moduleNameAndPositions = line.Split(' ');
                        var moduleName = moduleNameAndPositions[0];
                        for (int pos = 0; pos * 2 + 2 < moduleNameAndPositions.Length; pos++)
                        {
                            int x = int.Parse(moduleNameAndPositions[2 * pos + 1]);
                            int y = int.Parse(moduleNameAndPositions[2 * pos + 2]);
                            shipInfo.modules.Add(new(moduleName, x, y));
                        }
                    }
                    var shipList = i == 1 ? result.player0 : result.player1;
                    shipList.Add(shipInfo);
                }

            }
            return result;
        }

        public static Dictionary<string, ModuleCreation.ModuleInfo> ReadModules(string filename)
        {
            Dictionary<string, ModuleCreation.ModuleInfo> result=[];
            var modules = File.ReadAllText(filename).Split("--- module ---", splitDefault);

            foreach (var module in modules)
            {
                ModuleCreation.ModuleInfo currentModule=new();
                var s = module.Split("- components -",splitDefault);
                string baseInfo = s[0], components = s.Length==1?"":s[1];
                string[] baseInfoSplit = baseInfo.Split('\n',splitDefault);
                currentModule.name = baseInfoSplit[0];
                currentModule.bonus = ReadModuleBuff(baseInfoSplit[1]);
                currentModule.stats = ReadDict(baseInfoSplit[2]);

                foreach(string component in components.Split('\n', splitDefault))
                {
                    string[] nameAndProps = component.Split('|', StringSplitOptions.TrimEntries);
                    ModuleCreation.ComponentInfo currentComp = new()
                    {
                        componentName = nameAndProps[0],
                        parameters = ReadDict(nameAndProps[1])
                    };
                    currentModule.components.Add(currentComp);
                }
                result[currentModule.name] = currentModule;
            }
            return result;
        }
    }
}
