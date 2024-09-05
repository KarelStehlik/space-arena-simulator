using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using static SaSimulator.Physics;

namespace SaSimulator
{
    enum DamageType { Ballistics, Explosive, Laser };

    // A ship is made of rectangular modules arranged on a square grid.
    internal class Module : GameObject
    {
        public readonly int width, height; // Width and height in cells
        public Transform relativePosition; // relative to ship centre
        private readonly Sprite? sprite;
        private readonly Sprite? outline;
        private float life, maxLife;
        private float armor; // reduces non-laser damage taken by a flat amount
        private float reflect; // reduces laser damage by a percentage
        private float penetrationBlocking; // reduces damage of penetrating weapons by a percentage after hitting this. This is 0 for most modules
        private readonly List<IModuleComponent> components = [];
        public readonly Ship ship;

        public Module(int width, int height, string textureName, float maxLife, float armor, float penetrationBlocking, float reflect, Ship ship) : base(ship.game)
        {
            this.width = width;
            this.height = height;
            this.armor = armor;
            this.penetrationBlocking = penetrationBlocking;
            this.ship = ship;
            this.reflect = reflect;
            life = this.maxLife = maxLife;
            if (game.hasGraphics)
            {
                sprite = new(textureName)
                {
                    Size = new(width, height)
                };
                outline = new("cell")
                {
                    Size = new(width + .3f, height + .3f),
                    Color = ship.side == 0 ? Color.Lime : Color.Red
                };
            }
        }

        // return this
        public Module AddComponent(IModuleComponent component)
        {
            components.Add(component);
            return this;
        }

        public void DrawOutline(SpriteBatch batch)
        {
            outline.setTransform(ref WorldPosition);
            outline.Draw(batch);
        }

        public override void Draw(SpriteBatch batch)
        {
            sprite.setTransform(ref WorldPosition);
            float lifePart = life / maxLife;
            sprite.Color = IsDestroyed ? Color.Black : new(1 - lifePart, lifePart, 0);
            sprite.Draw(batch);
        }

        public override void Tick(Time dt)
        {
            WorldPosition = ship.WorldPosition + relativePosition;
            if (IsDestroyed)
            {
                return;
            }
            foreach (var component in components)
            {
                component.Tick(dt, this);
            }
        }

        // Return damage taken for the purposes of penetration
        public float TakeDamage(float amount, DamageType type)
        {
#if DEBUG
            if (IsDestroyed)
            {
                throw new Exception("module destroyed twice");
            }
#endif
            if (type == DamageType.Laser)
            {
                life -= amount * game.DamageScaling * (1 - reflect);
            }
            else
            {
                life -= amount * game.DamageScaling - armor;
            }
            if (life <= 0)
            {
                IsDestroyed = true;
                foreach (var component in components)
                {
                    component.OnDestroyed(this);
                }
                ship.modulesAlive--;
            }
            return (amount - armor) * (1 - penetrationBlocking);
        }
    }

    internal interface IModuleComponent
    {
        void Tick(Time dt, Module module);
        void OnDestroyed(Module module);
    }

    internal class Gun(Distance range, float firingArc, float spread) : IModuleComponent
    {
        public Distance range = range;
        float spread = spread;
        float firingArc = firingArc;
        private Ship? target;

        public void OnDestroyed(Module module)
        {
        }

        public virtual void Tick(Time dt, Module module)
        {
        }

        private bool CanTarget(Ship ship, Module thisModule)
        {
            return !ship.IsDestroyed && Vector2.Distance(ship.WorldPosition.Position, thisModule.WorldPosition.Position) < range.Cells &&
                   Physics.ConeCircleIntersect(ship.WorldPosition.Position, (float)ship.outerDiameter.Cells, thisModule.WorldPosition.Position, (float)thisModule.WorldPosition.rotation, firingArc);
        }

        protected Ship? GetTarget(Module thisModule)
        {
            if (target == null || !CanTarget(target, thisModule))
            {
                foreach (Ship ship in thisModule.ship.GetEnemies())
                {
                    if (CanTarget(ship, thisModule))
                    {
                        target = ship;
                        return ship;
                    }
                }
                target = null;
            }
            return target;
        }

        protected float Aim(Module thisModule)
        {
            if (target == null)
            {
                return 0;
            }
            Vector2 distance = target.WorldPosition.Position - thisModule.WorldPosition.Position;
            return Physics.ClampAngle((float)Math.Atan2(distance.Y, distance.X), (float)thisModule.WorldPosition.rotation, firingArc / 2) + spread * (float)(thisModule.game.rng.NextDouble() - .5);
        }
    }

    // The gun will load one bullet every [cooldown], up to [maxAmmo]. When the bullets reach [burstFireThreshold]
    // and an enemy is in range, the gun will enter burst fire, during which it will fire every [burstFireInterval]
    // until all bullets are depleted or no enemy is in range.
    // [firingArc] is the cone arc in which the gun can target and aim,
    // [spread] is a random deviation in the angle the bullet is fired (this can go past the firing arc)
    internal class BallisticGun(Time cooldown, float maxAmmo, int burstFireThreshold,
        Time burstFireInterval, Distance range, Speed bulletSpeed, float firingArc, float spread,
        float damage) : Gun(range, firingArc, spread)
    {
        Time duration = range / bulletSpeed;
        float bulletsPerSecond = 1 / (float)cooldown.Seconds;
        Speed speed = bulletSpeed;
        float damage = damage;
        float maxAmmo = maxAmmo;
        float ammo = 0;
        bool isBurstFire = false;
        Time burstFireActiveTime = 0.Seconds();

        public override void Tick(Time dt, Module module)
        {
            ammo += (float)dt.Seconds * bulletsPerSecond;
            ammo = ammo > maxAmmo ? maxAmmo : ammo;

            Ship? target = GetTarget(module);

            int bulletsToFire = 0;
            if (isBurstFire)
            {
                burstFireActiveTime += dt;
                while (ammo >= 1 && burstFireActiveTime.Seconds >= burstFireInterval.Seconds)
                {
                    ammo--;
                    bulletsToFire++;
                    burstFireActiveTime -= burstFireInterval;
                }
                if (ammo < 1 || target == null)
                {
                    isBurstFire = false;
                }
            }
            else
            {
                isBurstFire = ammo >= burstFireThreshold;
            }

            while (bulletsToFire > 0 && target != null)
            {
                bulletsToFire--;
                module.game.AddObject(
                    new Projectile(module.game, new(module.WorldPosition.x, module.WorldPosition.y, Aim(module)),
                    speed, duration, damage, module.ship.side, new(1, 1, .2f, .5f)));
            }
        }
    }

    internal class LaserGun(Time cooldown, Time duration,
        Distance range, float firingArc,
        float damage) : Gun(range, firingArc, 0)
    {
        Time cooldown = cooldown, duration = duration;
        float damage = damage;
        bool currentlyFiring = false;
        Time currentPhaseRemainingDuration = 0.Seconds();

        public override void Tick(Time dt, Module module)
        {
            // on cooldown
            if (!currentlyFiring && currentPhaseRemainingDuration.Seconds > 0)
            {
                currentPhaseRemainingDuration -= dt;
                return;
            }

            Ship? target = GetTarget(module);
            if (target == null)
            {
                // not firing and not in range
                // firing and not in range
                if (currentlyFiring)
                {
                    currentlyFiring = false;
                    currentPhaseRemainingDuration = cooldown;
                }
                return;
            }
            // not firing and in range
            if (!currentlyFiring)
            {
                currentlyFiring = true;
                currentPhaseRemainingDuration = duration;
            }

            // firing and in range
            currentPhaseRemainingDuration -= dt;
            Transform position = new(module.WorldPosition.x, module.WorldPosition.y, Aim(module));
            module.game.AddObject(new Laser(module.game, position, range, damage * (float)dt.Seconds, module.ship.side, module.ship.side == 0 ? Color.LightBlue : Color.Red));

            if (currentPhaseRemainingDuration.Seconds <= 0)
            {
                currentPhaseRemainingDuration = cooldown;
                currentlyFiring = false;
            }
        }
    }

    internal static class Modules
    {
        public static Module SmallSteelArmor(Ship ship)
        {
            return new(1, 1, "cell", 145, 3, 0, .55f, ship);
        }
        public static Module MediumSteelArmor(Ship ship)
        {
            return new(2, 2, "cell", 550, 4, 0, .55f, ship);
        }
        public static Module Chaingun(Ship ship)
        {
            Module gun = new(1, 1, "cell", 15, 0, 0, 0, ship);
            gun.AddComponent(new BallisticGun((1 / 3d).Seconds(), 1, 1, 0.Seconds(), 35.Cells(), 200.CellsPerSecond(), 70f.ToRadians(), 0.05f, 4));
            return gun;
        }
        public static Module SmallLaser(Ship ship)
        {
            Module gun = new(1, 1, "cell", 10005, 0, 0, 0, ship);
            gun.AddComponent(new LaserGun(2.Seconds(), 2.Seconds(), 500.Cells(), 360f.ToRadians(), 10));
            return gun;
        }
    }
}
