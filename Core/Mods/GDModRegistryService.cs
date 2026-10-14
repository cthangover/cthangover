using Godot;

namespace Cthangover.Core.Mods
{
	/// <summary>
	/// Godot-facing bridge over <see cref="IModRegistry"/> for GDScript mods:
	/// exposed as a <c>[GlobalClass]</c> so scripts can introspect the loaded
	/// mod set without any C# — e.g. to check whether an optional companion
	/// mod is present before registering cross-mod behaviour.
	/// </summary>
	[GlobalClass]
	public partial class GDModRegistryService : RefCounted
	{
		/// <summary>Registry this service delegates to; assigned by the loader.</summary>
		internal IModRegistry Inner;

		/// <summary>Returns <c>true</c> when a mod with the given id is currently loaded and enabled.</summary>
		public bool IsLoaded(string modId) => Inner.Mods.ContainsKey(modId);

		/// <summary>Returns loaded mod ids in load order (dependencies first).</summary>
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
