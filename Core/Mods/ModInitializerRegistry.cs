using System;
using System.Collections.Generic;
using System.Reflection;

namespace Cthangover.Core.Mods
{
    public static class ModInitializerRegistry
    {
        private static readonly List<IModInitializer> _initializers = new();
        private static readonly List<string> _loadedModIds = new();

        public static void RegisterAssembly(Assembly assembly, string modId)
        {
            var newInitializers = new List<IModInitializer>();
            foreach (var type in assembly.GetTypes())
            {
                if (typeof(IModInitializer).IsAssignableFrom(type) && !type.IsAbstract)
                {
                    try
                    {
                        newInitializers.Add((IModInitializer)Activator.CreateInstance(type));
                    }
                    catch { }
                }
            }

            foreach (var init in _initializers)
            {
                try { init.OnModLoaded(modId); }
                catch { }
            }

            foreach (var loadedId in _loadedModIds)
            {
                foreach (var init in newInitializers)
                {
                    try { init.OnModLoaded(loadedId); }
                    catch { }
                }
            }

            _initializers.AddRange(newInitializers);
            _loadedModIds.Add(modId);
        }
    }
}
