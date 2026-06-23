using System.Collections.Generic;
using Cthangover.Core.Mods;
using Cthangover.Core.Utils;

namespace Cthangover.Core.Factories
{
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
