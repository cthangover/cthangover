using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Cthangover.Core.Utils;

namespace Cthangover.Core.UI.Tool
{
    /// <summary>
    /// Singleton registry for modding/dev tools (IToolProvider implementations).
    /// Discovers tools via reflection on construction; supports RegisterAssembly
    /// for mod-injected tools. Deduplicates by Id. Get/GetAll provide lookup
    /// for the tools dropdown in MainMenu.
    /// </summary>
    public class ToolFactory
    {
        /// <summary>Singleton instance auto-constructed at field init time, triggering reflection-based tool discovery.</summary>
        public static readonly ToolFactory Instance = new();

        private readonly Dictionary<string, IToolProvider> _tools = new();

        private ToolFactory()
        {
            foreach (var tool in Reflections.FindAndCreate<IToolProvider>())
                Register(tool);

            GameLogger.Log("TOOLS", $"ToolFactory: loaded {_tools.Count} tool(s)");
        }

        /// <summary>
        /// Scans all types in the given assembly for <see cref="IToolProvider"/> implementations,
        /// instantiates them via <c>Activator.CreateInstance</c>, and registers each one.
        /// Primary hook for mods to register their own tools at runtime.
        /// </summary>
        public void RegisterAssembly(Assembly assembly)
        {
            foreach (var type in assembly.GetTypes())
            {
                if (typeof(IToolProvider).IsAssignableFrom(type) && !type.IsAbstract)
                {
                    try
                    {
                        var tool = (IToolProvider)Activator.CreateInstance(type);
                        Register(tool);
                    }
                    catch (Exception ex)
                    {
                        GameLogger.Log("TOOLS", $"ToolFactory: failed to create instance of '{type.FullName}': {ex.Message}", LogLevel.Error);
                    }
                }
            }
        }

        /// <summary>
        /// Registers a tool, deduplicating by <see cref="IToolProvider.Id"/>. If a tool with the
        /// same Id was already registered, the new one is silently discarded — first-come-wins.
        /// </summary>
        public void Register(IToolProvider tool)
        {
            if (_tools.ContainsKey(tool.Id))
                return;

            _tools[tool.Id] = tool;
            GameLogger.Log("TOOLS", $"ToolFactory: registered '{tool.Id}'");
        }

        /// <summary>
        /// Looks up a tool by its <see cref="IToolProvider.Id"/>. Returns <c>null</c> if not found.
        /// </summary>
        public IToolProvider Get(string id)
        {
            _tools.TryGetValue(id, out var tool);
            return tool;
        }

        /// <summary>
        /// Returns all registered tools as a read-only list. Used by <see cref="MainMenu"/> to
        /// populate the Tools dropdown.
        /// </summary>
        public IReadOnlyList<IToolProvider> GetAll()
        {
            return _tools.Values.ToList();
        }
    }
}
