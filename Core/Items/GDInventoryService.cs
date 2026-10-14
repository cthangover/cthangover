using Godot;

namespace Cthangover.Core.Items
{
	/// <summary>
	/// GDScript-facing wrapper around the player inventory
	/// (<see cref="IInventory"/>). Godot scripts cannot see the managed
	/// interface, so the engine hands them this <c>[GlobalClass]</c>
	/// <c>RefCounted</c> instead. Plain-id methods mirror the C# API; the
	/// <c>*ByItem</c> variants accept a <see cref="GDItem"/> snapshot (as
	/// passed to a script's <c>use(item)</c> callback) and unwrap it back to
	/// the managed item, so script code never touches ids it does not know.
	/// </summary>
	[GlobalClass]
	public partial class GDInventoryService : RefCounted
	{
		/// <summary>The wrapped player inventory; assigned by the engine when the service is created.</summary>
		internal IInventory Inner;

		/// <summary>True when any slot holds the item with the given id.</summary>
		public bool HasItem(string id) => Inner.HasItem(id);

		/// <summary>True when any slot holds the wrapped item.</summary>
		public bool HasItemByItem(GDItem item)
			=> item?.Inner != null && Inner.HasItem(item.Inner);

		/// <summary>Total stack size across slots for the given item id (0 if absent).</summary>
		public int CheckCount(string id) => Inner.CheckCount(id);

		/// <summary>Total stack size for the wrapped item (0 for a null wrapper).</summary>
		public int CheckCountByItem(GDItem item)
			=> item?.Inner != null ? Inner.CheckCount(item.Inner) : 0;

		/// <summary>Adds items by id; unknown ids are silently ignored by the inner inventory.</summary>
		public void Add(string id, int count = 1) => Inner.Add(id, count);

		/// <summary>Adds the wrapped item; a null wrapper is a no-op.</summary>
		public void AddByItem(GDItem item, int count = 1)
		{
			if (item?.Inner != null)
				Inner.Add(item.Inner, count);
		}

		/// <summary>Removes items by id; returns false when the slot holds fewer than <paramref name="count"/>.</summary>
		public bool Remove(string id, int count = 1) => Inner.Remove(id, count);

		/// <summary>Removes the wrapped item; returns false for a null wrapper.</summary>
		public bool RemoveByItem(GDItem item, int count = 1)
			=> item?.Inner != null && Inner.Remove(item.Inner, count);
	}
}
