using System;
using System.Collections.Generic;
using System.Linq;
using Cthangover.Core.Events;
using Cthangover.Core.Factories.Impls;
using Cthangover.Core.Utils;

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
	///
	/// The <c>Change</c> event fires with <c>(itemId, delta)</c> so
	/// subscribers receive full context. Additionally, each mutation
	/// publishes an <c>InventoryChangedEvent</c> to the global
	/// <c>ModEventBus</c>.
	/// </summary>
	public class Inventory : IInventory
	{
		public List<IItemContainer> Items { get; set; } = new();
		public event Action<string, int> Change;

		public Inventory()
		{
			AddStarterItem("food/wolf_meat", 10);
			AddStarterItem("resource/branches", 10);
		}

		/// <summary>
		/// Seeds one starter stack, skipping the entry when the item definition
		/// cannot be resolved. This can legitimately happen when the inventory is
		/// constructed while the launcher has not finished loading mods; adding a
		/// container with a <c>null</c> item would otherwise crash every later
		/// <see cref="HasItem(string)"/> call.
		/// </summary>
		private void AddStarterItem(string id, int count)
		{
			var item = ItemFactory.Instance.Get(id);
			if (item == null)
			{
				GameLogger.Log("INVENTORY", $"starter item '{id}' not found, skipping seed", LogLevel.Warning);
				return;
			}
			Items.Add(new ItemContainer { Item = item, Count = count });
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
				Change?.Invoke(id, count);
				PublishInventoryChanged(id, count, container.Count);
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
				Change?.Invoke(id, count);
				PublishInventoryChanged(id, count, count);
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

			var newCount = delta == 0 ? 0 : delta;
			Change?.Invoke(id, -count);
			PublishInventoryChanged(id, -count, newCount);
			return true;
		}

		private static void PublishInventoryChanged(string itemId, int delta, int newCount)
		{
			ModEventBus.Publish(new InventoryChangedEvent
			{
				ItemId = itemId,
				Delta = delta,
				NewCount = newCount,
			});
		}
	}
}
