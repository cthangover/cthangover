using System;
using System.Collections.Generic;

namespace Cthangover.Core.Items
{
    /// <summary>
    /// Reactive inventory contract. Items are stored as an ordered list of
    /// <c>IItemContainer</c> slots rather than a <c>Dictionary&lt;IItem, int&gt;</c>
    /// because slot order determines UI layout and each slot needs to be
    /// independently observable by the inventory renderer. The
    /// <c>Change</c> event fires on every add or remove so that UI panels
    /// can re-render without polling.
    ///
    /// All mutating methods accept both <c>IItem</c> and <c>string id</c>
    /// overloads — the <c>string</c> variants delegate to
    /// <c>ItemFactory.Instance.Get</c> internally, so callers passing raw
    /// IDs (e.g. from quest scripts or save data) don't need to resolve
    /// items themselves.
    /// </summary>
    public interface IInventory
    {
        /// <summary>Ordered slot list — order drives UI layout; empty slots are deleted on zero stacks.</summary>
        List<IItemContainer> Items { get; set; }
        /// <summary>Fires on every successful mutation with (itemId, signed delta).</summary>
        event Action<string, int> Change;
        /// <summary>True when any slot holds the item.</summary>
        public bool HasItem(IItem item);
        /// <summary>True when any slot holds the item with the given id.</summary>
        public bool HasItem(string id);
        /// <summary>Returns the slot holding the item, or <c>null</c>.</summary>
        public IItemContainer TryGet(IItem item);
        /// <summary>Returns the slot holding the given id, or <c>null</c>.</summary>
        public IItemContainer TryGet(string id);
        /// <summary>Total count across slots (0 if absent).</summary>
        public int CheckCount(IItem item);
        /// <summary>Total count across slots for the given id (0 if absent).</summary>
        public int CheckCount(string id);
        /// <summary>Stacks onto an existing slot or creates one by resolving the item reference.</summary>
        public void Add(IItem item, int count = 1);
        /// <summary>Stacks onto an existing slot or creates one via <c>ItemFactory</c>; unknown ids are silently ignored.</summary>
        public void Add(string id, int count = 1);
        /// <summary>Removes items; returns <c>false</c> without changes when the slot holds fewer than <paramref name="count"/>.</summary>
        public bool Remove(IItem item, int count = 1);
        /// <summary>Removes items by id; returns <c>false</c> without changes when the slot holds fewer than <paramref name="count"/>.</summary>
        public bool Remove(string id, int count = 1);
    }
}
