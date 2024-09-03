using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SaSimulator
{
    internal class GameObject
    {
        protected readonly Game game;
        public GameObject(Game game)
        {
            this.game = game;
        }
        public bool IsDestroyed { get; protected set; }
        public void Tick(Time dt)
        {

        }
    }

    internal class Game
    {
        public enum GameResult { win_0, win_1, draw, unfinished };
        public GameResult result { get; private set; } = GameResult.unfinished;
        private List<Ship> player0Ships = new(), player1Ships = new();
        private List<GameObject> gameObjects = new(), newGameObjects = new();

        public Game(List<ShipInfo> player0, List<ShipInfo> player1)
        {
            foreach (ShipInfo shipInfo in player0)
            {
                Ship ship = new(shipInfo, this);
                player0Ships.Add(ship);
                gameObjects.Add(ship);
            }
            foreach (ShipInfo shipInfo in player1)
            {
                Ship ship = new(shipInfo, this);
                player1Ships.Add(ship);
                gameObjects.Add(ship);
            }
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
    }
}
