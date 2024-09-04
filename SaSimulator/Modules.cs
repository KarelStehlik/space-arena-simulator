using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;

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
        private readonly Ship ship;

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
            if (life < 0)
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

    internal static class Modules
    {
        public static Module Test(Ship ship)
        {
            return new(2, 3, "boost", 100, 0, 0, ship);
        }
    }
}
