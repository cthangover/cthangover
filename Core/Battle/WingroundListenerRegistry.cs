using System;
using System.Collections.Generic;
using System.Reflection;
using Cthangover.Core.Utils;

namespace Cthangover.Core.Battle
{
	/// <summary>
	/// Singleton registry that collects <see cref="IWingroundListener"/>
	/// implementations (C# via reflection, GDScript via
	/// <see cref="RegisterListener"/>) and broadcasts winground
	/// lifecycle hooks to all of them in registration order.
	///
	/// Reflects the same pattern as
	/// <see cref="Relationship.RecruitBehaviourRegistry"/>: a
	/// static Instance, reflection-based <c>RegisterAssembly</c>,
	/// and manual <c>RegisterListener</c> for GDScript bridges.
	/// </summary>
	public class WingroundListenerRegistry
	{
		public static readonly WingroundListenerRegistry Instance = new();

		private readonly List<IWingroundListener> _listeners = new();

		/// <summary>
		/// Scans <paramref name="assembly"/> for concrete types that
		/// implement <see cref="IWingroundListener"/> and instantiates
		/// them via their parameterless constructor. Called from
		/// <see cref="Mods.ModAssemblyLoader.RegisterAssembly"/>.
		/// </summary>
		public void RegisterAssembly(Assembly assembly)
		{
			foreach (var instance in Reflections.FindAndCreate<IWingroundListener>(assembly))
			{
				try
				{
					_listeners.Add(instance);
					GameLogger.Log("BATTLE",
						$"WingroundListenerRegistry: registered C# listener '{instance.Id}' from assembly '{assembly.GetName().Name}'");
				}
				catch (Exception ex)
				{
					GameLogger.Log("BATTLE",
						$"WingroundListenerRegistry: failed to create listener '{instance.GetType().FullName}': {ex.Message}",
						LogLevel.Error);
				}
			}
		}

		/// <summary>
		/// Manually registers a listener (used by the GDScript bridge
		/// <see cref="GDWingroundListener"/>). Safe to call from any
		/// thread — the underlying list is not thread-safe but all
		/// registrations happen during mod initialisation before
		/// battle scenes are loaded.
		/// </summary>
		public void RegisterListener(IWingroundListener listener)
		{
			if (listener == null)
				return;

			_listeners.Add(listener);
			GameLogger.Log("BATTLE",
				$"WingroundListenerRegistry: registered listener '{listener.Id}'");
		}

		/// <summary>
		/// Broadcasts <see cref="IWingroundListener.OnBeforeExpAward"/>
		/// to every registered listener. Exceptions from individual
		/// listeners are logged and swallowed so one broken mod cannot
		/// break the victory screen for others.
		/// </summary>
		public void NotifyBeforeExpAward(WingroundContext ctx)
		{
			foreach (var listener in _listeners)
			{
				try
				{
					listener.OnBeforeExpAward(ctx);
				}
				catch (Exception ex)
				{
					GameLogger.Log("BATTLE",
						$"WingroundListenerRegistry: {listener.Id}.OnBeforeExpAward threw: {ex.Message}",
						LogLevel.Error);
				}
			}
		}

		/// <summary>
		/// Broadcasts <see cref="IWingroundListener.OnAfterLootGenerated"/>
		/// to every registered listener.
		/// </summary>
		public void NotifyAfterLootGenerated(WingroundContext ctx)
		{
			foreach (var listener in _listeners)
			{
				try
				{
					listener.OnAfterLootGenerated(ctx);
				}
				catch (Exception ex)
				{
					GameLogger.Log("BATTLE",
						$"WingroundListenerRegistry: {listener.Id}.OnAfterLootGenerated threw: {ex.Message}",
						LogLevel.Error);
				}
			}
		}

		/// <summary>
		/// Broadcasts <see cref="IWingroundListener.OnBeforeShow"/>
		/// to every registered listener.
		/// </summary>
		public void NotifyBeforeShow(WingroundContext ctx)
		{
			foreach (var listener in _listeners)
			{
				try
				{
					listener.OnBeforeShow(ctx);
				}
				catch (Exception ex)
				{
					GameLogger.Log("BATTLE",
						$"WingroundListenerRegistry: {listener.Id}.OnBeforeShow threw: {ex.Message}",
						LogLevel.Error);
				}
			}
		}
	}
}
