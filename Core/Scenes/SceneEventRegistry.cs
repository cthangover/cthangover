using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Cthangover.Core.UI.Executable;
using Cthangover.Core.Utils;

namespace Cthangover.Core.Scenes
{
	public static class SceneEventRegistry
	{
		private static readonly Dictionary<string, List<SceneEventInfo>> events = new();
		private static bool initialized;

		/// <summary>
		/// Scans all loaded assemblies for <see cref="ExecutableEvent"/> types decorated
		/// with <see cref="SceneEventAttribute"/> and registers them in the event table.
		/// Uses double-checked locking for thread safety. Subsequent calls are no-ops.
		/// </summary>
		public static void Initialize()
		{
			if (initialized)
				return;

			lock (events)
			{
				if (initialized)
					return;

				RegisterFromAttributes();
				initialized = true;
			}
		}

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
		{
			Register(typeof(T), sceneName, priority, after, condition);
		}

		/// <summary>
		/// Non-generic variant of <see cref="Register{T}"/> that accepts a
		/// <see cref="Type"/> directly.
		/// </summary>
		/// <param name="type">The <see cref="ExecutableEvent"/> type to register.</param>
		/// <param name="sceneName">The scene identifier to associate the event with.</param>
		/// <param name="priority">Execution priority (lower = earlier). Default 0.</param>
		/// <param name="after">Identifier of an event that must precede this one.</param>
		/// <param name="condition">Condition expression string evaluated at runtime.</param>
		public static void Register(Type type, string sceneName, int priority = 0, string after = null, string condition = null)
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

		/// <summary>
		/// Returns all registered <see cref="SceneEventInfo"/> entries for the given
		/// scene name, sorted topologically by priority and <c>After</c> dependencies.
		/// Ensures <see cref="Initialize"/> has been called first.
		/// </summary>
		/// <param name="sceneName">The scene identifier to retrieve events for.</param>
		public static List<SceneEventInfo> GetEvents(string sceneName)
		{
			Initialize();

			lock (events)
			{
				if (!events.TryGetValue(sceneName, out var list))
					return new List<SceneEventInfo>();

				return SortEvents(list).ToList();
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

        private static void RegisterFromAttributes()
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                RegisterFromAttributes(assembly);
        }

        /// <summary>
        /// Scans a specific assembly for <see cref="SceneEventAttribute"/>-decorated
        /// <see cref="ExecutableEvent"/> types and registers them. Thread-safe.
        /// Useful for registering events from dynamically loaded mod assemblies
        /// after initial startup.
        /// </summary>
        /// <param name="assembly">The assembly to scan for event types.</param>
        public static void RegisterAssembly(Assembly assembly)
        {
            lock (events)
            {
                RegisterFromAttributes(assembly);
            }
        }

        private static void RegisterFromAttributes(Assembly assembly)
        {
            foreach (var type in assembly.GetTypes())
            {
                var attr = type.GetCustomAttribute<SceneEventAttribute>();
                if (attr == null)
                    continue;

                if (!typeof(ExecutableEvent).IsAssignableFrom(type))
                    continue;

                Register(type, attr.SceneName, attr.Priority, attr.After, attr.Condition);
            }
        }
	}
}
