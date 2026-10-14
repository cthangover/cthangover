using Godot;

namespace Cthangover.Core.Items
{
	/// <summary>
	/// GDScript-facing snapshot of an <see cref="IItem"/>. Godot scripts cannot
	/// implement or inspect the managed <c>IItem</c> interface, so the engine
	/// wraps it in this <c>[GlobalClass]</c> <c>RefCounted</c> with plain fields
	/// before handing it to a GDScript's <c>use(item)</c> callback (see
	/// <see cref="GDItemAction"/>). <see cref="Inner"/> keeps the real item so
	/// the bridge can resolve it back for inventory calls; <c>Sprite</c> and
	/// <c>Properties</c> are deliberately not exposed — scripts work with ids
	/// and plain data only.
	/// </summary>
	[GlobalClass]
	public partial class GDItem : RefCounted
	{
		/// <summary>Item identifier (path-style id, e.g. <c>"food/ration"</c>).</summary>
		public string Id;
		/// <summary>Display name.</summary>
		public string Name;
		/// <summary>Tooltip/description text.</summary>
		public string Description;
		/// <summary>Purchase price.</summary>
		public int Cost;
		/// <summary><see cref="ItemType"/> flags as a plain int (flags survive the bridge better than enums).</summary>
		public int ItemType;

		/// <summary>Back-reference to the wrapped managed item; internal by design.</summary>
		internal IItem Inner;

		/// <summary>
		/// Wraps a managed item into the GDScript snapshot. Returns an empty
		/// wrapper (null fields) for a <c>null</c> input rather than throwing,
		/// so a missing item definition degrades to a harmless no-op in script
		/// code instead of crashing the bridge.
		/// </summary>
		public static GDItem From(IItem item)
		{
			var gd = new GDItem();
			if (item == null)
				return gd;

			gd.Id = item.ID;
			gd.Name = item.Name;
			gd.Description = item.Description;
			gd.Cost = item.Cost;
			gd.ItemType = (int)item.ItemType;
			gd.Inner = item;
			return gd;
		}
	}
}
