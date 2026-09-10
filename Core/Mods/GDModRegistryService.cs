using Godot;

namespace Cthangover.Core.Mods
{
	[GlobalClass]
	public partial class GDModRegistryService : RefCounted
	{
		internal IModRegistry Inner;

		public bool IsLoaded(string modId) => Inner.Mods.ContainsKey(modId);

		public string[] GetOrderedModIds()
		{
			var list = Inner.GetOrderedModIds();
			var result = new string[list.Count];
			for (var i = 0; i < list.Count; i++)
				result[i] = list[i];
			return result;
		}
	}
}
