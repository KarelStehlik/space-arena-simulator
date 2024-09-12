using Microsoft.Xna.Framework.Graphics;
using System;
using static SaSimulator.Physics;
using static SaSimulator.Ship;
using Microsoft.Xna.Framework;
using System.Linq;

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
        protected Speed speed, vx, vy;
        Time duration;
        protected float damage;
        protected Color color;
        protected Vector2 lastPosition;
        private readonly int side;

        public Projectile(Game game, Transform transform, Speed speed, Time duration, float damage, int side, Color color) : base(game)
        {
            this.duration = duration;
            this.damage = damage * game.DamageScaling;
            this.color = color;
            WorldPosition = transform;
            lastPosition = transform.Position;
            vx = speed * (float)Math.Cos(transform.rotation);
            vy = speed * (float)Math.Sin(transform.rotation);
            this.speed = speed;
            this.side = side;
        }

        public override void Tick(Time dt)
        {
            foreach (GameObject potentialTarget in game.collisionDetectionGrid.Get(WorldPosition,(speed*dt)))
            {
                if (!potentialTarget.IsDestroyed && potentialTarget is Ship target && target.side != side)
                {
                    foreach (HitDetected hit in target.RayIntersect(WorldPosition, speed * dt))
                    {
                        OnHit(target, hit);
                        if (IsDestroyed)
                        {
                            if (game.hasGraphics)
                            {
                                Time travelTime = hit.traveled / speed;
                                WorldPosition = new(WorldPosition.x + vx * travelTime, WorldPosition.y + vy * travelTime, WorldPosition.rotation);
                                DrawTrail();
                            }
                            return;
                        }
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

        protected virtual void OnHit(Ship target, HitDetected hit)
        {
            foreach (Shield shield in hit.cell.coveringShields)
            {
                if (shield.IsActive())
                {
                    shield.TakeShieldDamage(damage, DamageType.Ballistics);
                    IsDestroyed = true;
                    return;
                }
            }
            if (hit.cell.module == null)
            {
                return;
            }
            if (hit.cell.module.IsDestroyed) // when we hit a destroyed module, damage is transfered to the nearest non-destroyed module.
                                             // [game mechanic] This indeed allows it to bypass shields protecting those modules, as shown here:
                                             // https://youtube.com/shorts/hxgPU1mlzhA?feature=share
            {
                Time travelTime = hit.traveled / speed;
                target.GetNearestModule(new((WorldPosition.x + vx * travelTime).Cells, (WorldPosition.y + vy * travelTime).Cells)).TakeDamage(damage, DamageType.Ballistics);
                IsDestroyed = true;
                return;
            }
            hit.cell.module.TakeDamage(damage, DamageType.Ballistics);
            IsDestroyed = true;
        }

        protected virtual void DrawTrail()
        {
            game.AddObject(new BulletTrail(game, lastPosition, WorldPosition.Position, color, 0.1f.Seconds()));
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
            game.AddObject(new BulletTrail(game, lastPosition, WorldPosition.Position, color, 0.01f.Seconds()));
            lastPosition = WorldPosition.Position;
        }

        protected override void OnHit(Ship target, HitDetected hit)
        {
            if (hit.cell.module == null)
            {
                return;
            }
            if (hit.cell.module.IsDestroyed)
            {
                Time travelTime = hit.traveled / speed;
                target.GetNearestModule(new((WorldPosition.x + vx * travelTime).Cells, (WorldPosition.y + vy * travelTime).Cells)).TakeDamage(damage, DamageType.Laser);
                IsDestroyed = true;
                return;
            }
            hit.cell.module.TakeDamage(damage, DamageType.Laser);
            IsDestroyed = true;
        }
    }

    internal class Missile(Game game, Transform transform, Speed speed, Time duration, float radius, float damage, int side, Color color, Ship? target, float turningSpeed) :
        Projectile(game, transform, speed, duration, damage, side, color)
    {
        float radius = radius;

        public override void Tick(Time dt)
        {
            if (target!=null && !target.IsDestroyed)
            {
                // rotate towards target
                float leftSide = (WorldPosition.rotation + (float)Math.PI / 2);
                float newRotation = WorldPosition.rotation;
                if (Physics.IsPointInCone(target.WorldPosition.Position, WorldPosition.Position, leftSide, (float)Math.PI))
                {
                    newRotation += turningSpeed * dt.Seconds;
                }
                else
                {
                    newRotation -= turningSpeed * dt.Seconds;
                }
                WorldPosition = new(WorldPosition.x,WorldPosition.y, newRotation);

                vx = speed * (float)Math.Cos(newRotation);
                vy = speed * (float)Math.Sin(newRotation);
            }
            base.Tick(dt);
        }

        protected override void OnHit(Ship target, HitDetected hit)
        {
            foreach (Shield shield in hit.cell.coveringShields)
            {
                if (shield.IsActive())
                {
                    shield.TakeShieldDamage(damage, DamageType.Explosive); // missiles do not deal aoe damage when hitting shields
                    IsDestroyed = true;
                    return;
                }
            }
            if (hit.cell.module == null)
            {
                return;
            }

            System.Numerics.Vector2 explosionOrigin = WorldPosition.Position;
            if (hit.cell.module.IsDestroyed)
            {
                Time travelTime = hit.traveled / speed;
                explosionOrigin=target.GetNearestModule(new((WorldPosition.x + vx * travelTime).Cells, (WorldPosition.y + vy * travelTime).Cells)).WorldPosition.Position;
            }
            target.TakeAoeDamage(explosionOrigin, radius.Cells(), damage, DamageType.Explosive);
            IsDestroyed = true;
        }
    }
}
