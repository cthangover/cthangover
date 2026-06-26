using System;
using System.Collections.Generic;
using System.Reflection;
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Core.Items
{
    /// <summary>
    /// Reflection-based plugin registry for item action behaviours.
    /// On construction, scans every loaded assembly for <c>IItemAction</c>
    /// implementations via <c>Reflections.FindAndCreate</c> and indexes
    /// them by <c>ID</c>. This means a mod DLL can introduce a new item
    /// action simply by containing a public class that implements the
    /// interface — no registration code needed. <c>RegisterAssembly</c>
    /// handles the hot-reload case when a mod assembly is compiled or
    /// loaded at runtime.
    ///
    /// The <c>"null"</c> ID string is treated as a no-op action (the
    /// fallback for items that don't have an on-use effect), avoiding
    /// the need for a sentinel <c>NullItemAction</c> class.
    /// </summary>
    public class ItemActionFactory
    {
        
        private static Lazy<ItemActionFactory> instance = new(() => new ItemActionFactory());

        public static ItemActionFactory Instance => instance.Value;

        private readonly IDictionary<string, IItemAction> data;
		
        private ItemActionFactory()
        {
            data = new Dictionary<string, IItemAction>();
            foreach (var action in Reflections.FindAndCreate<IItemAction>())
                data.Add(action.ID, action);

            GameLogger.Log("ITEMS", "loaded '" + data.Count + "' item actions");
        }

        public IItemAction Get(string id)
        {
            if (string.IsNullOrWhiteSpace(id) || id == "null")
                return null;
            return data[id];
        }

        public static void RegisterAssembly(Assembly assembly)
        {
            foreach (var type in assembly.GetTypes())
            {
                if (typeof(IItemAction).IsAssignableFrom(type) && !type.IsAbstract && type.IsPublic)
                {
                    var action = (IItemAction)Activator.CreateInstance(type);
                    var factory = Instance;
                    if (!factory.data.ContainsKey(action.ID))
                        factory.data.Add(action.ID, action);
                }
            }
        }

    }
}
