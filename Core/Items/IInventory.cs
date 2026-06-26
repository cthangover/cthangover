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
        List<IItemContainer> Items { get; set; }
        event Action Change;
        public bool HasItem(IItem item);
        public bool HasItem(string id);
        public IItemContainer TryGet(IItem item);
        public IItemContainer TryGet(string id);
        public int CheckCount(IItem item);
        public int CheckCount(string id);
        public void Add(IItem item, int count = 1);
        public void Add(string id, int count = 1);
        public bool Remove(IItem item, int count = 1);
        public bool Remove(string id, int count = 1);
    }
}
