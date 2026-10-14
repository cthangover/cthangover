using System.Collections.Generic;
using System.Reflection;
using Cthangover.Core.Items;
using Cthangover.Core.Utils;

namespace Cthangover.Core.Factories.Impls
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
    /// <para/>
    /// The single instance lives in this mod assembly as a static lazy
    /// singleton; the static facade stays as the public API.
    /// </summary>
    public class ItemActionFactory
    {
        private static ItemActionFactory _instance;

        /// <summary>Static lazy singleton; kept as the public API.</summary>
        public static ItemActionFactory Instance => _instance ??= new ItemActionFactory();

		private readonly IDictionary<string, IItemAction> data;
		
		internal ItemActionFactory()
		{
			data = new Dictionary<string, IItemAction>();
			foreach (var action in Reflections.FindAndCreate<IItemAction>())
			{
				// The scan covers every loaded assembly, and mod assemblies are
				// never unloaded, so after a ModManager.Reload an implementation
				// can be discovered twice (stale + fresh assembly copy). The
				// first instance wins, matching RegisterAssembly's semantics.
				if (!string.IsNullOrEmpty(action.ID) && !data.ContainsKey(action.ID))
					data[action.ID] = action;
			}

			GameLogger.Log("ITEMS", "loaded '" + data.Count + "' item actions");
		}

		/// <summary>Manually registers a single action under its <see cref="IItemAction.ID"/>; duplicates and null/empty ids are ignored.</summary>
		public void Register(IItemAction action)
		{
			if (action != null && !string.IsNullOrEmpty(action.ID) && !data.ContainsKey(action.ID))
				data[action.ID] = action;
		}

		/// <summary>
		/// Resolves the behaviour registered under <paramref name="id"/>.
		/// The special id <c>"null"</c> (or an empty value) is an explicit
		/// no-action marker and returns <c>null</c>, as does an unknown id.
		/// </summary>
		public IItemAction Get(string id)
        {
            if (string.IsNullOrWhiteSpace(id) || id == "null")
                return null;
            return data[id];
        }

        /// <summary>Scans one assembly for <see cref="IItemAction"/> implementations and registers the new ones — used for hot-loaded mod assemblies.</summary>
        public static void RegisterAssembly(Assembly assembly)
        {
            var factory = Instance;
            foreach (var action in Reflections.FindAndCreate<IItemAction>(assembly))
            {
                if (!factory.data.ContainsKey(action.ID))
                    factory.data.Add(action.ID, action);
            }
        }

    }
}
