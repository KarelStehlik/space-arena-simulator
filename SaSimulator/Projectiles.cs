using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework.Graphics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using static SaSimulator.Physics;
using static SaSimulator.Ship;

namespace SaSimulator
{
    // This always has a sprite, as bullet trails only exist when graphics are enabled
    internal class BulletTrail : GameObject
    {
        readonly Sprite sprite;
        Time duration = 0.2.Seconds();

        public BulletTrail(Game game, Vector2 from, Vector2 to, Color color) : base(game)
        {
            sprite = new("beam")
            {
                Size = new Vector2(0.5f, Vector2.Distance(from, to)),
                Position = (from + to) / 2,
                Color = color,
                Rotation = (float)(Math.Atan2(to.Y - from.Y, to.X - from.X)+Math.PI/2)
            };
        }

        public override void Tick(Time dt)
        {
            duration -= dt;
            if(duration.Seconds <= 0)
            {
                IsDestroyed = true;
            }
        }

        public override void Draw(SpriteBatch batch)
        {
            sprite.Draw(batch);
        }
    }

    internal class Bullet : GameObject
    {
        Speed speed, vx, vy;
        Time duration;
        float damage;
        Color color;
        Vector2 lastPosition;
        private readonly int side;

        public Bullet(Game game, Transform transform, Speed speed, Time duration, float damage, int side, Color color) : base(game)
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
            foreach(var target in targets)
            {
                foreach (ModuleHit module in target.RayIntersect(WorldPosition, speed * dt))
                {
                    if (!module.module.IsDestroyed)
                    {
                        module.module.TakeDamage(damage);
                        IsDestroyed = true;
                        if (game.hasGraphics)
                        {
                            WorldPosition = new(((double)module.position.X).Cells(), ((double)module.position.Y).Cells(), WorldPosition.rotation);
                            DrawTrail();
                        }
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
            WorldPosition = new(WorldPosition.x+vx*dt, WorldPosition.y+vy*dt, WorldPosition.rotation);
        }

        private void DrawTrail()
        {
            game.AddObject(new BulletTrail(game, lastPosition, WorldPosition.Position, color));
            lastPosition = WorldPosition.Position;
        }

        public override void Draw(SpriteBatch batch)
        {
            if(IsDestroyed) return;
            DrawTrail();
        }
    }
}
