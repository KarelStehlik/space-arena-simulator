// Ignore Spelling: Chaingun

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using static SaSimulator.Physics;

namespace SaSimulator
{
    enum ModuleTag { Any, Armor, Weapon, Shield, Ballistic, Missile, Laser, Power, Repairbay, Engine, Junk }
    enum StatType
    {
        Health, Damage, Armor, Reflect, Firerate, Mass, PowerUse, PowerGen, Range,
        FiringArc, Thrust, TurnThrust, Strength, MaxRegen, RegenRate, Radius, ExplosionRadius, JunkHealth
    }
    // This represents a bonus to a specific stat on specific modules, such as "20% increased health of weapon modules"
    internal class ModuleBuff(float multiplier, StatType stat, ModuleTag targetModule)
    {
        public readonly StatType stat = stat;
        public float multiplier = multiplier;
        public readonly ModuleTag targetModule = targetModule;
    }

    enum DamageType { Ballistics, Explosive, Laser };

    // A ship is made of rectangular modules arranged on a square grid.
    internal class Module : GameObject
    {
        public readonly int width, height; // Width and height in cells
        public Transform relativePosition; // relative to ship center
        private readonly Sprite? sprite;
        private readonly Sprite? outline;
        private float currentHealthFraction = 1;
        private Attribute<float> maxHealth;
        private Attribute<float> armor; // reduces non-laser damage taken by a flat amount
        private Attribute<float> reflect; // reduces laser damage by a fraction
        private Attribute<float> powerUse;
        private Attribute<float> powerGen;
        private Attribute<float> mass;
        private readonly float penetrationBlocking; // reduces damage of penetrating weapons by a fraction after hitting this. This is 0 for most modules
        public readonly List<IModuleComponent> components = [];
        public readonly Ship ship;

        public Module(int height, int width, string textureName, float maxLife, float armor, float penetrationBlocking, float reflect, float powerUse, float powerGen, float mass, Ship ship) : base(ship.game)
        {
            this.width = width;
            this.height = height;
            this.armor = new(armor);
            this.penetrationBlocking = penetrationBlocking;
            this.ship = ship;
            this.reflect = new(reflect);
            this.maxHealth = new(maxLife);
            this.powerUse = new(powerUse);
            this.powerGen = new(powerGen);
            this.mass = new(mass);
            if (game.hasGraphics)
            {
                sprite = new(textureName)
                {
                    Size = new(width, height)
                };
                outline = new("borderless_cell")
                {
                    Size = new(width + .3f, height + .3f),
                    Color = ship.side == 0 ? Color.Blue : Color.Red
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
#if DEBUG
            if(outline==null)
            {
                throw new Exception("Module -> outline not set while drawing");
            }
#endif
            outline.SetTransform(WorldPosition);
            outline.Draw(batch);
        }

        public override void Draw(SpriteBatch batch)
        {
#if DEBUG
            if (sprite == null)
            {
                throw new Exception("Module -> sprite not set while drawing");
            }
#endif
            sprite.SetTransform(WorldPosition);
            sprite.Color = IsDestroyed ? Color.Black : new(1 - (float)currentHealthFraction, (float)currentHealthFraction, 0);
            sprite.Draw(batch);
            foreach (var component in components)
            {
                component.Draw(batch, this);
            }
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
            float damageTaken;
            if (type == DamageType.Laser)
            {
                damageTaken = amount * (1 - reflect);
            }
            else
            {
                damageTaken = amount - armor;
            }
            currentHealthFraction -= damageTaken / maxHealth;

            if (currentHealthFraction <= 0)
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

        static readonly StatType[] BaseModuleStats = [StatType.Health, StatType.Health, StatType.Health, StatType.Health, StatType.Health];
        public void ApplyBuff(ModuleBuff buff)
        {
            if (buff.targetModule == ModuleTag.Any)
            {
                ApplyBuffUnchecked(buff);
                return;
            }

            foreach (IModuleComponent component in components)
            {
                if (component.Tags.Contains(buff.targetModule))
                {
                    if (BaseModuleStats.Contains(buff.stat)) // buff matches one of the component's tags but applies to basic stats
                    {
                        ApplyBuffUnchecked(buff);
                        return;
                    }
                    component.ApplyBuff(buff); // buff matches one of the component's tags and applies to that component's stats
                }
            }
        }

        private void ApplyBuffUnchecked(ModuleBuff buff) // assumes the buff should affect this module and modifies one of the base module stats
        {
            switch (buff.stat)
            {
                case StatType.Health:
                    maxHealth.Increase += buff.multiplier; break;
                case StatType.Armor:
                    armor.Increase += buff.multiplier; break;
                case StatType.Reflect:
                    reflect.Increase += buff.multiplier; break;
                case StatType.PowerGen:
                    powerGen.Increase += buff.multiplier; break;
                case StatType.PowerUse:
                    powerUse.Increase += buff.multiplier; break;
            }
        }
    }

    internal interface IModuleComponent
    {
        ModuleTag[] Tags { get; }
        void Tick(Time dt, Module thisModule);
        void OnDestroyed(Module module);
        void ApplyBuff(ModuleBuff buff); // assumes the buff should affect this module and modifies one of the base module stats
        void Draw(SpriteBatch batch, Module thisModule);
    }

    // [speculative game mechanic]. In this implementation, shields will protect all cells whose center they cover
    // from all damage, regardless of whether the damage actually passes through the shield before reaching the edge of the cell.
    // see this video: https://youtube.com/shorts/8J-nw48iT7A?feature=share
    // the front surface of my 2 chainguns was not covered by any shields, yet they were protected. They were both destroyed around the same time,
    // probably because the shields broke.
    internal class Shield(float strength, Distance radius, float regenRate, float maxRegen) : IModuleComponent
    {
        private Attribute<float> strength = new(strength);
        private Attribute<Distance> radius = new(radius);
        public Distance Radius { get { return radius; } }
        private Attribute<float> regenRate = new(regenRate);
        private Attribute<float> maxRegen = new(maxRegen);
        private float regenRemainingFraction = 1, strengthRemainingFraction = 1;
        private bool mustReapply = false;
        private Sprite? sprite;
        private Time timeSinceDamageTaken = 10.Seconds();

        public ModuleTag[] Tags => [ModuleTag.Shield];

        public void ApplyBuff(ModuleBuff buff)
        {
            switch (buff.stat)
            {
                case StatType.Strength:
                    strength.Increase += buff.multiplier; break;
                case StatType.Radius:
                    radius.Increase += buff.multiplier;
                    mustReapply = true; break;
                case StatType.RegenRate:
                    regenRate.Increase += buff.multiplier; break;
                case StatType.MaxRegen:
                    maxRegen.Increase += buff.multiplier; break;
            }
        }

        public bool IsActive()
        {
            return strengthRemainingFraction > 0;
        }

        public void TakeShieldDamage(float amount, DamageType type)
        {
            strengthRemainingFraction -= amount / strength;
            timeSinceDamageTaken = 0.Seconds();
        }

        public void OnDestroyed(Module module)
        {
        }

        public void Tick(Time dt, Module thisModule)
        {
            if (mustReapply) // this shouldn't really happen, as no module bonuses affect shield radius. it is here for completeness.
            {
                thisModule.ship.RemoveShield(this);
                thisModule.ship.AddShield(this, thisModule, radius);
            }
            if (strengthRemainingFraction < 1)
            {
                float regenAmount = Math.Min(regenRate, regenRemainingFraction * maxRegen);
                regenRemainingFraction -= regenAmount / maxRegen;
                strengthRemainingFraction += regenAmount / strength;
            }
            timeSinceDamageTaken += dt;
        }

        public void Draw(SpriteBatch batch, Module thisModule)
        {
            sprite ??= new("shield")
                {
                    Size = new Vector2(Radius.Cells * 2, Radius.Cells * 2)
                };
            sprite.Position = thisModule.WorldPosition.Position;
            float opacity = Math.Max(0, 1f - timeSinceDamageTaken.Seconds);
            sprite.Color = new(opacity, opacity, opacity, opacity);
            sprite.Draw(batch);
        }
    }

    internal class Gun(Distance range, float firingArc, float spread, float damage, float fireRate) : IModuleComponent
    {
        protected Attribute<Distance> range = new(range);
        public Distance Range { get { return range; } }
        protected float spread = spread;
        Attribute<float> firingArc = new(firingArc);
        protected Attribute<float> damage = new(damage);
        protected Attribute<float> fireRate = new(fireRate);
        private Ship? target;

        public virtual ModuleTag[] Tags => [ModuleTag.Weapon];

        public void OnDestroyed(Module module)
        {
        }

        public virtual void Tick(Time dt, Module thisModule)
        {
        }

        private bool CanTarget(Ship ship, Module thisModule)
        {
            return !ship.IsDestroyed && Vector2.Distance(ship.WorldPosition.Position, thisModule.WorldPosition.Position) < (float)(range.Value.Cells) &&
                   ConeCircleIntersect(ship.WorldPosition.Position, ship.size.Cells, thisModule.WorldPosition.Position, thisModule.WorldPosition.rotation, firingArc);
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
            return ClampAngle((float)Math.Atan2(distance.Y, distance.X), (float)thisModule.WorldPosition.rotation, firingArc / 2) + spread * (float)(thisModule.game.rng.NextDouble() - .5);
        }

        public virtual void ApplyBuff(ModuleBuff buff)
        {
            switch (buff.stat)
            {
                case StatType.Range:
                    range.Increase += buff.multiplier; break;
                case StatType.FiringArc:
                    firingArc.Increase += buff.multiplier; break;
                case StatType.Damage:
                    damage.Increase += buff.multiplier; break;
                case StatType.Firerate:
                    fireRate.Increase += buff.multiplier; break;
            }
        }

        public void Draw(SpriteBatch batch, Module thisModule)
        {
        }
    }

    // The gun will load one bullet every [1/fire rate] seconds, up to [maxAmmo]. When the bullets reach [burstFireThreshold]
    // and an enemy is in range, the gun will enter burst fire, during which it will fire every [burstFireInterval]
    // until all bullets are depleted or no enemy is in range.
    // [firingArc] is the cone arc in which the gun can target and aim,
    // [spread] is a random deviation in the angle the bullet is fired (this can go past the firing arc)
    internal class BurstGun(float firerate, float maxAmmo, int burstFireThreshold,
        Time burstFireInterval, Distance range, Speed bulletSpeed, float firingArc, float spread,
        float damage) : Gun(range, firingArc, spread, damage, firerate)
    {
        public Time duration = range / bulletSpeed;
        protected Speed speed = bulletSpeed;
        protected float maxAmmo = maxAmmo;
        protected float ammo = 0;
        protected bool isBurstFire = false;
        protected Time burstFireActiveTime = 0.Seconds();

        public override ModuleTag[] Tags => [ModuleTag.Weapon, ModuleTag.Ballistic];

        public override void Tick(Time dt, Module thisModule)
        {
            ammo += dt.Seconds * fireRate;
            ammo = ammo > maxAmmo ? maxAmmo : ammo;

            Ship? target = GetTarget(thisModule);

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
                Fire(thisModule);
            }
        }

        public virtual void Fire(Module thisModule)
        {
            thisModule.game.AddObject(
                    new Projectile(thisModule.game, new(thisModule.WorldPosition.x, thisModule.WorldPosition.y, Aim(thisModule)),
                    speed, duration, damage, thisModule.ship.side, new(1, 1, .2f, .5f)));
        }
    }

    internal class MissileGun(float firerate, float maxAmmo, int burstFireThreshold,
        Time burstFireInterval, Distance range, Speed bulletSpeed, float firingArc, float spread, float radius,
        float damage, float guidanceStrength) :
        BurstGun(firerate, maxAmmo, burstFireThreshold, burstFireInterval, range, bulletSpeed, firingArc, spread, damage)
    {
        Attribute<float> radius = new(radius);
        public override ModuleTag[] Tags => [ModuleTag.Weapon, ModuleTag.Missile];

        public override void Fire(Module thisModule)
        {
            thisModule.game.AddObject(
                    new Missile(thisModule.game, new(thisModule.WorldPosition.x, thisModule.WorldPosition.y, Aim(thisModule)),
                    speed, duration, radius, damage, thisModule.ship.side, Color.LightGray, GetTarget(thisModule), guidanceStrength));
        }

        public override void ApplyBuff(ModuleBuff buff)
        {
            base.ApplyBuff(buff);
            if (buff.stat == StatType.ExplosionRadius)
            {
                radius.Increase += buff.multiplier;
            }
        }
    }

    internal class LaserGun(float firerate, Time duration,
        Distance range, float firingArc,
        float damage) : Gun(range, firingArc, 0, damage, firerate)
    {
        Time duration = duration;
        bool currentlyFiring = false;
        Time currentPhaseRemainingDuration = 0.Seconds();

        public override ModuleTag[] Tags => [ModuleTag.Weapon, ModuleTag.Laser];

        public override void Tick(Time dt, Module thisModule)
        {
            // on cooldown
            if (!currentlyFiring && currentPhaseRemainingDuration.Seconds > 0)
            {
                currentPhaseRemainingDuration -= dt;
                return;
            }

            Ship? target = GetTarget(thisModule);
            if (target == null)
            {
                // not firing and not in range
                // firing and not in range
                if (currentlyFiring)
                {
                    currentlyFiring = false;
                    currentPhaseRemainingDuration = (1 / fireRate).Seconds();
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
            Transform position = new(thisModule.WorldPosition.x, thisModule.WorldPosition.y, Aim(thisModule));
            thisModule.game.AddObject(new Laser(thisModule.game, position, this.range, damage * (float)dt.Seconds, thisModule.ship.side, thisModule.ship.side == 0 ? Color.LightBlue : Color.Red));

            if (currentPhaseRemainingDuration.Seconds <= 0)
            {
                currentPhaseRemainingDuration = (1 / fireRate).Seconds();
                currentlyFiring = false;
            }
        }
    }

    internal class JunkLauncher(float firerate, float maxAmmo, int burstFireThreshold,
        Time burstFireInterval, Distance range, Speed bulletSpeed, float health) :
        BurstGun(firerate, maxAmmo, burstFireThreshold, burstFireInterval, range, bulletSpeed, 2 * (float)Math.PI, 2*(float)Math.PI, 0)
    {
        Attribute<float> junkHealth = new(health);

        public override ModuleTag[] Tags => [ModuleTag.Junk];

        public override void Fire(Module thisModule)
        {
            thisModule.game.AddObject(new JunkPiece(thisModule.game, new(thisModule.WorldPosition.x, thisModule.WorldPosition.y, Aim(thisModule)), speed, duration, health,thisModule.ship.side));
        }

        public override void ApplyBuff(ModuleBuff buff)
        {
            base.ApplyBuff(buff);
            if (buff.stat == StatType.JunkHealth)
            {
                junkHealth.Increase += buff.multiplier;
            }
        }
    }

    internal static class Modules
    {
        public static Module SmallSteelArmor(Ship ship)
        {
            return new(1, 1, "cell", 145, 3, 0, .55f, 0, 0, 10, ship);
        }
        public static Module MediumSteelArmor(Ship ship)
        {
            return new(2, 2, "cell", 550, 4, 0, .55f, 0, 0, 10, ship);
        }
        public static Module Chaingun(Ship ship)
        {
            Module gun = new(1, 1, "cell", 15, 0, 0, 0, 0, 0, 10, ship);
            gun.AddComponent(new BurstGun(3.3333f, 1, 1, 0.Seconds(), 35.Cells(), 200.CellsPerSecond(), 70f.ToRadians(), 0.05f, 4));
            return gun;
        }
        public static Module SmallLaser(Ship ship)
        {
            Module gun = new(1, 1, "cell", 15, 0, 0, 0, 0, 0, 10, ship);
            gun.AddComponent(new LaserGun(0.5f, 2.Seconds(), 100.Cells(), 360f.ToRadians(), 10));
            return gun;
        }
        public static Module SmallMissile(Ship ship)
        {
            Module gun = new(1, 2, "cell", 30, 0, 0, 0, 0, 0, 10, ship);
            gun.AddComponent(new MissileGun(3.33333f, 1, 1, 0.Seconds(), 100.Cells(), 50.CellsPerSecond(), 70f.ToRadians(), 90f.ToRadians(), 2, 4, 2f)
            {
                duration = 3.Seconds()
            });
            return gun;
        }
        public static Module SmallShield(Ship ship)
        {
            Module shield = new(1, 2, "cell", 30, 0, 0, 0, 0, 0, 10, ship);
            shield.AddComponent(new Shield(20, 7.Cells(), 10, 200));
            return shield;
        }
        public static Module Junk(Ship ship)
        {
            Module junk = new(2,2,"cell",150,2,0,20,20,0,50,ship);
            junk.AddComponent(new JunkLauncher(3, 4, 3, 0.Seconds(), 100.Cells(), 10.CellsPerSecond(), 10));
            return junk;
        }
    }
}
