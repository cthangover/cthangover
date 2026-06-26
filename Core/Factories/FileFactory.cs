using System.Collections.Generic;
using Cthangover.Core.Mods;
using Cthangover.Core.Utils;

namespace Cthangover.Core.Factories
{
	/// <summary>
	/// Lightweight JSON-driven factory that delegates all mod file discovery
	/// to <c>ModManager.CollectJsonGroup</c>. The cache dictionary is built
	/// lazily on the first <c>Get</c> call — by deferring population until
	/// the resource is actually needed, factories that are created early
	/// (e.g. during boot) but rarely accessed don't waste startup time or
	/// memory parsing JSON that may never be used.
	/// </summary>
	public abstract class FileFactory<T> : IFileFactory<T> where T : class, IIdentifiable
	{
		private Dictionary<string, T> cache;

		public abstract string GroupName { get; }

		public T Get(string id)
		{
			if (string.IsNullOrEmpty(id))
				return null;

			if (cache == null)
				cache = ModManager.Instance.CollectJsonGroup<T>(GroupName);

			cache.TryGetValue(id, out var item);
			return item;
		}
	}
}
