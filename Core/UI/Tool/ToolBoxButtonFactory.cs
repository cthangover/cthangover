using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Cthangover.Core.Utils;

namespace Cthangover.Core.UI.Tool
{
    /// <summary>
    /// Singleton registry for dynamically-discovered toolbar button definitions.
    /// Scans assemblies via Reflections.FindAndCreate on construction, loading
    /// all IToolBoxButton implementations. Supports RegisterAssembly for mods
    /// to inject their own buttons at runtime. GetVisible() filters by each
    /// button's IsVisible() method, so buttons can conditionally hide.
    /// </summary>
    public class ToolBoxButtonFactory
    {
        /// <summary>Singleton instance auto-constructed at field init time, triggering reflection-based discovery.</summary>
        public static readonly ToolBoxButtonFactory Instance = new();

        private readonly List<IToolBoxButton> _buttons = new();

        private ToolBoxButtonFactory()
        {
            foreach (var button in Reflections.FindAndCreate<IToolBoxButton>())
                Register(button);

            GameLogger.Log("TOOLS", $"ToolBoxButtonFactory: loaded {_buttons.Count} button(s)");
        }

        /// <summary>
        /// Scans all types in the given assembly for <see cref="IToolBoxButton"/> implementations,
        /// instantiates them via <c>Activator.CreateInstance</c>, and registers each one.
        /// Primary hook for mods to inject their own toolbar buttons at runtime.
        /// </summary>
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
                    catch (Exception ex)
                    {
                        GameLogger.Log("TOOLS", $"ToolBoxButtonFactory: failed to create instance of '{type.FullName}': {ex.Message}", LogLevel.Error);
                    }
                }
            }
        }

        /// <summary>
        /// Adds a button definition to the registry. Safe to call multiple times — duplicates
        /// are not filtered because buttons targeting the same <see cref="IToolBoxButton.ToolId"/>
        /// may have different visibility rules.
        /// </summary>
        public void Register(IToolBoxButton button)
        {
            _buttons.Add(button);
            GameLogger.Log("TOOLS", $"ToolBoxButtonFactory: registered button for '{button.ToolId}'");
        }

        /// <summary>
        /// Returns all registered buttons that currently pass <see cref="IToolBoxButton.IsVisible"/>.
        /// Used by <see cref="ToolBox.AddToolButtons"/> to dynamically populate the toolbar HUD.
        /// </summary>
        public IReadOnlyList<IToolBoxButton> GetVisible()
        {
            return _buttons.Where(b => b.IsVisible()).ToList();
        }
    }
}
