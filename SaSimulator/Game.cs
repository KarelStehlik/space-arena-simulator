using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using static SaSimulator.Physics;

namespace SaSimulator
{
    internal class GameObject(Game game)
    {
        public Transform WorldPosition;
        public Distance size = 0.Cells(); // approximate distance from center to edge of bounding box
        public readonly Game game = game;

        public bool IsDestroyed { get; protected set; }
        public virtual void Tick(Time dt) { }
        public virtual void Draw(SpriteBatch batch) { }
        public virtual UniformGrid? BelongsToGrid() { return null; }
    }

    /// <summary>
    /// Stores data relating to a player's fleet, such as ships and stat modifiers.
    /// </summary>
    /// <param name="buffs">permanent modifiers to the player's modules</param>
    /// <param name="possibleModules">modules which the player's ships can use</param>
    internal class Player(List<ModuleBuff> buffs, IReadOnlyDictionary<string, ModuleCreation.ModuleInfo> possibleModules)
    {
        public readonly IReadOnlyDictionary<string, ModuleCreation.ModuleInfo> possibleModules=possibleModules;

        public readonly List<Ship> ships = [];
        public readonly IReadOnlyList<ModuleBuff> buffs = buffs;

        // [game mechanic] Module bonuses:
        // Having at least 1 of a particular type of module in your fleet grants a buff to all ships in the fleet.
        // this buff is specific to that module type, IE. Chaingun has a different bonus than Missile Launcher.
        // This buff vanishes when all modules of that type have been destroyed.
        private class ModuleBonus(ModuleBuff buff)
        {
            public readonly ModuleBuff buff=buff;
            public int grantedBy = 1;
        }
        private Dictionary<string, ModuleBonus> uniqueModuleBonuses=new();
        public void AddModuleBonus(string name, ModuleBuff buff)
        {
            if (uniqueModuleBonuses.ContainsKey(name))
            {
                uniqueModuleBonuses[name].grantedBy++;
            }
            else
            {
                uniqueModuleBonuses[name] = new(buff);
            }
        }
        public void RemoveModuleBonus(string name)
        {
            ModuleBonus bonus = uniqueModuleBonuses[name];
            if (--bonus.grantedBy == 0)
            {
                foreach (Ship ship in ships)
                {
                    ship.ApplyModuleBuff(bonus.buff * -1);
                }
                uniqueModuleBonuses.Remove(name);
            }
        }

        public void ApplyAllBuffs()
        {
            foreach (Ship ship in ships)
            {
                foreach (ModuleBuff buff in buffs)
                {
                    ship.ApplyModuleBuff(buff);
                }
                foreach(ModuleBonus bonus in uniqueModuleBonuses.Values)
                {
                    ship.ApplyModuleBuff(bonus.buff);
                }
            }
        }
    }

    /// <summary>
    /// directs the course of a game, ticks game objects and allows them to interact with each other. Ends when one of the players has no more ships left.
    /// </summary>
    /// <param name="ships">information from the battle description file</param>
    /// <param name="p0Modules">information from the player 0 module file</param>
    /// <param name="p1Modules">information from the player 1 module file</param>
    /// <param name="hasGraphics">determines whether the game should call graphics updates on objects.</param>
    /// <param name="timeout">after this amount of in-game time, the game will be considered a draw.</param>
    /// <param name="randomSeed"></param>
    /// <param name="deltatime">should be used to simulate lag, which affects certain game mechanics in Space Arena. A reasonable value for lag-free gameplay is 1/30 seconds</param>
    internal class Game(ShipLists ships,
        IReadOnlyDictionary<string, ModuleCreation.ModuleInfo> p0Modules, IReadOnlyDictionary<string, ModuleCreation.ModuleInfo> p1Modules,
        bool hasGraphics, Time timeout, int randomSeed, Time deltatime)
    {
        public readonly Time deltaTime = deltatime;
        public readonly bool hasGraphics = hasGraphics;
        public enum GameResult { win_0, win_1, draw, unfinished };
        public GameResult Result { get; private set; } = GameResult.unfinished;
        public readonly Player player0 = new(new(ships.player0Buffs.Concat(ships.globalBuffs)), p0Modules), player1 = new(new(ships.player1Buffs.Concat(ships.globalBuffs)), p1Modules);
        private readonly List<GameObject> gameObjects = [], newGameObjects = [];
        private readonly List<ShipInfo> player0ShipList = ships.player0, player1ShipList = ships.player1;
        public Time Time { get; private set; } = 0.Seconds();
        public readonly Time timeout = timeout;
        public readonly Random rng = new(randomSeed);
        public readonly UniformGrid hittableP0 = new(10), hittableP1 = new(10), missilesP1 = new(10), missilesP0 = new(10); // collision detection data structures
        public float DamageScaling { get; private set; } = 1; // [speculative game mechanic] it is clear that all damage ramps up over time.
                                                              // according to Discord, this increase is increases by 3% per second starting at 25 seconds.
        private static int gameId = 0;

        private void LoadPlayer(int side)
        {
            foreach (ShipInfo shipInfo in side==0?player0ShipList: player1ShipList)
            {
                Ship ship = new(shipInfo, this, side);
                (side==0?player0:player1).ships.Add(ship);
                gameObjects.Add(ship);
            }
        }

        public void Load()
        {
            int randPlayer = Interlocked.Increment(ref gameId)%2;
            LoadPlayer(randPlayer);
            LoadPlayer(1-randPlayer);
            player0.ApplyAllBuffs();
            player1.ApplyAllBuffs();
        }

        // Bounds of all ships in game.
        public RectangleF GetBounds()
        {
            IEnumerable<Ship> allShips = player0.ships.AsEnumerable().Concat(player1.ships);
            if (!allShips.Any())
            {
                return new(1, 1, 1, 1);
            }
            float minX = float.PositiveInfinity;
            float maxX = float.NegativeInfinity;
            float minY = float.PositiveInfinity;
            float maxY = float.NegativeInfinity;
            foreach (Ship ship in allShips)
            {
                minX = Math.Min(minX, (ship.WorldPosition.x - ship.size).Cells);
                maxX = Math.Max(maxX, (ship.WorldPosition.x + ship.size).Cells);
                minY = Math.Min(minY, (ship.WorldPosition.y - ship.size).Cells);
                maxY = Math.Max(maxY, (ship.WorldPosition.y + ship.size).Cells);
            }
            return new(minX, minY, maxX - minX, maxY - minY);
        }

        // new game objects will be added in the next tick
        public void AddObject(GameObject obj)
        {
            newGameObjects.Add(obj);
        }

        public void Tick()
        {
            Time += deltaTime;
            if (Time.Seconds > timeout.Seconds)
            {
                Result = GameResult.draw;
                return;
            }
            DamageScaling = 1 + (float)(Time.Seconds > 25 ? ((Time.Seconds - 25) * 0.03) : 0);

            // detect if a player has won
            player0.ships.RemoveAll(ship => ship.IsDestroyed);
            player1.ships.RemoveAll(ship => ship.IsDestroyed);
            if (player0.ships.Count == 0)
            {
                Result = player1.ships.Count == 0 ? GameResult.draw : GameResult.win_1;
                return;
            }
            if (player1.ships.Count == 0)
            {
                Result = GameResult.win_0;
                return;
            }

            // remove dead game objects
            gameObjects.RemoveAll(obj => obj.IsDestroyed);

            // add new ones
            gameObjects.AddRange(newGameObjects);
            newGameObjects.Clear();

            // populate collision detection
            var bounds = GetBounds();
            hittableP0.Reset(bounds);
            hittableP1.Reset(bounds);
            missilesP0.Reset(bounds);
            missilesP1.Reset(bounds);
            foreach (GameObject obj in gameObjects)
            {
                obj.BelongsToGrid()?.Add(obj);
            }

            // tick objects
            foreach (GameObject obj in gameObjects)
            {
                obj.Tick(deltaTime);
            }
        }

        public void Draw(SpriteBatch batch)
        {
            foreach (GameObject obj in gameObjects)
            {
                obj.Draw(batch);
            }
        }
    }
}
