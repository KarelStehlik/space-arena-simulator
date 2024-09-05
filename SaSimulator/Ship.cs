using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Reflection;
using static SaSimulator.Physics;

namespace SaSimulator
{
    internal class ModulePlacement(string module, int x, int y)
    {
        public readonly string module = module;
        // this is janky, but not a typo.
        // The space arena ship builder has the ship pointing upwards, so a player might expect the ship's front to be in the positive Y direction.
        // However, it made more sense for me to have the ship facing forward so from its reference frame "forward" would be positive X, that is angle 0.
        // This conversion happens here.
        public readonly int x = y, y = x;
    }
    // ShipInfo describes the layout of a ship and all modules on it, as well as their stat modifiers.
    internal class ShipInfo
    {
        public Speed speed = 1.CellsPerSecond();
        public float turnSpeed = 0.1f;
        public List<ModulePlacement> modules = [];
    }

    internal class Ship : GameObject
    {
        private readonly float MOVEMENT_DAMPENING = 0.8f;
        private readonly float ROTATION_DAMPENING = 0.1f;

        private readonly Module[,] cells; // for each cell in this ship's grid, stores which module lies in this cell. Each module is stored here once for every cell it covers
        private readonly List<Module> modules = []; // stores each module once
        private readonly int initialModuleNumber;
        public int modulesAlive;
        private readonly int width = 0, height = 0; // in cells
        public readonly Distance outerDiameter, innerDiameter;
        public readonly int side;
        private Speed acceleration, vx = 0.CellsPerSecond(), vy = 0.CellsPerSecond();
        private float turnSpeed, turning = 0;


        public Ship(ShipInfo info, Game game, int side) : base(game)
        {
            this.side = side;
            WorldPosition = side == 0 ? new(0.Cells(), 100.Cells(), -Math.PI / 2) : new(50.Cells(), 0.Cells(), Math.PI / 2);
            WorldPosition += new Transform(game.rng.Next(5).Cells(), game.rng.Next(50).Cells(), 0);
            acceleration = info.speed;
            turnSpeed = info.turnSpeed;

            // First, we create the modules
            foreach (ModulePlacement placement in info.modules)
            {
                // create the requested module
                MethodInfo method = typeof(Modules).GetMethod(placement.module) ?? throw new ArgumentException($"No such module: {placement.module}");
                Module module = method.Invoke(null, [this]) as Module;
                module.relativePosition = new(placement.x.Cells(), placement.y.Cells(), 0);

                modules.Add(module);
                // determine ship width and height
                width = width > placement.x + module.width ? width : placement.x + module.width;
                height = height > placement.y + module.height ? height : placement.y + module.height;
            }

            // create ship grid
            cells = new Module[width, height];

            innerDiameter = Math.Min(width, height).Cells();
            outerDiameter = Math.Max(width, height).Cells();
            initialModuleNumber = modulesAlive = modules.Count;

            // fill ship grid
            foreach (Module module in modules)
            {
                int xGridPos = (int)module.relativePosition.x.Cells;
                int yGridPos = (int)module.relativePosition.y.Cells;
                for (int x = xGridPos; x < xGridPos + module.width; x++)
                {
                    for (int y = yGridPos; y < yGridPos + module.height; y++)
                    {
                        if (cells[x, y] != null)
                        {
                            throw new ArgumentException($"Invalid ship: Cell [{x}, {y}] is covered by multiple modules.");
                        }
                        cells[x, y] = module;
                    }
                }
                // set module position relative to ship centre
                module.relativePosition += new Transform((-width + module.width).Cells() / 2, (-height + module.height).Cells() / 2, 0);
            }
        }

        public Module GetNearestModule(Vector2 worldPosition)
        {
            Module? best = null;
            float bestDistance = float.PositiveInfinity;
            foreach (Module module in modules)
            {
                if (module.IsDestroyed)
                {
                    continue;
                }
                float dist = Vector2.DistanceSquared(worldPosition, module.WorldPosition.Position);
                if (dist < bestDistance)
                {
                    best = module;
                    bestDistance = dist;
                }
            }
            return best;
        }

        public List<Ship> GetEnemies()
        {
            return side == 0 ? game.player1Ships : game.player0Ships;
        }

        private bool IsCriticallyDamaged()
        {
            return modulesAlive < initialModuleNumber * 0.3;
        }

        public override void Tick(Time dt)
        {
            Ship enemyMain = GetEnemies()[0];

            // do movement
            {
                // determine whether the enemy is to the left of this ship
                float leftSide = (float)(WorldPosition.rotation + Math.PI / 2);
                if (Physics.IsPointInCone(enemyMain.WorldPosition.Position, WorldPosition.Position, leftSide, (float)Math.PI))
                {
                    turning += turnSpeed * (float)dt.Seconds;
                }
                else
                {
                    turning -= turnSpeed * (float)dt.Seconds;
                }

                vx += acceleration * Math.Cos(WorldPosition.rotation) * dt.Seconds;
                vy += acceleration * Math.Sin(WorldPosition.rotation) * dt.Seconds;

                turning *= (float)Math.Pow(ROTATION_DAMPENING, dt.Seconds);
                vx *= (float)Math.Pow(MOVEMENT_DAMPENING, dt.Seconds);
                vy *= (float)Math.Pow(MOVEMENT_DAMPENING, dt.Seconds);

                WorldPosition = new Transform(WorldPosition.x + vx * dt, WorldPosition.y + vy * dt, WorldPosition.rotation + turning);
            }

            // process damage taken
            {
                if (IsCriticallyDamaged())
                {
                    IsDestroyed = true;
                    return;
                }
            }

            // tick modules
            {
                foreach (Module module in modules)
                {
                    module.Tick(dt);
                }
            }
        }

        public override void Draw(SpriteBatch batch)
        {
            foreach (Module module in modules)
            {
                module.DrawOutline(batch);
            }
            foreach (Module module in modules)
            {
                module.Draw(batch);
            }
        }

        public readonly struct ModuleHit(Module module, Vector2 position)
        {
            public readonly Module module = module;
            public readonly Vector2 position = position;
        }

        // Enumerates modules in all cells hit by the given ray.
        public IEnumerable<ModuleHit> RayIntersect(Transform rayOrigin, Distance rayLength)
        {
            // first, get the ray into the reference frame of this ship.
            Transform ray = WorldPosition - rayOrigin;

            // the lowest-coordinate corner of this ship
            Vector2 lowestCorner = new(-width / 2f, -height / 2f);
            Vector2 step = new((float)Math.Cos(ray.rotation), (float)Math.Sin(ray.rotation));
            Vector2 pos = ray.Position - lowestCorner;

            // commence brute force temporary solution
            for (int i = 0; i <= rayLength.Cells; i++)
            {
                if (pos.X > 0 && (int)pos.X < width && pos.Y > 0 && (int)pos.Y < height)
                {
                    if (cells[(int)pos.X, (int)pos.Y] != null)
                    {
                        yield return new(cells[(int)pos.X, (int)pos.Y], WorldPosition + (pos + lowestCorner));
                    }
                }
                pos += step;
            }
            yield break;
        }
    }
}
