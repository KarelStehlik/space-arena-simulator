using System;
using System.Collections.Generic;
using System.Reflection;

namespace SaSimulator
{
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

        /// <summary>
        /// creates a module with the given name from the ship's player's available modules.
        /// </summary>
        /// <param name="name">name of the module, eg. "Chaingun"</param>
        /// <param name="ship">ship on which to put the module. This ship's player determines the pool of available modules.</param>
        /// <returns></returns>
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

        /// <summary>
        /// All information about a module loaded from the modules file
        /// </summary>
        public class ModuleInfo()
        {
            public string name="";
            public ModuleBuff bonus=new(0,StatType.Health,ModuleTag.Any);
            public Dictionary<string, float> stats = [];
            public readonly List<ComponentInfo> components = [];
        }
    }
}
