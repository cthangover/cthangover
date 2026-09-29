using System.Collections.Generic;
using Cthangover.Core.Mods;
using Cthangover.Core.Mods.Resolvers;

namespace Cthangover.Core.Factories
{
	/// <summary>
	/// Lightweight JSON-driven factory. All data discovery is delegated to
	/// <see cref="EntityResolver"/>, which scans every mod file once and routes
	/// entities by their declared kind (read from <typeparamref name="T"/>'s
	/// <see cref="EntityKindAttribute"/>). The cache dictionary is built lazily
	/// on the first <c>Get</c> call — factories that are created early (e.g.
	/// during boot) but rarely accessed don't pay for parsing data they may
	/// never use.
	/// <para/>
	/// The collected dictionary is published only after the mod registry is
	/// finalized. A lookup made during the launcher boot window (the inventory
	/// autoload resolving starter items, for example) collects from a still
	/// empty registry; caching that empty result would outlive
	/// <c>ModManager.ClearCaches</c>, which flushes resolvers but not factory
	/// fields, and every later lookup would report the entity as missing.
	/// </summary>
	public abstract class FileFactory<T> : IFileFactory<T> where T : class, IIdentifiable
	{
		private Dictionary<string, T> cache;

		public T Get(string id)
		{
			if (string.IsNullOrEmpty(id))
				return null;

			var infos = cache ?? Entities.Collect<T>();

			if (ModManager.Instance.IsInitialized)
				cache ??= infos;

			infos.TryGetValue(id, out var item);
			return item;
		}
	}
}
