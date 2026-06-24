using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Cthangover.Core.Utils;

namespace Cthangover.Core.UI.Tool
{
    public class ToolBoxButtonFactory
    {
        public static readonly ToolBoxButtonFactory Instance = new();

        private readonly List<IToolBoxButton> _buttons = new();

        private ToolBoxButtonFactory()
        {
            foreach (var button in Reflections.FindAndCreate<IToolBoxButton>())
                Register(button);

            GameLogger.Log("TOOLS", $"ToolBoxButtonFactory: loaded {_buttons.Count} button(s)");
        }

        public void RegisterAssembly(Assembly assembly)
        {
            foreach (var type in assembly.GetTypes())
            {
                if (typeof(IToolBoxButton).IsAssignableFrom(type) && !type.IsAbstract)
                {
                    try
                    {
                        var button = (IToolBoxButton)Activator.CreateInstance(type);
                        Register(button);
                    }
                    catch { }
                }
            }
        }

        public void Register(IToolBoxButton button)
        {
            _buttons.Add(button);
            GameLogger.Log("TOOLS", $"ToolBoxButtonFactory: registered button for '{button.ToolId}'");
        }

        public IReadOnlyList<IToolBoxButton> GetVisible()
        {
            return _buttons.Where(b => b.IsVisible()).ToList();
        }
    }
}
