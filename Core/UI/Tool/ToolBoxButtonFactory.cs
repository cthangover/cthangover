using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Cthangover.Core.Bootstrap;
using Cthangover.Core.Utils;

namespace Cthangover.Core.UI.Tool
{
    /// <summary>
    /// Registry for dynamically-discovered toolbar button definitions.
    /// Scans assemblies via Reflections.FindAndCreate on construction, loading
    /// all IToolBoxButton implementations. Supports RegisterAssembly for mods
    /// to inject their own buttons at runtime. GetVisible() filters by each
    /// button's IsVisible() method, so buttons can conditionally hide.
    /// <para/>
    /// The single instance is owned by the <see cref="AppServices"/>
    /// composition root; the static facade stays as the public API during
    /// migration. Initialization moves from eager (static field) to lazy
    /// (first access through the owning <c>Lazy</c> slot).
    /// </summary>
    public class ToolBoxButtonFactory
    {
        /// <summary>Facade over the AppServices-owned instance; kept as the public API during migration.</summary>
        public static ToolBoxButtonFactory Instance => AppServices.Current.ToolBoxButtons.Value;

        private readonly List<IToolBoxButton> _buttons = new();
        private readonly HashSet<Type> _buttonTypes = new();

        internal ToolBoxButtonFactory()
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
            ICollection<IToolBoxButton> buttons;
            try
            {
                buttons = Reflections.FindAndCreate<IToolBoxButton>(assembly);
            }
            catch (Exception ex)
            {
                GameLogger.Log("TOOLS", $"ToolBoxButtonFactory: failed to scan assembly '{assembly.GetName().Name}': {ex.Message}", LogLevel.Error);
                return;
            }

            foreach (var button in buttons)
                Register(button);
        }

        /// <summary>
        /// Adds a button definition to the registry. Buttons of the same concrete type are
        /// deduplicated (the factory may see an assembly both through the initial AppDomain
        /// scan and through <see cref="RegisterAssembly"/>). Different button types targeting
        /// the same <see cref="IToolBoxButton.ToolId"/> are still allowed because they may
        /// have different visibility rules.
        /// </summary>
        public void Register(IToolBoxButton button)
        {
            if (!_buttonTypes.Add(button.GetType()))
                return;

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
