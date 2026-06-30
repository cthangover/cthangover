using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Cthangover.Core.Battle
{
    /// <summary>
    /// Pluggable battle-core registry. Scans assemblies for non-abstract
    /// IBattleCore implementations, registers them by ID (each core
    /// self-identifies), and activates one by ID. GetActive constructs a
    /// fresh instance every call — battle cores are stateless between
    /// battles. Falls back to the first registered core if no active
    /// ID was set, enabling mods to ship drop-in battle engines.
    /// </summary>
    public class BattleCoreRegistry
    {
        /// <summary>Singleton registry instance.</summary>
        public static readonly BattleCoreRegistry Instance = new();

        private readonly Dictionary<string, Type> _cores = new();
        private string _activeCoreId;

        /// <summary>
        /// Scans <paramref name="assembly"/> for non-abstract
        /// <see cref="IBattleCore"/> implementations, instantiates each
        /// to read its <see cref="IBattleCore.Id"/>, and registers the
        /// type by ID. Called at startup so mods can ship drop-in cores.
        /// </summary>
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

        /// <summary>
        /// Selects the active core by ID. Throws if the core is not
        /// registered. The selected core will be instantiated fresh on
        /// the next call to <see cref="GetActive"/>.
        /// </summary>
        public void SetActive(string id)
        {
            if (!_cores.ContainsKey(id))
                throw new Exception($"Battle core '{id}' not found");
            _activeCoreId = id;
        }

        /// <summary>
        /// Constructs a fresh instance of the active core type. Falls
        /// back to the first registered core if no active ID was set.
        /// Throws if no cores are registered at all.
        /// </summary>
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

        /// <summary>
        /// Returns the ID of the first registered core, or null if the
        /// registry is empty. Used as a fallback when no active core
        /// has been explicitly set.
        /// </summary>
        public string GetFirstCoreId()
        {
            return _cores.Keys.FirstOrDefault();
        }

        /// <summary>
        /// Checks whether a core with the given <paramref name="id"/>
        /// has been registered.
        /// </summary>
        public bool HasCore(string id)
        {
            return _cores.ContainsKey(id);
        }
    }
}
