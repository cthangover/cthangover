using Godot;

namespace Cthangover.Core.Items
{
	/// <summary>
	/// Adapter that exposes a GDScript object as an <see cref="IItemAction"/>.
	/// A mod script that provides <c>get_item_action_id()</c> is registered under
	/// that id (the bridge constructs this class); <see cref="UseAction"/> then
	/// marshals the managed item into a <see cref="GDItem"/> snapshot and calls
	/// the script's <c>use(item)</c>, whose return value becomes the success
	/// flag (returning <c>false</c> keeps the item unconsumed).
	/// </summary>
	public class GDItemAction : IItemAction
	{
		private readonly GodotObject _instance;

		/// <summary>
		/// Binds the adapter to a live GDScript object. The caller (item action
		/// registration from the GDScript bridge) supplies the id the script
		/// reported via <c>get_item_action_id()</c>.
		/// </summary>
		public GDItemAction(string id, GodotObject instance)
		{
			ID = id;
			_instance = instance;
		}

		/// <summary>Registry key matching the item JSON's <c>"ItemAction"</c> field.</summary>
		public string ID { get; }

		/// <summary>Calls the script's <c>use(item)</c> with a GDScript-safe item snapshot; the bool result is the consumption flag.</summary>
		public bool UseAction(IItem item)
		{
			var gdItem = GDItem.From(item);
			return _instance.Call("use", gdItem).AsBool();
		}
	}
}
