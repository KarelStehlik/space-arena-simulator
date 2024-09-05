using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using static SaSimulator.Physics;

namespace SaSimulator
{
    // A ship is made of rectangular modules arranged on a square grid.
    internal class Module : GameObject
    {
        public readonly int width, height; // Width and height in cells
        public Transform relativePosition; // relative to ship centre
        private readonly Sprite? sprite;
        private float life, maxLife;
        private float armor; // Armor reduces damage taken by a flat amount
        private float penetrationBlocking; // reduces damage of penetrating weapons by a percentage after hitting this. This is 0 for most modules
        private readonly List<IModuleComponent> components = [];
        public readonly Ship ship;

        public Module(int width, int height, string textureName, float maxLife, float armor, float penetrationBlocking, Ship ship) : base(ship.game)
        {
            this.width = width;
            this.height = height;
            this.armor = armor;
            this.penetrationBlocking = penetrationBlocking;
            this.ship = ship;
            life = this.maxLife = maxLife;
            if (game.hasGraphics)
            {
                sprite = new(textureName)
                {
                    Size = new(width, height)
                };
            }
        }

        // return this
        public Module AddComponent(IModuleComponent component)
        {
            components.Add(component);
            return this;
        }

        public override void Draw(SpriteBatch batch)
        {
            sprite.setTransform(ref WorldPosition);
            float lifePart = life / maxLife;
            sprite.Color = new(1 - lifePart, lifePart, 0);
            sprite.Draw(batch);
        }

        public override void Tick(Time dt)
        {
            WorldPosition = ship.WorldPosition + relativePosition;
            foreach (var component in components)
            {
                component.Tick(dt, this);
            }
        }

        // Return damage taken for the purposes of penetration
        public float TakeDamage(float amount)
        {
            life -= amount - armor;
            if (life <= 0)
            {
                IsDestroyed = true;
                foreach (var component in components)
                {
                    component.OnDestroyed(this);
                }
            }
            return (amount - armor) * (1 - penetrationBlocking);
        }
    }

    internal interface IModuleComponent
    {
        void Tick(Time dt, Module module);
        void OnDestroyed(Module module);
    }

    internal class Gun(Module module, Time cooldown, float maxAmmo, Distance range, Speed speed, float damage) : IModuleComponent
    {
        Time duration = range/speed;
        float bulletsPerSecond = 1 / (float)cooldown.Seconds;
        Distance range = range;
        Speed speed = speed;
        float damage = damage;
        float maxAmmo = maxAmmo;
        Module module = module;
        float ammo = 0;

        public void OnDestroyed(Module module)
        {
        }

        public void Tick(Time dt, Module module)
        {
            ammo += (float)dt.Seconds * bulletsPerSecond;
            ammo = ammo > maxAmmo ? maxAmmo : ammo;

            while (ammo > 0)
            {
                ammo--;
                double angle = module.WorldPosition.rotation;
                module.game.AddObject(new Bullet(module.game, new(module.WorldPosition.x, module.WorldPosition.y, angle), speed, duration, damage, module.ship.side, new(1, 1, .2f, .5f)));
            }
        }
    }

    internal static class Modules
    {
        public static Module Test(Ship ship)
        {
            return new(2, 3, "cell", 100, 0, 0, ship);
        }
        public static Module Gun(Ship ship)
        {
            Module gun = new(1, 1, "cell", 100, 0, 0, ship);
            gun.AddComponent(new Gun(gun, 0.5.Seconds(), 10, 40.Cells(), 60.CellsPerSecond(), 10));
            return gun;
        }
    }
}
