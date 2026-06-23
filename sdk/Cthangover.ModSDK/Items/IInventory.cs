using System;
using System.Collections.Generic;

namespace Cthangover.Core.Items
{
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
