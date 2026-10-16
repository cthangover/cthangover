using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Cthangover.Core.Bootstrap;
using Cthangover.Core.UI.Executable;
using Cthangover.Core.Utils;

namespace Cthangover.Core.Scenes
{
	/// <summary>
	/// Mod-derived registry of executable scene events: <see cref="ExecutableEvent"/>
	/// types discovered through <see cref="SceneEventAttribute"/> plus events
	/// registered imperatively by mod initializers after the build. The registry
	/// instance lives in <see cref="AppServices.Current"/>; the static members
	/// below are a facade delegating to it, so existing callers keep working
	/// unchanged both before and after a rebuild.
	/// </summary>
	public class SceneEventRegistry : IGameRegistry
	{
		private readonly Dictionary<string, List<SceneEventInfo>> events = new();
		private bool built;

		/// <summary>
		/// Factory for scenario-backed executable events, published by the
		/// dialog_base engine mod on load. SceneManager builds inline scenario
		/// events through it; null until the dialog mod registers its factory.
		/// </summary>
		public IScenarioEventFactory ScenarioEventFactory { get; set; }

		/// <summary>Shared instance the static facade delegates to.</summary>
		private static SceneEventRegistry Owned => AppServices.Current.SceneEvents;

		/// <summary>
		/// Creates an empty registry: cheap and side-effect free per the
		/// IGameRegistry contract. <see cref="Rebuild"/> fills the data.
		/// </summary>
		internal SceneEventRegistry()
		{
		}

		/// <inheritdoc />
		public string Id => "scene_events";

		/// <summary>
		/// Re-scans all loaded assemblies for <see cref="ExecutableEvent"/> types
		/// decorated with <see cref="SceneEventAttribute"/>, replacing the previous
		/// table. Forced: safe to call repeatedly. Events registered imperatively
		/// (by mod initializers) must re-register afterwards.
		/// </summary>
		public void Rebuild()
		{
			lock (events)
			{
				events.Clear();
				RegisterFromAttributes();
				built = true;
			}
		}

		/// <summary>
		/// Drops all registered events; the next build re-scans the assemblies.
		/// Called on mod reload through AppServices.ClearRegistries.
		/// </summary>
		public void Clear()
		{
			lock (events)
			{
				events.Clear();
				built = false;
			}
		}

		/// <summary>
		/// Scans all loaded assemblies for <see cref="ExecutableEvent"/> types decorated
		/// with <see cref="SceneEventAttribute"/> and registers them in the event table.
		/// Uses double-checked locking for thread safety; keeps the old Initialize
		/// idempotence — subsequent calls are no-ops.
		/// </summary>
		public void EnsureBuilt()
		{
			if (built)
				return;

			lock (events)
			{
				if (built)
					return;

				RegisterFromAttributes();
				built = true;
			}
		}

		// ---- Static facade (legacy API, unchanged for callers) ------------

		/// <summary>Legacy entry point; delegates to the shared instance.</summary>
		public static void Initialize() => Owned.EnsureBuilt();

		/// <summary>
		/// Registers a specific <see cref="ExecutableEvent"/> subclass for a scene with
		/// optional priority, dependency, and condition parameters.
		/// </summary>
		/// <typeparam name="T">The <see cref="ExecutableEvent"/> subclass to register.</typeparam>
		/// <param name="sceneName">The scene identifier to associate the event with.</param>
		/// <param name="priority">Execution priority (lower = earlier). Default 0.</param>
		/// <param name="after">Identifier of an event that must precede this one.</param>
		/// <param name="condition">Condition expression string evaluated at runtime.</param>
		public static void Register<T>(string sceneName, int priority = 0, string after = null, string condition = null) where T : class
			=> Owned.AddEvent(typeof(T), sceneName, priority, after, condition);

		/// <summary>
		/// Non-generic variant of <see cref="Register{T}"/> that accepts a
		/// <see cref="Type"/> directly. Works before and after the build: a
		/// registration made after Initialize simply joins the table.
		/// </summary>
		/// <param name="type">The <see cref="ExecutableEvent"/> type to register.</param>
		/// <param name="sceneName">The scene identifier to associate the event with.</param>
		/// <param name="priority">Execution priority (lower = earlier). Default 0.</param>
		/// <param name="after">Identifier of an event that must precede this one.</param>
		/// <param name="condition">Condition expression string evaluated at runtime.</param>
		public static void Register(Type type, string sceneName, int priority = 0, string after = null, string condition = null)
			=> Owned.AddEvent(type, sceneName, priority, after, condition);

		/// <summary>
		/// Returns all registered <see cref="SceneEventInfo"/> entries for the given
		/// scene name, sorted topologically by priority and <c>After</c> dependencies.
		/// Ensures the registry has been built first.
		/// </summary>
		/// <param name="sceneName">The scene identifier to retrieve events for.</param>
		public static List<SceneEventInfo> GetEvents(string sceneName)
			=> Owned.GetEventsSorted(sceneName);

		/// <summary>
		/// Scans a specific assembly for <see cref="SceneEventAttribute"/>-decorated
		/// <see cref="ExecutableEvent"/> types and registers them. Thread-safe.
		/// Useful for registering events from dynamically loaded mod assemblies
		/// after initial startup.
		/// </summary>
		/// <param name="assembly">The assembly to scan for event types.</param>
		public static void RegisterAssembly(Assembly assembly)
			=> Owned.AddFromAssembly(assembly);

		/// <summary>Unload pass: drops events contributed by the given assembly. Delegates to the shared instance.</summary>
		public static void UnregisterAssembly(Assembly assembly, string modId)
			=> Owned.RemoveFromAssembly(assembly, modId);

		// ---- Instance implementation --------------------------------------

		/// <summary>Direct instance access for core consumers migrating off the static facade.</summary>
		public void AddEvent(Type type, string sceneName, int priority, string after, string condition)
		{
			lock (events)
			{
				if (!events.TryGetValue(sceneName, out var list))
				{
					list = new List<SceneEventInfo>();
					events[sceneName] = list;
				}

				list.Add(new SceneEventInfo
				{
					Id = type.FullName,
					ClassName = type.FullName,
					Priority = priority,
					After = after,
					Condition = condition,
				});
			}
		}

		/// <summary>Direct instance access for core consumers migrating off the static facade.</summary>
		public List<SceneEventInfo> GetEventsSorted(string sceneName)
		{
			EnsureBuilt();

			lock (events)
			{
				if (!events.TryGetValue(sceneName, out var list))
					return new List<SceneEventInfo>();

				return SortEvents(list).ToList();
			}
		}

		/// <summary>Direct instance access for core consumers migrating off the static facade.</summary>
	public void AddFromAssembly(Assembly assembly)
		{
			lock (events)
			{
				RegisterFromAttributes(assembly);
			}
		}

		/// <summary>
		/// Removes events whose implementation class comes from the given
		/// assembly — the unload pass of the assembly consumer pipeline. The
		/// event table stores type names, so the reverse pass resolves each
		/// entry back to its type and drops the ones the unloading assembly
		/// contributed; scenario-file events are never touched here.
		/// </summary>
		public void RemoveFromAssembly(Assembly assembly, string modId)
		{
			var assemblyName = assembly.GetName().Name;
			lock (events)
			{
				foreach (var key in events.Keys.ToList())
				{
					events[key].RemoveAll(info =>
					{
						if (string.IsNullOrEmpty(info.ClassName))
							return false;
						var type = Type.GetType(info.ClassName, false) ?? FindType(info.ClassName);
						return type != null && type.Assembly.GetName().Name == assemblyName;
					});
					if (events[key].Count == 0)
						events.Remove(key);
				}
			}
		}

		private static Type FindType(string fullName)
		{
			foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
			{
				try
				{
					var type = assembly.GetType(fullName, false);
					if (type != null)
						return type;
				}
				catch
				{
					// unload window: assembly partially collected, skip
				}
			}
			return null;
		}

		private void RegisterFromAttributes()
		{
			foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
				RegisterFromAttributes(assembly);
		}

		private void RegisterFromAttributes(Assembly assembly)
		{
			Type[] types;
			try
			{
				types = assembly.GetTypes();
			}
			catch (ReflectionTypeLoadException ex)
			{
				// A collectible mod assembly can be mid-unload while a rebuild
				// scans the AppDomain; skip it the same way Reflections does.
				types = ex.Types.Where(t => t != null).ToArray();
			}
			catch (Exception ex)
			{
				GameLogger.Log("SCENE", $"SceneEventRegistry: cannot enumerate types of '{assembly.GetName().Name}': {ex.Message}", LogLevel.Warning);
				return;
			}

			foreach (var type in types)
			{
				var attr = type.GetCustomAttribute<SceneEventAttribute>();
				if (attr == null)
					continue;

				if (!typeof(IExecutableEvent).IsAssignableFrom(type))
					continue;

				AddEvent(type, attr.SceneName, attr.Priority, attr.After, attr.Condition);
			}
		}

		private static IEnumerable<SceneEventInfo> SortEvents(List<SceneEventInfo> list)
		{
			var visited = new HashSet<string>();
			var result = new List<SceneEventInfo>();
			var pending = new HashSet<string>();

			void Visit(SceneEventInfo info)
			{
				if (info.Id != null && visited.Contains(info.Id))
					return;

				if (info.Id != null && pending.Contains(info.Id))
				{
					GameLogger.Log("SCENE", $"Circular dependency detected: {info.Id}", LogLevel.Warning);
					return;
				}

				if (!string.IsNullOrEmpty(info.After))
				{
					var after = list.FirstOrDefault(e => e.Id == info.After);
					if (after != null)
					{
						pending.Add(info.Id);
						Visit(after);
						pending.Remove(info.Id);
					}
				}

				if (info.Id != null && visited.Add(info.Id))
					result.Add(info);
				else if (info.Id == null && !result.Contains(info))
					result.Add(info);
			}

			foreach (var info in list.OrderBy(e => e.Priority).ThenBy(e => e.Id))
				Visit(info);

			return result;
		}
	}
}
