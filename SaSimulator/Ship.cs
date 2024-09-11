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
        // this is questionable, but not a typo.
        // The space arena ship builder has the ship pointing upwards, so a player might expect the ship's front to be in the positive Y direction.
        // However, it made more sense for me to have the ship facing forward so from its reference frame "forward" would be angle 0, that is positive X
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
        public class Cell(Module? module)
        {
            public readonly Module? module = module;
            public readonly List<Shield> coveringShields = [];
        }

        private readonly float MOVEMENT_DAMPENING = 0.8f;
        private readonly float ROTATION_DAMPENING = 0.1f;

        private readonly Cell[,] cells; // for each cell in this ship's grid, stores which module lies in this cell and any shields covering it there.
                                        // Each module is stored here once for every cell it covers
        private readonly List<Module> modules = []; // stores each module once
        private readonly int initialModuleNumber;
        public int modulesAlive;
        private readonly int width = 0, height = 0; // in cells
        public readonly int side;
        private Speed acceleration, vx = 0.CellsPerSecond(), vy = 0.CellsPerSecond();
        private float turnPower, turningVelocity = 0;


        public Ship(ShipInfo info, Game game, int side) : base(game)
        {
            this.side = side;
            WorldPosition = side == 0 ? new(0.Cells(), 100.Cells(), -(float)Math.PI / 2) : new(50.Cells(), 0.Cells(), (float)Math.PI / 2);
            WorldPosition += new Transform(game.rng.Next(5).Cells(), game.rng.Next(50).Cells(), 0);
            acceleration = info.speed;
            turnPower = info.turnSpeed;

            int maxShieldRadius = 0;

            foreach (ModulePlacement placement in info.modules)
            {
                // create the requested module
                MethodInfo method = typeof(Modules).GetMethod(placement.module) ?? throw new ArgumentException($"No such module: {placement.module}");
                Module module = method.Invoke(null, [this]) as Module ?? throw new ArgumentException($"Unable to create module: {placement.module}");
                module.relativePosition = new(placement.x.Cells(), placement.y.Cells(), 0);

                modules.Add(module);

                // determine ship width and height
                width = Math.Max(width, placement.x + module.width);
                height = Math.Max(height, placement.y + module.height);

                foreach(IModuleComponent component in module.components)
                {
                    if(component is Shield shield)
                    {
                        maxShieldRadius = Math.Max(maxShieldRadius, (int)shield.Radius.Cells);
                    }
                }
            }

            width += 2 * maxShieldRadius;
            height += 2 * maxShieldRadius;

            // create ship grid
            cells = new Cell[width, height];

            size = Math.Max(width, height).Cells() / (float)Math.Sqrt(2);
            initialModuleNumber = modulesAlive = modules.Count;

            // fill ship grid
            foreach (Module module in modules)
            {
                int xGridPos = (int)module.relativePosition.x.Cells + maxShieldRadius;
                int yGridPos = (int)module.relativePosition.y.Cells + maxShieldRadius;
                for (int x = xGridPos; x < xGridPos + module.width; x++)
                {
                    for (int y = yGridPos; y < yGridPos + module.height; y++)
                    {
                        if (cells[x, y] != null)
                        {
                            throw new ArgumentException($"Invalid ship: Cell [{x}, {y}] is covered by multiple modules.");
                        }
                        cells[x, y] = new(module);
                    }
                }
                // set module position relative to ship centre
                module.relativePosition += new Transform((-width + module.width + 2*maxShieldRadius).Cells() / 2, (-height + module.height + 2*maxShieldRadius).Cells() / 2, 0);
            }

            // activate shields
            foreach(Module module in modules)
            {
                foreach(IModuleComponent component in module.components)
                {
                    if(component is Shield shield)
                    {
                        AddShield(shield, module, shield.Radius);
                    }
                }
            }
        }

        public Module? GetNearestModule(Vector2 worldPosition)
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
            return side == 0 ? game.player1.ships : game.player0.ships;
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
                if (IsPointInCone(enemyMain.WorldPosition.Position, WorldPosition.Position, leftSide, (float)Math.PI))
                {
                    turningVelocity += turnPower * dt.Seconds;
                }
                else
                {
                    turningVelocity -= turnPower * dt.Seconds;
                }

                vx += acceleration * (float)Math.Cos(WorldPosition.rotation) * dt.Seconds;
                vy += acceleration * (float)Math.Sin(WorldPosition.rotation) * dt.Seconds;

                turningVelocity *= (float)Math.Pow(ROTATION_DAMPENING, dt.Seconds);
                vx *= (float)Math.Pow(MOVEMENT_DAMPENING, dt.Seconds);
                vy *= (float)Math.Pow(MOVEMENT_DAMPENING, dt.Seconds);

                WorldPosition = new Transform(WorldPosition.x + vx * dt, WorldPosition.y + vy * dt, WorldPosition.rotation + turningVelocity*dt.Seconds);
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

        // removes a shield completely from covering any cells. This is very slow, and should basically never be called.
        // Instead, we will simply ignore depleted shields when processing hits.
        public void RemoveShield(Shield shield)
        {
            for(int x=0; x<cells.GetLength(0); x++)
            {
                for(int y=0; y<cells.GetLength(1); y++)
                {
                    Cell? cell = cells[x, y];
                    if (cell!=null && cell.coveringShields.Contains(shield))
                    {
                        cell.coveringShields.Remove(shield);
                    }
                }
            }
        }


        // adds a shield to all cells whose centre it covers. Doesn't generate new cells beyond the current 2d array,
        // (as we would need to reallocate everything and change a bunch of the ship's properties),
        // so a shield added this way which reaches past the ship will not have collisions beyond the ship's current borders.
        // There is no way in space arena to add shields or increase their size after ship creation, so this shouldn't be an issue.
        public void AddShield(Shield shield, Module source, Distance radius)
        {
            float middleCellX = source.relativePosition.x.Cells + width / 2;
            float middleCellY = source.relativePosition.y.Cells + height / 2;
            int minX = (int)(middleCellX - radius.Cells - 0.4f); // shield origin at x=4.5, radius 2 should just barely cover the x=2 cell whose centre is at 2.5.
            int minY = (int)(middleCellY - radius.Cells - 0.4f);
            int maxX = (int)(middleCellX + radius.Cells - 0.4f); // shield origin at x=4.5, radius 2 should just barely cover the x=6 cell whose centre is at 6.5.
            int maxY = (int)(middleCellY + radius.Cells - 0.4f);
            minX = Math.Max(0, minX);
            minY = Math.Max(0, minY);
            maxX = Math.Min(maxX, cells.GetLength(0)-1);
            maxY = Math.Min(maxY, cells.GetLength(1)-1);

            for(int x=minX; x<=maxX; x++)
            {
                for(int y=minY; y<=maxY; y++)
                {
                    if(Vector2.Distance(new(middleCellX,middleCellY), new(x+.5f,y+.5f)) < radius.Cells)
                    {
                        if(cells[x, y] == null)
                        {
                            cells[x, y] = new(null);
                        }
                        cells[x, y].coveringShields.Add(shield);
                    }
                }
            }
        }

        public readonly struct HitDetected(Cell cell, Distance traveled)
        {
            public readonly Cell cell = cell;
            public readonly Distance traveled = traveled;
        }

        // Enumerates modules in all cells hit by the given ray.
        public IEnumerable<HitDetected> RayIntersect(Transform rayOrigin, Distance rayLength)
        {
            // if the ray is too far, do nothing
            float maxDistance = (size + rayLength).Cells;
            if (Vector2.DistanceSquared(rayOrigin.Position,WorldPosition.Position) > maxDistance * maxDistance)
            {
                yield break;
            }

            // get the ray into the reference frame of this ship.
            Transform ray = WorldPosition - rayOrigin;

            // the lowest-coordinate corner of this ship
            Vector2 lowestCorner = new(-width / 2f, -height / 2f);

            float stepSize = 0.8f;
            Vector2 step = new Vector2((float)Math.Cos(ray.rotation), (float)Math.Sin(ray.rotation))*stepSize;
            Vector2 pos = ray.Position - lowestCorner;

            // commence simple search
            for (int i = 0; i*stepSize <= rayLength.Cells; i++)
            {
                if (pos.X > 0 && (int)pos.X < width && pos.Y > 0 && (int)pos.Y < height)
                {
                    if (cells[(int)pos.X, (int)pos.Y] != null)
                    {
                        yield return new(cells[(int)pos.X, (int)pos.Y], stepSize.Cells()*i);
                    }
                }
                pos += step;
            }
            yield break;
        }
    }
}
