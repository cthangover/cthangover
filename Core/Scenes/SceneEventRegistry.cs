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

		public static void Register<T>(string sceneName, int priority = 0, string after = null, string condition = null) where T : class
		{
			Register(typeof(T), sceneName, priority, after, condition);
		}

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
