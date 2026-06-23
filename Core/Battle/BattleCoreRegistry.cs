using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Cthangover.Core.Battle
{
    public class BattleCoreRegistry
    {
        public static readonly BattleCoreRegistry Instance = new();

        private readonly Dictionary<string, Type> _cores = new();
        private string _activeCoreId;

        public void RegisterAssembly(Assembly assembly)
        {
            foreach (var type in assembly.GetTypes())
            {
                if (typeof(IBattleCore).IsAssignableFrom(type) && !type.IsAbstract)
                {
                    var instance = (IBattleCore)Activator.CreateInstance(type);
                    _cores[instance.Id] = type;
                }
            }
        }

        public void SetActive(string id)
        {
            if (!_cores.ContainsKey(id))
                throw new Exception($"Battle core '{id}' not found");
            _activeCoreId = id;
        }

        public IBattleCore GetActive()
        {
            if (_activeCoreId != null && _cores.ContainsKey(_activeCoreId))
                return (IBattleCore)Activator.CreateInstance(_cores[_activeCoreId]);

            var firstId = GetFirstCoreId();
            if (firstId != null)
            {
                _activeCoreId = firstId;
                return (IBattleCore)Activator.CreateInstance(_cores[firstId]);
            }

            throw new Exception("No active battle core set. Use battle.set_core first.");
        }

        public string GetFirstCoreId()
        {
            return _cores.Keys.FirstOrDefault();
        }

        public bool HasCore(string id)
        {
            return _cores.ContainsKey(id);
        }
    }
}
