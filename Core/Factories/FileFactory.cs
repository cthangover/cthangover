using Cthangover.Core.Mods.Resolvers;

namespace Cthangover.Core.Factories
{
	/// <summary>
	/// Lightweight JSON-driven factory. All data discovery is delegated to
	/// <see cref="EntityResolver"/>, which scans every mod file once and routes
	/// entities by their declared kind (read from <typeparamref name="T"/>'s
	/// <see cref="EntityKindAttribute"/>). Nothing is cached here: the
	/// resolver keeps the collected dictionary until the next mod reload, so
	/// every lookup sees the data of the running mod set — also from a factory
	/// whose mod stays loaded across a reload.
	/// </summary>
	public abstract class FileFactory<T> : IFileFactory<T> where T : class, IIdentifiable
	{

		/// <summary>
		/// Returns the entity registered under <paramref name="id"/>, collecting
		/// the kind-routed dictionary from the entity resolver on first use.
		/// Unknown or empty ids return <c>null</c>.
		/// </summary>
		public T Get(string id)
		{
			if (string.IsNullOrEmpty(id))
				return null;

			// Entities.Collect is cached by the entity resolver until the next mod
			// reload; holding a copy here kept serving the previous mod set's data
			// to factories of mods that stay loaded across a reload.
			Entities.Collect<T>().TryGetValue(id, out var item);
			return item;
		}
	}
}
