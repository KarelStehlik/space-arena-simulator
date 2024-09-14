// Ignore Spelling: Aoe

using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
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

    /// <summary>
    /// describes the layout of a ship and all modules on it, as well as their stat modifiers.
    /// </summary>
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
            public readonly List<Modules.Shield> coveringShields = [];
        }

        private static readonly float MOVEMENT_DAMPENING = 0.75f;
        private static readonly float ROTATION_DAMPENING = 0.1f;
        private static readonly Time MIN_WARP_TIME = 5.Seconds(); // [speculative game mechanics] The time between warps is [this constant] + [cell count] / [sum of warp force]

        private readonly Cell[,] cells; // for each cell in this ship's grid, stores which module lies in this cell and any shields covering it there.
                                        // Each module is stored here once for every cell it covers
        public readonly List<Module> modules = []; // stores each module once
        private readonly int cellCount = 0; // number of cells occupied by modules
        private readonly int initialModuleNumber;
        public int modulesAlive = 0, energyModulesAlive = 0, weaponsAlive = 0;
        private readonly int width = 0, height = 0; // in cells
        public readonly int side;
        private Speed baseAcceleration, vx = 0.CellsPerSecond(), vy = 0.CellsPerSecond();
        private float baseTurnAcceleration, turningVelocity = 0;
        public float mass = 1;

        public float turnPower = 0, thrust = 0, energy = 0, energyUse = 0, warpForce = 0, afterburnerThrust=1, afterburnerTurnPower=1; // these are recalculated every tick

        private float energyPhase = 0, warpProgress = 0;
        private static readonly Time energyCycleDuration = 5.Seconds();
        private readonly Distance maxWeaponRange = 0.Cells();

        public Ship(ShipInfo info, Game game, int side) : base(game)
        {
            this.side = side;
            WorldPosition = side == 0 ? new(0.Cells(), 100.Cells(), -(float)Math.PI / 2) : new(50.Cells(), 0.Cells(), (float)Math.PI / 2);
            WorldPosition += new Transform(game.rng.Next(5).Cells(), game.rng.Next(50).Cells(), 0);
            baseAcceleration = info.speed;
            baseTurnAcceleration = info.turnSpeed;

            int maxShieldRadius = 0;

            foreach (ModulePlacement placement in info.modules)
            {
                // create the requested module
                Module module = ModuleCreation.Create(placement.module, this);
                module.relativePosition = new(placement.x.Cells(), placement.y.Cells(), 0);

                modules.Add(module);
                cellCount += module.height * module.width;

                // determine ship width and height
                width = Math.Max(width, placement.x + module.height);
                height = Math.Max(height, placement.y + module.width);

                foreach (ModuleComponent component in module.components)
                {
                    if (component is Modules.Shield shield)
                    {
                        maxShieldRadius = Math.Max(maxShieldRadius, (int)shield.Radius.Cells);
                    }
                    if (component is Modules.Gun gun)
                    {
                        maxWeaponRange = Math.Max(maxWeaponRange.Cells, gun.Range.Cells).Cells();
                    }
                }
            }

            width += 2 * maxShieldRadius;
            height += 2 * maxShieldRadius;

            // create ship grid
            cells = new Cell[width, height];

            size = Math.Max(width, height).Cells() / (float)Math.Sqrt(2);
            initialModuleNumber = modules.Count;

            // fill ship grid
            foreach (Module module in modules)
            {
                int xGridPos = (int)module.relativePosition.x.Cells + maxShieldRadius;
                int yGridPos = (int)module.relativePosition.y.Cells + maxShieldRadius;
                for (int x = xGridPos; x < xGridPos + module.height; x++)
                {
                    for (int y = yGridPos; y < yGridPos + module.width; y++)
                    {
                        if (cells[x, y] != null)
                        {
                            throw new ArgumentException($"Invalid ship: Cell [{x-maxShieldRadius}, {y- maxShieldRadius}] is covered by multiple modules.");
                        }
                        cells[x, y] = new(module);
                    }
                }
                // set module position relative to ship center
                module.relativePosition += new Transform((-width + module.height + 2 * maxShieldRadius).Cells() / 2, (-height + module.width + 2 * maxShieldRadius).Cells() / 2, 0);
            }

            // activate shields
            foreach (Module module in modules)
            {
                module.Initialize();
                foreach (ModuleComponent component in module.components)
                {
                    if (component is Modules.Shield shield)
                    {
                        AddShield(shield, module, shield.Radius);
                    }
                }
            }
        }

        // A ship is only active [energy/energyUse] of the time.
        private bool IsPowered()
        {
            return energyPhase * energyUse < energy;
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

        public void ApplyModuleBuff(ModuleBuff buff)
        {
            foreach (Module module in modules)
            {
                module.ApplyBuff(buff);
            }
        }

        public Player ThisPlayer()
        {
            return side == 0 ? game.player0 : game.player1;
        }

        public List<Ship> GetEnemies()
        {
            return side == 0 ? game.player1.ships : game.player0.ships;
        }

        // there are 3 known conditions of ship destruction: no weapons (unless this is a main ship and still has supports), no power, or "seriously damaged."
        // [speculative game mechanic] it is unclear what "seriously damaged" means.
        private bool IsCriticallyDamaged()
        {
            return modulesAlive < initialModuleNumber * 0.3 || energyModulesAlive ==0 || (weaponsAlive==0 && (ThisPlayer().ships.Count == 1 || this!= ThisPlayer().ships[0]));
        }

        private enum MovementAction { Forward, Retreat, CircleLeft, CircleRight };
        private static MovementAction[] possibleMoveActions = Enum.GetValues(typeof(MovementAction)).OfType<MovementAction>().ToArray();
        private Time movementActionRemainingDuration = 2.Seconds();
        private MovementAction currentAction = MovementAction.Forward;
        private static readonly Time movementActionMaxDuration = 2f.Seconds();
        // [speculative game mechanic] Ships always turn towards the enemy main,
        // however that is all we really know about movement patterns.
        // sometimes they rush forward, sometimes they fly away and sometimes they try to circle it.
        // How exactly this works is still mysterious.
        private void Accelerate(Time dt)
        {
            Ship enemyMain = GetEnemies()[0];

            // [speculative game mechanic] I believe that a ship's mobility involves the thrust from engines, which is affected by mass, and the base movement speed which is not.
            // common builds using armor have such mass that the base speed is already dominant, so changes in thrust or mass don't make much of a difference.
            // there was once an anomaly with 700% increased mass which seemed to affect nothing, it could have been a bug but i believe it was really just due to this.
            float turnAmount = (baseTurnAcceleration + turnPower / mass) * dt.Seconds * afterburnerTurnPower;
            Speed acceleration = (baseAcceleration + thrust.CellsPerSecond() / mass) * dt.Seconds * afterburnerThrust;
            if (thrust == 0 && turnPower == 0)
            {
                acceleration *= 0.2f;
                turnAmount *= 0.2f;
            }

            // turning
            float leftSide = (float)(WorldPosition.rotation + Math.PI / 2);
            if (IsPointInCone(enemyMain.WorldPosition.Position, WorldPosition.Position, leftSide, (float)Math.PI))
            {
                turningVelocity += turnAmount;
            }
            else
            {
                turningVelocity -= turnAmount;
            }

            // thrust
            float accelAngle = WorldPosition.rotation;
            switch (currentAction)
            {
                case MovementAction.Forward:
                    break;
                case MovementAction.Retreat:
                    accelAngle += (float)Math.PI; break;
                case MovementAction.CircleLeft:
                    accelAngle -= (float)Math.PI / 2; break;
                case MovementAction.CircleRight:
                    accelAngle += (float)Math.PI / 2; break;
            }
            if (thrust == 0 && turnPower == 0)
            {
                acceleration *= 0.2f;
            }
            vx += acceleration * (float)Math.Cos(accelAngle);
            vy += acceleration * (float)Math.Sin(accelAngle);
            movementActionRemainingDuration -= dt;
            if (movementActionRemainingDuration.Seconds <= 0)
            {
                movementActionRemainingDuration = movementActionMaxDuration;
                currentAction = possibleMoveActions[game.rng.Next(possibleMoveActions.Length)];
                float distanceToEnemyMain = Vector2.Distance(enemyMain.WorldPosition.Position, WorldPosition.Position);
                if (currentAction == MovementAction.Retreat && distanceToEnemyMain > maxWeaponRange.Cells * 0.8f ||
                    distanceToEnemyMain > maxWeaponRange.Cells * 1.2f)
                {
                    currentAction = MovementAction.Forward; // if enemy main is past weapon range, we don't retreat
                }
            }
        }

        private void Warp()
        {
            vx = 0.CellsPerSecond();
            vy = 0.CellsPerSecond();
            Ship enemyMain = GetEnemies()[0];
            WorldPosition = enemyMain.WorldPosition;
            WorldPosition += new Transform(0.Cells(), 0.Cells(), (float)(game.rng.NextDouble() - 0.5f) * 3);
            float warpDistance = Math.Min(maxWeaponRange.Cells * (float)-game.rng.NextDouble(), -size.Cells - enemyMain.size.Cells);
            WorldPosition += new Transform(warpDistance.Cells(), 0.Cells(), 0);
        }

        // Destroys the ship.
        private void Destroy()
        {
            IsDestroyed = true;
            foreach (Module m in modules)
            {
                if (!m.IsDestroyed)
                {
                    m.Destroy();
                }
            }
        }

        public override void Tick(Time dt)
        {
            // passive actions
            turningVelocity *= (float)Math.Pow(ROTATION_DAMPENING, dt.Seconds);
            float moveDamping = (float)Math.Pow(MOVEMENT_DAMPENING, dt.Seconds);
            vx *= moveDamping;
            vy *= moveDamping;
            WorldPosition = new Transform(WorldPosition.x + vx * dt, WorldPosition.y + vy * dt, WorldPosition.rotation + turningVelocity * dt.Seconds);

            if (IsCriticallyDamaged())
            {
                Destroy();
                return;
            }

            // modules can increase these with their Tick()
            turnPower = 0; thrust = 0; energy = 0; energyUse = 0; warpForce = 0; afterburnerThrust = 1; afterburnerTurnPower = 1;

            foreach (Module module in modules)
            {
                module.UpdatePosition();
                module.ProcessEnergy();
            }

            energyPhase += dt / energyCycleDuration;
            energyPhase %= 1;
            if (!IsPowered())
            {
                return; // everything past this point requires energy
            }

            // tick modules
            {
                foreach (Module module in modules)
                {
                    module.Tick(dt);
                }
            }


            // do movement
            Accelerate(dt);

            if (warpForce > 0)
            {
                warpProgress += dt / (MIN_WARP_TIME + (cellCount / warpForce).Seconds());
                if (warpProgress >= 1)
                {
                    warpProgress = (float)(game.rng.NextDouble()*0.1-0.05);
                    Warp();
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
            if (!IsPowered())
            {
                Sprite sprite = new("power") { Size = new(size.Cells * 0.7f, size.Cells * 0.7f) };
                sprite.SetTransform(WorldPosition);
                sprite.Draw(batch);
            }
        }

        public override UniformGrid? BelongsToGrid()
        {
            return side == 0 ? game.hittableP0 : game.hittableP1;
        }

        // removes a shield completely from covering any cells. This is very slow, and should basically never be called.
        // Instead, we will simply ignore depleted shields when processing hits.
        public void RemoveShield(Modules.Shield shield)
        {
            for (int x = 0; x < cells.GetLength(0); x++)
            {
                for (int y = 0; y < cells.GetLength(1); y++)
                {
                    Cell? cell = cells[x, y];
                    if (cell != null && cell.coveringShields.Contains(shield))
                    {
                        cell.coveringShields.Remove(shield);
                    }
                }
            }
        }


        // adds a shield to all cells whose center it covers. Doesn't generate new cells beyond the current 2d array,
        // (as we would need to reallocate everything and change a bunch of the ship's properties),
        // so a shield added this way which reaches past the ship will not have collisions beyond the ship's current borders.
        // There is no way in space arena to add shields or increase their size after ship creation, so this shouldn't be an issue.
        public void AddShield(Modules.Shield shield, Module source, Distance radius)
        {
            float middleCellX = source.relativePosition.x.Cells + width / 2;
            float middleCellY = source.relativePosition.y.Cells + height / 2;
            int minX = (int)(middleCellX - radius.Cells - 0.4f); // shield origin at x=4.5, radius 2 should just barely cover the x=2 cell whose center is at 2.5.
            int minY = (int)(middleCellY - radius.Cells - 0.4f);
            int maxX = (int)(middleCellX + radius.Cells - 0.4f); // shield origin at x=4.5, radius 2 should just barely cover the x=6 cell whose center is at 6.5.
            int maxY = (int)(middleCellY + radius.Cells - 0.4f);
            minX = Math.Max(0, minX);
            minY = Math.Max(0, minY);
            maxX = Math.Min(maxX, cells.GetLength(0) - 1);
            maxY = Math.Min(maxY, cells.GetLength(1) - 1);

            for (int x = minX; x <= maxX; x++)
            {
                for (int y = minY; y <= maxY; y++)
                {
                    if (Vector2.Distance(new(middleCellX, middleCellY), new(x + .5f, y + .5f)) < radius.Cells)
                    {
                        if (cells[x, y] == null)
                        {
                            cells[x, y] = new(null);
                        }
                        cells[x, y].coveringShields.Add(shield);
                    }
                }
            }
        }

        // damages all modules in cells whose center lies in the AOE.
        // [speculative game mechanic] This aoe is a square, which is evidently correct for reactor explosions, less clear for missiles.
        // [speculative game mechanic] each module will only take damage from this once, even if multiple of its cells are affected.
        public void TakeAoeDamage(Vector2 originWorldPos, Distance radius, float amount, DamageType type)
        {
            Vector2 relativePos = originWorldPos.RelativeTo(WorldPosition);
            float middleCellX = relativePos.X + width / 2f;
            float middleCellY = relativePos.Y + height / 2f;
            int minX = (int)(middleCellX - radius.Cells - 0.4f);
            int minY = (int)(middleCellY - radius.Cells - 0.4f);
            int maxX = (int)(middleCellX + radius.Cells - 0.4f);
            int maxY = (int)(middleCellY + radius.Cells - 0.4f);
            minX = Math.Max(0, minX);
            minY = Math.Max(0, minY);
            maxX = Math.Min(maxX, cells.GetLength(0) - 1);
            maxY = Math.Min(maxY, cells.GetLength(1) - 1);
            List<Module> alreadyHit = [];

            for (int x = minX; x <= maxX; x++)
            {
                for (int y = minY; y <= maxY; y++)
                {
                    Cell? cell = cells[x, y];
                    if (cell != null && cell.module != null && !cell.module.IsDestroyed && !alreadyHit.Contains(cell.module))
                    {
                        alreadyHit.Add(cell.module);
                        cell.module.TakeDamage(amount, type);
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
            // get the ray into the reference frame of this ship.
            Transform ray = rayOrigin.RelativeTo(WorldPosition);

            // the lowest-coordinate corner of this ship
            Vector2 lowestCorner = new(-width / 2f, -height / 2f);

            float stepSize = 0.8f;
            Vector2 step = new Vector2((float)Math.Cos(ray.rotation), (float)Math.Sin(ray.rotation)) * stepSize;
            Vector2 pos = ray.Position - lowestCorner;

            // [speculative game mechanic] this algorithm can indeed lead to damage passing between two modules sharing a corner.
            // That is the case in Space Arena as well, however it may be more or less common or work slightly differently. 
            for (int i = 0; i * stepSize <= rayLength.Cells; i++)
            {
                if (pos.X > 0 && (int)pos.X < width && pos.Y > 0 && (int)pos.Y < height)
                {
                    if (cells[(int)pos.X, (int)pos.Y] != null)
                    {
                        yield return new(cells[(int)pos.X, (int)pos.Y], stepSize.Cells() * i);
                    }
                }
                pos += step;
            }
        }
    }
}
