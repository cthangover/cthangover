using Godot;

namespace Cthangover.Core.Items
{
	[GlobalClass]
	public partial class GDInventoryService : RefCounted
	{
		internal IInventory Inner;

		public bool HasItem(string id) => Inner.HasItem(id);

		public bool HasItemByItem(GDItem item)
			=> item?.Inner != null && Inner.HasItem(item.Inner);

		public int CheckCount(string id) => Inner.CheckCount(id);

		public int CheckCountByItem(GDItem item)
			=> item?.Inner != null ? Inner.CheckCount(item.Inner) : 0;

		public void Add(string id, int count = 1) => Inner.Add(id, count);

		public void AddByItem(GDItem item, int count = 1)
		{
			if (item?.Inner != null)
				Inner.Add(item.Inner, count);
		}

		public bool Remove(string id, int count = 1) => Inner.Remove(id, count);

		public bool RemoveByItem(GDItem item, int count = 1)
			=> item?.Inner != null && Inner.Remove(item.Inner, count);
	}
}
