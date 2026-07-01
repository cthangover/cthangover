using System;
using Cthangover.Core.Interactive;
using Cthangover.Core.Mods;
using Cthangover.Core.Utils;

namespace Cthangover.Core.Factories.Impls
{
	/// <summary>
	/// Factory for <c>InteractiveDefinition</c> lookups. Wraps
	/// <c>ModManager.CollectInteractives()</c> with a thread-safe singleton
	/// accessor. Since the underlying collection is already cached by
	/// <c>ModManager</c>, this factory is a thin forwarding layer that
	/// provides the same <c>Instance.Get(id)</c> API convention as every
	/// other factory in the project.
	/// </summary>
	public class InteractiveFactory
	{
		private static readonly Lazy<InteractiveFactory> _instance = new(() => new InteractiveFactory());

		private InteractiveFactory()
		{
		}

		/// <summary>Thread-safe singleton instance.</summary>
		public static InteractiveFactory Instance => _instance.Value;

		/// <summary>
		/// Looks up an interactive definition by its unique ID.
		/// Returns <c>null</c> if no definition with that ID exists
		/// in any mod.
		/// </summary>
		/// <param name="id">The <c>ID</c> from the definition JSON.</param>
		public InteractiveDefinition Get(string id)
		{
			if (string.IsNullOrEmpty(id))
				return null;

			var dict = ModManager.Instance.CollectInteractives();
			if (dict.TryGetValue(id, out var def))
				return def;

			GameLogger.Log("FACTORY", $"InteractiveFactory: definition '{id}' not found", LogLevel.Error);
			return null;
		}
	}
}
