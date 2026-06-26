using System;
using System.Collections.Generic;
using System.Linq;
using Cthangover.Core.Factories.Impls;

namespace Cthangover.Core.Items
{
	/// <summary>
	/// Concrete player inventory backed by an ordered <c>List&lt;IItemContainer&gt;</c>.
	/// Uses LINQ scans (<c>FirstOrDefault</c>, <c>Any</c>) rather than a
	/// <c>Dictionary</c> because inventory sizes in this game are small
	/// enough that O(n) lookups are imperceptible, and list ordering
	/// directly drives UI slot layout — the first container appears in
	/// the first grid cell.
	///
	/// On <c>Remove</c>, when a stack count reaches zero the entire
	/// container is deleted from the list rather than kept as an empty
	/// slot, preventing the UI from rendering gaps. The constructor seeds
	/// wolf meat and branches for testing convenience — in production,
	/// save data would override this initial state.
	/// </summary>
	public class Inventory : IInventory
	{
		public List<IItemContainer> Items { get; set; } = new();
		public event Action Change;

		public Inventory()
		{
			Items.Add(new ItemContainer { Item = ItemFactory.Instance.Get("food/wolf_meat"),    Count = 10});
			Items.Add(new ItemContainer { Item = ItemFactory.Instance.Get("resource/branches"), Count = 10});
		}
		
		public bool HasItem(IItem item)
		{
			if (item == null)
				return false;
			return HasItem(item.ID);
		}

		public bool HasItem(string id)
		{
			if (string.IsNullOrWhiteSpace(id))
				return false;
			return Items.Any(o => o.Item.ID == id);
		}

		public IItemContainer TryGet(IItem item)
		{
			if (item == null)
				return null;
			return TryGet(item.ID);
		}
		
		public IItemContainer TryGet(string id)
		{
			if (string.IsNullOrWhiteSpace(id))
				return null;
			return Items.FirstOrDefault(o => o.Item != null && o.Item.ID == id);
		}

		public int CheckCount(IItem item)
		{
			if (item == null)
				return 0;
			return CheckCount(item.ID);
		}

		public int CheckCount(string id)
		{
			var container = TryGet(id);
			return container?.Count ?? 0;
		}
		
		public void Add(IItem item, int count = 1)
		{
			if (item == null)
				return;
			Add(item.ID, count);
		}
		
		public void Add(string id, int count = 1)
		{
			var container = TryGet(id);
			if (container != null)
			{
				container.Count += count;
				Change?.Invoke();
				return;
			}
			var item = ItemFactory.Instance.Get(id);
			if (item != null)
			{
				container = new ItemContainer
				{
					Item = item,
					Count = count,
				};
				Items.Add(container);
				Change?.Invoke();
			}
		}
		
		public bool Remove(IItem item, int count = 1)
		{
			if (item == null)
				return false;
			return Remove(item.ID, count);
		}

		public bool Remove(string id, int count = 1)
		{
			var container = TryGet(id);
			if (container == null)
				return false;

			var delta = container.Count - count;
			if (delta < 0)
				return false;
			
			if (delta == 0)
				Items.Remove(container);
			else
				container.Count -= count;
			
			Change?.Invoke();
			return true;
		}
		
	}
}
