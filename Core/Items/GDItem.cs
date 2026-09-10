using Godot;

namespace Cthangover.Core.Items
{
	[GlobalClass]
	public partial class GDItem : RefCounted
	{
		public string Id;
		public string Name;
		public string Description;
		public int Cost;
		public int ItemType;

		internal IItem Inner;

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
