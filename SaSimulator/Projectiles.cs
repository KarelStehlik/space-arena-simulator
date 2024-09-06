using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using static SaSimulator.Physics;
using static SaSimulator.Ship;

namespace SaSimulator
{
    // This always has a sprite, as bullet trails only exist when graphics are enabled
    internal class BulletTrail : GameObject
    {
        readonly Sprite sprite;
        Time duration;

        public BulletTrail(Game game, Vector2 from, Vector2 to, Color color, Time duration) : base(game)
        {
            this.duration = duration;
            sprite = new("beam")
            {
                Size = new Vector2(0.2f, Vector2.Distance(from, to)),
                Position = (from + to) / 2,
                Color = color,
                Rotation = (float)(Math.Atan2(to.Y - from.Y, to.X - from.X) + Math.PI / 2)
            };
        }

        public override void Tick(Time dt)
        {
            duration -= dt;
            if (duration.Seconds <= 0)
            {
                IsDestroyed = true;
            }
        }

        public override void Draw(SpriteBatch batch)
        {
            sprite.Draw(batch);
        }
    }

    internal class Projectile : GameObject
    {
        Speed speed, vx, vy;
        Time duration;
        float damage;
        protected Color color;
        protected Vector2 lastPosition;
        private readonly int side;

        public Projectile(Game game, Transform transform, Speed speed, Time duration, float damage, int side, Color color) : base(game)
        {
            this.duration = duration;
            this.damage = damage;
            this.color = color;
            WorldPosition = transform;
            lastPosition = transform.Position;
            vx = speed * Math.Cos(transform.rotation);
            vy = speed * Math.Sin(transform.rotation);
            this.speed = speed;
            this.side = side;
        }

        public override void Tick(Time dt)
        {
            var targets = side == 0 ? game.player1Ships : game.player0Ships;
            foreach (var target in targets)
            {
                foreach (ModuleHit module in target.RayIntersect(WorldPosition, speed * dt))
                {
                    OnHit(target, module);
                    if (IsDestroyed)
                    {
                        return;
                    }
                }
            }
            duration -= dt;
            if (duration.Seconds <= 0)
            {
                IsDestroyed = true;
                if (game.hasGraphics)
                {
                    DrawTrail();
                }
            }
            WorldPosition = new(WorldPosition.x + vx * dt, WorldPosition.y + vy * dt, WorldPosition.rotation);
        }

        protected virtual void OnHit(Ship target, ModuleHit module)
        {
            IsDestroyed = true;
            if (game.hasGraphics)
            {
                WorldPosition = new(((double)module.position.X).Cells(), ((double)module.position.Y).Cells(), WorldPosition.rotation);
                DrawTrail();
            }
            if (module.module.IsDestroyed)
            {
                target.GetNearestModule(module.position).TakeDamage(damage, DamageType.Ballistics);
            }
            else
            {
                module.module.TakeDamage(damage, DamageType.Ballistics);
            }
            return;
        }

        protected virtual void DrawTrail()
        {
            game.AddObject(new BulletTrail(game, lastPosition, WorldPosition.Position, color, 0.1.Seconds()));
            lastPosition = WorldPosition.Position;
        }

        public override void Draw(SpriteBatch batch)
        {
            if (IsDestroyed) return;
            DrawTrail();
        }
    }


    internal class Laser(Game game, Transform transform, Distance length, float damage, int side, Color color) :
        Projectile(game, transform, length/1.Seconds(), 0.Seconds(), damage, side, color)
    {
        public override void Tick(Time dt)
        {
            base.Tick(1.Seconds());
        }

        protected override void DrawTrail()
        {
            game.AddObject(new BulletTrail(game, lastPosition, WorldPosition.Position, color, 0.01.Seconds()));
            lastPosition = WorldPosition.Position;
        }

        protected override void OnHit(Ship target, ModuleHit module)
        {
            IsDestroyed = true;
            if (game.hasGraphics)
            {
                WorldPosition = new(((double)module.position.X).Cells(), ((double)module.position.Y).Cells(), WorldPosition.rotation);
                DrawTrail();
            }
            if (module.module.IsDestroyed)
            {
                target.GetNearestModule(module.position).TakeDamage(damage, DamageType.Laser);
            }
            else
            {
                module.module.TakeDamage(damage, DamageType.Laser);
            }
            return;
        }
    }
}
