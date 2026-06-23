using System;
using System.Collections.Generic;
using System.Reflection;
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Core.Items
{
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
