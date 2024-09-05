using System;
using System.Collections.Generic;
using System.IO;

namespace SaSimulator
{
    class ShipLists(List<ShipInfo> player0, List<ShipInfo> player1)
    {
        public readonly List<ShipInfo> player0 = player0, player1 = player1;
    }
    internal class FileLoading
    {
        public static ShipLists Read(string filename)
        {
            List<ShipInfo> player0 = [], player1 = [];

            var Players = File.ReadAllText(filename).Split("--- new player ---", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            string globalArgs = Players[0]; // TODO: global modifiers, such as space arena anomalies or default module level
            for (int i = 1; i < Players.Length; i++)
            {
                var ships = Players[i].Split("--- new ship ---", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                string playerArgs = ships[0]; // TODO: player specific modifiers, such as ship bonus
                for (int s = 1; s < ships.Length; s++)
                {
                    ShipInfo shipInfo = new(); // TODO: ship speed, turning
                    var lines = ships[s].Split('\n');
                    var shipArgs = lines[0].Split(' ');
                    shipInfo.speed = double.Parse(shipArgs[0]).CellsPerSecond();
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
                    var shipList = i == 1 ? player0 : player1;
                    shipList.Add(shipInfo);
                }

            }
            return new(player0, player1);
        }
    }
}
