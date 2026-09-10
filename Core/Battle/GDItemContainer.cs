using Godot;

namespace Cthangover.Core.Battle
{
	/// <summary>
	/// GDScript-compatible DTO for a single loot entry in the
	/// victory-screen loot grid. Mirrors the shape of a
	/// <see cref="Items.ItemContainer"/> without exposing the full
	/// <see cref="Items.IItem"/> object — GDScript only needs the
	/// item factory ID and stack count to display and reason about
	/// loot.
	/// </summary>
	[GlobalClass]
	public partial class GDItemContainer : RefCounted
	{
		/// <summary>
		/// Factory ID of the item, matching the key in
		/// <c>items.json</c> (e.g. <c>"gold_coin"</c>,
		/// <c>"health_potion"</c>).
		/// </summary>
		public string ItemId;

		/// <summary>Stack count for this loot entry.</summary>
		public int Count;
	}
}
