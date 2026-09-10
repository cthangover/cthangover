using Godot;

namespace Cthangover.Core.Items
{
	public class GDItemAction : IItemAction
	{
		private readonly GodotObject _instance;

		public GDItemAction(string id, GodotObject instance)
		{
			ID = id;
			_instance = instance;
		}

		public string ID { get; }

		public bool UseAction(IItem item)
		{
			var gdItem = GDItem.From(item);
			return _instance.Call("use", gdItem).AsBool();
		}
	}
}
