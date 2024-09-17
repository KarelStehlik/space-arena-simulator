// Ignore Spelling: Chaingun

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using static SaSimulator.Physics;

namespace SaSimulator
{
    /// <summary>
    /// A way to narrow down modules into different (possibly overlapping) types.
    /// </summary>
    enum ModuleTag { Any, Armor, Weapon, Shield, Ballistic, Missile, Laser, RepairBay, Engine, Junk, PointDefense, Reactor }
    /// <summary>
    /// Stats that a module (or module component) may have, which can be modified with Module.ApplyBuff()
    /// </summary>
    enum StatType
    {
        Health, Damage, Armor, Reflect, Firerate, Mass, EnergyUse, EnergyGen, Range, WarpForce, RepairRate, MaxRepair,
        FiringArc, Thrust, TurnThrust, Strength, MaxRegen, RegenRate, ShieldRadius, ExplosionRadius, JunkHealth, AfterburnerThrust, AfterburnerTurning
    }
    // This represents a bonus to a specific stat on specific modules, such as "20% increased health of weapon modules"
    internal class ModuleBuff(float multiplier, StatType stat, ModuleTag targetModule)
    {
        public readonly StatType stat = stat;
        public float multiplier = multiplier;
        public readonly ModuleTag targetModule = targetModule;

        public static ModuleBuff operator *(ModuleBuff first, float other)
        {
            return new(other * first.multiplier, first.stat, first.targetModule);
        }
    }

    enum DamageType { Ballistics, Explosive, Laser };

    /// <summary>
    /// The building block of a ship. Modifies some of the ship's properties such as mass and energy, can have multiple [ModuleComponent]s which do various things.
    /// </summary>
    internal class Module(int width, int height, float maxHealth, float armor, float penetrationBlocking,
        float reflect, float energyUse, float energyGen, float mass, Ship ship) : GameObject(ship.game)
    {
        public readonly int height = height, width = width; // Width and height in cells
        public Transform relativePosition; // relative to ship center
        private float currentHealthFraction = 1;
        private Attribute<float> maxHealth = new(maxHealth);
        private Attribute<float> armor = new(armor); // reduces non-laser damage taken by a flat amount
        private Attribute<float> reflect = new(reflect); // reduces laser damage by a fraction
        private Attribute<float> energyUse = new(energyUse);
        private Attribute<float> energyGen = new(energyGen);
        private Attribute<float> mass = new(mass);
        public float EnergyUse { get { return energyUse; } }
        public float EnergyGen { get { return energyGen; } }
        public float MaxHealth { get { return maxHealth; } }
        public float Mass { get { return mass; } }
        private readonly float penetrationBlocking = penetrationBlocking; // reduces damage of penetrating weapons by a fraction after hitting this. This is 0 for most modules
        public readonly List<ModuleComponent> components = [];
        public readonly Ship ship = ship;
        public bool DePowered { get; private set; } = false;

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
                Size = new(height + .3f, width + .3f),
                Color = ship.side == 0 ? Color.Blue : Color.Red
            };
            outline.SetTransform(WorldPosition);
            outline.Draw(batch);
        }

        public override void Draw(SpriteBatch batch)
        {
            Sprite sprite = new("cell")
            {
                Size = new(height, width)
            };
            sprite.SetTransform(WorldPosition);
            sprite.Color = IsDestroyed ? Color.Black : new(1 - (float)currentHealthFraction, (float)currentHealthFraction, 0);
            if (DePowered && !IsDestroyed)
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
            if (DePowered || IsDestroyed)
            {
                return;
            }
            ship.energyUse += energyUse;
            ship.energy += energyGen;
        }

        public override void Tick(Time dt)
        {
            if (DePowered || IsDestroyed)
            {
                DePowered = false;
                return;
            }
            foreach (var component in components)
            {
                component.Tick(dt, this);
            }
        }

        // destroys this module
        public void Destroy()
        {
            IsDestroyed = true;
            bool isPower = energyGen > 0;
            bool isWeapon = false;
            foreach (var component in components)
            {
                component.OnDestroyed(this);
                isWeapon |= component.Tags().Contains(ModuleTag.Weapon);
            }
            ship.modulesAlive--;
            ship.weaponsAlive -= isWeapon ? 1 : 0;
            ship.energyModulesAlive -= isPower ? 1 : 0;
            ship.mass -= mass; // [speculative game mechanic] it is unknown whether ships lose mass when modules are destroyed.
        }

        // Return healing done
        public float Heal(float amount)
        {
            float newHealthFraction = Math.Min(1, currentHealthFraction + amount / maxHealth);
            float healingDone = (newHealthFraction - currentHealthFraction) * maxHealth;
            currentHealthFraction = newHealthFraction;
            return healingDone;
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
            bool isPower = energyGen > 0;
            bool isWeapon = false;

            foreach (var component in components)
            {
                component.Init(this);
                isWeapon |= component.Tags().Contains(ModuleTag.Weapon);
            }
            ship.weaponsAlive += isWeapon ? 1 : 0;
            ship.energyModulesAlive += isPower ? 1 : 0;
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
        public virtual void Tick(Time dt, Module thisModule) { }
        public virtual void Init(Module thisModule) { }
        public virtual void OnDestroyed(Module thisModule) { }
        public virtual void ApplyBuff(ModuleBuff buff, Module thisModule) { } // assumes the buff should affect this module and modifies one of the base module stats
        public virtual void Draw(SpriteBatch batch, Module thisModule) { }
    }

    internal class Modules
    {
        public class Armor() : ModuleComponent
        {
            public override ModuleTag[] Tags() => [ModuleTag.Armor];
        }

        public class Reactor() : ModuleComponent
        {
            public override ModuleTag[] Tags() => [ModuleTag.Reactor];
        }

        public class DeathExplode(float radius, float damage) : ModuleComponent
        {
            private readonly float radius=radius, damage=damage;
            public override void OnDestroyed(Module thisModule)
            {
                thisModule.ship.TakeAoeDamage(thisModule.WorldPosition.Position, radius.Cells()*1.05f, damage, DamageType.Explosive);
                // we slightly increase the radius to prevent rounding errors
            }
        }

        public class Engine(float thrust, float turning, float warp) : ModuleComponent
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

        // [speculative game mechanic] Afterburners are complete black magic in Space Arena.
        // in this simulator, each afterburner had a random 5-10 second cooldown
        public class Afterburner(float thrust, float turning, Time duration) : ModuleComponent
        {
            Attribute<float> thrust = new(thrust), turning = new(turning);
            Attribute<Time> duration = new(duration);
            Time currentPhaseRemainingDuration = 0.Seconds();
            bool active = true;

            public override ModuleTag[] Tags() => [ModuleTag.Engine];

            public override void ApplyBuff(ModuleBuff buff, Module thisModule)
            {
                switch (buff.stat)
                {
                    case StatType.AfterburnerThrust:
                        thrust.Increase += buff.multiplier;
                        break;
                    case StatType.AfterburnerTurning:
                        turning.Increase += buff.multiplier;
                        break;
                }
            }

            public override void Tick(Time dt, Module thisModule)
            {
                currentPhaseRemainingDuration -= dt;
                if (currentPhaseRemainingDuration.Seconds < 0)
                {
                    active = !active;
                    currentPhaseRemainingDuration = active ? duration : 5.Seconds() * (1 + (float)thisModule.game.rng.NextDouble());
                }
                if (active)
                {
                    thisModule.ship.afterburnerTurnPower += turning - 1;
                    thisModule.ship.afterburnerThrust += thrust - 1;
                }
            }
        }

        public class RepairBay(float maxRepair, float repairSpeed, int maxModules) : ModuleComponent
        {
            private float repairPartRemaining = 1;
            private Attribute<float> maxRepair = new(maxRepair);
            private Attribute<float> repairSpeed = new(repairSpeed);
            private int maxModules = maxModules;
            public override ModuleTag[] Tags() => [ModuleTag.RepairBay];

            public override void ApplyBuff(ModuleBuff buff, Module thisModule)
            {
                switch (buff.stat)
                {
                    case StatType.RepairRate:
                        repairSpeed.Increase += buff.multiplier;
                        break;
                    case StatType.MaxRepair:
                        maxRepair.Increase += buff.multiplier;
                        break;
                }
            }

            public override void Tick(Time dt, Module thisModule)
            {
                if (repairPartRemaining < 0)
                {
                    return;
                }
                float repairAmount = repairSpeed * dt.Seconds;
                int remainingRepairs = maxModules;
                foreach (Module module in thisModule.ship.modules)
                {
                    float healed = module.Heal(repairAmount);
                    if (healed > 0)
                    {
                        repairPartRemaining -= healed / maxRepair;
                        remainingRepairs--;
                        if (remainingRepairs <= 0)
                        {
                            return;
                        }
                    }
                }
            }
        }

        // [speculative game mechanic]. In this implementation, shields will protect all cells whose center they cover
        // from all damage, regardless of whether the damage actually passes through the shield before reaching the edge of the cell.
        // see this video: https://youtube.com/shorts/8J-nw48iT7A?feature=share
        // the front surface of my 2 chainguns was not covered by any shields, yet they were protected. They were both destroyed around the same time,
        // probably because the shields broke.
        public class Shield(float strength, Distance radius, float regenRate, float maxRegen) : ModuleComponent
        {
            private Attribute<float> strength = new(strength);
            private Attribute<Distance> radius = new(radius);
            public Distance Radius { get { return radius; } }
            private Attribute<float> regenRate = new(regenRate);
            private Attribute<float> maxRegen = new(maxRegen);
            private float regenRemainingFraction = 1, strengthRemainingFraction = 1;
            private bool mustReapply = false;
            private Time timeSinceDamageTaken = 10.Seconds();
            private Module? thisModule;

            public override ModuleTag[] Tags() => [ModuleTag.Shield];

            public override void ApplyBuff(ModuleBuff buff, Module thisModule)
            {
                switch (buff.stat)
                {
                    case StatType.Strength:
                        strength.Increase += buff.multiplier; break;
                    case StatType.ShieldRadius:
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
                return strengthRemainingFraction > 0 && thisModule!=null && !thisModule.DePowered && !thisModule.IsDestroyed;
            }

            public void TakeShieldDamage(float amount, DamageType type)
            {
                strengthRemainingFraction -= amount / strength;
                timeSinceDamageTaken = 0.Seconds();
            }

            public override void Init(Module thisModule)
            {
                this.thisModule ??= thisModule;
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
                if(thisModule == null || thisModule.DePowered || thisModule.IsDestroyed)
                {
                    return;
                }
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

        public class Gun(Distance range, float firingArc, float spread, float damage, float fireRate) : ModuleComponent
        {
            protected Attribute<Distance> range = new(range);
            public Distance Range { get { return range; } }
            protected float spread = spread.ToRadians();
            Attribute<float> firingArc = new(firingArc.ToRadians());
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
        public class BurstGun(float fireRate, float maxAmmo, int burstFireThreshold,
            Time burstFireInterval, Distance range, Speed bulletSpeed, float firingArc, float spread,
            float damage) : Gun(range, firingArc, spread, damage, fireRate)
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
                if (target == null)
                {
                    isBurstFire = false;
                }

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

        public class PenetratingGun(float fireRate, float maxAmmo, int burstFireThreshold,
            Time burstFireInterval, Distance range, Speed bulletSpeed, float firingArc, float spread,
            float damage, float penetration) :
            BurstGun(fireRate,maxAmmo,burstFireThreshold,burstFireInterval,range,bulletSpeed,firingArc,spread,damage)
        {
            Attribute<float> penetration = new(penetration);
            public override void Fire(Module thisModule)
            {
                thisModule.game.AddObject(
                        new PenetratingProjectile(thisModule.game, new(thisModule.WorldPosition.x, thisModule.WorldPosition.y, Aim(thisModule)),
                        speed, duration, damage, thisModule.ship.side, new(1, 1, .2f, .5f), penetration));
            }
        }

        public class MissileGun(float fireRate, float maxAmmo, int burstFireThreshold,
            Time burstFireInterval, Distance range, Speed bulletSpeed, float firingArc, float spread, float radius,
            float damage, float guidanceStrength, Time duration) :
            BurstGun(fireRate, maxAmmo, burstFireThreshold, burstFireInterval, range, bulletSpeed, firingArc, spread, damage)
        {
            Time dur = duration;
            protected Attribute<float> radius = new(radius);
            public override ModuleTag[] Tags() => [ModuleTag.Weapon, ModuleTag.Missile];

            public override void Init(Module thisModule)
            {
                duration = dur;
            }

            public override void Fire(Module thisModule)
            {
                thisModule.game.AddObject(
                        new Missile(thisModule.game, new(thisModule.WorldPosition.x, thisModule.WorldPosition.y, Aim(thisModule)),
                        speed, duration, radius, damage, thisModule.ship.side, Color.LightGray, GetTarget(thisModule), guidanceStrength));
            }

            public override void ApplyBuff(ModuleBuff buff, Module thisModule)
            {
                base.ApplyBuff(buff, thisModule);
                if (buff.stat == StatType.ExplosionRadius)
                {
                    radius.Increase += buff.multiplier;
                }
            }
        }

        public class TorpedoGun(float fireRate, float maxAmmo, int burstFireThreshold,
            Time burstFireInterval, Distance range, Speed bulletSpeed, float firingArc, float spread, float radius,
            float damage, Time duration) :
            MissileGun(fireRate, maxAmmo, burstFireThreshold, burstFireInterval, range, bulletSpeed, firingArc, spread, radius, damage, 0, duration)
        {
            public override void Fire(Module thisModule)
            {
                thisModule.game.AddObject(
                        new Torpedo(thisModule.game, new(thisModule.WorldPosition.x, thisModule.WorldPosition.y, Aim(thisModule)),
                        speed, duration, radius, damage, thisModule.ship.side, Color.LightGray));
            }
        }

        public class MineGun(float fireRate, float maxAmmo, int burstFireThreshold,
            Time burstFireInterval, Distance range, Speed bulletSpeed, float firingArc, float spread, float radius,
            float damage, Time duration) :
            MissileGun(fireRate, maxAmmo, burstFireThreshold, burstFireInterval, range, bulletSpeed, firingArc, spread, radius, damage, 0, duration)
        {
            public override void Fire(Module thisModule)
            {
                thisModule.game.AddObject(
                        new Mine(thisModule.game, new(thisModule.WorldPosition.x, thisModule.WorldPosition.y, Aim(thisModule)),
                        speed, duration, radius, damage, thisModule.ship.side, Color.LightGray));
            }
        }

        public class LaserGun(float fireRate, Time duration,
            Distance range, float firingArc,
            float damage) : Gun(range, firingArc, 0, damage, fireRate)
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

        public class JunkLauncher(float fireRate, float maxAmmo, int burstFireThreshold,
            Time burstFireInterval, Distance range, Speed bulletSpeed, float JunkHealth, Time duration) :
            BurstGun(fireRate, maxAmmo, burstFireThreshold, burstFireInterval, range, bulletSpeed, 2 * (float)Math.PI, 2 * (float)Math.PI, 0)
        {
            Attribute<float> health = new(JunkHealth);
            new Attribute<Time> duration = new(duration);

            public override ModuleTag[] Tags() => [ModuleTag.Junk];

            public override void Fire(Module thisModule)
            {
                thisModule.game.AddObject(new JunkPiece(thisModule.game, new(thisModule.WorldPosition.x, thisModule.WorldPosition.y, Aim(thisModule)), speed, duration, health, thisModule.ship.side));
            }

            public override void ApplyBuff(ModuleBuff buff, Module thisModule)
            {
                base.ApplyBuff(buff, thisModule);
                if (buff.stat == StatType.JunkHealth)
                {
                    health.Increase += buff.multiplier;
                }
            }
        }

        public class PointDefense(float fireRate, float missileInterceptChance, float torpedoInterceptChance, float mineInterceptChance, Distance range) : ModuleComponent
        {
            float sireRate = fireRate, missileChance = missileInterceptChance, torpedoChance = torpedoInterceptChance, mineChance = mineInterceptChance;
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
                loaded += sireRate * dt.Seconds;
                if (loaded >= 1)
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
                        if (target is Torpedo torpedo && thisModule.game.rng.NextDouble() < torpedoChance)
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
                        if (thisModule.game.hasGraphics && success)
                        {
                            thisModule.game.AddObject(new BulletTrail(thisModule.game, thisModule.WorldPosition.Position, target.WorldPosition.Position, Color.Green, 0.2f.Seconds()));
                        }
                        return;
                    }
                }
            }
        }

        public class ModuleBonus(string name, ModuleBuff buff) : ModuleComponent
        {
            public override void Init(Module thisModule)
            {
                thisModule.ship.ThisPlayer().AddModuleBonus(name, buff);
            }

            public override void OnDestroyed(Module thisModule)
            {
                thisModule.ship.ThisPlayer().RemoveModuleBonus(name);
            }
        }

        public class Debug() : ModuleComponent
        {
            public override void Tick(Time dt, Module thisModule)
            {
                Console.WriteLine($"module health: {thisModule.MaxHealth}");
            }
        }
    }
}
