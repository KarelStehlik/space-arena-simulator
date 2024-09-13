using System;
using System.Collections.Generic;
using System.Reflection;

namespace SaSimulator
{
    public class Test1
    {
        public static void foo() { }
        public class Test2 { }
    }

    internal class ModuleCreation
    {
        private static object?[] ParseArguments(Type typeToConstruct, Dictionary<string, float> parameters, Ship ship)
        {
            ParameterInfo[] parameterInfo = typeToConstruct.GetConstructors()[0].GetParameters();
            string name = typeToConstruct.Name;
            object?[] processedParams = new object?[parameterInfo.Length];
            for (int i = 0; i < parameterInfo.Length; i++)
            {
                float paramValue = parameters.GetValueOrDefault(parameterInfo[i].Name ?? "", float.PositiveInfinity);
                if (paramValue == float.PositiveInfinity)
                {
                    paramValue = 0;
                    if (parameterInfo[i].ParameterType != typeof(Ship))
                    {
                        Console.WriteLine($"Warning: parameter {parameterInfo[i].Name} for module {name} is unfilled");
                    }
                }
                Type paramType = parameterInfo[i].ParameterType;
                if (paramType == typeof(float))
                {
                    processedParams[i] = paramValue;
                }
                else if (paramType == typeof(int))
                {
                    processedParams[i] = (int)paramValue;
                }
                else if (paramType == typeof(Distance))
                {
                    processedParams[i] = paramValue.Cells();
                }
                else if (paramType == typeof(Time))
                {
                    processedParams[i] = paramValue.Seconds();
                }
                else if (paramType == typeof(Speed))
                {
                    processedParams[i] = paramValue.CellsPerSecond();
                }
                else if (paramType == typeof(Ship))
                {
                    processedParams[i] = ship;
                }
                else
                {
                    throw new Exception($"Parameter {i} of module component {name} has problematic type {paramType}");
                }

            }
            return processedParams;
        }

        private static Module CreateModule(Dictionary<string, float> parameters, Ship ship)
        {
            Type type = typeof(Module);
            return Activator.CreateInstance(type, ParseArguments(type, parameters, ship)) as Module ?? throw new Exception($"Unable to create base module for unknown reason");
        }

        private static ModuleComponent CreateComponent(string name, Dictionary<string, float> parameters, Ship ship)
        {
            Type type = name=="Module"? typeof(Module) : typeof(Modules).GetNestedType(name) ?? throw new ArgumentException($"No such module component: {name}");
            return Activator.CreateInstance(type, ParseArguments(type, parameters, ship)) as ModuleComponent ?? throw new Exception($"Unable to create module component \"{name}\" for unknown reason");
        }

        private static Module CreateFullModule(ModuleInfo info, Ship ship)
        {
            Module m = CreateModule(info.stats, ship);
            foreach(ComponentInfo ci in info.components)
            {
                m.AddComponent(CreateComponent(ci.componentName, ci.parameters, ship));
            }
            m.AddComponent(new Modules.ModuleBonus(info.name, info.bonus));
            return m;
        }

        public static Module Create(string name, Ship ship)
        {
            try
            {
                return CreateFullModule(ship.ThisPlayer().possibleModules[name], ship);
            }catch (KeyNotFoundException)
            {
                Console.WriteLine($"no such module: {name}");
                throw;
            }
        }

        public class ComponentInfo()
        {
            public string componentName = "";
            public Dictionary<string, float> parameters = [];
        }

        public class ModuleInfo()
        {
            public string name="";
            public ModuleBuff bonus=new(0,StatType.Health,ModuleTag.Any);
            public Dictionary<string, float> stats = [];
            public readonly List<ComponentInfo> components = [];
        }

        public static Module Debug(Ship ship)
        {
            Module m = new(1, 1, 100, 3, 0, .55f, 0, 0, 10, ship);
            m.AddComponent(new Modules.Debug());
            return m;
        }
        public static Module Bonus(Ship ship)
        {
            Module m = new(1, 1, 10, 3, 0, .55f, 0, 10, 10, ship);
            m.AddComponent(new Modules.ModuleBonus("bonus", new(2, StatType.Health, ModuleTag.Any)));
            return m;
        }
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
            Module gun = CreateModule(new Dictionary<string, float>()
            {
                ["height"]=1,
                ["width"] =1,
                ["maxHealth"] =15,
                ["armor"] =0,
                ["penetrationBlocking"] =0,
                ["reflect"] =0,
                ["energyUse"] =5,
                ["energyGen"] =0,
                ["mass"] =10,
            }, ship);
            //gun.AddComponent(new Modules.BurstGun(3.3333f, 1, 1, 0.Seconds(), 35.Cells(), 200.CellsPerSecond(), 70f.ToRadians(), 0.05f, 4));
            gun.AddComponent(CreateComponent("BurstGun", new Dictionary<string, float>()
            {
                ["fireRate"] = 3.3333f,
                ["maxAmmo"] = 1f,
                ["burstFireThreshold"] = 1f,
                ["burstFireInterval"] = 0f,
                ["range"] = 35f,
                ["bulletSpeed"] = 200f,
                ["firingArc"] = 70f.ToRadians(),
                ["spread"] = 0.05f,
                ["damage"] = 4f,
            }, ship));
            return gun;
        }
        public static Module SmallLaser(Ship ship)
        {
            Module gun = new(1, 1, 15, 0, 0, 0, 0, 0, 10, ship);
            gun.AddComponent(new Modules.LaserGun(0.5f, 2.Seconds(), 100.Cells(), 360f.ToRadians(), 10));
            return gun;
        }
        public static Module SmallMissile(Ship ship)
        {
            Module gun = new(1, 2, 30, 0, 0, 0, 0, 0, 10, ship);
            gun.AddComponent(new Modules.MissileGun(3.33333f, 1, 1, 0.Seconds(), 100.Cells(), 50.CellsPerSecond(), 70f.ToRadians(), 90f.ToRadians(), 2, 4, 2f,3.Seconds()));
            return gun;
        }
        public static Module SmallShield(Ship ship)
        {
            Module shield = new(1, 2, 30, 0, 0, 0, 0, 0, 10, ship);
            shield.AddComponent(new Modules.Shield(20, 7.Cells(), 10, 200));
            return shield;
        }
        public static Module Junk(Ship ship)
        {
            Module junk = new(2, 2, 150, 2, 0, 20, 20, 0, 50, ship);
            junk.AddComponent(new Modules.JunkLauncher(3, 4, 3, 0.Seconds(), 100.Cells(), 10.CellsPerSecond(), 10));
            return junk;
        }
        public static Module PointDefense(Ship ship)
        {
            Module pdt = new(2, 2, 150, 2, 0, 20, 20, 0, 50, ship);
            pdt.AddComponent(new Modules.PointDefense(10, .5f, .5f, .5f, 19.Cells()));
            return pdt;
        }
    }
}
