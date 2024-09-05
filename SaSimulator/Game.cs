﻿using Microsoft.Xna.Framework.Graphics;
using System;
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

    internal class Game(List<ShipInfo> player0, List<ShipInfo> player1, bool hasGraphics, Time timeout, int randomSeed, Time deltatime)
    {
        public readonly Time deltatime = deltatime;
        public readonly bool hasGraphics = hasGraphics;
        public enum GameResult { win_0, win_1, draw, unfinished };
        public GameResult result { get; private set; } = GameResult.unfinished;
        public readonly List<Ship> player0Ships = [], player1Ships = [];
        private readonly List<GameObject> gameObjects = [], newGameObjects = [];
        private readonly List<ShipInfo> player0 = player0, player1 = player1;
        public Time time { get; private set; } = 0.Seconds();
        public readonly Time timeout = timeout;
        public readonly Random rng = new(randomSeed);
        public float DamageScaling { get; private set; } = 1; // According to Discord, all damage increases by 3% per second starting at 25 seconds

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
            if (!allShips.Any())
            {
                return new(1, 1, 1, 1);
            }
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
        public void Tick()
        {
            time += deltatime;
            if (time.Seconds > timeout.Seconds)
            {
                result = GameResult.draw;
                return;
            }
            DamageScaling = 1 + (float)(time.Seconds > 25 ? ((time.Seconds - 25) * 0.03) : 0);

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
                obj.Tick(deltatime);
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
