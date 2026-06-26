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
        public static readonly ToolFactory Instance = new();

        private readonly Dictionary<string, IToolProvider> _tools = new();

        private ToolFactory()
        {
            foreach (var tool in Reflections.FindAndCreate<IToolProvider>())
                Register(tool);

            GameLogger.Log("TOOLS", $"ToolFactory: loaded {_tools.Count} tool(s)");
        }

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

        public void Register(IToolProvider tool)
        {
            if (_tools.ContainsKey(tool.Id))
                return;

            _tools[tool.Id] = tool;
            GameLogger.Log("TOOLS", $"ToolFactory: registered '{tool.Id}'");
        }

        public IToolProvider Get(string id)
        {
            _tools.TryGetValue(id, out var tool);
            return tool;
        }

        public IReadOnlyList<IToolProvider> GetAll()
        {
            return _tools.Values.ToList();
        }
    }
}
