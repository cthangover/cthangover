using System.Collections.Generic;
using Cthangover.Core.Items;

namespace Cthangover.Core.Factories.Impls
{
    /// <summary>
    /// Registry of item action behaviours, indexed by <c>ID</c>. It discovers
    /// nothing itself: C# actions of every mod arrive through
    /// <c>ItemActionConsumer</c> as the kernel registers their assembly and
    /// leave with their mod, and GDScript-declared actions arrive through
    /// <c>ItemActionBridge</c>. A mod therefore introduces a new item action
    /// simply by containing a public class that implements the interface.
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

        private readonly IDictionary<string, IItemAction> data = new Dictionary<string, IItemAction>();

        internal ItemActionFactory()
        {
        }

        /// <summary>Registers a single action under its <see cref="IItemAction.ID"/>; the first action of an id wins, null/empty ids are ignored.</summary>
        public void Register(IItemAction action)
        {
            if (action != null && !string.IsNullOrEmpty(action.ID) && !data.ContainsKey(action.ID))
                data[action.ID] = action;
        }

        /// <summary>
        /// Removes exactly <paramref name="action"/> (its mod is being unloaded);
        /// an id owned by another action instance is left alone.
        /// </summary>
        public void Unregister(IItemAction action)
        {
            if (action != null && !string.IsNullOrEmpty(action.ID) && data.TryGetValue(action.ID, out var registered) && registered == action)
                data.Remove(action.ID);
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
    }
}
