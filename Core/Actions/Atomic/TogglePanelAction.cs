using Godot;

namespace Cthangover.Core.Actions.Atomic
{
    /// <summary>
    /// Toggles the visibility of any Control node in the scene by name.
    /// Uses ctx.Scene.Find for recursive tree search — panels can be nested
    /// arbitrarily deep. Unlike Show/Hide methods that use Widget lifecycle,
    /// this toggles Godot's native Visible property directly, so it works on
    /// non-Widget controls as well. The toggle is unconditional: if the panel
    /// doesn't exist, it logs an error rather than creating it.
    /// </summary>
    public class TogglePanelAction : IScenarioAction
    {
        /// <summary>
        /// Registered as "ui.toggle_panel" — toggles the visibility of
        /// any Control-derived node in the scene by name. Uses
        /// ctx.Scene.Find for recursive tree search, so panels can be
        /// nested arbitrarily deep. Toggles Godot's native Visible
        /// property directly (not Widget lifecycle methods), so it works
        /// on non-Widget controls. The toggle is unconditional — if the
        /// panel doesn't exist, it logs a warning rather than creating it.
        /// </summary>
        public string Name => "ui.toggle_panel";

        /// <summary>
        /// Reads the "name" variable, locates the Control node via
        /// recursive scene tree search, and flips its Visible property.
        /// Logs both the toggle result (new visibility state) and the
        /// not-found case. Returns early with a warning if "name" is
        /// missing.
        /// </summary>
        public void Run(IActionContext ctx)
        {
            var name = ctx.GetParam("name");

            if (string.IsNullOrEmpty(name))
            {
                ctx.Log("EVENT", "TogglePanelAction: missing 'name' variable");
                return;
            }

            var panel = ctx.Scene.Find<Control>(name);
            if (panel != null)
            {
                panel.Visible = !panel.Visible;
                ctx.Log("WIDGET", $"TogglePanelAction: toggled '{name}' visibility to {panel.Visible}");
            }
            else
            {
                ctx.Log("EVENT", $"TogglePanelAction: panel '{name}' not found");
            }
        }
    }
}
