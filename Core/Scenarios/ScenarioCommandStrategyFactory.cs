using System.Collections.Generic;
using System.Reflection;
using Cthangover.Core.Utils;

namespace Cthangover.Core.Scenarios
{
    public static class ScenarioCommandStrategyFactory
    {
        private static readonly Dictionary<string, IScenarioCommandStrategy> Strategies;

        static ScenarioCommandStrategyFactory()
        {
            Strategies = new();
            foreach (var strategy in Reflections.FindAndCreate<IScenarioCommandStrategy>())
                Strategies[strategy.Command] = strategy;
        }

        public static IScenarioCommandStrategy Get(string command)
        {
            Strategies.TryGetValue(command, out var strategy);
            return strategy;
        }

        public static void RegisterAssembly(Assembly assembly)
        {
            foreach (var type in assembly.GetTypes())
            {
                if (typeof(IScenarioCommandStrategy).IsAssignableFrom(type) && !type.IsAbstract && type.IsPublic)
                {
                    var strategy = (IScenarioCommandStrategy)System.Activator.CreateInstance(type);
                    if (!Strategies.ContainsKey(strategy.Command))
                        Strategies[strategy.Command] = strategy;
                }
            }
        }
    }
}
