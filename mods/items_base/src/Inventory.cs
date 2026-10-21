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
		/// <summary>Ordered slot list; order drives UI layout and empty slots are removed on zero stacks.</summary>
		public List<IItemContainer> Items { get; set; } = new();

		/// <summary>
		/// Stacks a loaded save names but no loaded mod defines (the mod was
		/// removed or the id renamed). They are no usable stacks — every
		/// container in <see cref="Items"/> has an item — but the items slot
		/// writes them back, so they return with their mod.
		/// </summary>
		public List<Settings.CItem> UnresolvedItems { get; } = new();
		/// <summary>Fires on every successful add/remove with (itemId, signed delta); also mirrored as <see cref="Events.InventoryChangedEvent"/> on the ModEventBus.</summary>
		public event Action<string, int> Change;

		/// <summary>
		/// Creates an inventory pre-seeded with the starter kit (10 wolf meat,
		/// 10 branches) so a new game always has basic supplies.
		/// </summary>
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

		/// <summary>True when any slot holds the given item (null-safe).</summary>
		public bool HasItem(IItem item)
		{
			if (item == null)
				return false;
			return HasItem(item.ID);
		}

		/// <summary>True when any slot holds the item with the given id; null/empty ids return false.</summary>
		public bool HasItem(string id)
		{
			if (string.IsNullOrWhiteSpace(id))
				return false;
			return Items.Any(o => o.Item != null && o.Item.ID == id);
		}

		/// <summary>Returns the slot holding the given item, or <c>null</c> (null-safe).</summary>
		public IItemContainer TryGet(IItem item)
		{
			if (item == null)
				return null;
			return TryGet(item.ID);
		}

		/// <summary>Returns the slot holding the given id, or <c>null</c> for null/empty ids.</summary>
		public IItemContainer TryGet(string id)
		{
			if (string.IsNullOrWhiteSpace(id))
				return null;
			return Items.FirstOrDefault(o => o.Item != null && o.Item.ID == id);
		}

		/// <summary>Total count across slots for the given item (0 when absent or null).</summary>
		public int CheckCount(IItem item)
		{
			if (item == null)
				return 0;
			return CheckCount(item.ID);
		}

		/// <summary>Total count across slots for the given id (0 when absent).</summary>
		public int CheckCount(string id)
		{
			var container = TryGet(id);
			return container?.Count ?? 0;
		}

		/// <summary>Adds the item (delegates to the id overload); null items are ignored.</summary>
		public void Add(IItem item, int count = 1)
		{
			if (item == null)
				return;
			Add(item.ID, count);
		}

		/// <summary>
		/// Stacks onto an existing slot or creates a new one by resolving the id
		/// through <see cref="Factories.Impls.ItemFactory"/>; unknown ids are
		/// silently ignored. Fires <see cref="Change"/> and publishes
		/// <see cref="Events.InventoryChangedEvent"/> on success.
		/// </summary>
		public void Add(string id, int count = 1)
		{
			if (count <= 0)
			{
				GameLogger.Log("INVENTORY", $"Add('{id}', {count}): count must be positive, ignored", LogLevel.Warning);
				return;
			}

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

		/// <summary>Removes the item (delegates to the id overload); null items return false.</summary>
		public bool Remove(IItem item, int count = 1)
		{
			if (item == null)
				return false;
			return Remove(item.ID, count);
		}

		/// <summary>
		/// Removes <paramref name="count"/> items from the slot, deleting the
		/// container when the stack reaches zero. Returns <c>false</c> without
		/// changes when the slot holds fewer items than requested.
		/// </summary>
		public bool Remove(string id, int count = 1)
		{
			// A negative count would pass the stock check and grow the stack.
			if (count <= 0)
				return false;

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
