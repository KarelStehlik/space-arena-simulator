using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using static SaSimulator.Physics;

namespace SaSimulator
{
    internal class GameObject(Game game)
    {
        public Transform WorldPosition;
        public readonly Game game = game;

        public bool IsDestroyed { get; protected set; }
        public virtual void Tick(Time dt) { }
        public virtual void Draw(SpriteBatch batch) { }
    }

    internal class Game(List<ShipInfo> player0, List<ShipInfo> player1, bool hasGraphics, Time timeout)
    {
        public readonly bool hasGraphics = hasGraphics;
        public enum GameResult { win_0, win_1, draw, unfinished };
        public GameResult result { get; private set; } = GameResult.unfinished;
        public readonly List<Ship> player0Ships = [], player1Ships = [];
        private readonly List<GameObject> gameObjects = [], newGameObjects = [];
        private readonly List<ShipInfo> player0 = player0, player1 = player1;
        private Time time = 0.Seconds(), timeout = timeout;

        public void Load()
        {
            foreach (ShipInfo shipInfo in player0)
            {
                Ship ship = new(shipInfo, this, 0);
                player0Ships.Add(ship);
                gameObjects.Add(ship);
            }
            foreach (ShipInfo shipInfo in player1)
            {
                Ship ship = new(shipInfo, this, 1);
                player1Ships.Add(ship);
                gameObjects.Add(ship);
            }
        }

        // Bounds of all ships in game. Does not include projectiles.
        public RectangleF GetBounds()
        {
            IEnumerable<Ship> allShips = player0Ships.AsEnumerable().Concat(player1Ships);
            float minX = allShips.Select(go => (float)(go.WorldPosition.x - go.outerDiameter / 2).Cells).Min();
            float maxX = allShips.Select(go => (float)(go.WorldPosition.x + go.outerDiameter / 2).Cells).Max();
            float minY = allShips.Select(go => (float)(go.WorldPosition.y - go.outerDiameter / 2).Cells).Min();
            float maxY = allShips.Select(go => (float)(go.WorldPosition.y + go.outerDiameter / 2).Cells).Max();
            return new(minX, minY, maxX - minX, maxY - minY);
        }

        // new game objects will be added in thhe next tick
        public void AddObject(GameObject obj)
        {
            newGameObjects.Add(obj);
        }

        // dt should be used to simulate lag, which affects certain game mechanics in Space Arena.
        // A reasonable value for lag-free gameplay is 1/30 seconds
        public void Tick(Time dt)
        {
            time += dt;
            if (time.Seconds > timeout.Seconds)
            {
                result = GameResult.draw;
                return;
            }
            // detect if a player has won
            player0Ships.RemoveAll(ship => ship.IsDestroyed);
            player1Ships.RemoveAll(ship => ship.IsDestroyed);
            if (player0Ships.Count == 0)
            {
                result = player1Ships.Count == 0 ? GameResult.draw : GameResult.win_1;
                return;
            }
            if (player1Ships.Count == 0)
            {
                result = GameResult.win_0;
                return;
            }

            // remove dead game objects
            gameObjects.RemoveAll(obj => obj.IsDestroyed);

            // add new ones
            gameObjects.AddRange(newGameObjects);
            newGameObjects.Clear();

            // game tick
            foreach (GameObject obj in gameObjects)
            {
                obj.Tick(dt);
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
