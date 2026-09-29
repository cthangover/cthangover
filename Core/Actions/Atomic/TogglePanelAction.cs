using Cthangover.Core.UI;
using Godot;

namespace Cthangover.Core.Actions.Atomic;

/// <summary>
///     Toggles the visibility of any Control node in the scene by name.
///     Uses ctx.Scene.Find for recursive tree search — panels can be nested
///     arbitrarily deep. When the name is not found in the scene, the action
///     falls back to <see cref="UiRegistry.Toggle"/> so host-built panels
///     (e.g. the world map, live under UiHost's CanvasLayer rather than the
///     scene tree) can be opened from scenarios. Unlike Show/Hide methods that
///     use Widget lifecycle, scene controls are toggled through Godot's native
///     Visible property directly, so this works on non-Widget controls as well.
/// </summary>
public class TogglePanelAction : IScenarioAction
{
    /// <summary>
    ///     Registered as "ui.toggle_panel" — toggles the visibility of
    ///     any Control-derived node in the scene by name; if no scene node
    ///     matches, the name is treated as a mod UI entry id and toggled
    ///     through the UI registry. The toggle is unconditional — if neither
    ///     exists, it logs a warning rather than creating anything.
    /// </summary>
    public string Name => "ui.toggle_panel";

    /// <summary>
    ///     Reads the "name" variable, locates the Control node via
    ///     recursive scene tree search, and flips its Visible property.
    ///     Falls back to UiRegistry.Toggle(id) for hosted panels. Logs both
    ///     the toggle result (new visibility state) and the not-found case.
    ///     Returns early with a warning if "name" is missing.
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
            return;
        }

        if (UiRegistry.Get(name) != null)
        {
            UiRegistry.Toggle(name);
            ctx.Log("WIDGET", $"TogglePanelAction: toggled hosted panel '{name}'");
            return;
        }

        ctx.Log("EVENT", $"TogglePanelAction: panel '{name}' not found");
    }
}