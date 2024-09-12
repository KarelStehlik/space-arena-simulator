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
    enum ModuleTag { Any, Armor, Weapon, Shield, Ballistic, Missile, Laser, Energy, RepairBay, Engine, Junk, PointDefense }
    enum StatType
    {
        Health, Damage, Armor, Reflect, Firerate, Mass, EnergyUse, EnergyGen, Range, WarpForce,
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
        private float currentHealthFraction = 1;
        private Attribute<float> maxHealth;
        private Attribute<float> armor; // reduces non-laser damage taken by a flat amount
        private Attribute<float> reflect; // reduces laser damage by a fraction
        private Attribute<float> energyUse;
        private Attribute<float> energyGen;
        private Attribute<float> mass;
        public float EnergyUse { get { return energyUse; } }
        public float EnergyGen { get { return energyGen; } }
        public float Mass { get { return mass; } }
        private readonly float penetrationBlocking; // reduces damage of penetrating weapons by a fraction after hitting this. This is 0 for most modules
        public readonly List<ModuleComponent> components = [];
        public readonly Ship ship;
        public bool DePowered { get; private set; } = false;

        public Module(int height, int width, float maxHealth, float armor, float penetrationBlocking, float reflect, float energyUse, float energyGen, float mass, Ship ship) : base(ship.game)
        {
            this.width = width;
            this.height = height;
            this.armor = new(armor);
            this.penetrationBlocking = penetrationBlocking;
            this.ship = ship;
            this.reflect = new(reflect);
            this.maxHealth = new(maxHealth);
            this.energyUse = new(energyUse);
            this.energyGen = new(energyGen);
            this.mass = new(mass);
        }

        public void DePower() // disables this module's ticks and power generation until the next game tick
        {
            DePowered = true;
        }

        // return this
        public Module AddComponent(ModuleComponent component)
        {
            components.Add(component);
            return this;
        }

        public void DrawOutline(SpriteBatch batch)
        {
            Sprite outline = new("borderless_cell")
            {
                Size = new(width + .3f, height + .3f),
                Color = ship.side == 0 ? Color.Blue : Color.Red
            };
            outline.SetTransform(WorldPosition);
            outline.Draw(batch);
        }

        public override void Draw(SpriteBatch batch)
        {
            Sprite sprite = new("cell")
            {
                Size = new(width, height)
            };
            sprite.SetTransform(WorldPosition);
            sprite.Color = IsDestroyed ? Color.Black : new(1 - (float)currentHealthFraction, (float)currentHealthFraction, 0);
            if(DePowered && !IsDestroyed)
            {
                sprite.Color = Color.Yellow;
            }
            sprite.Draw(batch);
            foreach (var component in components)
            {
                component.Draw(batch, this);
            }
        }

        public void UpdatePosition()
        {
            WorldPosition = ship.WorldPosition + relativePosition;
        }

        public void ProcessEnergy()
        {
            if(DePowered)
            {
                return;
            }
            ship.energyUse += energyUse;
            ship.energy += energyGen;
        }

        public override void Tick(Time dt)
        { 
            if (IsDestroyed)
            {
                return;
            }
            if (DePowered)
            {
                DePowered = false;
                return;
            }
            foreach (var component in components)
            {
                component.Tick(dt, this);
            }
        }

        private void Destroy()
        {
            IsDestroyed = true;
            foreach (var component in components)
            {
                component.OnDestroyed(this);
            }
            ship.modulesAlive--;
            ship.mass -= mass; // [speculative game mechanic] it is unknown whether ships lose mass when modules are destroyed.
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
                Destroy();
            }
            return (amount - armor) * (1 - penetrationBlocking);
        }

        public void Initialize()
        {
            ship.mass += Mass;
            ship.modulesAlive++;
        }

        static readonly StatType[] BaseModuleStats = [StatType.Health, StatType.Health, StatType.Health, StatType.Health, StatType.Health];
        public void ApplyBuff(ModuleBuff buff)
        {
            if (buff.targetModule == ModuleTag.Any)
            {
                ApplyBuffUnchecked(buff);
                return;
            }

            foreach (ModuleComponent component in components)
            {
                if (component.Tags().Contains(buff.targetModule))
                {
                    if (BaseModuleStats.Contains(buff.stat)) // buff matches one of the component's tags but applies to basic stats
                    {
                        ApplyBuffUnchecked(buff);
                        return;
                    }
                    component.ApplyBuff(buff, this); // buff matches one of the component's tags and applies to that component's stats
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
                case StatType.EnergyGen:
                    energyGen.Increase += buff.multiplier;
                    break;
                case StatType.EnergyUse:
                    energyUse.Increase += buff.multiplier;
                    break;
                case StatType.Mass:
                    ship.mass -= mass;
                    mass.Increase += buff.multiplier;
                    ship.mass += mass;
                    break;
            }
        }
    }

    internal class ModuleComponent
    {
        public virtual ModuleTag[] Tags() { return []; }
        public virtual void Tick(Time dt, Module thisModule){ }
        public virtual void OnDestroyed(Module module){ }
        public virtual void ApplyBuff(ModuleBuff buff, Module thisModule){ } // assumes the buff should affect this module and modifies one of the base module stats
        public virtual void Draw(SpriteBatch batch, Module thisModule){ }
    }

    internal class Engine(float thrust, float turning, float warp) : ModuleComponent
    {
        Attribute<float> thrust = new(thrust), turning = new(turning), warp = new(warp);

        public override ModuleTag[] Tags() => [ModuleTag.Engine];

        public override void ApplyBuff(ModuleBuff buff, Module thisModule)
        {
            switch (buff.stat)
            {
                case StatType.Thrust:
                    thrust.Increase += buff.multiplier;
                    break;
                case StatType.TurnThrust:
                    turning.Increase += buff.multiplier;
                    break;
                case StatType.WarpForce:
                    warp.Increase += buff.multiplier;
                    break;
            }
        }

        public override void Tick(Time dt, Module thisModule)
        {
            thisModule.ship.warpForce += warp;
            thisModule.ship.turnPower += turning;
            thisModule.ship.thrust += thrust;
        }
    }

    // [speculative game mechanic]. In this implementation, shields will protect all cells whose center they cover
    // from all damage, regardless of whether the damage actually passes through the shield before reaching the edge of the cell.
    // see this video: https://youtube.com/shorts/8J-nw48iT7A?feature=share
    // the front surface of my 2 chainguns was not covered by any shields, yet they were protected. They were both destroyed around the same time,
    // probably because the shields broke.
    internal class Shield(float strength, Distance radius, float regenRate, float maxRegen) : ModuleComponent
    {
        private Attribute<float> strength = new(strength);
        private Attribute<Distance> radius = new(radius);
        public Distance Radius { get { return radius; } }
        private Attribute<float> regenRate = new(regenRate);
        private Attribute<float> maxRegen = new(maxRegen);
        private float regenRemainingFraction = 1, strengthRemainingFraction = 1;
        private bool mustReapply = false;
        private Time timeSinceDamageTaken = 10.Seconds();

        public override ModuleTag[] Tags() => [ModuleTag.Shield];

        public override void ApplyBuff(ModuleBuff buff, Module thisModule)
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

        public override void Tick(Time dt, Module thisModule)
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

        public override void Draw(SpriteBatch batch, Module thisModule)
        {
            Sprite sprite = new("shield")
                {
                    Size = new Vector2(Radius.Cells * 2, Radius.Cells * 2)
                };
            sprite.Position = thisModule.WorldPosition.Position;
            float opacity = Math.Max(0, 1f - timeSinceDamageTaken.Seconds);
            sprite.Color = new(opacity, opacity, opacity, opacity);
            sprite.Draw(batch);
        }
    }

    internal class Gun(Distance range, float firingArc, float spread, float damage, float fireRate) : ModuleComponent
    {
        protected Attribute<Distance> range = new(range);
        public Distance Range { get { return range; } }
        protected float spread = spread;
        Attribute<float> firingArc = new(firingArc);
        protected Attribute<float> damage = new(damage);
        protected Attribute<float> fireRate = new(fireRate);
        private Ship? target;

        public override ModuleTag[] Tags() => [ModuleTag.Weapon];

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

        public override void ApplyBuff(ModuleBuff buff, Module thisModule)
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

        public override ModuleTag[] Tags() => [ModuleTag.Weapon, ModuleTag.Ballistic];

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
        public override ModuleTag[] Tags() => [ModuleTag.Weapon, ModuleTag.Missile];

        public override void Fire(Module thisModule)
        {
            thisModule.game.AddObject(
                    new Missile(thisModule.game, new(thisModule.WorldPosition.x, thisModule.WorldPosition.y, Aim(thisModule)),
                    speed, duration, radius, damage, thisModule.ship.side, Color.LightGray, GetTarget(thisModule), guidanceStrength));
        }

        public override void ApplyBuff(ModuleBuff buff, Module thisModule)
        {
            base.ApplyBuff(buff,thisModule);
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

        public override ModuleTag[] Tags() => [ModuleTag.Weapon, ModuleTag.Laser];

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

        public override ModuleTag[] Tags() => [ModuleTag.Junk];

        public override void Fire(Module thisModule)
        {
            thisModule.game.AddObject(new JunkPiece(thisModule.game, new(thisModule.WorldPosition.x, thisModule.WorldPosition.y, Aim(thisModule)), speed, duration, health,thisModule.ship.side));
        }

        public override void ApplyBuff(ModuleBuff buff, Module thisModule)
        {
            base.ApplyBuff(buff,thisModule);
            if (buff.stat == StatType.JunkHealth)
            {
                junkHealth.Increase += buff.multiplier;
            }
        }
    }

    internal class PointDefense(float firerate, float missileInterceptChance, float torpedoInterceptChance, float mineInterceptChance, Distance range) : ModuleComponent
    {
        float firerate = firerate, missileChance = missileInterceptChance, torpedoChance = torpedoInterceptChance, mineChance = mineInterceptChance;
        float loaded = 0;
        Attribute<Distance> range = new(range);

        public override ModuleTag[] Tags() => [ModuleTag.PointDefense];

        public override void ApplyBuff(ModuleBuff buff, Module thisModule)
        {
            if (buff.stat == StatType.Range)
            {
                range.Increase += buff.multiplier;
            }
        }


        public override void Tick(Time dt, Module thisModule)
        {
            loaded += firerate * dt.Seconds;
            if(loaded >= 1)
            {
                loaded = 0; // [speculative game mechanic] it is known that point defense is less effective at low frame rates.
                            // this is one possible way to simulate that - it can shoot at most once per game tick.

                // fire
                UniformGrid targets = thisModule.ship.side == 0 ? thisModule.game.missilesP1 : thisModule.game.missilesP0;
                foreach (var target in targets.Get(thisModule.WorldPosition.x, thisModule.WorldPosition.y, range))
                {
                    // we find the first non-destroyed missile, try to shoot it down, then we're done and return.
                    if (target.IsDestroyed)
                    {
                        continue;
                    }
                    bool success = false;
                    if (target is Torpedo torpedo && thisModule.game.rng.NextDouble()<torpedoChance)
                    {
                        torpedo.ShootDown();
                        success = true;
                    }
                    else if (target is Mine mine && thisModule.game.rng.NextDouble() < mineChance)
                    {
                        mine.ShootDown();
                        success = true;
                    }
                    else if (target is Missile missile && thisModule.game.rng.NextDouble() < missileChance)
                    {
                        missile.ShootDown();
                        success = true;
                    }
                    if(thisModule.game.hasGraphics && success)
                    {
                        thisModule.game.AddObject(new BulletTrail(thisModule.game, thisModule.WorldPosition.Position, target.WorldPosition.Position, Color.Green, 0.2f.Seconds()));
                    }
                    return;
                }
            }
        }
    }

    internal static class Modules
    {
        public static Module SmallSteelArmor(Ship ship)
        {
            return new(1, 1, 145, 3, 0, .55f, 0, 0, 10, ship);
        }
        public static Module SmallReactor(Ship ship)
        {
            return new(1, 1, 10, 3, 0, .55f, 0, 50, 10, ship);
        }
        public static Module MediumSteelArmor(Ship ship)
        {
            return new(2, 2, 550, 4, 0, .55f, 0, 0, 10, ship);
        }
        public static Module Chaingun(Ship ship)
        {
            Module gun = new(1, 1, 15, 0, 0, 0, 0, 0, 10, ship);
            gun.AddComponent(new BurstGun(3.3333f, 1, 1, 0.Seconds(), 35.Cells(), 200.CellsPerSecond(), 70f.ToRadians(), 0.05f, 4));
            return gun;
        }
        public static Module SmallLaser(Ship ship)
        {
            Module gun = new(1, 1, 15, 0, 0, 0, 0, 0, 10, ship);
            gun.AddComponent(new LaserGun(0.5f, 2.Seconds(), 100.Cells(), 360f.ToRadians(), 10));
            return gun;
        }
        public static Module SmallMissile(Ship ship)
        {
            Module gun = new(1, 2, 30, 0, 0, 0, 0, 0, 10, ship);
            gun.AddComponent(new MissileGun(3.33333f, 1, 1, 0.Seconds(), 100.Cells(), 50.CellsPerSecond(), 70f.ToRadians(), 90f.ToRadians(), 2, 4, 2f)
            {
                duration = 3.Seconds()
            });
            return gun;
        }
        public static Module SmallShield(Ship ship)
        {
            Module shield = new(1, 2, 30, 0, 0, 0, 0, 0, 10, ship);
            shield.AddComponent(new Shield(20, 7.Cells(), 10, 200));
            return shield;
        }
        public static Module Junk(Ship ship)
        {
            Module junk = new(2,2,150,2,0,20,20,0,50,ship);
            junk.AddComponent(new JunkLauncher(3, 4, 3, 0.Seconds(), 100.Cells(), 10.CellsPerSecond(), 10));
            return junk;
        }
        public static Module PointDefense(Ship ship)
        {
            Module pdt = new(2, 2, 150, 2, 0, 20, 20, 0, 50, ship);
            pdt.AddComponent(new PointDefense(10,.5f,.5f,.5f,19.Cells()));
            return pdt;
        }
    }
}
