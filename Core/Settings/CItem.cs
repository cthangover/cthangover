using System;

namespace Cthangover.Core.Settings
{
    /// <summary>
    /// Lightweight serializable inventory entry representing a stack of a
    /// single item type. Stored inside <see cref="SavePackage"/> during save
    /// and restored into the full <see cref="Cthangover.Core.Items.IItemContainer"/>
    /// world objects on load. The <c>ID</c> is resolved against the items
    /// engine mod's <c>ItemFactory</c> to fetch the blueprint.
    /// </summary>
    [Serializable]
    public class CItem
    {
        /// <summary>Factory key for the item blueprint.</summary>
        public string ID { get; set; }
        /// <summary>Number of items in this stack.</summary>
        public int Count { get; set; }
    }
}
