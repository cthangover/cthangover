using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Cthangover.Core.Utils;

namespace Cthangover.Core.Actions
{
    public class ScenarioActionFactory
    {
        private static readonly Lazy<ScenarioActionFactory> instance = new(() => new ScenarioActionFactory());

        public static ScenarioActionFactory Instance => instance.Value;

        private readonly Dictionary<string, IScenarioAction> actions = new();

        private ScenarioActionFactory()
        {
            foreach (var action in Reflections.FindAndCreate<IScenarioAction>())
            {
                Register(action);
            }

            GameLogger.Log("FACTORY", $"loaded '{actions.Count}' scenario actions");
        }

        public IScenarioAction Get(string name)
        {
            if (actions.TryGetValue(name, out var action))
                return action;

            GameLogger.Log("FACTORY", $"ScenarioAction '{name}' not found", LogLevel.Error);
            return null;
        }

        public void Register(IScenarioAction action)
        {
            if (actions.ContainsKey(action.Name))
                return;
            
            actions.Add(action.Name, action);
            GameLogger.Log("FACTORY", $"registered scenario action '{action.Name}'");
        }

        public void RegisterAssembly(Assembly assembly)
        {
            foreach (var type in assembly.GetTypes())
            {
                if (typeof(IScenarioAction).IsAssignableFrom(type) && !type.IsAbstract)
                {
                    var action = (IScenarioAction)Activator.CreateInstance(type);
                    Register(action);
                }
            }
        }

        public List<IScenarioAction> GetAll() => actions.Values.ToList();
    }
}
